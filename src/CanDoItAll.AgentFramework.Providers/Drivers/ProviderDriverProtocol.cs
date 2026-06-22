using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Providers;

internal static class ProviderDriverProtocol
{
    public static object[] BuildChatMessages(ProviderChatCompletionRequest request)
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
        if (!string.IsNullOrWhiteSpace(request.Prompt))
        {
            messages.Add(new { role = "user", content = request.Prompt.Trim() });
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
