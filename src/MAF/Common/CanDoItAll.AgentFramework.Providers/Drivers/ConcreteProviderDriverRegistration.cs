namespace CanDoItAll.AgentFramework.Providers;

public static class ConcreteProviderDriverRegistration
{
    public static AgentProviderDriverRegistryBuilder AddOpenAiProviderDriver(
        this AgentProviderDriverRegistryBuilder builder,
        HttpClient httpClient,
        IProviderDriverCredentialResolver credentialResolver,
        IProviderHttpClientSelector? httpClientSelector = null,
        IProviderInferenceRelayTransport? inferenceRelayTransport = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddDriver(new OpenAiProviderDriver(
            httpClient,
            credentialResolver,
            httpClientSelector,
            inferenceRelayTransport));
    }

    public static AgentProviderDriverRegistryBuilder AddAzureOpenAiProviderDriver(
        this AgentProviderDriverRegistryBuilder builder,
        HttpClient httpClient,
        IProviderDriverCredentialResolver credentialResolver)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddDriver(new AzureOpenAiProviderDriver(httpClient, credentialResolver));
    }

    public static AgentProviderDriverRegistryBuilder AddOllamaProviderDriver(
        this AgentProviderDriverRegistryBuilder builder,
        HttpClient httpClient,
        IProviderInferenceRelayTransport? inferenceRelayTransport = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddDriver(new OllamaProviderDriver(
            httpClient,
            inferenceRelayTransport));
    }

    public static AgentProviderDriverRegistryBuilder AddComfyUiProviderDriver(
        this AgentProviderDriverRegistryBuilder builder,
        HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddDriver(new ComfyUiProviderDriver(httpClient));
    }
}
