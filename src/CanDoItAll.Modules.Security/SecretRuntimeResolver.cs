using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Security;

public static class SecretRuntimePurposes
{
    public const string AgentProviderApiKey = "agent-provider-api-key";
    public const string AgentMcpEnvironmentVariable = "agent-mcp-environment-variable";
    public const string AgentMcpHeader = "agent-mcp-header";
    public const string StorageCredential = "storage-credential";
}

public sealed record SecretRuntimeRequest(
    Guid SecretId,
    string Purpose,
    IReadOnlyCollection<Guid>? AllowedSecretIds = null,
    string? ConsumerType = null,
    string? ConsumerId = null);

public interface ISecretRuntimeResolver
{
    Task<string?> ResolveValueAsync(SecretRuntimeRequest request, CancellationToken cancellationToken = default);
}

public sealed class SecretRuntimeResolver(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ISecretVault vault,
    ISecretProtector legacyProtector) : ISecretRuntimeResolver
{
    public async Task<string?> ResolveValueAsync(
        SecretRuntimeRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var secret = await dbContext.Set<SecretRecord>()
            .Where(item => item.Id == request.SecretId)
            .Select(item => new ResolvableSecretRecord(item.Id, item.Name, item.EncryptedPayload))
            .SingleOrDefaultAsync(cancellationToken);
        if (secret is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(secret.Payload))
        {
            return null;
        }

        try
        {
            if (SecretVaultRecordReference.TryParse(secret.Payload, out var vaultKey))
            {
                return await vault.GetAsync(vaultKey, cancellationToken)
                    ?? throw new InvalidOperationException("The referenced vault payload was not found.");
            }

            return legacyProtector.Unprotect(secret.Payload);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                $"Secret '{secret.Name}' ({secret.Id:D}) could not be resolved for purpose '{request.Purpose}'.",
                exception);
        }
    }

    private static void ValidateRequest(SecretRuntimeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.SecretId == Guid.Empty)
        {
            throw new ArgumentException("Secret id is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Purpose))
        {
            throw new ArgumentException("Secret resolution purpose is required.", nameof(request));
        }

        if (request.AllowedSecretIds is not null &&
            !request.AllowedSecretIds.Contains(request.SecretId))
        {
            throw new InvalidOperationException(
                $"Secret '{request.SecretId:D}' is not allowed for purpose '{request.Purpose}'.");
        }
    }

    private sealed record ResolvableSecretRecord(Guid Id, string Name, string Payload);
}

public static class SecretVaultRecordReference
{
    private const string Prefix = "vault:v1:";
    private const string SecretRecordScope = "secret-records";

    public static string BuildKey(Guid secretRecordId, Guid materialId)
    {
        if (secretRecordId == Guid.Empty)
        {
            throw new ArgumentException("Secret record id is required.", nameof(secretRecordId));
        }

        if (materialId == Guid.Empty)
        {
            throw new ArgumentException("Secret material id is required.", nameof(materialId));
        }

        return $"{SecretRecordScope}/{secretRecordId:N}/{materialId:N}";
    }

    public static string Create(Guid secretRecordId, Guid materialId)
        => Create(BuildKey(secretRecordId, materialId));

    public static string Create(string vaultKey)
    {
        if (string.IsNullOrWhiteSpace(vaultKey))
        {
            throw new ArgumentException("Vault key is required.", nameof(vaultKey));
        }

        var normalizedKey = vaultKey.Trim();
        if (normalizedKey.Contains('\r', StringComparison.Ordinal) ||
            normalizedKey.Contains('\n', StringComparison.Ordinal))
        {
            throw new ArgumentException("Vault key cannot contain line breaks.", nameof(vaultKey));
        }

        return $"{Prefix}{normalizedKey}";
    }

    public static bool TryParse(string? payload, out string vaultKey)
    {
        vaultKey = string.Empty;
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        var normalizedPayload = payload.Trim();
        if (!normalizedPayload.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        vaultKey = normalizedPayload[Prefix.Length..];
        return !string.IsNullOrWhiteSpace(vaultKey);
    }
}
