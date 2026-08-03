using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class OllamaToolResultProtocolHandler(HttpMessageHandler innerHandler)
    : DelegatingHandler(innerHandler)
{
    private const string AssistantRole = "assistant";
    private const string ContentLengthHeaderName = "Content-Length";
    private const string ContentPropertyName = "content";
    private const string ContentTypeHeaderName = "Content-Type";
    private const string FunctionCallIdPropertyName = "callId";
    private const string FunctionPropertyName = "function";
    private const string FunctionResultPropertyName = "result";
    private const string MessagesPropertyName = "messages";
    private const string NamePropertyName = "name";
    private const string RolePropertyName = "role";
    private const string ToolCallIdPropertyName = "id";
    private const string ToolCallsPropertyName = "tool_calls";
    private const string ToolNamePropertyName = "tool_name";
    private const string ToolRole = "tool";

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.Method != HttpMethod.Post ||
            request.RequestUri?.AbsolutePath.EndsWith("/api/chat", StringComparison.OrdinalIgnoreCase) != true ||
            request.Content is null)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        var originalContent = request.Content;
        var preservedHeaders = originalContent.Headers
            .Where(header =>
                !string.Equals(header.Key, ContentLengthHeaderName, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(header.Key, ContentTypeHeaderName, StringComparison.OrdinalIgnoreCase))
            .Select(header => (header.Key, Values: header.Value.ToArray()))
            .ToArray();
        await using var requestStream = await originalContent.ReadAsStreamAsync(cancellationToken);
        var requestBody = await JsonNode.ParseAsync(
                requestStream,
                cancellationToken: cancellationToken) as JsonObject
            ?? throw new InvalidOperationException("Ollama chat request content could not be deserialized.");
        var messages = requestBody[MessagesPropertyName] as JsonArray
            ?? throw new InvalidOperationException("Ollama chat request does not contain a messages array.");
        NormalizeToolResults(messages);
        var normalizedContent = new StringContent(
            requestBody.ToJsonString(),
            Encoding.UTF8,
            "application/json");
        foreach (var header in preservedHeaders)
        {
            normalizedContent.Headers.TryAddWithoutValidation(header.Key, header.Values);
        }

        request.Content = normalizedContent;
        originalContent.Dispose();

        return await base.SendAsync(request, cancellationToken);
    }

    private static void NormalizeToolResults(JsonArray messages)
    {
        var pendingCalls = new List<PendingToolCall>();
        foreach (var message in messages.OfType<JsonObject>())
        {
            var role = message[RolePropertyName]?.GetValue<string>();
            if (string.Equals(role, AssistantRole, StringComparison.Ordinal))
            {
                AddPendingCalls(message[ToolCallsPropertyName] as JsonArray, pendingCalls);
                continue;
            }

            if (!string.Equals(role, ToolRole, StringComparison.Ordinal))
            {
                continue;
            }

            var existingToolName = message[ToolNamePropertyName]?.GetValue<string>();
            var functionResult = string.IsNullOrWhiteSpace(existingToolName)
                ? ParseFunctionResult(message[ContentPropertyName]?.GetValue<string>())
                : new NormalizedFunctionResult(
                    string.Empty,
                    message[ContentPropertyName]?.GetValue<string>() ?? string.Empty);
            var pendingCallIndex = ResolvePendingCallIndex(
                pendingCalls,
                functionResult.CallId,
                existingToolName);
            var pendingCall = pendingCalls[pendingCallIndex];
            pendingCalls.RemoveAt(pendingCallIndex);

            message[ToolNamePropertyName] = pendingCall.Name;
            message[ContentPropertyName] = functionResult.Content;
        }
    }

    private static void AddPendingCalls(
        JsonArray? toolCalls,
        ICollection<PendingToolCall> pendingCalls)
    {
        foreach (var toolCall in toolCalls?.OfType<JsonObject>() ?? [])
        {
            var function = toolCall[FunctionPropertyName] as JsonObject;
            var name = function?[NamePropertyName]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("Ollama function call does not contain a tool name.");
            }

            pendingCalls.Add(new PendingToolCall(
                toolCall[ToolCallIdPropertyName]?.GetValue<string>(),
                name));
        }
    }

    private static NormalizedFunctionResult ParseFunctionResult(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new NormalizedFunctionResult(string.Empty, string.Empty);
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !TryGetPropertyIgnoreCase(
                    document.RootElement,
                    FunctionCallIdPropertyName,
                    out var callIdElement) ||
                !TryGetPropertyIgnoreCase(
                    document.RootElement,
                    FunctionResultPropertyName,
                    out var result))
            {
                return new NormalizedFunctionResult(string.Empty, content);
            }

            var callId = callIdElement.ValueKind == JsonValueKind.String
                ? callIdElement.GetString()
                : null;
            if (string.IsNullOrWhiteSpace(callId))
            {
                throw new InvalidOperationException(
                    "OllamaSharp function-result envelope does not contain a valid call identifier.");
            }

            var normalizedContent = result.ValueKind == JsonValueKind.String
                ? result.GetString() ?? string.Empty
                : result.GetRawText();
            return new NormalizedFunctionResult(callId, normalizedContent);
        }
        catch (JsonException)
        {
            return new NormalizedFunctionResult(string.Empty, content);
        }
    }

    private static bool TryGetPropertyIgnoreCase(
        JsonElement element,
        string propertyName,
        out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static int ResolvePendingCallIndex(
        IReadOnlyList<PendingToolCall> pendingCalls,
        string resultCallId,
        string? toolName)
    {
        if (pendingCalls.Count == 0)
        {
            throw new InvalidOperationException(
                "Ollama tool result cannot be correlated with a preceding function call.");
        }

        if (!string.IsNullOrWhiteSpace(resultCallId))
        {
            for (var index = 0; index < pendingCalls.Count; index++)
            {
                if (string.Equals(pendingCalls[index].CallId, resultCallId, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            if (pendingCalls.Any(call => !string.IsNullOrWhiteSpace(call.CallId)))
            {
                throw new InvalidOperationException(
                    "Ollama tool result call identifier does not match a preceding function call.");
            }
        }

        if (!string.IsNullOrWhiteSpace(toolName))
        {
            for (var index = 0; index < pendingCalls.Count; index++)
            {
                if (string.Equals(pendingCalls[index].Name, toolName, StringComparison.Ordinal))
                {
                    return index;
                }
            }

            throw new InvalidOperationException(
                "Ollama tool name does not match a preceding function call.");
        }

        return 0;
    }

    private sealed record PendingToolCall(string? CallId, string Name);

    private sealed record NormalizedFunctionResult(string CallId, string Content);
}
