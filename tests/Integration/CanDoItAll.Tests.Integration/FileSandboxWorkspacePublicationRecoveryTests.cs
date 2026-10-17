using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
[Trait("Category", "HostPlatform")]
public sealed class FileSandboxWorkspacePublicationRecoveryTests {
    [Theory]
    [InlineData(RecoveryKind.ChatStart)]
    [InlineData(RecoveryKind.GenericRun)]
    [InlineData(RecoveryKind.ExistingRun)]
    [InlineData(RecoveryKind.AgentDeletion)]
    public async Task Recovery_releases_workspace_before_keys_and_refuses_a_changed_journal_without_replaying_it(RecoveryKind kind) {
        await using var scenario = await Scenario.CreateAsync();
        var pendingRun = kind is RecoveryKind.ExistingRun or RecoveryKind.AgentDeletion
            ? scenario.Original.Run
            : scenario.Original.Run with { Id = Guid.NewGuid(), Revision = 1, ChatSessionId = kind == RecoveryKind.ChatStart ? scenario.Session.Id : null };
        await scenario.InterruptAsync(kind, pendingRun);
        var journalPath = scenario.JournalPath(kind);
        var originalJournal = await File.ReadAllBytesAsync(journalPath);
        var originalRun = File.Exists(scenario.Layout.RunPath(pendingRun.Id))
            ? await File.ReadAllBytesAsync(scenario.Layout.RunPath(pendingRun.Id)) : null;

        for (var cancellationCase = 0; cancellationCase < 2; cancellationCase++) {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var wait = new KeyWait();
            IAsyncDisposable? heldRun = await scenario.HoldRunAsync(pendingRun, cancellation.Token);
            Task<Exception?>? attempt = null;
            var attemptObserved = false;
            Exception? primary = null;
            try {
                var recoveryStore = scenario.NewStore();
                attempt = Record.ExceptionAsync(() => recoveryStore.LoadSnapshotAsync(cancellation.Token));
                await wait.Entered.WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(attempt.IsCompleted);
                await using (var availableWorkspace = await scenario.Writer.AcquireCoordinationAsync(scenario.Layout.RootPath,
                    scenario.Layout.WorkspaceLockPath, TimeSpan.FromSeconds(2), requirePrivateUnixMode: false, cancellation.Token)) {
                    Assert.Equal(originalJournal, await File.ReadAllBytesAsync(journalPath, cancellation.Token));
                }
                if (cancellationCase == 0) {
                    cancellation.Cancel();
                    var outcome = await attempt.WaitAsync(TimeSpan.FromSeconds(5));
                    attemptObserved = true;
                    Assert.IsAssignableFrom<OperationCanceledException>(outcome);
                    var firstKey = FileSandboxWorkspaceWriteSet.Run(pendingRun, pendingRun, readsAgent: true).LockPaths(scenario.Layout)[0];
                    await using var releasedPartialKey = await scenario.Writer.AcquireCoordinationAsync(scenario.Layout.RootPath,
                        firstKey, TimeSpan.FromSeconds(2), requirePrivateUnixMode: false, CancellationToken.None);
                } else {
                    await File.AppendAllTextAsync(journalPath, " ", cancellation.Token);
                    var release = heldRun;
                    heldRun = null;
                    await release.DisposeAsync();
                    var outcome = await attempt.WaitAsync(TimeSpan.FromSeconds(5));
                    attemptObserved = true;
                    var conflict = Assert.IsType<SandboxWorkspacePublicationConflictException>(outcome);
                    Assert.Equal(SandboxWorkspacePublicationConflictKind.PendingRecoveryChanged, conflict.Kind);
                }
                Assert.True(File.Exists(journalPath));
                if (originalRun is null) {
                    Assert.False(File.Exists(scenario.Layout.RunPath(pendingRun.Id)));
                } else {
                    Assert.Equal(originalRun, await File.ReadAllBytesAsync(scenario.Layout.RunPath(pendingRun.Id), cancellationToken: CancellationToken.None));
                }
            } catch (Exception failure) {
                primary = failure;
            } finally {
                await CompleteAttemptAsync(primary, cancellation, heldRun, attempt, attemptObserved);
            }
        }

        var recovered = await scenario.NewStore().LoadSnapshotAsync();
        Assert.False(File.Exists(journalPath));
        if (kind == RecoveryKind.AgentDeletion) {
            Assert.DoesNotContain(recovered.Document.Agents, item => item.Id == scenario.Agent.Id);
            Assert.DoesNotContain(recovered.Document.ExecutionRuns, item => item.AgentId == scenario.Agent.Id);
            Assert.False(Directory.Exists(scenario.Layout.RunRoot(pendingRun.Id)));
            Assert.False(File.Exists(scenario.Layout.SessionPath(scenario.Session.Id)));
        } else {
            var run = Assert.Single(recovered.Document.ExecutionRuns, item => item.Id == pendingRun.Id);
            Assert.Equal(kind == RecoveryKind.ExistingRun ? scenario.Original.Run.Revision + 1 : pendingRun.Revision, run.Revision);
            Assert.Equal("published after recovery", run.ResultSummary);
            Assert.Equal(pendingRun.AgentId, run.AgentId);
            Assert.Equal(pendingRun.ChatSessionId, run.ChatSessionId);
        }
        var committedIndex = await File.ReadAllBytesAsync(scenario.Layout.WorkspaceIndexPath);
        await scenario.NewStore().LoadSnapshotAsync();
        Assert.Equal(committedIndex, await File.ReadAllBytesAsync(scenario.Layout.WorkspaceIndexPath));
        await using var reacquired = await scenario.HoldRunAsync(pendingRun, CancellationToken.None);
    }

