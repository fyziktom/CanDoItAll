using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Security;

public sealed class EnvironmentSecretBootstrapService(ISecretVault secretVault) {
    private const string RuntimeBootstrapOpenAiApiKeyEnvironmentVariable = "OPENAI_API_KEY";
    private static readonly Guid DefaultOpenAiApiKeySecretId = Guid.Parse("86F781F1-1E76-4B45-9F1A-42B8CF13D8C7");
    private const string DefaultOpenAiApiKeySecretName = "OpenAI API key";

    public async Task<Guid?> EnsureOpenAiEnvironmentSecretAsync(
        ResolvedDatabaseProfile profile,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(profile);
        var options = new DbContextOptionsBuilder<SecurityDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, profile);
        await using var dbContext = new SecurityDbContext(options.Options);
        var configuredKey = Environment.GetEnvironmentVariable(RuntimeBootstrapOpenAiApiKeyEnvironmentVariable);
        var existingSecret = await dbContext.Set<SecretRecord>()
            .Where(item => item.Id == DefaultOpenAiApiKeySecretId || item.Name == DefaultOpenAiApiKeySecretName)
            .OrderBy(item => item.Id == DefaultOpenAiApiKeySecretId ? 0 : 1)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(configuredKey)) {
            return existingSecret?.Id;
        }

        var normalizedKey = configuredKey.Trim();
        var timestamp = DateTimeOffset.UtcNow;
        if (existingSecret is null) {
            existingSecret = new SecretRecord {
                Id = DefaultOpenAiApiKeySecretId,
                CreatedAtUtc = timestamp
            };
            dbContext.Set<SecretRecord>().Add(existingSecret);
        }

        var oldVaultKey = SecretVaultRecordReference.TryParse(existingSecret.EncryptedPayload, out var parsedOldVaultKey)
            ? parsedOldVaultKey
            : null;
        var existingValue = string.IsNullOrWhiteSpace(oldVaultKey)
            ? null
            : await secretVault.GetAsync(oldVaultKey, cancellationToken);
        var metadataJson = JsonSerializer.Serialize(new {
            source = "environment",
            environmentVariable = RuntimeBootstrapOpenAiApiKeyEnvironmentVariable,
            managedBy = "runtime-bootstrap"
        });
        var metadataChanged =
            !string.Equals(existingSecret.Name, DefaultOpenAiApiKeySecretName, StringComparison.Ordinal) ||
            existingSecret.Kind != SecretKind.ApiKey ||
            !string.Equals(existingSecret.Scope, "workspace", StringComparison.Ordinal) ||
            !string.Equals(existingSecret.MetadataJson, metadataJson, StringComparison.Ordinal);

        if (string.Equals(existingValue, normalizedKey, StringComparison.Ordinal) && !metadataChanged) {
            return existingSecret.Id;
        }

        var newVaultKey = SecretVaultRecordReference.BuildKey(existingSecret.Id, Guid.NewGuid());
        await secretVault.SetAsync(newVaultKey, normalizedKey, cancellationToken);

        existingSecret.Name = DefaultOpenAiApiKeySecretName;
        existingSecret.Kind = SecretKind.ApiKey;
        existingSecret.Scope = "workspace";
        existingSecret.MetadataJson = metadataJson;
        existingSecret.RotationNote = $"Synchronized from {RuntimeBootstrapOpenAiApiKeyEnvironmentVariable}.";
        existingSecret.EncryptedPayload = SecretVaultRecordReference.Create(newVaultKey);
        existingSecret.UpdatedAtUtc = timestamp;

        await dbContext.SaveChangesAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(oldVaultKey) &&
            !string.Equals(oldVaultKey, newVaultKey, StringComparison.Ordinal)) {
            await secretVault.DeleteAsync(oldVaultKey, cancellationToken);
        }

        return existingSecret.Id;
    }

}
