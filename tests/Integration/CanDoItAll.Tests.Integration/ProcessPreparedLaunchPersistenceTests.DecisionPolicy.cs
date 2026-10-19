using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessPreparedLaunchPersistenceTests {
    [Theory]
    [InlineData(false, false, ProcessDecisionChildOwner.Agent)]
    [InlineData(true, false, ProcessDecisionChildOwner.Agent)]
    [InlineData(true, true, ProcessDecisionChildOwner.Agent)]
    [InlineData(true, true, ProcessDecisionChildOwner.Workflow)]
    public async Task Decision_policy_survives_preparation_commit_reload_and_rejects_every_repair_substitution(
        bool admitted, bool declaredContract, ProcessDecisionChildOwner executor) {
        await using var app = await TestApplication.CreateAsync(Harness());
        var services = app.Services;
        var original = ProcessPreparedLaunchFixture.Create(ProcessPreparedLaunchFixture.Local(Profile(services).Profile.Id), new(Guid.NewGuid()));
        var assignment = Assert.Single(original.InitialCommit.InitialAssignments!);
        var policy = ProcessDecisionPolicyFixture.Create(original.InitialCommit.InitialPlan!.Definition.VersionId, assignment.StepInstanceId) with {
            ExecutionContract = declaredContract ? ProcessDecisionPolicyFixture.Contract(executor) : null
        };
        var prepared = await Store(services).PrepareAsync(original with { InitialCommit = original.InitialCommit with {
            InitialAssignments = [assignment with { DecisionDataPolicy = admitted ? policy : null }]
        } });
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
                        DecisionDataPolicy = policy with { ExecutionContract = replacement }
                    }]).AsTask());
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
