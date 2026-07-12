using CanDoItAll.Memory.Abstractions;
using System.Text.Json;

namespace CanDoItAll.Modules.Memory.Services;

internal static class MemoryProviderExtensionEditorMapper
{
    public static MemoryExtensionData Build(MemoryProviderProfileEditorModel editor)
    {
        var values = editor.PreservedExtensions.Values.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Clone(),
            StringComparer.Ordinal);

        MemoryProviderHttpExtensionCodec.RemoveManagedValues(values);
        MemoryProviderMcpExtensionCodec.RemoveManagedValues(values);
        var providerUiUrl = editor.SupportsIframeUi
            ? ValidateProviderUiUrl(editor.ProviderUiUrl)
            : null;
        MemoryProviderExtensionValues.SetString(
            values,
            MemoryProviderUiSurfaceKeys.ProviderVendorUiUrlExtension,
            providerUiUrl);

        switch (editor.DriverKind)
        {
            case MemoryProviderDriverKind.Http:
            case MemoryProviderDriverKind.NativeRemote:
                MemoryProviderHttpExtensionCodec.Write(values, editor.DriverKind, editor.Http);
                break;
            case MemoryProviderDriverKind.Mcp:
                MemoryProviderMcpExtensionCodec.Write(values, editor.Mcp);
                break;
        }

        return new MemoryExtensionData(values);
    }

    public static MemoryExtensionData SanitizePreserved(MemoryExtensionData extensions)
    {
        var values = extensions.Values.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Clone(),
            StringComparer.Ordinal);
        values.Remove(MemoryProviderHttpExtensionCodec.LegacyHttpRawApiKey);
        values.Remove(MemoryProviderHttpExtensionCodec.LegacyNativeRawApiKey);
        return new MemoryExtensionData(values);
    }

    public static IReadOnlyList<string> FindLegacyRawCredentialKeys(MemoryExtensionData extensions) =>
        new[]
        {
            MemoryProviderHttpExtensionCodec.LegacyHttpRawApiKey,
            MemoryProviderHttpExtensionCodec.LegacyNativeRawApiKey
        }
        .Where(extensions.Values.ContainsKey)
        .ToArray();

    public static string ReadProviderUiUrl(IReadOnlyDictionary<string, JsonElement> values) =>
        MemoryProviderExtensionValues.ReadString(values, MemoryProviderUiSurfaceKeys.ProviderVendorUiUrlExtension);

    private static string ValidateProviderUiUrl(string configuredUrl)
    {
        if (!Uri.TryCreate(configuredUrl?.Trim(), UriKind.Absolute, out var uri) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException(
                "Provider UI URL must be an absolute HTTP(S) URI without embedded credentials, query strings, or fragments.");
        }

        var isHttps = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        var isLoopbackHttp = string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && uri.IsLoopback;
        if (!isHttps && !isLoopbackHttp)
        {
            throw new InvalidOperationException("Provider UI URL must use HTTPS or loopback HTTP.");
        }

        return uri.AbsoluteUri;
    }
}
