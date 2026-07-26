using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public enum AgentPackageImportFailureKind
{
    InvalidRequest,
    Conflict,
    PreconditionFailed
}

public sealed class AgentPackageImportException(
    AgentPackageImportFailureKind kind,
    string code,
    string message) : InvalidOperationException(message)
{
    public AgentPackageImportFailureKind Kind { get; } = kind;
    public string Code { get; } = code;
}

internal sealed class AgentPackageImportService(
    ISandboxWorkspaceStore store,
    IAgentPackageService packageService,
    IProviderProfileService providerProfileService)
{
    private const int MaximumRetainedOperations = 2048;

    private static readonly JsonSerializerOptions FingerprintSerializerOptions = new(JsonSerializerDefaults.Web);

    public async Task<AgentPackageImportReceipt> ImportAsync(
        Stream package,
        AgentPackageImportCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(command);
        ValidateCommand(command);
        var externalIdentity = NormalizeExternalIdentity(command.ExternalNamespace, command.ExternalKey);
        command = command with
        {
            IdempotencyKey = command.IdempotencyKey.Trim(),
            ExternalNamespace = externalIdentity.Namespace,
            ExternalKey = externalIdentity.Key
        };

        var imported = await packageService.ImportAsync(
            package,
            new AgentPackageReadOptions
            {
                ExpectedPackageSha256 = command.ExpectedPackageSha256
            },
            cancellationToken);
        var requestFingerprint = CreateRequestFingerprint(command, imported.PackageSha256);
        AgentPackageImportReceipt? result = null;

        await store.UpdateWorkspaceAsync(document =>
        {
            var previousOperation = document.AgentPackageImportOperations.FirstOrDefault(operation =>
                string.Equals(operation.IdempotencyKey, command.IdempotencyKey, StringComparison.Ordinal));
            if (previousOperation is not null)
            {
                if (!string.Equals(previousOperation.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
                {
                    throw new AgentPackageImportException(
                        AgentPackageImportFailureKind.Conflict,
                        "agent-package.idempotency-conflict",
                        "The idempotency key was already used with different package import parameters.");
                }

                result = previousOperation.Receipt with { Replayed = true };
                return document;
            }

            var mutation = CreateMutation(document, imported, command);
            var operation = new AgentPackageImportOperationRecord(
                command.IdempotencyKey,
                requestFingerprint,
                mutation.Receipt,
                DateTimeOffset.UtcNow);
            result = mutation.Receipt;

            return mutation.Document with
            {
                AgentPackageImportOperations = mutation.Document.AgentPackageImportOperations
                    .Append(operation)
                    .OrderByDescending(item => item.CompletedAtUtc)
                    .Take(MaximumRetainedOperations)
                    .ToList()
            };
        }, cancellationToken);

        return result ?? throw new InvalidOperationException("Agent package import completed without a receipt.");
    }

    private ImportMutation CreateMutation(
        SandboxWorkspaceDocument document,
        AgentImportResult imported,
        AgentPackageImportCommand command)
    {
        var warnings = imported.Warnings.ToList();
        var unresolved = new List<string>();
        var importedAgent = NormalizeAgent(imported.Agent);
        var existingAgent = document.Agents.FirstOrDefault(item => item.Id == importedAgent.Id);
        var externalIdentity = new AgentExternalIdentity(
            command.ExternalNamespace,
            command.ExternalKey);
        var existingBinding = document.AgentExternalBindings.FirstOrDefault(item =>
            string.Equals(item.Namespace, externalIdentity.Namespace, StringComparison.Ordinal) &&
            string.Equals(item.Key, externalIdentity.Key, StringComparison.Ordinal));
        if (existingBinding is not null &&
            (command.Mode != AgentPackageImportMode.ReplaceExactVersion ||
             existingBinding.AgentId != importedAgent.Id))
        {
            throw new AgentPackageImportException(
                AgentPackageImportFailureKind.Conflict,
                "agent-package.external-key-conflict",
                "The package external identity is already bound to another import.");
        }

        ValidateModePreconditions(command, existingAgent);

        var targetAgentId = command.Mode == AgentPackageImportMode.Clone
            ? Guid.NewGuid()
            : importedAgent.Id;
        var resolvedAgent = ResolveReferences(
            importedAgent,
            imported,
            document,
            targetAgentId,
            unresolved,
            warnings);

        if (command.Mode == AgentPackageImportMode.Clone)
        {
            var now = DateTimeOffset.UtcNow;
            resolvedAgent = resolvedAgent with
            {
                Id = targetAgentId,
                IsTemplate = false,
                TemplateKey = WorkspaceCatalogIdentityNormalizer.NormalizeTemplateKey(
                    $"{resolvedAgent.TemplateKey}-{targetAgentId:N}",
                    resolvedAgent.Name),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            warnings.Add("Clone mode imports the agent definition only; sessions, run evidence, metrics, and memory are not copied.");
        }

        EnsureTemplateIdentityAvailable(document, resolvedAgent, command.Mode);

        var baseDocument = command.Mode == AgentPackageImportMode.ReplaceExactVersion
            ? PruneAgentWorkspace(document, resolvedAgent.Id)
            : document;
        var mutatedDocument = baseDocument with
        {
            Agents = baseDocument.Agents
                .Append(resolvedAgent)
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };

        if (command.Mode != AgentPackageImportMode.Clone)
        {
            mutatedDocument = AppendImportedHistory(mutatedDocument, imported);
        }

        var configurationSha256 =
            AgentConfigurationVersion.Create(resolvedAgent);
        var externalBinding = new AgentExternalBindingRecord(
            externalIdentity.Namespace,
            externalIdentity.Key,
            resolvedAgent.Id,
            configurationSha256,
            resolvedAgent.Status == AgentLifecycleStatus.Archived,
            resolvedAgent.UpdatedAtUtc);
        mutatedDocument = mutatedDocument with
        {
            AgentExternalBindings = mutatedDocument.AgentExternalBindings
                .Where(item =>
                    !string.Equals(item.Namespace, externalIdentity.Namespace, StringComparison.Ordinal) ||
                    !string.Equals(item.Key, externalIdentity.Key, StringComparison.Ordinal))
                .Append(externalBinding)
                .OrderBy(item => item.Namespace, StringComparer.Ordinal)
                .ThenBy(item => item.Key, StringComparer.Ordinal)
                .ToList()
        };
        var receipt = new AgentPackageImportReceipt(
            resolvedAgent.Id,
            command.Mode,
            command.ExternalKey.Trim(),
            imported.PackageSha256,
            imported.PackageSchemaVersion,
            resolvedAgent.UpdatedAtUtc.ToString("O"),
            configurationSha256,
            unresolved.Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToList(),
            warnings.Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToList(),
            Replayed: false)
        {
            ExternalNamespace = externalIdentity.Namespace
        };
        return new ImportMutation(mutatedDocument, receipt);
    }

    private AgentDefinition ResolveReferences(
        AgentDefinition agent,
        AgentImportResult imported,
        SandboxWorkspaceDocument document,
        Guid targetAgentId,
        ICollection<string> unresolved,
        ICollection<string> warnings)
    {
        Guid? providerProfileId = null;
        if (agent.ProviderProfileId.HasValue)
        {
            providerProfileId = ResolveProviderId(agent.ProviderProfileId.Value, imported.Providers, document.Providers);
            if (!providerProfileId.HasValue)
            {
                unresolved.Add($"provider:{agent.ProviderProfileId.Value:D}");
            }
        }

        var assignments = new List<AgentCapabilityAssignment>();
        foreach (var assignment in agent.Capabilities)
        {
            var resolvedCapability = ResolveCapability(assignment, imported.Capabilities, document.Capabilities);
            if (resolvedCapability is null)
            {
                unresolved.Add($"capability:{assignment.Kind}:{assignment.CapabilityKey}");
                continue;
            }

            assignments.Add(assignment with
            {
                CapabilityId = resolvedCapability.Id,
                CapabilityKey = resolvedCapability.Key,
                Kind = resolvedCapability.Kind
            });
        }

        var permissions = agent.Permissions ?? AgentPermissionsPolicy.Default;
        if (permissions.NormalizedAllowedSecrets.Count > 0)
        {
            warnings.Add("Secret references were removed; secrets must be bound explicitly in the target environment.");
        }

        return agent with
        {
            Id = targetAgentId,
            ProviderProfileId = providerProfileId,
            Capabilities = assignments,
            Permissions = permissions with { AllowedSecrets = [] }
        };
    }

    private Guid? ResolveProviderId(
        Guid importedProviderId,
        IReadOnlyList<ProviderProfile> importedProviders,
        IReadOnlyList<ProviderProfile> existingProviders)
    {
        var exact = existingProviders.FirstOrDefault(item => item.Id == importedProviderId);
        if (exact is not null)
        {
            return exact.Id;
        }

        var importedProvider = importedProviders.FirstOrDefault(item => item.Id == importedProviderId);
        if (importedProvider is null)
        {
            return null;
        }

        var identity = providerProfileService.GetIdentityKey(
            providerProfileService.NormalizeImportedProfile(importedProvider));
        var matches = existingProviders
            .Where(item => string.Equals(
                providerProfileService.GetIdentityKey(item),
                identity,
                StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Id)
            .Distinct()
            .Take(2)
            .ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    private static CapabilityCatalogItem? ResolveCapability(
        AgentCapabilityAssignment assignment,
        IReadOnlyList<CapabilityCatalogItem> importedCapabilities,
        IReadOnlyList<CapabilityCatalogItem> existingCapabilities)
    {
        var exact = existingCapabilities.FirstOrDefault(item => item.Id == assignment.CapabilityId);
        if (exact is not null)
        {
            return exact;
        }

        var importedCapability = importedCapabilities.FirstOrDefault(item => item.Id == assignment.CapabilityId);
        var kind = importedCapability?.Kind ?? assignment.Kind;
        var key = WorkspaceCatalogIdentityNormalizer.NormalizeCapabilityKey(
            importedCapability?.Key ?? assignment.CapabilityKey);
        var matches = existingCapabilities
            .Where(item => item.Kind == kind &&
                           string.Equals(
                               WorkspaceCatalogIdentityNormalizer.NormalizeCapabilityKey(item.Key),
                               key,
                               StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToList();
        return matches.Count == 1 ? matches[0] : null;
    }

    private static AgentDefinition NormalizeAgent(AgentDefinition agent)
    {
        if (agent.Id == Guid.Empty || string.IsNullOrWhiteSpace(agent.Name))
        {
            throw new AgentPackageImportException(
                AgentPackageImportFailureKind.InvalidRequest,
                "agent-package.agent-invalid",
                "The package agent must have a non-empty ID and name.");
        }

        return agent with
        {
            Name = agent.Name.Trim(),
            RoleTitle = agent.RoleTitle.Trim(),
            Summary = agent.Summary.Trim(),
            Instructions = agent.Instructions.Trim(),
            Model = agent.Model.Trim(),
            ConfigurationJson = agent.ConfigurationJson.Trim(),
            TemplateKey = WorkspaceCatalogIdentityNormalizer.NormalizeTemplateKey(agent.TemplateKey, agent.Name),
            Capabilities = agent.Capabilities ?? [],
            Tags = (agent.Tags ?? [])
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }

    private static void ValidateCommand(AgentPackageImportCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Trim().Length > 200)
        {
            throw new AgentPackageImportException(
                AgentPackageImportFailureKind.InvalidRequest,
                "agent-package.idempotency-key-invalid",
                "Idempotency-Key is required and must not exceed 200 characters.");
        }

        if (string.IsNullOrWhiteSpace(command.ExternalKey) || command.ExternalKey.Trim().Length > 200)
        {
            throw new AgentPackageImportException(
                AgentPackageImportFailureKind.InvalidRequest,
                "agent-package.external-key-invalid",
                "ExternalKey is required and must not exceed 200 characters.");
        }

        if (command.Mode == AgentPackageImportMode.ReplaceExactVersion && !command.ExpectedAgentVersion.HasValue)
        {
            throw new AgentPackageImportException(
                AgentPackageImportFailureKind.InvalidRequest,
                "agent-package.expected-version-required",
                "ReplaceExactVersion requires expectedAgentVersion.");
        }
    }

    private static AgentExternalIdentity NormalizeExternalIdentity(
        string externalNamespace,
        string externalKey)
    {
        try
        {
            return AgentExternalIdentityNormalizer.Normalize(externalNamespace, externalKey);
        }
        catch (ArgumentException exception)
        {
            throw new AgentPackageImportException(
                AgentPackageImportFailureKind.InvalidRequest,
                "agent-package.external-identity-invalid",
                exception.Message);
        }
    }

    private static void ValidateModePreconditions(
        AgentPackageImportCommand command,
        AgentDefinition? existingAgent)
    {
        if (command.Mode == AgentPackageImportMode.Create && existingAgent is not null)
        {
            throw new AgentPackageImportException(
                AgentPackageImportFailureKind.Conflict,
                "agent-package.agent-already-exists",
                "Create mode cannot replace an existing agent.");
        }

        if (command.Mode == AgentPackageImportMode.ReplaceExactVersion)
        {
            if (existingAgent is null)
            {
                throw new AgentPackageImportException(
                    AgentPackageImportFailureKind.Conflict,
                    "agent-package.agent-not-found",
                    "ReplaceExactVersion requires an existing agent with the package agent ID.");
            }

            if (existingAgent.UpdatedAtUtc != command.ExpectedAgentVersion)
            {
                throw new AgentPackageImportException(
                    AgentPackageImportFailureKind.PreconditionFailed,
                    "agent-package.version-conflict",
                    "The existing agent version does not match expectedAgentVersion.");
            }
        }
    }

    private static void EnsureTemplateIdentityAvailable(
        SandboxWorkspaceDocument document,
        AgentDefinition importedAgent,
        AgentPackageImportMode mode)
    {
        var identity = WorkspaceCatalogIdentityNormalizer.GetAgentTemplateIdentity(importedAgent);
        var collision = document.Agents.Any(item =>
            item.Id != importedAgent.Id &&
            string.Equals(
                WorkspaceCatalogIdentityNormalizer.GetAgentTemplateIdentity(item),
                identity,
                StringComparison.OrdinalIgnoreCase));
        if (collision && mode != AgentPackageImportMode.Clone)
        {
            throw new AgentPackageImportException(
                AgentPackageImportFailureKind.Conflict,
                "agent-package.template-key-conflict",
                $"Agent template key '{importedAgent.TemplateKey}' is already in use.");
        }
    }

    private static SandboxWorkspaceDocument AppendImportedHistory(
        SandboxWorkspaceDocument document,
        AgentImportResult imported)
    {
        return document with
        {
            Memory = document.Memory.Concat(imported.Memory).OrderBy(item => item.CreatedAtUtc).ToList(),
            ChatSessions = document.ChatSessions.Concat(imported.Sessions).OrderByDescending(item => item.UpdatedAtUtc).ToList(),
            ExecutionRuns = document.ExecutionRuns.Concat(imported.Runs).OrderByDescending(item => item.UpdatedAtUtc).ToList(),
            ExecutionLog = document.ExecutionLog.Concat(imported.ExecutionLog).OrderByDescending(item => item.CreatedAtUtc).ToList(),
            Metrics = document.Metrics.Concat(imported.Metrics).OrderByDescending(item => item.CreatedAtUtc).ToList(),
            ExecutionApprovals = document.ExecutionApprovals.Concat(imported.Approvals).OrderByDescending(item => item.DecidedAtUtc ?? item.RequestedAtUtc).ToList(),
            ExecutionArtifacts = document.ExecutionArtifacts.Concat(imported.Artifacts).OrderByDescending(item => item.CreatedAtUtc).ToList(),
            ExecutionWorkflowCheckpoints = document.ExecutionWorkflowCheckpoints.Concat(imported.Checkpoints).OrderByDescending(item => item.CapturedAtUtc).ToList(),
            ToolExecutionReceipts = document.ToolExecutionReceipts.Concat(imported.ToolReceipts).OrderByDescending(item => item.CompletedAtUtc).ToList()
        };
    }

    private static SandboxWorkspaceDocument PruneAgentWorkspace(
        SandboxWorkspaceDocument document,
        Guid agentId)
    {
        var sessionIds = document.ChatSessions
            .Where(item => item.AgentId == agentId)
            .Select(item => item.Id)
            .ToHashSet();
        var runIds = document.ExecutionRuns
            .Where(item => item.AgentId == agentId ||
                           item.ChatSessionId.HasValue && sessionIds.Contains(item.ChatSessionId.Value))
            .Select(item => item.Id)
            .ToHashSet();

        return document with
        {
            Agents = document.Agents.Where(item => item.Id != agentId).ToList(),
            AgentExternalBindings = document.AgentExternalBindings
                .Where(item => item.AgentId != agentId)
                .ToList(),
            Memory = document.Memory.Where(item => item.AgentId != agentId).ToList(),
            ChatSessions = document.ChatSessions.Where(item => item.AgentId != agentId).ToList(),
            ExecutionRuns = document.ExecutionRuns.Where(item => !runIds.Contains(item.Id)).ToList(),
            ExecutionLog = document.ExecutionLog.Where(item =>
                item.AgentId != agentId &&
                (!item.ChatSessionId.HasValue || !sessionIds.Contains(item.ChatSessionId.Value)) &&
                (item.ExecutionRunId == Guid.Empty || !runIds.Contains(item.ExecutionRunId))).ToList(),
            Metrics = document.Metrics.Where(item =>
                item.AgentId != agentId &&
                (!item.ChatSessionId.HasValue || !sessionIds.Contains(item.ChatSessionId.Value)) &&
                (item.ExecutionRunId == Guid.Empty || !runIds.Contains(item.ExecutionRunId))).ToList(),
            ProviderUsageObservations = document.ProviderUsageObservations.Where(item =>
                item.AgentId != agentId &&
                (!item.ChatSessionId.HasValue || !sessionIds.Contains(item.ChatSessionId.Value)) &&
                (!item.ExecutionRunId.HasValue || !runIds.Contains(item.ExecutionRunId.Value))).ToList(),
            ExecutionApprovals = document.ExecutionApprovals.Where(item => !runIds.Contains(item.ExecutionRunId)).ToList(),
            ExecutionArtifacts = document.ExecutionArtifacts.Where(item => !runIds.Contains(item.ExecutionRunId)).ToList(),
            ExecutionWorkflowCheckpoints = document.ExecutionWorkflowCheckpoints.Where(item => !runIds.Contains(item.ExecutionRunId)).ToList(),
            ToolExecutionReceipts = document.ToolExecutionReceipts.Where(item => !runIds.Contains(item.ExecutionRunId)).ToList()
        };
    }

    private static string CreateRequestFingerprint(AgentPackageImportCommand command, string packageSha256)
    {
        var material = string.Join(
            "\n",
            packageSha256,
            command.Mode,
            command.ExternalKey.Trim(),
            command.ExpectedAgentVersion?.ToString("O") ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    private sealed record ImportMutation(
        SandboxWorkspaceDocument Document,
        AgentPackageImportReceipt Receipt);
}
