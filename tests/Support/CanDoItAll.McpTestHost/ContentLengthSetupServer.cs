using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CanDoItAll.McpTestHost;

internal static class ContentLengthSetupServer {
    internal static async Task<int> RunAsync() {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var input = Console.OpenStandardInput();
        var output = Console.OpenStandardOutput();
        for (var requestCount = 0; requestCount < 8; requestCount++) {
            var header = new List<byte>();
            var single = new byte[1];
            while (header.Count < 1024) {
                if (await input.ReadAsync(single, deadline.Token) == 0) {
                    return 0;
                }
                header.Add(single[0]);
                if (header.Count >= 4 && header[^4] == '\r' && header[^3] == '\n' && header[^2] == '\r' && header[^1] == '\n') {
                    break;
                }
            }
            var lengthHeader = Encoding.ASCII.GetString(header.ToArray()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
                .Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
            var length = int.Parse(lengthHeader.AsSpan("Content-Length:".Length), CultureInfo.InvariantCulture);
            if (length is <= 0 or > 16384) {
                throw new InvalidDataException("Owned setup request exceeded its bound.");
            }
            var bytes = new byte[length];
            await input.ReadExactlyAsync(bytes, deadline.Token);
            using var request = JsonDocument.Parse(bytes);
            var root = request.RootElement;
            if (!root.TryGetProperty("id", out var id)) {
                continue;
            }
            object result = root.GetProperty("method").GetString() switch {
                "initialize" => new {
                    protocolVersion = root.GetProperty("params").GetProperty("protocolVersion").GetString(),
                    capabilities = new { tools = new { } }, serverInfo = new { name = "CA1 owned stdio", version = "1.0" }
                },
                "tools/list" => new { tools = new[] { new {
                    name = "echo", description = "Owned setup-only fixture", inputSchema = new { type = "object", properties = new { } }
                } } },
                _ => throw new InvalidDataException("Only initialize and tool discovery are allowed by this setup fixture.")
            };
            var response = JsonSerializer.SerializeToUtf8Bytes(new { jsonrpc = "2.0", id, result });
            await output.WriteAsync(Encoding.ASCII.GetBytes($"Content-Length: {response.Length.ToString(CultureInfo.InvariantCulture)}\r\n\r\n"), deadline.Token);
            await output.WriteAsync(response, deadline.Token);
            await output.FlushAsync(deadline.Token);
        }
        throw new InvalidDataException("Owned setup request count exceeded its bound.");
    }
}
