using System.Data.Common;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessRootCancellationFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessCancellationRecoveryFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessCancellationRuntimeRecoveryTests {
    [Fact]
    public async Task Native_keyset_includes_root_and_terminal_descendants_without_loading_runtime_payloads() {
        await using var app = await TestApplication.CreateAsync(ProcessExecutionAuthorityPersistenceTests.Harness());
        await using var context = Context(app.Services);
        var owner = ProcessDecisionRunScopeFixture.Owner(context, app.Services);
        var root = await CommitAsync(owner, ProcessDecisionRunScopeFixture.Claim(await CommitAsync(owner, ProcessProjectAdmissionFixture.Initial())));
        var first = await CommitAsync(owner, Descendant(root));
        var second = await CommitAsync(owner, Descendant(root));
        var unrelated = await CommitAsync(owner, ProcessProjectAdmissionFixture.Initial());
        var terminal = await new ProcessRuntimeEngine(owner).RequestCancellationAsync(first, Command(first));
        Assert.True(terminal.Succeeded);
        await StopAsync(owner, root);
        var queries = new MetadataQueries();
        var reader = new EfProcessCancellationRuntimeReader(new Factory(Options(app.Services, queries)));
        var selected = new List<ProcessRunId>();
        ProcessRunId? after = null;
        for (var operation = 0; operation < 5; operation++) {
            var next = await reader.NextAsync(root.RunId, after, default);
            if (next is null) {
                break;
            }
            selected.Add(next.Value);
            after = next;
        }
        Assert.Equal(new[] { root.RunId, first.RunId, second.RunId }.OrderBy(item => item.Value), selected);
        Assert.Equal(4, queries.Commands.Count);
        Assert.All(queries.Commands, query => {
            Assert.Contains("LIMIT", query.Sql, StringComparison.Ordinal);
            Assert.DoesNotContain("JOIN", query.Sql, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PlanHash", query.Sql, StringComparison.Ordinal);
            Assert.Equal(5, query.Timeout);
        });
        Assert.Equal(new(first.RunId, ProcessRuntimeStatus.Cancelled), await reader.ReadAsync(root.RunId, first.RunId, default));
        Assert.Null(await reader.ReadAsync(root.RunId, unrelated.RunId, default));
        Assert.Null(await reader.NextAsync(ProcessRunId.New(), null, default));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => reader.NextAsync(root.RunId, default(ProcessRunId), default));
    }

    [Fact]
    public async Task Lost_native_cancel_acknowledgement_replays_original_intent_after_new_lease_and_context() {
        await using var app = await TestApplication.CreateAsync(ProcessExecutionAuthorityPersistenceTests.Harness());
        await using var setup = Context(app.Services);
        var owner = ProcessDecisionRunScopeFixture.Owner(setup, app.Services);
        var root = await CommitAsync(owner, ProcessDecisionRunScopeFixture.Claim(await CommitAsync(owner, ProcessProjectAdmissionFixture.Initial())));
        var descendant = await CommitAsync(owner, Descendant(root));
        await StopAsync(owner, root);
        var queue = Queue(app.Services);
        var claim = (await queue.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease))!;
        for (var operation = 0; operation < 8 && claim.Intent is null && claim.Position.Stage == ProcessCancellationRecoveryStage.Runtime; operation++) {
            var checkpoint = await AdvanceAsync(app.Services, claim);
            claim = (await queue.CheckpointAsync(claim, checkpoint)).Claim!;
        }
        var original = Assert.IsType<ProcessCancellationCancelRun>(claim.Intent);
        Assert.Equal(descendant.RunId, original.RunId);
        Assert.Equal(descendant.RunId, claim.Position.CurrentRun);
        var failure = new IOException("The native cancellation committed before acknowledgement.");
        Assert.Same(failure, await Assert.ThrowsAsync<IOException>(() => AdvanceAsync(app.Services, claim, new LostAcknowledgement(failure))));
        await using var inspection = Context(app.Services);
        var committed = await new EfProcessRuntimeUnitOfWork(inspection).FindCompletedCommandAsync(original.Context.CommandId);
        Assert.NotNull(committed);
        Assert.Equal(ProcessRuntimeStatus.Cancelled, committed.State.Status);
        var events = await inspection.RuntimeEvents.CountAsync();
        var receipts = await inspection.IdempotencyKeys.CountAsync();
        Assert.Equal(ProcessCancellationCheckpointStatus.Released, (await queue.CheckpointAsync(claim,
            new(claim.Position, claim.Intent, RetryAfter: TimeSpan.Zero))).Status);
        var reopened = Queue(app.Services);
        var recovered = (await reopened.ClaimAsync(ProcessCancellationRecoveryOrigin.LiveBarrier, Lease))!;
        Assert.Equal(original, recovered.Intent);
        Assert.Equal(claim.Position, recovered.Position);
        Assert.True(recovered.LeaseGeneration > claim.LeaseGeneration);
        var resolution = await AdvanceAsync(app.Services, recovered);
        Assert.Equal(new(original.Context.CommandId, ProcessCancellationIntentOutcome.Committed), resolution.Resolution);
        var saved = await reopened.CheckpointAsync(recovered, resolution);
        Assert.Equal(ProcessCancellationCheckpointStatus.Applied, saved.Status);
        Assert.Null(saved.Claim!.Intent);
        Assert.Equal(events, await inspection.RuntimeEvents.CountAsync());
        Assert.Equal(receipts, await inspection.IdempotencyKeys.CountAsync());
        var notification = await AdvanceAsync(app.Services, saved.Claim);
        Assert.Equal(descendant.RunId, notification.Position.RunAfter);
        Assert.False(notification.Position.CycleHadPending);
    }

    private static async Task<ProcessRuntimeStateSnapshot> CommitAsync(EfProcessRuntimeUnitOfWork owner, ProcessRuntimeCommitRequest request) {
        var result = await owner.CommitAsync(request);
        Assert.True(result.Succeeded, string.Join("; ", result.Diagnostics.Select(item => item.Message)));
        return result.State;
    }

    private static async Task<ProcessCancellationRecoveryCheckpoint> AdvanceAsync(IServiceProvider services,
        ProcessCancellationRecoveryClaim claim, params IInterceptor[] interceptors) {
        await using var context = new ProcessPersistenceDbContext(Options(services, interceptors));
        var owner = new EfProcessRuntimeUnitOfWork(context);
        var factory = new Factory(Options(services));
        var step = new ProcessCancellationRuntimeRecoveryStep(new EfProcessCancellationRuntimeReader(factory),
            new EfProcessRootCancellationBarrierReader(factory), new NoExecutions(), owner, owner, owner, TimeProvider.System);
        var result = await step.AdvanceAsync(claim, default);
        result.Validate(claim);
        return result;
    }

    private sealed class NoExecutions : IProcessExecutionRecovery {
        public Task<ProcessCancellationExecutionProgress> InspectNextAsync(ProcessRunId rootRunId, ProcessRunId runId,
            ProcessExecutionRunId? after, CancellationToken cancellationToken) => Task.FromResult(new ProcessCancellationExecutionProgress(after, true));
    }

    private sealed class MetadataQueries : DbCommandInterceptor {
        internal List<(string Sql, int Timeout)> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Commands.Add((command.CommandText, command.CommandTimeout));
            return ValueTask.FromResult(result);
        }
    }
}
