using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using NotificationService.Hubs;
using NotificationService.Services;
using Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.AddSignalR();
builder.Services.AddSingleton<SensorConnectionRegistry>();

var app = builder.Build();

app.MapHub<SensorHub>("/hubs/sensors");

var notifications = app.MapGroup("/api/notifications");

notifications.MapPost("/sensors/{sensorId}/activate", async (
    string sensorId,
    SensorConnectionRegistry registry,
    IHubContext<SensorHub> hubContext,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    var logger = loggerFactory.CreateLogger("NotificationActivate");
    if (registry.TryGetConnectionId(sensorId, out var connectionId))
    {
        await hubContext.Clients.Client(connectionId).SendAsync("Activate", cancellationToken);
        logger.LogInformation("Sent Activate to {SensorId}.", sensorId);
        return Results.Ok();
    }

    logger.LogWarning("Activate requested for disconnected sensor {SensorId}.", sensorId);
    return Results.Accepted();
});

notifications.MapPost("/sensors/{sensorId}/status", async (
    string sensorId,
    StatusNotificationRequest request,
    SensorConnectionRegistry registry,
    IHubContext<SensorHub> hubContext,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    var logger = loggerFactory.CreateLogger("NotificationStatus");
    var payload = new StatusNotificationRequest
    {
        SensorId = sensorId,
        Status = request.Status,
        Message = request.Message
    };
    if (registry.TryGetConnectionId(sensorId, out var connectionId))
    {
        await hubContext.Clients.Client(connectionId).SendAsync("StatusChanged", payload, cancellationToken);
    }

    logger.LogInformation("Status update for {SensorId}: {Status}.", sensorId, request.Status);
    return Results.Ok();
});

notifications.MapPost("/alarms", async (
    AlarmNotificationRequest request,
    IHubContext<SensorHub> hubContext,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    var logger = loggerFactory.CreateLogger("NotificationAlarm");
    await hubContext.Clients.All.SendAsync("Alarm", request, cancellationToken);
    logger.LogInformation("Broadcast alarm from {Source} with priority {Priority}.", request.Source, request.Priority);
    return Results.Ok();
});

notifications.MapPost("/consensus", async (
    ConsensusNotificationRequest request,
    IHubContext<SensorHub> hubContext,
    ILoggerFactory loggerFactory,
    CancellationToken cancellationToken) =>
{
    var logger = loggerFactory.CreateLogger("NotificationConsensus");
    await hubContext.Clients.All.SendAsync("ConsensusCalculated", request, cancellationToken);
    logger.LogInformation("Broadcast consensus value {Value} for window {WindowStart} - {WindowEnd}.", request.Value, request.WindowStart, request.WindowEnd);
    return Results.Ok();
});

app.Run();
