using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared;

namespace IngestionService.Services;

public sealed class MeasurementProcessingService(
    SensorSystemDbContext dbContext,
    IOptions<SensorSystemOptions> sensorOptions,
    IOptions<ServerCryptoOptions> cryptoOptions,
    NotificationApiClient notifications,
    ILogger<MeasurementProcessingService> logger)
{
    public async Task<MeasurementIngestResponse> ProcessAsync(SecureEnvelopeDto envelope, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(envelope.SensorId))
        {
            return Reject("Missing outer sensorId.");
        }

        var sensor = await dbContext.Sensors.SingleOrDefaultAsync(x => x.SensorId == envelope.SensorId, cancellationToken);
        if (sensor is null)
        {
            return Reject("Sensor is not registered.");
        }

        if (sensor.Status is SensorStatus.InactiveBlocked or SensorStatus.DosBlocked)
        {
            return Reject("Sensor is blocked or marked BAD.");
        }

        byte[] payloadBytes;
        try
        {
            using var serverPrivateKey = SecureMessageCrypto.LoadPrivateKeyFromFile(cryptoOptions.Value.ServerPrivateKeyPath);
            payloadBytes = SecureMessageCrypto.DecryptPayload(envelope, serverPrivateKey);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to decrypt message from {SensorId}.", envelope.SensorId);
            return Reject("Payload decryption failed.");
        }

        try
        {
            using var sensorPublicKey = SecureMessageCrypto.LoadPublicKeyFromPem(sensor.PublicKeyPem);
            if (!SecureMessageCrypto.VerifySignature(envelope, sensorPublicKey))
            {
                logger.LogWarning("Invalid signature for {SensorId}.", envelope.SensorId);
                return Reject("Signature validation failed.");
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to verify signature for {SensorId}.", envelope.SensorId);
            return Reject("Signature validation failed.");
        }

        MeasurementPayloadDto? payload;
        try
        {
            payload = JsonSerializer.Deserialize<MeasurementPayloadDto>(payloadBytes, SecureMessageCrypto.JsonSerializerOptions);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "Failed to deserialize payload from {SensorId}.", envelope.SensorId);
            return Reject("Payload deserialization failed.");
        }

        if (payload is null)
        {
            return Reject("Payload is empty.");
        }

        if (!string.Equals(envelope.SensorId, payload.SensorId, StringComparison.Ordinal))
        {
            return Reject("Outer and inner sensor identifiers do not match.");
        }

        var now = DateTimeOffset.UtcNow;
        if (Math.Abs((now - payload.Timestamp).TotalSeconds) > sensorOptions.Value.ReplayToleranceSeconds)
        {
            logger.LogWarning("Rejected old payload from {SensorId}.", payload.SensorId);
            return Reject("Payload timestamp is outside the accepted tolerance.");
        }

        if (sensor.LastAcceptedMessageId.HasValue && payload.MessageId <= sensor.LastAcceptedMessageId.Value)
        {
            logger.LogWarning("Rejected replay messageId candidate from {SensorId}.", payload.SensorId);
            return Reject("Payload messageId is not sequential.");
        }

        var measurement = new SensorMeasurement
        {
            SensorId = payload.SensorId,
            Value = payload.Value,
            Timestamp = payload.Timestamp,
            ReceivedAt = now,
            MessageId = payload.MessageId,
            DataQuality = payload.DataQuality,
            AlarmPriority = payload.AlarmPriority,
            IsConsensus = false,
            SignatureValid = true,
            ReplayAccepted = true,
            RawPayloadHash = SecureMessageCrypto.ComputePayloadHash(payloadBytes)
        };

        sensor.LastMessageAt = now;
        sensor.LastAcceptedMessageId = payload.MessageId;
        sensor.UpdatedAt = now;

        dbContext.SensorMeasurements.Add(measurement);
        await dbContext.SaveChangesAsync(cancellationToken);

        if (payload.AlarmPriority != AlarmPriority.None)
        {
            await notifications.SendAlarmAsync(
                new AlarmNotificationRequest
                {
                    Source = payload.SensorId,
                    Value = payload.Value,
                    Priority = payload.AlarmPriority,
                    Timestamp = payload.Timestamp,
                    IsConsensus = false
                },
                cancellationToken);
        }

        logger.LogInformation(
            "Accepted measurement {MessageId} from {SensorId} with value {Value}.",
            payload.MessageId,
            payload.SensorId,
            payload.Value);

        return new MeasurementIngestResponse
        {
            Accepted = true,
            Message = "Measurement accepted.",
            AcceptedMessageId = payload.MessageId
        };
    }

    private static MeasurementIngestResponse Reject(string message) =>
        new()
        {
            Accepted = false,
            Message = message
        };
}
