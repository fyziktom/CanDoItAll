using System.Text.Json;
using System.Text.RegularExpressions;

namespace CanDoItAll.SharedProviders.E2E;

internal static class E2eModelEvidence {
    public const string Command = "read-model-evidence";

    public static async Task ReadAsync(CancellationToken cancellationToken) {
        var options = E2eScenarioCommandLine.Parse(["run-scenarios", "--phase", "normal"]);
        using var http = new E2eScenarioHttpClient();
        var token = await E2eSecretFile.ReadRequiredAsync(options.UpstreamControlTokenFilePath, "upstream control token", cancellationToken);
        using var response = await http.GetAsync(options.UpstreamControlBaseUri, "/_test/captures", token, null, cancellationToken);
        if (!response.IsSuccessStatusCode) {
            throw new E2eSafeException("Owned upstream captures are unavailable.");
        }
        var snapshot = await http.ReadJsonAsync<E2eCaptureSnapshot>(response, cancellationToken);
        var observations = new List<ModelObservation>();
        foreach (var request in snapshot.Requests) {
            if (request.Path is not ("/v1/chat/completions" or "/v1/responses" or "/v1/images/generations" or "/api/chat")) {
                continue;
            }
            using var body = JsonDocument.Parse(request.Body);
            var marker = Regex.Match(request.Body, "PP2 native model (sp1\\.[a-f0-9]{32}\\.[A-Za-z0-9_-]{43})", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
            if (!marker.Success) {
                continue;
            }
            if (request.BodyTruncated || !body.RootElement.TryGetProperty("model", out var model) || model.ValueKind != JsonValueKind.String) {
                throw new E2eSafeException("Native model evidence has an incomplete upstream request.");
            }
            var trace = request.Headers.SafeValues.FirstOrDefault(pair => pair.Key.Equals("traceparent", StringComparison.OrdinalIgnoreCase)).Value;
            observations.Add(new(request.Sequence, request.Path, marker.Groups[1].Value, model.GetString()!, request.ResponseStatusCode, trace?.SingleOrDefault()));
        }
        Console.WriteLine(JsonSerializer.Serialize(new { snapshot.Capacity, snapshot.Count, Observations = observations }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
    }

    private sealed record ModelObservation(long Sequence, string Path, string ClientRoute, string UpstreamModel, int? Status, string? TraceParent);
}
