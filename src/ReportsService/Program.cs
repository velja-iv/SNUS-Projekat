using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddDbContext<SensorSystemDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("SensorSystem")));

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SensorSystemDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

var reports = app.MapGroup("/api/reports");

reports.MapGet("/measurements", async (
    string? sensorId,
    DateTimeOffset? from,
    DateTimeOffset? to,
    SensorSystemDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var query = dbContext.SensorMeasurements.AsNoTracking().AsQueryable();
    if (!string.IsNullOrWhiteSpace(sensorId))
    {
        query = query.Where(x => x.SensorId == sensorId);
    }

    if (from.HasValue)
    {
        query = query.Where(x => x.Timestamp >= from.Value);
    }

    if (to.HasValue)
    {
        query = query.Where(x => x.Timestamp <= to.Value);
    }

    var data = await query
        .OrderByDescending(x => x.Timestamp)
        .Select(x => new MeasurementReportDto
        {
            SensorId = x.SensorId,
            Value = x.Value,
            Timestamp = x.Timestamp,
            ReceivedAt = x.ReceivedAt,
            MessageId = x.MessageId,
            DataQuality = x.DataQuality,
            AlarmPriority = x.AlarmPriority
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(data);
});

reports.MapGet("/consensus", async (
    DateTimeOffset? from,
    DateTimeOffset? to,
    SensorSystemDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var query = dbContext.ConsensusMeasurements.AsNoTracking().AsQueryable();
    if (from.HasValue)
    {
        query = query.Where(x => x.WindowStart >= from.Value);
    }

    if (to.HasValue)
    {
        query = query.Where(x => x.WindowEnd <= to.Value);
    }

    var data = await query
        .OrderByDescending(x => x.WindowStart)
        .Select(x => new ConsensusReportDto
        {
            Value = x.Value,
            WindowStart = x.WindowStart,
            WindowEnd = x.WindowEnd,
            CalculatedAt = x.CalculatedAt,
            ParticipatingCount = x.ParticipatingCount,
            ExcludedCount = x.ExcludedCount,
            AlarmPriority = x.AlarmPriority,
            Algorithm = x.Algorithm
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(data);
});

reports.MapGet("/alarms", async (
    DateTimeOffset? from,
    DateTimeOffset? to,
    SensorSystemDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var rawQuery = dbContext.SensorMeasurements
        .AsNoTracking()
        .Where(x => x.AlarmPriority != AlarmPriority.None);
    var consensusQuery = dbContext.ConsensusMeasurements
        .AsNoTracking()
        .Where(x => x.AlarmPriority != AlarmPriority.None);

    if (from.HasValue)
    {
        rawQuery = rawQuery.Where(x => x.Timestamp >= from.Value);
        consensusQuery = consensusQuery.Where(x => x.WindowEnd >= from.Value);
    }

    if (to.HasValue)
    {
        rawQuery = rawQuery.Where(x => x.Timestamp <= to.Value);
        consensusQuery = consensusQuery.Where(x => x.WindowEnd <= to.Value);
    }

    var raw = await rawQuery
        .Select(x => new AlarmReportDto
        {
            SourceType = "Raw",
            SensorId = x.SensorId,
            Value = x.Value,
            AlarmPriority = x.AlarmPriority,
            Timestamp = x.Timestamp
        })
        .ToListAsync(cancellationToken);

    var consensus = await consensusQuery
        .Select(x => new AlarmReportDto
        {
            SourceType = "Consensus",
            SensorId = null,
            Value = x.Value,
            AlarmPriority = x.AlarmPriority,
            Timestamp = x.WindowEnd
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(raw.Concat(consensus).OrderByDescending(x => x.Timestamp));
});

reports.MapGet("/sensors", async (SensorSystemDbContext dbContext, CancellationToken cancellationToken) =>
{
    var data = await dbContext.Sensors
        .AsNoTracking()
        .OrderBy(x => x.RegisteredAt)
        .Select(x => new SensorReportDto
        {
            SensorId = x.SensorId,
            DataQuality = x.DataQuality,
            Status = x.Status,
            RegisteredAt = x.RegisteredAt,
            UpdatedAt = x.UpdatedAt,
            LastMessageAt = x.LastMessageAt,
            ConsecutiveFaultCount = x.ConsecutiveFaultCount,
            TotalFaultCount = x.TotalFaultCount
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(data);
});

reports.MapGet("/sensors/active", async (SensorSystemDbContext dbContext, CancellationToken cancellationToken) =>
{
    var data = await dbContext.Sensors
        .AsNoTracking()
        .Where(x => x.Status == SensorStatus.Active)
        .OrderBy(x => x.RegisteredAt)
        .Select(x => new SensorReportDto
        {
            SensorId = x.SensorId,
            DataQuality = x.DataQuality,
            Status = x.Status,
            RegisteredAt = x.RegisteredAt,
            UpdatedAt = x.UpdatedAt,
            LastMessageAt = x.LastMessageAt,
            ConsecutiveFaultCount = x.ConsecutiveFaultCount,
            TotalFaultCount = x.TotalFaultCount
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(data);
});

reports.MapGet("/sensors/bad", async (SensorSystemDbContext dbContext, CancellationToken cancellationToken) =>
{
    var data = await dbContext.Sensors
        .AsNoTracking()
        .Where(x => x.DataQuality == DataQuality.BAD || x.Status == SensorStatus.Bad)
        .OrderBy(x => x.RegisteredAt)
        .Select(x => new SensorReportDto
        {
            SensorId = x.SensorId,
            DataQuality = x.DataQuality,
            Status = x.Status,
            RegisteredAt = x.RegisteredAt,
            UpdatedAt = x.UpdatedAt,
            LastMessageAt = x.LastMessageAt,
            ConsecutiveFaultCount = x.ConsecutiveFaultCount,
            TotalFaultCount = x.TotalFaultCount
        })
        .ToListAsync(cancellationToken);

    return Results.Ok(data);
});

app.Run();
