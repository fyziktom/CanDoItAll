using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration;

public sealed partial class ProjectTransferHistoryPersistenceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Project_import_retains_original_Workflow_manifest_scalars_without_a_surviving_project(bool completed) {
        await using var fixture = await TransferFixture.CreateAsync();
        var project = new Project { Name = "Unrelated source project" };
        await using (var source = await fixture.SourceContextAsync()) {
            source.Add(project);
            await source.SaveChangesAsync();
        }
        var retained = new WorkflowStructureOutputRecord {
            RunId = Guid.NewGuid(), OccurrencePath = "retained", Slot = 0,
            DatabaseProfileId = fixture.TargetProfile.Profile.Id, ProjectId = project.Id, ProjectLifetimeId = Guid.NewGuid(),
            PlanJson = " Original private plan bytes ", ReceiptJson = completed ? " Original receipt bytes " : "",
            StoragePlacementIntentId = Guid.NewGuid(), AssetDispatchStarted = true, IsComplete = completed, NextInspectionAtUtc = Now
        };
        await using (var target = await fixture.TargetContextAsync()) {
            target.Add(retained);
            await target.SaveChangesAsync();
        }
        var result = await fixture.TransferAsync();
        Assert.False(result.Success);
        Assert.Equal(0, result.RecordsCopied);
        Assert.Contains("retained workflow structure output manifests", result.Message, StringComparison.Ordinal);
        await using var restarted = await fixture.TargetContextAsync();
        Assert.Empty(await restarted.Set<Project>().ToArrayAsync());
        var saved = await restarted.Set<WorkflowStructureOutputRecord>().AsNoTracking().SingleAsync();
        Assert.Equal(retained.DatabaseProfileId, saved.DatabaseProfileId);
        Assert.Equal(retained.ProjectId, saved.ProjectId);
        Assert.Equal(retained.ProjectLifetimeId, saved.ProjectLifetimeId);
        Assert.Equal(retained.PlanJson, saved.PlanJson);
        Assert.Equal(retained.ReceiptJson, saved.ReceiptJson);
        Assert.Equal(retained.StoragePlacementIntentId, saved.StoragePlacementIntentId);
        Assert.Equal(retained.AssetDispatchStarted, saved.AssetDispatchStarted);
        Assert.Equal(retained.IsComplete, saved.IsComplete);
    }
}
