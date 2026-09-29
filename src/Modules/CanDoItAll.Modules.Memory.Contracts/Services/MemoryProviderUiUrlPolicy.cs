using CanDoItAll.Memory.Abstractions;
using System.Text.Json;

namespace CanDoItAll.Modules.Memory.Services;

public static class MemoryProviderUiUrlPolicy {
    public const string BlockedUrl = "Blocked unsafe provider UI URL";
    public static bool TryNormalize(string configuredUrl, out string safeUrl) {
        safeUrl = string.Empty;
        if (string.IsNullOrWhiteSpace(configuredUrl) || !Uri.TryCreate(configuredUrl.Trim(), UriKind.Absolute, out var uri) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)) {
            return false;
        }
        var allowed = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && uri.IsLoopback);
        if (allowed) {
            safeUrl = uri.AbsoluteUri;
        }
        return allowed;
    }

    public static MemoryExtensionData Sanitize(MemoryProviderProfile profile) {
        var urlKeys = profile.Manifest.UiSurfaces.Select(s => s.UrlSettingKey).Where(k => k is not null).Append(MemoryProviderUiSurfaceKeys.ProviderVendorUiUrlExtension).ToHashSet(StringComparer.Ordinal);
        var values = profile.Manifest.Extensions.Values
            .Where(pair => pair.Key != MemoryEditorDefaults.LegacyHttpCredential && pair.Key != MemoryEditorDefaults.LegacyNativeCredential)
            .ToDictionary(pair => pair.Key, pair => urlKeys.Contains(pair.Key) && pair.Value.ValueKind == JsonValueKind.String && !TryNormalize(pair.Value.GetString() ?? string.Empty, out _)
                ? JsonSerializer.SerializeToElement(BlockedUrl) : pair.Value.Clone(), StringComparer.Ordinal);
        return new(values);
    }
}
