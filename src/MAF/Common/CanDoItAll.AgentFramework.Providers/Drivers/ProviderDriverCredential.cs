using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Providers;

public sealed record ProviderDriverCredential(
    bool IsResolved,
    string ApiKey,
    string FailureMessage)
{
    public static ProviderDriverCredential Resolved(string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        return new ProviderDriverCredential(true, apiKey.Trim(), string.Empty);
    }

    public static ProviderDriverCredential Missing(string failureMessage)
    {
        return new ProviderDriverCredential(false, string.Empty, failureMessage);
    }
}

public interface IProviderDriverCredentialResolver
{
    ProviderDriverCredential Resolve(ProviderProfile provider);
}

public sealed class EnvironmentProviderDriverCredentialResolver : IProviderDriverCredentialResolver
{
    public ProviderDriverCredential Resolve(ProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (string.IsNullOrWhiteSpace(provider.ApiKeyEnvironmentVariable))
        {
            return ProviderDriverCredential.Missing($"Provider '{provider.Name}' does not define an API key environment variable.");
        }

        var value = Environment.GetEnvironmentVariable(provider.ApiKeyEnvironmentVariable.Trim());
        return string.IsNullOrWhiteSpace(value)
            ? ProviderDriverCredential.Missing($"Environment variable '{provider.ApiKeyEnvironmentVariable}' is not set for provider '{provider.Name}'.")
            : ProviderDriverCredential.Resolved(value);
    }
}
