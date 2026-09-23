using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed partial class ProcessCatalogAuthorityPersistenceTests {
    [Theory]
    [InlineData(ImportedAssetState.Prepared)]
    [InlineData(ImportedAssetState.Materialized)]
    [InlineData(ImportedAssetState.Committed)]
    public async Task Imported_asset_history_is_readable_but_cannot_reenter_native_or_byte_effects(ImportedAssetState state) {
        var clock = new NativeClock();
        var driver = new AssetDriverProbe();
        await using var app = await TestApplication.CreateAsync(AssetHarness(clock, driver));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var test = await CreateAssetCaseAsync(services, clock, false);
        var owner = services.GetRequiredService<ProjectWorkbenchService>();
        var prepared = await owner.PrepareProcessAssetAsync(test.Invocation, default);
        if (state != ImportedAssetState.Prepared) {
            prepared = await owner.MaterializeProcessAssetAsync(prepared, AssetMedia(), default);
        }
        if (state == ImportedAssetState.Committed) {
            await owner.CommitProcessAssetAsync(prepared, default);
        }
        string original;
        var provenance = new RetainedEvidenceImport(Guid.NewGuid(), Guid.NewGuid());
        await using (var database = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync()) {
            var row = await database.Set<ProjectProcessAssetContributionRecord>().SingleAsync(item => item.IntentId == prepared.Plan.Producer.IntentId.Value);
            original = AssetEvidenceWithoutImport(row);
            row.ImportedHistory = provenance;
            await database.SaveChangesAsync();
        }
        await using var restartedScope = app.Services.CreateAsyncScope();
        var restarted = restartedScope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var history = await restarted.FindProcessAssetAsync(prepared.Plan.Producer.IntentId);
        Assert.NotNull(history);
        Assert.Equal(provenance, history.ImportedHistory);
        Assert.Equal(prepared.PlanFingerprint, history.PlanFingerprint);
        Assert.Equal(JsonSerializer.Serialize(prepared.Plan, ProjectProcessAssetPersistence.Json),
            JsonSerializer.Serialize(history.Plan, ProjectProcessAssetPersistence.Json));
        Assert.Equal(state == ImportedAssetState.Committed, history.Receipt is not null);
        var stripped = history with { ImportedHistory = null };
        var writes = driver.StableWrites;
        await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.PrepareProcessAssetAsync(test.Invocation, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.RequireProcessAssetMediaReadAsync(stripped, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.MaterializeProcessAssetAsync(stripped, AssetMedia(), default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => restarted.CommitProcessAssetAsync(stripped, default));
        Assert.Equal(writes, driver.StableWrites);
        await using (var database = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync()) {
            var row = await database.Set<ProjectProcessAssetContributionRecord>().AsNoTracking().SingleAsync(item => item.IntentId == prepared.Plan.Producer.IntentId.Value);
            Assert.Equal(original, AssetEvidenceWithoutImport(row));
            Assert.Equal(provenance, row.ImportedHistory);
        }
        var fresh = new ProjectProcessAssetInvocation(test.Invocation.Admitted with { IntentId = new(Guid.NewGuid()) },
            test.Invocation.Execution, test.Invocation.ParentNodeKey);
        var independent = await restarted.PrepareProcessAssetAsync(fresh, default);
        Assert.Null(independent.ImportedHistory);
        Assert.NotEqual(prepared.Plan.NativeObjectId, independent.Plan.NativeObjectId);
        Assert.NotEqual(prepared.Plan.StorageIntentId, independent.Plan.StorageIntentId);
        Assert.Equal(writes, driver.StableWrites);
    }

    [Fact]
    public async Task Registered_Process_asset_producer_cannot_credit_an_imported_receipt_to_its_current_journal() {
        var clock = new NativeClock();
        var driver = new AssetDriverProbe();
        await using var app = await TestApplication.CreateAsync(AssetHarness(clock, driver));
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var test = await CreateAssetCaseAsync(services, clock, false);
        var context = await ToolProducerContextAsync(test.Owner, test.Run);
        var provider = Assert.Single(services.GetServices<IAgentRuntimeToolProvider>().OfType<ProjectStructureAgentRuntimeToolProvider>());
        using var audit = WorkspaceExecutionAuditContext.BeginScope(test.Run);
        await using var lease = await test.Journal.AcquireRunAsync(test.Run.ToolAdmission!.Session.Reference, default);
        using var bound = lease.Bind();
        await test.Journal.BeginSegmentAsync(lease, CanDoItAll.Tests.Integration.Runtime.AgentToolAdmissionJournalFixture.Envelope(), null, default);
        var batch = Assert.Single((await test.Journal.AdmitBatchAsync(lease, test.Payload.Digest,
            CanDoItAll.Tests.Integration.Runtime.AgentToolAdmissionJournalFixture.Envelope(), [new("asset", test.Payload, false)], default)).Batches);
        var claim = await test.Journal.ClaimInvocationAsync(lease, batch.Id, "asset", test.Payload, default);
        using var dispatch = claim.Bind();
        var tool = Assert.Single((await provider.CreateToolsAsync(context, default)).OfType<AIFunction>(),
            item => item.Name == ProjectStructureToolPolicy.ProjectStructureAssetCreate);
        var input = new ProjectProcessAssetProposalCodec().Read(test.Payload).Create!;
        ProjectStructureNodeSummary result;
        using (var effects = AgentToolInvocationEffectScope.Begin()) {
            result = ReadRegisteredToolResult<ProjectStructureNodeSummary>(tool, await tool.InvokeAsync(new AIFunctionArguments {
                ["projectId"] = test.Owner.Project.ProjectId, ["request"] = input
            }));
            Assert.NotNull(effects.CommittedEffect);
        }
        await using (var database = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync()) {
            var row = await database.Set<ProjectProcessAssetContributionRecord>().SingleAsync(item => item.IntentId == claim.Proposal.IntentId.Value);
            row.ImportedHistory = new(Guid.NewGuid(), Guid.NewGuid());
            await database.SaveChangesAsync();
        }
        using (var effects = AgentToolInvocationEffectScope.Begin()) {
            await Assert.ThrowsAsync<InvalidOperationException>(async () => {
                await tool.InvokeAsync(new AIFunctionArguments { ["projectId"] = test.Owner.Project.ProjectId, ["request"] = input });
            });
            Assert.Null(effects.CommittedEffect);
        }
        Assert.Equal(1, driver.StableWrites);
        var history = await services.GetRequiredService<ProjectWorkbenchService>().FindProcessAssetAsync(claim.Proposal.IntentId);
        Assert.NotNull(history!.ImportedHistory);
        Assert.Equal(result.ProcessAssetReceipt!.Receipt, history.Receipt);
    }

    private static string AssetEvidenceWithoutImport(ProjectProcessAssetContributionRecord row) {
        var json = JsonSerializer.SerializeToNode(row)!.AsObject();
        json.Remove(nameof(ProjectProcessAssetContributionRecord.ImportedHistory));
        return json.ToJsonString();
    }

    public enum ImportedAssetState { Prepared, Materialized, Committed }
}
