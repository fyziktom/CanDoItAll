using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

public sealed class AgentCapabilitiesSession(IAgentCapabilitiesReads reads) : IDisposable {
    private CancellationTokenSource? readCancellation;
    private ImmutableArray<AgentDefinition> agents = [];
    private ImmutableArray<CapabilityCatalogItem> capabilities = [];
    private bool hasCatalog;
    private bool disposed;

    public long Generation { get; private set; }
    public Guid? TargetAgentId { get; private set; }
    public AgentDefinition? SelectedAgent { get; private set; }
    public AgentEditorModel? Draft { get; private set; }
    public AgentCapabilitiesSelection Selection => new(SelectedAgent?.Id);
    public AgentCapabilitiesLoadState LoadState { get; private set; } = AgentCapabilitiesLoadState.Loading;
    public string? LoadError { get; private set; }

    public AgentCapabilitiesSnapshot Snapshot {
        get {
            var curator = agents.FirstOrDefault(CapabilityCuratorAgentIdentity.Matches);
            return new(
                agents.Select(agent => new AgentCapabilitiesAgent(agent.Id, agent.Name, agent.RoleTitle, agent.Model,
                    agent.Id == SelectedAgent?.Id && Draft is not null ? Draft.SelectedCapabilityIds.Count : agent.Capabilities.Count)).ToImmutableArray(),
                capabilities,
                Draft?.SelectedCapabilityIds.ToImmutableArray() ?? [],
                new(curator?.Name ?? CapabilityCuratorAgentIdentity.DefaultDisplayName,
                    curator?.AvatarImageUrl ?? CapabilityCuratorAgentIdentity.DefaultAvatarImageUrl,
                    LoadState == AgentCapabilitiesLoadState.Ready && curator is { Status: AgentLifecycleStatus.Active, IsTemplate: false }
                        && curator.Permissions.CanUseTools)) { LoadError = LoadError };
        }
    }

    public bool IsCurrent(long generation) => !disposed && Generation == generation;

    public async Task<bool> LoadAsync(Guid? requestedAgentId) {
        var preferred = requestedAgentId ?? SelectedAgent?.Id;
        var (generation, token) = BeginRead(preferred);
        hasCatalog = false;
        try {
            var catalog = await reads.LoadCatalogAsync(token);
            if (!IsCurrent(generation)) {
                return false;
            }

            agents = catalog.Agents.Where(agent => !agent.IsTemplate).ToImmutableArray();
            capabilities = catalog.Capabilities.Select(capability => capability with {
                Tags = capability.Tags.ToImmutableArray()
            }).ToImmutableArray();
            hasCatalog = true;
            TargetAgentId = preferred ?? agents.FirstOrDefault()?.Id;
            return await ReadTargetAsync(generation, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested || !IsCurrent(generation)) {
            return false;
        }
        catch (Exception) when (!IsCurrent(generation)) {
            return false;
        }
        catch (Exception) {
            return Fail("Unable to load the capability workspace. Retry the current target.");
        }
        finally {
            FinishRead(generation);
        }
    }

    public async Task<bool> SelectAsync(Guid? requestedAgentId) {
        if (!hasCatalog) {
            return await LoadAsync(requestedAgentId);
        }

        var target = requestedAgentId ?? SelectedAgent?.Id ?? agents.FirstOrDefault()?.Id;
        var (generation, token) = BeginRead(target);
        try {
            return await ReadTargetAsync(generation, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested || !IsCurrent(generation)) {
            return false;
        }
        catch (Exception) when (!IsCurrent(generation)) {
            return false;
        }
        catch (Exception) {
            return Fail("Unable to load the selected agent's capabilities. Retry the current target.");
        }
        finally {
            FinishRead(generation);
        }
    }

    public Task<bool> RefreshAsync() => LoadAsync(TargetAgentId);

    private (long Generation, CancellationToken Token) BeginRead(Guid? target) {
        ObjectDisposedException.ThrowIf(disposed, this);
        Generation++;
        CancelRead();
        readCancellation = new();
        TargetAgentId = target;
        SelectedAgent = null;
        Draft = null;
        LoadState = AgentCapabilitiesLoadState.Loading;
        LoadError = null;
        return (Generation, readCancellation.Token);
    }

    private async Task<bool> ReadTargetAsync(long generation, CancellationToken token) {
        if (TargetAgentId is not { } target) {
            LoadState = AgentCapabilitiesLoadState.Ready;
            return true;
        }

        var agent = agents.FirstOrDefault(item => item.Id == target);
        if (agent is null) {
            return Fail("The requested agent is not available in the capability workspace.");
        }

        var editor = await reads.ReadEditorAsync(target, token);
        if (!IsCurrent(generation)) {
            return false;
        }

        if (editor.Id != target) {
            return Fail("The selected agent could not be loaded with its expected identity.");
        }

        Draft = editor;
        SelectedAgent = agent;
        LoadState = AgentCapabilitiesLoadState.Ready;
        return true;
    }

    private bool Fail(string message) {
        SelectedAgent = null;
        Draft = null;
        LoadError = message;
        LoadState = AgentCapabilitiesLoadState.Failed;
        return true;
    }

    private void FinishRead(long generation) {
        if (Generation != generation) {
            return;
        }

        readCancellation?.Dispose();
        readCancellation = null;
    }

    private void CancelRead() {
        var cancellation = readCancellation;
        readCancellation = null;
        if (cancellation is null) {
            return;
        }

        cancellation.Cancel();
        cancellation.Dispose();
    }

    public void Dispose() {
        if (disposed) {
            return;
        }

        disposed = true;
        CancelRead();
    }
}
