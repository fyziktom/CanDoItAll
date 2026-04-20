using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Security;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

using AgentFrameworkProviderProfile = CanDoItAll.AgentFramework.Models.ProviderProfile;

internal sealed class SecretStoreAgentProviderCredentialResolver(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ISecretProtector secretProtector) : IAgentProviderCredentialResolver
{
    public ProviderCredentialResolution Resolve(
        AgentFrameworkProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var secretRecordId = AgentFrameworkProviderMetadata.ResolveSecretRecordId(provider);
        if (secretRecordId.HasValue)
        {
            using var dbContext = dbContextFactory.CreateDbContext();
            var secret = dbContext.Set<SecretRecord>()
                .SingleOrDefault(item => item.Id == secretRecordId.Value);
            if (secret is null)
            {
                return new ProviderCredentialResolution(
                    string.Empty,
                    $"secret record '{secretRecordId.Value:D}'",
                    $"Secret record '{secretRecordId.Value:D}' was not found.");
            }

            try
            {
                return new ProviderCredentialResolution(
                    secretProtector.Unprotect(secret.EncryptedPayload),
                    $"secret record '{secret.Name}'",
                    string.Empty);
            }
            catch (Exception exception)
            {
                return new ProviderCredentialResolution(
                    string.Empty,
                    $"secret record '{secret.Name}'",
                    $"Secret record '{secret.Name}' could not be decrypted: {exception.Message}");
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
        var value = Environment.GetEnvironmentVariable(variableName)?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(value)
            ? new ProviderCredentialResolution(
                string.Empty,
                $"environment variable '{variableName}'",
                $"Environment variable '{variableName}' is not set.")
            : new ProviderCredentialResolution(
                value,
                $"environment variable '{variableName}'",
                string.Empty);
    }
}
