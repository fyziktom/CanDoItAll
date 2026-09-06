using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Pages.Components;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class AgentCapabilitiesSessionTests {
    [Fact]
    public async Task Initial_requested_agent_loads_exact_target() {
        var reads = new Reads();
        using var session = new AgentCapabilitiesSession(reads);
        await session.LoadAsync(reads.Beta.Id);
        Assert.Equal([reads.Beta.Id], reads.Requests);
        Assert.Same(reads.Beta, session.SelectedAgent);
        Assert.Equal(AgentCapabilitiesLoadState.Ready, session.LoadState);
    }

    [Fact]
    public async Task Missing_initial_requested_agent_fails_closed() {
        var reads = new Reads();
        using var session = new AgentCapabilitiesSession(reads);
        var missing = Guid.NewGuid();
        await session.LoadAsync(missing);
        AssertFailed(session, missing);
        Assert.Empty(reads.Requests);
    }

    [Fact]
    public async Task Valid_selection_then_missing_request_clears_authoritative_selection() {
        var reads = new Reads();
        using var session = new AgentCapabilitiesSession(reads);
        await session.LoadAsync(reads.Alpha.Id);
        var missing = Guid.NewGuid();
        await session.SelectAsync(missing);
        AssertFailed(session, missing);
        Assert.Equal([reads.Alpha.Id], reads.Requests);
    }

    [Fact]
    public async Task Late_A_read_cannot_replace_B() {
        var reads = new Reads();
        var pending = new TaskCompletionSource<AgentEditorModel>();
        reads.Read = (id, _) => id == reads.Alpha.Id ? pending.Task : Task.FromResult(AgentEditorModel.FromDefinition(reads.Beta));
        using var session = new AgentCapabilitiesSession(reads);
        var old = session.LoadAsync(reads.Alpha.Id);
        await session.SelectAsync(reads.Beta.Id);
        pending.SetResult(AgentEditorModel.FromDefinition(reads.Alpha));
        Assert.False(await old);
        Assert.Equal(reads.Beta.Id, session.Selection.AgentId);
        Assert.Equal(reads.Beta.Id, session.Draft!.Id);
    }

    [Fact]
    public async Task Late_A_failure_cannot_fail_B() {
        var reads = new Reads();
        var pending = new TaskCompletionSource<AgentEditorModel>();
        reads.Read = (id, _) => id == reads.Alpha.Id ? pending.Task : Task.FromResult(AgentEditorModel.FromDefinition(reads.Beta));
        using var session = new AgentCapabilitiesSession(reads);
        var old = session.LoadAsync(reads.Alpha.Id);
        await session.SelectAsync(reads.Beta.Id);
        pending.SetException(new InvalidOperationException("Stale fixture failure"));
        Assert.False(await old);
        Assert.Equal(reads.Beta.Id, session.Selection.AgentId);
        Assert.Equal(AgentCapabilitiesLoadState.Ready, session.LoadState);
        Assert.Null(session.LoadError);
    }

    [Fact]
    public async Task Disposal_cancels_owned_reads() {
        var reads = new Reads();
        var pending = new TaskCompletionSource<AgentEditorModel>();
        reads.Read = (_, _) => pending.Task;
        using var session = new AgentCapabilitiesSession(reads);
        var load = session.LoadAsync(reads.Alpha.Id);
        session.Dispose();
        session.Dispose();
        Assert.True(reads.LastToken.IsCancellationRequested);
        pending.SetResult(AgentEditorModel.FromDefinition(reads.Alpha));
        Assert.False(await load);
        Assert.Null(session.Draft);
    }

    [Fact]
    public async Task Refresh_preserves_current_valid_selection() {
        var reads = new Reads();
        using var session = new AgentCapabilitiesSession(reads);
        await session.LoadAsync(reads.Beta.Id);
        await session.RefreshAsync();
        Assert.Equal([reads.Beta.Id, reads.Beta.Id], reads.Requests);
        Assert.Equal(reads.Beta.Id, session.Selection.AgentId);
    }

    [Fact]
    public async Task Refresh_missing_selected_agent_fails_closed() {
        var reads = new Reads();
        using var session = new AgentCapabilitiesSession(reads);
        await session.LoadAsync(reads.Beta.Id);
        reads.Agents = [reads.Alpha];
        await session.RefreshAsync();
        AssertFailed(session, reads.Beta.Id);
    }

    [Fact]
    public async Task Selected_agent_read_failure_clears_prior_editor() {
        var reads = new Reads();
        using var session = new AgentCapabilitiesSession(reads);
        await session.LoadAsync(reads.Alpha.Id);
        reads.Read = (_, _) => Task.FromException<AgentEditorModel>(new InvalidOperationException("Private upstream fixture detail"));
        await session.SelectAsync(reads.Beta.Id);
        AssertFailed(session, reads.Beta.Id);
        Assert.DoesNotContain("Private upstream", session.LoadError);
        reads.Read = null;
        await session.RefreshAsync();
        Assert.Equal(reads.Beta.Id, session.Selection.AgentId);
    }

    [Fact]
    public async Task Wrong_editor_identity_fails_closed() {
        var reads = new Reads();
        reads.Read = (_, _) => Task.FromResult(AgentEditorModel.FromDefinition(reads.Alpha));
        using var session = new AgentCapabilitiesSession(reads);
        await session.LoadAsync(reads.Beta.Id);
        AssertFailed(session, reads.Beta.Id);
    }

    [Fact]
    public async Task Superseding_selection_cancels_the_prior_read() {
        var reads = new Reads();
        var pending = new TaskCompletionSource<AgentEditorModel>();
        reads.Read = (id, _) => id == reads.Alpha.Id ? pending.Task : Task.FromResult(AgentEditorModel.FromDefinition(reads.Beta));
        using var session = new AgentCapabilitiesSession(reads);
        var load = session.LoadAsync(reads.Alpha.Id);
        var oldToken = reads.LastToken;
        await session.SelectAsync(reads.Beta.Id);
        Assert.True(oldToken.IsCancellationRequested);
        pending.SetResult(AgentEditorModel.FromDefinition(reads.Alpha));
        Assert.False(await load);
    }

    [Fact]
    public async Task Presentation_snapshot_owns_mutable_collections() {
        var reads = new Reads();
        var tags = new List<string> { "original" };
        var capability = new CapabilityCatalogItem(Guid.NewGuid(), CapabilityKind.Skill, "fixture", "Fixture", "", "", "{}",
            CapabilityProofStatus.NotRun, "", null, false) { Tags = tags };
        reads.Capabilities = [capability];
        using var session = new AgentCapabilitiesSession(reads);
        await session.LoadAsync(reads.Alpha.Id);
        session.Draft!.SelectedCapabilityIds = [capability.Id];
        var snapshot = session.Snapshot;
        tags.Add("later");
        session.Draft.SelectedCapabilityIds.Clear();
        Assert.Equal(["original"], snapshot.Capabilities[0].Tags);
        Assert.Equal<Guid>([capability.Id], snapshot.SelectedCapabilityIds);
        Assert.Empty(session.Snapshot.SelectedCapabilityIds);
    }

    private static void AssertFailed(AgentCapabilitiesSession session, Guid target) {
        Assert.Equal(target, session.TargetAgentId);
        Assert.Null(session.Selection.AgentId);
        Assert.Null(session.SelectedAgent);
        Assert.Null(session.Draft);
        Assert.Equal(AgentCapabilitiesLoadState.Failed, session.LoadState);
        Assert.NotEmpty(session.LoadError!);
    }

    private sealed class Reads : IAgentCapabilitiesReads {
        public AgentDefinition Alpha { get; } = Agent("Alpha");
        public AgentDefinition Beta { get; } = Agent("Beta");
        public IReadOnlyList<AgentDefinition> Agents { get; set; }
        public IReadOnlyList<CapabilityCatalogItem> Capabilities { get; set; } = [];
        public List<Guid> Requests { get; } = [];
        public CancellationToken LastToken { get; private set; }
        public Func<Guid, CancellationToken, Task<AgentEditorModel>>? Read { get; set; }

        public Reads() => Agents = [Alpha, Beta];
        public Task<AgentCapabilitiesCatalog> LoadCatalogAsync(CancellationToken cancellationToken) => Task.FromResult(new AgentCapabilitiesCatalog(Agents, Capabilities));
        public Task<AgentEditorModel> ReadEditorAsync(Guid agentId, CancellationToken cancellationToken) {
            LastToken = cancellationToken;
            Requests.Add(agentId);
            return Read?.Invoke(agentId, cancellationToken) ?? Task.FromResult(AgentEditorModel.FromDefinition(Agents.Single(agent => agent.Id == agentId)));
        }

        private static AgentDefinition Agent(string name) => new(Guid.NewGuid(), name, "Role", "Summary", "Instructions",
            AgentLifecycleStatus.Active, null, "model", AgentWorkloadKind.General, AgentChatHistoryMode.FrameworkManaged,
            0.2, true, false, "{}", false, "", AgentPermissionsPolicy.Default, [], [], DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
    }
}
