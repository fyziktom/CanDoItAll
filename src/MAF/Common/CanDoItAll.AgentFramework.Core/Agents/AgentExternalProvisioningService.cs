using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public enum AgentExternalProvisioningFailureKind
{
    InvalidRequest,
    NotFound,
    Conflict,
    PreconditionFailed
}

public sealed class AgentExternalProvisioningException(
    AgentExternalProvisioningFailureKind kind,
    string code,
    string message) : InvalidOperationException(message)
{
    public AgentExternalProvisioningFailureKind Kind { get; } = kind;
    public string Code { get; } = code;
}

internal sealed class AgentExternalProvisioningService(
    ISandboxWorkspaceStore store,
    IProviderProfileService providerProfileService)
{
    private const int MaximumRetainedOperations = 2048;
    private static readonly HashSet<string> RawSecretPropertyNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "apiKey",
            "accessToken",
            "bearerToken",
            "clientSecret",
            "password",
            "privateKey",
            "refreshToken"
        };
    private static readonly JsonSerializerOptions FingerprintSerializerOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<AgentExternalProvisioningResource> GetAsync(
        string externalNamespace,
        string key,
        CancellationToken cancellationToken = default)
    {
        var identity = NormalizeIdentity(externalNamespace, key);
        var catalog = await store.LoadCatalogAsync(cancellationToken);
        var binding = FindBinding(catalog.AgentExternalBindings, identity)
            ?? throw Failure(
                AgentExternalProvisioningFailureKind.NotFound,
                "agents.external-key-not-found",
                "No agent is bound to the requested external identity in this workspace.");
        if (!catalog.Agents.Any(item => item.Id == binding.AgentId))
        {
            throw Failure(
                AgentExternalProvisioningFailureKind.NotFound,
                "agents.external-key-agent-not-found",
                "The external identity binding does not resolve to an agent in this workspace.");
        }

        return ToResource(binding);
    }

    public async Task<AgentExternalProvisioningReceipt> UpsertAsync(
        AgentExternalProvisioningCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateIdempotencyKey(command.IdempotencyKey);
        ValidateExternalAgent(command.Agent);
        var identity = NormalizeIdentity(command.Namespace, command.Key);
        var idempotencyKey = command.IdempotencyKey.Trim();
        var expectedVersion = NormalizeVersion(command.ExpectedConfigurationVersion);
        var requestFingerprint = CreateRequestFingerprint(identity, expectedVersion, command.Agent);
        AgentExternalProvisioningReceipt? result = null;

        await store.UpdateWorkspaceAsync(document =>
        {
            var previousOperation = document.AgentExternalProvisioningOperations.FirstOrDefault(operation =>
                string.Equals(operation.IdempotencyKey, idempotencyKey, StringComparison.Ordinal));
            if (previousOperation is not null)
            {
                if (!string.Equals(
                        previousOperation.RequestFingerprint,
                        requestFingerprint,
                        StringComparison.Ordinal))
                {
                    throw Failure(
                        AgentExternalProvisioningFailureKind.Conflict,
                        "agents.external-key-idempotency-conflict",
                        "The idempotency key was already used with a different provisioning request.");
                }

                result = previousOperation.Receipt with { Replayed = true };
                return document;
            }

            var binding = FindBinding(document.AgentExternalBindings, identity);
            var created = binding is null;
            if (created && expectedVersion is not null)
            {
                throw Failure(
                    AgentExternalProvisioningFailureKind.PreconditionFailed,
                    "agents.external-key-create-version-conflict",
                    "If-Match cannot be supplied when creating a new external identity binding.");
            }

            if (!created && expectedVersion is null)
            {
                throw Failure(
                    AgentExternalProvisioningFailureKind.PreconditionFailed,
                    "agents.external-key-version-required",
                    "If-Match is required when updating an existing external identity binding.");
            }

            if (binding is not null &&
                !string.Equals(binding.ConfigurationVersion, expectedVersion, StringComparison.Ordinal))
            {
                throw Failure(
                    AgentExternalProvisioningFailureKind.PreconditionFailed,
                    "agents.external-key-version-conflict",
                    "The supplied configuration version is stale.");
            }

            var existingAgent = binding is null
                ? null
                : document.Agents.FirstOrDefault(item => item.Id == binding.AgentId)
                    ?? throw Failure(
                        AgentExternalProvisioningFailureKind.Conflict,
                        "agents.external-key-agent-missing",
                        "The external identity binding points to an agent that no longer exists.");
            var agentId = binding?.AgentId ?? Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;
            var editor = CopyForPersistence(command.Agent);
            var definition = AgentDefinitionFactory.Create(
                document.ToCatalog(),
                editor,
                agentId,
                existingAgent,
                now,
                providerProfileService,
                "External-key provisioning");
            var configurationVersion = AgentConfigurationVersion.Create(definition);
            var isNoOp = binding is not null &&
                         string.Equals(
                             binding.ConfigurationVersion,
                             configurationVersion,
                             StringComparison.Ordinal);
            var persistedAgent = isNoOp ? existingAgent! : definition;
            var persistedBinding = isNoOp
                ? binding!
                : new AgentExternalBindingRecord(
                    identity.Namespace,
                    identity.Key,
                    agentId,
                    configurationVersion,
                    definition.Status == AgentLifecycleStatus.Archived,
                    now);
            var warnings = isNoOp
                ? new[] { "The requested configuration already matches the stored agent." }
                : [];
            var receipt = new AgentExternalProvisioningReceipt(
                identity.Namespace,
                identity.Key,
                agentId,
                persistedBinding.ConfigurationVersion,
                Created: created,
                Replayed: false,
                Archived: persistedBinding.IsArchived,
                Warnings: warnings);
            var operation = new AgentExternalProvisioningOperationRecord(
                idempotencyKey,
                requestFingerprint,
                receipt,
                now);
            result = receipt;

            return document with
            {
                Agents = isNoOp
                    ? document.Agents
                    : document.Agents
                        .Where(item => item.Id != agentId)
                        .Append(persistedAgent)
                        .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                AgentExternalBindings = isNoOp
                    ? document.AgentExternalBindings
                    : document.AgentExternalBindings
                        .Where(item => !HasIdentity(item, identity))
                        .Append(persistedBinding)
                        .OrderBy(item => item.Namespace, StringComparer.Ordinal)
                        .ThenBy(item => item.Key, StringComparer.Ordinal)
                        .ToList(),
                AgentExternalProvisioningOperations = AppendOperation(
                    document.AgentExternalProvisioningOperations,
                    operation)
            };
        }, cancellationToken);

        return result ?? throw new InvalidOperationException(
            "External agent provisioning completed without a receipt.");
    }

    public async Task<AgentExternalProvisioningReceipt> ArchiveAsync(
        AgentExternalArchiveCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateIdempotencyKey(command.IdempotencyKey);
        var identity = NormalizeIdentity(command.Namespace, command.Key);
        var idempotencyKey = command.IdempotencyKey.Trim();
        var expectedVersion = NormalizeVersion(command.ExpectedConfigurationVersion)
            ?? throw Failure(
                AgentExternalProvisioningFailureKind.PreconditionFailed,
                "agents.external-key-version-required",
                "If-Match is required when archiving an external identity binding.");
        var requestFingerprint = CreateArchiveFingerprint(identity, expectedVersion);
        AgentExternalProvisioningReceipt? result = null;

        await store.UpdateWorkspaceAsync(document =>
        {
            var previousOperation = document.AgentExternalProvisioningOperations.FirstOrDefault(operation =>
                string.Equals(operation.IdempotencyKey, idempotencyKey, StringComparison.Ordinal));
            if (previousOperation is not null)
            {
                if (!string.Equals(
                        previousOperation.RequestFingerprint,
                        requestFingerprint,
                        StringComparison.Ordinal))
                {
                    throw Failure(
                        AgentExternalProvisioningFailureKind.Conflict,
                        "agents.external-key-idempotency-conflict",
                        "The idempotency key was already used with a different provisioning request.");
                }

                result = previousOperation.Receipt with { Replayed = true };
                return document;
            }

            var binding = FindBinding(document.AgentExternalBindings, identity)
                ?? throw Failure(
                    AgentExternalProvisioningFailureKind.NotFound,
                    "agents.external-key-not-found",
                    "No agent is bound to the requested external identity in this workspace.");
            if (!string.Equals(binding.ConfigurationVersion, expectedVersion, StringComparison.Ordinal))
            {
                throw Failure(
                    AgentExternalProvisioningFailureKind.PreconditionFailed,
                    "agents.external-key-version-conflict",
                    "The supplied configuration version is stale.");
            }

            var agent = document.Agents.FirstOrDefault(item => item.Id == binding.AgentId)
                ?? throw Failure(
                    AgentExternalProvisioningFailureKind.Conflict,
                    "agents.external-key-agent-missing",
                    "The external identity binding points to an agent that no longer exists.");
            var now = DateTimeOffset.UtcNow;
            var archivedAgent = agent with
            {
                Status = AgentLifecycleStatus.Archived,
                UpdatedAtUtc = now
            };
            var archivedBinding = binding with
            {
                ConfigurationVersion = AgentConfigurationVersion.Create(archivedAgent),
                IsArchived = true,
                UpdatedAtUtc = now
            };
            var receipt = new AgentExternalProvisioningReceipt(
                identity.Namespace,
                identity.Key,
                binding.AgentId,
                archivedBinding.ConfigurationVersion,
                Created: false,
                Replayed: false,
                Archived: true,
                Warnings: []);
            var operation = new AgentExternalProvisioningOperationRecord(
                idempotencyKey,
                requestFingerprint,
                receipt,
                now);
            result = receipt;

            return document with
            {
                Agents = document.Agents
                    .Where(item => item.Id != agent.Id)
                    .Append(archivedAgent)
                    .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                AgentExternalBindings = document.AgentExternalBindings
                    .Where(item => !HasIdentity(item, identity))
                    .Append(archivedBinding)
                    .OrderBy(item => item.Namespace, StringComparer.Ordinal)
                    .ThenBy(item => item.Key, StringComparer.Ordinal)
                    .ToList(),
                AgentExternalProvisioningOperations = AppendOperation(
                    document.AgentExternalProvisioningOperations,
                    operation)
            };
        }, cancellationToken);

        return result ?? throw new InvalidOperationException(
            "External agent archive completed without a receipt.");
    }

    private static AgentEditorModel CopyForPersistence(AgentEditorModel source)
    {
        return new AgentEditorModel
        {
            Name = source.Name,
            RoleTitle = source.RoleTitle,
            Summary = source.Summary,
            Instructions = source.Instructions,
            AvatarImageUrl = source.AvatarImageUrl,
            Status = source.Status,
            ProviderProfileId = source.ProviderProfileId,
            Model = source.Model,
            Workload = source.Workload,
            ChatHistoryMode = source.ChatHistoryMode,
            Temperature = source.Temperature,
            RequirePerServiceCallChatHistoryPersistence =
                source.RequirePerServiceCallChatHistoryPersistence,
            EnableBackgroundResponses = source.EnableBackgroundResponses,
            ConfigurationJson = source.ConfigurationJson,
            IsTemplate = source.IsTemplate,
            TemplateKey = source.TemplateKey,
            Permissions = source.Permissions with { AllowedSecrets = [] },
            AllowedSecretReferences = [],
            ProjectStructureAccess = source.ProjectStructureAccess ?? new(),
            ProcessAccess = source.ProcessAccess ?? new(),
            WorkspaceToolAccess = source.WorkspaceToolAccess ?? new(),
            ImageGenerationAccess = source.ImageGenerationAccess ?? new(),
            VoiceAccess = source.VoiceAccess ?? new(),
            MemoryAccess = source.MemoryAccess ?? new(),
            SelectedCapabilityIds = source.SelectedCapabilityIds
                .Distinct()
                .OrderBy(item => item)
                .ToList(),
            Tags = source.Tags
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static string CreateRequestFingerprint(
        AgentExternalIdentity identity,
        string? expectedVersion,
        AgentEditorModel agent)
    {
        var material = new
        {
            Operation = "upsert",
            Identity = AgentExternalIdentityNormalizer.ToCanonicalString(identity),
            ExpectedVersion = expectedVersion,
            Agent = CopyForPersistence(agent)
        };
        return Convert.ToHexString(SHA256.HashData(
            JsonSerializer.SerializeToUtf8Bytes(material, FingerprintSerializerOptions)));
    }

    private static string CreateArchiveFingerprint(
        AgentExternalIdentity identity,
        string expectedVersion)
    {
        var material = string.Join(
            "\n",
            "archive",
            AgentExternalIdentityNormalizer.ToCanonicalString(identity),
            expectedVersion);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    private static IReadOnlyList<AgentExternalProvisioningOperationRecord> AppendOperation(
        IReadOnlyList<AgentExternalProvisioningOperationRecord> operations,
        AgentExternalProvisioningOperationRecord operation)
    {
        return operations
            .Append(operation)
            .OrderByDescending(item => item.CompletedAtUtc)
            .Take(MaximumRetainedOperations)
            .ToList();
    }

    private static AgentExternalBindingRecord? FindBinding(
        IReadOnlyList<AgentExternalBindingRecord> bindings,
        AgentExternalIdentity identity)
        => bindings.FirstOrDefault(item => HasIdentity(item, identity));

    private static bool HasIdentity(
        AgentExternalBindingRecord binding,
        AgentExternalIdentity identity)
        => string.Equals(binding.Namespace, identity.Namespace, StringComparison.Ordinal) &&
           string.Equals(binding.Key, identity.Key, StringComparison.Ordinal);

    private static AgentExternalProvisioningResource ToResource(AgentExternalBindingRecord binding)
        => new(
            binding.Namespace,
            binding.Key,
            binding.AgentId,
            binding.ConfigurationVersion,
            binding.IsArchived,
            binding.UpdatedAtUtc);

    private static AgentExternalIdentity NormalizeIdentity(string externalNamespace, string key)
    {
        try
        {
            return AgentExternalIdentityNormalizer.Normalize(externalNamespace, key);
        }
        catch (ArgumentException exception)
        {
            throw Failure(
                AgentExternalProvisioningFailureKind.InvalidRequest,
                "agents.external-key-invalid",
                exception.Message);
        }
    }

    private static string? NormalizeVersion(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return null;
        }

        var normalized = version.Trim().Trim('"');
        if (normalized.Length != 64 || normalized.Any(character => !Uri.IsHexDigit(character)))
        {
            throw Failure(
                AgentExternalProvisioningFailureKind.InvalidRequest,
                "agents.external-key-version-invalid",
                "If-Match must contain a 64-character hexadecimal configuration version.");
        }

        return normalized.ToUpperInvariant();
    }

    private static void ValidateIdempotencyKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 200)
        {
            throw Failure(
                AgentExternalProvisioningFailureKind.InvalidRequest,
                "agents.external-key-idempotency-key-invalid",
                "Idempotency-Key is required and must not exceed 200 characters.");
        }
    }

    private static void ValidateExternalAgent(AgentEditorModel? agent)
    {
        if (agent is null || string.IsNullOrWhiteSpace(agent.Name))
        {
            throw Failure(
                AgentExternalProvisioningFailureKind.InvalidRequest,
                "agents.external-key-agent-invalid",
                "An agent with a non-empty name is required.");
        }

        if (agent.Id.HasValue || agent.ExpectedUpdatedAtUtc.HasValue)
        {
            throw Failure(
                AgentExternalProvisioningFailureKind.InvalidRequest,
                "agents.external-key-server-identity-only",
                "Agent ID and expectedUpdatedAtUtc are server-owned for external-key provisioning.");
        }

        if (agent.Permissions is null ||
            agent.AllowedSecretReferences is null ||
            agent.Permissions.NormalizedAllowedSecrets.Count > 0 ||
            agent.AllowedSecretReferences.Count > 0)
        {
            throw Failure(
                AgentExternalProvisioningFailureKind.InvalidRequest,
                "agents.external-key-secret-reference-forbidden",
                "External provisioning cannot bind workspace secret references.");
        }

        if (agent.SelectedCapabilityIds is null || agent.Tags is null)
        {
            throw Failure(
                AgentExternalProvisioningFailureKind.InvalidRequest,
                "agents.external-key-agent-invalid",
                "Capability and tag collections cannot be null.");
        }

        if (ContainsRawSecretMaterial(agent.ConfigurationJson))
        {
            throw Failure(
                AgentExternalProvisioningFailureKind.InvalidRequest,
                "agents.external-key-secret-material-forbidden",
                "External provisioning configuration cannot contain raw secret material.");
        }
    }

    private static bool ContainsRawSecretMaterial(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            return ContainsRawSecretMaterial(document.RootElement);
        }
        catch (JsonException exception)
        {
            throw Failure(
                AgentExternalProvisioningFailureKind.InvalidRequest,
                "agents.external-key-configuration-json-invalid",
                $"Agent configurationJson must be valid JSON: {exception.Message}");
        }
    }

    private static bool ContainsRawSecretMaterial(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (RawSecretPropertyNames.Contains(property.Name) &&
                    property.Value.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(property.Value.GetString()))
                {
                    return true;
                }

                if (ContainsRawSecretMaterial(property.Value))
                {
                    return true;
                }

                if (property.Name.EndsWith("Json", StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(property.Value.GetString()))
                {
                    try
                    {
                        using var embedded = JsonDocument.Parse(property.Value.GetString()!);
                        if (ContainsRawSecretMaterial(embedded.RootElement))
                        {
                            return true;
                        }
                    }
                    catch (JsonException)
                    {
                        // Non-JSON strings in extension fields are handled by the owning metadata parser.
                    }
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (ContainsRawSecretMaterial(item))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static AgentExternalProvisioningException Failure(
        AgentExternalProvisioningFailureKind kind,
        string code,
        string message)
        => new(kind, code, message);
}
