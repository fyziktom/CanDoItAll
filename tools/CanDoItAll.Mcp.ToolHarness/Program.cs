using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Client;

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    WriteIndented = true
};

var options = HarnessOptions.Parse(args);

var transport = new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "CanDoItAll.Mcp.ToolHarness",
    Command = options.ServerCommand,
    Arguments = options.ServerArguments,
    WorkingDirectory = options.WorkingDirectory,
    ShutdownTimeout = TimeSpan.FromSeconds(15)
});

await using var client = await McpClient.CreateAsync(transport);

if (string.Equals(options.ToolName, "tools/list", StringComparison.OrdinalIgnoreCase))
{
    var tools = await client.ListToolsAsync();
    Console.WriteLine(JsonSerializer.Serialize(tools, jsonOptions));
    return;
}

var result = await client.CallToolAsync(options.ToolName, options.Arguments);
var output = result.StructuredContent is JsonElement structuredContent
    ? (object)structuredContent
    : result.Content;
Console.WriteLine(JsonSerializer.Serialize(output, jsonOptions));

if (result.IsError is true)
{
    Environment.ExitCode = 1;
}

return;

sealed class HarnessOptions
{
    public required string ServerCommand { get; init; }

    public required string[] ServerArguments { get; init; }

    public required string WorkingDirectory { get; init; }

    public required string ToolName { get; init; }

    public required IReadOnlyDictionary<string, object?> Arguments { get; init; }

    public static HarnessOptions Parse(string[] args)
    {
        string? serverAssembly = null;
        string? settingsPath = null;
        string? toolName = null;
        string? workingDirectory = null;
        string? argumentsJson = null;
        string? argumentsFile = null;
        string? serverCommandOverride = null;
        List<string> serverArgs = [];
        List<string> argumentPairs = [];

        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--server-assembly":
                    serverAssembly = GetRequiredValue(args, ref index);
                    break;
                case "--settings":
                    settingsPath = GetRequiredValue(args, ref index);
                    break;
                case "--tool":
                    toolName = GetRequiredValue(args, ref index);
                    break;
                case "--working-directory":
                    workingDirectory = GetRequiredValue(args, ref index);
                    break;
                case "--arguments-json":
                    argumentsJson = GetRequiredValue(args, ref index);
                    break;
                case "--arguments-file":
                    argumentsFile = GetRequiredValue(args, ref index);
                    break;
                case "--server-command":
                    serverCommandOverride = GetRequiredValue(args, ref index);
                    break;
                case "--server-arg":
                    serverArgs.Add(GetRequiredValue(args, ref index));
                    break;
                case "--arg":
                    argumentPairs.Add(GetRequiredValue(args, ref index));
                    break;
            }
        }

        if (string.IsNullOrWhiteSpace(toolName))
        {
            throw new InvalidOperationException("--tool is required.");
        }

        if (!string.IsNullOrWhiteSpace(argumentsFile))
        {
            argumentsJson = File.ReadAllText(argumentsFile);
        }

        IReadOnlyDictionary<string, object?> arguments = string.IsNullOrWhiteSpace(argumentsJson)
            ? ParseArgumentPairs(argumentPairs)
            : ParseArguments(argumentsJson);

        if (!string.IsNullOrWhiteSpace(serverCommandOverride))
        {
            return new HarnessOptions
            {
                ServerCommand = serverCommandOverride,
                ServerArguments = serverArgs.ToArray(),
                WorkingDirectory = workingDirectory ?? Environment.CurrentDirectory,
                ToolName = toolName,
                Arguments = arguments
            };
        }

        if (string.IsNullOrWhiteSpace(serverAssembly))
        {
            throw new InvalidOperationException("Either --server-command or --server-assembly must be supplied.");
        }

        List<string> effectiveArgs =
        [
            serverAssembly
        ];

        if (!string.IsNullOrWhiteSpace(settingsPath))
        {
            effectiveArgs.Add("--settings");
            effectiveArgs.Add(settingsPath);
        }

        return new HarnessOptions
        {
            ServerCommand = "dotnet",
            ServerArguments = effectiveArgs.ToArray(),
            WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(serverAssembly)!,
            ToolName = toolName,
            Arguments = arguments
        };
    }

    private static string GetRequiredValue(string[] args, ref int index)
    {
        if (index >= args.Length - 1)
        {
            throw new InvalidOperationException($"Missing value for argument '{args[index]}'.");
        }

        index++;
        return args[index];
    }

    private static IReadOnlyDictionary<string, object?> ParseArguments(string json)
    {
        var node = JsonNode.Parse(json) as JsonObject
            ?? throw new InvalidOperationException("--arguments-json must parse to a JSON object.");

        return node.ToDictionary(
            pair => pair.Key,
            pair => pair.Value?.Deserialize<object?>());
    }

    private static IReadOnlyDictionary<string, object?> ParseArgumentPairs(IReadOnlyList<string> pairs)
    {
        Dictionary<string, object?> arguments = new(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in pairs)
        {
            var separatorIndex = pair.IndexOf('=');
            if (separatorIndex <= 0)
            {
                throw new InvalidOperationException($"--arg '{pair}' must use the format key=value.");
            }

            var key = pair[..separatorIndex];
            var value = pair[(separatorIndex + 1)..];
            arguments[key] = ParseScalarOrJson(value);
        }

        return arguments;
    }

    private static object? ParseScalarOrJson(string value)
    {
        if (string.Equals(value, "null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (bool.TryParse(value, out var boolValue))
        {
            return boolValue;
        }

        if (int.TryParse(value, out var intValue))
        {
            return intValue;
        }

        if (long.TryParse(value, out var longValue))
        {
            return longValue;
        }

        if ((value.StartsWith("{", StringComparison.Ordinal) && value.EndsWith("}", StringComparison.Ordinal)) ||
            (value.StartsWith("[", StringComparison.Ordinal) && value.EndsWith("]", StringComparison.Ordinal)))
        {
            return JsonNode.Parse(value)?.Deserialize<object?>();
        }

        return value;
    }
}
