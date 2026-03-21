using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectWorkbenchServiceIntegrationTests
{
    [Fact]
    public async Task GetStructureAsync_builds_a_structure_surface_for_sqlite_projects()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var saveResult = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = "Workbench Structure Validation",
            Description = "Structure projection smoke test.",
            Objective = "Ensure structure surfaces load from SQLite.",
            CurrentPhase = "Discovery",
            Phases =
            [
                new ProjectPhaseEditorModel
                {
                    Name = "Discovery",
                    Goal = "Investigate flow stability.",
                    Status = ProjectPhaseStatus.Active,
                    StartDateUtc = new DateTime(2026, 3, 19),
                    EndDateUtc = new DateTime(2026, 3, 21)
                }
            ]
        });

        Assert.True(saveResult.IsSuccess);
        Assert.NotEqual(Guid.Empty, saveResult.Value);
        var surface = await workbench.GetStructureAsync(saveResult.Value);

        Assert.Equal("Workbench Structure Validation", surface.ProjectName);
        Assert.Contains(surface.Nodes, node => node.ObjectType == ProjectObjectType.ProjectRoot);
        Assert.Contains(surface.Nodes, node => node.ObjectType == ProjectObjectType.Phase && node.Title == "Discovery");
        Assert.Contains(surface.Links, link => link.Kind == ProjectObjectLinkKind.Contains);
    }

    [Fact]
    public async Task GetCalendarAsync_returns_phase_events_for_sqlite_projects()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var saveResult = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = "Workbench Calendar Validation",
            Description = "Calendar projection smoke test.",
            Objective = "Ensure calendar surfaces load from SQLite.",
            CurrentPhase = "Execution",
            Phases =
            [
                new ProjectPhaseEditorModel
                {
                    Name = "Execution",
                    Goal = "Deliver the repaired build.",
                    Status = ProjectPhaseStatus.Active,
                    StartDateUtc = new DateTime(2026, 3, 22),
                    EndDateUtc = new DateTime(2026, 3, 24)
                }
            ]
        });

        Assert.True(saveResult.IsSuccess);
        Assert.NotEqual(Guid.Empty, saveResult.Value);
        var surface = await workbench.GetCalendarAsync(saveResult.Value);

        var phaseEvent = Assert.Single(surface.Events);
        Assert.Equal("Execution", phaseEvent.Title);
        Assert.Equal(ProjectObjectType.Phase, phaseEvent.ObjectType);
    }

    [Fact]
    public async Task GetStructureAsync_recreates_missing_workbench_tables_for_existing_sqlite_databases()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var saveResult = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = "Workbench Schema Repair",
            Description = "Rebuild missing workbench tables in-place.",
            Objective = "Keep existing SQLite data usable after adding workbench persistence.",
            CurrentPhase = "Recovery"
        });

        Assert.True(saveResult.IsSuccess);

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            await dbContext.Database.ExecuteSqlRawAsync("""DROP TABLE IF EXISTS "Workbench_ProjectObjectLinks";""");
            await dbContext.Database.ExecuteSqlRawAsync("""DROP TABLE IF EXISTS "Workbench_ProjectObjects";""");
            await dbContext.Database.ExecuteSqlRawAsync("""DROP TABLE IF EXISTS "Workbench_ViewStates";""");
        }

        var surface = await workbench.GetStructureAsync(saveResult.Value);

        Assert.Equal("Workbench Schema Repair", surface.ProjectName);
        Assert.Contains(surface.Nodes, node => node.ObjectType == ProjectObjectType.ProjectRoot);

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var tableNames = await verificationContext.Database
            .SqlQueryRaw<string>(
                """
                SELECT "name"
                FROM "sqlite_master"
                WHERE "type" = 'table'
                  AND "name" IN ('Workbench_ProjectObjects', 'Workbench_ProjectObjectLinks', 'Workbench_ViewStates');
                """)
            .ToListAsync();

        Assert.Contains("Workbench_ProjectObjects", tableNames);
        Assert.Contains("Workbench_ProjectObjectLinks", tableNames);
        Assert.Contains("Workbench_ViewStates", tableNames);
    }

    [Fact]
    public async Task UpdateObjectAsync_persists_inline_note_text_for_custom_nodes()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var saveResult = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = "Workbench Inline Note",
            Description = "Exercise inline note updates.",
            Objective = "Persist canvas-authored note text.",
            CurrentPhase = "Discovery"
        });

        Assert.True(saveResult.IsSuccess);

        var created = await workbench.CreateObjectAsync(
            saveResult.Value,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "New note",
                string.Empty,
                "Original text",
                $"project:{saveResult.Value}",
                320,
                240));

        var updated = await workbench.UpdateObjectAsync(
            saveResult.Value,
            created.Id,
            "Updated inline note",
            string.Empty,
            "Updated inline note text");

        Assert.NotNull(updated);
        Assert.Equal("Updated inline note", updated!.Title);
        Assert.Equal("Updated inline note text", updated.Notes);

        var surface = await workbench.GetStructureAsync(saveResult.Value);
        var note = Assert.Single(surface.Nodes, node => node.Id == created.Id);
        Assert.Equal("Updated inline note", note.Title);
        Assert.Equal("Updated inline note text", note.Notes);
    }

    [Fact]
    public async Task CreateObjectAsync_links_prompt_flow_nodes_to_blank_prompt_sessions()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var promptFactory = scope.ServiceProvider.GetRequiredService<PromptFactoryService>();

        var saveResult = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = "Prompt Flow Node",
            Description = "Exercise prompt-flow linking from project structure.",
            Objective = "Create a reusable prompt flow from the project canvas.",
            CurrentPhase = "Discovery"
        });

        Assert.True(saveResult.IsSuccess);

        var created = await workbench.CreateObjectAsync(
            saveResult.Value,
            new ProjectObjectCreateRequest(
                ProjectObjectType.PromptFlow,
                "Feature intake flow",
                "Capture the feature framing",
                "Created from the project structure canvas.",
                $"project:{saveResult.Value}",
                480,
                320));

        Assert.StartsWith("/prompt-factory?sessionId=", created.Route, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("prompt-session", created.ArtifactKind);
        Assert.True(created.ArtifactId.HasValue);

        var editor = await promptFactory.GetEditorAsync(created.ArtifactId.Value);
        Assert.Equal(saveResult.Value, editor.ProjectId);
        Assert.Equal("Feature intake flow", editor.SessionName);
        Assert.Equal("Discovery", editor.Phase);
        Assert.Null(editor.FlowTemplateId);
        Assert.Empty(editor.SelectedBlockIds);
        Assert.Empty(editor.Nodes);

        var surface = await workbench.GetStructureAsync(saveResult.Value);
        var flowNode = Assert.Single(surface.Nodes, node => node.Id == created.Id);
        Assert.Equal(created.Route, flowNode.Route);
        Assert.Equal("prompt-session", flowNode.ArtifactKind);
    }

    [Fact]
    public async Task ExecuteNodeCommandAsync_wizard_repairs_legacy_prompt_flow_routes()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var saveResult = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = "Prompt Flow Repair",
            Description = "Repair prompt-flow routing for legacy structure nodes.",
            Objective = "Open the prompt wizard from project structure even when the node predates the feature.",
            CurrentPhase = "Execution"
        });

        Assert.True(saveResult.IsSuccess);

        var created = await workbench.CreateObjectAsync(
            saveResult.Value,
            new ProjectObjectCreateRequest(
                ProjectObjectType.PromptFlow,
                "Repairable flow",
                "Legacy route",
                "Simulate an existing prompt flow node.",
                $"project:{saveResult.Value}",
                520,
                280));

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var record = await dbContext.Set<ProjectObjectRecord>()
                .FirstAsync(item => item.ProjectId == saveResult.Value && item.NodeKey == created.Id);
            record.Route = $"/projects/{saveResult.Value}/structure";
            record.ExternalArtifactKind = ProjectObjectType.PromptFlow.ToString();
            record.ExternalArtifactId = null;
            await dbContext.SaveChangesAsync();
        }

        var artifact = await workbench.ExecuteNodeCommandAsync(saveResult.Value, created.Id, ProjectStructureCommandKind.Wizard);

        Assert.NotNull(artifact);
        Assert.StartsWith("/prompt-factory?sessionId=", artifact!.Route, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("prompt-session", artifact.Kind);

        var surface = await workbench.GetStructureAsync(saveResult.Value);
        var flowNode = Assert.Single(surface.Nodes, node => node.Id == created.Id);
        Assert.StartsWith("/prompt-factory?sessionId=", flowNode.Route, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("prompt-session", flowNode.ArtifactKind);
        Assert.True(flowNode.ArtifactId.HasValue);
    }
}
