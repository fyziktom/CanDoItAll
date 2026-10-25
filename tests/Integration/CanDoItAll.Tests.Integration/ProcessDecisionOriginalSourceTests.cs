using System.Data.Common;
using System.Text.Json;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionRunScopeFixture;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionDispatchAuthorityFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessDecisionOriginalSourceTests(Xunit.Abstractions.ITestOutputHelper output) {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot, 0)]
    [InlineData(ReadBasisMode.StepSnapshot, 0)]
    [InlineData(ReadBasisMode.RunSnapshot, 7)]
    [InlineData(ReadBasisMode.StepSnapshot, -7)]
    public async Task Registered_original_reader_preserves_source_bytes_but_never_dispatches(ReadBasisMode mode, int subMicrosecondTicks) {
        var clock = new ProcessDecisionPublicationTestClock();
        await using var app = await TestApplication.CreateAsync(clock.Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var context = Context(services);
        var fixture = await CreateAsync(context, services, mode, prepared: true,
            authority: await services.GetRequiredService<ProjectProcessLaunchAuthorityService>().CaptureLocalAsync(null, ProcessLaunchOperatorSurface.UserInterface),
            assignmentCreatedAt: subMicrosecondTicks == 0 ? null : new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(subMicrosecondTicks));
        var liveResult = await fixture.ReadAsync(Query(services, clock));
        var live = liveResult.Snapshot!;
        Assert.NotNull(liveResult.OriginalDecisionPolicy);
        Assert.True(live.ObservedCurrentDispatch);
        var binding = fixture.Original.Binding;
        var result = await services.GetRequiredService<IProcessOriginalReceiptAuthorityReader>()
            .ObserveOriginalDecisionForReceiptReadAsync(fixture.Evidence, binding, binding.ExecutionContract, fixture.Original.Material.ContentContract);
        AssertSameOriginal(live, result);
        Assert.Equal(liveResult.OriginalDecisionPolicy, result.OriginalDecisionPolicy);
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => services.GetRequiredService<IProcessOriginalReceiptAuthorityReader>()
            .ObserveOriginalDecisionForReceiptReadAsync(fixture.Evidence, binding with { Scope = binding.Scope with { DatabaseProfileId = Guid.NewGuid() } },
                binding.ExecutionContract, fixture.Original.Material.ContentContract));
    }

    [Theory]
    [InlineData(LaterChange.Executor)]
    [InlineData(LaterChange.Readiness)]
    [InlineData(LaterChange.Policy)]
    [InlineData(LaterChange.Operations)]
    [InlineData(LaterChange.Expired)]
    [InlineData(LaterChange.Released)]
    [InlineData(LaterChange.Cancelled)]
    [InlineData(LaterChange.Closed)]
    [InlineData(LaterChange.Superseded)]
    public async Task Original_evidence_survives_current_repairs_and_closed_or_superseded_work(LaterChange change) {
        var clock = new ProcessDecisionPublicationTestClock();
        await using var app = await TestApplication.CreateAsync(clock.Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var context = Context(services);
        var fixture = await CreateAsync(context, services, ReadBasisMode.StepSnapshot, prepared: true);
        var liveResult = await fixture.ReadAsync(Query(services, clock));
        var live = liveResult.Snapshot!;
        Assert.NotNull(liveResult.OriginalDecisionPolicy);
        if (change == LaterChange.Expired) {
            clock.SetUtcNow(fixture.State.Claims.Single().ExpiresAtUtc);
        } else if (change is LaterChange.Closed or LaterChange.Superseded) {
            var closed = await Owner(context, services).CommitAsync(await ClosureRequestAsync(context, fixture.State,
                fixture.Original.Enrolled.Decision!.Child!, NoEffect(), 7));
            if (change == LaterChange.Superseded) {
                await SupersedeAsync(context, services, closed.State, fixture.Original.Material);
            }
        } else {
            context.ChangeTracker.Clear();
            var assignment = await context.RuntimeStepAssignments.SingleAsync(item => item.RunId == fixture.State.RunId.Value);
            switch (change) {
                case LaterChange.Executor:
                    assignment.ExecutorId = Guid.NewGuid().ToString("D");
                    break;
                case LaterChange.Readiness:
                    assignment.ReadinessHash = "sha256:repaired-readiness";
                    break;
                case LaterChange.Policy:
                    var policy = ProcessDecisionDataPolicyAdmissionCodec.Deserialize(assignment.DecisionDataPolicyJson!);
                    var previous = policy.Policy;
                    DecisionDataPolicy revised = new(previous.ReadBasisMode, previous.PublicationFreshness, previous.OnBasisChange,
                        new(previous.Budget.MaximumReplans, previous.Budget.MaximumWorkDuration,
                            TimeSpan.FromTicks(previous.Budget.MaximumBasisAge.Ticks / 2)));
                    Assert.NotEqual(liveResult.OriginalDecisionPolicy, revised);
                    assignment.DecisionDataPolicyJson = ProcessDecisionDataPolicyAdmissionCodec.Serialize(policy with {
                        WorkspaceHeadRevision = Guid.NewGuid(), WorkspacePolicy = revised, Policy = revised,
                        DefinitionOverride = null, StepOverride = null, AuthorizedInvocationOverride = null
                    });
                    break;
                case LaterChange.Operations:
                    assignment.OperationTargetScope = "repaired-target-scope";
                    break;
                case LaterChange.Released:
                    (await context.DispatchClaims.SingleAsync(item => item.RunId == fixture.State.RunId.Value)).Status = DispatchClaimStatus.Released;
                    break;
                case LaterChange.Cancelled:
                    (await context.RuntimeStates.SingleAsync(item => item.RunId == fixture.State.RunId.Value)).Status = ProcessRuntimeStatus.CancelRequested;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(change));
            }
            await context.SaveChangesAsync();
        }
        var result = await ReadAsync(Query(services, clock), fixture);
        AssertSameOriginal(live, result);
        Assert.Equal(liveResult.OriginalDecisionPolicy, result.OriginalDecisionPolicy);
        if (change is LaterChange.Readiness or LaterChange.Policy or LaterChange.Operations) {
            await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => fixture.ReadAsync(Query(services, clock)));
        } else {
            Assert.NotEqual(true, (await fixture.ReadAsync(Query(services, clock))).Snapshot?.ObservedCurrentDispatch);
        }
    }

    [Theory]
    [InlineData(Substitution.Agent)]
    [InlineData(Substitution.StepKey)]
    [InlineData(Substitution.Root)]
    [InlineData(Substitution.Child)]
    [InlineData(Substitution.Grant)]
    [InlineData(Substitution.Profile)]
    [InlineData(Substitution.Namespace)]
    [InlineData(Substitution.Stamp)]
    [InlineData(Substitution.Material)]
    [InlineData(Substitution.ExecutionContract)]
    [InlineData(Substitution.ContentContract)]
    public async Task Original_read_rejects_cross_source_child_material_and_contract_substitution(Substitution change) {
        var clock = new ProcessDecisionPublicationTestClock();
        await using var app = await TestApplication.CreateAsync(clock.Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var context = Context(scope.ServiceProvider);
        var fixture = await CreateAsync(context, scope.ServiceProvider, prepared: true);
        var binding = fixture.Original.Binding;
        var other = Guid.NewGuid();
        binding = change switch {
            Substitution.Root => binding with { Child = binding.Child with { Address = binding.Child.Address with { RootRunId = new(other) } },
                Scope = binding.Scope with { RootRunId = new(other), OwnerRunId = new(other) } },
            Substitution.Child => binding with { Child = binding.Child with { Address = binding.Child.Address with { ChildId = new(other) } } },
            Substitution.Grant => binding with { Child = binding.Child with { Grant = binding.Child.Grant with { WorkTicks = binding.Child.Grant.WorkTicks + 1 } } },
            Substitution.Profile => binding with { Scope = binding.Scope with { DatabaseProfileId = other } },
            Substitution.Namespace => binding with { Scope = binding.Scope with { WorkspaceNamespaceId = other } },
            Substitution.Stamp => binding with { Material = binding.Material with { Fingerprint = new string('A', 64) } },
            Substitution.Material => binding with { Child = binding.Child with { MaterialId = new(other) },
                Material = binding.Material with { Reference = binding.Material.Reference with { MaterialId = new(other) } } },
            _ => binding
        };
        var evidence = change switch {
            Substitution.Agent => fixture.Evidence with { ExecutorAgentId = other },
            Substitution.StepKey => fixture.Evidence with { StepKey = "another-step" },
            Substitution.Child => fixture.Evidence with { ExecutionRunId = other },
            _ => fixture.Evidence
        };
        var execution = change == Substitution.ExecutionContract ? binding.ExecutionContract with { Version = 2 } : binding.ExecutionContract;
        var content = change == Substitution.ContentContract ? fixture.Original.Material.ContentContract! with { Version = 2 } : fixture.Original.Material.ContentContract;
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => Query(scope.ServiceProvider, clock)
            .ReadOriginalDecisionAsync(evidence, binding, execution, content));
    }

    [Theory]
    [InlineData(ClaimChange.Token)]
    [InlineData(ClaimChange.BeforeCreation)]
    [InlineData(ClaimChange.AtExpiry)]
    public async Task Historical_read_requires_the_original_claim_and_creation_interval(ClaimChange change) {
        var clock = new ProcessDecisionPublicationTestClock();
        await using var app = await TestApplication.CreateAsync(clock.Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var context = Context(scope.ServiceProvider);
        var fixture = await CreateAsync(context, scope.ServiceProvider, prepared: true);
        var claim = fixture.State.Claims.Single();
        var evidence = change switch {
            ClaimChange.Token => fixture.Evidence with { DispatchClaimToken = Guid.NewGuid() },
            ClaimChange.BeforeCreation => fixture.Evidence with { ExecutionCreatedAtUtc = claim.CreatedAtUtc.AddTicks(-1) },
            ClaimChange.AtExpiry => fixture.Evidence with { ExecutionCreatedAtUtc = claim.ExpiresAtUtc },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        var result = await ReadAsync(Query(scope.ServiceProvider, clock), fixture with { Evidence = evidence });
        Assert.Equal(ProcessExecutionAuthorityDisposition.DispatchBindingChanged, result.Disposition);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public async Task Current_assignment_cannot_substitute_for_a_missing_accepted_admission() {
        var clock = new ProcessDecisionPublicationTestClock();
        await using var app = await TestApplication.CreateAsync(clock.Harness());
        await using var scope = app.Services.CreateAsyncScope();
        await using var context = Context(scope.ServiceProvider);
        var fixture = await CreateAsync(context, scope.ServiceProvider);
        Assert.True((await fixture.ReadAsync(Query(scope.ServiceProvider, clock))).Snapshot!.ObservedCurrentDispatch);
        await Assert.ThrowsAsync<ProcessExecutionAuthorityMismatchException>(() => ReadAsync(Query(scope.ServiceProvider, clock), fixture));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12)]
    public async Task Original_read_has_four_bounded_queries_without_current_assignment_or_material_payload(int siblings) {
        var clock = new ProcessDecisionPublicationTestClock();
        await using var app = await TestApplication.CreateAsync(clock.Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await using var context = Context(services);
        var fixture = await CreateAsync(context, services, prepared: true);
        var state = fixture.State;
        for (var index = 0; index < siblings; index++) {
            state = (await Owner(context, services).CommitAsync(await EnrollmentRequestAsync(context, state))).State;
        }
        var probe = new ReadProbe();
        var result = await ReadAsync(Query(services, clock, probe), fixture);
        Assert.False(result.Snapshot!.ObservedCurrentDispatch);
        Assert.Equal(4, probe.Commands.Count);
        Assert.Equal(0, probe.Writes);
        Assert.Single(probe.Commands, query => query.Sql.Contains("process_prepared_launches", StringComparison.Ordinal));
        await context.Database.OpenConnectionAsync();
        foreach (var query in probe.Commands) {
            Assert.StartsWith("SELECT", query.Sql.TrimStart(), StringComparison.Ordinal);
            Assert.Contains("WHERE", query.Sql, StringComparison.Ordinal);
            Assert.Contains("LIMIT 2", query.Sql, StringComparison.Ordinal);
            if (!query.Sql.Contains("process_prepared_launches", StringComparison.Ordinal)) {
                Assert.DoesNotContain("PayloadJson", query.Sql, StringComparison.Ordinal);
            }
            Assert.DoesNotContain("RuntimeStepAssignments", query.Sql, StringComparison.Ordinal);
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) " + query.Sql;
            command.CommandTimeout = 5;
            command.Parameters.AddRange(query.Parameters);
            var plan = Assert.IsType<string>(await command.ExecuteScalarAsync());
            output.WriteLine(plan);
            using var parsed = JsonDocument.Parse(plan);
            Assert.Equal(1d, parsed.RootElement[0].GetProperty("Plan").GetProperty("Actual Rows").GetDouble());
        }
    }

    private static void AssertSameOriginal(ProcessExecutionDispatchAuthority original, ProcessExecutionDispatchAuthorityResult result) {
        Assert.Equal(ProcessExecutionAuthorityDisposition.Bound, result.Disposition);
        var actual = Assert.IsType<ProcessExecutionDispatchAuthority>(result.Snapshot);
        Assert.False(actual.ObservedCurrentDispatch);
        Assert.Equal(JsonSerializer.Serialize(original with { ObservedCurrentDispatch = false, ObservedAtUtc = actual.ObservedAtUtc }),
            JsonSerializer.Serialize(actual));
    }

    private static Task<ProcessExecutionDispatchAuthorityResult> ReadAsync(EfProcessExecutionAuthorityQuery query, ProcessDecisionDispatchAuthorityFixture fixture)
        => query.ReadOriginalDecisionAsync(fixture.Evidence, fixture.Original.Binding,
            fixture.Original.Binding.ExecutionContract, fixture.Original.Material.ContentContract);

    private static async Task SupersedeAsync(ProcessPersistenceDbContext context, IServiceProvider services,
        ProcessRuntimeStateSnapshot state, ProcessDecisionMaterial original) {
        var reconciling = await QuiesceAsync(context, services, state);
        var budget = await BudgetAsync(context, reconciling.State);
        var reserved = await Owner(context, services).CommitAsync(Request(reconciling.State, null,
            new ProcessDecisionBudgetReservation(new(Step(state), budget.BudgetId, budget.Revision), new(Guid.NewGuid()), new(100, 0))));
        var current = await DecisionAsync(context, reserved.State);
        var balance = reserved.DecisionBudget!.Change.Balance;
        var material = ProcessDecisionMaterial.FromContent(original.Scope,
            original.Reference with { Generation = original.Reference.Generation + 1, MaterialId = new(Guid.NewGuid()) },
            original.ContentContract!, original.CapturedAtUtc.AddSeconds(1), "{\"facts\":[\"refreshed\"]}");
        await Owner(context, services).CommitAsync(Request(reserved.State,
            new ProcessDecisionRefresh(Step(state), current.Expectation, material.Reference.MaterialId, ProcessDecisionRefreshKind.ManagerRefresh) { Material = material.Stamp },
            new ProcessDecisionBudgetSettlement(new(Step(state), balance.BudgetId, balance.Revision), reserved.DecisionBudget.Command.AllocationId,
                new(2, new(5, 0)))) with { DecisionMaterial = material });
    }

    public enum LaterChange { Executor, Readiness, Policy, Operations, Expired, Released, Cancelled, Closed, Superseded }
    public enum Substitution { Agent, StepKey, Root, Child, Grant, Profile, Namespace, Stamp, Material, ExecutionContract, ContentContract }
    public enum ClaimChange { Token, BeforeCreation, AtExpiry }

    private sealed record ReadCommand(string Sql, NpgsqlParameter[] Parameters);

    private sealed class ReadProbe : DbCommandInterceptor {
        internal List<ReadCommand> Commands { get; } = [];
        internal int Writes { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Commands.Add(new(command.CommandText, command.Parameters.Cast<NpgsqlParameter>()
                .Select(parameter => (NpgsqlParameter)((ICloneable)parameter).Clone()).ToArray()));
            return ValueTask.FromResult(result);
        }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            Writes++;
            return ValueTask.FromResult(result);
        }
    }
}
