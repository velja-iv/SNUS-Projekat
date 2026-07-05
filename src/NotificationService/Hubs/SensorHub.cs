using Microsoft.AspNetCore.SignalR;
using NotificationService.Services;

namespace NotificationService.Hubs;

public sealed class SensorHub(
    SensorConnectionRegistry connectionRegistry,
    ILogger<SensorHub> logger) : Hub
{
    public Task RegisterSensorConnection(string sensorId)
    {
        connectionRegistry.Register(sensorId, Context.ConnectionId);
        logger.LogInformation("Sensor {SensorId} connected with connection {ConnectionId}.", sensorId, Context.ConnectionId);
        return Task.CompletedTask;
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var sensorId = connectionRegistry.RemoveByConnectionId(Context.ConnectionId);
        if (!string.IsNullOrWhiteSpace(sensorId))
        {
            logger.LogInformation("Sensor {SensorId} disconnected.", sensorId);
        }

        return base.OnDisconnectedAsync(exception);
    }
}
