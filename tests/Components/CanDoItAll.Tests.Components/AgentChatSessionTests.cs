using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework.Pages.Components;
using Microsoft.Extensions.Logging.Abstractions;
using static CanDoItAll.Tests.Components.AgentFramework.AgentChatPanelResponsivenessTests;
using ProviderService = CanDoItAll.Modules.AgentFramework.ProviderManagement.IProviderRuntimeAdministrationService;

namespace CanDoItAll.Tests.Components.AgentFramework;

public sealed class AgentChatSessionTests {
    [Theory]
    [InlineData(ProfileReadStage.Catalog)]
    [InlineData(ProfileReadStage.Workspace)]
    [InlineData(ProfileReadStage.Detail)]
    public async Task Profile_change_during_a_read_rejects_the_old_snapshot_before_acceptance(ProfileReadStage stage) {
        var (service, reads) = CreateReads();
        var agent = CreateAgent();
        var chat = CreateSession(agent.Id);
        var run = CreateRunningRun(agent.Id, chat.Id) with { State = ExecutionState.Completed };
        var source = new MutableProfileGeneration();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task PauseAsync(ProfileReadStage current) {
            if (stage == current) {
                started.SetResult();
                await release.Task;
            }
        }
        reads.Catalog = async (_, _) => {
            await PauseAsync(ProfileReadStage.Catalog);
            return [agent];
        };
        reads.Workspace = async (_, _, _) => {
            await PauseAsync(ProfileReadStage.Workspace);
            return CreateWorkspace(agent.Id, chat, run);
        };
        reads.Detail = async (_, _) => {
            await PauseAsync(ProfileReadStage.Detail);
            return new(run, chat, [], []);
        };
        using var session = Session(service, source);
        var load = session.LoadAsync(agent.Id, chat.Id, false, expectedProfileGeneration: source.GetGeneration());
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var generation = session.Generation;
        source.Value = new(2);
        release.SetResult();

        Assert.False(await load);
        Assert.False(session.IsCurrent(generation));
        Assert.False(session.MatchesAccepted(agent.Id, chat.Id));
        Assert.Null(session.Workspace);
        Assert.Null(session.Agent);
    }

    [Fact]
    public async Task Recovery_read_rejects_a_profile_change_before_admission_without_querying_the_new_profile() {
        var (service, reads) = CreateReads();
        var agent = CreateAgent();
        var chat = CreateSession(agent.Id);
        reads.Agents = [agent];
        reads.Workspace = (_, _, _) => Task.FromResult(CreateWorkspace(agent.Id, chat));
        var source = new MutableProfileGeneration();
        using var session = Session(service, source);
        var original = source.GetGeneration();
        source.Value = new(2);

        Assert.False(await session.LoadAsync(agent.Id, chat.Id, false, expectedProfileGeneration: original));
        Assert.Equal(0, reads.CatalogCalls);
        Assert.Equal(0, reads.WorkspaceCalls);
        Assert.True(await session.LoadAsync(agent.Id, chat.Id, false));
        Assert.True(session.MatchesAccepted(agent.Id, chat.Id));
    }

    [Theory]
    [InlineData(WorkflowOwnershipTests.DelayedReadOutcome.Success, false)]
    [InlineData(WorkflowOwnershipTests.DelayedReadOutcome.Failure, false)]
    [InlineData(WorkflowOwnershipTests.DelayedReadOutcome.Cancellation, false)]
    [InlineData(WorkflowOwnershipTests.DelayedReadOutcome.Success, true)]
    [InlineData(WorkflowOwnershipTests.DelayedReadOutcome.Failure, true)]
    [InlineData(WorkflowOwnershipTests.DelayedReadOutcome.Cancellation, true)]
    public async Task Target_read_token_remains_usable_until_its_own_request_completes(WorkflowOwnershipTests.DelayedReadOutcome outcome, bool dispose) {
        var (service, reads) = CreateReads();
        var first = CreateAgent();
        var second = CreateAgent();
        reads.Agents = [first, second];
        var entered = new TaskCompletionSource<CancellationToken>();
        var release = new TaskCompletionSource();
        Exception? tokenFailure = null;
        var callbacks = 0;
        var signaled = false;
        reads.Workspace = async (id, _, token) => {
            if (id != first.Id) {
                return CreateWorkspace(id, CreateSession(id));
            }
            entered.SetResult(token);
            await release.Task;
            tokenFailure = Record.Exception(() => {
                using var registration = token.Register(() => callbacks++);
                signaled = token.WaitHandle.WaitOne(0);
            });
            return outcome switch {
                WorkflowOwnershipTests.DelayedReadOutcome.Success => CreateWorkspace(id, CreateSession(id)),
                WorkflowOwnershipTests.DelayedReadOutcome.Failure => throw new InvalidOperationException("PRIVATE_DELAYED_CHAT_READ"),
                _ => throw new OperationCanceledException(token)
            };
        };
        using var session = Session(service);
        var old = session.LoadAsync(first.Id, null, false);
        var token = await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        if (dispose) {
            session.Dispose();
        } else {
            Assert.True(await session.LoadAsync(second.Id, null, false));
        }
        Assert.True(token.IsCancellationRequested);
        release.SetResult();
        Assert.False(await old);
        Assert.Null(tokenFailure);
        Assert.Equal(1, callbacks);
        Assert.True(signaled);
        Assert.Throws<ObjectDisposedException>(() => {
            _ = token.WaitHandle;
        });
        Assert.Empty(session.ErrorMessage);
        Assert.False(session.IsLoading);
        if (!dispose) {
            Assert.Equal(second.Id, session.Workspace!.AgentId);
        }
    }

