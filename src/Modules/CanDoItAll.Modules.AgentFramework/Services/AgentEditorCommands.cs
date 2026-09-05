using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.AgentFramework;

public abstract record AgentEditorSaveOutcome {
    private AgentEditorSaveOutcome() { }
    public sealed record Committed(Guid AgentId) : AgentEditorSaveOutcome;
    public sealed record Rejected(string Message, bool IsConflict = false) : AgentEditorSaveOutcome;
    public sealed record Unconfirmed(string Message) : AgentEditorSaveOutcome;
}

public sealed record AgentEditorCatalogRefresh(AgentEditorModel Draft,
    IReadOnlyList<AgentDefinition> Agents, IReadOnlyList<CapabilityCatalogItem> Capabilities, Guid? LinkedPartyId);

public interface IAgentEditorCommands {
    Task<AgentEditorSaveOutcome> SaveAsync(AgentEditorModel request, CancellationToken cancellationToken = default);
    Task<AgentEditorCatalogRefresh> ReconcileAsync(Guid agentId, IReadOnlyList<ProviderProfile> providers,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CapabilityCatalogItem>> ReadCapabilitiesAsync(CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid agentId, CancellationToken cancellationToken = default);
    Task VerifyCapabilityAsync(Guid agentId, Guid capabilityId, CancellationToken cancellationToken = default);
}

public sealed class AgentEditorCommands(IAgentFrameworkWorkspaceService workspace,
    IExternalTargetPathRegistryFactory externalTargets) : IAgentEditorCommands {
    public async Task<AgentEditorSaveOutcome> SaveAsync(AgentEditorModel request, CancellationToken cancellationToken = default) {
        try {
            NormalizeWorkspaceAccess(request);
        } catch (Exception exception) {
            return new AgentEditorSaveOutcome.Rejected(exception.Message);
        }
        try {
            return new AgentEditorSaveOutcome.Committed(await workspace.SaveAgentAsync(request, cancellationToken));
        } catch (AgentCatalogConcurrencyException exception) {
            return new AgentEditorSaveOutcome.Rejected(exception.Message, IsConflict: true);
        } catch (Exception exception) {
            return new AgentEditorSaveOutcome.Unconfirmed(exception.Message);
        }
    }

    public async Task<AgentEditorCatalogRefresh> ReconcileAsync(Guid agentId, IReadOnlyList<ProviderProfile> providers,
        CancellationToken cancellationToken = default) {
        var agents = await workspace.ListAgentsAsync(includeTemplates: false, cancellationToken);
        var capabilities = await workspace.ListCapabilitiesAsync(cancellationToken);
        var definition = agents.FirstOrDefault(agent => agent.Id == agentId)
            ?? throw new InvalidOperationException("The saved agent is not available in the refreshed catalog.");
        return new(AgentEditorModel.FromDefinition(definition,
                providers.FirstOrDefault(provider => provider.Id == definition.ProviderProfileId)?.Kind),
            agents, capabilities, AgentFrameworkCrmHrMetadata.Read(definition.ConfigurationJson)?.PartyId);
    }

    public Task<IReadOnlyList<CapabilityCatalogItem>> ReadCapabilitiesAsync(CancellationToken cancellationToken = default)
        => workspace.ListCapabilitiesAsync(cancellationToken);

    public Task DeleteAsync(Guid agentId, CancellationToken cancellationToken = default)
        => workspace.DeleteAgentAsync(agentId, cancellationToken);

    public Task VerifyCapabilityAsync(Guid agentId, Guid capabilityId, CancellationToken cancellationToken = default)
        => workspace.VerifyCapabilityAsync(agentId, capabilityId, cancellationToken);

    private void NormalizeWorkspaceAccess(AgentEditorModel request) {
        var normalized = AgentWorkspaceToolAccessMetadata.Normalize(request.WorkspaceToolAccess);
        var registry = externalTargets.Create(normalized.ExternalTargetRootBindings);
        var aliases = normalized.AllowedExternalTargetAliases
            .Select(alias => AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(alias, registry)
                ?? throw new InvalidOperationException($"External workspace root '{alias}' is not a supported path or alias."))
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .OrderBy(alias => alias, StringComparer.Ordinal)
            .ToList();
        normalized.AllowedExternalTargetAliases = aliases;
        normalized.ExternalTargetRootBindings = normalized.ExternalTargetRootBindings
            .Concat(registry.ExportBindings(aliases)).ToList();
        request.WorkspaceToolAccess = AgentWorkspaceToolAccessMetadata.Normalize(normalized);
    }
}
