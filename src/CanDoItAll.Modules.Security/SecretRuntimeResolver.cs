using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Security;

public static class SecretRuntimePurposes
{
    public const string AgentProviderApiKey = "agent-provider-api-key";
    public const string AgentMcpEnvironmentVariable = "agent-mcp-environment-variable";
    public const string AgentMcpHeader = "agent-mcp-header";
    public const string StorageCredential = "storage-credential";
    public const string PluginConnectionSecret = "plugin-connection-secret";
    public const string PluginWorkflowExecutorSecret = "plugin-workflow-executor-secret";
    public const string PluginSettingsValidation = "plugin-settings-validation";
}

public static class SecretRuntimeConsumerTypes
{
    public const string AgentMcp = "agent-mcp";
    public const string ProviderProfile = "provider-profile";
    public const string StorageCredential = "storage-credential";
    public const string WorkflowExecutor = "workflow-executor";
    public const string WorkflowHttpExecutor = "workflow-http";
    public const string Plugin = "plugin";
    public const string PluginConnection = "plugin-connection";
    public const string PluginWorkflowExecutor = "plugin-workflow-executor";
}

public static class SecretRuntimeConsumerIds
{
    public static string AgentMcp(Guid agentId, string capabilityName, string bindingName)
        => $"{RequireGuid(agentId, nameof(agentId))}/{RequireSegment(capabilityName, nameof(capabilityName))}/{RequireSegment(bindingName, nameof(bindingName))}";

    public static string ProviderProfile(Guid providerProfileId)
        => RequireGuid(providerProfileId, nameof(providerProfileId)).ToString("D");

    public static string StorageRuntime()
        => "storage-runtime";

    public static string StorageCatalog(Guid storageCatalogId)
        => RequireGuid(storageCatalogId, nameof(storageCatalogId)).ToString("D");

    public static string WorkflowNode(Guid workflowId, string nodeId)
        => $"{RequireGuid(workflowId, nameof(workflowId)):D}/{RequireSegment(nodeId, nameof(nodeId))}";

    public static string PluginConnection(string pluginId, string connectionId)
        => $"{RequireSegment(pluginId, nameof(pluginId))}/{RequireSegment(connectionId, nameof(connectionId))}";

    private static Guid RequireGuid(Guid value, string parameterName)
        => value == Guid.Empty
            ? throw new ArgumentException("Identifier is required.", parameterName)
            : value;

    private static string RequireSegment(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Identifier segment is required.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Contains('\r', StringComparison.Ordinal) ||
            normalized.Contains('\n', StringComparison.Ordinal) ||
            normalized.Contains('/', StringComparison.Ordinal))
        {
            throw new ArgumentException("Identifier segment cannot contain slashes or line breaks.", parameterName);
        }

        return normalized;
    }
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
    private static readonly HashSet<string> StrictBindingConsumerTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        SecretRuntimeConsumerTypes.Plugin,
        SecretRuntimeConsumerTypes.PluginConnection,
        SecretRuntimeConsumerTypes.PluginWorkflowExecutor
    };

    public async Task<string?> ResolveValueAsync(
        SecretRuntimeRequest request,
        CancellationToken cancellationToken = default)
    {
        var authorization = ValidateRequest(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var secret = await dbContext.Set<SecretRecord>()
            .Where(item => item.Id == request.SecretId)
            .Select(item => new ResolvableSecretRecord(item.Id, item.Name, item.EncryptedPayload))
            .SingleOrDefaultAsync(cancellationToken);
        if (secret is null)
        {
            return null;
        }

        await AuthorizeAsync(dbContext, request, authorization, cancellationToken);

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

    private static SecretRuntimeAuthorization ValidateRequest(SecretRuntimeRequest request)
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

        var purpose = request.Purpose.Trim();
        var consumerType = NormalizeOptional(request.ConsumerType);
        var consumerId = NormalizeOptional(request.ConsumerId);
        if ((consumerType is null) != (consumerId is null))
        {
            throw new ArgumentException("Secret runtime consumer type and consumer id must be supplied together.", nameof(request));
        }

        if (request.AllowedSecretIds is not null &&
            !request.AllowedSecretIds.Contains(request.SecretId))
        {
            throw new InvalidOperationException(
                $"Secret '{request.SecretId:D}' is not allowed for purpose '{request.Purpose}'.");
        }

        return new SecretRuntimeAuthorization(
            purpose,
            consumerType,
            consumerId,
            consumerType is not null && StrictBindingConsumerTypes.Contains(consumerType));
    }

    private static async Task AuthorizeAsync(
        AppDbContext dbContext,
        SecretRuntimeRequest request,
        SecretRuntimeAuthorization authorization,
        CancellationToken cancellationToken)
    {
        if (authorization.ConsumerType is null || authorization.ConsumerId is null)
        {
            return;
        }

        var hasPersistedBinding = await dbContext.Set<SecretReference>().AnyAsync(reference =>
            reference.SecretRecordId == request.SecretId &&
            reference.ContextType == authorization.ConsumerType &&
            reference.ContextId == authorization.ConsumerId &&
            reference.Purpose == authorization.Purpose,
            cancellationToken);
        if (hasPersistedBinding)
        {
            return;
        }

        if (!authorization.RequiresPersistedBinding && request.AllowedSecretIds is not null)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Secret '{request.SecretId:D}' is not authorized for consumer '{authorization.ConsumerType}/{authorization.ConsumerId}' and purpose '{authorization.Purpose}'.");
    }

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Contains('\r', StringComparison.Ordinal) ||
            normalized.Contains('\n', StringComparison.Ordinal))
        {
            throw new ArgumentException("Secret runtime consumer values cannot contain line breaks.");
        }

        return normalized;
    }

    private sealed record ResolvableSecretRecord(Guid Id, string Name, string Payload);

    private sealed record SecretRuntimeAuthorization(
        string Purpose,
        string? ConsumerType,
        string? ConsumerId,
        bool RequiresPersistedBinding);
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
