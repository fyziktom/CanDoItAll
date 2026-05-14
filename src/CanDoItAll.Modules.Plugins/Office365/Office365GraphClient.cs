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
    private const string DefaultProcessedCategoryColor = "preset0";

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

    public async Task<Office365MessageCategoryMutationResult> MarkMessageProcessedAsync(
        string accessToken,
        string messageId,
        string sourceCategory,
        string processedCategory,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new InvalidOperationException("Office365 message id is required.");
        }

        if (string.IsNullOrWhiteSpace(sourceCategory))
        {
            throw new InvalidOperationException("Office365 source category is required.");
        }

        if (string.IsNullOrWhiteSpace(processedCategory))
        {
            throw new InvalidOperationException("Office365 processed category is required.");
        }

        var normalizedMessageId = messageId.Trim();
        var normalizedSourceCategory = sourceCategory.Trim();
        var normalizedProcessedCategory = processedCategory.Trim();
        using var client = httpClientFactory.CreateClient(nameof(Office365GraphClient));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var categoryCreated = await EnsureMasterCategoryAsync(client, normalizedProcessedCategory, cancellationToken);
        var currentCategories = await GetMessageCategoriesAsync(client, normalizedMessageId, cancellationToken);
        var sourceCategoryRemoved = currentCategories.Any(category => CategoryEquals(category, normalizedSourceCategory));
        var updatedCategories = currentCategories
            .Where(category => !CategoryEquals(category, normalizedSourceCategory))
            .ToList();
        var processedCategoryAdded = !updatedCategories.Any(category => CategoryEquals(category, normalizedProcessedCategory));
        if (processedCategoryAdded)
        {
            updatedCategories.Add(normalizedProcessedCategory);
        }

        if (sourceCategoryRemoved || processedCategoryAdded)
        {
            var request = new GraphMessageCategoryUpdateRequest(updatedCategories);
            var url = $"{GraphBaseUrl}/me/messages/{Uri.EscapeDataString(normalizedMessageId)}";
            using var httpRequest = new HttpRequestMessage(HttpMethod.Patch, url)
            {
                Content = JsonContent.Create(request, options: JsonOptions)
            };
            using var response = await client.SendAsync(httpRequest, cancellationToken);
            await EnsureSuccessAsync(response, "update Microsoft Graph message categories", cancellationToken);
        }

        return new Office365MessageCategoryMutationResult(
            "office365",
            normalizedMessageId,
            normalizedSourceCategory,
            normalizedProcessedCategory,
            sourceCategoryRemoved,
            processedCategoryAdded,
            categoryCreated,
            updatedCategories);
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

    private static async Task<bool> EnsureMasterCategoryAsync(
        HttpClient client,
        string processedCategory,
        CancellationToken cancellationToken)
    {
        var existingCategories = await ListMasterCategoriesAsync(client, cancellationToken);
        if (existingCategories.Any(category => CategoryEquals(category.DisplayName, processedCategory)))
        {
            return false;
        }

        var request = new GraphMasterCategoryCreateRequest(processedCategory, DefaultProcessedCategoryColor);
        using var createResponse = await client.PostAsJsonAsync(
            $"{GraphBaseUrl}/me/outlook/masterCategories",
            request,
            JsonOptions,
            cancellationToken);
        if (createResponse.StatusCode == HttpStatusCode.Conflict)
        {
            var categoriesAfterConflict = await ListMasterCategoriesAsync(client, cancellationToken);
            if (categoriesAfterConflict.Any(category => CategoryEquals(category.DisplayName, processedCategory)))
            {
                return false;
            }
        }

        await EnsureSuccessAsync(createResponse, "create Microsoft Graph Outlook category", cancellationToken);
        return true;
    }

    private static async Task<IReadOnlyList<GraphMasterCategory>> ListMasterCategoriesAsync(
        HttpClient client,
        CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(
            $"{GraphBaseUrl}/me/outlook/masterCategories?$select=displayName,color",
            cancellationToken);
        await EnsureSuccessAsync(response, "list Microsoft Graph Outlook categories", cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<GraphMasterCategoriesResponse>(JsonOptions, cancellationToken);
        return payload?.Value ?? [];
    }

    private static async Task<IReadOnlyList<string>> GetMessageCategoriesAsync(
        HttpClient client,
        string messageId,
        CancellationToken cancellationToken)
    {
        var url = $"{GraphBaseUrl}/me/messages/{Uri.EscapeDataString(messageId)}?$select=id,categories";
        using var response = await client.GetAsync(url, cancellationToken);
        await EnsureSuccessAsync(response, "get Microsoft Graph message categories", cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<GraphMessageCategorySnapshot>(JsonOptions, cancellationToken)
                      ?? throw new InvalidOperationException($"Microsoft Graph message '{messageId}' category response was empty.");
        return payload.Categories ?? [];
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

    private static bool CategoryEquals(string left, string right)
        => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

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

    private sealed record GraphMasterCategoriesResponse(IReadOnlyList<GraphMasterCategory>? Value);

    private sealed record GraphMasterCategory(string DisplayName, string Color);

    private sealed record GraphMasterCategoryCreateRequest(string DisplayName, string Color);

    private sealed record GraphMessageCategorySnapshot(
        string Id,
        IReadOnlyList<string>? Categories);

    private sealed record GraphMessageCategoryUpdateRequest(IReadOnlyList<string> Categories);

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
