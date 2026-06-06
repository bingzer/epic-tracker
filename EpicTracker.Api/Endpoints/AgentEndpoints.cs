using Carter;
using Microsoft.Extensions.Options;

namespace EpicTracker.Api.Endpoints;

public class AgentEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/agents", GetAgents);
    }

    private static async Task<IResult> GetAgents(
        IOptions<EpicTrackerOptions> options,
        IHttpClientFactory httpFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var http = httpFactory.CreateClient();
            var url = $"{options.Value.TmuxBrokerUrl.TrimEnd('/')}/api/agents";
            var response = await http.GetAsync(url, cancellationToken);
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            return Results.Bytes(bytes, "application/json");
        }
        catch (Exception ex)
        {
            return Results.Bytes(System.Text.Encoding.UTF8.GetBytes($"[]"), "application/json");
        }
    }
}
