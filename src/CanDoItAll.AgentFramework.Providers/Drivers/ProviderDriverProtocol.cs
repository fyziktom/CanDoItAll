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
