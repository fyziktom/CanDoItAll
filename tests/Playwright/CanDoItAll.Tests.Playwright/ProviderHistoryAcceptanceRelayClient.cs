using CanDoItAll.SharedProviders.Abstractions;
using System.Text;
using System.Text.Json;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

internal static class ProviderHistoryAcceptanceRelayClient {
    public static Guid ReadCredentialId(string token, string expectedScopes) {
        var encoded = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        encoded = encoded.PadRight((encoded.Length + 3) / 4 * 4, '=');
        using var payload = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(encoded)));
        Assert.Equal(
            expectedScopes.Split(' ').Order(StringComparer.Ordinal),
            payload.RootElement.GetProperty("scope").GetString()!.Split(' ').Order(StringComparer.Ordinal));
        return Guid.ParseExact(payload.RootElement.GetProperty("jti").GetString()!, "N");
    }

    public static async Task InvokeAsync(
        IAPIRequestContext request,
        string baseUrl,
        string token,
        string providerName) {
        var headers = new Dictionary<string, string> {
            ["Authorization"] = $"Bearer {token}"
        };
        var catalogResponse = await request.GetAsync(
            $"{baseUrl}{SharedProviderRoutes.Catalog}",
            new APIRequestContextOptions { Headers = headers });
        Assert.Equal(200, catalogResponse.Status);
        var catalog = SharedProviderProtocolJson.DeserializeCatalog(await catalogResponse.TextAsync());
        var publication = Assert.Single(
            catalog.Providers,
            candidate => string.Equals(candidate.DisplayName, providerName, StringComparison.Ordinal));
        var response = await request.PostAsync(
            $"{baseUrl}{SharedProviderRoutes.ChatCompletions}",
            new APIRequestContextOptions {
                Headers = headers,
                DataObject = new {
                    model = publication.DefaultModelId.Value,
                    messages = new[] {
                        new { role = "user", content = "Reply briefly without tools." }
                    },
                    stream = false
                },
                Timeout = 120_000
            });
        Assert.Equal(200, response.Status);
        Assert.False(string.IsNullOrWhiteSpace(await response.TextAsync()));
    }
}
