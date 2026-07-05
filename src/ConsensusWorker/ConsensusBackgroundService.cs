using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared;

namespace ConsensusWorker;

public sealed class ConsensusBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<ConsensusOptions> consensusOptions,
    IOptions<AlarmThresholdsDto> alarmThresholds,
    ILogger<ConsensusBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessLatestWindowAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Consensus processing failed.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task ProcessLatestWindowAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SensorSystemDbContext>();
        var ingestionEventsClient = scope.ServiceProvider.GetRequiredService<IngestionEventsClient>();

        var windowEnd = TruncateToMinute(DateTimeOffset.UtcNow);
        var windowStart = windowEnd.AddSeconds(-consensusOptions.Value.WindowSeconds);

        if (await dbContext.ConsensusMeasurements.AnyAsync(x => x.WindowStart == windowStart && x.WindowEnd == windowEnd, cancellationToken))
        {
            return;
        }

        var goodSensors = await dbContext.Sensors
            .Where(x => x.DataQuality == DataQuality.GOOD)
            .Select(x => x.SensorId)
            .ToListAsync(cancellationToken);

        var sensorAverages = await dbContext.SensorMeasurements
            .Where(x => goodSensors.Contains(x.SensorId) && x.Timestamp >= windowStart && x.Timestamp < windowEnd)
            .GroupBy(x => x.SensorId)
            .Select(group => new
            {
                SensorId = group.Key,
                Average = group.Average(x => x.Value)
            })
            .ToListAsync(cancellationToken);

        if (sensorAverages.Count < 3)
        {
            logger.LogWarning("Skipping consensus for {WindowStart} - {WindowEnd}: only {Count} GOOD sensor averages.", windowStart, windowEnd, sensorAverages.Count);
            return;
        }

        var orderedValues = sensorAverages.Select(x => x.Average).OrderBy(x => x).ToList();
        var trimCount = Math.Min(orderedValues.Count / 2, (int)Math.Floor(orderedValues.Count * (consensusOptions.Value.TrimPercent / 100.0)));
        var trimmedValues = orderedValues.Skip(trimCount).Take(orderedValues.Count - (2 * trimCount)).ToList();
        if (trimmedValues.Count == 0)
        {
            trimmedValues = orderedValues;
        }

        var consensusValue = trimmedValues.Average();
        var excludedCount = sensorAverages.Count - trimmedValues.Count;
        var priority = AlarmEvaluator.Evaluate(consensusValue, alarmThresholds.Value);
        var now = DateTimeOffset.UtcNow;

        dbContext.ConsensusMeasurements.Add(new ConsensusMeasurement
        {
            Value = consensusValue,
            WindowStart = windowStart,
            WindowEnd = windowEnd,
            CalculatedAt = now,
            ParticipatingCount = trimmedValues.Count,
            ExcludedCount = excludedCount,
            AlarmPriority = priority,
            IsConsensus = true,
            Algorithm = "BFT-TrimmedAverage"
        });

        var badSensors = new List<string>();
        foreach (var item in sensorAverages)
        {
            var sensor = await dbContext.Sensors.SingleAsync(x => x.SensorId == item.SensorId, cancellationToken);
            var distance = Math.Abs(item.Average - consensusValue);
            if (distance > consensusOptions.Value.OutlierThresholdCelsius)
            {
                sensor.ConsecutiveFaultCount += 1;
                sensor.TotalFaultCount += 1;
                sensor.UpdatedAt = now;

                if (sensor.ConsecutiveFaultCount >= consensusOptions.Value.BadSensorIncidentThreshold)
                {
                    sensor.DataQuality = DataQuality.BAD;
                    sensor.Status = SensorStatus.Bad;
                    sensor.BadMarkedAt = now;
                    badSensors.Add(sensor.SensorId);
                }
            }
            else
            {
                sensor.ConsecutiveFaultCount = 0;
                sensor.UpdatedAt = now;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Consensus {Value} calculated for {WindowStart} - {WindowEnd} with {Participants} participants and {Excluded} excluded.",
            consensusValue,
            windowStart,
            windowEnd,
            trimmedValues.Count,
            excludedCount);

        await ingestionEventsClient.SendConsensusEventAsync(
            new ConsensusEventRequest
            {
                WindowStart = windowStart,
                WindowEnd = windowEnd,
                ConsensusValue = consensusValue,
                AlarmPriority = priority,
                BadSensors = badSensors
            },
            cancellationToken);
    }

    private static DateTimeOffset TruncateToMinute(DateTimeOffset timestamp) =>
        new(timestamp.Year, timestamp.Month, timestamp.Day, timestamp.Hour, timestamp.Minute, 0, TimeSpan.Zero);
}
