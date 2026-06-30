using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Modules.Plugins;

public sealed class GmailApiClient(IHttpClientFactory httpClientFactory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string GmailBaseUrl = "https://gmail.googleapis.com/gmail/v1/users/me";

    public async Task<PluginEmailMessageBatch> DownloadMessagesByLabelAsync(
        string accessToken,
        string label,
        int maxMessages,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new InvalidOperationException("Gmail label is required.");
        }

        var normalizedMax = Math.Clamp(maxMessages, 1, 25);
        using var client = httpClientFactory.CreateClient(nameof(GmailApiClient));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var labelId = await ResolveLabelIdAsync(client, label.Trim(), cancellationToken);
        var messageIds = await ListMessageIdsAsync(client, labelId, normalizedMax, cancellationToken);
        var messages = new List<PluginEmailMessage>();
        foreach (var messageId in messageIds)
        {
            messages.Add(await GetMessageAsync(client, messageId, cancellationToken));
        }

        return new PluginEmailMessageBatch(
            "gmail",
            "label",
            label.Trim(),
            messages.Count,
            messages);
    }

    public async Task<GmailMessageLabelMutationResult> MarkMessageProcessedAsync(
        string accessToken,
        string messageId,
        string sourceLabel,
        string processedLabel,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new InvalidOperationException("Gmail message id is required.");
        }

        if (string.IsNullOrWhiteSpace(sourceLabel))
        {
            throw new InvalidOperationException("Gmail source label is required.");
        }

        if (string.IsNullOrWhiteSpace(processedLabel))
        {
            throw new InvalidOperationException("Gmail processed label is required.");
        }

        using var client = httpClientFactory.CreateClient(nameof(GmailApiClient));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var sourceLabelId = await ResolveLabelIdAsync(client, sourceLabel.Trim(), cancellationToken);
        var processedLabelId = await ResolveOrCreateLabelIdAsync(client, processedLabel.Trim(), cancellationToken);
        var currentLabelIds = await GetMessageLabelIdsAsync(client, messageId.Trim(), cancellationToken);
        var sourceLabelRemoved = currentLabelIds.Contains(sourceLabelId);
        var processedLabelAdded = !currentLabelIds.Contains(processedLabelId);
        if (!sourceLabelRemoved && !processedLabelAdded)
        {
            return new GmailMessageLabelMutationResult(
                "gmail",
                messageId.Trim(),
                sourceLabel.Trim(),
                processedLabel.Trim(),
                SourceLabelRemoved: false,
                ProcessedLabelAdded: false);
        }

        IReadOnlyList<string> addLabelIds = processedLabelAdded ? [processedLabelId] : [];
        IReadOnlyList<string> removeLabelIds = sourceLabelRemoved ? [sourceLabelId] : [];
        var request = new GmailModifyLabelsRequest(addLabelIds, removeLabelIds);
        var url = $"{GmailBaseUrl}/messages/{WebUtility.UrlEncode(messageId.Trim())}/modify";
        using var response = await client.PostAsJsonAsync(url, request, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(response, "modify Gmail message labels", cancellationToken);

        return new GmailMessageLabelMutationResult(
            "gmail",
            messageId.Trim(),
            sourceLabel.Trim(),
            processedLabel.Trim(),
            sourceLabelRemoved,
            processedLabelAdded);
    }

    private static async Task<string> ResolveLabelIdAsync(
        HttpClient client,
        string label,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync($"{GmailBaseUrl}/labels", cancellationToken);
        await EnsureSuccessAsync(response, "list Gmail labels", cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<GmailLabelsResponse>(JsonOptions, cancellationToken);
        var match = payload?.Labels?.FirstOrDefault(item =>
            string.Equals(item.Id, label, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Name, label, StringComparison.OrdinalIgnoreCase));
        return match?.Id ?? throw new InvalidOperationException($"Gmail label '{label}' was not found.");
    }

    private static async Task<string> ResolveOrCreateLabelIdAsync(
        HttpClient client,
        string label,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync($"{GmailBaseUrl}/labels", cancellationToken);
        await EnsureSuccessAsync(response, "list Gmail labels", cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<GmailLabelsResponse>(JsonOptions, cancellationToken);
        var match = payload?.Labels?.FirstOrDefault(item =>
            string.Equals(item.Id, label, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.Name, label, StringComparison.OrdinalIgnoreCase));
        if (match is not null)
        {
            return match.Id;
        }

        var createRequest = new GmailLabelCreateRequest(
            label,
            "labelShow",
            "show");
        using var createResponse = await client.PostAsJsonAsync($"{GmailBaseUrl}/labels", createRequest, JsonOptions, cancellationToken);
        await EnsureSuccessAsync(createResponse, "create Gmail label", cancellationToken);
        var created = await createResponse.Content.ReadFromJsonAsync<GmailLabel>(JsonOptions, cancellationToken)
                      ?? throw new InvalidOperationException($"Gmail label '{label}' create response was empty.");
        return string.IsNullOrWhiteSpace(created.Id)
            ? throw new InvalidOperationException($"Gmail label '{label}' create response did not include an id.")
            : created.Id;
    }

    private static async Task<IReadOnlyList<string>> ListMessageIdsAsync(
        HttpClient client,
        string labelId,
        int maxMessages,
        CancellationToken cancellationToken)
    {
        var url = $"{GmailBaseUrl}/messages?labelIds={WebUtility.UrlEncode(labelId)}&maxResults={maxMessages}";
        using var response = await client.GetAsync(url, cancellationToken);
        await EnsureSuccessAsync(response, "list Gmail messages", cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<GmailMessagesResponse>(JsonOptions, cancellationToken);
        return payload?.Messages?.Select(message => message.Id).Where(id => !string.IsNullOrWhiteSpace(id)).ToArray() ?? [];
    }

    private static async Task<PluginEmailMessage> GetMessageAsync(
        HttpClient client,
        string messageId,
        CancellationToken cancellationToken)
    {
        var url = $"{GmailBaseUrl}/messages/{WebUtility.UrlEncode(messageId)}?format=full";
        using var response = await client.GetAsync(url, cancellationToken);
        await EnsureSuccessAsync(response, "get Gmail message", cancellationToken);
        var message = await response.Content.ReadFromJsonAsync<GmailMessageResponse>(JsonOptions, cancellationToken)
                      ?? throw new InvalidOperationException($"Gmail message '{messageId}' response was empty.");
        var headers = BuildHeaderLookup(message.Payload?.Headers);
        headers.TryGetValue("Subject", out var subject);
        headers.TryGetValue("From", out var from);
        headers.TryGetValue("Date", out var date);

        return new PluginEmailMessage(
            message.Id,
            message.ThreadId,
            subject ?? string.Empty,
            from ?? string.Empty,
            date ?? string.Empty,
            message.Snippet,
            ExtractBodyText(message.Payload),
            message.LabelIds ?? [],
            string.Empty);
    }

    private static async Task<IReadOnlySet<string>> GetMessageLabelIdsAsync(
        HttpClient client,
        string messageId,
        CancellationToken cancellationToken)
    {
        var url = $"{GmailBaseUrl}/messages/{WebUtility.UrlEncode(messageId)}?format=minimal";
        using var response = await client.GetAsync(url, cancellationToken);
        await EnsureSuccessAsync(response, "get Gmail message labels", cancellationToken);
        var message = await response.Content.ReadFromJsonAsync<GmailMessageLabelResponse>(JsonOptions, cancellationToken)
                      ?? throw new InvalidOperationException($"Gmail message '{messageId}' label response was empty.");
        return new HashSet<string>(
            message.LabelIds?.Where(labelId => !string.IsNullOrWhiteSpace(labelId)) ?? [],
            StringComparer.OrdinalIgnoreCase);
    }

    private static string ExtractBodyText(GmailMessagePart? part)
    {
        if (part is null)
        {
            return string.Empty;
        }

        if (string.Equals(part.MimeType, "text/plain", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(part.Body?.Data))
        {
            return DecodeBase64Url(part.Body.Data);
        }

        foreach (var child in part.Parts ?? [])
        {
            var value = ExtractBodyText(child);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        if (!string.IsNullOrWhiteSpace(part.Body?.Data))
        {
            return DecodeBase64Url(part.Body.Data);
        }

        return string.Empty;
    }

    private static IReadOnlyDictionary<string, string> BuildHeaderLookup(IReadOnlyList<GmailHeader>? headers)
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers ?? [])
        {
            if (string.IsNullOrWhiteSpace(header.Name) ||
                lookup.ContainsKey(header.Name))
            {
                continue;
            }

            lookup[header.Name] = header.Value;
        }

        return lookup;
    }

    private static string DecodeBase64Url(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        var padding = normalized.Length % 4;
        if (padding > 0)
        {
            normalized = normalized.PadRight(normalized.Length + 4 - padding, '=');
        }

        return Encoding.UTF8.GetString(Convert.FromBase64String(normalized));
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        string operation,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException($"Failed to {operation}. Status={(int)response.StatusCode} {response.ReasonPhrase}. Body={Truncate(body, 500)}");
    }

    private static string Truncate(string value, int maxLength)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Length <= maxLength
                ? value
                : value[..maxLength];

    private sealed record GmailLabelsResponse(IReadOnlyList<GmailLabel>? Labels);

    private sealed record GmailLabel(string Id, string Name);

    private sealed record GmailLabelCreateRequest(
        string Name,
        string LabelListVisibility,
        string MessageListVisibility);

    private sealed record GmailModifyLabelsRequest(
        IReadOnlyList<string> AddLabelIds,
        IReadOnlyList<string> RemoveLabelIds);

    private sealed record GmailMessagesResponse(IReadOnlyList<GmailMessageId>? Messages);

    private sealed record GmailMessageId(string Id);

    private sealed record GmailMessageResponse(
        string Id,
        string ThreadId,
        string Snippet,
        IReadOnlyList<string>? LabelIds,
        GmailMessagePart? Payload);

    private sealed record GmailMessageLabelResponse(
        string Id,
        IReadOnlyList<string>? LabelIds);

    private sealed record GmailMessagePart(
        string MimeType,
        IReadOnlyList<GmailHeader>? Headers,
        GmailMessageBody? Body,
        IReadOnlyList<GmailMessagePart>? Parts);

    private sealed record GmailHeader(string Name, string Value);

    private sealed record GmailMessageBody([property: JsonPropertyName("data")] string? Data);
}
