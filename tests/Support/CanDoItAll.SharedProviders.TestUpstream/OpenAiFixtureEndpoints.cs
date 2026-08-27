using System.Text.Json;

namespace CanDoItAll.SharedProviders.TestUpstream;

internal static class OpenAiFixtureEndpointRouteBuilderExtensions
{
    private const long CreatedAt = 1_700_000_000;
    private const string ImageGenerationCompletedText =
        "deterministic fixture response: generated image at shared-provider-ui/generated.png";
    private const string StructuredJson = "{\"result\":\"fixture\",\"value\":42}";
    private static readonly TokenUsage ChatUsage = new(5, 3, 8);
    private static readonly ResponseUsage ResponsesUsage = new(5, 3, 8);

    public static IEndpointRouteBuilder MapOpenAiFixtureEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1");
        group.MapGet("/models", WriteModelsAsync);
        group.MapPost("/chat/completions", WriteChatCompletionAsync);
        group.MapPost("/responses", WriteResponseAsync);
        group.MapPost("/images/generations", WriteImageGenerationAsync);
        return endpoints;
    }

    private static async Task WriteModelsAsync(HttpContext context, TestControlState control)
    {
        if (await TestFailureResponder.TryWriteAsync(context, control, FixtureSurface.Models))
        {
            return;
        }

        await WriteJsonAsync(
            context,
            new ModelListResponse(
                "list",
                [
                    new("e2e-duplicate-model", "model", CreatedAt, "candoitall-fixture"),
                    new("e2e-structured-allow", "model", CreatedAt, "candoitall-fixture"),
                    new("e2e-openai-image", "model", CreatedAt, "candoitall-fixture"),
                    new("e2e-comfyui-image", "model", CreatedAt, "candoitall-fixture"),
                    new("e2e-unshared", "model", CreatedAt, "candoitall-fixture"),
                    new("e2e-client-a-personal", "model", CreatedAt, "candoitall-fixture")
                ]));
    }

    private static async Task WriteChatCompletionAsync(
        HttpContext context,
        ChatCompletionRequest request,
        TestControlState control)
    {
        var controlSnapshot = control.Get();
        if (await TestFailureResponder.TryWriteAsync(
                context,
                controlSnapshot,
                FixtureSurface.ChatCompletions))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Model) || request.Messages is null || request.Messages.Count == 0)
        {
            await WriteValidationErrorAsync(context, "A model and at least one message are required.");
            return;
        }

        var streamMode = controlSnapshot.ResolveStreamMode(FixtureSurface.ChatCompletions);
        if (!request.Stream && streamMode == FixtureStreamMode.HoldAfterFirstFrame)
        {
            await WriteValidationErrorAsync(
                context,
                "The deterministic hold mode requires a streamed request.");
            return;
        }

        if (request.Stream)
        {
            await WriteChatStreamAsync(
                context,
                request,
                streamMode);
            return;
        }

        await WriteJsonAsync(context, CreateChatCompletion(request));
    }

    private static async Task WriteResponseAsync(
        HttpContext context,
        ResponsesRequest request,
        TestControlState control)
    {
        var controlSnapshot = control.Get();
        if (await TestFailureResponder.TryWriteAsync(
                context,
                controlSnapshot,
                FixtureSurface.Responses))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Model) || request.Input.ValueKind == JsonValueKind.Undefined)
        {
            await WriteValidationErrorAsync(context, "A model and input are required.");
            return;
        }

        var streamMode = controlSnapshot.ResolveStreamMode(FixtureSurface.Responses);
        if (!request.Stream && streamMode == FixtureStreamMode.HoldAfterFirstFrame)
        {
            await WriteValidationErrorAsync(
                context,
                "The deterministic hold mode requires a streamed request.");
            return;
        }

        if (request.Stream)
        {
            await WriteResponsesStreamAsync(
                context,
                request,
                streamMode);
            return;
        }

        await WriteJsonAsync(context, CreateResponse(request));
    }

    private static async Task WriteImageGenerationAsync(
        HttpContext context,
        ImageGenerationRequest request,
        TestControlState control)
    {
        if (await TestFailureResponder.TryWriteAsync(context, control, FixtureSurface.ImageGenerations))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(request.Model) ||
            string.IsNullOrWhiteSpace(request.Prompt) ||
            request.N is < 1 or > 4 ||
            !string.Equals(request.ResponseFormat, "b64_json", StringComparison.Ordinal) ||
            !FixtureImages.TryGet(request.OutputFormat, out var image))
        {
            await WriteValidationErrorAsync(
                context,
                "The image request requires a model, prompt, 1-4 images, b64_json, and png, jpeg, or webp output.");
            return;
        }

        var data = Enumerable.Range(0, request.N)
            .Select(_ => new ImageGenerationData(
                Convert.ToBase64String(image.Bytes),
                "deterministic fixture image"))
            .ToArray();
        await WriteJsonAsync(context, new ImageGenerationResponse(CreatedAt, data));
    }

    private static ChatCompletionResponse CreateChatCompletion(ChatCompletionRequest request)
    {
        var tool = ResolveChatTool(request);
        var message = tool is null
            ? new ChatAssistantMessage(
                "assistant",
                ResolveChatResponseText(request))
            : new ChatAssistantMessage(
                "assistant",
                null,
                [
                    new ChatToolCall(
                        "call_fixture",
                        "function",
                        new ChatFunctionCall(tool.Function.Name, ResolveToolArguments(tool.Function.Name)))
                ]);
        return new ChatCompletionResponse(
            "chatcmpl-fixture",
            "chat.completion",
            CreatedAt,
            request.Model,
            [new ChatChoice(0, message, tool is null ? "stop" : "tool_calls")],
            ChatUsage);
    }

    private static ResponsesResponse CreateResponse(ResponsesRequest request)
    {
        var tool = ResolveResponseTool(request);
        var output = tool is null
            ? new ResponseOutputItem(
                "msg_fixture",
                "message",
                "completed",
                "assistant",
                [
                    new ResponseOutputContent(
                        "output_text",
                        ResolveResponseText(request),
                        [])
                ])
            : new ResponseOutputItem(
                "fc_fixture",
                "function_call",
                "completed",
                CallId: "call_fixture",
                Name: tool.Name,
                Arguments: ResolveToolArguments(tool.Name));
        return new ResponsesResponse(
            "resp-fixture",
            "response",
            CreatedAt,
            "completed",
            request.Model,
            [output],
            ResponsesUsage);
    }

    private static async Task WriteChatStreamAsync(
        HttpContext context,
        ChatCompletionRequest request,
        FixtureStreamMode streamMode)
    {
        PrepareStream(context);
        var tool = ResolveChatTool(request);
        var chunks = tool is null
            ? CreateTextChatChunks(request)
            : CreateToolChatChunks(request, tool);
        await WriteSseSequenceAsync(context, chunks.Select(chunk => (
            EventName: (string?)null,
            Payload: chunk)), streamMode);
    }

    private static IReadOnlyList<ChatStreamChunk> CreateTextChatChunks(ChatCompletionRequest request)
    {
        var content = ResolveChatResponseText(request);
        var splitIndex = Math.Max(1, content.Length / 2);
        return
        [
            CreateChatChunk(request.Model, new ChatStreamDelta(Role: "assistant")),
            CreateChatChunk(request.Model, new ChatStreamDelta(Content: content[..splitIndex])),
            CreateChatChunk(request.Model, new ChatStreamDelta(Content: content[splitIndex..])),
            CreateChatChunk(request.Model, new ChatStreamDelta(), "stop", ChatUsage)
        ];
    }

    private static IReadOnlyList<ChatStreamChunk> CreateToolChatChunks(
        ChatCompletionRequest request,
        ChatToolRequest tool)
        =>
        [
            CreateChatChunk(request.Model, new ChatStreamDelta(Role: "assistant")),
            CreateChatChunk(
                request.Model,
                new ChatStreamDelta(ToolCalls:
                [
                    new ChatStreamToolCall(
                        0,
                        "call_fixture",
                        "function",
                        new ChatStreamFunctionCall(tool.Function.Name, string.Empty))
                ])),
            CreateChatChunk(
                request.Model,
                new ChatStreamDelta(ToolCalls:
                [
                    new ChatStreamToolCall(
                        0,
                        Function: new ChatStreamFunctionCall(Arguments: ResolveToolArguments(tool.Function.Name)))
                ])),
            CreateChatChunk(request.Model, new ChatStreamDelta(), "tool_calls", ChatUsage)
        ];

    private static ChatToolRequest? ResolveChatTool(ChatCompletionRequest request)
    {
        var tools = request.Tools?
            .Where(candidate =>
                string.Equals(candidate.Type, "function", StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(candidate.Function?.Name))
            .ToList();
        if (tools is null || tools.Count == 0)
        {
            return null;
        }

        if (HasToolResult(request.Messages))
        {
            return null;
        }

        var imageTool = tools.FirstOrDefault(candidate =>
            string.Equals(candidate.Function.Name, "image_generation_create", StringComparison.Ordinal));
        if (imageTool is null)
        {
            return tools[0];
        }

        return !RequestsImageGeneration(request.Messages)
            ? null
            : imageTool;
    }

    private static ResponseToolRequest? ResolveResponseTool(ResponsesRequest request)
    {
        var tools = request.Tools?
            .Where(candidate =>
                string.Equals(candidate.Type, "function", StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(candidate.Name))
            .ToList();
        if (tools is null || tools.Count == 0)
        {
            return null;
        }

        var imageTool = tools.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, "image_generation_create", StringComparison.Ordinal));
        if (imageTool is null)
        {
            return tools[0];
        }

        var input = request.Input.GetRawText();
        return input.Contains("function_call_output", StringComparison.OrdinalIgnoreCase) ||
               !RequestsImageGeneration(input)
            ? null
            : imageTool;
    }

    private static string ResolveChatResponseText(ChatCompletionRequest request)
    {
        if (request.ResponseFormat.HasValue)
        {
            return StructuredJson;
        }

        if (HasToolResult(request.Messages) && RequestsImageGeneration(request.Messages))
        {
            return ImageGenerationCompletedText;
        }

        return request.Messages.Any(message =>
            GetMessageContentJson(message).Contains("image_url", StringComparison.OrdinalIgnoreCase))
            ? "deterministic fixture analyzed the attached image"
            : "deterministic fixture response";
    }

    private static string ResolveResponseText(ResponsesRequest request)
    {
        if (request.Text.HasValue)
        {
            return StructuredJson;
        }

        return request.Input.GetRawText().Contains("input_image", StringComparison.OrdinalIgnoreCase)
            ? "deterministic fixture analyzed the attached image"
            : "deterministic fixture response";
    }

    private static bool HasToolResult(IEnumerable<ChatMessageRequest> messages)
        => messages.Any(message => string.Equals(message.Role, "tool", StringComparison.OrdinalIgnoreCase));

    private static bool RequestsImageGeneration(IEnumerable<ChatMessageRequest> messages)
        => messages
            .Where(message => string.Equals(
                message.Role,
                "user",
                StringComparison.OrdinalIgnoreCase))
            .Any(message => RequestsImageGeneration(GetMessageContentJson(message)));

    private static string GetMessageContentJson(ChatMessageRequest message)
    {
        if (message.Content is not { } content || content.ValueKind == JsonValueKind.Undefined)
        {
            return string.Empty;
        }

        return content.GetRawText();
    }

    private static bool RequestsImageGeneration(string text)
        => text.Contains("generate an image", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("create an image", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("generate image", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("create image", StringComparison.OrdinalIgnoreCase);

    private static string ResolveToolArguments(string toolName)
        => string.Equals(toolName, "image_generation_create", StringComparison.Ordinal)
            ? "{\"request\":{\"prompt\":\"deterministic shared provider image\",\"outputWorkspacePath\":\"shared-provider-ui/generated.png\",\"outputFormat\":\"png\"}}"
            : "{\"value\":\"fixture\"}";

    private static ChatStreamChunk CreateChatChunk(
        string model,
        ChatStreamDelta delta,
        string? finishReason = null,
        TokenUsage? usage = null)
        => new(
            "chatcmpl-fixture-stream",
            "chat.completion.chunk",
            CreatedAt,
            model,
            [new ChatStreamChoice(0, delta, finishReason)],
            usage);

    private static async Task WriteResponsesStreamAsync(
        HttpContext context,
        ResponsesRequest request,
        FixtureStreamMode streamMode)
    {
        PrepareStream(context);
        var completed = CreateResponse(request);
        var content = completed.Output[0].Content?.FirstOrDefault()?.Text ?? string.Empty;
        var splitIndex = Math.Max(1, content.Length / 2);
        var created = new ResponseCreatedEvent(
            "response.created",
            0,
            new ResponseSummary(
                completed.Id,
                completed.Object,
                completed.CreatedAt,
                "in_progress",
                completed.Model,
                [],
                null));
        var events = new (string? EventName, object Payload)[]
        {
            (created.Type, created),
            (
                "response.output_text.delta",
                new ResponseOutputTextDeltaEvent(
                    "response.output_text.delta",
                    1,
                    0,
                    0,
                    content.Length == 0 ? string.Empty : content[..splitIndex])),
            (
                "response.output_text.delta",
                new ResponseOutputTextDeltaEvent(
                    "response.output_text.delta",
                    2,
                    0,
                    0,
                    content.Length == 0 ? string.Empty : content[splitIndex..])),
            (
                "response.completed",
                new ResponseCompletedEvent("response.completed", 3, completed))
        };
        await WriteSseSequenceAsync(context, events, streamMode);
    }

    private static async Task WriteSseSequenceAsync<T>(
        HttpContext context,
        IEnumerable<(string? EventName, T Payload)> events,
        FixtureStreamMode streamMode)
    {
        try
        {
            var frameIndex = 0;
            foreach (var (eventName, payload) in events)
            {
                if (eventName is not null)
                {
                    await context.Response.WriteAsync($"event: {eventName}\n", context.RequestAborted);
                }

                var json = JsonSerializer.Serialize(payload, FixtureJson.Options);
                await context.Response.WriteAsync($"data: {json}\n\n", context.RequestAborted);
                await context.Response.Body.FlushAsync(context.RequestAborted);
                frameIndex++;
                if (await StopAfterFrameAsync(context, streamMode, frameIndex))
                {
                    return;
                }
            }

            await context.Response.WriteAsync("data: [DONE]\n\n", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
        }
    }

    private static async Task WriteSseSequenceAsync(
        HttpContext context,
        IEnumerable<(string? EventName, object Payload)> events,
        FixtureStreamMode streamMode)
    {
        try
        {
            var frameIndex = 0;
            foreach (var (eventName, payload) in events)
            {
                if (eventName is not null)
                {
                    await context.Response.WriteAsync($"event: {eventName}\n", context.RequestAborted);
                }

                var json = JsonSerializer.Serialize(payload, payload.GetType(), FixtureJson.Options);
                await context.Response.WriteAsync($"data: {json}\n\n", context.RequestAborted);
                await context.Response.Body.FlushAsync(context.RequestAborted);
                frameIndex++;
                if (await StopAfterFrameAsync(context, streamMode, frameIndex))
                {
                    return;
                }
            }

            await context.Response.WriteAsync("data: [DONE]\n\n", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
        }
    }

    private static async Task<bool> StopAfterFrameAsync(
        HttpContext context,
        FixtureStreamMode streamMode,
        int frameIndex)
    {
        if (streamMode == FixtureStreamMode.HoldAfterFirstFrame && frameIndex == 1)
        {
            await Task.Delay(FixtureLimits.MaximumControlledTimeout, context.RequestAborted);
            return true;
        }

        await Task.Delay(FixtureLimits.StreamChunkDelay, context.RequestAborted);
        return false;
    }

    private static void PrepareStream(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers["x-request-id"] = "fixture-stream-request";
    }

    private static async Task WriteValidationErrorAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(
            new FixtureErrorEnvelope(new FixtureError(
                message,
                "fixture_validation_error",
                "fixture_validation_error")),
            FixtureJson.Options,
            context.RequestAborted);
    }

    private static async Task WriteJsonAsync<T>(HttpContext context, T response)
    {
        context.Response.Headers["x-request-id"] = "fixture-request";
        await context.Response.WriteAsJsonAsync(response, FixtureJson.Options, context.RequestAborted);
    }
}
