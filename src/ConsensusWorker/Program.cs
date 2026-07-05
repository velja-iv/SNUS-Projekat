using ConsensusWorker;
using Microsoft.EntityFrameworkCore;
using Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<SensorSystemOptions>(builder.Configuration.GetSection("SensorSystem"));
builder.Services.Configure<ConsensusOptions>(builder.Configuration.GetSection("Consensus"));
builder.Services.Configure<AlarmThresholdsDto>(builder.Configuration.GetSection("AlarmThresholds"));
builder.Services.Configure<ServiceEndpointOptions>(builder.Configuration.GetSection("Services"));

builder.Services.AddDbContext<SensorSystemDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("SensorSystem")));

builder.Services.AddHttpClient<IngestionEventsClient>();
builder.Services.AddHostedService<ConsensusBackgroundService>();

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SensorSystemDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

app.MapGet("/", () => Results.Ok(new { status = "consensus-worker-running" }));

app.Run();
