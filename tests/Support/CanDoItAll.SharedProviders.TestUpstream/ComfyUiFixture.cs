using System.Text.Json;

namespace CanDoItAll.SharedProviders.TestUpstream;

public sealed record ComfyUiSystemStatsResponse(
    ComfyUiSystemInfo System,
    IReadOnlyList<ComfyUiDeviceInfo> Devices);

public sealed record ComfyUiSystemInfo(string Os, long RamTotal, long RamFree);

public sealed record ComfyUiDeviceInfo(
    string Name,
    string Type,
    long VramTotal,
    long VramFree);

public sealed record ComfyUiPromptRequest(JsonElement Prompt);

public sealed record ComfyUiPromptResponse(
    string PromptId,
    long Number,
    IReadOnlyDictionary<string, IReadOnlyList<string>> NodeErrors);

public sealed record ComfyUiHistoryEntry(
    IReadOnlyDictionary<string, ComfyUiOutput> Outputs,
    ComfyUiExecutionStatus Status);

public sealed record ComfyUiOutput(IReadOnlyList<ComfyUiImageReference> Images);

public sealed record ComfyUiImageReference(string Filename, string Subfolder, string Type);

public sealed record ComfyUiExecutionStatus(bool Completed, IReadOnlyList<JsonElement> Messages);

internal sealed class ComfyUiFixtureState
{
    private readonly object sync = new();
    private readonly LinkedList<ComfyUiPromptState> prompts = new();
    private readonly Dictionary<string, LinkedListNode<ComfyUiPromptState>> promptsById =
        new(StringComparer.Ordinal);
    private long sequence;

    public ComfyUiPromptState Enqueue(JsonElement prompt)
    {
        var number = Interlocked.Increment(ref sequence);
        var promptId = $"fixture-prompt-{number:D4}";
        var outputNodeId = FindOutputNodeId(prompt);
        var state = new ComfyUiPromptState(
            promptId,
            number,
            outputNodeId,
            $"{promptId}.png");
        lock (sync)
        {
            var node = prompts.AddLast(state);
            promptsById[promptId] = node;
            while (prompts.Count > FixtureLimits.MaximumComfyUiPrompts)
            {
                var oldest = prompts.First!;
                prompts.RemoveFirst();
                promptsById.Remove(oldest.Value.PromptId);
            }
        }

        return state;
    }

    public bool TryGet(string promptId, out ComfyUiPromptState state)
    {
        lock (sync)
        {
            if (promptsById.TryGetValue(promptId, out var node))
            {
                state = node.Value;
                return true;
            }
        }

        state = default!;
        return false;
    }

    public bool ContainsFile(string filename)
    {
        lock (sync)
        {
            return prompts.Any(prompt => string.Equals(
                prompt.FileName,
                filename,
                StringComparison.Ordinal));
        }
    }

    private static string FindOutputNodeId(JsonElement prompt)
    {
        if (prompt.ValueKind == JsonValueKind.Object)
        {
            foreach (var node in prompt.EnumerateObject())
            {
                if (node.Value.ValueKind == JsonValueKind.Object &&
                    node.Value.TryGetProperty("class_type", out var classType) &&
                    classType.ValueKind == JsonValueKind.String &&
                    classType.GetString()?.Contains("SaveImage", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return node.Name;
                }
            }
        }

        return "9";
    }
}

internal sealed record ComfyUiPromptState(
    string PromptId,
    long Number,
    string OutputNodeId,
    string FileName);

internal static class ComfyUiFixtureEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapComfyUiFixtureEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/system_stats", WriteSystemStatsAsync);
        endpoints.MapPost("/prompt", WritePromptAsync);
        endpoints.MapGet("/history/{promptId}", WriteHistoryAsync);
        endpoints.MapGet("/view", WriteViewAsync);
        return endpoints;
    }

    private static async Task WriteSystemStatsAsync(HttpContext context, TestControlState control)
    {
        if (await TestFailureResponder.TryWriteAsync(
                context,
                control,
                FixtureSurface.ComfyUiSystemStats))
        {
            return;
        }

        await context.Response.WriteAsJsonAsync(
            new ComfyUiSystemStatsResponse(
                new ComfyUiSystemInfo("fixture", 1_073_741_824, 536_870_912),
                [new ComfyUiDeviceInfo("fixture-device", "cpu", 536_870_912, 268_435_456)]),
            FixtureJson.Options,
            context.RequestAborted);
    }

    private static async Task WritePromptAsync(
        HttpContext context,
        ComfyUiPromptRequest request,
        TestControlState control,
        ComfyUiFixtureState state)
    {
        if (await TestFailureResponder.TryWriteAsync(context, control, FixtureSurface.ComfyUiPrompt))
        {
            return;
        }

        if (request.Prompt.ValueKind != JsonValueKind.Object)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(
                new FixtureErrorEnvelope(new FixtureError(
                    "A ComfyUI prompt object is required.",
                    "fixture_validation_error",
                    "fixture_validation_error")),
                FixtureJson.Options,
                context.RequestAborted);
            return;
        }

        var prompt = state.Enqueue(request.Prompt);
        await context.Response.WriteAsJsonAsync(
            new ComfyUiPromptResponse(
                prompt.PromptId,
                prompt.Number,
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)),
            FixtureJson.Options,
            context.RequestAborted);
    }

    private static async Task WriteHistoryAsync(
        HttpContext context,
        string promptId,
        TestControlState control,
        ComfyUiFixtureState state)
    {
        if (await TestFailureResponder.TryWriteAsync(context, control, FixtureSurface.ComfyUiHistory))
        {
            return;
        }

        if (!state.TryGet(promptId, out var prompt))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(
                new Dictionary<string, ComfyUiHistoryEntry>(StringComparer.Ordinal),
                FixtureJson.Options,
                context.RequestAborted);
            return;
        }

        IReadOnlyDictionary<string, ComfyUiOutput> outputs =
            new Dictionary<string, ComfyUiOutput>(StringComparer.Ordinal)
            {
                [prompt.OutputNodeId] = new(
                    [new ComfyUiImageReference(prompt.FileName, string.Empty, "output")])
            };
        var history = new Dictionary<string, ComfyUiHistoryEntry>(StringComparer.Ordinal)
        {
            [prompt.PromptId] = new(
                outputs,
                new ComfyUiExecutionStatus(true, []))
        };
        await context.Response.WriteAsJsonAsync(
            history,
            FixtureJson.Options,
            context.RequestAborted);
    }

    private static async Task WriteViewAsync(
        HttpContext context,
        string filename,
        string? subfolder,
        string? type,
        TestControlState control,
        ComfyUiFixtureState state)
    {
        if (await TestFailureResponder.TryWriteAsync(context, control, FixtureSurface.ComfyUiView))
        {
            return;
        }

        if (!state.ContainsFile(filename) ||
            !string.IsNullOrEmpty(subfolder) ||
            !string.Equals(type, "output", StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        FixtureImages.TryGet("png", out var image);
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = image.ContentType;
        context.Response.ContentLength = image.Bytes.Length;
        await context.Response.Body.WriteAsync(image.Bytes, context.RequestAborted);
    }
}
