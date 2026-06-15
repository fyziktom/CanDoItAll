using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class WorkbenchSourceSnapshotIntegrationTests
{
    [Fact]
    public async Task Workbench_snapshot_provider_exposes_stable_source_grounded_items()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var provider = scope.ServiceProvider.GetRequiredService<IProjectStructureSourceSnapshotProvider>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectId = await CreateProjectAsync(projects);

        var customNode = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Snapshot task",
                "Deterministic source item",
                "This note is source evidence. token=super-secret-token",
                ParentNodeKey: null,
                X: 42,
                Y: 84,
                ObjectSubtype: "task"));
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var record = await dbContext.Set<ProjectObjectRecord>().SingleAsync(item => item.NodeKey == customNode.Id);
            record.MetadataJson = """{"zIndex":7,"workItem":{"description":"Metadata-backed layout extension"},"apiKey":"sk-workbench-secret123"}""";
            await dbContext.SaveChangesAsync();
        }

        var first = await provider.ReadSnapshotAsync(new ProjectStructureSourceSnapshotRequest(projectId, Take: 2));
        var second = await provider.ReadSnapshotAsync(new ProjectStructureSourceSnapshotRequest(projectId, Take: 2));

        Assert.Equal(first.Manifest.SnapshotId, second.Manifest.SnapshotId);
        Assert.Equal(
            first.Items.Select(item => item.Id.Value),
            second.Items.Select(item => item.Id.Value));
        Assert.True(first.Manifest.TotalItemCount > first.Items.Count);
        Assert.True(first.Manifest.HasMore);
        Assert.NotNull(first.Manifest.NextCursor);
        Assert.Equal(MemorySourceSnapshotHashScope.FullSnapshot, first.Manifest.SnapshotHashScope);
        Assert.Equal(MemorySourceSnapshotProviderVersions.WorkbenchProjectStructure, first.Manifest.ProviderVersion);

        var resumed = await provider.ReadSnapshotAsync(new ProjectStructureSourceSnapshotRequest(
            projectId,
            first.Manifest.NextCursor,
            Take: 100));
        Assert.DoesNotContain(
            resumed.Items,
            item => first.Items.Any(firstItem => firstItem.Id == item.Id));

        var full = await provider.ReadSnapshotAsync(new ProjectStructureSourceSnapshotRequest(projectId, Take: 100));
        var snapshotNode = Assert.Single(
            full.Items,
            item => item.EntityKind == MemorySourceEntityKind.ProjectNode &&
                    string.Equals(item.Provenance.SourceEntityId, customNode.Id, StringComparison.Ordinal));

        Assert.Equal("Snapshot task", snapshotNode.Title);
        Assert.Equal(7, snapshotNode.Layout?.ZIndex);
        Assert.NotNull(snapshotNode.CreatedAtUtc);
        Assert.NotNull(snapshotNode.UpdatedAtUtc);
        Assert.Contains("This note is source evidence.", snapshotNode.Content, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", snapshotNode.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret-token", snapshotNode.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("sk-workbench-secret123", snapshotNode.Layout?.MetadataJson ?? string.Empty, StringComparison.Ordinal);
        Assert.Equal(MemorySourceAccessMode.Redacted, snapshotNode.Permission.AccessMode);
        Assert.Equal(MemorySourceSensitivity.Sensitive, snapshotNode.Permission.Sensitivity);
        Assert.True(snapshotNode.Permission.ContainsSensitivePayload);
        Assert.Equal(MemorySourceHashClassification.RestrictedIntegrity, snapshotNode.HashPolicy.Classification);
        Assert.False(snapshotNode.HashPolicy.Exportable);
        Assert.Contains(snapshotNode.Links, link => link.Kind == ProjectObjectLinkKind.Contains.ToString());

        var invalidCursorException = await Assert.ThrowsAsync<MemorySourceSnapshotCursorException>(async () =>
            await provider.ReadSnapshotAsync(new ProjectStructureSourceSnapshotRequest(
                projectId,
                new MemorySourceSnapshotCursor("not-a-supported-cursor"),
                Take: 2)));
        Assert.Equal(MemorySourceSnapshotCursorFailureReason.InvalidFormat, invalidCursorException.Reason);

        var wrongKindCursor = MemorySourceSnapshotCursor.Create(
            MemorySourceKind.ProcessRuntime,
            projectId,
            MemorySourceSnapshotProviderVersions.WorkbenchProjectStructure,
            1,
            first.Items[0].Id);
        var wrongKindException = await Assert.ThrowsAsync<MemorySourceSnapshotCursorException>(async () =>
            await provider.ReadSnapshotAsync(new ProjectStructureSourceSnapshotRequest(projectId, wrongKindCursor, Take: 2)));
        Assert.Equal(MemorySourceSnapshotCursorFailureReason.SourceKindMismatch, wrongKindException.Reason);

        var staleCursor = MemorySourceSnapshotCursor.Create(
            MemorySourceKind.WorkbenchProjectStructure,
            projectId,
            MemorySourceSnapshotProviderVersions.WorkbenchProjectStructure,
            1,
            MemorySourceItemId.Create(
                MemorySourceKind.WorkbenchProjectStructure,
                projectId,
                MemorySourceEntityKind.ProjectNode,
                "deleted-node"));
        var staleException = await Assert.ThrowsAsync<MemorySourceSnapshotCursorException>(async () =>
            await provider.ReadSnapshotAsync(new ProjectStructureSourceSnapshotRequest(projectId, staleCursor, Take: 2)));
        Assert.Equal(MemorySourceSnapshotCursorFailureReason.StaleAnchor, staleException.Reason);
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projects)
    {
        var result = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = "Snapshot provider project",
            Description = "Project source snapshot validation.",
            Objective = "Prove source snapshot contracts.",
            CurrentPhase = "Discovery",
            Phases =
            [
                new ProjectPhaseEditorModel
                {
                    Name = "Discovery",
                    Goal = "Inspect source data.",
                    Status = ProjectPhaseStatus.Active,
                    StartDateUtc = new DateTime(2026, 5, 15),
                    EndDateUtc = new DateTime(2026, 5, 16)
                }
            ]
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }
}
