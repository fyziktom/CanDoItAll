using System.Text.Json;
using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Http;

public static class HttpMemoryProviderConfigurationKeys
{
    public const string LegacyRawApiKey = "host.candoitall.memory.http.apiKey";
    public const string BaseUrl = "host.candoitall.memory.http.baseUrl";
    public const string QueryPath = "host.candoitall.memory.http.queryPath";
    public const string HealthPath = "host.candoitall.memory.http.healthPath";
    public const string ApiKeyEnvironmentVariable = "host.candoitall.memory.http.apiKeyEnvironmentVariable";
    public const string AuthHeaderName = "host.candoitall.memory.http.authHeaderName";
    public const string AuthScheme = "host.candoitall.memory.http.authScheme";
    public const string TimeoutMilliseconds = "host.candoitall.memory.http.timeoutMilliseconds";
    public const string MaxRetryAttempts = "host.candoitall.memory.http.maxRetryAttempts";
}

public static class HttpMemoryProviderEndpoints
{
    public const string Query = "/memory/query";
    public const string Ingest = "/memory/ingest";
    public const string Feedback = "/memory/feedback";
    public const string Status = "/memory/operations/{operationId}";
    public const string Events = "/memory/events";
    public const string Health = "/memory/health";
}

public sealed record HttpMemoryProviderConfiguration(
    Uri BaseUrl,
    string QueryPath,
    string HealthPath,
    string? ApiKeyEnvironmentVariable,
    string AuthHeaderName,
    string AuthScheme,
    TimeSpan Timeout,
    int MaxRetryAttempts)
{
    public static HttpMemoryProviderConfiguration FromProfile(
        MemoryProviderProfile profile,
        HttpMemoryProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var values = profile.Manifest.Extensions.Values;
        RejectPersistedRawCredential(values, HttpMemoryProviderConfigurationKeys.LegacyRawApiKey);
        var baseUrl = ReadRequiredUri(values, HttpMemoryProviderConfigurationKeys.BaseUrl);
        var authHeaderName = ReadString(values, HttpMemoryProviderConfigurationKeys.AuthHeaderName) ?? "Authorization";
        var authScheme = ReadString(values, HttpMemoryProviderConfigurationKeys.AuthScheme) ?? "Bearer";
        ValidateAuthentication(authHeaderName, authScheme);
        var apiKeyEnvironmentVariable = ReadString(
            values,
            HttpMemoryProviderConfigurationKeys.ApiKeyEnvironmentVariable);
        ValidateCredentialReference(apiKeyEnvironmentVariable);
        return new HttpMemoryProviderConfiguration(
            baseUrl,
            ReadString(values, HttpMemoryProviderConfigurationKeys.QueryPath) ?? HttpMemoryProviderEndpoints.Query,
            ReadString(values, HttpMemoryProviderConfigurationKeys.HealthPath) ?? HttpMemoryProviderEndpoints.Health,
            apiKeyEnvironmentVariable,
            authHeaderName,
            authScheme,
            ReadTimeout(values, options.DefaultTimeout),
            ReadRetryCount(values, options.MaxRetryAttempts));
    }

    public string? ResolveApiKey()
    {
        if (string.IsNullOrWhiteSpace(ApiKeyEnvironmentVariable))
        {
            return null;
        }

        var value = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(value) ||
            value.Contains('\r') ||
            value.Contains('\n'))
        {
            throw new InvalidOperationException(
                $"HTTP memory provider credential environment variable '{ApiKeyEnvironmentVariable}' is missing or contains an invalid header value.");
        }

        return value.Trim();
    }

    public Uri BuildUri(string relativePath) =>
        HttpMemoryProviderUriBuilder.Build(BaseUrl, relativePath);

    private static Uri ReadRequiredUri(
        IReadOnlyDictionary<string, JsonElement> values,
        string key)
    {
        var value = ReadString(values, key)
            ?? throw new InvalidOperationException($"HTTP memory provider profile is missing required extension '{key}'.");
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            !IsSecureRemoteEndpoint(uri))
        {
            throw new InvalidOperationException(
                $"HTTP memory provider extension '{key}' must be an absolute HTTPS URI without embedded credentials, query strings, or fragments; loopback HTTP is allowed for local development.");
        }

        return uri;
    }

    private static bool IsSecureRemoteEndpoint(Uri uri)
    {
        return uri.Scheme == Uri.UriSchemeHttps ||
            (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);
    }

    private static string? ReadString(
        IReadOnlyDictionary<string, JsonElement> values,
        string key)
    {
        if (!values.TryGetValue(key, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            string? missingValue = null;
            return missingValue;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : throw new InvalidOperationException($"HTTP memory provider extension '{key}' must be a string.");
    }

    private static TimeSpan ReadTimeout(
        IReadOnlyDictionary<string, JsonElement> values,
        TimeSpan defaultTimeout)
    {
        if (!values.TryGetValue(HttpMemoryProviderConfigurationKeys.TimeoutMilliseconds, out var value))
        {
            var configuredTimeout = defaultTimeout;
            return configuredTimeout;
        }

        var milliseconds = value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed)
            ? parsed
            : throw new InvalidOperationException($"HTTP memory provider extension '{HttpMemoryProviderConfigurationKeys.TimeoutMilliseconds}' must be an integer.");
        if (milliseconds <= 0)
        {
            throw new InvalidOperationException("HTTP memory provider timeout must be positive.");
        }

        return TimeSpan.FromMilliseconds(milliseconds);
    }

    private static int ReadRetryCount(
        IReadOnlyDictionary<string, JsonElement> values,
        int defaultRetryCount)
    {
        if (!values.TryGetValue(HttpMemoryProviderConfigurationKeys.MaxRetryAttempts, out var value))
        {
            var configuredRetryCount = defaultRetryCount;
            return configuredRetryCount;
        }

        var retryCount = value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed)
            ? parsed
            : throw new InvalidOperationException($"HTTP memory provider extension '{HttpMemoryProviderConfigurationKeys.MaxRetryAttempts}' must be an integer.");
        if (retryCount < 0)
        {
            throw new InvalidOperationException("HTTP memory provider retry count cannot be negative.");
        }

        return retryCount;
    }

    private static void RejectPersistedRawCredential(
        IReadOnlyDictionary<string, JsonElement> values,
        string key)
    {
        if (values.ContainsKey(key))
        {
            throw new InvalidOperationException(
                $"HTTP memory provider extension '{key}' stores a raw credential. Replace it with '{HttpMemoryProviderConfigurationKeys.ApiKeyEnvironmentVariable}'.");
        }
    }

    private static void ValidateAuthentication(
        string headerName,
        string authScheme)
    {
        if (!HttpMemoryProviderHeaderBindingValidator.IsHttpToken(headerName))
        {
            throw new InvalidOperationException("HTTP memory provider authentication header name is invalid.");
        }

        if (string.Equals(headerName, "Authorization", StringComparison.OrdinalIgnoreCase) &&
            !HttpMemoryProviderHeaderBindingValidator.IsHttpToken(authScheme))
        {
            throw new InvalidOperationException("HTTP memory provider authorization scheme is invalid.");
        }
    }

    private static void ValidateCredentialReference(string? environmentVariable)
    {
        if (environmentVariable is not null &&
            !HttpMemoryProviderHeaderBindingValidator.IsEnvironmentVariableName(environmentVariable))
        {
            throw new InvalidOperationException(
                $"HTTP memory provider extension '{HttpMemoryProviderConfigurationKeys.ApiKeyEnvironmentVariable}' must be an environment-variable identifier using ASCII letters, digits, or underscores.");
        }
    }
}
