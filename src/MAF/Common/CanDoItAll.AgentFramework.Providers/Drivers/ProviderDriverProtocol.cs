using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Providers;

internal static class ProviderDriverProtocol
{
    public static object[] BuildChatMessages(ProviderChatCompletionRequest request)
        => BuildOpenAiChatMessages(request);

    public static object[] BuildOpenAiChatMessages(ProviderChatCompletionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new { role = "system", content = request.SystemPrompt.Trim() });
        }

        messages.AddRange(request.Messages
            .OrderBy(message => message.CreatedAtUtc)
            .Where(message => !string.IsNullOrWhiteSpace(message.Content))
            .Select(message => new { role = MapRole(message.Role), content = message.Content.Trim() }));
        var attachments = NormalizeImageAttachments(request.Attachments);
        if (!string.IsNullOrWhiteSpace(request.Prompt) || attachments.Count > 0)
        {
            messages.Add(new
            {
                role = "user",
                content = CreateOpenAiUserContent(request.Prompt, attachments)
            });
        }

        return messages.ToArray();
    }

    public static object[] BuildOpenAiResponsesInput(ProviderChatCompletionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new { role = "system", content = request.SystemPrompt.Trim() });
        }

        messages.AddRange(request.Messages
            .OrderBy(message => message.CreatedAtUtc)
            .Where(message => !string.IsNullOrWhiteSpace(message.Content))
            .Select(message => new { role = MapRole(message.Role), content = message.Content.Trim() }));
        var attachments = NormalizeImageAttachments(request.Attachments);
        if (!string.IsNullOrWhiteSpace(request.Prompt) || attachments.Count > 0)
        {
            messages.Add(new
            {
                role = "user",
                content = CreateOpenAiResponsesUserContent(request.Prompt, attachments)
            });
        }

        return messages.ToArray();
    }

    public static void AddOpenAiChatCompletionModelParameters(
        IDictionary<string, object?> payload,
        ProviderChatCompletionRequest request)
    {
        var reasoningEffort = AgentThinkingEffortPolicy.ResolveEffectiveEffort(
            request.Provider,
            request.Model,
            request.ModelParameterConfigurationJson);
        if (reasoningEffort is not null)
        {
            payload["reasoning_effort"] = AgentThinkingEffortPolicy.FormatEffort(reasoningEffort.Value);
        }

        var maxOutputTokens = AgentProviderModelParameterPolicy.ResolveMaxOutputTokens(
            request.Provider.Kind,
            request.Model,
            request.Provider.ConfigurationJson,
            request.ModelParameterConfigurationJson);
        if (maxOutputTokens is not null)
        {
            payload["max_completion_tokens"] = maxOutputTokens.Value;
        }
    }

    public static void AddOpenAiResponsesModelParameters(
        IDictionary<string, object?> payload,
        ProviderChatCompletionRequest request)
    {
        var reasoningEffort = AgentThinkingEffortPolicy.ResolveEffectiveEffort(
            request.Provider,
            request.Model,
            request.ModelParameterConfigurationJson);
        if (reasoningEffort is not null)
        {
            payload["reasoning"] = new Dictionary<string, object>
            {
                ["effort"] = AgentThinkingEffortPolicy.FormatEffort(reasoningEffort.Value)
            };
        }

        var maxOutputTokens = AgentProviderModelParameterPolicy.ResolveMaxOutputTokens(
            request.Provider.Kind,
            request.Model,
            request.Provider.ConfigurationJson,
            request.ModelParameterConfigurationJson);
        if (maxOutputTokens is not null)
        {
            payload["max_output_tokens"] = maxOutputTokens.Value;
        }
    }

    public static ProviderChatCompletionResult ReadOpenAiResponsesResult(
        string model,
        JsonElement root)
    {
        EnsureOpenAiResponseCompleted(root);
        var usage = root.TryGetProperty("usage", out var usageElement)
            ? usageElement
            : default;
        return new ProviderChatCompletionResult(
            model,
            ReadOpenAiResponseText(root),
            usage.ValueKind == JsonValueKind.Object ? ProviderDriverJson.ReadInt(usage, "input_tokens") : 0,
            usage.ValueKind == JsonValueKind.Object ? ProviderDriverJson.ReadInt(usage, "output_tokens") : 0);
    }

    public static object[] BuildOllamaChatMessages(ProviderChatCompletionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var messages = new List<object>();
        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new { role = "system", content = request.SystemPrompt.Trim() });
        }

        messages.AddRange(request.Messages
            .OrderBy(message => message.CreatedAtUtc)
            .Where(message => !string.IsNullOrWhiteSpace(message.Content))
            .Select(message => new { role = MapRole(message.Role), content = message.Content.Trim() }));
        var attachments = NormalizeImageAttachments(request.Attachments);
        if (!string.IsNullOrWhiteSpace(request.Prompt) || attachments.Count > 0)
        {
            var content = request.Prompt.Trim();
            messages.Add(attachments.Count == 0
                ? new { role = "user", content }
                : new
                {
                    role = "user",
                    content,
                    images = attachments.Select(attachment => Convert.ToBase64String(attachment.Bytes)).ToArray()
                });
        }

        return messages.ToArray();
    }

    public static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        throw new InvalidOperationException($"{operation} failed with HTTP {(int)response.StatusCode}: {ReadErrorMessage(content)}");
    }

    private static string MapRole(ChatMessageRole role)
    {
        return role switch
        {
            ChatMessageRole.System => "system",
            ChatMessageRole.User => "user",
            ChatMessageRole.Assistant => "assistant",
            _ => "user"
        };
    }

    private static string ReadOpenAiResponseText(JsonElement root)
    {
        var directText = ProviderDriverJson.ReadString(root, "output_text");
        if (!string.IsNullOrWhiteSpace(directText))
        {
            return directText;
        }

        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        foreach (var outputItem in output.EnumerateArray())
        {
            if (!string.Equals(
                    ProviderDriverJson.ReadString(outputItem, "type"),
                    "message",
                    StringComparison.Ordinal) ||
                !outputItem.TryGetProperty("content", out var content) ||
                content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var contentItem in content.EnumerateArray())
            {
                var value = ProviderDriverJson.ReadString(contentItem, "type") switch
                {
                    "output_text" => ProviderDriverJson.ReadString(contentItem, "text"),
                    "refusal" => ProviderDriverJson.ReadString(contentItem, "refusal"),
                    _ => string.Empty
                };
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                if (text.Length > 0)
                {
                    text.AppendLine();
                }

                text.Append(value);
            }
        }

        return text.ToString();
    }

    private static void EnsureOpenAiResponseCompleted(JsonElement root)
    {
        var status = ProviderDriverJson.ReadString(root, "status");
        if (string.Equals(status, "completed", StringComparison.Ordinal))
        {
            return;
        }

        var detail = status switch
        {
            "failed" => ReadNestedResponseDetail(root, "error", "message"),
            "incomplete" => ReadNestedResponseDetail(root, "incomplete_details", "reason"),
            _ => string.Empty
        };
        var detailSuffix = string.IsNullOrWhiteSpace(detail)
            ? string.Empty
            : $": {detail}";
        var statusLabel = string.IsNullOrWhiteSpace(status)
            ? "missing"
            : status;
        throw new InvalidOperationException($"OpenAI response ended with status '{statusLabel}'{detailSuffix}");
    }

    private static string ReadNestedResponseDetail(
        JsonElement root,
        string objectPropertyName,
        string detailPropertyName)
    {
        if (!root.TryGetProperty(objectPropertyName, out var container) ||
            container.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        var detail = ProviderDriverJson.ReadString(container, detailPropertyName).Trim();
        return detail.Length <= 800
            ? detail
            : detail[..800];
    }

    private static string ReadErrorMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "The response body was empty.";
        }

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            if (TryReadErrorMessage(root, out var message))
            {
                return message;
            }
        }
        catch (JsonException)
        {
        }

        return content.Length <= 800
            ? content.Trim()
            : content[..800].Trim();
    }

    private static object CreateOpenAiUserContent(
        string prompt,
        IReadOnlyList<ProviderChatAttachment> attachments)
    {
        if (attachments.Count == 0)
        {
            return prompt.Trim();
        }

        var parts = new List<object>();
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            parts.Add(new
            {
                type = "text",
                text = prompt.Trim()
            });
        }

        parts.AddRange(attachments.Select(attachment => new
        {
            type = "image_url",
            image_url = new
            {
                url = BuildDataUrl(attachment)
            }
        }));
        return parts;
    }

    private static object CreateOpenAiResponsesUserContent(
        string prompt,
        IReadOnlyList<ProviderChatAttachment> attachments)
    {
        if (attachments.Count == 0)
        {
            return prompt.Trim();
        }

        var parts = new List<object>();
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            parts.Add(new
            {
                type = "input_text",
                text = prompt.Trim()
            });
        }

        parts.AddRange(attachments.Select(attachment => new
        {
            type = "input_image",
            image_url = BuildDataUrl(attachment)
        }));
        return parts;
    }

    private static IReadOnlyList<ProviderChatAttachment> NormalizeImageAttachments(
        IReadOnlyList<ProviderChatAttachment>? attachments)
    {
        return attachments?
            .Where(attachment => attachment.Bytes.Length > 0)
            .Select(attachment => attachment with
            {
                Name = string.IsNullOrWhiteSpace(attachment.Name) ? "image" : attachment.Name.Trim(),
                ContentType = NormalizeImageContentType(attachment.ContentType)
            })
            .ToList()
            ?? [];
    }

    private static string NormalizeImageContentType(string contentType)
    {
        var normalized = string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType.Trim();
        if (!normalized.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Provider chat attachment content type '{normalized}' is not supported for vision chat.");
        }

        return normalized;
    }

    private static string BuildDataUrl(ProviderChatAttachment attachment)
        => $"data:{attachment.ContentType};base64,{Convert.ToBase64String(attachment.Bytes)}";

    private static bool TryReadErrorMessage(
        JsonElement root,
        out string message)
    {
        message = string.Empty;
        if (root.TryGetProperty("error", out var error))
        {
            if (error.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(error.GetString()))
            {
                message = error.GetString()!.Trim();
                return true;
            }

            if (error.ValueKind == JsonValueKind.Object &&
                error.TryGetProperty("message", out var nestedMessage) &&
                nestedMessage.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(nestedMessage.GetString()))
            {
                message = nestedMessage.GetString()!.Trim();
                return true;
            }
        }

        foreach (var propertyName in new[] { "message", "detail" })
        {
            if (root.TryGetProperty(propertyName, out var value) &&
                value.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(value.GetString()))
            {
                message = value.GetString()!.Trim();
                return true;
            }
        }

        return false;
    }
}
