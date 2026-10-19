using System.Collections.Immutable;
using System.Data;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectTaskPlanActiveWorkTests {
    [Fact]
    public async Task Active_work_interpretation_excludes_closed_wait_and_replays_without_spending_work_again() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var original = await fixture.CommandAsync(1);
        var old = original.Source.DecisionStartedAtUtc.AddHours(-1);
        var legacy = Version(original, 1, old);
        Assert.Equal(ProjectTaskPlanRefusal.BudgetElapsed,
            Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await fixture.Publisher().PublishAsync(legacy, fixture.Invocation)).Reason);
        await fixture.VerifyAsync(0, 0);

        var command = Version(await fixture.CommandAsync(1), 2, old);
        var authority = new ActiveWorkAuthority(fixture.Authority, command);
        var invocation = new ProjectTaskPlanInvocation(fixture.Caller, authority, fixture.Authority);
        var accepted = Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Publisher().PublishAsync(command, invocation));
        Assert.False(accepted.Replayed);
        Assert.Equal(3, authority.Checks);
        Assert.Equal(ProjectTaskPlanLookupStatus.Found, (await fixture.LookupAsync(command.Operation)).Status);
        await fixture.VerifyAsync(1, 1, accepted.Acknowledgement);

        authority.Exhausted = true;
        var replay = Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Publisher().PublishAsync(command, invocation));
        Assert.True(replay.Replayed);
        Assert.Equal(3, authority.Checks);
        Assert.Equal(accepted.Acknowledgement.Tasks, replay.Acknowledgement.Tasks);
        await fixture.VerifyAsync(1, 1);

        var reinterpretation = Version(command, 1);
        Assert.Equal(ProjectTaskPlanRefusal.IdentityConflict,
            Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await fixture.Publisher().PublishAsync(reinterpretation, fixture.Invocation)).Reason);
        await fixture.VerifyAsync(1, 1);
    }

    [Fact]
    public async Task Version_two_without_live_work_authority_refuses_before_task_sql() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var command = Version(await fixture.CommandAsync(), 2);
        fixture.Probe.Reset();
        Assert.Equal(ProjectTaskPlanRefusal.SourceChanged,
            Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await fixture.Publisher().PublishAsync(command, fixture.Invocation)).Reason);
        Assert.DoesNotContain(fixture.Probe.Commands, sql => sql.Contains("INSERT INTO \"Workbench_ProjectObjects\"", StringComparison.Ordinal));
        await fixture.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Work_expiring_after_staging_rolls_back_tasks_bindings_and_receipt() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var command = Version(await fixture.CommandAsync(), 2);
        var authority = new ActiveWorkAuthority(fixture.Authority, command);
        var flushed = false;
        fixture.Faults.Save.AfterFlush = _ => {
            flushed = true;
            authority.Exhausted = true;
            return Task.CompletedTask;
        };
        var outcome = await fixture.Publisher().PublishAsync(command, new(fixture.Caller, authority, fixture.Authority));
        Assert.True(flushed);
        Assert.Equal(ProjectTaskPlanRefusal.BudgetElapsed, Assert.IsType<ProjectTaskPlanOutcome.Rejected>(outcome).Reason);
        await fixture.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Work_expiring_during_a_real_project_lock_wait_refuses_after_the_wait() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var command = Version(await fixture.CommandAsync(), 2);
        var authority = new ActiveWorkAuthority(fixture.Authority, command);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await using var blocker = await fixture.ContextAsync(deadline.Token);
        await using var held = await blocker.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, deadline.Token);
        var holderPid = await blocker.Database.SqlQuery<int>($"SELECT pg_backend_pid() AS \"Value\"").SingleAsync(deadline.Token);
        await SerializableMutationScope.AcquireRelationalScopeLocksAsync(blocker,
            [ProjectMutationScopeKeys.ForProject(fixture.Project.ProjectId)], deadline.Token);
        fixture.Probe.Reset();
        var publishing = fixture.Publisher().PublishAsync(command, new(fixture.Caller, authority, fixture.Authority), deadline.Token);
        ProjectTaskPlanOutcome outcome;
        try {
            await fixture.WaitForProjectWaiterAsync(holderPid, deadline.Token);
            authority.Exhausted = true;
        } finally {
            await held.RollbackAsync(CancellationToken.None);
            outcome = await publishing;
        }
        Assert.Equal(ProjectTaskPlanRefusal.BudgetElapsed, Assert.IsType<ProjectTaskPlanOutcome.Rejected>(outcome).Reason);
        Assert.DoesNotContain(fixture.Probe.Commands, sql => sql.Contains("INSERT INTO \"Workbench_ProjectObjects\"", StringComparison.Ordinal));
        await fixture.VerifyAsync(0, 0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Active_work_does_not_waive_material_currentness_or_wall_clock_basis_age(bool expiredBasis) {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var original = await fixture.CommandAsync(1);
        var basis = expiredBasis ? new ProjectTaskPlanBasis(original.Basis.Namespace, original.Basis.Project,
            original.Basis.CapturedAtUtc.AddHours(-1), original.Basis.Tasks) : original.Basis;
        var command = Version(original, 2, original.Source.DecisionStartedAtUtc.AddHours(-2), basis: basis);
        if (!expiredBasis) {
            Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Publisher().PublishAsync(await fixture.CommandAsync(1), fixture.Invocation));
        }
        var authority = new ActiveWorkAuthority(fixture.Authority, command);
        var refused = Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await fixture.Publisher()
            .PublishAsync(command, new(fixture.Caller, authority, fixture.Authority)));
        Assert.Equal(expiredBasis ? ProjectTaskPlanRefusal.BudgetElapsed : ProjectTaskPlanRefusal.BasisChanged, refused.Reason);
        Assert.Equal(!expiredBasis, refused.RequiresReview);
        await fixture.VerifyAsync(expiredBasis ? 0 : 1, expiredBasis ? 0 : 1);
    }

    [Fact]
    public async Task Replan_policy_is_supported_only_with_active_work_and_never_replans_inside_the_publisher() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var original = await fixture.CommandAsync(1);
        var policy = new DecisionDataPolicy(ReadBasisMode.StepSnapshot, PublicationFreshness.RequireCurrent, OnBasisChange.Replan,
            new(1, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5)));
        var publisher = fixture.Publisher(new(1, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10)));
        var legacy = Version(original, 1, policy: policy);
        Assert.Equal(ProjectTaskPlanRefusal.PolicyChanged,
            Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await publisher.PublishAsync(legacy, fixture.Invocation)).Reason);
        var command = Version(await fixture.CommandAsync(1), 2, policy: policy);
        var authority = new ActiveWorkAuthority(fixture.Authority, command);
        Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await publisher.PublishAsync(command, new(fixture.Caller, authority, fixture.Authority)));
        var stale = new ProjectTaskPlanCommand(new(command.Operation.Namespace, command.Operation.Epoch, Guid.NewGuid(), command.Operation.CallerBinding),
            command.Basis, command.Proposal, command.Policy, command.Source, schemaVersion: 2);
        var staleAuthority = new ActiveWorkAuthority(fixture.Authority, stale);
        var refused = Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await publisher.PublishAsync(stale,
            new(fixture.Caller, staleAuthority, fixture.Authority)));
        Assert.Equal(ProjectTaskPlanRefusal.BasisChanged, refused.Reason);
        Assert.False(refused.RequiresReview);
        await fixture.VerifyAsync(1, 1);
    }

    private static ProjectTaskPlanCommand Version(ProjectTaskPlanCommand original, int version, DateTimeOffset? started = null,
        DecisionDataPolicy? policy = null, ProjectTaskPlanBasis? basis = null)
        => new(original.Operation, basis ?? original.Basis, original.Proposal, policy ?? original.Policy,
            new(original.Source.DecisionId, original.Source.Generation, started ?? original.Source.DecisionStartedAtUtc,
                original.Source.Kind, version, original.Source.Fingerprint), schemaVersion: version);

    private sealed class ActiveWorkAuthority(TaskPlanAuthority inner, ProjectTaskPlanCommand original) : IProjectTaskPlanPublicationAuthority {
        private readonly ProjectTaskPlanCommand expected = original;
        internal int Checks { get; private set; }
        internal bool Exhausted { get; set; }
        public async Task<IProjectTaskPlanHeldAuthority> AcquireAsync(ProjectTaskPlanSource source, ProjectWriteAdmission target,
            CancellationToken cancellationToken) => new Held(this, await inner.AcquireAsync(source, target, cancellationToken));

        private sealed class Held(ActiveWorkAuthority owner, IProjectTaskPlanHeldAuthority authority) : IProjectTaskPlanActiveWorkAuthority {
            public ImmutableArray<ProjectWriteAdmission> AdditionalProjects => authority.AdditionalProjects;
            public Task RequireCurrentAsync(CancellationToken cancellationToken) => authority.RequireCurrentAsync(cancellationToken);
            public void RequireActiveWork(ProjectTaskPlanSource source, DecisionDataPolicy policy) {
                Assert.Equal(owner.expected.Source, source);
                Assert.Equal(owner.expected.Policy, policy);
                owner.Checks++;
                if (owner.Exhausted) {
                    throw new ProjectTaskPlanAuthorityException(ProjectTaskPlanRefusal.BudgetElapsed);
                }
            }
            public ValueTask DisposeAsync() => authority.DisposeAsync();
        }
    }
}
