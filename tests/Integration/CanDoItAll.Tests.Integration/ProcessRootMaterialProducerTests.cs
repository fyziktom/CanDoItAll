using System.Data.Common;
using System.Text;
using System.Text.Json;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit.Abstractions;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessRootMaterialProducerTests(ITestOutputHelper output) {
    [Fact]
    public async Task Registered_preparation_captures_real_material_before_any_claim_and_reuses_it() {
        await using var fixture = await ProcessRootMaterialFixture.CreateAsync();
        var coordinator = fixture.Services.GetRequiredService<ProcessDecisionRootPreparationCoordinator>();
        var prepared = await coordinator.PrepareAsync(fixture.State);
        Assert.Equal(ProcessDecisionRootPreparationStatus.Ready, prepared.Status);
        await using var context = Context(fixture.Services);
        var material = Assert.Single(await context.DecisionMaterials.ToArrayAsync());
        var basis = ProjectTaskPlanBasisCodec.Decode(Encoding.UTF8.GetBytes(material.PayloadJson));
        Assert.Equal(fixture.Sql.Project, basis.Project.Expectation.Project);
        Assert.Equal(fixture.Root.WorkspaceNamespaceId, basis.Namespace.Value);
        Assert.Empty(await context.DispatchClaims.ToArrayAsync());
        var allocation = Assert.Single(await context.DecisionBudgetAllocations.ToArrayAsync());
        Assert.NotNull(allocation.OwnerWorkRevision);
        var owner = ProcessRootMaterialFixture.Owner(context, fixture.Services);
        var claimed = await owner.CommitAsync(ProcessDecisionRunScopeFixture.Claim(prepared.State));
        Assert.True(claimed.CommittedNow);
        Assert.Equal(ProcessDecisionRootPreparationStatus.Ready, (await coordinator.PrepareAsync(claimed.State)).Status);
        Assert.Equal(1, await context.DecisionMaterials.CountAsync());
        Assert.Equal(1, await context.DecisionBudgetAllocations.CountAsync());
    }

    [Fact]
    public async Task Competing_preparation_calls_invoke_the_real_capturer_once_outside_the_root_transaction() {
        await using var fixture = await ProcessRootMaterialFixture.CreateAsync();
        await using var first = Context(fixture.Services);
        await using var second = Context(fixture.Services);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var capturer = new CaptureProbe(fixture.Capturer, async token => {
            Assert.Null(first.Database.CurrentTransaction);
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
        });
        var winner = fixture.Coordinator(first, capturer).PrepareAsync(fixture.State);
        ProcessDecisionRootPreparationResult loser;
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            loser = await fixture.Coordinator(second, capturer).PrepareAsync(fixture.State);
            Assert.Equal(ProcessDecisionRootPreparationStatus.CapturePending, loser.Status);
        } finally {
            release.TrySetResult();
            await winner;
        }
        Assert.Equal(ProcessDecisionRootPreparationStatus.Ready, (await winner).Status);
        Assert.Equal(1, capturer.Calls);
        Assert.Single(await first.DecisionMaterials.ToArrayAsync());
        Assert.Single(await first.DecisionBudgetAllocations.ToArrayAsync());
    }

    [Theory]
    [InlineData(ProcessDecisionRunScopeCommandKind.BeginCapture)]
    [InlineData(ProcessDecisionRunScopeCommandKind.CompleteCapture)]
    public async Task Lost_acknowledgements_never_repeat_capture(ProcessDecisionRunScopeCommandKind lost) {
        await using var fixture = await ProcessRootMaterialFixture.CreateAsync();
        await using var context = Context(fixture.Services);
        var capturer = new CaptureProbe(fixture.Capturer);
        var owner = ProcessRootMaterialFixture.Owner(context, fixture.Services);
        var coordinator = fixture.Coordinator(context, capturer, new LostAcknowledgement(owner, lost));
        if (lost == ProcessDecisionRunScopeCommandKind.BeginCapture) {
            await Assert.ThrowsAsync<IOException>(() => coordinator.PrepareAsync(fixture.State));
        } else {
            Assert.Equal(ProcessDecisionRootPreparationStatus.Ready, (await coordinator.PrepareAsync(fixture.State)).Status);
        }
        var current = (await owner.LoadAsync(fixture.State.RunId))!;
        var replay = await coordinator.PrepareAsync(current);
        Assert.Equal(lost == ProcessDecisionRunScopeCommandKind.BeginCapture
            ? ProcessDecisionRootPreparationStatus.CapturePending : ProcessDecisionRootPreparationStatus.Ready, replay.Status);
        Assert.Equal(lost == ProcessDecisionRunScopeCommandKind.BeginCapture ? 0 : 1, capturer.Calls);
        Assert.Single(await context.DecisionBudgetAllocations.ToArrayAsync());
    }

    [Fact]
    public async Task Root_cancellation_during_capture_aborts_material_and_settles_closed_work() {
        await using var fixture = await ProcessRootMaterialFixture.CreateAsync();
        await using var context = Context(fixture.Services);
        var capturer = new CaptureProbe(fixture.Capturer, after: async _ => {
            await using var cancellationContext = Context(fixture.Services);
            var owner = ProcessRootMaterialFixture.Owner(cancellationContext, fixture.Services);
            var state = (await owner.LoadAsync(fixture.State.RunId))!;
            var request = Request(state, null);
            await owner.CommitAsync(request with { Mutation = request.Mutation with {
                State = request.Mutation.State with { Status = ProcessRuntimeStatus.CancelRequested }
            } });
        });
        var result = await fixture.Coordinator(context, capturer).PrepareAsync(fixture.State);
        Assert.Equal(ProcessDecisionRootPreparationStatus.Stopped, result.Status);
        Assert.Empty(await context.DecisionMaterials.ToArrayAsync());
        Assert.NotNull(Assert.Single(await context.DecisionBudgetAllocations.ToArrayAsync()).OwnerWorkRevision);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Closed_failure_or_overrun_settles_actual_work_without_activating_material(bool overrun) {
        await using var fixture = await ProcessRootMaterialFixture.CreateAsync();
        await using var context = Context(fixture.Services);
        var clock = new WorkClock();
        var capturer = new CaptureProbe(fixture.Capturer, after: _ => {
            clock.Elapsed = overrun ? TimeSpan.FromSeconds(11) : TimeSpan.FromSeconds(2);
            if (!overrun) {
                throw new IOException("Closed read failed.");
            }
            return Task.CompletedTask;
        });
        var coordinator = fixture.Coordinator(context, capturer, clock: clock);
        if (overrun) {
            Assert.Equal(ProcessDecisionRootPreparationStatus.ReviewRequired, (await coordinator.PrepareAsync(fixture.State)).Status);
        } else {
            await Assert.ThrowsAsync<IOException>(() => coordinator.PrepareAsync(fixture.State));
        }
        var budget = (await new EfProcessDecisionBudgetStore(context).ReadAsync(fixture.State.RunId, null))!;
        Assert.Equal(clock.Elapsed.Ticks, budget.ConsumedWorkTicks);
        Assert.Equal(0, budget.ReservedWorkTicks);
        Assert.Equal(overrun, budget.OverrunDetected);
        Assert.Empty(await context.DecisionMaterials.ToArrayAsync());
        var current = (await ProcessRootMaterialFixture.Owner(context, fixture.Services).LoadAsync(fixture.State.RunId))!;
        var retry = await fixture.Coordinator(context).PrepareAsync(current);
        Assert.Equal(overrun ? ProcessDecisionRootPreparationStatus.ReviewRequired : ProcessDecisionRootPreparationStatus.Ready, retry.Status);
        Assert.Equal(overrun ? 1 : 2, await context.DecisionBudgetAllocations.CountAsync());
    }

    [Fact]
    public async Task Cancelled_awaited_capture_settles_its_closed_duration_without_detaching_work() {
        await using var fixture = await ProcessRootMaterialFixture.CreateAsync();
        await using var context = Context(fixture.Services);
        using var cancellation = new CancellationTokenSource();
        var clock = new WorkClock();
        var capturer = new CaptureProbe(fixture.Capturer, token => {
            clock.Elapsed = TimeSpan.FromSeconds(2);
            cancellation.Cancel();
            token.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Coordinator(context, capturer, clock: clock)
            .PrepareAsync(fixture.State, cancellation.Token));
        var allocation = Assert.Single(await context.DecisionBudgetAllocations.ToArrayAsync());
        Assert.Equal(clock.Elapsed.Ticks, allocation.ConsumedWorkTicks);
        Assert.NotNull(allocation.OwnerWorkRevision);
        Assert.Empty(await context.DecisionMaterials.ToArrayAsync());
    }

    [Fact]
    public async Task A_descendant_request_cannot_become_the_uncaptured_root_owner() {
        await using var fixture = await ProcessRootMaterialFixture.CreateAsync();
        await using var context = Context(fixture.Services);
        var capturer = new CaptureProbe(fixture.Capturer);
        var descendant = fixture.State with { RunId = new(Guid.NewGuid()) };
        var result = await fixture.Coordinator(context, capturer).PrepareAsync(descendant);
        Assert.Equal(ProcessDecisionRootPreparationStatus.CapturePending, result.Status);
        Assert.Equal(0, capturer.Calls);
        Assert.Empty(await context.DecisionBudgetAllocations.ToArrayAsync());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(128)]
    public async Task Root_selection_reads_three_bounded_rows_without_assignments_prompts_or_material_payload(int steps) {
        await using var fixture = await ProcessRootMaterialFixture.CreateAsync(steps);
        var probe = new ReadProbe();
        await using var context = Context(fixture.Services, probe);
        var selected = await fixture.Query(context).ReadAsync(fixture.State);
        Assert.Equal(fixture.Root.MaterialScope, selected!.Scope.Admission);
        Assert.Equal(3, probe.Commands.Count);
        Assert.All(probe.Commands, command => {
            Assert.DoesNotContain("\"PayloadJson\"", command.Sql, StringComparison.Ordinal);
            Assert.DoesNotContain("process_accepted_assignment_sources", command.Sql, StringComparison.Ordinal);
            Assert.DoesNotContain("\"Prompt\"", command.Sql, StringComparison.Ordinal);
            Assert.Contains("LIMIT 2", command.Sql, StringComparison.Ordinal);
        });
        await context.Database.OpenConnectionAsync();
        var scalarBytes = 0;
        foreach (var selectedCommand in probe.Commands) {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = selectedCommand.Sql;
            command.CommandTimeout = 5;
            command.Parameters.AddRange(selectedCommand.Parameters);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            for (var index = 0; index < reader.FieldCount; index++) {
                if (!await reader.IsDBNullAsync(index)) {
                    scalarBytes += JsonSerializer.SerializeToUtf8Bytes(reader.GetValue(index)).Length;
                }
            }
            Assert.False(await reader.ReadAsync());
        }
        Assert.InRange(scalarBytes, 1, 32_768);
        output.WriteLine("Steps={0}; commands=3; selected rows=3; serialized selected scalar bytes={1}", steps, scalarBytes);
        var headerBytes = await context.PreparedLaunches.Where(item => item.RunId == fixture.State.RunId.Value)
            .Select(item => item.AcceptedSourceHeaderJson!).SingleAsync();
        Assert.InRange(Encoding.UTF8.GetByteCount(headerBytes), 1, 4096);
    }

    [Fact]
    public async Task Explicit_legacy_source_is_refused_without_decoding_its_payload() {
        await using var fixture = await ProcessRootMaterialFixture.CreateAsync();
        await using var context = Context(fixture.Services);
        await context.PreparedLaunches.Where(item => item.RunId == fixture.State.RunId.Value).ExecuteUpdateAsync(update => update
            .SetProperty(item => item.AcceptedSourceVersion, ProcessAcceptedSourceVersion.LegacyPreparedLaunch)
            .SetProperty(item => item.AcceptedSourceHeaderJson, (string?)null)
            .SetProperty(item => item.AcceptedSourceHeaderFingerprint, (string?)null)
            .SetProperty(item => item.PayloadJson, "invalid legacy review copy"));
        var result = await fixture.Coordinator(context).PrepareAsync(fixture.State);
        Assert.Equal(ProcessDecisionRootPreparationStatus.SourceProjectionRequired, result.Status);
        Assert.Empty(await context.DecisionBudgetAllocations.ToArrayAsync());
    }

    private sealed class CaptureProbe(IProcessDecisionRootCapturer inner, Func<CancellationToken, Task>? before = null,
        Func<CancellationToken, Task>? after = null) : IProcessDecisionRootCapturer {
        internal int Calls { get; private set; }
        public ProcessDecisionContentContract ContentContract => inner.ContentContract;
        public async Task<ProcessDecisionRootCaptureResult> CaptureAsync(ProcessDecisionRootCaptureRequest request, CancellationToken cancellationToken = default) {
            Calls++;
            if (before is not null) {
                await before(cancellationToken);
            }
            var result = await inner.CaptureAsync(request, cancellationToken);
            if (after is not null) {
                await after(cancellationToken);
            }
            return result;
        }
    }

    private sealed class LostAcknowledgement(IProcessRuntimeUnitOfWork inner, ProcessDecisionRunScopeCommandKind lost) : IProcessRuntimeUnitOfWork {
        private bool thrown;
        public async Task<ProcessRuntimeCommitResult> CommitAsync(ProcessRuntimeCommitRequest request, CancellationToken cancellationToken = default) {
            var committed = await inner.CommitAsync(request, cancellationToken);
            if (!thrown && request.DecisionRunScope?.Kind == lost) {
                thrown = true;
                throw new IOException("The committed capture acknowledgement was lost.");
            }
            return committed;
        }
    }

    private sealed class WorkClock : TimeProvider {
        internal TimeSpan Elapsed { get; set; }
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Elapsed.Ticks;
    }

    private sealed class ReadProbe : DbCommandInterceptor {
        internal List<SelectedCommand> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Commands.Add(new(command.CommandText, command.Parameters.Cast<NpgsqlParameter>()
                .Select(parameter => (NpgsqlParameter)((ICloneable)parameter).Clone()).ToArray()));
            return ValueTask.FromResult(result);
        }
    }

    private sealed record SelectedCommand(string Sql, NpgsqlParameter[] Parameters);
}
