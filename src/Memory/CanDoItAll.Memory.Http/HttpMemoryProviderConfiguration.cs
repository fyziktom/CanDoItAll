using System.Text.Json;
using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Http;

public static class HttpMemoryProviderConfigurationKeys
{
    public const string BaseUrl = "host.candoitall.memory.http.baseUrl";
    public const string QueryPath = "host.candoitall.memory.http.queryPath";
    public const string HealthPath = "host.candoitall.memory.http.healthPath";
    public const string ApiKey = "host.candoitall.memory.http.apiKey";
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
    string? ApiKey,
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
        var baseUrl = ReadRequiredUri(values, HttpMemoryProviderConfigurationKeys.BaseUrl);
        return new HttpMemoryProviderConfiguration(
            baseUrl,
            ReadString(values, HttpMemoryProviderConfigurationKeys.QueryPath) ?? HttpMemoryProviderEndpoints.Query,
            ReadString(values, HttpMemoryProviderConfigurationKeys.HealthPath) ?? HttpMemoryProviderEndpoints.Health,
            ReadString(values, HttpMemoryProviderConfigurationKeys.ApiKey),
            ReadString(values, HttpMemoryProviderConfigurationKeys.AuthHeaderName) ?? "Authorization",
            ReadString(values, HttpMemoryProviderConfigurationKeys.AuthScheme) ?? "Bearer",
            ReadTimeout(values, options.DefaultTimeout),
            ReadRetryCount(values, options.MaxRetryAttempts));
    }

    public Uri BuildUri(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            throw new ArgumentException("HTTP memory provider path must not be empty.", nameof(relativePath));
        }

        return new Uri(BaseUrl, relativePath.StartsWith("/", StringComparison.Ordinal)
            ? relativePath
            : $"/{relativePath}");
    }

    private static Uri ReadRequiredUri(
        IReadOnlyDictionary<string, JsonElement> values,
        string key)
    {
        var value = ReadString(values, key)
            ?? throw new InvalidOperationException($"HTTP memory provider profile is missing required extension '{key}'.");
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"HTTP memory provider extension '{key}' must be an absolute HTTP(S) URI.");
        }

        return uri;
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
}
