using System.Text.Json;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.Memory.Http;

public static class NativeRemoteMemoryProviderConfigurationKeys
{
    public const string ServiceBaseUrl = "native.cognitiveMemory.remote.serviceBaseUrl";
    public const string QueryPath = "native.cognitiveMemory.remote.queryPath";
    public const string HealthPath = "native.cognitiveMemory.remote.healthPath";
    public const string ApiKey = "native.cognitiveMemory.remote.apiKey";
    public const string AuthHeaderName = "native.cognitiveMemory.remote.authHeaderName";
    public const string AuthScheme = "native.cognitiveMemory.remote.authScheme";
    public const string TimeoutMilliseconds = "native.cognitiveMemory.remote.timeoutMilliseconds";
    public const string MaxRetryAttempts = "native.cognitiveMemory.remote.maxRetryAttempts";
}

public sealed class NativeRemoteMemoryProviderOptions
{
    public const string DefaultClientName = "CanDoItAll.Memory.NativeRemote";

    public string ClientName { get; set; } = DefaultClientName;

    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public int MaxRetryAttempts { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ClientName))
        {
            throw new InvalidOperationException("Native remote memory provider client name must be configured.");
        }

        if (DefaultTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Native remote memory provider timeout must be positive.");
        }

        if (MaxRetryAttempts < 0)
        {
            throw new InvalidOperationException("Native remote memory provider retry count cannot be negative.");
        }
    }

    internal HttpMemoryProviderOptions ToHttpOptions() =>
        new()
        {
            ClientName = ClientName,
            DefaultTimeout = DefaultTimeout,
            MaxRetryAttempts = MaxRetryAttempts
        };
}

public sealed class NativeRemoteMemoryProviderDriver(
    IHttpClientFactory httpClientFactory,
    NativeRemoteMemoryProviderOptions options) : IMemoryProviderDriver, IMemoryProviderHealthDriver
{
    private readonly HttpMemoryProviderDriver innerDriver = new(httpClientFactory, options.ToHttpOptions());

    public MemoryProviderDriverKind DriverKind => MemoryProviderDriverKind.NativeRemote;

    public Task<MemoryProviderDriverResult> ExecuteContextQueryAsync(
        MemoryProviderProfile provider,
        MemoryOperationRecord operation,
        MemoryContextQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        return innerDriver.ExecuteContextQueryAsync(
            CreateHttpCompatibleProfile(provider),
            operation,
            request,
            cancellationToken);
    }

    public Task<MemoryProviderHealth> GetHealthAsync(
        MemoryProviderProfile provider,
        CancellationToken cancellationToken = default)
    {
        return innerDriver.GetHealthAsync(CreateHttpCompatibleProfile(provider), cancellationToken);
    }

    private static MemoryProviderProfile CreateHttpCompatibleProfile(MemoryProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (provider.DriverKind != MemoryProviderDriverKind.NativeRemote)
        {
            throw new InvalidOperationException($"Provider '{provider.InstanceId}' is not configured for the native remote driver.");
        }

        var nativeValues = provider.Manifest.Extensions.Values;
        var extensions = new List<(string Key, JsonElement Value)>
        {
            (HttpMemoryProviderConfigurationKeys.BaseUrl, JsonSerializer.SerializeToElement(ReadRequiredString(
                nativeValues,
                NativeRemoteMemoryProviderConfigurationKeys.ServiceBaseUrl))),
            (HttpMemoryProviderConfigurationKeys.QueryPath, JsonSerializer.SerializeToElement(ReadString(
                nativeValues,
                NativeRemoteMemoryProviderConfigurationKeys.QueryPath) ?? HttpMemoryProviderEndpoints.Query)),
            (HttpMemoryProviderConfigurationKeys.HealthPath, JsonSerializer.SerializeToElement(ReadString(
                nativeValues,
                NativeRemoteMemoryProviderConfigurationKeys.HealthPath) ?? HttpMemoryProviderEndpoints.Health))
        };
        CopyOptionalString(nativeValues, extensions, NativeRemoteMemoryProviderConfigurationKeys.ApiKey, HttpMemoryProviderConfigurationKeys.ApiKey);
        CopyOptionalString(nativeValues, extensions, NativeRemoteMemoryProviderConfigurationKeys.AuthHeaderName, HttpMemoryProviderConfigurationKeys.AuthHeaderName);
        CopyOptionalString(nativeValues, extensions, NativeRemoteMemoryProviderConfigurationKeys.AuthScheme, HttpMemoryProviderConfigurationKeys.AuthScheme);
        CopyOptionalValue(nativeValues, extensions, NativeRemoteMemoryProviderConfigurationKeys.TimeoutMilliseconds, HttpMemoryProviderConfigurationKeys.TimeoutMilliseconds);
        CopyOptionalValue(nativeValues, extensions, NativeRemoteMemoryProviderConfigurationKeys.MaxRetryAttempts, HttpMemoryProviderConfigurationKeys.MaxRetryAttempts);

        return provider with
        {
            DriverKind = MemoryProviderDriverKind.Http,
            Manifest = provider.Manifest with { Extensions = MemoryExtensionData.From(extensions.ToArray()) }
        };
    }

    private static void CopyOptionalString(
        IReadOnlyDictionary<string, JsonElement> source,
        List<(string Key, JsonElement Value)> target,
        string sourceKey,
        string targetKey)
    {
        var value = ReadString(source, sourceKey);
        if (value is not null)
        {
            target.Add((targetKey, JsonSerializer.SerializeToElement(value)));
        }
    }

    private static void CopyOptionalValue(
        IReadOnlyDictionary<string, JsonElement> source,
        List<(string Key, JsonElement Value)> target,
        string sourceKey,
        string targetKey)
    {
        if (source.TryGetValue(sourceKey, out var value) && value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            target.Add((targetKey, value.Clone()));
        }
    }

    private static string ReadRequiredString(
        IReadOnlyDictionary<string, JsonElement> values,
        string key) =>
        ReadString(values, key)
            ?? throw new InvalidOperationException($"Native remote memory provider profile is missing required extension '{key}'.");

    private static string? ReadString(
        IReadOnlyDictionary<string, JsonElement> values,
        string key)
    {
        if (!values.TryGetValue(key, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : throw new InvalidOperationException($"Native remote memory provider extension '{key}' must be a string.");
    }
}
