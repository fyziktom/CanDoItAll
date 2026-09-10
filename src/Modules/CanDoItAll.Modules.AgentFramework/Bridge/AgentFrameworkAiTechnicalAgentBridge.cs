using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class AgentFrameworkAiTechnicalAgentBridge(
    IAiTechnicalAgentProjectionStore projections,
    ICanDoItAllAgentWorkspaceFactory workspaceFactory) : IAiTechnicalAgentBridge {
    private const string CrmHrRuntimeAgentTemplateKeyPrefix = "crmhr-ai-resource";

    public async Task SynchronizeDirectoryProjectionAsync(CancellationToken cancellationToken = default) {
        var workspace = OrganizationWorkspace();
        await SynchronizeAsync(workspace, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, AiTechnicalAgentDirectorySummary>> GetDirectorySummariesAsync(
        IReadOnlyList<Guid> partyIds, CancellationToken cancellationToken = default)
        => (await projections.ReadAsync(partyIds, cancellationToken)).ToDictionary(item => item.Key, item => item.Value.Directory);

    public async Task<IReadOnlyDictionary<Guid, AiAgentStaffingFactModel>> GetStaffingFactsAsync(
        IReadOnlyList<Guid> partyIds, CancellationToken cancellationToken = default)
        => (await projections.ReadAsync(partyIds, cancellationToken)).ToDictionary(item => item.Key, item => item.Value.Staffing);

    public async Task<AiTechnicalAgentWorkspaceModel> GetWorkspaceAsync(Guid partyId, CancellationToken cancellationToken = default) {
        var snapshot = await OrganizationWorkspace().LoadCatalogSnapshotAsync(cancellationToken);
        var catalog = snapshot.Snapshot.Catalog;
        var resource = (await projections.ReadAsync([partyId], cancellationToken)).GetValueOrDefault(partyId);
        var binding = resource?.Directory;
        RequireBindingSource(binding, snapshot);
        var agent = ResolveBoundAgent(binding, catalog.Agents, partyId);
        var metadata = agent is null ? null : AgentFrameworkCrmHrMetadata.Read(agent.ConfigurationJson);
        var provider = agent is null ? null : ResolveEffectiveProvider(agent, catalog.Providers.ToDictionary(item => item.Id));
        var capabilities = ResolveCapabilities(metadata, agent, catalog.Capabilities.ToDictionary(item => item.Id, item => item.Name));
        return new(agent?.Id ?? binding?.TechnicalAgentId,
            binding?.BindingStatus ?? (agent is null ? AiResourceBindingStatus.Unbound : AiResourceBindingStatus.Bound),
            binding?.BindingSummary ?? "No technical binding.", BuildAgentsRoute(agent?.Id ?? binding?.TechnicalAgentId),
            agent?.ProviderProfileId, provider?.Name ?? string.Empty, metadata?.ExecutionMode ?? AiExecutionMode.Remote,
            agent is null ? string.Empty : ResolveEffectiveModel(agent, provider), capabilities,
            catalog.Providers.Select(item => new AiProviderOptionModel(item.Id, item.Name, item.Kind.ToString(), item.DefaultModel, item.IsEnabled)).ToArray());
    }

    public async Task<Result<AiTechnicalAgentSaveResult>> SaveAsync(AiAgentProfileEditorModel model,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(model);
        var resource = (await projections.ReadAsync([model.PartyId], cancellationToken)).GetValueOrDefault(model.PartyId);
        if (resource?.PartyType is null) {
            return Result<AiTechnicalAgentSaveResult>.Failure(Error.Validation("AI agent party was not found.", "crmhr.ai-agent.party-not-found"));
        }

        if (resource.PartyType != PartyType.AiAgent) {
            return Result<AiTechnicalAgentSaveResult>.Failure(Error.Validation("Only AI agent parties can have technical profiles.", "crmhr.ai-agent.party-type-invalid"));
        }

        var workspace = OrganizationWorkspace();
        var snapshot = await workspace.LoadCatalogSnapshotAsync(cancellationToken);
        var catalog = snapshot.Snapshot.Catalog;
        RequireBindingSource(resource.Directory, snapshot);
        if (model.ProviderProfileId is Guid providerId && !catalog.Providers.Any(provider => provider.Id == providerId)) {
            return Result<AiTechnicalAgentSaveResult>.Failure(Error.Validation("Provider profile must reference an existing provider.", "crmhr.ai-agent.provider-invalid"));
        }

        var agent = ResolveBoundAgent(resource.Directory, catalog.Agents, model.PartyId);
        if (agent is null && resource.Directory.TechnicalAgentId.HasValue && resource.Directory.BindingStatus != AiResourceBindingStatus.PendingBackfill) {
            return Result<AiTechnicalAgentSaveResult>.Failure(Error.Validation(
                "The bound technical Agent is unavailable. Reconcile the binding before creating another profile.", "crmhr.ai-agent.technical-agent-missing"));
        }

        var editor = await workspace.GetAgentEditorAsync(agent?.Id, cancellationToken);
        var capabilities = AgentFrameworkCrmHrMetadata.NormalizeCapabilities(model.Capabilities);
        if (agent is null) {
            editor.Name = resource.DisplayName;
            editor.RoleTitle = "AI resource";
            editor.Summary = string.IsNullOrWhiteSpace(resource.Summary) ? $"{resource.DisplayName} technical runtime profile." : resource.Summary.Trim();
            editor.Instructions = AgentFrameworkCrmHrMetadata.BuildInstructions(editor.Instructions, resource.DisplayName, model.Notes, capabilities);
            editor.Status = AgentLifecycleStatus.Active;
            editor.IsTemplate = false;
            editor.TemplateKey = $"{CrmHrRuntimeAgentTemplateKeyPrefix}-{model.PartyId:N}";
        }

        editor.ProviderProfileId = model.ProviderProfileId;
        editor.Model = model.DefaultModel?.Trim() ?? string.Empty;
        editor.ConfigurationJson = AgentFrameworkCrmHrMetadata.Write(editor.ConfigurationJson, model.PartyId, model.ExecutionMode, capabilities);
        editor.Tags = AgentFrameworkCrmHrMetadata.EnsurePartyTag(editor.Tags, model.PartyId)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        Guid technicalAgentId;
        try {
            technicalAgentId = await workspace.SaveAgentAsync(editor, cancellationToken);
        } catch (AgentEditorValidationException exception) {
            return Result<AiTechnicalAgentSaveResult>.Failure(Error.Validation(exception.Message, "crmhr.ai-agent.agentframework-validation-failed"));
        }

        try {
            await SynchronizeAsync(workspace, cancellationToken);
            var current = (await projections.ReadAsync([model.PartyId], cancellationToken)).GetValueOrDefault(model.PartyId)?.Directory;
            if (current?.TechnicalAgentId != technicalAgentId) {
                throw new InvalidOperationException("The saved technical Agent is not reflected in the expected CRM Party binding.");
            }

            return Result<AiTechnicalAgentSaveResult>.Success(new(technicalAgentId, current.BindingStatus, current.BindingSummary, current.AgentsRoute));
        } catch (Exception exception) {
            throw new AgentDirectoryProjectionSynchronizationException(technicalAgentId, exception, partyId: model.PartyId);
        }
    }

    private IAgentFrameworkWorkspaceService OrganizationWorkspace()
        => workspaceFactory.GetWorkspaceService(workspaceFactory.GetOrganizationScope());

    private async Task SynchronizeAsync(IAgentFrameworkWorkspaceService workspace, CancellationToken cancellationToken) {
        var snapshot = await workspace.LoadCatalogSnapshotAsync(cancellationToken);
        if (snapshot.Workspace.WorkspaceScope != workspaceFactory.GetOrganizationScope() ||
            snapshot.Snapshot.Revision != snapshot.Snapshot.Catalog.CatalogDataRevision) {
            throw new InvalidOperationException("The workspace returned an inconsistent organization catalog snapshot.");
        }

        var catalog = snapshot.Snapshot.Catalog;
        var providers = catalog.Providers.ToDictionary(item => item.Id);
        var capabilityNames = catalog.Capabilities.ToDictionary(item => item.Id, item => item.Name);
        var agents = catalog.Agents.Where(agent => !agent.IsTemplate).Select(agent => {
            var metadata = AgentFrameworkCrmHrMetadata.Read(agent.ConfigurationJson);
            var provider = ResolveEffectiveProvider(agent, providers);
            var capabilities = ResolveCapabilities(metadata, agent, capabilityNames);
            return new AiTechnicalProjectionEntry(agent.Id,
                AgentFrameworkCrmHrMetadata.ResolvePartyId(agent.ConfigurationJson, agent.Tags), agent.Name.Trim(),
                string.IsNullOrWhiteSpace(agent.Summary) ? $"{agent.RoleTitle} technical runtime profile.".Trim() : agent.Summary.Trim(),
                agent.Status, metadata?.ExecutionMode ?? AiExecutionMode.Remote, provider?.Name ?? string.Empty,
                ResolveEffectiveModel(agent, provider), agent.RoleTitle, agent.Instructions, agent.TemplateKey,
                agent.Tags.ToImmutableArray(), capabilities.Select(capability => new AiTechnicalProjectionCapability(
                    capability.Name, capability.Scope, capability.ToolAccess, capability.Limitations, capability.Notes)).ToImmutableArray());
        }).ToImmutableArray();
        await projections.ApplyAsync(new(new(snapshot.Workspace.DatabaseProfileId, snapshot.Workspace.WorkspaceScope),
            snapshot.Snapshot.Revision, agents), cancellationToken);
    }

    private static AgentDefinition? ResolveBoundAgent(AiTechnicalAgentDirectorySummary? binding, IReadOnlyList<AgentDefinition> agents, Guid partyId) {
        if (binding?.TechnicalAgentId is Guid technicalAgentId) {
            var bound = agents.SingleOrDefault(agent => agent.Id == technicalAgentId && !agent.IsTemplate);
            if (bound is not null || binding.BindingStatus != AiResourceBindingStatus.PendingBackfill) {
                return bound;
            }
        }

        var matching = agents.Where(agent => !agent.IsTemplate && AgentFrameworkCrmHrMetadata.ResolvePartyId(agent.ConfigurationJson, agent.Tags) == partyId).ToArray();
        return matching.Length switch {
            0 => null,
            1 => matching[0],
            _ => throw new InvalidOperationException("Multiple technical Agents claim the same unbound CRM Party. Reconcile the catalog projection before editing.")
        };
    }

    private static void RequireBindingSource(AiTechnicalAgentDirectorySummary? binding, AgentWorkspaceCatalogSnapshot snapshot) {
        if (binding?.Projection is { } projection &&
            (projection.Source.DatabaseProfileId != snapshot.Workspace.DatabaseProfileId ||
                projection.Source.Scope != snapshot.Workspace.WorkspaceScope)) {
            throw new InvalidOperationException("The CRM binding belongs to a different catalog source. Reconcile source ownership before editing.");
        }
    }

    private static IReadOnlyList<AiCapabilityEditorModel> ResolveCapabilities(AgentFrameworkCrmHrMetadataModel? metadata,
        AgentDefinition? agent, IReadOnlyDictionary<Guid, string> names) {
        if (metadata?.Capabilities.Count > 0) {
            return metadata.Capabilities;
        }

        if (agent is null || agent.Capabilities.Count == 0) {
            return metadata?.Capabilities ?? [];
        }

        return agent.Capabilities.Select(capability => new AiCapabilityEditorModel {
            Name = names.GetValueOrDefault(capability.CapabilityId) ?? capability.CapabilityKey,
            ToolAccess = capability.Kind.ToString(),
            Limitations = capability.ProofStatus == CapabilityProofStatus.Verified ? string.Empty : $"Proof status: {capability.ProofStatus}",
            Notes = capability.ProofNotes
        }).ToArray();
    }

    private static ProviderProfile? ResolveEffectiveProvider(AgentDefinition agent, IReadOnlyDictionary<Guid, ProviderProfile> providers)
        => agent.ProviderProfileId is Guid providerId && providers.TryGetValue(providerId, out var provider)
            ? ManagedSeedProviderFallbacks.Apply(agent, provider)
            : null;

    private static string ResolveEffectiveModel(AgentDefinition agent, ProviderProfile? provider)
        => provider is null ? string.Empty : ManagedSeedProviderFallbacks.ResolveModel(agent, provider);

    private static string BuildAgentsRoute(Guid? agentId)
        => agentId.HasValue ? $"/agents?tab=agents&agentId={agentId.Value:D}" : "/agents?tab=agents";
}
