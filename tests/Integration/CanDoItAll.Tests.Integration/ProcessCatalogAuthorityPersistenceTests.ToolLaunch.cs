using System.Data.Common;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Background_tool_preparation_and_acceptance_require_the_actual_owner_policy_before_writes(bool acceptance) {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock);
        var preparation = await CreateToolLaunchAsync(services, run, clock);
        if (acceptance) {
            var saved = await ToolStore(services, fixture, clock).PrepareAsync(preparation);
            await using var context = fixture.Context();
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ToolUnit(services, fixture, context, clock, missingPolicy: true).CommitAsync(ProcessPreparedLaunchFixture.Commit(saved)));
            Assert.Contains("owner admission policy", failure.Message, StringComparison.Ordinal);
            await using var fresh = fixture.Context();
            await AssertNoInitialRowsAsync(fresh, saved);
            Assert.Null((await fixture.Store.GetAsync(saved.Preparation.AdmissionId))!.AcceptedAtUtc);
        } else {
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ToolStore(services, fixture, clock, missingPolicy: true).PrepareAsync(preparation));
            Assert.Contains("owner admission policy", failure.Message, StringComparison.Ordinal);
            Assert.Null(await fixture.Store.FindByIntentAsync(preparation.CallerIntentId!.Value));
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Background_tool_claim_expiry_before_or_after_flush_rolls_back_all_new_owner_effects(bool acceptance, bool afterFlush) {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock);
        var preparation = await CreateToolLaunchAsync(services, run, clock);
        var saved = acceptance ? await ToolStore(services, fixture, clock).PrepareAsync(preparation) : null;
        var probe = new ToolLaunchProbe(preparation.AdmissionId.Value, acceptance) {
            AfterFlush = afterFlush ? () => clock.Now = clock.Now.AddHours(2) : null
        };
        if (!afterFlush) {
            clock.Now = clock.Now.AddHours(2);
        }
        if (acceptance) {
            await using var context = fixture.Context(probe);
            await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() =>
                ToolUnit(services, fixture, context, clock).CommitAsync(ProcessPreparedLaunchFixture.Commit(saved!)));
            await using var fresh = fixture.Context();
            await AssertNoInitialRowsAsync(fresh, saved!);
            var retained = (await fixture.Store.GetAsync(preparation.AdmissionId))!;
            Assert.Equal(saved!.PreparationFingerprint, retained.PreparationFingerprint);
            Assert.Null(retained.AcceptedAtUtc);
        } else {
            await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() =>
                ToolStore(services, fixture, clock, probe).PrepareAsync(preparation));
            Assert.Null(await fixture.Store.GetAsync(preparation.AdmissionId));
        }
        Assert.Equal(afterFlush ? 1 : 0, probe.Flushes);
        Assert.Equal(0, probe.Commits);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Background_tool_lost_commit_ack_preserves_exact_exception_and_original_preparation_or_accepted_run(
        bool acceptance, bool argumentException) {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock);
        var preparation = await CreateToolLaunchAsync(services, run, clock);
        Exception failure = argumentException ? new ArgumentException("Injected Process tool commit acknowledgement loss.")
            : new IOException("Injected Process tool commit acknowledgement loss.");
        var probe = new ToolLaunchProbe(preparation.AdmissionId.Value, acceptance) { AfterCommitFailure = failure };
        var saved = acceptance ? await ToolStore(services, fixture, clock).PrepareAsync(preparation) : null;
        if (acceptance) {
            await using var context = fixture.Context(probe);
            Assert.Same(failure, await Record.ExceptionAsync(() =>
                ToolUnit(services, fixture, context, clock).CommitAsync(ProcessPreparedLaunchFixture.Commit(saved!))));
        } else {
            Assert.Same(failure, await Record.ExceptionAsync(() => ToolStore(services, fixture, clock, probe).PrepareAsync(preparation)));
        }
        Assert.Equal(1, probe.Flushes);
        Assert.Equal(1, probe.Commits);
        var retained = (await fixture.Store.GetAsync(preparation.AdmissionId))!;
        Assert.Equal(preparation.AdmissionId, retained.Preparation.AdmissionId);
        Assert.Equal(preparation.ToolSource!.Execution.Evidence, retained.Preparation.ToolSource!.Execution.Evidence);
        Assert.Equal(acceptance, retained.AcceptedAtUtc is not null);
        clock.Now = clock.Now.AddHours(2);
        await fixture.RevokeAsync(Revocation.Write);
        if (acceptance) {
            await using var restarted = fixture.Context();
            var replay = await ToolUnit(services, fixture, restarted, clock).CommitAsync(ProcessPreparedLaunchFixture.Commit(retained));
            Assert.Equal(ProcessRuntimeTransitionOutcome.Applied, replay.Outcome);
            Assert.Equal(retained.Preparation.InitialCommit.Mutation.State.RunId, replay.State.RunId);
            await AssertToolInitialRowsAsync(fixture, retained);
        } else {
            var replay = await ToolStore(services, fixture, clock).PrepareAsync(preparation);
            Assert.Equal(retained.PreparationFingerprint, replay.PreparationFingerprint);
            Assert.Equal(retained.Preparation.AdmissionId, replay.Preparation.AdmissionId);
            await using var fresh = fixture.Context();
            await AssertNoInitialRowsAsync(fresh, retained);
        }
        Assert.Equal(1, probe.Commits);
        await using var catalogAvailable = await fixture.Source.AcquireAgentReadLeaseAsync(fixture.Agent.Id);
        Assert.Equal(fixture.Agent.Id, catalogAvailable.Agent!.Id);
    }

    [Fact]
    public async Task Concurrent_same_tool_intent_converges_on_one_preparation_and_changed_target_conflicts() {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock);
        var first = await CreateToolLaunchAsync(services, run, clock);
        var contender = await CreateToolLaunchAsync(services, run, clock, first.CallerIntentId);
        Assert.Equal(first.RequestFingerprint, contender.RequestFingerprint);
        Assert.NotEqual(first.AdmissionId, contender.AdmissionId);
        var results = await Task.WhenAll(ToolStore(services, fixture, clock).PrepareAsync(first),
            ToolStore(services, fixture, clock).PrepareAsync(contender));
        Assert.Equal(results[0].Preparation.AdmissionId, results[1].Preparation.AdmissionId);
        Assert.Equal(results[0].PreparationFingerprint, results[1].PreparationFingerprint);
        await using var context = fixture.Context();
        Assert.Equal(1, await context.PreparedLaunches.CountAsync(row => row.CallerIntentId == first.CallerIntentId!.Value.Value));
        var changed = contender with { LinkTarget = new(fixture.Project.ProjectId, "other-node", "other-binding") };
        var request = changed.Request with { LinkTarget = changed.LinkTarget, ProjectNodeId = "other-node" };
        changed = changed with { Request = request, RequestFingerprint = ProcessLaunchIntentFingerprint.Compute(request) };
        await Assert.ThrowsAsync<ProcessLaunchIntentConflictException>(() => ToolStore(services, fixture, clock).PrepareAsync(changed));
        Assert.Equal(results[0].PreparationFingerprint, (await fixture.Store.FindByIntentAsync(first.CallerIntentId!.Value))!.PreparationFingerprint);
    }

    [Fact]
    public async Task Normal_prepared_launch_reader_remains_independent_of_uncommitted_tool_admission_and_retains_source_profile() {
        var clock = new NativeClock();
        await using var app = await TestApplication.CreateAsync(NativeHarness(clock));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var fixture = await Fixture.CreateAsync(services);
        var run = await CreateClaimedExecutionAsync(services, fixture, clock);
        var preparation = await CreateToolLaunchAsync(services, run, clock);
        var independentRead = false;
        var probe = new ToolLaunchProbe(preparation.AdmissionId.Value, false) {
            BeforeCommit = async token => {
                Assert.Null(await fixture.Store.FindByIntentAsync(preparation.CallerIntentId!.Value, token));
                independentRead = true;
            }
        };
        var saved = await ToolStore(services, fixture, clock, probe).PrepareAsync(preparation);
        Assert.True(independentRead);
        Assert.Equal(fixture.Project.DatabaseProfileId, saved.Preparation.ToolSource!.Execution.SourceAuthority!.DatabaseProfileId);
        Assert.Equal(saved.Preparation.AdmissionId, (await fixture.Store.FindByIntentAsync(preparation.CallerIntentId!.Value))!.Preparation.AdmissionId);
        await using var context = fixture.Context();
        var entity = await context.PreparedLaunches.AsNoTracking().SingleAsync(row => row.Id == preparation.AdmissionId.Value);
        Assert.True(entity.ReferencesProject());
    }

    private static async Task<ProcessPreparedLaunch> CreateToolLaunchAsync(IServiceProvider services, ExecutionRunRecord run,
        NativeClock clock, ProcessLaunchIntentId? intent = null) {
        var reader = (IProcessExecutionDispatchAuthorityReader)NativeReader(services, clock);
        var dispatch = Assert.IsType<ProcessExecutionDispatchAuthority>((await reader.ReadAsync(run.Id)).Snapshot);
        var preparation = ProcessPreparedLaunchFixture.Create(dispatch.SourceAuthority, intent ?? new(Guid.NewGuid()));
        var source = new ProcessLaunchToolSource(ProcessLaunchToolSource.CurrentSchemaVersion, dispatch, run.SourceKind,
            run.SourceId, new string('c', 64), preparation.CallerIntentId!.Value, new string('d', 64));
        var request = preparation.Request with { ToolSource = source, ProducerInputFingerprint = source.ProposalFingerprint };
        return preparation with { Request = request, ToolSource = source, RequestFingerprint = ProcessLaunchIntentFingerprint.Compute(request) };
    }

    private static ProjectProcessToolLaunchAdmissionPolicy ToolPolicy(IServiceProvider services, Fixture fixture, NativeClock clock)
        => new(NativeReader(services, clock), new EfProcessExecutionMutationGuard(NativeOptions<ProcessPersistenceDbContext>(services),
            fixture.Coordinator, clock), fixture.Authority);

    private static EfProcessPreparedLaunchStore ToolStore(IServiceProvider services, Fixture fixture, NativeClock clock,
        IInterceptor? interceptor = null, bool missingPolicy = false) {
        var options = NativeOptions<ProcessPersistenceDbContext>(services, interceptor);
        return new(new Factory<ProcessPersistenceDbContext>(options, static configured => new(configured)), options, clock,
            fixture.Coordinator, missingPolicy ? null : ToolPolicy(services, fixture, clock));
    }

    private static EfProcessRuntimeUnitOfWork ToolUnit(IServiceProvider services, Fixture fixture, ProcessPersistenceDbContext context,
        NativeClock clock, bool missingPolicy = false) {
        var admissions = new ProjectWriteAdmissionService(new Factory<ProjectsDbContext>(fixture.ProjectOptions, static configured => new(configured)),
            fixture.ProjectOptions, fixture.Coordinator, services.GetRequiredService<ICanonicalRuntimeDatabase>());
        return new(context, clock, fixture.Coordinator, new ProjectProcessAdmissionPolicy(admissions), fixture.Authority,
            missingPolicy ? null : ToolPolicy(services, fixture, clock));
    }

    private static async Task AssertToolInitialRowsAsync(Fixture fixture, ProcessPreparedLaunchSnapshot saved) {
        await using var context = fixture.Context();
        var request = saved.Preparation.InitialCommit;
        var run = request.Mutation.State.RunId.Value;
        Assert.Equal(1, await context.RuntimeStates.CountAsync(row => row.RunId == run));
        Assert.Equal(1, await context.InstancePlans.CountAsync(row => row.PlanId == request.Mutation.State.PlanId.Value));
        Assert.Equal(request.InitialAssignments!.Count, await context.RuntimeStepAssignments.CountAsync(row => row.RunId == run));
        Assert.Equal(request.Mutation.Events.Count, await context.RuntimeEvents.CountAsync(row => row.RunId == run));
        Assert.Equal(1, await context.IdempotencyKeys.CountAsync(row => row.RunId == run));
    }

    private sealed class ToolLaunchProbe(Guid admissionId, bool accepted) : SaveChangesInterceptor, IDbTransactionInterceptor {
        public int Flushes { get; private set; }
        public int Commits { get; private set; }
        public Action? AfterFlush { get; init; }
        public Func<CancellationToken, Task>? BeforeCommit { get; init; }
        public Exception? AfterCommitFailure { get; init; }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (eventData.Context is ProcessPersistenceDbContext context && result > 0 &&
                    context.ChangeTracker.Entries<ProcessPreparedLaunchEntity>().Any(entry => entry.Entity.Id == admissionId &&
                        (entry.Entity.AcceptedAtUtc is not null) == accepted)) {
                Assert.NotNull(context.Database.CurrentTransaction);
                Flushes++;
                AfterFlush?.Invoke();
            }
            return ValueTask.FromResult(result);
        }

        public async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) {
            if (Flushes != 0 && BeforeCommit is not null) {
                await BeforeCommit(cancellationToken);
            }
            return result;
        }

        public async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) {
            var connection = eventData.Context?.Database.GetDbConnection()
                ?? throw new InvalidOperationException("The commit probe requires the owner context.");
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT EXISTS (SELECT 1 FROM process_prepared_launches WHERE \"Id\" = @id AND (\"AcceptedAtUtc\" IS NOT NULL) = @accepted)";
            command.Parameters.Add(new NpgsqlParameter<Guid>("id", admissionId));
            command.Parameters.Add(new NpgsqlParameter<bool>("accepted", accepted));
            if (await command.ExecuteScalarAsync(cancellationToken) is true) {
                Commits++;
                if (AfterCommitFailure is not null) {
                    throw AfterCommitFailure;
                }
            }
        }
    }
}
