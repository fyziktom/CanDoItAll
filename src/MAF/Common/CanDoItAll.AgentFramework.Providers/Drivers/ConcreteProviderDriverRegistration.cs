namespace CanDoItAll.AgentFramework.Providers;

public static class ConcreteProviderDriverRegistration
{
    public static AgentProviderDriverRegistryBuilder AddOpenAiProviderDriver(
        this AgentProviderDriverRegistryBuilder builder,
        HttpClient httpClient,
        IProviderDriverCredentialResolver credentialResolver)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddDriver(new OpenAiProviderDriver(httpClient, credentialResolver));
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
        HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddDriver(new OllamaProviderDriver(httpClient));
    }

    public static AgentProviderDriverRegistryBuilder AddComfyUiProviderDriver(
        this AgentProviderDriverRegistryBuilder builder,
        HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddDriver(new ComfyUiProviderDriver(httpClient));
    }
}
