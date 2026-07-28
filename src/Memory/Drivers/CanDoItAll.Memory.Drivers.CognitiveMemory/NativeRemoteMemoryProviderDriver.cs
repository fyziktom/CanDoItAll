using System.Text.Json;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Http;

namespace CanDoItAll.Memory.Drivers.CognitiveMemory;

public sealed class NativeRemoteMemoryProviderOptions
{
    public const string DefaultClientName = "CanDoItAll.Memory.NativeRemote";

    public string ClientName { get; set; } = DefaultClientName;

    public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public int MaxRetryAttempts { get; set; }

    public MemoryProviderResponseSizeLimit ResponseSizeLimit { get; set; } =
        MemoryProviderResponseSizeLimit.Default;

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

        ResponseSizeLimit.EnsureValid();
    }

    internal HttpMemoryProviderOptions ToHttpOptions() =>
        new()
        {
            ClientName = ClientName,
            DefaultTimeout = DefaultTimeout,
            MaxRetryAttempts = MaxRetryAttempts,
            ResponseSizeLimit = ResponseSizeLimit
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
        if (nativeValues.ContainsKey(NativeRemoteMemoryProviderConfigurationKeys.LegacyRawApiKey))
        {
            throw new InvalidOperationException(
                $"Native remote memory provider extension '{NativeRemoteMemoryProviderConfigurationKeys.LegacyRawApiKey}' stores a raw credential. Replace it with '{NativeRemoteMemoryProviderConfigurationKeys.ApiKeyEnvironmentVariable}'.");
        }

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
        CopyOptionalString(
            nativeValues,
            extensions,
            NativeRemoteMemoryProviderConfigurationKeys.ApiKeyEnvironmentVariable,
            HttpMemoryProviderConfigurationKeys.ApiKeyEnvironmentVariable);
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
