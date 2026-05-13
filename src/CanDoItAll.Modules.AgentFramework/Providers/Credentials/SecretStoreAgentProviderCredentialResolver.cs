using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Security;
using Microsoft.Extensions.Configuration;

namespace CanDoItAll.Modules.AgentFramework;

using AgentFrameworkProviderProfile = CanDoItAll.AgentFramework.Models.ProviderProfile;

internal sealed class SecretStoreAgentProviderCredentialResolver(
    ISecretRuntimeResolver secretResolver,
    IConfiguration configuration) : IAgentProviderCredentialResolver
{
    public ProviderCredentialResolution Resolve(
        AgentFrameworkProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var secretRecordId = AgentFrameworkProviderMetadata.ResolveSecretRecordId(provider);
        if (secretRecordId.HasValue)
        {
            try
            {
                var secretValue = secretResolver.ResolveValueAsync(
                        new SecretRuntimeRequest(secretRecordId.Value, SecretRuntimePurposes.AgentProviderApiKey))
                    .ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();
                if (string.IsNullOrWhiteSpace(secretValue))
                {
                    return new ProviderCredentialResolution(
                        string.Empty,
                        $"secret record '{secretRecordId.Value:D}'",
                        $"Secret record '{secretRecordId.Value:D}' was not found or did not contain a usable value.");
                }

                return new ProviderCredentialResolution(
                    secretValue,
                    $"secret record '{secretRecordId.Value:D}'",
                    string.Empty,
                    ShouldPromoteToProcessEnvironment: false);
            }
            catch (Exception exception)
            {
                return new ProviderCredentialResolution(
                    string.Empty,
                    $"secret record '{secretRecordId.Value:D}'",
                    $"Secret record '{secretRecordId.Value:D}' could not be resolved: {exception.Message}");
            }
        }

        if (string.IsNullOrWhiteSpace(provider.ApiKeyEnvironmentVariable))
        {
            return new ProviderCredentialResolution(
                string.Empty,
                "not configured",
                "No secret record or API key environment variable is configured for this provider.");
        }

        var variableName = provider.ApiKeyEnvironmentVariable.Trim();
        var value = AgentProviderEnvironmentCredential.ResolveAndPromote(variableName);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return new ProviderCredentialResolution(
                value,
                $"environment variable '{variableName}'",
                string.Empty);
        }

        var configuredValue = configuration[variableName];
        if (!string.IsNullOrWhiteSpace(configuredValue))
        {
            AgentProviderEnvironmentCredential.PromoteProcessValue(variableName, configuredValue);
            return new ProviderCredentialResolution(
                configuredValue.Trim(),
                $"application configuration key '{variableName}'",
                string.Empty);
        }

        return new ProviderCredentialResolution(
            string.Empty,
            $"environment variable '{variableName}'",
            $"Environment variable '{variableName}' is not set and application configuration key '{variableName}' is empty. {AgentProviderEnvironmentCredential.DescribePresence(variableName)}");
    }
}