    [Fact]
    public async Task Same_size_run_bytes_changed_without_a_revision_change_cannot_be_overwritten_by_a_prepared_target() {
        await using var scenario = await Scenario.CreateAsync();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var wait = new KeyWait();
        IAsyncDisposable? held = await scenario.HoldRunAsync(scenario.Original.Run, cancellation.Token);
        Task<Exception?>? attempt = null;
        var attemptObserved = false;
        Exception? primary = null;
        ExecutionRunDetail? accepted = null;
        var callbacks = 0;
        try {
            attempt = Record.ExceptionAsync(async () => {
                accepted = await scenario.NewStore().UpdateExecutionRunDetailAsync(scenario.Original.Run.Id, detail => {
                    Interlocked.Increment(ref callbacks);
                    return detail with { Run = detail.Run with { ResultSummary = "unpublished target", Revision = detail.Run.Revision + 1 } };
                }, cancellation.Token);
            });
            await wait.Entered.WaitAsync(TimeSpan.FromSeconds(5));
            var path = scenario.Layout.RunPath(scenario.Original.Run.Id);
            var before = await File.ReadAllBytesAsync(path, cancellation.Token);
            var changed = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(before).Replace("Original result", "Modified result", StringComparison.Ordinal));
            Assert.Equal(before.Length, changed.Length);
            Assert.NotEqual(before, changed);
            await File.WriteAllBytesAsync(path, changed, cancellation.Token);
            var release = held;
            held = null;
            await release.DisposeAsync();
            var outcome = await attempt.WaitAsync(TimeSpan.FromSeconds(5));
            attemptObserved = true;
            var conflict = Assert.IsType<SandboxWorkspacePublicationConflictException>(outcome);
            Assert.Equal(SandboxWorkspacePublicationConflictKind.OriginalBytesChanged, conflict.Kind);
            Assert.Null(accepted);
            Assert.Equal(1, callbacks);
            Assert.Equal(changed, await File.ReadAllBytesAsync(path, cancellation.Token));
            var saved = Assert.IsType<ExecutionRunDetail>(await scenario.Store.GetExecutionRunDetailAsync(scenario.Original.Run.Id));
            Assert.Equal(scenario.Original.Run.Revision, saved.Run.Revision);
            Assert.Equal("Modified result", saved.Run.ResultSummary);
        } catch (Exception failure) {
            primary = failure;
        } finally {
            await CompleteAttemptAsync(primary, cancellation, held, attempt, attemptObserved);
        }
    }

    [Fact]
    public async Task A_waiting_run_only_publication_allows_unrelated_commit_and_uses_fresh_shared_indexes_without_callback_replay() {
        await using var scenario = await Scenario.CreateAsync();
        var independent = await scenario.Store.SaveExecutionRunDetailAsync(scenario.Original with {
            Run = scenario.Original.Run with { Id = Guid.NewGuid(), ChatSessionId = null }, ChatSession = null
        });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var wait = new KeyWait();
        IAsyncDisposable? held = await scenario.HoldRunAsync(scenario.Original.Run, cancellation.Token);
        Task<Exception?>? attempt = null;
        Task<ExecutionRunDetail>? independentAttempt = null;
        var attemptObserved = false;
        Exception? primary = null;
        ExecutionRunDetail? accepted = null;
        var callbacks = 0;
        try {
            attempt = Record.ExceptionAsync(async () => {
                accepted = await scenario.NewStore().UpdateExecutionRunDetailAsync(scenario.Original.Run.Id, detail => {
                    Interlocked.Increment(ref callbacks);
                    return detail with { Run = detail.Run with { ResultSummary = "selected accepted", Revision = detail.Run.Revision + 1 } };
                }, cancellation.Token);
            });
            await wait.Entered.WaitAsync(TimeSpan.FromSeconds(5));
            independentAttempt = scenario.NewStore().UpdateExecutionRunDetailAsync(independent.Run.Id,
                detail => detail with { Run = detail.Run with { ResultSummary = "independent accepted", Revision = detail.Run.Revision + 1 } },
                cancellation.Token);
            var independentResult = await independentAttempt.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(independent.Run.Revision + 1, independentResult.Run.Revision);
            Assert.False(attempt.IsCompleted);
            var release = held;
            held = null;
            await release.DisposeAsync();
            var outcome = await attempt.WaitAsync(TimeSpan.FromSeconds(5));
            attemptObserved = true;
            Assert.Null(outcome);
            Assert.NotNull(accepted);
            Assert.Equal(1, callbacks);
            var selected = Assert.IsType<ExecutionRunDetail>(await scenario.Store.GetExecutionRunDetailAsync(scenario.Original.Run.Id));
            Assert.Equal(scenario.Original.Run.Revision + 1, selected.Run.Revision);
            Assert.Equal("selected accepted", selected.Run.ResultSummary);
            var stillIndependent = Assert.IsType<ExecutionRunDetail>(await scenario.Store.GetExecutionRunDetailAsync(independent.Run.Id));
            Assert.Equal(independentResult.Run.Revision, stillIndependent.Run.Revision);
            Assert.Equal("independent accepted", stillIndependent.Run.ResultSummary);
        } catch (Exception failure) {
            primary = failure;
        } finally {
            await CompleteAttemptAsync(primary, cancellation, held, attempt, attemptObserved, independentAttempt);
        }
        await using var reacquired = await scenario.HoldRunAsync(scenario.Original.Run, CancellationToken.None);
    }

    [Theory]
    [InlineData(PendingMutation.Run)]
    [InlineData(PendingMutation.Workspace)]
    [InlineData(PendingMutation.AgentDeletion)]
    public async Task Pending_publication_evidence_appearing_during_key_wait_refuses_before_any_new_effect(PendingMutation mutation) {
        await using var scenario = await Scenario.CreateAsync();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var target = mutation switch {
            PendingMutation.Run => scenario.Layout.RunPath(scenario.Original.Run.Id),
            PendingMutation.Workspace => scenario.Layout.SessionPath(scenario.Session.Id),
            PendingMutation.AgentDeletion => Path.Combine(scenario.Layout.RunRoot(scenario.Original.Run.Id), "absent.json"),
            _ => throw new ArgumentOutOfRangeException(nameof(mutation))
        };
        var malformed = Path.Combine(Path.GetDirectoryName(target)!, $".{Path.GetFileName(target)}.not-a-guid.pending");
        var marker = Path.Combine(Path.GetDirectoryName(target)!, $".{Path.GetFileName(target)}.{Guid.NewGuid():N}.pending");
        await File.WriteAllTextAsync(malformed, "not a durable publication marker", cancellation.Token);
        var observedPaths = new[] { scenario.Layout.CatalogPath, scenario.Layout.WorkspaceIndexPath,
            scenario.Layout.RunPath(scenario.Original.Run.Id), scenario.Layout.SessionPath(scenario.Session.Id) };
        var originals = new List<byte[]>();
        foreach (var path in observedPaths) {
            originals.Add(await File.ReadAllBytesAsync(path, cancellation.Token));
        }
        using var wait = new KeyWait();
        IAsyncDisposable? held = await scenario.HoldRunAsync(scenario.Original.Run, cancellation.Token);
        Task<Exception?>? attempt = null;
        var attemptObserved = false;
        Exception? primary = null;
        var accepted = false;
        try {
            attempt = Record.ExceptionAsync(async () => {
                var owner = scenario.NewStore();
                switch (mutation) {
                    case PendingMutation.Run:
                        await owner.UpdateExecutionRunDetailAsync(scenario.Original.Run.Id,
                            detail => detail with { Run = detail.Run with { ResultSummary = "unpublished", Revision = detail.Run.Revision + 1 } },
                            cancellation.Token);
                        break;
                    case PendingMutation.Workspace:
                        await owner.UpdateWorkspaceAsync(document => document with {
                            Agents = document.Agents.Select(agent => agent.Id == scenario.Agent.Id
                                ? agent with { Name = "unpublished catalog change" } : agent).ToArray(),
                            ChatSessions = document.ChatSessions.Select(session => session.Id == scenario.Session.Id
                                ? session with { Title = "unpublished session change" } : session).ToArray()
                        }, cancellation.Token);
                        break;
                    case PendingMutation.AgentDeletion:
                        await owner.DeleteAgentWorkspaceDataAsync(scenario.Agent.Id, cancellation.Token);
                        break;
                }
                accepted = true;
            });
            await wait.Entered.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(attempt.IsCompleted);
            var evidence = Encoding.UTF8.GetBytes("preserved uncertain publication bytes");
            await File.WriteAllBytesAsync(marker, evidence, cancellation.Token);
            var release = held;
            held = null;
            await release.DisposeAsync();
            var outcome = await attempt.WaitAsync(TimeSpan.FromSeconds(5));
            attemptObserved = true;
            var conflict = Assert.IsType<SandboxWorkspacePublicationConflictException>(outcome);
            Assert.Equal(SandboxWorkspacePublicationConflictKind.PendingRecoveryChanged, conflict.Kind);
            Assert.False(accepted);
            for (var index = 0; index < observedPaths.Length; index++) {
                Assert.Equal(originals[index], await File.ReadAllBytesAsync(observedPaths[index], cancellation.Token));
            }
            Assert.Equal(evidence, await File.ReadAllBytesAsync(marker, cancellation.Token));
            Assert.True(File.Exists(malformed));
            var existingEvidence = await Assert.ThrowsAsync<SandboxWorkspacePublicationConflictException>(async () =>
                await scenario.NewStore().UpdateExecutionRunDetailAsync(scenario.Original.Run.Id,
                    detail => detail with { Run = detail.Run with { ResultSummary = "also unpublished", Revision = detail.Run.Revision + 1 } },
                    cancellation.Token));
            Assert.Equal(SandboxWorkspacePublicationConflictKind.PendingRecoveryChanged, existingEvidence.Kind);
            Assert.Equal(evidence, await File.ReadAllBytesAsync(marker, cancellation.Token));
            foreach (var kind in Enum.GetValues<RecoveryKind>()) {
                Assert.False(File.Exists(scenario.JournalPath(kind)));
            }
            if (mutation == PendingMutation.AgentDeletion) {
                Assert.False(File.Exists(target));
            }
        } catch (Exception failure) {
            primary = failure;
        } finally {
            await CompleteAttemptAsync(primary, cancellation, held, attempt, attemptObserved);
        }
        await using var reacquired = await scenario.HoldRunAsync(scenario.Original.Run, CancellationToken.None);
        await using var workspace = await scenario.Writer.AcquireCoordinationAsync(scenario.Layout.RootPath,
            scenario.Layout.WorkspaceLockPath, TimeSpan.FromSeconds(2), requirePrivateUnixMode: false, CancellationToken.None);
    }

    private static async Task CompleteAttemptAsync(Exception? primary, CancellationTokenSource cancellation,
        IAsyncDisposable? held, Task<Exception?>? attempt, bool attemptObserved, Task? independentAttempt = null) {
        var failures = new List<Exception>();
        if (primary is not null) {
            failures.Add(primary);
        }
        try {
            await cancellation.CancelAsync();
        } catch (Exception failure) {
            failures.Add(failure);
        }
        try {
            if (held is not null) {
                await held.DisposeAsync();
            }
        } catch (Exception failure) {
            failures.Add(failure);
        }
        try {
            if (attempt is not null) {
                var failure = await attempt;
                if (!attemptObserved && failure is not null &&
                    !(failure is OperationCanceledException && cancellation.IsCancellationRequested) && !ReferenceEquals(failure, primary)) {
                    failures.Add(failure);
                }
            }
        } catch (Exception failure) {
            failures.Add(failure);
        }
        try {
            if (independentAttempt is not null) {
                await independentAttempt;
            }
        } catch (OperationCanceledException) when (cancellation.IsCancellationRequested) {
        } catch (Exception failure) {
            if (!ReferenceEquals(failure, primary)) {
                failures.Add(failure);
            }
        }
        if (failures.Count == 1) {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }
        if (failures.Count > 1) {
            throw new AggregateException("Publication proof and owned cleanup failed.", failures);
        }
    }

    public enum PendingMutation {
        Run,
        Workspace,
        AgentDeletion
    }

    public enum RecoveryKind {
        ChatStart,
        GenericRun,
        ExistingRun,
        AgentDeletion
    }

    private sealed class KeyWait : IDisposable {
        private readonly Activity activity = new Activity("workspace-publication-proof").SetIdFormat(ActivityIdFormat.W3C).Start();
        private readonly ActivityListener listener;
        private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal KeyWait() {
            listener = new() {
                ShouldListenTo = source => source.Name == AgentFrameworkTelemetry.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStarted = started => {
                    if (started.OperationName == "workspace.publication.keys.wait" && started.TraceId == activity.TraceId) {
                        entered.TrySetResult();
                    }
                }
            };
            ActivitySource.AddActivityListener(listener);
        }

        internal Task Entered => entered.Task;

        public void Dispose() {
            listener.Dispose();
            activity.Dispose();
        }
    }

    private sealed class Scenario(CanDoItAllTestEnvironment environment, string root, FileSandboxWorkspaceStore store,
        AgentDefinition agent, ChatSessionRecord session, ExecutionRunDetail original) : IAsyncDisposable {
        internal FileSandboxWorkspaceStore Store { get; } = store;
        internal AgentDefinition Agent { get; } = agent;
        internal ChatSessionRecord Session { get; } = session;
        internal ExecutionRunDetail Original { get; } = original;
        internal FileSandboxWorkspaceStorageLayout Layout { get; } = new(root, WorkspaceScopeDescriptor.Sandbox);
        internal DurableFileWriter Writer { get; } = new(new PhysicalFileSystemPathPolicyFactory());

        internal FileSandboxWorkspaceStore NewStore() => new(Layout.RootPath, WorkspaceScopeDescriptor.Sandbox);

        internal ValueTask<IAsyncDisposable> HoldRunAsync(ExecutionRunRecord run, CancellationToken cancellationToken) =>
            Writer.AcquireCoordinationAsync(Layout.RootPath,
                FileSandboxWorkspaceWriteSet.Run(run, run, readsAgent: false).LockPaths(Layout)[0],
                TimeSpan.FromSeconds(5), requirePrivateUnixMode: false, cancellationToken);

        internal string JournalPath(RecoveryKind kind) => Path.Combine(Layout.ExecutionStorageRoot, kind switch {
            RecoveryKind.ChatStart => "pending-chat-run-start.json",
            RecoveryKind.GenericRun => "pending-run-start.json",
            RecoveryKind.ExistingRun => "pending-run-detail-update.json",
            RecoveryKind.AgentDeletion => "pending-agent-deletion.json",
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        });

        internal async Task InterruptAsync(RecoveryKind kind, ExecutionRunRecord run) {
            var failing = new FileSandboxWorkspaceStore(Layout.RootPath, WorkspaceScopeDescriptor.Sandbox,
                stage => Fail(kind == RecoveryKind.ChatStart && stage == ChatBackedRunCommitStage.JournalPersisted),
                stage => Fail(kind == RecoveryKind.ExistingRun && stage == ExistingRunDetailCommitStage.JournalPersisted),
                stage => Fail(kind == RecoveryKind.GenericRun && stage == GenericNewRunCommitStage.JournalPersisted),
                jsonReadDiagnostics: null,
                agentDeletionCommitBoundary: stage => Fail(kind == RecoveryKind.AgentDeletion && stage == AgentDeletionCommitStage.JournalPersisted));
            await Assert.ThrowsAsync<InjectedPublicationFailure>(async () => {
                switch (kind) {
                    case RecoveryKind.ChatStart:
                        var catalog = await failing.LoadCatalogSnapshotAsync();
                        await failing.BeginChatBackedRunAsync(new(Agent.Id, Agent.ProviderProfileId!.Value, catalog.Revision, Session.Id), context => {
                            var message = new ChatMessageRecord(Guid.NewGuid(), ChatMessageRole.User, "Original request", run.CreatedAtUtc, 1);
                            var targetSession = context.Session! with {
                                LatestExecutionRunId = run.Id, UpdatedAtUtc = run.UpdatedAtUtc,
                                Messages = [.. context.Session!.Messages, message]
                            };
                            return new(new(run with { ResultSummary = "published after recovery" }, targetSession, [], []), message);
                        });
                        break;
                    case RecoveryKind.GenericRun:
                        await failing.SaveExecutionRunDetailAsync(new(run with { ResultSummary = "published after recovery" }, null, [], []));
                        break;
                    case RecoveryKind.ExistingRun:
                        await failing.UpdateExecutionRunDetailAsync(run.Id, detail => detail with {
                            Run = detail.Run with { ResultSummary = "published after recovery", Revision = detail.Run.Revision + 1 }
                        });
                        break;
                    case RecoveryKind.AgentDeletion:
                        await failing.DeleteAgentWorkspaceDataAsync(Agent.Id);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(kind));
                }
            });

            static void Fail(bool selected) {
                if (selected) {
                    throw new InjectedPublicationFailure();
                }
            }
        }

        internal static async Task<Scenario> CreateAsync() {
            var environment = CanDoItAllTestEnvironment.Create("workspace-publication-recovery");
            try {
                var profile = environment.CreateInMemoryProfile("primary");
                var store = new FileSandboxWorkspaceStore(profile.WorkspaceRootPath, WorkspaceScopeDescriptor.Sandbox);
                var catalog = await store.LoadCatalogAsync();
                var agent = catalog.Agents.First(item => item.ProviderProfileId.HasValue) with {
                    Id = Guid.NewGuid(), Name = "Publication proof", IsTemplate = false, TemplateKey = string.Empty,
                    ConfigurationJson = "{}"
                };
                await store.UpdateCatalogAsync(current => current with { Agents = [.. current.Agents, agent] });
                var now = DateTimeOffset.UtcNow;
                var session = await store.CreateChatSessionAsync(new(Guid.NewGuid(), agent.Id, "Publication proof", now, now, []));
                var run = new ExecutionRunRecord(Guid.NewGuid(), agent.Id, session.Id, session.Title,
                    "integration-test", session.Id.ToString("N"), Guid.NewGuid().ToString("N"), string.Empty,
                    "integration-test", "integration-test", "{}", "Original request", "Original result", "Fixture provider", "fixture-model",
                    ExecutionState.Completed, RunOutcome.Succeeded, now, now, now, now, string.Empty, null, []) {
                    ProviderProfileId = agent.ProviderProfileId
                };
                var detail = await store.SaveExecutionRunDetailAsync(new(run, session with { LatestExecutionRunId = run.Id }, [], []));
                return new(environment, profile.WorkspaceRootPath, store, agent, detail.ChatSession!, detail);
            } catch (Exception failure) {
                await TestFixtureCleanup.DisposeAfterFailureAsync(failure, environment);
                throw;
            }
        }

        public ValueTask DisposeAsync() => environment.DisposeAsync();
    }

    private sealed class InjectedPublicationFailure : Exception;
}
