using Microsoft.EntityFrameworkCore;

namespace Shared;

public sealed class Sensor
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SensorId { get; set; } = string.Empty;
    public string PublicKeyPem { get; set; } = string.Empty;
    public DataQuality DataQuality { get; set; } = DataQuality.GOOD;
    public SensorStatus Status { get; set; } = SensorStatus.Standby;
    public DateTimeOffset? LastMessageAt { get; set; }
    public long? LastAcceptedMessageId { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DosBlockedUntil { get; set; }
    public DateTimeOffset? InactiveBlockedAt { get; set; }
    public DateTimeOffset? BadMarkedAt { get; set; }
    public int ConsecutiveFaultCount { get; set; }
    public int TotalFaultCount { get; set; }
    public string? ConnectionId { get; set; }
    public double TemperatureMin { get; set; }
    public double TemperatureMax { get; set; }
    public double Priority1Low { get; set; }
    public double Priority1High { get; set; }
    public double Priority2Low { get; set; }
    public double Priority2High { get; set; }
    public double Priority3Low { get; set; }
    public double Priority3High { get; set; }
}

public sealed class SensorMeasurement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SensorId { get; set; } = string.Empty;
    public double Value { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public long MessageId { get; set; }
    public DataQuality DataQuality { get; set; } = DataQuality.GOOD;
    public AlarmPriority AlarmPriority { get; set; } = AlarmPriority.None;
    public bool IsConsensus { get; set; }
    public bool SignatureValid { get; set; }
    public bool ReplayAccepted { get; set; }
    public string? RawPayloadHash { get; set; }
}

public sealed class ConsensusMeasurement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public double Value { get; set; }
    public DateTimeOffset WindowStart { get; set; }
    public DateTimeOffset WindowEnd { get; set; }
    public DateTimeOffset CalculatedAt { get; set; }
    public int ParticipatingCount { get; set; }
    public int ExcludedCount { get; set; }
    public AlarmPriority AlarmPriority { get; set; } = AlarmPriority.None;
    public bool IsConsensus { get; set; } = true;
    public string Algorithm { get; set; } = "TrimmedAverage";
}

public sealed class SensorSystemDbContext(DbContextOptions<SensorSystemDbContext> options) : DbContext(options)
{
    public DbSet<Sensor> Sensors => Set<Sensor>();
    public DbSet<SensorMeasurement> SensorMeasurements => Set<SensorMeasurement>();
    public DbSet<ConsensusMeasurement> ConsensusMeasurements => Set<ConsensusMeasurement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Sensor>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.SensorId).IsUnique();
            entity.Property(x => x.SensorId).HasMaxLength(128);
            entity.Property(x => x.PublicKeyPem).HasColumnType("text");
            entity.Property(x => x.ConnectionId).HasMaxLength(128);
            entity.Property(x => x.DataQuality).HasConversion<string>();
            entity.Property(x => x.Status).HasConversion<string>();
        });

        modelBuilder.Entity<SensorMeasurement>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.SensorId, x.Timestamp });
            entity.HasIndex(x => new { x.SensorId, x.MessageId }).IsUnique();
            entity.Property(x => x.SensorId).HasMaxLength(128);
            entity.Property(x => x.DataQuality).HasConversion<string>();
            entity.Property(x => x.AlarmPriority).HasConversion<string>();
            entity.Property(x => x.RawPayloadHash).HasMaxLength(128);
        });

        modelBuilder.Entity<ConsensusMeasurement>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.WindowStart, x.WindowEnd }).IsUnique();
            entity.Property(x => x.AlarmPriority).HasConversion<string>();
            entity.Property(x => x.Algorithm).HasMaxLength(64);
        });
    }
}
