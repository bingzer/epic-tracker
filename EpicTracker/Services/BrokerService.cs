using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EpicTracker.Services;

public class BrokerService(HttpClient http, IOptions<EpicTrackerOptions> options, ILogger<BrokerService> logger)
{
    private string BaseUrl => options.Value.TmuxBrokerUrl.TrimEnd('/');

    public async Task CreateChannel(string channelId, string from, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync($"{BaseUrl}/api/channels", new { channelId, from }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task InviteToChannel(string channelId, string sessionName, string from, CancellationToken cancellationToken = default)
    {
        var response = await http.PostAsJsonAsync($"{BaseUrl}/api/channels/{channelId}/members", new { sessionName, from }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task PostToChannel(string channelId, string from, string message, CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/channels/{channelId}/messages")
        {
            Content = JsonContent.Create(new { from, payload = message })
        };
        var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteChannel(string channelId, string from, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await http.DeleteAsync($"{BaseUrl}/api/channels/{channelId}?from={Uri.EscapeDataString(from)}", cancellationToken);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete broker channel {ChannelId} — channel may not exist", channelId);
        }
    }
}
