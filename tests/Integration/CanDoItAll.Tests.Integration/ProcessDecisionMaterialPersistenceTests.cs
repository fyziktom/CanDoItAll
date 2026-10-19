using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using static CanDoItAll.Tests.Integration.Processes.ProcessDecisionPersistenceFixture;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessDecisionMaterialPersistenceTests {
    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task Canonical_material_preserves_exact_bytes_ticks_and_a_small_versioned_receipt(ReadBasisMode mode) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var request = await OriginalAsync(context, app.Services, mode);
        if (mode == ReadBasisMode.StepSnapshot) {
            var large = Copy(request.DecisionMaterial!, payload: "{\"note\":\"" + new string('a', ProcessDecisionMaterial.MaximumPayloadUtf8Bytes - 11) + "\"}");
            Assert.Equal(ProcessDecisionMaterial.MaximumPayloadUtf8Bytes, large.PayloadUtf8Bytes);
            request = request with { DecisionMaterial = large, Decision = request.Decision! with { Material = large.Stamp } };
        }
        var result = await Owner(context, app.Services).CommitAsync(request);
        var original = request.DecisionMaterial!;
        Assert.NotEqual(0, original.CapturedAtUtc.Ticks % 10);
        var observed = await Reader(context, app.Services).ReadAsync(original.Scope, original.Reference);
        Assert.NotNull(observed);
        original.RequireOriginal(observed);
        Assert.Equal(original.Stamp, result.Decision!.Command.Material);
        Assert.Equal(original.Reference.MaterialId.Value, (await context.DecisionGenerations.SingleAsync()).CanonicalMaterialId);
        Assert.Equal(mode == ReadBasisMode.RunSnapshot, (await context.DecisionMaterialScopes.SingleAsync()).StepInstanceId is null);
        var receipt = (await context.IdempotencyKeys.SingleAsync(item => item.CommandId == request.CommandId.Value)).DecisionReceiptJson!;
        using var json = JsonDocument.Parse(receipt);
        Assert.Equal(2, json.RootElement.GetProperty("version").GetInt32());
        Assert.DoesNotContain("payloadJson", receipt, StringComparison.Ordinal);
        Assert.True(receipt.Length < 4096);
        using var serviceScope = app.Services.CreateScope();
        var composed = serviceScope.ServiceProvider.GetRequiredService<IProcessDecisionMaterialReader>();
        original.RequireOriginal((await composed.ReadAsync(original.Scope, original.Reference))!);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task Retry_cannot_change_original_material_or_drop_its_command_binding(int change) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var request = await OriginalAsync(context, app.Services);
        var owner = Owner(context, app.Services);
        var first = await owner.CommitAsync(request);
        var original = request.DecisionMaterial!;
        var changed = change switch {
            0 => Copy(original, payload: "{\"tasks\":{}}"),
            1 => Copy(original, captured: original.CapturedAtUtc.AddTicks(1)),
            2 => Copy(original, scope: original.Scope with { WorkspaceNamespaceId = Guid.NewGuid() }),
            3 => Copy(original, reference: original.Reference with { Generation = 2 }),
            4 => Copy(original, contract: original.ExecutionContract with { Version = 2 }),
            _ => original
        };
        var retry = request with { DecisionMaterial = change == 5 ? null : changed,
            Decision = request.Decision! with { Material = change == 6 ? null : changed.Stamp } };
        var failure = await Record.ExceptionAsync(() => owner.CommitAsync(retry));
        Assert.True(failure is InvalidDataException or InvalidOperationException, failure?.ToString());
        Assert.Equal(first.State.UpdatedAtUtc, (await owner.LoadAsync(first.State.RunId))!.UpdatedAtUtc);
        original.RequireOriginal((await Reader(context, app.Services).ReadAsync(original.Scope, original.Reference))!);
        Assert.Equal(1, await context.DecisionMaterials.CountAsync());
        Assert.Equal(first.Decision, (await owner.CommitAsync(request)).Decision);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public async Task Material_must_match_original_profile_namespace_runtime_scope_and_contract(int change) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var request = await OriginalAsync(context, app.Services);
        var original = request.DecisionMaterial!;
        var scope = change switch {
            0 => original.Scope with { DatabaseProfileId = Guid.NewGuid() },
            1 => original.Scope with { WorkspaceNamespaceId = Guid.NewGuid() },
            2 => original.Scope with { RootRunId = new(Guid.NewGuid()) },
            3 => original.Scope with { OwnerRunId = new(Guid.NewGuid()) },
            4 => original.Scope with { StepInstanceId = new(Guid.NewGuid()) },
            5 => original.Scope with { StepInstanceId = null },
            _ => original.Scope
        };
        var changed = Copy(original, scope: scope, contract: change == 6 ? original.ExecutionContract with { Version = 2 } : null,
            reference: change == 7 ? original.Reference with { Generation = 2 } : null);
        var invalid = request with { DecisionMaterial = changed, Decision = request.Decision! with { Material = changed.Stamp } };
        await Assert.ThrowsAsync<InvalidDataException>(() => Owner(context, app.Services).CommitAsync(invalid));
        Assert.Empty(await context.DecisionMaterials.ToArrayAsync());
        Assert.Empty(await context.DecisionMaterialScopes.ToArrayAsync());
        Assert.Empty(await context.DecisionHeads.ToArrayAsync());
        Assert.Equal(request.OriginalState.UpdatedAtUtc, (await Owner(context, app.Services).LoadAsync(request.OriginalState.RunId))!.UpdatedAtUtc);
    }

    [Fact]
    public async Task Restart_and_refresh_keep_original_receipt_and_material_separate_from_the_new_generation() {
        await using var environment = CanDoItAllTestEnvironment.Create("process-decision-generation");
        var harness = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = environment.CreatePostgreSqlProfile("material-restart") };
        ProcessRuntimeCommitRequest original;
        ProcessDecisionReceipt receipt;
        await using (var app = await TestApplication.CreateAsync(harness)) {
            await using var context = Context(app.Services);
            original = await OriginalAsync(context, app.Services);
            var started = await Owner(context, app.Services).CommitAsync(original);
            receipt = started.Decision!;
            var refreshed = await Owner(context, app.Services).CommitAsync(await RefreshRequestAsync(context, started.State, original.DecisionMaterial!));
            Assert.Equal(2, refreshed.Decision!.Current.Reference.Generation);
            Assert.Equal(2, await context.DecisionMaterials.CountAsync());
        }
        await using var restarted = await TestApplication.CreateAsync(harness);
        await using var reopened = Context(restarted.Services);
        var replay = await Owner(reopened, restarted.Services).CommitAsync(original);
        Assert.Equal(receipt, replay.Decision);
        var material = original.DecisionMaterial!;
        material.RequireOriginal((await Reader(reopened, restarted.Services).ReadAsync(material.Scope, material.Reference))!);
        Assert.Equal(2, (await DecisionAsync(reopened, replay.State)).Reference.Generation);
        Assert.Equal(2, await reopened.DecisionMaterials.CountAsync());
    }

    [Fact]
    public async Task Failed_runtime_flush_rolls_back_material_scope_generation_budget_and_receipt() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var request = await OriginalAsync(context, app.Services);
        var balance = await BudgetAsync(context, request.OriginalState);
        var fault = new AfterMaterialFlush();
        await using (var broken = Context(app.Services, fault)) {
            Assert.Same(fault.Failure, await Assert.ThrowsAsync<InvalidOperationException>(() => Owner(broken, app.Services).CommitAsync(request)));
        }
        Assert.True(fault.Flushed);
        Assert.Empty(await context.DecisionMaterials.ToArrayAsync());
        Assert.Empty(await context.DecisionMaterialScopes.ToArrayAsync());
        Assert.Empty(await context.DecisionHeads.ToArrayAsync());
        Assert.Equal(balance, await BudgetAsync(context, request.OriginalState));
        Assert.Null(await Owner(context, app.Services).FindCompletedCommandAsync(request.CommandId));
        Assert.True((await Owner(context, app.Services).CommitAsync(request)).Succeeded);
    }

    [Fact]
    public async Task Racing_same_command_keeps_one_material_and_one_capture_settlement() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var request = await OriginalAsync(context, app.Services);
        await using var other = Context(app.Services);
        var results = await Task.WhenAll(Owner(context, app.Services).CommitAsync(request), Owner(other, app.Services).CommitAsync(request));
        Assert.Equal(results[0].Decision, results[1].Decision);
        Assert.Equal(1, await context.DecisionMaterialScopes.CountAsync());
        Assert.Equal(1, await context.DecisionMaterials.CountAsync());
        Assert.Equal(5, (await BudgetAsync(context, results[0].State)).ConsumedWorkTicks);
    }

    [Fact]
    public async Task Sibling_decisions_reuse_one_run_snapshot_without_copying_its_payload() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var owner = Owner(context, app.Services);
        var initial = ProcessProjectAdmissionFixture.Initial();
        var originalPlan = initial.InitialPlan!;
        var secondId = ProcessStepInstanceId.New();
        var plan = originalPlan with { Steps = [originalPlan.Steps.Single(), originalPlan.Steps.Single() with {
            StepInstanceId = secondId, StepKey = "second", StepDefinitionId = ProcessStepDefinitionId.New()
        }] };
        plan = plan with { PlanHash = ProcessPlanHasher.Compute(plan) };
        var state = initial.OriginalState with { PlanHash = plan.PlanHash, Steps = [initial.OriginalState.Steps.Single(),
            initial.OriginalState.Steps.Single() with { StepInstanceId = secondId, StepDefinitionId = plan.Steps[1].StepDefinitionId }] };
        var assignment = initial.InitialAssignments!.Single();
        var assignments = new[] { assignment, assignment with { StepInstanceId = secondId, StepKey = "second" } }
            .Select(item => item with { DecisionDataPolicy = ProcessDecisionPolicyFixture.Create(plan.Definition.VersionId, item.StepInstanceId) with {
                ExecutionContract = Contract(), MaterialProtocol = ProcessDecisionMaterialProtocol.CanonicalSnapshotV1
            } }).ToArray();
        state = (await owner.CommitAsync(initial with { InitialPlan = plan, InitialAssignments = assignments, OriginalState = state,
            Mutation = initial.Mutation with { State = state } })).State;
        var scope = new ProcessDecisionMaterialScope(new(Guid.NewGuid()), Database(app.Services).Profile.Profile.Id,
            assignments[0].DecisionDataPolicy!.WorkspaceNamespaceId, state.RootRunId, state.RunId, null);
        var material = new ProcessDecisionMaterial(scope, new(scope.Id, 1, new(Guid.NewGuid())), Contract(), ProcessProjectAdmissionFixture.Now, "{\"tasks\":[]}");
        foreach (var step in state.Steps) {
            var budget = (await new EfProcessDecisionBudgetStore(context).ReadAsync(state.RunId, step.StepInstanceId))!;
            var allocation = new DecisionBudgetAllocationId(Guid.NewGuid());
            var reserved = await owner.CommitAsync(Change(state) with {
                DecisionBudget = new ProcessDecisionBudgetReservation(new(step.StepInstanceId, budget.BudgetId, budget.Revision), allocation, new(10, 0))
            });
            state = (await owner.CommitAsync(Change(reserved.State) with {
                Decision = new ProcessDecisionStart(step.StepInstanceId, new(Guid.NewGuid()), material.Reference.MaterialId) { Material = material.Stamp },
                DecisionMaterial = material,
                DecisionBudget = new ProcessDecisionBudgetSettlement(new(step.StepInstanceId, budget.BudgetId, reserved.DecisionBudget!.Change.Balance.Revision),
                    allocation, new(1, new(5, 0)))
            })).State;
        }
        Assert.Equal(2, await context.DecisionHeads.CountAsync());
        Assert.Equal(1, await context.DecisionMaterialScopes.CountAsync());
        Assert.Equal(1, await context.DecisionMaterials.CountAsync());
        Assert.All(await context.DecisionGenerations.ToArrayAsync(), item => Assert.Equal(material.Reference.MaterialId.Value, item.CanonicalMaterialId));

        static ProcessRuntimeCommitRequest Change(ProcessRuntimeStateSnapshot current)
            => new(RuntimeCommandId.New(), current, new(ProcessRuntimeTransitionOutcome.Applied,
                current with { UpdatedAtUtc = current.UpdatedAtUtc.AddSeconds(1) }, [], [], [], []));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Legacy_material_is_not_invented_and_contract_material_cannot_be_omitted(bool declared) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var request = await StartRequestAsync(context, declared ? admission => admission with { ExecutionContract = Contract() } : null);
        if (declared) {
            await Assert.ThrowsAsync<InvalidDataException>(() => Owner(context, app.Services).CommitAsync(request));
        } else {
            var result = await Owner(context, app.Services).CommitAsync(request);
            Assert.Null(result.Decision!.Command.Material);
            Assert.Null((await context.DecisionGenerations.SingleAsync()).CanonicalMaterialId);
            var receipt = (await context.IdempotencyKeys.SingleAsync(item => item.CommandId == request.CommandId.Value)).DecisionReceiptJson!;
            using var json = JsonDocument.Parse(receipt);
            Assert.Equal(1, json.RootElement.GetProperty("version").GetInt32());
            Assert.False(json.RootElement.GetProperty("command").TryGetProperty("material", out _));
        }
        Assert.Empty(await context.DecisionMaterials.ToArrayAsync());
        Assert.Empty(await context.DecisionMaterialScopes.ToArrayAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Exact_reads_and_retries_refuse_corrupted_retained_material(int change) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var request = await OriginalAsync(context, app.Services);
        await Owner(context, app.Services).CommitAsync(request);
        var row = await context.DecisionMaterials.SingleAsync();
        if (change == 0) {
            row.PayloadJson = row.PayloadJson.Replace("[]", "{}", StringComparison.Ordinal);
        } else if (change == 1) {
            row.Fingerprint = new string('A', 64);
        } else {
            row.CapturedAtUtcTicks++;
        }
        await context.SaveChangesAsync();
        var material = request.DecisionMaterial!;
        await Assert.ThrowsAsync<InvalidDataException>(() => Reader(context, app.Services).ReadAsync(material.Scope, material.Reference));
        await Assert.ThrowsAsync<InvalidDataException>(() => Owner(context, app.Services).CommitAsync(request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Retained_material_receipt_rejects_version_downgrade_and_changed_stamp_shape(int change) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var request = await OriginalAsync(context, app.Services);
        await Owner(context, app.Services).CommitAsync(request);
        var row = await context.IdempotencyKeys.SingleAsync(item => item.CommandId == request.CommandId.Value);
        var document = JsonNode.Parse(row.DecisionReceiptJson!)!;
        var command = document["command"]!.AsObject();
        switch (change) {
            case 0:
                document["version"] = 1;
                break;
            case 1:
                command.Remove("material");
                break;
            case 2:
                command["material"]!["fingerprint"] = request.DecisionMaterial!.Fingerprint.ToLowerInvariant();
                break;
            case 3:
                command["material"]!["future"] = true;
                break;
        }
        row.DecisionReceiptJson = document.ToJsonString();
        await context.SaveChangesAsync();
        var failure = await Record.ExceptionAsync(() => Owner(context, app.Services).FindCompletedCommandAsync(request.CommandId));
        Assert.True(failure is InvalidDataException or JsonException, failure?.ToString());
    }

    [Fact]
    public async Task Missing_profile_refuses_writes_and_wrong_read_scope_does_not_return_another_snapshot() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var request = await OriginalAsync(context, app.Services);
        await Assert.ThrowsAsync<InvalidOperationException>(() => new EfProcessRuntimeUnitOfWork(context).CommitAsync(request));
        await Owner(context, app.Services).CommitAsync(request);
        var material = request.DecisionMaterial!;
        var reader = Reader(context, app.Services);
        Assert.Null(await reader.ReadAsync(material.Scope with { WorkspaceNamespaceId = Guid.NewGuid() }, material.Reference));
        Assert.Null(await reader.ReadAsync(material.Scope, material.Reference with { Generation = 2 }));
        await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadAsync(material.Scope with { DatabaseProfileId = Guid.NewGuid() }, material.Reference));
    }

    [Fact]
    public async Task Run_snapshot_cannot_refresh_through_a_step_local_owner() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var request = await OriginalAsync(context, app.Services, ReadBasisMode.RunSnapshot);
        var started = await Owner(context, app.Services).CommitAsync(request);
        var refresh = await RefreshRequestAsync(context, started.State, request.DecisionMaterial!);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Owner(context, app.Services).CommitAsync(refresh));
        Assert.Equal(1, await context.DecisionMaterials.CountAsync());
        Assert.Equal(ProcessDecisionPhase.Reconciling, (await DecisionAsync(context, refresh.OriginalState)).Phase);
    }

    [Theory]
    [InlineData(ReadBasisMode.RunSnapshot)]
    [InlineData(ReadBasisMode.StepSnapshot)]
    public async Task SQL_byte_bound_scope_uniqueness_and_material_foreign_key_protect_retained_rows(ReadBasisMode mode) {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        var request = await OriginalAsync(context, app.Services, mode);
        await Owner(context, app.Services).CommitAsync(request);
        var material = request.DecisionMaterial!;
        var tooLarge = "{\"x\":\"" + new string('漢', ProcessDecisionMaterial.MaximumPayloadUtf8Bytes / 3) + "\"}";
        var byteBound = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE process_decision_materials SET \"PayloadJson\" = {tooLarge}, \"PayloadUtf8Bytes\" = octet_length({tooLarge}) WHERE \"MaterialId\" = {material.Reference.MaterialId.Value}"));
        Assert.Equal(PostgresErrorCodes.CheckViolation, byteBound.SqlState);
        var duplicate = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO process_decision_material_scopes (\"ScopeId\", \"DatabaseProfileId\", \"WorkspaceNamespaceId\", \"RootRunId\", \"OwnerRunId\", \"StepInstanceId\", \"ContractId\", \"ContractVersion\", \"Executor\") SELECT {Guid.NewGuid()}, \"DatabaseProfileId\", \"WorkspaceNamespaceId\", \"RootRunId\", \"OwnerRunId\", \"StepInstanceId\", \"ContractId\", \"ContractVersion\", \"Executor\" FROM process_decision_material_scopes WHERE \"ScopeId\" = {material.Scope.Id.Value}"));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
        var deletion = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM process_decision_materials WHERE \"MaterialId\" = {material.Reference.MaterialId.Value}"));
        Assert.Equal(PostgresErrorCodes.RestrictViolation, deletion.SqlState);
        material.RequireOriginal((await Reader(context, app.Services).ReadAsync(material.Scope, material.Reference))!);
    }

    [Fact]
    public async Task Empty_migration_round_trips_and_retained_material_blocks_downgrade() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        await MigrateAsync(context, true);
        await MigrateAsync(context, false);
        var request = await OriginalAsync(context, app.Services);
        await Owner(context, app.Services).CommitAsync(request);
        var refusal = await Assert.ThrowsAsync<PostgresException>(() => MigrateAsync(context, true));
        Assert.Equal(PostgresErrorCodes.RaiseException, refusal.SqlState);
        var material = request.DecisionMaterial!;
        material.RequireOriginal((await Reader(context, app.Services).ReadAsync(material.Scope, material.Reference))!);
    }

    [Fact]
    public async Task Canonical_assignment_blocks_downgrade_before_its_first_capture_is_committed() {
        await using var app = await TestApplication.CreateAsync();
        await using var context = Context(app.Services);
        await OriginalAsync(context, app.Services);
        Assert.Empty(await context.DecisionMaterials.ToArrayAsync());
        var refusal = await Assert.ThrowsAsync<PostgresException>(() => MigrateAsync(context, true));
        Assert.Equal(PostgresErrorCodes.RaiseException, refusal.SqlState);
    }

    private static async Task<ProcessRuntimeCommitRequest> OriginalAsync(ProcessPersistenceDbContext context, IServiceProvider services,
        ReadBasisMode mode = ReadBasisMode.StepSnapshot) {
        var request = await StartRequestAsync(context, original => {
            var stepOverride = original.StepOverride! with { ReadBasisMode = mode };
            return original with { ExecutionContract = Contract(), MaterialProtocol = ProcessDecisionMaterialProtocol.CanonicalSnapshotV1, StepOverride = stepOverride,
                Policy = DecisionDataPolicyResolver.Resolve(original.WorkspacePolicy, original.OwnerCapabilities,
                    definition: original.DefinitionOverride, step: stepOverride, authorizedInvocation: original.AuthorizedInvocationOverride,
                    enclosingBudgets: original.EnclosingBudgets) };
        });
        var assignment = await context.RuntimeStepAssignments.AsNoTracking().SingleAsync(item => item.RunId == request.OriginalState.RunId.Value);
        var admission = ProcessDecisionDataPolicyAdmissionCodec.Deserialize(assignment.DecisionDataPolicyJson!);
        var scope = new ProcessDecisionMaterialScope(new(Guid.NewGuid()), Database(services).Profile.Profile.Id, admission.WorkspaceNamespaceId,
            request.OriginalState.RootRunId, request.OriginalState.RunId, mode == ReadBasisMode.StepSnapshot ? Step(request.OriginalState) : null);
        var start = (ProcessDecisionStart)request.Decision!;
        var material = new ProcessDecisionMaterial(scope, new(scope.Id, 1, start.MaterialId), Contract(),
            new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero).AddTicks(7), "{ \"tasks\": [] }");
        return request with { DecisionMaterial = material, Decision = start with { Material = material.Stamp } };
    }

    private static async Task<ProcessRuntimeCommitRequest> RefreshRequestAsync(ProcessPersistenceDbContext context,
        ProcessRuntimeStateSnapshot state, ProcessDecisionMaterial original) {
        var reconciling = await PhaseAsync(context, state, ProcessDecisionPhaseChange.BeginReconciliation);
        var reserved = await ReserveCaptureAsync(context, reconciling.State, 0);
        var current = await DecisionAsync(context, reserved.State);
        var balance = reserved.DecisionBudget!.Change.Balance;
        var material = Copy(original, reference: original.Reference with { Generation = original.Reference.Generation + 1, MaterialId = new(Guid.NewGuid()) },
            captured: original.CapturedAtUtc.AddSeconds(1), payload: "{\"tasks\":[1]}");
        return Request(reserved.State, new ProcessDecisionRefresh(Step(state), current.Expectation, material.Reference.MaterialId,
            ProcessDecisionRefreshKind.ManagerRefresh) { Material = material.Stamp },
            new ProcessDecisionBudgetSettlement(new(Step(state), balance.BudgetId, balance.Revision), reserved.DecisionBudget.Command.AllocationId,
                new(2, new(5, 0)))) with { DecisionMaterial = material };
    }

    private static ProcessDecisionMaterial Copy(ProcessDecisionMaterial original, ProcessDecisionMaterialScope? scope = null,
        ProcessDecisionMaterialReference? reference = null, ProcessDecisionExecutionContract? contract = null, DateTimeOffset? captured = null, string? payload = null)
        => new(scope ?? original.Scope, reference ?? original.Reference, contract ?? original.ExecutionContract,
            captured ?? original.CapturedAtUtc, payload ?? original.PayloadJson);

    private static ProcessDecisionExecutionContract Contract() => ProcessDecisionPolicyFixture.Contract(ProcessDecisionChildOwner.Agent);
    private static ICanonicalRuntimeDatabase Database(IServiceProvider services) => services.GetRequiredService<ICanonicalRuntimeDatabase>();
    private static EfProcessRuntimeUnitOfWork Owner(ProcessPersistenceDbContext context, IServiceProvider services) => new(context, runtimeDatabase: Database(services));
    private static EfProcessDecisionMaterialStore Reader(ProcessPersistenceDbContext context, IServiceProvider services) => new(context, Database(services));

    internal static async Task MigrateAsync(ProcessPersistenceDbContext context, bool down) {
        var migration = new CanDoItAll.Migrations.PostgreSql.Migrations.AddProcessDecisionMaterials();
        var commands = context.GetService<IMigrationsSqlGenerator>().Generate(down ? migration.DownOperations : migration.UpOperations, context.Model);
        await using var transaction = await context.Database.BeginTransactionAsync();
        foreach (var command in commands) {
            await using var sql = context.Database.GetDbConnection().CreateCommand();
            sql.Transaction = transaction.GetDbTransaction();
            sql.CommandText = command.CommandText;
            await sql.ExecuteNonQueryAsync();
        }
        await transaction.CommitAsync();
    }

    private sealed class AfterMaterialFlush : SaveChangesInterceptor {
        public InvalidOperationException Failure { get; } = new("Injected failure after material SQL flush.");
        public bool Flushed { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            Flushed = result > 0 && eventData.Context!.Database.CurrentTransaction is not null;
            throw Failure;
        }
    }
}