    [Fact]
    public async Task Explicit_missing_agent_is_unavailable_without_loading_another_agent() {
        var (service, reads) = CreateReads();
        reads.Agents = [CreateAgent()];
        using var session = Session(service);
        var requested = Guid.NewGuid();
        Assert.False(await session.LoadAsync(requested, null, false));
        Assert.Equal(requested, session.DesiredAgentId);
        Assert.Null(session.Agent);
        Assert.Null(session.Workspace);
        Assert.Equal(0, reads.WorkspaceCalls);
        Assert.Contains("not available", session.ErrorMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Catalog_completion_or_failure_for_A_cannot_publish_over_B(bool fail) {
        var (service, reads) = CreateReads();
        var a = CreateAgent();
        var b = CreateAgent() with { Name = "Accepted B" };
        var pending = new TaskCompletionSource<IReadOnlyList<AgentDefinition>>();
        reads.Catalog = (_, _) => reads.CatalogCalls == 1 ? pending.Task : Task.FromResult<IReadOnlyList<AgentDefinition>>([b]);
        reads.Workspace = (id, _, _) => Task.FromResult(CreateWorkspace(id, CreateSession(id)));
        using var session = Session(service);
        var old = session.LoadAsync(a.Id, null, false);
        var token = session.TargetCancellation;
        Assert.True(await session.LoadAsync(b.Id, null, false));
        if (fail) {
            pending.SetException(new InvalidOperationException("private catalog failure"));
        } else {
            pending.SetResult([a]);
        }
        Assert.False(await old);
        Assert.True(token.IsCancellationRequested);
        Assert.Equal(b.Id, session.Agent!.Id);
        Assert.Equal(b.Id, Assert.Single(session.Agents).Id);
        Assert.Empty(session.ErrorMessage);
        Assert.False(session.IsLoading);
    }

    [Fact]
    public async Task Late_workspace_finally_cannot_clear_new_loading_or_accept_an_old_target() {
        var (service, reads) = CreateReads();
        var a = CreateAgent();
        var b = CreateAgent();
        reads.Agents = [a, b];
        var oldResult = new TaskCompletionSource<ChatAgentWorkspaceSnapshot>();
        var newResult = new TaskCompletionSource<ChatAgentWorkspaceSnapshot>();
        reads.Workspace = (id, _, _) => id == a.Id ? oldResult.Task : newResult.Task;
        using var session = Session(service);
        var first = session.LoadAsync(a.Id, null, false);
        var second = session.LoadAsync(b.Id, null, false);
        oldResult.SetResult(CreateWorkspace(a.Id, CreateSession(a.Id)));
        Assert.False(await first);
        Assert.True(session.IsLoading);
        Assert.Null(session.Workspace);
        newResult.SetResult(CreateWorkspace(b.Id, CreateSession(b.Id)));
        Assert.True(await second);
        Assert.Equal(b.Id, session.Workspace!.AgentId);
    }

    [Fact]
    public async Task Run_detail_for_S1_is_rejected_after_S2_and_old_events_are_fenced() {
        var (service, reads) = CreateReads();
        var agent = CreateAgent();
        var s1 = CreateSession(agent.Id);
        var s2 = CreateSession(agent.Id);
        var r1 = CreateRunningRun(agent.Id, s1.Id);
        var r2 = CreateRunningRun(agent.Id, s2.Id);
        reads.Agents = [agent];
        reads.Workspace = (_, id, _) => Task.FromResult(CreateWorkspace(agent.Id, id == s1.Id ? s1 : s2, id == s1.Id ? r1 : r2));
        var oldDetail = new TaskCompletionSource<ExecutionRunDetail>();
        reads.Detail = (id, _) => id == r1.Id ? oldDetail.Task : Task.FromResult(new ExecutionRunDetail(r2, s2, [], []));
        using var session = Session(service);
        var first = session.LoadAsync(agent.Id, s1.Id, false);
        var oldGeneration = session.Generation;
        Assert.True(await session.LoadAsync(agent.Id, s2.Id, false));
        oldDetail.SetResult(new(r1, s1, [], []));
        Assert.False(await first);
        Assert.Equal(s2.Id, session.Workspace!.SelectedSessionId);
        Assert.Equal(r2.Id, session.Workspace.SelectedRun!.Id);
        var entry = new ExecutionLogEntry(Guid.NewGuid(), agent.Id, s1.Id, DateTimeOffset.UtcNow, ExecutionState.Completed, "Old", "obsolete") { ExecutionRunId = r1.Id };
        Assert.False(session.TryObserveExecution(entry, oldGeneration));
        Assert.False(session.TryObserveExecution(entry, session.Generation));
        Assert.Empty(session.ExecutionLog);
    }

    [Fact]
    public async Task Explicit_missing_session_never_relabels_the_fallback_session() {
        var (service, reads) = CreateReads();
        var agent = CreateAgent();
        var fallback = CreateSession(agent.Id);
        reads.Agents = [agent];
        reads.Workspace = (_, _, _) => Task.FromResult(CreateWorkspace(agent.Id, fallback));
        using var session = Session(service);
        var missing = Guid.NewGuid();
        Assert.False(await session.LoadAsync(agent.Id, missing, false));
        Assert.Equal(missing, session.DesiredSessionId);
        Assert.Null(session.Workspace);
        Assert.Contains("thread is not available", session.ErrorMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Focused_fast_path_requires_matching_identity_and_non_template(bool invalid) {
        var (service, reads) = CreateReads();
        var agent = CreateAgent();
        var supplied = invalid ? agent with { IsTemplate = true } : agent;
        reads.Workspace = (id, _, _) => Task.FromResult(CreateWorkspace(id, CreateSession(id)));
        using var session = Session(service);
        Assert.Equal(!invalid, await session.LoadAsync(agent.Id, null, true, supplied));
        Assert.Equal(invalid ? 1 : 0, reads.CatalogCalls);
        Assert.Equal(invalid ? null : agent.Id, session.Agent?.Id);
    }

    [Fact]
    public async Task Focused_fast_path_rejects_a_wrong_ID_or_inactive_definition() {
        var (service, reads) = CreateReads();
        var agent = CreateAgent();
        using var session = Session(service);
        Assert.False(await session.LoadAsync(agent.Id, null, true, agent with { Id = Guid.NewGuid() }));
        Assert.False(await session.LoadAsync(agent.Id, null, true, agent with { Status = AgentLifecycleStatus.Suspended }));
        Assert.Null(session.Agent);
        Assert.Equal(0, reads.WorkspaceCalls);
    }

    [Fact]
    public async Task Accepted_collections_are_copied_and_same_agent_metadata_changes_once() {
        var (service, reads) = CreateReads();
        var tags = new List<string> { "accepted" };
        var agent = CreateAgent() with { Tags = tags };
        var messages = new List<ChatMessageRecord>();
        var thread = CreateSession(agent.Id) with { Messages = messages };
        reads.Agents = [agent];
        reads.Workspace = (_, _, _) => Task.FromResult(CreateWorkspace(agent.Id, thread));
        using var session = Session(service);
        Assert.True(await session.LoadAsync(agent.Id, thread.Id, false));
        var revision = session.AgentRevision;
        Assert.True(await session.RefreshCatalogAsync());
        Assert.Equal(revision, session.AgentRevision);
        tags.Add("late mutation");
        messages.Add(new(Guid.NewGuid(), ChatMessageRole.Assistant, "late message", DateTimeOffset.UtcNow, 1));
        Assert.Single(session.Agent!.Tags);
        Assert.Empty(session.Workspace!.SelectedSession!.Messages);
        reads.Agents = [agent with { Name = "Changed observation" }];
        Assert.True(await session.RefreshCatalogAsync());
        Assert.Equal(revision + 1, session.AgentRevision);
        Assert.True(await session.RefreshCatalogAsync());
        Assert.Equal(revision + 1, session.AgentRevision);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Disposal_suppresses_non_cooperative_catalog_or_workspace_completion(bool catalog) {
        var (service, reads) = CreateReads();
        var agent = CreateAgent();
        var pendingCatalog = new TaskCompletionSource<IReadOnlyList<AgentDefinition>>();
        var pendingWorkspace = new TaskCompletionSource<ChatAgentWorkspaceSnapshot>();
        reads.Agents = [agent];
        if (catalog) {
            reads.Catalog = (_, _) => pendingCatalog.Task;
        }
        reads.Workspace = (_, _, _) => pendingWorkspace.Task;
        var session = Session(service);
        var load = session.LoadAsync(agent.Id, null, false);
        var token = session.TargetCancellation;
        session.Dispose();
        pendingCatalog.TrySetResult([agent]);
        pendingWorkspace.TrySetResult(CreateWorkspace(agent.Id, CreateSession(agent.Id)));
        Assert.False(await load);
        Assert.True(token.IsCancellationRequested);
        Assert.Null(session.Workspace);
        Assert.Null(session.Agent);
    }

    internal static (IAgentFrameworkWorkspaceService Service, ReadProxy Reads) CreateReads() {
        var service = DispatchProxy.Create<IAgentFrameworkWorkspaceService, ReadProxy>();
        return (service, (ReadProxy)(object)service);
    }

    private static AgentChatSession Session(IAgentFrameworkWorkspaceService service, IAgentExecutionProfileGenerationSource? generationSource = null)
        => new(service, DispatchProxy.Create<ProviderService, ReadProxy>(),
            generationSource ?? new FixedAgentExecutionProfileGenerationSource(new(0)), NullLogger.Instance);

    public enum ProfileReadStage { Catalog, Workspace, Detail }

    private sealed class MutableProfileGeneration : IAgentExecutionProfileGenerationSource {
        public DatabaseProfileGeneration Value { get; set; } = new(0);
        public DatabaseProfileGeneration GetGeneration() => Value;
    }

    public class ReadProxy : DispatchProxy {
        public IReadOnlyList<AgentDefinition> Agents { get; set; } = [];
        public Func<bool, CancellationToken, Task<IReadOnlyList<AgentDefinition>>>? Catalog { get; set; }
        public Func<Guid, Guid?, CancellationToken, Task<ChatAgentWorkspaceSnapshot>>? Workspace { get; set; }
        public Func<Guid, CancellationToken, Task<ExecutionRunDetail>>? Detail { get; set; }
        public Func<Guid, Guid, string, Task<ChatSessionRecord>>? Rename { get; set; }
        public Func<Guid, Task<ChatSessionRecord>>? CreateThread { get; set; }
        public Func<Guid, Task<AgentEditorModel>>? Editor { get; set; }
        public Func<Guid, CancellationToken, Task<AgentEditorModel>>? EditorWithToken { get; set; }
        public Func<AgentEditorModel, Task<Guid>>? SaveAgent { get; set; }
        public int CatalogCalls { get; private set; }
        public int WorkspaceCalls { get; private set; }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            switch (targetMethod?.Name) {
                case nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync):
                    CatalogCalls++;
                    return Catalog?.Invoke((bool)args![0]!, (CancellationToken)args![1]!) ?? Task.FromResult(Agents);
                case nameof(ProviderService.ListProvidersAsync):
                    return Task.FromResult<IReadOnlyList<ProviderProfile>>([]);
                case nameof(IAgentFrameworkWorkspaceService.GetChatAgentWorkspaceAsync):
                    WorkspaceCalls++;
                    return Workspace!((Guid)args![0]!, (Guid?)args![1], (CancellationToken)args![2]!);
                case nameof(IAgentFrameworkWorkspaceService.GetExecutionRunDetailAsync):
                    return Detail!((Guid)args![0]!, (CancellationToken)args![1]!);
                case nameof(IAgentFrameworkWorkspaceService.RenameChatSessionAsync):
                    return Rename!((Guid)args![0]!, (Guid)args![1]!, (string)args![2]!);
                case nameof(IAgentFrameworkWorkspaceService.GetOrCreateChatSessionAsync):
                    return CreateThread!((Guid)args![0]!);
                case nameof(IAgentFrameworkWorkspaceService.GetAgentEditorAsync):
                    return EditorWithToken?.Invoke((Guid)args![0]!, args.OfType<CancellationToken>().Single()) ?? Editor!((Guid)args![0]!);
                case nameof(IAgentFrameworkWorkspaceService.SaveAgentAsync):
                    return SaveAgent!((AgentEditorModel)args![0]!);
                case "add_ExecutionUpdated":
                case "remove_ExecutionUpdated":
                    return null;
                default:
                    throw new NotSupportedException(targetMethod?.Name);
            }
        }
    }
}
