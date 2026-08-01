using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Modules.Plugins;

public interface IOffice365WorkflowClient
{
    Task<PluginEmailMessageBatch> DownloadMessagesByCategoryAsync(
        string accessToken,
        string category,
        int maxMessages,
        CancellationToken cancellationToken = default);

    Task<PluginEmailMessageBatch> DownloadOneUnprocessedMessageByAddressAsync(
        string accessToken,
        Office365MessageAddressFilterSettings settings,
        CancellationToken cancellationToken = default);

    Task<Office365MessageCategoryMutationResult> MarkMessageProcessedAsync(
        string accessToken,
        string messageId,
        string? sourceCategory,
        string processedCategory,
        CancellationToken cancellationToken = default);
}

public sealed class Office365GraphClient(IHttpClientFactory httpClientFactory) : IOffice365WorkflowClient
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

    public async Task<PluginEmailMessageBatch> DownloadOneUnprocessedMessageByAddressAsync(
        string accessToken,
        Office365MessageAddressFilterSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var normalizedEmail = NormalizeEmailAddress(settings.EmailAddress);
        var normalizedProcessedCategory = NormalizeProcessedCategory(settings.ProcessedCategory);
        var normalizedMax = Math.Clamp(settings.MaxCandidateMessages, 1, 50);
        using var client = httpClientFactory.CreateClient(nameof(Office365GraphClient));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.TryAddWithoutValidation("Prefer", "outlook.body-content-type=\"text\"");

        var messages = await TryListMessagesByAddressServerSideAsync(
            client,
            normalizedEmail,
            normalizedProcessedCategory,
            settings,
            cancellationToken);
        if (messages is null)
        {
            messages = await ListAddressCandidatesAsync(
                client,
                normalizedEmail,
                normalizedProcessedCategory,
                settings,
                normalizedMax,
                cancellationToken);
        }

        var bounded = messages
            .Where(message => MessageMatchesAddress(message, normalizedEmail, settings.MatchMode))
            .Where(message => !HasCategory(message, normalizedProcessedCategory))
            .OrderByDescending(message => TryParseDateTimeOffset(message.ReceivedDateTime))
            .Take(1)
            .Select(message => ToEmailMessage(message, settings.IncludeBody, settings.MaxBodyCharacters))
            .ToArray();

        return new PluginEmailMessageBatch(
            "office365",
            "emailAddress",
            normalizedEmail,
            bounded.Length,
            bounded);
    }

    public async Task<Office365MessageCategoryMutationResult> MarkMessageProcessedAsync(
        string accessToken,
        string messageId,
        string? sourceCategory,
        string processedCategory,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            throw new InvalidOperationException("Office365 message id is required.");
        }

        if (string.IsNullOrWhiteSpace(processedCategory))
        {
            throw new InvalidOperationException("Office365 processed category is required.");
        }

        var normalizedMessageId = messageId.Trim();
        var normalizedSourceCategory = sourceCategory?.Trim() ?? string.Empty;
        var normalizedProcessedCategory = processedCategory.Trim();
        using var client = httpClientFactory.CreateClient(nameof(Office365GraphClient));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var categoryCreated = await EnsureMasterCategoryAsync(client, normalizedProcessedCategory, cancellationToken);
        var currentCategories = await GetMessageCategoriesAsync(client, normalizedMessageId, cancellationToken);
        var hasSourceCategory = !string.IsNullOrWhiteSpace(normalizedSourceCategory);
        var sourceCategoryRemoved = hasSourceCategory &&
                                    currentCategories.Any(category => CategoryEquals(category, normalizedSourceCategory));
        var updatedCategories = currentCategories
            .Where(category => !hasSourceCategory || !CategoryEquals(category, normalizedSourceCategory))
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

    private static async Task<IReadOnlyList<GraphMessage>?> TryListMessagesByAddressServerSideAsync(
        HttpClient client,
        string emailAddress,
        string processedCategory,
        Office365MessageAddressFilterSettings settings,
        CancellationToken cancellationToken)
    {
        var filter = BuildAddressFilter(emailAddress, settings.MatchMode);
        filter = $"{filter} and not(categories/any(c:c eq '{EscapeODataString(processedCategory)}'))";
        if (settings.LookbackHours > 0)
        {
            var receivedAfter = DateTimeOffset.UtcNow.AddHours(-settings.LookbackHours).UtcDateTime.ToString("O");
            filter = $"{filter} and receivedDateTime ge {receivedAfter}";
        }

        var url = BuildMessagesUrl(settings.MailFolderId, top: 1, filter, orderByReceivedDesc: true);
        using var response = await client.GetAsync(url, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadFromJsonAsync<GraphMessagesResponse>(JsonOptions, cancellationToken);
            return payload?.Value ?? [];
        }

        if (response.StatusCode != HttpStatusCode.BadRequest)
        {
            await EnsureSuccessAsync(response, "list Microsoft Graph messages by address", cancellationToken);
        }

        return null;
    }

    private static async Task<IReadOnlyList<GraphMessage>> ListAddressCandidatesAsync(
        HttpClient client,
        string emailAddress,
        string processedCategory,
        Office365MessageAddressFilterSettings settings,
        int maxCandidates,
        CancellationToken cancellationToken)
    {
        _ = emailAddress;
        var filter = $"not(categories/any(c:c eq '{EscapeODataString(processedCategory)}'))";
        if (settings.LookbackHours > 0)
        {
            var receivedAfter = DateTimeOffset.UtcNow.AddHours(-settings.LookbackHours).UtcDateTime.ToString("O");
            filter = $"{filter} and receivedDateTime ge {receivedAfter}";
        }

        var url = BuildMessagesUrl(settings.MailFolderId, maxCandidates, filter, orderByReceivedDesc: true);
        using var response = await client.GetAsync(url, cancellationToken);
        await EnsureSuccessAsync(response, "list bounded Microsoft Graph fallback message candidates", cancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<GraphMessagesResponse>(JsonOptions, cancellationToken);
        return payload?.Value ?? [];
    }

    private static string BuildMessagesUrl(
        string mailFolderId,
        int top,
        string filter,
        bool orderByReceivedDesc)
    {
        var path = string.IsNullOrWhiteSpace(mailFolderId)
            ? "/me/messages"
            : $"/me/mailFolders/{Uri.EscapeDataString(mailFolderId.Trim())}/messages";
        var select = "id,subject,from,sender,receivedDateTime,bodyPreview,body,categories,webLink,conversationId,internetMessageId";
        var query = $"$top={top}&$select={WebUtility.UrlEncode(select)}&$filter={WebUtility.UrlEncode(filter)}";
        if (orderByReceivedDesc)
        {
            query += "&$orderby=receivedDateTime%20desc";
        }

        return $"{GraphBaseUrl}{path}?{query}";
    }

    private static string BuildAddressFilter(
        string emailAddress,
        Office365EmailAddressMatchMode matchMode)
    {
        var escaped = EscapeODataString(emailAddress);
        return matchMode switch
        {
            Office365EmailAddressMatchMode.FromEquals => $"from/emailAddress/address eq '{escaped}'",
            Office365EmailAddressMatchMode.SenderEquals => $"sender/emailAddress/address eq '{escaped}'",
            _ => $"(from/emailAddress/address eq '{escaped}' or sender/emailAddress/address eq '{escaped}')"
        };
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
        => ToEmailMessage(message, includeBody: true, maxBodyCharacters: int.MaxValue);

    private static PluginEmailMessage ToEmailMessage(
        GraphMessage message,
        bool includeBody,
        int maxBodyCharacters)
    {
        var bodyText = includeBody
            ? Truncate(message.Body?.Content ?? string.Empty, Math.Max(maxBodyCharacters, 0))
            : string.Empty;
        var from = message.From?.EmailAddress?.Address ??
                   message.Sender?.EmailAddress?.Address ??
                   message.From?.EmailAddress?.Name ??
                   message.Sender?.EmailAddress?.Name ??
                   string.Empty;

        return new PluginEmailMessage(
            message.Id,
            message.ConversationId ?? message.InternetMessageId ?? string.Empty,
            message.Subject ?? string.Empty,
            from,
            message.ReceivedDateTime ?? string.Empty,
            message.BodyPreview ?? string.Empty,
            bodyText,
            message.Categories ?? [],
            message.WebLink ?? string.Empty);
    }

    private static string NormalizeEmailAddress(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmed) ||
            trimmed.Any(char.IsWhiteSpace) ||
            trimmed.Count(character => character == '@') != 1)
        {
            throw new InvalidOperationException("Office365 email address is required and must be a single address.");
        }

        try
        {
            var address = new MailAddress(trimmed);
            if (!string.Equals(address.Address, trimmed, StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(address.User) ||
                string.IsNullOrWhiteSpace(address.Host))
            {
                throw new InvalidOperationException("Office365 email address must not include a display name.");
            }

            return address.Address.ToLowerInvariant();
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException("Office365 email address is invalid.", exception);
        }
    }

    private static string NormalizeProcessedCategory(string processedCategory)
    {
        if (string.IsNullOrWhiteSpace(processedCategory))
        {
            throw new InvalidOperationException("Office365 processed category is required.");
        }

        return processedCategory.Trim();
    }

    private static bool MessageMatchesAddress(
        GraphMessage message,
        string emailAddress,
        Office365EmailAddressMatchMode matchMode)
    {
        var fromMatches = AddressEquals(message.From?.EmailAddress?.Address, emailAddress);
        var senderMatches = AddressEquals(message.Sender?.EmailAddress?.Address, emailAddress);
        return matchMode switch
        {
            Office365EmailAddressMatchMode.FromEquals => fromMatches,
            Office365EmailAddressMatchMode.SenderEquals => senderMatches,
            _ => fromMatches || senderMatches
        };
    }

    private static bool HasCategory(
        GraphMessage message,
        string category)
        => message.Categories?.Any(candidate => CategoryEquals(candidate, category)) == true;

    private static bool AddressEquals(
        string? left,
        string right)
        => string.Equals(left?.Trim(), right, StringComparison.OrdinalIgnoreCase);

    private static DateTimeOffset TryParseDateTimeOffset(string? value)
        => DateTimeOffset.TryParse(value, out var parsed)
            ? parsed
            : DateTimeOffset.MinValue;

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
        string? Subject,
        GraphRecipient? From,
        GraphRecipient? Sender,
        string? ReceivedDateTime,
        string? BodyPreview,
        GraphItemBody? Body,
        IReadOnlyList<string>? Categories,
        string? WebLink,
        string? ConversationId,
        string? InternetMessageId);

    private sealed record GraphRecipient(GraphEmailAddress? EmailAddress);

    private sealed record GraphEmailAddress(string Name, string Address);

    private sealed record GraphItemBody(
        [property: JsonPropertyName("contentType")] string ContentType,
        string Content);
}
