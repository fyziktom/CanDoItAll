using System.Data.Common;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionRunScopeFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionDispatchAuthorityFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessDecisionDispatchAuthorityTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot, false, false)]
    [InlineData(ReadBasisMode.RunSnapshot, true, false)]
    [InlineData(ReadBasisMode.StepSnapshot, false, false)]
    [InlineData(ReadBasisMode.RunSnapshot, false, true)]
    [InlineData(ReadBasisMode.StepSnapshot, false, true)]
    public async Task Selected_exact_child_dispatch_keeps_legacy_reservation_closed(ReadBasisMode mode, bool descendant, bool prepared) {
        var clock = new ProcessDecisionPublicationTestClock();
        await using var app = await TestApplication.CreateAsync(clock.Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var context = Context(scope.ServiceProvider);
        var fixture = await CreateAsync(context, scope.ServiceProvider, mode, descendant, prepared);
        var query = Query(scope.ServiceProvider, clock);
        var legacy = Assert.IsType<ProcessExecutionDispatchAuthority>((await query.ReadDispatchAsync(fixture.Evidence)).Snapshot);
        var selected = Assert.IsType<ProcessExecutionDispatchAuthority>((await fixture.ReadAsync(query)).Snapshot);
        Assert.False(legacy.ObservedCurrentDispatch);
        Assert.True(selected.ObservedCurrentDispatch);
        Assert.Equal(fixture.Evidence, selected.Evidence);
        Assert.Equal(fixture.Original.Binding.Child.Address.RootRunId, selected.RootRunId);
        Assert.NotEqual(legacy.OwnerFingerprint, selected.OwnerFingerprint);
        Assert.Equal(legacy.OwnerFingerprint, (await query.ReadDispatchAsync(fixture.Evidence)).Snapshot!.OwnerFingerprint);
        Assert.Equal(selected.OwnerFingerprint, (await fixture.ReadAsync(Query(scope.ServiceProvider, clock))).Snapshot!.OwnerFingerprint);
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => query.ReadReservationAsync(fixture.Evidence));
    }

    [Theory]
    [InlineData(ClaimSubstitution.Agent)]
    [InlineData(ClaimSubstitution.Claim)]
    [InlineData(ClaimSubstitution.BeforeCreation)]
    [InlineData(ClaimSubstitution.AtExpiry)]
    public async Task Selected_dispatch_still_requires_original_Agent_claim_and_creation_interval(ClaimSubstitution change) {
        var clock = new ProcessDecisionPublicationTestClock();
        await using var app = await TestApplication.CreateAsync(clock.Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var context = Context(scope.ServiceProvider);
        var fixture = await CreateAsync(context, scope.ServiceProvider);
        var claim = fixture.State.Claims.Single();
        var evidence = change switch {
            ClaimSubstitution.Agent => fixture.Evidence with { ExecutorAgentId = Guid.NewGuid() },
            ClaimSubstitution.Claim => fixture.Evidence with { DispatchClaimToken = Guid.NewGuid() },
            ClaimSubstitution.BeforeCreation => fixture.Evidence with { ExecutionCreatedAtUtc = claim.CreatedAtUtc.AddTicks(-1) },
            ClaimSubstitution.AtExpiry => fixture.Evidence with { ExecutionCreatedAtUtc = claim.ExpiresAtUtc },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        var result = await (fixture with { Evidence = evidence }).ReadAsync(Query(scope.ServiceProvider, clock));
        Assert.Equal(ProcessExecutionAuthorityDisposition.DispatchBindingChanged, result.Disposition);
        Assert.Null(result.Snapshot);
    }

    [Theory]
    [InlineData(BindingSubstitution.Execution)]
    [InlineData(BindingSubstitution.Root)]
    [InlineData(BindingSubstitution.Child)]
    [InlineData(BindingSubstitution.Grant)]
    [InlineData(BindingSubstitution.Profile)]
    [InlineData(BindingSubstitution.Namespace)]
    [InlineData(BindingSubstitution.Stamp)]
    [InlineData(BindingSubstitution.Material)]
    [InlineData(BindingSubstitution.Contract)]
    [InlineData(BindingSubstitution.SupportedExecution)]
    [InlineData(BindingSubstitution.SupportedContent)]
    public async Task Selected_dispatch_refuses_substituted_child_grant_scope_stamp_or_contract(BindingSubstitution change) {
        var clock = new ProcessDecisionPublicationTestClock();
        await using var app = await TestApplication.CreateAsync(clock.Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var context = Context(scope.ServiceProvider);
        var fixture = await CreateAsync(context, scope.ServiceProvider);
        var original = fixture.Original.Binding;
        var other = Guid.NewGuid();
        var binding = change switch {
            BindingSubstitution.Root => original with {
                Child = original.Child with { Address = original.Child.Address with { RootRunId = new(other) } },
                Scope = original.Scope with { RootRunId = new(other), OwnerRunId = new(other) }
            },
            BindingSubstitution.Child => original with { Child = original.Child with { Address = original.Child.Address with { ChildId = new(other) } } },
            BindingSubstitution.Grant => original with { Child = original.Child with { Grant = original.Child.Grant with { WorkTicks = original.Child.Grant.WorkTicks + 1 } } },
            BindingSubstitution.Profile => original with { Scope = original.Scope with { DatabaseProfileId = other } },
            BindingSubstitution.Namespace => original with { Scope = original.Scope with { WorkspaceNamespaceId = other } },
            BindingSubstitution.Stamp => original with { Material = original.Material with { Fingerprint = new string('A', 64) } },
            BindingSubstitution.Material => original with { Child = original.Child with { MaterialId = new(other) },
                Material = original.Material with { Reference = original.Material.Reference with { MaterialId = new(other) } } },
            BindingSubstitution.Contract => original with { ExecutionContract = original.ExecutionContract with { Version = 2 } },
            _ => original
        };
        var evidence = change switch {
            BindingSubstitution.Execution => fixture.Evidence with { ExecutionRunId = other },
            BindingSubstitution.Child => fixture.Evidence with { ExecutionRunId = other },
            _ => fixture.Evidence
        };
        var execution = change == BindingSubstitution.SupportedExecution ? original.ExecutionContract with { Version = 2 } : original.ExecutionContract;
        var content = change == BindingSubstitution.SupportedContent ? fixture.Original.Material.ContentContract! with { Version = 2 } : fixture.Original.Material.ContentContract;
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => Query(scope.ServiceProvider, clock)
            .ReadDecisionDispatchAsync(evidence, binding, execution, content));
        Assert.True((await fixture.ReadAsync(Query(scope.ServiceProvider, clock))).Snapshot!.ObservedCurrentDispatch);
    }

    [Theory]
    [InlineData(CurrentChange.Expired)]
    [InlineData(CurrentChange.ReplacedClaim)]
    [InlineData(CurrentChange.Attempt)]
    [InlineData(CurrentChange.ReleasedClaim)]
    [InlineData(CurrentChange.CancelRequested)]
    [InlineData(CurrentChange.WaitingRun)]
    [InlineData(CurrentChange.CompletedStep)]
    [InlineData(CurrentChange.WaitingReview)]
    [InlineData(CurrentChange.ParentReconciliation)]
    [InlineData(CurrentChange.ClosedChild)]
    [InlineData(CurrentChange.TerminalExecution)]
    public async Task Current_revocation_preserves_the_original_read_and_fingerprint(CurrentChange change) {
        var clock = new ProcessDecisionPublicationTestClock();
        await using var app = await TestApplication.CreateAsync(clock.Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var context = Context(services);
        var fixture = await CreateAsync(context, services);
        var original = (await fixture.ReadAsync(Query(services, clock))).Snapshot!;
        if (change == CurrentChange.Expired) {
            clock.SetUtcNow(fixture.State.Claims.Single().ExpiresAtUtc);
        } else if (change == CurrentChange.TerminalExecution) {
            fixture = fixture with { Evidence = fixture.Evidence with { ExecutionMayDispatch = false } };
        } else if (change == CurrentChange.WaitingReview) {
            await QuiesceAsync(context, services, fixture.State, ProcessDecisionPhaseChange.WaitForReview);
        } else if (change == CurrentChange.ParentReconciliation) {
            var runScope = (await Scopes(context, services).ReadAsync(fixture.Original.Binding.Scope.Id))!;
            await Owner(context, services).CommitAsync(ScopeRequest(fixture.State,
                new(runScope.Expectation, ProcessDecisionRunScopeCommandKind.BeginReconciliation)));
        } else if (change == CurrentChange.ClosedChild) {
            await Owner(context, services).CommitAsync(await ClosureRequestAsync(context, fixture.State,
                fixture.Original.Enrolled.Decision!.Child!, NoEffect(), 7));
        } else {
            context.ChangeTracker.Clear();
            var state = await context.RuntimeStates.SingleAsync(item => item.RunId == fixture.State.RunId.Value);
            var step = await context.RuntimeSteps.SingleAsync(item => item.RunId == fixture.State.RunId.Value);
            var claim = await context.DispatchClaims.SingleAsync(item => item.RunId == fixture.State.RunId.Value);
            switch (change) {
                case CurrentChange.ReplacedClaim:
                    step.ActiveClaimToken = Guid.NewGuid();
                    break;
                case CurrentChange.Attempt:
                    step.AttemptNumber++;
                    break;
                case CurrentChange.ReleasedClaim:
                    claim.Status = DispatchClaimStatus.Released;
                    break;
                case CurrentChange.CancelRequested:
                    state.Status = ProcessRuntimeStatus.CancelRequested;
                    break;
                case CurrentChange.WaitingRun:
                    state.Status = ProcessRuntimeStatus.Waiting;
                    break;
                case CurrentChange.CompletedStep:
                    step.Status = ProcessRuntimeStepStatus.Completed;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(change));
            }
            await context.SaveChangesAsync();
        }
        var result = await fixture.ReadAsync(Query(services, clock));
        Assert.Equal(ProcessExecutionAuthorityDisposition.Bound, result.Disposition);
        var current = Assert.IsType<ProcessExecutionDispatchAuthority>(result.Snapshot);
        Assert.False(current.ObservedCurrentDispatch);
        Assert.Equal(original.OwnerFingerprint, current.OwnerFingerprint);
    }

    [Theory]
    [InlineData(AdmissionChange.Policy)]
    [InlineData(AdmissionChange.Agent)]
    [InlineData(AdmissionChange.Readiness)]
    public async Task Prepared_launch_retains_the_original_policy_Agent_and_readiness(AdmissionChange change) {
        var clock = new ProcessDecisionPublicationTestClock();
        await using var app = await TestApplication.CreateAsync(clock.Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var context = Context(scope.ServiceProvider);
        var fixture = await CreateAsync(context, scope.ServiceProvider, prepared: true);
        Assert.True((await fixture.ReadAsync(Query(scope.ServiceProvider, clock))).Snapshot!.ObservedCurrentDispatch);
        context.ChangeTracker.Clear();
        var assignment = await context.RuntimeStepAssignments.SingleAsync(item => item.RunId == fixture.State.RunId.Value);
        if (change == AdmissionChange.Policy) {
            var policy = ProcessDecisionDataPolicyAdmissionCodec.Deserialize(assignment.DecisionDataPolicyJson!);
            assignment.DecisionDataPolicyJson = ProcessDecisionDataPolicyAdmissionCodec.Serialize(policy with { WorkspaceHeadRevision = Guid.NewGuid() });
        } else if (change == AdmissionChange.Agent) {
            var replacement = Guid.NewGuid();
            assignment.ExecutorId = replacement.ToString("D");
            fixture = fixture with { Evidence = fixture.Evidence with { ExecutorAgentId = replacement } };
        } else {
            assignment.ReadinessHash = "sha256:replaced-readiness";
        }
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => fixture.ReadAsync(Query(scope.ServiceProvider, clock)));
        Assert.False((await Query(scope.ServiceProvider, clock).ReadDispatchAsync(fixture.Evidence)).Snapshot!.ObservedCurrentDispatch);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12)]
    public async Task Selected_observation_has_three_exact_reads_without_material_payload_or_sibling_scans(int siblings) {
        var clock = new ProcessDecisionPublicationTestClock();
        await using var app = await TestApplication.CreateAsync(clock.Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var context = Context(services);
        var fixture = await CreateAsync(context, services);
        var state = fixture.State;
        for (var index = 0; index < siblings; index++) {
            state = (await Owner(context, services).CommitAsync(await EnrollmentRequestAsync(context, state))).State;
        }
        var probe = new ReadProbe();
        var result = await fixture.ReadAsync(Query(services, clock, probe));
        Assert.True(result.Snapshot!.ObservedCurrentDispatch);
        Assert.Equal(3, probe.Commands.Count);
        Assert.All(probe.Commands, command => {
            Assert.StartsWith("SELECT", command.TrimStart(), StringComparison.Ordinal);
            Assert.Contains("WHERE", command, StringComparison.Ordinal);
            Assert.Contains("LIMIT 2", command, StringComparison.Ordinal);
            Assert.DoesNotContain("PayloadJson", command, StringComparison.Ordinal);
        });
        Assert.Equal(0, probe.Writes);
    }

    [Fact]
    public async Task Selected_observation_cannot_authorize_the_legacy_mutation_guard() {
        var clock = new ProcessDecisionPublicationTestClock();
        await using var app = await TestApplication.CreateAsync(clock.Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var context = Context(services);
        var fixture = await CreateAsync(context, services);
        var selected = (await fixture.ReadAsync(Query(services, clock))).Snapshot!;
        var transactions = services.GetRequiredService<CoordinatedDatabaseTransaction>();
        var guard = new EfProcessExecutionMutationGuard(Options(services), transactions, clock);
        await using var transaction = await context.Database.BeginTransactionAsync();
        using var participation = transactions.Enter(context);
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => guard.RequireDispatchAsync(selected));
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => guard.RequireForMutationAsync(selected));
    }

    [Fact]
    public async Task Host_restart_resolves_the_same_selected_source_without_new_material_or_grants() {
        await using var environment = CanDoItAllTestEnvironment.Create("process-decision-publication");
        var profile = environment.CreatePostgreSqlProfile("selected-child-restart");
        var clock = new ProcessDecisionPublicationTestClock();
        var harness = clock.Harness(environment, profile);
        ProcessDecisionDispatchAuthorityFixture fixture;
        string fingerprint;
        int allocations;
        await using (var app = await TestApplication.CreateAsync(harness)) {
            await using var scope = app.Services.CreateAsyncScope();
            await using var context = Context(scope.ServiceProvider);
            fixture = await CreateAsync(context, scope.ServiceProvider, prepared: true);
            fingerprint = (await fixture.ReadAsync(Query(scope.ServiceProvider, clock))).Snapshot!.OwnerFingerprint;
            allocations = await context.DecisionBudgetAllocations.CountAsync();
        }
        await using var restarted = await TestApplication.CreateAsync(harness);
        await using var reopened = restarted.Services.CreateAsyncScope();
        var retained = (await fixture.ReadAsync(Query(reopened.ServiceProvider, clock))).Snapshot!;
        Assert.True(retained.ObservedCurrentDispatch);
        Assert.Equal(fingerprint, retained.OwnerFingerprint);
        await using var read = Context(reopened.ServiceProvider);
        Assert.Equal(allocations, await read.DecisionBudgetAllocations.CountAsync());
        Assert.Single(await read.DecisionMaterials.ToArrayAsync());
    }

    public enum ClaimSubstitution { Agent, Claim, BeforeCreation, AtExpiry }
    public enum BindingSubstitution { Execution, Root, Child, Grant, Profile, Namespace, Stamp, Material, Contract, SupportedExecution, SupportedContent }
    public enum CurrentChange { Expired, ReplacedClaim, Attempt, ReleasedClaim, CancelRequested, WaitingRun, CompletedStep, WaitingReview,
        ParentReconciliation, ClosedChild, TerminalExecution }
    public enum AdmissionChange { Policy, Agent, Readiness }

    [Fact]
    public async Task Lease_expiry_during_the_last_metadata_read_is_observed_before_authorization_returns() {
        var clock = new ProcessDecisionPublicationTestClock();
        await using var app = await TestApplication.CreateAsync(clock.Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var context = Context(scope.ServiceProvider);
        var fixture = await CreateAsync(context, scope.ServiceProvider);
        var expires = fixture.State.Claims.Single().ExpiresAtUtc;
        var probe = new ReadProbe(count => {
            if (count == 3) {
                clock.SetUtcNow(expires);
            }
        });
        var result = (await fixture.ReadAsync(Query(scope.ServiceProvider, clock, probe))).Snapshot!;
        Assert.Equal(3, probe.Commands.Count);
        Assert.False(result.ObservedCurrentDispatch);
        Assert.Equal(expires, result.ObservedAtUtc);
    }

    private sealed class ReadProbe(Action<int>? beforeRead = null) : DbCommandInterceptor {
        internal List<string> Commands { get; } = [];
        internal int Writes { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Commands.Add(command.CommandText);
            beforeRead?.Invoke(Commands.Count);
            return ValueTask.FromResult(result);
        }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            Writes++;
            return ValueTask.FromResult(result);
        }
    }
}
