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

            if (mode == "--exit-before-list" && method == "tools/list")
            {
                return 0;
            }

            if (mode == "--stderr-exit-before-list" && method == "tools/list")
            {
                await Console.Error.WriteAsync(
                    new string('x', 20 * 1024) +
                    " token=stdio-secret-that-must-not-leak").ConfigureAwait(false);
                await Console.Error.FlushAsync().ConfigureAwait(false);
                return 7;
            }

            if (mode == "--ping-before-response" &&
                method is "tools/list" or "tools/call" &&
                !await ExchangePeerRequestAsync(
                    $"ping-{method}",
                    "ping",
                    expectedErrorCode: null).ConfigureAwait(false))
            {
                return 31;
            }

            if (mode == "--unsupported-peer-request" &&
                method == "tools/list" &&
                !await ExchangePeerRequestAsync(
                    "unsupported-1",
                    "roots/list",
                    expectedErrorCode: -32601).ConfigureAwait(false))
            {
                return 32;
            }

            if (mode == "--notification-before-list" && method == "tools/list")
            {
                await WriteLineAsync(
                    "{\"jsonrpc\":\"2.0\",\"method\":\"notifications/progress\",\"params\":{}}")
                    .ConfigureAwait(false);
            }

            if (mode == "--excessive-unmatched" && method == "tools/list")
            {
                for (var index = 0; index < 65; index++)
                {
                    await WriteLineAsync(
                        $"{{\"jsonrpc\":\"2.0\",\"method\":\"notifications/progress\",\"params\":{{\"progress\":{index}}}}}")
                        .ConfigureAwait(false);
                }

                continue;
            }

            if (mode == "--overlong-list" && method == "tools/list")
            {
                await WriteLineAsync(new string('x', 8 * 1024 * 1024 + 1))
                    .ConfigureAwait(false);
                continue;
            }

            if (mode == "--deep-list" && method == "tools/list")
            {
                await WriteLineAsync(
                    new string('[', 66) + "0" + new string(']', 66))
                    .ConfigureAwait(false);
                continue;
            }

            if (mode == "--duplicate-peer-id" && method == "tools/list")
            {
                if (!await ExchangePeerRequestAsync(
                        "duplicate-1",
                        "ping",
                        expectedErrorCode: null).ConfigureAwait(false))
                {
                    return 33;
                }

                await WriteLineAsync(
                    "{\"jsonrpc\":\"2.0\",\"id\":\"duplicate-1\",\"method\":\"ping\"}")
                    .ConfigureAwait(false);
                await Console.In.ReadLineAsync().ConfigureAwait(false);
                return 0;
            }

            if (mode == "--invalid-peer-id" && method == "tools/list")
            {
                await WriteLineAsync(
                    "{\"jsonrpc\":\"2.0\",\"id\":{},\"method\":\"ping\"}")
                    .ConfigureAwait(false);
                continue;
            }

            if (mode == "--duplicate-id-property" && method == "tools/list")
            {
                await WriteLineAsync(
                    "{\"jsonrpc\":\"2.0\",\"id\":7,\"id\":8,\"method\":\"ping\"}")
                    .ConfigureAwait(false);
                continue;
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
            if (mode == "--exit-after-list" && method == "tools/list")
            {
                return 0;
            }
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

    private static async Task<bool> ExchangePeerRequestAsync(
        string id,
        string method,
        int? expectedErrorCode)
    {
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["method"] = method
        };
        await WriteLineAsync(request.ToJsonString()).ConfigureAwait(false);

        var line = await Console.In.ReadLineAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        using var response = JsonDocument.Parse(line);
        var root = response.RootElement;
        if (!root.TryGetProperty("jsonrpc", out var jsonRpc) ||
            jsonRpc.GetString() != "2.0" ||
            !root.TryGetProperty("id", out var responseId) ||
            responseId.GetString() != id)
        {
            return false;
        }

        if (expectedErrorCode is null)
        {
            return root.TryGetProperty("result", out var result) &&
                result.ValueKind == JsonValueKind.Object;
        }

        return root.TryGetProperty("error", out var error) &&
            error.ValueKind == JsonValueKind.Object &&
            error.TryGetProperty("code", out var code) &&
            code.TryGetInt32(out var actualCode) &&
            actualCode == expectedErrorCode;
    }

    private static async Task WriteLineAsync(string value)
    {
        await Console.Out.WriteLineAsync(value).ConfigureAwait(false);
        await Console.Out.FlushAsync().ConfigureAwait(false);
    }
}
