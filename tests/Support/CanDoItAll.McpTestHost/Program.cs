using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.McpTestHost;

public static class McpTestHostMarker;

public static class Program
{
    public static async Task<int> Main(string[] arguments)
    {
        var mode = arguments.FirstOrDefault();
        var pidFile = ReadOption(arguments, "--pid-file");
        var readyFile = ReadOption(arguments, "--ready-file");
        if (!string.IsNullOrWhiteSpace(pidFile))
        {
            await File.WriteAllTextAsync(pidFile, Environment.ProcessId.ToString()).ConfigureAwait(false);
        }

        if (mode is "--external-hang")
        {
            SignalReady(readyFile);
            await Task.Delay(Timeout.InfiniteTimeSpan).ConfigureAwait(false);
        }

        if (mode is "--external-json")
        {
            await Console.In.ReadToEndAsync().ConfigureAwait(false);
            await Console.Out.WriteAsync(JsonSerializer.Serialize(new
            {
                ok = true,
                arguments
            })).ConfigureAwait(false);
            return 0;
        }

        if (mode is "--external-invalid")
        {
            var input = await Console.In.ReadToEndAsync().ConfigureAwait(false);
            await Console.Out.WriteAsync("not-json " + input).ConfigureAwait(false);
            return 0;
        }

        if (mode is "--external-fail")
        {
            var input = await Console.In.ReadToEndAsync().ConfigureAwait(false);
            await Console.Error.WriteAsync(input).ConfigureAwait(false);
            return 7;
        }

        while (await Console.In.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var request = JsonDocument.Parse(line);
            var root = request.RootElement;
            if (!root.TryGetProperty("id", out var id))
            {
                continue;
            }

            var method = root.GetProperty("method").GetString();
            if (mode == "--missing-initialize-result" && method == "initialize")
            {
                await Console.Out.WriteLineAsync(
                    $"{{\"jsonrpc\":\"2.0\",\"id\":{id.GetRawText()}}}").ConfigureAwait(false);
                await Console.Out.FlushAsync().ConfigureAwait(false);
                continue;
            }

            if (mode == "--unsupported-protocol" && method == "initialize")
            {
                await Console.Out.WriteLineAsync(
                    $"{{\"jsonrpc\":\"2.0\",\"id\":{id.GetRawText()},\"result\":{{\"protocolVersion\":\"1900-01-01\",\"capabilities\":{{}},\"serverInfo\":{{\"name\":\"Unsupported\",\"version\":\"1.0\"}}}}}}").ConfigureAwait(false);
                await Console.Out.FlushAsync().ConfigureAwait(false);
                continue;
            }

            if (mode == "--hang-list" && method == "tools/list")
            {
                SignalReady(readyFile);
                await Task.Delay(Timeout.InfiniteTimeSpan).ConfigureAwait(false);
            }

            if (mode == "--invalid-list" && method == "tools/list")
            {
                await Console.Out.WriteLineAsync(
                    "not-json " + Environment.GetEnvironmentVariable("MCP_TEST_SECRET")).ConfigureAwait(false);
                await Console.Out.FlushAsync().ConfigureAwait(false);
                continue;
            }

            if (mode == "--scalar-list" && method == "tools/list")
            {
                await Console.Out.WriteLineAsync(
                    $"{{\"jsonrpc\":\"2.0\",\"id\":{id.GetRawText()},\"result\":[]}}").ConfigureAwait(false);
                await Console.Out.FlushAsync().ConfigureAwait(false);
                continue;
            }

            if (mode == "--array-list" && method == "tools/list")
            {
                await Console.Out.WriteLineAsync("[]").ConfigureAwait(false);
                await Console.Out.FlushAsync().ConfigureAwait(false);
                continue;
            }

            if (mode == "--malformed-tool" && method == "tools/list")
            {
                await Console.Out.WriteLineAsync(
                    $"{{\"jsonrpc\":\"2.0\",\"id\":{id.GetRawText()},\"result\":{{\"tools\":[{{\"name\":\"echo\",\"description\":42,\"inputSchema\":[]}}]}}}}").ConfigureAwait(false);
                await Console.Out.FlushAsync().ConfigureAwait(false);
                continue;
            }

            if (mode == "--missing-jsonrpc-list" && method == "tools/list")
            {
                await Console.Out.WriteLineAsync(
                    $"{{\"id\":{id.GetRawText()},\"result\":{{\"tools\":[]}}}}").ConfigureAwait(false);
                await Console.Out.FlushAsync().ConfigureAwait(false);
                continue;
            }

            if (mode == "--missing-call-result" && method == "tools/call")
            {
                await Console.Out.WriteLineAsync(
                    $"{{\"jsonrpc\":\"2.0\",\"id\":{id.GetRawText()}}}").ConfigureAwait(false);
                await Console.Out.FlushAsync().ConfigureAwait(false);
                continue;
            }

            if (mode == "--scalar-call-result" && method == "tools/call")
            {
                await Console.Out.WriteLineAsync(
                    $"{{\"jsonrpc\":\"2.0\",\"id\":{id.GetRawText()},\"result\":42}}").ConfigureAwait(false);
                await Console.Out.FlushAsync().ConfigureAwait(false);
                continue;
            }

            var response = new JsonObject
            {
                ["jsonrpc"] = "2.0",
                ["id"] = JsonNode.Parse(id.GetRawText())
            };
            response["result"] = method switch
            {
                "initialize" => new JsonObject
                {
                    ["protocolVersion"] = "2025-06-18",
                    ["capabilities"] = new JsonObject(),
                    ["serverInfo"] = new JsonObject
                    {
                        ["name"] = "CanDoItAll.McpTestHost",
                        ["version"] = "1.0.0"
                    }
                },
                "tools/list" => new JsonObject
                {
                    ["tools"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["name"] = "echo",
                            ["description"] = "Deterministic portability echo.",
                            ["inputSchema"] = new JsonObject
                            {
                                ["type"] = "object"
                            }
                        }
                    }
                },
                "tools/call" => new JsonObject
                {
                    ["ok"] = true,
                    ["secretPresent"] = !string.IsNullOrEmpty(
                        Environment.GetEnvironmentVariable("MCP_TEST_SECRET")),
                    ["arguments"] = JsonSerializer.SerializeToNode(arguments)
                },
                _ => new JsonObject()
            };
            await Console.Out.WriteLineAsync(response.ToJsonString()).ConfigureAwait(false);
            await Console.Out.FlushAsync().ConfigureAwait(false);
        }

        return 0;
    }

    private static string? ReadOption(
        IReadOnlyList<string> arguments,
        string option)
    {
        for (var index = 0; index + 1 < arguments.Count; index++)
        {
            if (string.Equals(arguments[index], option, StringComparison.Ordinal))
            {
                return arguments[index + 1];
            }
        }

        return null;
    }

    private static void SignalReady(string? readyFile)
    {
        if (!string.IsNullOrWhiteSpace(readyFile))
        {
            File.WriteAllText(readyFile, "ready");
        }
    }
}
