using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared;

namespace IngestionService.Services;

public sealed class SensorRegistryService(
    SensorSystemDbContext dbContext,
    IOptions<SensorSystemOptions> sensorOptions,
    NotificationApiClient notifications,
    ILogger<SensorRegistryService> logger)
{
    public async Task<SensorRegistrationResponse> RegisterAsync(SensorRegistrationRequest request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var sensor = await dbContext.Sensors.SingleOrDefaultAsync(x => x.SensorId == request.SensorId, cancellationToken);

        if (sensor is not null &&
            (sensor.DataQuality == DataQuality.BAD || sensor.Status is SensorStatus.Bad or SensorStatus.DosBlocked))
        {
            return Reject("SensorId is marked as BAD and cannot be reused.");
        }

        if (sensor is not null && !string.Equals(sensor.PublicKeyPem.Trim(), request.PublicKeyPem.Trim(), StringComparison.Ordinal))
        {
            return Reject("SensorId already exists with a different public key.");
        }

        var activeCount = await dbContext.Sensors.CountAsync(x => x.Status == SensorStatus.Active, cancellationToken);
        var desiredStatus = ResolveRegistrationStatus(sensor, request.InitialDataQuality, activeCount);

        if (sensor is null)
        {
            sensor = new Sensor
            {
                SensorId = request.SensorId,
                PublicKeyPem = request.PublicKeyPem,
                RegisteredAt = now
            };
            dbContext.Sensors.Add(sensor);
        }

        sensor.PublicKeyPem = request.PublicKeyPem;
        sensor.DataQuality = request.InitialDataQuality;
        sensor.Status = desiredStatus;
        sensor.DosBlockedUntil = null;
        sensor.InactiveBlockedAt = null;
        sensor.ConnectionId = null;
        sensor.TemperatureMin = request.TemperatureGeneration.Min;
        sensor.TemperatureMax = request.TemperatureGeneration.Max;
        sensor.Priority1Low = request.AlarmThresholds.Priority1Low;
        sensor.Priority1High = request.AlarmThresholds.Priority1High;
        sensor.Priority2Low = request.AlarmThresholds.Priority2Low;
        sensor.Priority2High = request.AlarmThresholds.Priority2High;
        sensor.Priority3Low = request.AlarmThresholds.Priority3Low;
        sensor.Priority3High = request.AlarmThresholds.Priority3High;
        sensor.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Registered sensor {SensorId} with status {Status}.", sensor.SensorId, sensor.Status);

        return new SensorRegistrationResponse
        {
            Accepted = true,
            Status = sensor.Status,
            RetryAfterSeconds = null,
            Reason = sensor.Status == SensorStatus.Standby ? "Registered as standby." : null
        };
    }

    public async Task MarkSensorDosBlockedAsync(string sensorId, CancellationToken cancellationToken)
    {
        var sensor = await dbContext.Sensors.SingleOrDefaultAsync(x => x.SensorId == sensorId, cancellationToken);
        if (sensor is null)
        {
            return;
        }

        if (sensor.DataQuality == DataQuality.BAD && sensor.Status == SensorStatus.DosBlocked)
        {
            return;
        }

        var wasActive = sensor.Status == SensorStatus.Active;
        var now = DateTimeOffset.UtcNow;

        sensor.Status = SensorStatus.DosBlocked;
        sensor.DataQuality = DataQuality.BAD;
        sensor.BadMarkedAt = now;
        sensor.DosBlockedUntil = now.AddSeconds(sensorOptions.Value.DoSBlockSeconds);
        sensor.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogWarning("Sensor {SensorId} marked as DoS blocked.", sensorId);

        await notifications.SendStatusChangedAsync(
            new StatusNotificationRequest
            {
                SensorId = sensor.SensorId,
                Status = sensor.Status,
                Message = "Sensor blocked due to DoS protection."
            },
            cancellationToken);

        if (wasActive)
        {
            await EnsureRequiredActiveSensorsAsync(cancellationToken);
        }
    }

    public async Task CheckInactiveSensorsAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = now.AddSeconds(-sensorOptions.Value.InactiveTimeoutSeconds);
        var changed = false;

        var activeSensors = await dbContext.Sensors
            .Where(x => x.Status == SensorStatus.Active)
            .OrderBy(x => x.RegisteredAt)
            .ToListAsync(cancellationToken);

        foreach (var sensor in activeSensors)
        {
            var lastSeen = sensor.LastMessageAt ?? sensor.RegisteredAt;
            if (lastSeen > cutoff)
            {
                continue;
            }

            sensor.Status = SensorStatus.InactiveBlocked;
            sensor.InactiveBlockedAt = now;
            sensor.UpdatedAt = now;
            changed = true;

            logger.LogWarning("Sensor {SensorId} marked inactive.", sensor.SensorId);

            await notifications.SendStatusChangedAsync(
                new StatusNotificationRequest
                {
                    SensorId = sensor.SensorId,
                    Status = sensor.Status,
                    Message = "Sensor marked inactive after heartbeat timeout."
                },
                cancellationToken);
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await EnsureRequiredActiveSensorsAsync(cancellationToken);
        }
    }

    public async Task HandleConsensusEventAsync(ConsensusEventRequest request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var createdBadSensors = new List<string>();

        foreach (var sensorId in request.BadSensors.Distinct(StringComparer.Ordinal))
        {
            var sensor = await dbContext.Sensors.SingleOrDefaultAsync(x => x.SensorId == sensorId, cancellationToken);
            if (sensor is null || sensor.DataQuality == DataQuality.BAD)
            {
                continue;
            }

            sensor.DataQuality = DataQuality.BAD;
            sensor.Status = SensorStatus.Bad;
            sensor.BadMarkedAt = now;
            sensor.UpdatedAt = now;
            createdBadSensors.Add(sensor.SensorId);

            await notifications.SendStatusChangedAsync(
                new StatusNotificationRequest
                {
                    SensorId = sensor.SensorId,
                    Status = sensor.Status,
                    Message = "Sensor marked BAD by consensus analysis."
                },
                cancellationToken);
        }

        if (createdBadSensors.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await EnsureRequiredActiveSensorsAsync(cancellationToken);
        }

        await notifications.SendConsensusAsync(
            new ConsensusNotificationRequest
            {
                WindowStart = request.WindowStart,
                WindowEnd = request.WindowEnd,
                Value = request.ConsensusValue,
                Priority = request.AlarmPriority,
                BadSensors = createdBadSensors
            },
            cancellationToken);

        if (request.AlarmPriority != AlarmPriority.None)
        {
            await notifications.SendAlarmAsync(
                new AlarmNotificationRequest
                {
                    Source = "ConsensusWorker",
                    Value = request.ConsensusValue,
                    Priority = request.AlarmPriority,
                    Timestamp = request.WindowEnd,
                    IsConsensus = true
                },
                cancellationToken);
        }
    }

    private async Task EnsureRequiredActiveSensorsAsync(CancellationToken cancellationToken)
    {
        var requiredActive = sensorOptions.Value.RequiredActiveSensors;

        while (await dbContext.Sensors.CountAsync(x => x.Status == SensorStatus.Active, cancellationToken) < requiredActive)
        {
            var standby = await dbContext.Sensors
                .Where(x => x.Status == SensorStatus.Standby && x.DataQuality == DataQuality.GOOD)
                .OrderBy(x => x.RegisteredAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (standby is null)
            {
                logger.LogWarning("No GOOD standby sensor available to activate.");
                return;
            }

            standby.Status = SensorStatus.Active;
            standby.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Activated standby sensor {SensorId}.", standby.SensorId);

            await notifications.SendActivationAsync(standby.SensorId, cancellationToken);
            await notifications.SendStatusChangedAsync(
                new StatusNotificationRequest
                {
                    SensorId = standby.SensorId,
                    Status = standby.Status,
                    Message = "Sensor activated as reserve."
                },
                cancellationToken);
        }
    }

    private SensorStatus ResolveRegistrationStatus(Sensor? existingSensor, DataQuality requestedQuality, int activeCount)
    {
        if (existingSensor?.Status == SensorStatus.Active)
        {
            return SensorStatus.Active;
        }

        if (requestedQuality == DataQuality.BAD)
        {
            return SensorStatus.Standby;
        }

        return activeCount < sensorOptions.Value.RequiredActiveSensors
            ? SensorStatus.Active
            : SensorStatus.Standby;
    }

    private SensorRegistrationResponse Reject(string reason) =>
        new()
        {
            Accepted = false,
            Status = null,
            RetryAfterSeconds = sensorOptions.Value.DeniedRegisterRetrySeconds,
            Reason = reason
        };
}
