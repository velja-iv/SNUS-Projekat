using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Shared;

namespace ConsensusWorker;

public sealed class IngestionEventsClient(
    HttpClient httpClient,
    IOptions<ServiceEndpointOptions> serviceEndpoints,
    ILogger<IngestionEventsClient> logger)
{
    public async Task SendConsensusEventAsync(ConsensusEventRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var baseUrl = serviceEndpoints.Value.IngestionBaseUrl.TrimEnd('/');
            var response = await httpClient.PostAsJsonAsync($"{baseUrl}/api/ingest/events/consensus", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Consensus event delivery failed with status {StatusCode}.", response.StatusCode);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Consensus event delivery failed.");
        }
    }
}
