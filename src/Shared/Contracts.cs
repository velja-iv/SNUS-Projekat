namespace Shared;

public sealed class TemperatureGenerationDto
{
    public double Min { get; init; }
    public double Max { get; init; }
}

public sealed class AlarmThresholdsDto
{
    public double Priority1Low { get; init; }
    public double Priority1High { get; init; }
    public double Priority2Low { get; init; }
    public double Priority2High { get; init; }
    public double Priority3Low { get; init; }
    public double Priority3High { get; init; }
}

public sealed class SensorRegistrationRequest
{
    public string SensorId { get; init; } = string.Empty;
    public DataQuality InitialDataQuality { get; init; } = DataQuality.GOOD;
    public string PublicKeyPem { get; init; } = string.Empty;
    public TemperatureGenerationDto TemperatureGeneration { get; init; } = new();
    public AlarmThresholdsDto AlarmThresholds { get; init; } = new();
}

public sealed class SensorRegistrationResponse
{
    public bool Accepted { get; init; }
    public SensorStatus? Status { get; init; }
    public int? RetryAfterSeconds { get; init; }
    public string? Reason { get; init; }
}

public sealed class SecureEnvelopeDto
{
    public string SensorId { get; init; } = string.Empty;
    public string EncryptedAesKey { get; init; } = string.Empty;
    public string Iv { get; init; } = string.Empty;
    public string CipherText { get; init; } = string.Empty;
    public string Tag { get; init; } = string.Empty;
    public string Signature { get; init; } = string.Empty;
}

public sealed class MeasurementPayloadDto
{
    public string SensorId { get; init; } = string.Empty;
    public long MessageId { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public double Value { get; init; }
    public AlarmPriority AlarmPriority { get; init; }
    public DataQuality DataQuality { get; init; } = DataQuality.GOOD;
}

public sealed class MeasurementIngestResponse
{
    public bool Accepted { get; init; }
    public string Message { get; init; } = string.Empty;
    public long? AcceptedMessageId { get; init; }
}

public sealed class ConsensusEventRequest
{
    public DateTimeOffset WindowStart { get; init; }
    public DateTimeOffset WindowEnd { get; init; }
    public double ConsensusValue { get; init; }
    public AlarmPriority AlarmPriority { get; init; }
    public List<string> BadSensors { get; init; } = [];
}

public sealed class AlarmNotificationRequest
{
    public string Source { get; init; } = string.Empty;
    public double Value { get; init; }
    public AlarmPriority Priority { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public bool IsConsensus { get; init; }
}

public sealed class StatusNotificationRequest
{
    public string SensorId { get; init; } = string.Empty;
    public SensorStatus Status { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class ConsensusNotificationRequest
{
    public DateTimeOffset WindowStart { get; init; }
    public DateTimeOffset WindowEnd { get; init; }
    public double Value { get; init; }
    public AlarmPriority Priority { get; init; }
    public IReadOnlyCollection<string> BadSensors { get; init; } = [];
}

public sealed class MeasurementReportDto
{
    public string SensorId { get; init; } = string.Empty;
    public double Value { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public DateTimeOffset ReceivedAt { get; init; }
    public long MessageId { get; init; }
    public DataQuality DataQuality { get; init; }
    public AlarmPriority AlarmPriority { get; init; }
}

public sealed class ConsensusReportDto
{
    public double Value { get; init; }
    public DateTimeOffset WindowStart { get; init; }
    public DateTimeOffset WindowEnd { get; init; }
    public DateTimeOffset CalculatedAt { get; init; }
    public int ParticipatingCount { get; init; }
    public int ExcludedCount { get; init; }
    public AlarmPriority AlarmPriority { get; init; }
    public string Algorithm { get; init; } = string.Empty;
}

public sealed class AlarmReportDto
{
    public string SourceType { get; init; } = string.Empty;
    public string? SensorId { get; init; }
    public double Value { get; init; }
    public AlarmPriority AlarmPriority { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}

public sealed class SensorReportDto
{
    public string SensorId { get; init; } = string.Empty;
    public DataQuality DataQuality { get; init; }
    public SensorStatus Status { get; init; }
    public DateTimeOffset RegisteredAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? LastMessageAt { get; init; }
    public int ConsecutiveFaultCount { get; init; }
    public int TotalFaultCount { get; init; }
}

public sealed class SensorSystemOptions
{
    public int RequiredActiveSensors { get; init; } = 5;
    public int InactiveTimeoutSeconds { get; init; } = 10;
    public int DoSMaxMessagesPerSecond { get; init; } = 10;
    public int DoSBlockSeconds { get; init; } = 30;
    public int DeniedRegisterRetrySeconds { get; init; } = 60;
    public int ReplayToleranceSeconds { get; init; } = 30;
}

public sealed class ConsensusOptions
{
    public int WindowSeconds { get; init; } = 60;
    public int TrimPercent { get; init; } = 20;
    public double OutlierThresholdCelsius { get; init; } = 15.0;
    public int BadSensorIncidentThreshold { get; init; } = 3;
}

public sealed class ServerCryptoOptions
{
    public string ServerPrivateKeyPath { get; init; } = "keys/server-private.pem";
    public string ServerPublicKeyPath { get; init; } = "keys/server-public.pem";
}

public sealed class NotificationServiceOptions
{
    public string BaseUrl { get; init; } = "http://localhost:8082";
}

public sealed class ServiceEndpointOptions
{
    public string IngestionBaseUrl { get; init; } = "http://localhost:8080";
}

public sealed class SensorClientConfiguration
{
    public string SensorId { get; init; } = string.Empty;
    public DataQuality InitialDataQuality { get; init; } = DataQuality.GOOD;
    public TemperatureGenerationDto TemperatureGeneration { get; init; } = new();
    public AlarmThresholdsDto AlarmThresholds { get; init; } = new();
    public SensorMode Mode { get; init; } = SensorMode.Normal;
    public SensorServerOptions Server { get; init; } = new();
    public SensorCryptoOptions Crypto { get; init; } = new();
}

public sealed class SensorServerOptions
{
    public string IngressBaseUrl { get; init; } = "http://localhost:8080";
    public string NotificationHubUrl { get; init; } = "http://localhost:8082/hubs/sensors";
}

public sealed class SensorCryptoOptions
{
    public string SensorPrivateKeyPath { get; init; } = string.Empty;
    public string SensorPublicKeyPath { get; init; } = string.Empty;
    public string ServerPublicKeyPath { get; init; } = string.Empty;
}
