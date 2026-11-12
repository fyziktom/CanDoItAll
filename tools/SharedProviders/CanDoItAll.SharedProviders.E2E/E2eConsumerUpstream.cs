using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CanDoItAll.SharedProviders.E2E;

internal static class E2eConsumerUpstream {
    public const string Command = "consumer-upstream";

    public static async Task ExecuteAsync(string[] args, CancellationToken cancellationToken) {
        var (method, path) = args switch {
            [Command, "script"] => (HttpMethod.Put, "/_test/response-script"),
            [Command, "progress"] => (HttpMethod.Get, "/_test/response-script"),
            [Command, "clear"] => (HttpMethod.Delete, "/_test/response-script"),
            [Command, "captures"] => (HttpMethod.Get, "/_test/captures"),
            _ => throw new E2eSafeException("Unknown owned consumer upstream operation.")
        };
        var options = E2eScenarioCommandLine.Parse(["run-scenarios", "--phase", "normal"]);
        var token = await E2eSecretFile.ReadRequiredAsync(options.UpstreamControlTokenFilePath, "upstream control token", cancellationToken);
        using var http = new HttpClient { BaseAddress = options.UpstreamControlBaseUri, Timeout = TimeSpan.FromSeconds(30) };
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (method == HttpMethod.Put) {
            var buffer = new char[65_537];
            var count = await Console.In.ReadBlockAsync(buffer, cancellationToken);
            if (count is 0 or > 65_536) {
                throw new E2eSafeException("The consumer response plan exceeded its input bound.");
            }
            request.Content = new StringContent(new string(buffer, 0, count), Encoding.UTF8, "application/json");
        }
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) {
            throw new E2eSafeException("The owned consumer upstream operation was refused.");
        }
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        Console.WriteLine(JsonSerializer.Serialize(body.RootElement));
    }
}
