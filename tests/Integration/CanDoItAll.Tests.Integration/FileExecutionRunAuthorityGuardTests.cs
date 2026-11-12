using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using static CanDoItAll.Tests.Integration.Runtime.FileAgentPlanningPublicationSnapshotTests;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "HostPlatform")]
[Trait("Category", "FileSystemPortability")]
public sealed class FileExecutionRunAuthorityGuardTests {
    [Fact]
    public Task Authority_is_checked_after_selected_key_wait_before_the_prepared_change_is_published()
        => RunAsync(async scenario => {
            var before = await File.ReadAllBytesAsync(scenario.Layout.RunPath(scenario.Run.Id), scenario.Token);
            var index = await File.ReadAllBytesAsync(scenario.Layout.WorkspaceIndexPath, scenario.Token);
            await scenario.HoldRunKeyAsync();
            using var wait = new KeyWait();
            var allowed = true;
            var preparations = 0;
            var checks = 0;
            var pending = scenario.Track(scenario.Store.UpdateExecutionRunDetailAsync(scenario.Run.Id, current => {
                preparations++;
                return Changed(current);
            }, token => {
                token.ThrowIfCancellationRequested();
                checks++;
                if (!allowed) {
                    throw new AgentToolReceiptAccessDeniedException();
                }
                return Task.CompletedTask;
            }, scenario.Token));
            await wait.Entered.Task.WaitAsync(scenario.Token);
            Assert.Equal(1, preparations);
            Assert.Equal(0, checks);
            allowed = false;
            await scenario.ReleaseBlockerAsync();
            await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => scenario.ObserveAsync(pending));
            Assert.Equal(1, checks);
            Assert.Equal(1, preparations);
            Assert.Equal(before, await File.ReadAllBytesAsync(scenario.Layout.RunPath(scenario.Run.Id), scenario.Token));
            Assert.Equal(index, await File.ReadAllBytesAsync(scenario.Layout.WorkspaceIndexPath, scenario.Token));
            await scenario.RequireAllKeysAvailableAsync();
        }, inherited: true);

    [Fact]
    public Task Independent_catalog_publication_progresses_while_external_authority_is_observed()
        => RunAsync(async scenario => {
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var checks = 0;
            var preparations = 0;
            var pending = scenario.Track(scenario.Store.UpdateExecutionRunDetailAsync(scenario.Run.Id, current => {
                preparations++;
                return Changed(current);
            }, async token => {
                checks++;
                entered.SetResult();
                await release.Task.WaitAsync(token);
            }, scenario.Token));
            try {
                await entered.Task.WaitAsync(scenario.Token);
                Assert.False(pending.IsCompleted);
                await scenario.RequireWorkspaceAvailableAsync();
                await scenario.Store.UpdateCatalogAsync(current => current with {
                    Agents = current.Agents.Select(agent => agent.Id == scenario.UnrelatedId
                        ? agent with { Summary = "Independent manager work" } : agent).ToArray()
                }, scenario.Token);
            } finally {
                release.TrySetResult();
            }
            await scenario.ObserveAsync(pending);
            Assert.Equal(1, checks);
            Assert.Equal(1, preparations);
            var saved = (await scenario.Store.GetExecutionRunAsync(scenario.Run.Id, scenario.Token))!;
            Assert.Equal(scenario.Run.Revision + 1, saved.Revision);
            Assert.Equal("Guarded result", saved.ResultSummary);
            Assert.Equal("Independent manager work", (await scenario.Store.LoadCatalogAsync(scenario.Token))
                .Agents.Single(agent => agent.Id == scenario.UnrelatedId).Summary);
            await scenario.RequireAllKeysAvailableAsync();
        }, inherited: true);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Guarded_noop_checks_authority_without_advancing_aggregate_or_workspace(bool allowed)
        => RunAsync(async scenario => {
            var before = await File.ReadAllBytesAsync(scenario.Layout.RunPath(scenario.Run.Id), scenario.Token);
            var index = await File.ReadAllBytesAsync(scenario.Layout.WorkspaceIndexPath, scenario.Token);
            var checks = 0;
            var pending = scenario.Track(scenario.Store.UpdateExecutionRunDetailAsync(scenario.Run.Id, current => current, token => {
                token.ThrowIfCancellationRequested();
                checks++;
                if (!allowed) {
                    throw new AgentToolReceiptAccessDeniedException();
                }
                return Task.CompletedTask;
            }, scenario.Token));
            if (allowed) {
                await scenario.ObserveAsync(pending);
            } else {
                await Assert.ThrowsAsync<AgentToolReceiptAccessDeniedException>(() => scenario.ObserveAsync(pending));
            }
            Assert.Equal(1, checks);
            Assert.Equal(before, await File.ReadAllBytesAsync(scenario.Layout.RunPath(scenario.Run.Id), scenario.Token));
            Assert.Equal(index, await File.ReadAllBytesAsync(scenario.Layout.WorkspaceIndexPath, scenario.Token));
            await scenario.RequireAllKeysAvailableAsync();
        }, inherited: true);

    [Fact]
    public Task Cancellation_during_authority_observation_releases_keys_and_preserves_the_original()
        => RunAsync(async scenario => {
            using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(scenario.Token);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var before = await File.ReadAllBytesAsync(scenario.Layout.RunPath(scenario.Run.Id), scenario.Token);
            var pending = scenario.Track(scenario.Store.UpdateExecutionRunDetailAsync(scenario.Run.Id, Changed, async token => {
                entered.SetResult();
                await never.Task.WaitAsync(token);
            }, cancelled.Token));
            await entered.Task.WaitAsync(scenario.Token);
            cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scenario.ObserveAsync(pending));
            Assert.Equal(before, await File.ReadAllBytesAsync(scenario.Layout.RunPath(scenario.Run.Id), scenario.Token));
            await scenario.RequireAllKeysAvailableAsync();
        }, inherited: true);

    [Fact]
    public Task Authority_failure_is_preserved_without_publishing_or_leaking_selected_keys()
        => RunAsync(async scenario => {
            var expected = new IOException("Current authority observation failed.");
            var before = await File.ReadAllBytesAsync(scenario.Layout.RunPath(scenario.Run.Id), scenario.Token);
            var pending = scenario.Track(scenario.Store.UpdateExecutionRunDetailAsync(scenario.Run.Id, Changed,
                _ => throw expected, scenario.Token));
            Assert.Same(expected, await Assert.ThrowsAsync<IOException>(() => scenario.ObserveAsync(pending)));
            Assert.Equal(before, await File.ReadAllBytesAsync(scenario.Layout.RunPath(scenario.Run.Id), scenario.Token));
            await scenario.RequireAllKeysAvailableAsync();
        }, inherited: true);

    [Fact]
    public Task Original_run_witness_is_rechecked_after_external_authority_observation()
        => RunAsync(async scenario => {
            var path = scenario.Layout.RunPath(scenario.Run.Id);
            var before = await File.ReadAllBytesAsync(path, scenario.Token);
            var changedBytes = before.Concat(new byte[] { (byte)'\n' }).ToArray();
            var pending = scenario.Track(scenario.Store.UpdateExecutionRunDetailAsync(scenario.Run.Id, Changed,
                token => File.WriteAllBytesAsync(path, changedBytes, token), scenario.Token));
            try {
                await Assert.ThrowsAsync<SandboxWorkspacePublicationConflictException>(() => scenario.ObserveAsync(pending));
                Assert.Equal(changedBytes, await File.ReadAllBytesAsync(path, scenario.Token));
                Assert.Equal(scenario.Run.Revision,
                    (await scenario.Store.GetExecutionRunAsync(scenario.Run.Id, scenario.Token))!.Revision);
                await scenario.RequireAllKeysAvailableAsync();
            } finally {
                await File.WriteAllBytesAsync(path, before, scenario.Token);
            }
        }, inherited: true);

    [Fact]
    public async Task Unsupported_store_refuses_guarded_mutation_without_calling_legacy_writer_or_guard() {
        var legacy = new LegacyWriter();
        ISandboxWorkspaceExecutionRunMutationStore store = legacy;
        var checks = 0;
        await Assert.ThrowsAsync<NotSupportedException>(() => store.UpdateExecutionRunDetailAsync(Guid.NewGuid(), Changed, _ => {
            checks++;
            return Task.CompletedTask;
        }, default));
        Assert.Equal(0, legacy.Writes);
        Assert.Equal(0, checks);
    }

    private static ExecutionRunDetail Changed(ExecutionRunDetail current) => current with {
        Run = current.Run with { Revision = checked(current.Run.Revision + 1), ResultSummary = "Guarded result" }
    };

    private sealed class LegacyWriter : ISandboxWorkspaceExecutionRunMutationStore {
        internal int Writes { get; private set; }

        public Task<ExecutionRunDetail> UpdateExecutionRunDetailAsync(Guid executionRunId,
            Func<ExecutionRunDetail, ExecutionRunDetail> update, CancellationToken cancellationToken = default) {
            Writes++;
            throw new InvalidOperationException("Legacy mutation was entered.");
        }

        public Task<ExecutionRunDetail> UpdateExecutionRunDetailAsync(Guid executionRunId,
            Func<SandboxWorkspaceCatalog, ExecutionRunDetail, ExecutionRunDetail> update,
            CancellationToken cancellationToken = default) {
            Writes++;
            throw new InvalidOperationException("Legacy catalog mutation was entered.");
        }
    }
}
