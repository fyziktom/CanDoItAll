using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Http;

namespace CanDoItAll.Modules.Memory.Services;

internal sealed record MemoryProviderHttpExtensionKeys(
    string BaseUrl,
    string QueryPath,
    string HealthPath,
    string ApiKeyEnvironmentVariable,
    string AuthHeaderName,
    string AuthScheme,
    string TimeoutMilliseconds,
    string MaxRetryAttempts)
{
    private static readonly MemoryProviderHttpExtensionKeys Http = new(
        HttpMemoryProviderConfigurationKeys.BaseUrl,
        HttpMemoryProviderConfigurationKeys.QueryPath,
        HttpMemoryProviderConfigurationKeys.HealthPath,
        HttpMemoryProviderConfigurationKeys.ApiKeyEnvironmentVariable,
        HttpMemoryProviderConfigurationKeys.AuthHeaderName,
        HttpMemoryProviderConfigurationKeys.AuthScheme,
        HttpMemoryProviderConfigurationKeys.TimeoutMilliseconds,
        HttpMemoryProviderConfigurationKeys.MaxRetryAttempts);

    private static readonly MemoryProviderHttpExtensionKeys Native = new(
        NativeRemoteMemoryProviderConfigurationKeys.ServiceBaseUrl,
        NativeRemoteMemoryProviderConfigurationKeys.QueryPath,
        NativeRemoteMemoryProviderConfigurationKeys.HealthPath,
        NativeRemoteMemoryProviderConfigurationKeys.ApiKeyEnvironmentVariable,
        NativeRemoteMemoryProviderConfigurationKeys.AuthHeaderName,
        NativeRemoteMemoryProviderConfigurationKeys.AuthScheme,
        NativeRemoteMemoryProviderConfigurationKeys.TimeoutMilliseconds,
        NativeRemoteMemoryProviderConfigurationKeys.MaxRetryAttempts);

    public static IReadOnlySet<string> AllManagedKeys { get; } = Http.Values()
        .Concat(Native.Values())
        .ToHashSet(StringComparer.Ordinal);

    public static MemoryProviderHttpExtensionKeys For(MemoryProviderDriverKind driverKind) => driverKind switch
    {
        MemoryProviderDriverKind.NativeRemote => Native,
        _ => Http
    };

    private IEnumerable<string> Values()
    {
        yield return BaseUrl;
        yield return QueryPath;
        yield return HealthPath;
        yield return ApiKeyEnvironmentVariable;
        yield return AuthHeaderName;
        yield return AuthScheme;
        yield return TimeoutMilliseconds;
        yield return MaxRetryAttempts;
    }
}
