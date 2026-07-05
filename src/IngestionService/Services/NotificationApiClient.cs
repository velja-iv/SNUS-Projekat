using System.Net.Http.Json;
using Shared;

namespace IngestionService.Services;

public sealed class NotificationApiClient(HttpClient httpClient, ILogger<NotificationApiClient> logger)
{
    public async Task SendActivationAsync(string sensorId, CancellationToken cancellationToken)
    {
        await PostAsync($"api/notifications/sensors/{sensorId}/activate", new { }, cancellationToken);
    }

    public async Task SendStatusChangedAsync(StatusNotificationRequest request, CancellationToken cancellationToken)
    {
        await PostAsync($"api/notifications/sensors/{request.SensorId}/status", request, cancellationToken);
    }

    public async Task SendAlarmAsync(AlarmNotificationRequest request, CancellationToken cancellationToken)
    {
        await PostAsync("api/notifications/alarms", request, cancellationToken);
    }

    public async Task SendConsensusAsync(ConsensusNotificationRequest request, CancellationToken cancellationToken)
    {
        await PostAsync("api/notifications/consensus", request, cancellationToken);
    }

    private async Task PostAsync<T>(string path, T payload, CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.PostAsJsonAsync(path, payload, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Notification call to {Path} failed with status {StatusCode}.", path, response.StatusCode);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Notification call to {Path} failed.", path);
        }
    }
}
