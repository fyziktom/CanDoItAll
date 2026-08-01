namespace CanDoItAll.Memory.Abstractions;

public static class NativeRemoteMemoryProviderConfigurationKeys
{
    public const string LegacyRawApiKey = "native.cognitiveMemory.remote.apiKey";
    public const string ServiceBaseUrl = "native.cognitiveMemory.remote.serviceBaseUrl";
    public const string QueryPath = "native.cognitiveMemory.remote.queryPath";
    public const string HealthPath = "native.cognitiveMemory.remote.healthPath";
    public const string ApiKeyEnvironmentVariable = "native.cognitiveMemory.remote.apiKeyEnvironmentVariable";
    public const string AuthHeaderName = "native.cognitiveMemory.remote.authHeaderName";
    public const string AuthScheme = "native.cognitiveMemory.remote.authScheme";
    public const string TimeoutMilliseconds = "native.cognitiveMemory.remote.timeoutMilliseconds";
    public const string MaxRetryAttempts = "native.cognitiveMemory.remote.maxRetryAttempts";
}
