using System.Data;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class PromptFactoryServiceIntegrationTests
{
    [Fact]
    public async Task GetEditorAsync_repairs_legacy_factory_schema_and_seeds_missing_templates()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<PromptFactoryService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var legacyBlockId = Guid.NewGuid();

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            await dbContext.Database.ExecuteSqlRawAsync("""DROP TABLE IF EXISTS "Factory_PromptBuildSessions";""");
            await dbContext.Database.ExecuteSqlRawAsync("""DROP TABLE IF EXISTS "Factory_PromptRunNodes";""");
            await dbContext.Database.ExecuteSqlRawAsync("""DROP TABLE IF EXISTS "Factory_PromptRuns";""");
            await dbContext.Database.ExecuteSqlRawAsync("""DROP TABLE IF EXISTS "Factory_PromptBlueprints";""");
            await dbContext.Database.ExecuteSqlRawAsync("""DROP TABLE IF EXISTS "Factory_PromptFlowTemplates";""");
            await dbContext.Database.ExecuteSqlRawAsync("""DROP TABLE IF EXISTS "Factory_PromptBlocks";""");

            await dbContext.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE "Factory_PromptBlocks" (
                    "Id" TEXT NOT NULL PRIMARY KEY,
                    "Name" TEXT NOT NULL,
                    "BlockKind" INTEGER NOT NULL,
                    "Summary" TEXT NOT NULL DEFAULT '',
                    "Content" TEXT NOT NULL DEFAULT '',
                    "IsRecommendedByDefault" INTEGER NOT NULL DEFAULT 0
                );
                """);

            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO "Factory_PromptBlocks" ("Id", "Name", "BlockKind", "Summary", "Content", "IsRecommendedByDefault")
                VALUES ({legacyBlockId}, {"Legacy Block"}, {0}, {"Legacy summary"}, {"Legacy content"}, {1});
                """);
        }

        var editor = await factory.GetEditorAsync(null);
        var blocks = await factory.ListBlocksAsync();
        var templates = await factory.ListTemplatesAsync();
        var blueprints = await factory.ListBlueprintsAsync();

        Assert.NotNull(editor.FlowTemplateId);
        Assert.NotNull(editor.BlueprintId);
        Assert.Contains(blocks, item => item.Id == legacyBlockId && item.Name == "Legacy Block");
        Assert.NotEmpty(templates);
        Assert.NotEmpty(blueprints);

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var blockColumns = await ReadColumnNamesAsync(verificationContext, "Factory_PromptBlocks");
        var templateColumns = await ReadColumnNamesAsync(verificationContext, "Factory_PromptFlowTemplates");

        Assert.Contains("PromptTypeRules", blockColumns);
        Assert.Contains("BlueprintRules", blockColumns);
        Assert.Contains("PhaseRules", blockColumns);
        Assert.Contains("PromptTypeRules", templateColumns);
    }

    [Fact]
    public async Task GetRecommendedBlockIdsAsync_prefers_selected_flow_without_irrelevant_defaults()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<PromptFactoryService>();

        var blueprint = (await factory.ListBlueprintsAsync()).Single(item => item.Key == "architecture-spec");
        var flow = (await factory.ListTemplatesAsync()).Single(item => item.Key == "architecture-review-plan-implement-validate");
        var blocks = await factory.ListBlocksAsync();

        var recommendedIds = await factory.GetRecommendedBlockIdsAsync(blueprint.Id, flow.Id, "architecture");
        var recommendedKeys = blocks
            .Where(item => recommendedIds.Contains(item.Id))
            .Select(item => item.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("role-architecture-lead", recommendedKeys);
        Assert.Contains("role-senior-reviewer", recommendedKeys);
        Assert.Contains("architecture-blueprint", recommendedKeys);
        Assert.DoesNotContain("stack-arduino-firmware", recommendedKeys);
        Assert.DoesNotContain("stack-midi-audio", recommendedKeys);
        Assert.DoesNotContain("role-embedded-midi-engineer", recommendedKeys);
    }

    [Fact]
    public async Task BuildAsync_uses_agent_sequence_for_nodes_and_prompt_output()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<PromptFactoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();

        var projectId = await CreateProjectAsync(projectsService, "Architecture Flow Project");
        var blueprint = (await factory.ListBlueprintsAsync()).Single(item => item.Key == "architecture-spec");
        var flow = (await factory.ListTemplatesAsync()).Single(item => item.Key == "architecture-review-plan-implement-validate");
        var recommendedIds = await factory.GetRecommendedBlockIdsAsync(blueprint.Id, flow.Id, "architecture");

        var result = await factory.BuildAsync(new PromptFactoryEditorModel
        {
            ProjectId = projectId,
            SessionName = "Architecture flow session",
            Phase = "architecture",
            BlueprintId = blueprint.Id,
            FlowTemplateId = flow.Id,
            SelectedBlockIds = recommendedIds.ToList(),
            DraftTitle = "Architecture flow draft",
            CanvasUiStateJson = "{}",
            ComponentCustomizations = [],
            SessionAttachments = []
        });

        Assert.True(result.IsSuccess, string.Join(" ", result.Errors.Select(error => error.Message)));
        var editor = result.Value!;

        Assert.Equal(flow.AgentSequence.Count, editor.Nodes.Count);
        Assert.Contains(editor.Nodes, node => node.Title.Contains("Architecture Lead", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Architecture Lead", editor.GeneratedPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Role framing", editor.GeneratedPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Arduino", editor.GeneratedPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("MIDI", editor.GeneratedPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("embedded environments", editor.GeneratedPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RestoreSessionStateAsync_restores_prior_prompt_step_state()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<PromptFactoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();

        var projectId = await CreateProjectAsync(projectsService, "Prompt History Project");
        var blueprint = (await factory.ListBlueprintsAsync()).Single(item => item.Key == "architecture-spec");
        var flow = (await factory.ListTemplatesAsync()).Single(item => item.Key == "architecture-review-plan-implement-validate");
        var recommendedIds = await factory.GetRecommendedBlockIdsAsync(blueprint.Id, flow.Id, "architecture");

        var buildResult = await factory.BuildAsync(new PromptFactoryEditorModel
        {
            ProjectId = projectId,
            SessionName = "History session",
            Phase = "architecture",
            BlueprintId = blueprint.Id,
            FlowTemplateId = flow.Id,
            SelectedBlockIds = recommendedIds.ToList(),
            DraftTitle = "History draft",
            CanvasUiStateJson = "{}",
            ComponentCustomizations = [],
            SessionAttachments = []
        });

        Assert.True(buildResult.IsSuccess, string.Join(" ", buildResult.Errors.Select(error => error.Message)));
        var snapshot = buildResult.Value!;

        var firstNode = snapshot.Nodes[0];
        var updateResult = await factory.UpdateNodeAsync(firstNode.Id, "Retitled architecture step", "Updated notes for history verification.");
        Assert.True(updateResult.IsSuccess, string.Join(" ", updateResult.Errors.Select(error => error.Message)));

        var modifiedEditor = await factory.GetEditorAsync(snapshot.SessionId);
        Assert.Equal("Retitled architecture step", modifiedEditor.Nodes[0].Title);

        var restoreResult = await factory.RestoreSessionStateAsync(snapshot);
        Assert.True(restoreResult.IsSuccess, string.Join(" ", restoreResult.Errors.Select(error => error.Message)));

        var restoredEditor = restoreResult.Value!;
        Assert.Equal(firstNode.Title, restoredEditor.Nodes[0].Title);
        Assert.Equal(firstNode.Notes, restoredEditor.Nodes[0].Notes);
        Assert.Equal(flow.AgentSequence.Count, restoredEditor.Nodes.Count);
    }

    [Fact]
    public async Task PrepareAttachmentAsync_persists_storage_reference_backed_preview_routes()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<PromptFactoryService>();

        var prepared = await factory.PrepareAttachmentAsync(
            new PromptSessionAttachmentSummary
            {
                Kind = "file",
                Title = "Release notes"
            },
            new CanvasWorkbenchUploadedFile
            {
                FileName = "release-notes.pdf",
                ContentType = "application/pdf",
                Base64Data = Convert.ToBase64String("%PDF-1.4 release notes"u8.ToArray())
            });

        Assert.StartsWith("/storage/objects/preview?ref=", prepared.MediaRoute, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("release-notes.pdf", prepared.MediaOriginalFileName);
        Assert.Equal("application/pdf", prepared.MediaContentType);
        Assert.True(StorageJson.TryParseReference(prepared.StorageObjectReferenceJson, out var reference));
        Assert.NotNull(reference);
        Assert.Equal(StorageProviderKind.FileSystem, reference!.ProviderKind);
        Assert.Equal(prepared.MediaRelativePath, reference.Locator);
    }

    [Fact]
    public async Task ExportAsync_routes_prompt_exports_through_storage_placement()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<PromptFactoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workspacePathResolver = scope.ServiceProvider.GetRequiredService<IWorkspacePathResolver>();

        var projectId = await CreateProjectAsync(projectsService, "Prompt Export Storage");
        var blueprint = (await factory.ListBlueprintsAsync()).Single(item => item.Key == "architecture-spec");
        var flow = (await factory.ListTemplatesAsync()).Single(item => item.Key == "architecture-review-plan-implement-validate");
        var recommendedIds = await factory.GetRecommendedBlockIdsAsync(blueprint.Id, flow.Id, "architecture");

        var exportResult = await factory.ExportAsync(new PromptFactoryEditorModel
        {
            ProjectId = projectId,
            SessionName = "Export session",
            Phase = "architecture",
            BlueprintId = blueprint.Id,
            FlowTemplateId = flow.Id,
            SelectedBlockIds = recommendedIds.ToList(),
            DraftTitle = "Export draft",
            CanvasUiStateJson = "{}",
            ComponentCustomizations = [],
            SessionAttachments = []
        });

        Assert.True(exportResult.IsSuccess, string.Join(" ", exportResult.Errors.Select(error => error.Message)));
        Assert.EndsWith(".md", exportResult.Value, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(
            Path.GetFullPath(workspacePathResolver.ResolveManagedFilesRoot()),
            Path.GetFullPath(exportResult.Value!),
            StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(exportResult.Value));
        var markdown = await File.ReadAllTextAsync(exportResult.Value!);
        Assert.Contains("Architecture Lead", markdown, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<HashSet<string>> ReadColumnNamesAsync(AppDbContext dbContext, string tableName)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;
        if (shouldCloseConnection)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"""PRAGMA table_info("{tableName}");""";

            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                if (!reader.IsDBNull(1))
                {
                    columns.Add(reader.GetString(1));
                }
            }

            return columns;
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var project = await projectsService.GetAsync(null);
        project.Name = name;
        project.Description = $"{name} description";
        project.Objective = $"{name} objective";
        project.CurrentPhase = "architecture";

        var saveResult = await projectsService.SaveAsync(project);
        Assert.True(saveResult.IsSuccess, string.Join(" ", saveResult.Errors.Select(error => error.Message)));
        return saveResult.Value!;
    }
}
