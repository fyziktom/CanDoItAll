using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.CrmHr;
using IProviderRuntimeAdministrationService = CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderRuntimeAdministrationService;

namespace CanDoItAll.Modules.AgentFramework;

public sealed record AgentEditorProject(Guid Id, string Name);
public sealed record AgentEditorSecret(Guid Id, string Name, string KindLabel);
public sealed record AgentEditorReferenceResult<T>(IReadOnlyList<T> Items, string? Error = null);

public sealed record AgentEditorLoadResult(
    AgentEditorModel Draft,
    IReadOnlyList<AgentDefinition> Agents,
    IReadOnlyList<CapabilityCatalogItem> Capabilities,
    AgentEditorReferenceResult<ProviderProfile> Providers,
    AgentEditorReferenceResult<AgentEditorSecret> Secrets,
    Guid? LinkedPartyId);

public interface IAgentEditorReads {
    Task<IReadOnlyList<CapabilityCatalogItem>> ReadCapabilitiesAsync(CancellationToken cancellationToken = default);
    Task<AgentEditorLoadResult> LoadAsync(AgentEditorTarget target,
        IReadOnlyList<ProviderProfile>? initialProviders = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProviderProfile>> ReadProvidersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentEditorProject>> ReadProjectsAsync(CancellationToken cancellationToken = default);
}

public sealed class AgentEditorReads(
    IAgentFrameworkWorkspaceService workspace,
    IProviderRuntimeAdministrationService providers,
    IAgentEditorAccessQuery access) : IAgentEditorReads {
    public async Task<AgentEditorLoadResult> LoadAsync(AgentEditorTarget target,
        IReadOnlyList<ProviderProfile>? initialProviders = null, CancellationToken cancellationToken = default) {
        var agentsTask = workspace.ListAgentsAsync(includeTemplates: false, cancellationToken);
        var capabilitiesTask = workspace.ListCapabilitiesAsync(cancellationToken);
        var providersTask = CaptureReferenceAsync(() => initialProviders is null
            ? ReadProvidersAsync(cancellationToken)
            : Task.FromResult(initialProviders), cancellationToken);
        var secretsTask = CaptureReferenceAsync(() => access.ReadSecretsAsync(cancellationToken), cancellationToken);
        await Task.WhenAll(agentsTask, capabilitiesTask, providersTask, secretsTask);
        var agents = await agentsTask;
        var providerResult = await providersTask;
        var definition = agents.FirstOrDefault(agent => agent.Id == target.AgentId);
        var draft = definition is not null
            ? AgentEditorModel.FromDefinition(definition,
                providerResult.Items.FirstOrDefault(provider => provider.Id == definition.ProviderProfileId)?.Kind)
            : target.AgentId is { } id
                ? await workspace.GetAgentEditorAsync(id, cancellationToken)
                : new AgentEditorModel();
        return new(draft, agents, await capabilitiesTask, providerResult, await secretsTask,
            definition is null ? null : AgentFrameworkCrmHrMetadata.Read(definition.ConfigurationJson)?.PartyId);
    }

    public Task<IReadOnlyList<CapabilityCatalogItem>> ReadCapabilitiesAsync(CancellationToken cancellationToken = default)
        => workspace.ListCapabilitiesAsync(cancellationToken);

    public Task<IReadOnlyList<ProviderProfile>> ReadProvidersAsync(CancellationToken cancellationToken = default)
        => providers.ListProvidersAsync(cancellationToken);

    public Task<IReadOnlyList<AgentEditorProject>> ReadProjectsAsync(CancellationToken cancellationToken = default)
        => access.ReadProjectsAsync(cancellationToken);

    private static async Task<AgentEditorReferenceResult<T>> CaptureReferenceAsync<T>(
        Func<Task<IReadOnlyList<T>>> read, CancellationToken cancellationToken) {
        try {
            return new(await read());
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) {
            return new([], exception.Message);
        }
    }
}
