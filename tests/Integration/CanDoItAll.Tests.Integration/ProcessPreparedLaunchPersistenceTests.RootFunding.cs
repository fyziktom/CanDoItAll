using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessPreparedLaunchPersistenceTests {
    [Theory]
    [InlineData(ReadBasisMode.StepSnapshot)]
    [InlineData(ReadBasisMode.RunSnapshot)]
    public async Task Prepared_root_funding_is_original_immutable_and_blocks_unsafe_downgrade(ReadBasisMode mode) {
        await using var app = await TestApplication.CreateAsync(Harness());
        var services = app.Services;
        var original = FundedPreparation(services, mode);
        var saved = await Store(services).PrepareAsync(original);
        await using var context = new ProcessPersistenceDbContext(ProcessOptions(services));
        Assert.Empty(await context.DecisionBudgets.ToArrayAsync());
        var downgrade = await Assert.ThrowsAsync<PostgresException>(() => ProcessDecisionFundingPersistenceTests.FundingMigrationAsync(context, true));
        Assert.Equal(PostgresErrorCodes.RaiseException, downgrade.SqlState);
        var retained = (await Store(services).GetAsync(saved.Preparation.AdmissionId))!;
        var expected = ProcessDecisionRunAdmissionCodec.Serialize(original.InitialCommit.InitialRunDecisionAdmission!);
        Assert.Equal(expected, ProcessDecisionRunAdmissionCodec.Serialize(retained.Preparation.InitialCommit.InitialRunDecisionAdmission!));
        var owner = new EfProcessRuntimeUnitOfWork(context, coordinatedTransaction: Coordinator(services), launchAuthorityPolicy: new AuthorityPolicy(),
            runtimeDatabase: services.GetRequiredService<ICanonicalRuntimeDatabase>());
        var request = ProcessPreparedLaunchFixture.Commit(retained);
        Assert.True((await owner.CommitAsync(request)).Succeeded);
        Assert.True((await owner.CommitAsync(request)).Succeeded);
        Assert.Equal(expected, (await context.DecisionBudgets.AsNoTracking().SingleAsync(item => item.StepInstanceId == null)).RunAdmissionJson);
        foreach (var substitution in new ProcessDecisionRunAdmission?[] {
            null,
            request.InitialRunDecisionAdmission! with { BudgetId = new(Guid.NewGuid()) },
            request.InitialRunDecisionAdmission! with { WorkspaceHeadRevision = Guid.NewGuid() }
        }) {
            await Assert.ThrowsAsync<ProcessLaunchIntentConflictException>(() => owner.CommitAsync(request with { InitialRunDecisionAdmission = substitution }));
        }
        Assert.Equal(2, await context.DecisionBudgets.CountAsync());
        Assert.Empty(await context.DecisionBudgetAllocations.ToArrayAsync());
    }

    [Fact]
    public async Task Root_policy_preparation_uses_a_distinct_hash_domain_from_legacy_readers() {
        await using var app = await TestApplication.CreateAsync(Harness());
        var saved = await Store(app.Services).PrepareAsync(FundedPreparation(app.Services, ReadBasisMode.RunSnapshot));
        await using var context = new ProcessPersistenceDbContext(ProcessOptions(app.Services));
        var row = await context.PreparedLaunches.SingleAsync(item => item.Id == saved.Preparation.AdmissionId.Value);
        var legacy = Hash("process-decision-policy-v1\n" + row.PayloadJson);
        Assert.Equal(Hash("process-root-decision-policy-v1\nprocess-decision-policy-v1\n" + row.PayloadJson), row.PreparationFingerprint);
        Assert.NotEqual(legacy, row.PreparationFingerprint);
        row.PreparationFingerprint = legacy;
        await context.SaveChangesAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Store(app.Services).GetAsync(saved.Preparation.AdmissionId));
        Assert.Empty(await context.DecisionBudgets.ToArrayAsync());

        static string Hash(string value) => "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }

    private static ProcessPreparedLaunch FundedPreparation(IServiceProvider services, ReadBasisMode mode) {
        var original = ProcessPreparedLaunchFixture.Create(ProcessPreparedLaunchFixture.Local(Profile(services).Profile.Id), new(Guid.NewGuid()));
        var initial = original.InitialCommit;
        var root = ProcessDecisionRunPolicyFixture.Root(initial.OriginalState.RunId, initial.InitialPlan!.Definition.VersionId, Profile(services).Profile.Id, mode);
        return original with { InitialCommit = initial with { InitialRunDecisionAdmission = root,
            InitialAssignments = initial.InitialAssignments!.Select(item => item with {
                DecisionDataPolicy = ProcessDecisionRunPolicyFixture.Child(root, item.StepInstanceId)
            }).ToArray()
        } };
    }

    [Fact]
    public async Task Prepared_shared_scope_is_immutable_and_blocks_downgrade_before_runtime_admission() {
        await using var app = await TestApplication.CreateAsync(Harness());
        var original = FundedPreparation(app.Services, ReadBasisMode.RunSnapshot);
        var root = ProcessDecisionRunPolicyFixture.WithSharedScope(original.InitialCommit.InitialRunDecisionAdmission!);
        original = original with { InitialCommit = original.InitialCommit with { InitialRunDecisionAdmission = root,
            InitialAssignments = original.InitialCommit.InitialAssignments!.Select(item => item with {
                DecisionDataPolicy = ProcessDecisionRunPolicyFixture.Child(root, item.StepInstanceId)
            }).ToArray()
        } };
        var saved = await Store(app.Services).PrepareAsync(original);
        await using var context = new ProcessPersistenceDbContext(ProcessOptions(app.Services));
        Assert.Empty(await context.DecisionRunScopes.ToArrayAsync());
        var failure = await Assert.ThrowsAsync<PostgresException>(() => ProcessDecisionRunScopeRecoveryTests.MigrateAsync(context, true));
        Assert.Equal(PostgresErrorCodes.RaiseException, failure.SqlState);
        var retained = (await Store(app.Services).GetAsync(saved.Preparation.AdmissionId))!;
        Assert.Equal(root.MaterialScope, retained.Preparation.InitialCommit.InitialRunDecisionAdmission!.MaterialScope);
        var row = await context.PreparedLaunches.SingleAsync(item => item.Id == saved.Preparation.AdmissionId.Value);
        var expected = "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            "process-shared-run-scope-v1\nprocess-root-decision-policy-v1\nprocess-decision-policy-v1\n" + row.PayloadJson))).ToLowerInvariant();
        Assert.Equal(expected, row.PreparationFingerprint);
        var owner = new EfProcessRuntimeUnitOfWork(context, coordinatedTransaction: Coordinator(app.Services), launchAuthorityPolicy: new AuthorityPolicy(),
            runtimeDatabase: app.Services.GetRequiredService<ICanonicalRuntimeDatabase>());
        var request = ProcessPreparedLaunchFixture.Commit(retained);
        Assert.True((await owner.CommitAsync(request)).Succeeded);
        Assert.True((await owner.CommitAsync(request)).Succeeded);
        Assert.Single(await context.DecisionRunScopes.ToArrayAsync());
        await Assert.ThrowsAsync<ProcessLaunchIntentConflictException>(() => owner.CommitAsync(request with {
            InitialRunDecisionAdmission = request.InitialRunDecisionAdmission! with { MaterialScope = null }
        }));
    }
}
