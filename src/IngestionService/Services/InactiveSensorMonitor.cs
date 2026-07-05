namespace IngestionService.Services;

public sealed class InactiveSensorMonitor(
    IServiceScopeFactory scopeFactory,
    ILogger<InactiveSensorMonitor> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var registry = scope.ServiceProvider.GetRequiredService<SensorRegistryService>();
                await registry.CheckInactiveSensorsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Inactive sensor monitor failed.");
            }
        }
    }
}
