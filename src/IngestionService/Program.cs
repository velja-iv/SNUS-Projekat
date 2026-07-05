using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using IngestionService.Services;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.Configure<SensorSystemOptions>(builder.Configuration.GetSection("SensorSystem"));
builder.Services.Configure<ConsensusOptions>(builder.Configuration.GetSection("Consensus"));
builder.Services.Configure<AlarmThresholdsDto>(builder.Configuration.GetSection("AlarmThresholds"));
builder.Services.Configure<ServerCryptoOptions>(builder.Configuration.GetSection("Crypto"));
builder.Services.Configure<NotificationServiceOptions>(builder.Configuration.GetSection("NotificationService"));

builder.Services.AddDbContext<SensorSystemDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("SensorSystem")));

builder.Services.AddHttpClient<NotificationApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<NotificationServiceOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
});

builder.Services.AddScoped<SensorRegistryService>();
builder.Services.AddScoped<MeasurementProcessingService>();
builder.Services.AddHostedService<InactiveSensorMonitor>();

var sensorSystemOptions = builder.Configuration.GetSection("SensorSystem").Get<SensorSystemOptions>() ?? new();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        var sensorId = context.HttpContext.Items[MeasurementSensorIdMiddleware.SensorIdItemKey]?.ToString();
        if (!string.IsNullOrWhiteSpace(sensorId))
        {
            using var scope = context.HttpContext.RequestServices.CreateScope();
            var registry = scope.ServiceProvider.GetRequiredService<SensorRegistryService>();
            await registry.MarkSensorDosBlockedAsync(sensorId, token);
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            new MeasurementIngestResponse
            {
                Accepted = false,
                Message = "Rate limit exceeded. Sensor marked as DoS blocked."
            },
            cancellationToken: token);
    };

    options.AddPolicy("measurement-limit", httpContext =>
    {
        var sensorId = httpContext.Items[MeasurementSensorIdMiddleware.SensorIdItemKey]?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(sensorId, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = sensorSystemOptions.DoSMaxMessagesPerSecond,
            Window = TimeSpan.FromSeconds(1),
            AutoReplenishment = true,
            QueueLimit = 0
        });
    });
});

var app = builder.Build();

app.UseMiddleware<MeasurementSensorIdMiddleware>();
app.UseRateLimiter();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SensorSystemDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

var ingest = app.MapGroup("/api/ingest");

ingest.MapPost("/register", async (SensorRegistrationRequest request, SensorRegistryService registry, CancellationToken cancellationToken) =>
{
    var response = await registry.RegisterAsync(request, cancellationToken);
    return Results.Ok(response);
});

ingest.MapPost("/measurements", async (SecureEnvelopeDto envelope, MeasurementProcessingService processor, CancellationToken cancellationToken) =>
{
    var response = await processor.ProcessAsync(envelope, cancellationToken);
    return response.Accepted
        ? Results.Ok(response)
        : Results.BadRequest(response);
}).RequireRateLimiting("measurement-limit");

ingest.MapPost("/events/consensus", async (ConsensusEventRequest request, SensorRegistryService registry, CancellationToken cancellationToken) =>
{
    await registry.HandleConsensusEventAsync(request, cancellationToken);
    return Results.Ok();
});

ingest.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
