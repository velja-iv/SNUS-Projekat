using System.Text;
using System.Text.Json;

namespace IngestionService.Services;

public sealed class MeasurementSensorIdMiddleware(RequestDelegate next)
{
    public const string SensorIdItemKey = "OuterSensorId";

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.Equals("/api/ingest/measurements", StringComparison.OrdinalIgnoreCase) &&
            HttpMethods.IsPost(context.Request.Method))
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
            var body = await reader.ReadToEndAsync(context.RequestAborted);
            context.Request.Body.Position = 0;

            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    using var document = JsonDocument.Parse(body);
                    if (document.RootElement.TryGetProperty("sensorId", out var sensorIdElement))
                    {
                        context.Items[SensorIdItemKey] = sensorIdElement.GetString();
                    }
                }
                catch (JsonException)
                {
                    // Allow malformed requests to reach the endpoint and fail normally there.
                }
            }
        }

        await next(context);
    }
}
