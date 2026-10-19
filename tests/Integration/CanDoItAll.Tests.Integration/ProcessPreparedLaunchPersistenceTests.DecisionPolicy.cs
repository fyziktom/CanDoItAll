using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessPreparedLaunchPersistenceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Preparation_cannot_embed_runtime_decision_or_captured_material(bool payload) {
        await using var app = await TestApplication.CreateAsync(Harness());
        var original = ProcessPreparedLaunchFixture.Create(ProcessPreparedLaunchFixture.Local(Profile(app.Services).Profile.Id), new(Guid.NewGuid()));
        var state = original.InitialCommit.OriginalState;
        var step = state.Steps.Single().StepInstanceId;
        var scope = new ProcessDecisionMaterialScope(new(Guid.NewGuid()), Profile(app.Services).Profile.Id, Guid.NewGuid(), state.RootRunId, state.RunId, step);
        var material = new ProcessDecisionMaterial(scope, new(scope.Id, 1, new(Guid.NewGuid())), ProcessDecisionPolicyFixture.Contract(ProcessDecisionChildOwner.Agent),
            ProcessProjectAdmissionFixture.Now, "{}");
        var invalid = original with { InitialCommit = original.InitialCommit with {
            Decision = payload ? null : new ProcessDecisionStart(step, new(Guid.NewGuid()), material.Reference.MaterialId),
            DecisionMaterial = payload ? material : null
        } };
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store(app.Services).PrepareAsync(invalid));
    }

    [Theory]
    [InlineData(false, false, ProcessDecisionChildOwner.Agent, false, false)]
    [InlineData(true, false, ProcessDecisionChildOwner.Agent, false, false)]
    [InlineData(true, true, ProcessDecisionChildOwner.Agent, false, false)]
    [InlineData(true, true, ProcessDecisionChildOwner.Workflow, false, false)]
    [InlineData(true, true, ProcessDecisionChildOwner.Agent, true, false)]
    [InlineData(true, true, ProcessDecisionChildOwner.Workflow, true, false)]
    [InlineData(true, true, ProcessDecisionChildOwner.Agent, true, true)]
    [InlineData(true, true, ProcessDecisionChildOwner.Workflow, true, true)]
    public async Task Decision_policy_survives_preparation_commit_reload_and_rejects_every_repair_substitution(
        bool admitted, bool declaredContract, ProcessDecisionChildOwner executor, bool canonicalMaterial, bool sharedContent) {
        await using var app = await TestApplication.CreateAsync(Harness());
        var services = app.Services;
        var original = ProcessPreparedLaunchFixture.Create(ProcessPreparedLaunchFixture.Local(Profile(services).Profile.Id), new(Guid.NewGuid()));
        var assignment = Assert.Single(original.InitialCommit.InitialAssignments!);
        var policy = ProcessDecisionPolicyFixture.Create(original.InitialCommit.InitialPlan!.Definition.VersionId, assignment.StepInstanceId) with {
            ExecutionContract = declaredContract ? ProcessDecisionPolicyFixture.Contract(executor) : null,
            MaterialProtocol = sharedContent ? ProcessDecisionMaterialProtocol.SharedContentV1 : canonicalMaterial ? ProcessDecisionMaterialProtocol.CanonicalSnapshotV1 : null,
            ContentContract = sharedContent ? ProcessDecisionPolicyFixture.ContentContract() : null
        };
        var prepared = await Store(services).PrepareAsync(original with { InitialCommit = original.InitialCommit with {
            InitialAssignments = [assignment with { DecisionDataPolicy = admitted ? policy : null }]
        } });
        if (canonicalMaterial) {
            await using var pending = new ProcessPersistenceDbContext(ProcessOptions(services));
            var refusal = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => ProcessDecisionMaterialPersistenceTests.MigrateAsync(pending, true));
            Assert.Equal(Npgsql.PostgresErrorCodes.RaiseException, refusal.SqlState);
        }
        await using (var context = new ProcessPersistenceDbContext(ProcessOptions(services))) {
            var owner = new EfProcessRuntimeUnitOfWork(context, coordinatedTransaction: Coordinator(services), launchAuthorityPolicy: new AuthorityPolicy());
            Assert.True((await owner.CommitAsync(ProcessPreparedLaunchFixture.Commit(prepared))).Succeeded);
        }
        var encoded = Encode(admitted ? policy : null);
        for (var reload = 0; reload < 2; reload++) {
            await using var context = new ProcessPersistenceDbContext(ProcessOptions(services));
            var store = new EfProcessRuntimeStepAssignmentStore(context);
            var current = Assert.Single(await store.LoadByRunAsync(assignment.RunId));
            Assert.Equal(encoded, Encode(current.DecisionDataPolicy));
            await store.SaveAsync([current with { Prompt = "A permitted recovery prompt edit." }]);
            foreach (var replacement in new ProcessDecisionDataPolicyAdmission?[] { admitted ? null : policy, policy with { WorkspaceHeadRevision = Guid.NewGuid() } }) {
                await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync([current with { DecisionDataPolicy = replacement }]).AsTask());
            }
            if (admitted) {
                var contract = ProcessDecisionPolicyFixture.Contract(executor);
                foreach (var replacement in new ProcessDecisionExecutionContract?[] { declaredContract ? null : contract,
                    contract with { ContractId = Guid.NewGuid() }, contract with { Version = 2 },
                    contract with { Executor = executor == ProcessDecisionChildOwner.Agent ? ProcessDecisionChildOwner.Workflow : ProcessDecisionChildOwner.Agent } }) {
                    await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync([current with {
                        DecisionDataPolicy = policy with { ExecutionContract = replacement, MaterialProtocol = replacement is null ? null : policy.MaterialProtocol,
                            ContentContract = replacement is null ? null : policy.ContentContract }
                    }]).AsTask());
                }
                if (declaredContract) {
                    await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync([current with {
                        DecisionDataPolicy = policy with { MaterialProtocol = canonicalMaterial ? null : ProcessDecisionMaterialProtocol.CanonicalSnapshotV1,
                            ContentContract = null }
                    }]).AsTask());
                }
                if (sharedContent) {
                    foreach (var content in new[] { policy.ContentContract! with { SchemaId = Guid.NewGuid() }, policy.ContentContract! with { Version = 2 } }) {
                        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveAsync([current with {
                            DecisionDataPolicy = policy with { ContentContract = content }
                        }]).AsTask());
                    }
                }
            }
            Assert.Equal(encoded, (await context.RuntimeStepAssignments.AsNoTracking().SingleAsync(item => item.RunId == assignment.RunId.Value)).DecisionDataPolicyJson);
            var retained = (await Store(services).GetAsync(prepared.Preparation.AdmissionId))!;
            Assert.Equal(prepared.PreparationFingerprint, retained.PreparationFingerprint);
            Assert.Equal(encoded, Encode(Assert.Single(retained.Preparation.InitialCommit.InitialAssignments!).DecisionDataPolicy));
        }
        await using var corrupted = new ProcessPersistenceDbContext(ProcessOptions(services));
        await corrupted.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE process_runtime_step_assignments SET "DecisionDataPolicyJson" = {"{}"} WHERE "RunId" = {assignment.RunId.Value}
            """);
        var reader = new EfProcessRuntimeStepAssignmentStore(corrupted);
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => reader.LoadByRunAsync(assignment.RunId).AsTask());
        var substituted = Encode(policy with { StepInstanceId = CanDoItAll.Processes.Abstractions.ProcessStepInstanceId.New() });
        await corrupted.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE process_runtime_step_assignments SET "DecisionDataPolicyJson" = {substituted} WHERE "RunId" = {assignment.RunId.Value}
            """);
        await Assert.ThrowsAsync<InvalidDataException>(() => reader.LoadByRunAsync(assignment.RunId).AsTask());

        static string? Encode(ProcessDecisionDataPolicyAdmission? value)
            => value is null ? null : ProcessDecisionDataPolicyAdmissionCodec.Serialize(value);
    }
}
