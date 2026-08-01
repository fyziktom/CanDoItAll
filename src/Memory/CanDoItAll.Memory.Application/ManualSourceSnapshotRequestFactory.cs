using CanDoItAll.Memory.SourceGateway;

namespace CanDoItAll.Memory.Application;

internal static class ManualSourceSnapshotRequestFactory
{
    public static ManualSourceSnapshotRequest Create(MemorySourceGatewayRequest request)
    {
        var parameters = request.Parameters;
        var payloadKind = GetRequired(parameters, ManualMemorySourceGatewayParameterKeys.PayloadKind);
        var title = GetRequired(parameters, ManualMemorySourceGatewayParameterKeys.Title);
        var contentText = GetOptional(parameters, ManualMemorySourceGatewayParameterKeys.ContentText);
        var locator = GetOptional(parameters, ManualMemorySourceGatewayParameterKeys.Locator);
        var contentType = GetOptional(parameters, ManualMemorySourceGatewayParameterKeys.ContentType);
        var sourceCategory = GetOptional(parameters, ManualMemorySourceGatewayParameterKeys.SourceCategory);
        var tags = GetOptional(parameters, ManualMemorySourceGatewayParameterKeys.Tags)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (Enum.TryParse<ManualMemorySourcePayloadKind>(payloadKind, ignoreCase: true, out var parsedPayloadKind))
        {
            Validate(parsedPayloadKind, contentText, locator);
        }

        return new ManualSourceSnapshotRequest(
            request.ScopeId,
            payloadKind,
            title,
            contentText,
            locator,
            string.IsNullOrWhiteSpace(contentType) ? "text/plain" : contentType.Trim(),
            string.IsNullOrWhiteSpace(sourceCategory) ? "manual" : sourceCategory.Trim(),
            tags,
            request.Cursor,
            request.Take);
    }

    private static void Validate(
        ManualMemorySourcePayloadKind payloadKind,
        string contentText,
        string locator)
    {
        switch (payloadKind)
        {
            case ManualMemorySourcePayloadKind.Text:
                if (string.IsNullOrWhiteSpace(contentText))
                {
                    throw new InvalidOperationException("Manual text sources must include content text.");
                }

                ManualMemorySourceSafetyPolicy.EnsureContentAllowed(contentText);
                break;
            case ManualMemorySourcePayloadKind.FileReference:
                if (string.IsNullOrWhiteSpace(locator))
                {
                    throw new InvalidOperationException("Manual file reference sources must include a locator.");
                }

                break;
            case ManualMemorySourcePayloadKind.LinkReference:
                ManualMemorySourceSafetyPolicy.EnsureUriAllowed(locator);
                break;
            default:
                throw new InvalidOperationException($"Manual source payload kind '{payloadKind}' is not supported.");
        }
    }

    private static string GetRequired(
        IReadOnlyDictionary<string, string> parameters,
        string key)
    {
        var value = GetOptional(parameters, key);
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Manual source gateway parameter '{key}' is required.")
            : value;
    }

    private static string GetOptional(
        IReadOnlyDictionary<string, string> parameters,
        string key)
        => parameters.TryGetValue(key, out var value) ? value.Trim() : string.Empty;
}
