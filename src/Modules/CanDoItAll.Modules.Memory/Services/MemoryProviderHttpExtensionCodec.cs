using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Http;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Memory.Services;

internal static class MemoryProviderHttpExtensionCodec
{
    public const string LegacyHttpRawApiKey = HttpMemoryProviderConfigurationKeys.LegacyRawApiKey;
    public const string LegacyNativeRawApiKey = NativeRemoteMemoryProviderConfigurationKeys.LegacyRawApiKey;

    private static readonly Regex HttpTokenPattern = new(
        "^[!#$%&'*+\\-.^_`|~0-9A-Za-z]+$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static MemoryProviderHttpTransportEditorModel Read(
        MemoryProviderDriverKind driverKind,
        IReadOnlyDictionary<string, JsonElement> values)
    {
        var keys = MemoryProviderHttpExtensionKeys.For(driverKind);
        return new MemoryProviderHttpTransportEditorModel
        {
            BaseUrl = MemoryProviderExtensionValues.ReadString(values, keys.BaseUrl),
            QueryPath = MemoryProviderExtensionValues.ReadString(values, keys.QueryPath, HttpMemoryProviderEndpoints.Query),
            HealthPath = MemoryProviderExtensionValues.ReadString(values, keys.HealthPath, HttpMemoryProviderEndpoints.Health),
            ApiKeyEnvironmentVariable = MemoryProviderExtensionValues.ReadString(values, keys.ApiKeyEnvironmentVariable),
            AuthHeaderName = MemoryProviderExtensionValues.ReadString(values, keys.AuthHeaderName, "Authorization"),
            AuthScheme = MemoryProviderExtensionValues.ReadString(values, keys.AuthScheme, "Bearer"),
            TimeoutMilliseconds = MemoryProviderExtensionValues.ReadInt(values, keys.TimeoutMilliseconds, 30_000),
            MaxRetryAttempts = MemoryProviderExtensionValues.ReadInt(values, keys.MaxRetryAttempts, 0)
        };
    }

    public static void Write(
        IDictionary<string, JsonElement> values,
        MemoryProviderDriverKind driverKind,
        MemoryProviderHttpTransportEditorModel editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        var keys = MemoryProviderHttpExtensionKeys.For(driverKind);
        var credential = MemoryProviderCredentialReference.ParseOptional(
            editor.ApiKeyEnvironmentVariable,
            nameof(editor.ApiKeyEnvironmentVariable));
        ValidateHeaderName(editor.AuthHeaderName);
        ValidateAuthScheme(editor.AuthHeaderName, editor.AuthScheme);
        ValidateRemoteEndpoint(editor.BaseUrl, driverKind);

        MemoryProviderExtensionValues.SetString(values, keys.BaseUrl, editor.BaseUrl);
        MemoryProviderExtensionValues.SetString(values, keys.QueryPath, editor.QueryPath);
        MemoryProviderExtensionValues.SetString(values, keys.HealthPath, editor.HealthPath);
        MemoryProviderExtensionValues.SetString(values, keys.ApiKeyEnvironmentVariable, credential?.EnvironmentVariableName);
        MemoryProviderExtensionValues.SetString(values, keys.AuthHeaderName, editor.AuthHeaderName);
        MemoryProviderExtensionValues.SetString(values, keys.AuthScheme, editor.AuthScheme);
        MemoryProviderExtensionValues.SetNumber(values, keys.TimeoutMilliseconds, editor.TimeoutMilliseconds, minimum: 1);
        MemoryProviderExtensionValues.SetNumber(values, keys.MaxRetryAttempts, editor.MaxRetryAttempts, minimum: 0);

        ValidatePaths(editor, driverKind);
    }

    public static void RemoveManagedValues(IDictionary<string, JsonElement> values)
    {
        foreach (var key in MemoryProviderHttpExtensionKeys.AllManagedKeys)
        {
            values.Remove(key);
        }

        values.Remove(LegacyHttpRawApiKey);
        values.Remove(LegacyNativeRawApiKey);
    }

    private static void ValidateRemoteEndpoint(string baseUrl, MemoryProviderDriverKind driverKind)
    {
        if (!Uri.TryCreate(baseUrl?.Trim(), UriKind.Absolute, out var uri) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new InvalidOperationException($"{driverKind} memory provider base URL must be an absolute HTTP(S) URI without embedded credentials, query strings, or fragments.");
        }

        var isHttps = string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        var isLoopbackHttp = string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) && uri.IsLoopback;
        if (!isHttps && !isLoopbackHttp)
        {
            throw new InvalidOperationException($"{driverKind} memory provider base URL must use HTTPS or loopback HTTP.");
        }
    }

    private static void ValidatePaths(
        MemoryProviderHttpTransportEditorModel editor,
        MemoryProviderDriverKind driverKind)
    {
        if (!IsSafeRelativePath(editor.QueryPath) || !IsSafeRelativePath(editor.HealthPath))
        {
            throw new InvalidOperationException(
                $"{driverKind} memory provider query and health paths must be rooted relative paths without query strings, fragments, authority changes, or control characters.");
        }
    }

    private static bool IsSafeRelativePath(string path) =>
        !string.IsNullOrWhiteSpace(path) &&
        path[0] == '/' &&
        !path.Contains("//", StringComparison.Ordinal) &&
        !path.Contains('\\') &&
        !path.Contains('?') &&
        !path.Contains('#') &&
        !path.Any(char.IsControl) &&
        Uri.TryCreate(path, UriKind.Relative, out _);

    private static void ValidateHeaderName(string headerName)
    {
        if (string.IsNullOrWhiteSpace(headerName) || !HttpTokenPattern.IsMatch(headerName.Trim()))
        {
            throw new ArgumentException("Authentication header must be a valid HTTP header name.", nameof(headerName));
        }
    }

    private static void ValidateAuthScheme(string headerName, string authScheme)
    {
        if (string.Equals(headerName?.Trim(), "Authorization", StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(authScheme) || !HttpTokenPattern.IsMatch(authScheme.Trim())))
        {
            throw new ArgumentException(
                "Authorization scheme must be a valid RFC HTTP token.",
                nameof(authScheme));
        }
    }

}
