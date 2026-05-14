using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Modules.Plugins;

public sealed class Office365GraphClient(IHttpClientFactory httpClientFactory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string GraphBaseUrl = "https://graph.microsoft.com/v1.0";

    public async Task<PluginEmailMessageBatch> DownloadMessagesByCategoryAsync(
        string accessToken,
        string category,
        int maxMessages,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            throw new InvalidOperationException("Office365 category is required.");
        }

        var normalizedMax = Math.Clamp(maxMessages, 1, 25);
        using var client = httpClientFactory.CreateClient(nameof(Office365GraphClient));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Prefer", "outlook.body-content-type=\"text\"");
        var messages = await ListMessagesAsync(client, category.Trim(), normalizedMax, cancellationToken);
        return new PluginEmailMessageBatch(
            "office365",
            "category",
            category.Trim(),
            messages.Count,
            messages);
    }

    private static async Task<IReadOnlyList<PluginEmailMessage>> ListMessagesAsync(
        HttpClient client,
        string category,
        int maxMessages,
        CancellationToken cancellationToken)
    {
        var filter = $"categories/any(c:c eq '{EscapeODataString(category)}')";
        var select = "id,subject,from,receivedDateTime,bodyPreview,body,categories,webLink";
        var url = $"{GraphBaseUrl}/me/messages?$top={maxMessages}&$select={WebUtility.UrlEncode(select)}&$filter={WebUtility.UrlEncode(filter)}";
        using var response = await client.GetAsync(url, cancellationToken);
        await EnsureSuccessAsync(response, "list Microsoft Graph messages", cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<GraphMessagesResponse>(JsonOptions, cancellationToken);
        return payload?.Value.Select(ToEmailMessage).ToArray() ?? [];
    }

    private static PluginEmailMessage ToEmailMessage(GraphMessage message)
        => new(
            message.Id,
            string.Empty,
            message.Subject,
            message.From?.EmailAddress?.Address ?? message.From?.EmailAddress?.Name ?? string.Empty,
            message.ReceivedDateTime,
            message.BodyPreview,
            message.Body?.Content ?? string.Empty,
            message.Categories,
            message.WebLink);

    private static string EscapeODataString(string value)
        => value.Replace("'", "''", StringComparison.Ordinal);

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

    private sealed record GraphMessagesResponse(IReadOnlyList<GraphMessage> Value);

    private sealed record GraphMessage(
        string Id,
        string Subject,
        GraphRecipient? From,
        string ReceivedDateTime,
        string BodyPreview,
        GraphItemBody? Body,
        IReadOnlyList<string> Categories,
        string WebLink);

    private sealed record GraphRecipient(GraphEmailAddress? EmailAddress);

    private sealed record GraphEmailAddress(string Name, string Address);

    private sealed record GraphItemBody(
        [property: JsonPropertyName("contentType")] string ContentType,
        string Content);
}
