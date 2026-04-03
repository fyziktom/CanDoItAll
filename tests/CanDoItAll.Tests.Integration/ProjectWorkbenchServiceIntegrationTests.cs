using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Infrastructure.Storage;
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
    public async Task UpdateObjectAsync_persists_typed_metadata_and_schedule_for_custom_nodes()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var saveResult = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = "Workbench Typed Edit",
            Description = "Exercise typed edit persistence.",
            Objective = "Persist metadata and schedule changes for editable nodes.",
            CurrentPhase = "Execution"
        });

        Assert.True(saveResult.IsSuccess);

        var initialMetadata = ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
        {
            Meeting = new ProjectMeetingMetadata
            {
                Address = "Old office",
                MeetingUrl = "https://old.example.com",
                RepeatCadence = ProjectRepeatCadence.Weekly
            }
        });

        var created = await workbench.CreateObjectAsync(
            saveResult.Value,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Meeting,
                "Client workshop",
                "Discovery",
                "Original workshop note.",
                $"project:{saveResult.Value}",
                320,
                240,
                new DateTimeOffset(2026, 4, 1, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.Zero),
                "onsite",
                null,
                initialMetadata));

        var updatedMetadata = ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
        {
            Meeting = new ProjectMeetingMetadata
            {
                Address = "New office",
                MeetingUrl = "https://meet.example.com/workshop",
                RepeatCadence = ProjectRepeatCadence.Monthly
            }
        });

        var updated = await workbench.UpdateObjectAsync(
            saveResult.Value,
            created.Id,
            new ProjectObjectEditRequest(
                "Client workshop updated",
                "Refined agenda",
                "Updated workshop note.",
                new DateTimeOffset(2026, 4, 2, 13, 30, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 4, 2, 15, 0, 0, TimeSpan.Zero),
                updatedMetadata));

        Assert.NotNull(updated);
        Assert.Equal("Client workshop updated", updated!.Title);
        Assert.Equal("Refined agenda", updated.Subtitle);
        Assert.Equal("Updated workshop note.", updated.Notes);
        Assert.Equal(new DateTimeOffset(2026, 4, 2, 13, 30, 0, TimeSpan.Zero), updated.StartUtc);
        Assert.Equal(new DateTimeOffset(2026, 4, 2, 15, 0, 0, TimeSpan.Zero), updated.EndUtc);

        var surface = await workbench.GetStructureAsync(saveResult.Value);
        var meeting = Assert.Single(surface.Nodes, node => node.Id == created.Id);
        Assert.Equal("Client workshop updated", meeting.Title);
        Assert.Equal("Refined agenda", meeting.Subtitle);
        Assert.Equal("Updated workshop note.", meeting.Notes);
        Assert.Equal(new DateTimeOffset(2026, 4, 2, 13, 30, 0, TimeSpan.Zero), meeting.StartUtc);
        Assert.Equal(new DateTimeOffset(2026, 4, 2, 15, 0, 0, TimeSpan.Zero), meeting.EndUtc);

        var parsedMetadata = ProjectObjectMetadataSerializer.Parse(meeting.MetadataJson);
        Assert.NotNull(parsedMetadata.Meeting);
        Assert.Equal("New office", parsedMetadata.Meeting!.Address);
        Assert.Equal("https://meet.example.com/workshop", parsedMetadata.Meeting.MeetingUrl);
        Assert.Equal(ProjectRepeatCadence.Monthly, parsedMetadata.Meeting.RepeatCadence);
    }

    [Fact]
    public async Task CreateAndUpdateObjectAsync_persists_duration_seconds_for_custom_nodes()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var projectId = await CreateProjectAsync(projects, "Duration persistence");
        var startUtc = new DateTimeOffset(2026, 4, 2, 9, 0, 0, TimeSpan.Zero);

        var created = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Timed note",
                string.Empty,
                "Seed duration through the workbench service.",
                $"project:{projectId}",
                420,
                280,
                startUtc,
                null,
                null,
                null,
                null,
                5400));

        Assert.Equal(startUtc, created.StartUtc);
        Assert.Equal(startUtc.AddMinutes(90), created.EndUtc);
        Assert.Equal(5400, created.DurationSeconds);

        var updated = await workbench.UpdateObjectAsync(
            projectId,
            created.Id,
            new ProjectObjectEditRequest(
                "Timed note updated",
                string.Empty,
                "Adjust the prepared duration value.",
                startUtc.AddHours(1),
                null,
                created.MetadataJson,
                7200));

        Assert.NotNull(updated);
        Assert.Equal(startUtc.AddHours(1), updated!.StartUtc);
        Assert.Equal(startUtc.AddHours(3), updated.EndUtc);
        Assert.Equal(7200, updated.DurationSeconds);

        var surface = await workbench.GetStructureAsync(projectId);
        var persistedNode = Assert.Single(surface.Nodes, node => node.Id == created.Id);
        Assert.Equal(7200, persistedNode.DurationSeconds);
        Assert.Equal(startUtc.AddHours(3), persistedNode.EndUtc);
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

    [Fact]
    public async Task CreateObjectAsync_persists_uploaded_file_nodes_as_managed_attachments()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var saveResult = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = "Managed Attachment Node",
            Description = "Persist an uploaded file on the structure canvas.",
            Objective = "Keep file metadata and the managed-files route intact.",
            CurrentPhase = "Review"
        });

        Assert.True(saveResult.IsSuccess);

        var created = await workbench.CreateObjectAsync(
            saveResult.Value,
            new ProjectObjectCreateRequest(
                ProjectObjectType.File,
                "Release checklist",
                "PDF attachment",
                "Persist file metadata",
                $"project:{saveResult.Value}",
                460,
                260,
                null,
                null,
                string.Empty,
                new ProjectObjectMediaPayload(
                    "release-checklist.pdf",
                    "application/pdf",
                    Convert.ToBase64String("%PDF-1.4 release checklist"u8.ToArray()))));

        Assert.StartsWith("/storage/objects/preview?ref=", created.Route, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("release-checklist.pdf", created.MediaOriginalFileName);
        Assert.Equal("application/pdf", created.MediaContentType);
        Assert.True(StorageJson.TryParseReference(created.StorageObjectReferenceJson, out var createdReference));
        Assert.NotNull(createdReference);
        Assert.Equal(StorageProviderKind.FileSystem, createdReference!.ProviderKind);
        Assert.Equal(created.MediaRelativePath, createdReference.Locator);

        var surface = await workbench.GetStructureAsync(saveResult.Value);
        var fileNode = Assert.Single(surface.Nodes, node => node.Id == created.Id);
        Assert.Equal(created.Route, fileNode.Route);
        Assert.Equal("release-checklist.pdf", fileNode.MediaOriginalFileName);
        Assert.Equal("application/pdf", fileNode.MediaContentType);
        Assert.True(StorageJson.TryParseReference(fileNode.StorageObjectReferenceJson, out var nodeReference));
        Assert.NotNull(nodeReference);
        Assert.Equal(StorageProviderKind.FileSystem, nodeReference!.ProviderKind);
        Assert.Contains("Uploaded", fileNode.Badges);
    }

    [Fact]
    public async Task CreateObjectAsync_and_ReparentObjectAsync_attach_detached_nodes_to_the_project_root()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var saveResult = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = "Workbench Root Parenting",
            Description = "Keep root-level nodes connected to the project root.",
            Objective = "Prevent detached nodes from floating without hierarchy links.",
            CurrentPhase = "Execution"
        });

        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;
        var projectRootNodeKey = BuildProjectRootNodeKey(projectId);

        var created = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Top-level note",
                string.Empty,
                "Created without an explicit parent.",
                null,
                420,
                260));

        Assert.Equal(projectRootNodeKey, created.ParentId);

        var surfaceAfterCreate = await workbench.GetStructureAsync(projectId);
        var createdNode = Assert.Single(surfaceAfterCreate.Nodes, node => node.Id == created.Id);
        Assert.Equal(projectRootNodeKey, createdNode.ParentId);
        Assert.Contains(surfaceAfterCreate.Links, link =>
            string.Equals(link.SourceId, projectRootNodeKey, StringComparison.Ordinal) &&
            string.Equals(link.TargetId, created.Id, StringComparison.Ordinal) &&
            link.Kind == ProjectObjectLinkKind.Contains);

        var child = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Nested task",
                string.Empty,
                "Disconnect should return this node to the project root.",
                created.Id,
                640,
                360,
                null,
                null,
                "task"));

        var reparented = await workbench.ReparentObjectAsync(projectId, child.Id, null);

        Assert.NotNull(reparented);
        Assert.Equal(projectRootNodeKey, reparented!.ParentId);

        var surfaceAfterReparent = await workbench.GetStructureAsync(projectId);
        var movedChild = Assert.Single(surfaceAfterReparent.Nodes, node => node.Id == child.Id);
        Assert.Equal(projectRootNodeKey, movedChild.ParentId);
        Assert.Contains(surfaceAfterReparent.Links, link =>
            string.Equals(link.SourceId, projectRootNodeKey, StringComparison.Ordinal) &&
            string.Equals(link.TargetId, child.Id, StringComparison.Ordinal) &&
            link.Kind == ProjectObjectLinkKind.Contains);
        Assert.DoesNotContain(surfaceAfterReparent.Links, link =>
            string.Equals(link.SourceId, created.Id, StringComparison.Ordinal) &&
            string.Equals(link.TargetId, child.Id, StringComparison.Ordinal) &&
            (link.Kind == ProjectObjectLinkKind.BelongsTo || link.Kind == ProjectObjectLinkKind.Contains));
    }

    [Fact]
    public async Task ReparentObjectAsync_moves_nodes_and_DeleteObjectAsync_removes_descendants_and_links()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var saveResult = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = "Workbench Graph Surgery",
            Description = "Exercise reparenting and deletion from the workbench service.",
            Objective = "Keep parent-child structure and links consistent after graph edits.",
            CurrentPhase = "Execution"
        });

        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var firstParent = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "First parent",
                string.Empty,
                "Original parent branch.",
                $"project:{projectId}",
                420,
                220));

        var secondParent = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Second parent",
                string.Empty,
                "Target parent branch.",
                $"project:{projectId}",
                760,
                220));

        var child = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Child node",
                string.Empty,
                "Move and delete this subtree.",
                firstParent.Id,
                560,
                340));

        var grandchild = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Grandchild task",
                string.Empty,
                "Nested descendant that should be deleted together with the child.",
                child.Id,
                760,
                420,
                null,
                null,
                "task"));

        await workbench.LinkObjectsAsync(projectId, secondParent.Id, child.Id, ProjectObjectLinkKind.DependsOn);

        var reparented = await workbench.ReparentObjectAsync(projectId, child.Id, secondParent.Id);

        Assert.NotNull(reparented);
        Assert.Equal(secondParent.Id, reparented!.ParentId);

        var surfaceAfterReparent = await workbench.GetStructureAsync(projectId);
        var movedChild = Assert.Single(surfaceAfterReparent.Nodes, node => node.Id == child.Id);
        Assert.Equal(secondParent.Id, movedChild.ParentId);

        var deletedCount = await workbench.DeleteObjectAsync(projectId, child.Id);

        Assert.Equal(2, deletedCount);

        var surfaceAfterDelete = await workbench.GetStructureAsync(projectId);
        Assert.DoesNotContain(surfaceAfterDelete.Nodes, node => node.Id == child.Id);
        Assert.DoesNotContain(surfaceAfterDelete.Nodes, node => node.Id == grandchild.Id);
        Assert.DoesNotContain(surfaceAfterDelete.Links, link =>
            string.Equals(link.SourceId, child.Id, StringComparison.Ordinal) ||
            string.Equals(link.TargetId, child.Id, StringComparison.Ordinal) ||
            string.Equals(link.SourceId, grandchild.Id, StringComparison.Ordinal) ||
            string.Equals(link.TargetId, grandchild.Id, StringComparison.Ordinal));
    }

    [Fact]
    public async Task UpdateObjectMetadataAsync_persists_transcript_provider_state_and_review_status()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var saveResult = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = "Transcript Metadata Update",
            Description = "Exercise typed metadata updates for transcript nodes.",
            Objective = "Persist transcript provider results without stringly-typed hacks.",
            CurrentPhase = "Review"
        });

        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var initialMetadata = ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
        {
            Transcript = new ProjectTranscriptMetadata
            {
                TranscriptText = "Initial transcript body."
            }
        });

        var transcript = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Transcript,
                "Client transcript",
                "Initial capture",
                "Initial transcript body.",
                $"project:{projectId}",
                520,
                260,
                null,
                null,
                null,
                null,
                initialMetadata));

        var providerId = Guid.NewGuid();
        var updatedMetadata = ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
        {
            Transcript = new ProjectTranscriptMetadata
            {
                TranscriptText = "Alice owes the rollout checklist.",
                SummaryText = "Alice owns the checklist before Friday.",
                MyTasksText = "- Review the rollout checklist",
                OthersDeliveriesText = "- Alice: rollout checklist",
                LastProviderProfileId = providerId,
                LastProviderName = "Local llama",
                LastActionKind = ProjectLlmActionKind.FindMyTasks
            }
        });

        var updated = await workbench.UpdateObjectMetadataAsync(
            projectId,
            transcript.Id,
            updatedMetadata,
            notes: "Alice owes the rollout checklist.",
            status: "Review");

        Assert.NotNull(updated);
        Assert.Equal("Review", updated!.Status);
        Assert.Equal("Alice owes the rollout checklist.", updated.Notes);

        var surface = await workbench.GetStructureAsync(projectId);
        var persistedTranscript = Assert.Single(surface.Nodes, node => node.Id == transcript.Id);
        Assert.Equal("Review", persistedTranscript.Status);
        Assert.Equal("Alice owes the rollout checklist.", persistedTranscript.Notes);

        var parsedMetadata = ProjectObjectMetadataSerializer.Parse(persistedTranscript.MetadataJson);
        Assert.NotNull(parsedMetadata.Transcript);
        Assert.Equal(providerId, parsedMetadata.Transcript!.LastProviderProfileId);
        Assert.Equal("Local llama", parsedMetadata.Transcript.LastProviderName);
        Assert.Equal(ProjectLlmActionKind.FindMyTasks, parsedMetadata.Transcript.LastActionKind);
        Assert.Equal("- Review the rollout checklist", parsedMetadata.Transcript.MyTasksText);
        Assert.Equal("- Alice: rollout checklist", parsedMetadata.Transcript.OthersDeliveriesText);
    }

    [Fact]
    public async Task GetStructureAsync_projects_hierarchy_adds_parent_subproject_and_shared_parent_nodes()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var currentProjectId = await CreateProjectAsync(projects, "Current project");
        var directParentProjectId = await CreateProjectAsync(projects, "Direct parent");
        var subprojectId = await CreateProjectAsync(projects, "Direct child");
        var sharedParentProjectId = await CreateProjectAsync(projects, "Shared parent");

        Assert.True((await projects.AddSubprojectAsync(directParentProjectId, currentProjectId)).IsSuccess);
        Assert.True((await projects.AddSubprojectAsync(currentProjectId, subprojectId)).IsSuccess);
        Assert.True((await projects.AddSubprojectAsync(sharedParentProjectId, subprojectId)).IsSuccess);

        var surface = await workbench.GetStructureAsync(currentProjectId);

        var projectRoot = Assert.Single(surface.Nodes, node => node.ProjectRole == ProjectStructureProjectRole.ActiveProject);
        var parentNode = Assert.Single(surface.Nodes, node =>
            node.ProjectRole == ProjectStructureProjectRole.ParentProject &&
            node.RelatedProjectId == directParentProjectId);
        var subprojectNode = Assert.Single(surface.Nodes, node =>
            node.ProjectRole == ProjectStructureProjectRole.Subproject &&
            node.RelatedProjectId == subprojectId);
        var sharedParentNode = Assert.Single(surface.Nodes, node =>
            node.ProjectRole == ProjectStructureProjectRole.AdditionalParentProject &&
            node.RelatedProjectId == sharedParentProjectId);

        Assert.Equal(2, subprojectNode.ParentProjectCount);
        Assert.Contains("2 parents", subprojectNode.Badges);
        Assert.EndsWith($"/projects/{subprojectId}/structure", subprojectNode.Route, StringComparison.OrdinalIgnoreCase);

        Assert.Contains(surface.Links, link =>
            string.Equals(link.SourceId, parentNode.Id, StringComparison.Ordinal) &&
            string.Equals(link.TargetId, projectRoot.Id, StringComparison.Ordinal) &&
            link.Kind == ProjectObjectLinkKind.BelongsTo);
        Assert.Contains(surface.Links, link =>
            string.Equals(link.SourceId, projectRoot.Id, StringComparison.Ordinal) &&
            string.Equals(link.TargetId, subprojectNode.Id, StringComparison.Ordinal) &&
            link.Kind == ProjectObjectLinkKind.Contains);
        Assert.Contains(surface.Links, link =>
            string.Equals(link.SourceId, sharedParentNode.Id, StringComparison.Ordinal) &&
            string.Equals(link.TargetId, subprojectNode.Id, StringComparison.Ordinal) &&
            link.Kind == ProjectObjectLinkKind.BelongsTo);
    }

    [Fact]
    public async Task GetStructureAsync_projects_hierarchy_keeps_recursive_descendants_and_uses_visible_project_nodes_for_extra_parents()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var currentProjectId = await CreateProjectAsync(projects, "Current project");
        var alphaChildProjectId = await CreateProjectAsync(projects, "Alpha child");
        var betaBranchProjectId = await CreateProjectAsync(projects, "Beta branch");
        var sharedGrandchildProjectId = await CreateProjectAsync(projects, "Shared grandchild");

        Assert.True((await projects.AddSubprojectAsync(currentProjectId, alphaChildProjectId)).IsSuccess);
        Assert.True((await projects.AddSubprojectAsync(currentProjectId, betaBranchProjectId)).IsSuccess);
        Assert.True((await projects.AddSubprojectAsync(alphaChildProjectId, sharedGrandchildProjectId)).IsSuccess);
        Assert.True((await projects.AddSubprojectAsync(betaBranchProjectId, sharedGrandchildProjectId)).IsSuccess);

        var surface = await workbench.GetStructureAsync(currentProjectId);

        var alphaChildNode = Assert.Single(surface.Nodes, node =>
            node.ProjectRole == ProjectStructureProjectRole.Subproject &&
            node.RelatedProjectId == alphaChildProjectId);
        var betaBranchNode = Assert.Single(surface.Nodes, node =>
            node.ProjectRole == ProjectStructureProjectRole.Subproject &&
            node.RelatedProjectId == betaBranchProjectId);
        var sharedGrandchildNode = Assert.Single(surface.Nodes, node =>
            node.ProjectRole == ProjectStructureProjectRole.Subproject &&
            node.RelatedProjectId == sharedGrandchildProjectId);

        Assert.Equal(alphaChildNode.Id, sharedGrandchildNode.ParentId);
        Assert.Equal(2, sharedGrandchildNode.ParentProjectCount);
        Assert.Contains("2 parents", sharedGrandchildNode.Badges);
        Assert.DoesNotContain(surface.Nodes, node =>
            node.ProjectRole == ProjectStructureProjectRole.AdditionalParentProject &&
            node.RelatedProjectId == betaBranchProjectId);
        Assert.Contains(surface.Links, link =>
            string.Equals(link.SourceId, alphaChildNode.Id, StringComparison.Ordinal) &&
            string.Equals(link.TargetId, sharedGrandchildNode.Id, StringComparison.Ordinal) &&
            link.Kind == ProjectObjectLinkKind.Contains);
        Assert.Contains(surface.Links, link =>
            string.Equals(link.SourceId, betaBranchNode.Id, StringComparison.Ordinal) &&
            string.Equals(link.TargetId, sharedGrandchildNode.Id, StringComparison.Ordinal) &&
            link.Kind == ProjectObjectLinkKind.BelongsTo);
    }

    [Fact]
    public async Task ReclassifyObjectAsync_converts_notes_to_common_blocks()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var saveResult = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = "Workbench Reclassification",
            Description = "Exercise note to common block conversion.",
            Objective = "Persist typed block mutations.",
            CurrentPhase = "Execution"
        });

        Assert.True(saveResult.IsSuccess);

        const string noteBody = "Deploy gateway\r\nRemember WiFi coverage";
        var created = await workbench.CreateObjectAsync(
            saveResult.Value,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Scratch note",
                string.Empty,
                noteBody,
                $"project:{saveResult.Value}",
                320,
                240));

        var updated = await workbench.ReclassifyObjectAsync(
            saveResult.Value,
            created.Id,
            new ProjectObjectReclassificationRequest(
                ProjectObjectType.ProjectBlock,
                "deployment",
                "Deploy gateway",
                string.Empty,
                noteBody,
                "{}"));

        Assert.NotNull(updated);
        Assert.Equal(ProjectObjectType.ProjectBlock, updated!.ObjectType);
        Assert.Equal("deployment", updated.ObjectSubtype);
        Assert.Equal("Deploy gateway", updated.Title);
        Assert.Equal(noteBody, updated.Notes);

        var surface = await workbench.GetStructureAsync(saveResult.Value);
        var persistedNode = Assert.Single(surface.Nodes, node => string.Equals(node.Id, created.Id, StringComparison.Ordinal));
        Assert.Equal(ProjectObjectType.ProjectBlock, persistedNode.ObjectType);
        Assert.Equal("deployment", persistedNode.ObjectSubtype);
        Assert.Equal("Deploy gateway", persistedNode.Title);
        Assert.Equal(noteBody, persistedNode.Notes);
        Assert.Equal(ProjectObjectPaletteKeys.Info, persistedNode.VisualProfile.PaletteKey);
    }

    [Fact]
    public async Task UnlinkObjectsAsync_removes_user_authored_dependency_links()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var projectId = await CreateProjectAsync(projects, "Dependency unlink");
        var prerequisite = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Prepared note",
                string.Empty,
                "A note can act as a dependency too.",
                $"project:{projectId}",
                360,
                240));
        var dependent = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Execute work",
                string.Empty,
                "Blocked until the note is done.",
                $"project:{projectId}",
                640,
                360,
                null,
                null,
                "task"));

        await workbench.LinkObjectsAsync(projectId, dependent.Id, prerequisite.Id, ProjectObjectLinkKind.DependsOn);

        var beforeUnlink = await workbench.GetStructureAsync(projectId);
        Assert.Contains(beforeUnlink.Links, link =>
            string.Equals(link.SourceId, dependent.Id, StringComparison.Ordinal) &&
            string.Equals(link.TargetId, prerequisite.Id, StringComparison.Ordinal) &&
            link.Kind == ProjectObjectLinkKind.DependsOn);

        var removed = await workbench.UnlinkObjectsAsync(projectId, dependent.Id, prerequisite.Id, ProjectObjectLinkKind.DependsOn);

        Assert.True(removed);

        var afterUnlink = await workbench.GetStructureAsync(projectId);
        Assert.DoesNotContain(afterUnlink.Links, link =>
            string.Equals(link.SourceId, dependent.Id, StringComparison.Ordinal) &&
            string.Equals(link.TargetId, prerequisite.Id, StringComparison.Ordinal) &&
            link.Kind == ProjectObjectLinkKind.DependsOn);
    }

    [Fact]
    public async Task MoveDescendantsToProjectAsync_moves_subtrees_into_the_target_project_root()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var sourceResult = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = "Source project",
            Description = "Owns the subtree before extraction.",
            Objective = "Move descendants into a new subproject.",
            CurrentPhase = "Execution"
        });
        var targetResult = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = "Target project",
            Description = "Receives the extracted subtree.",
            Objective = "Receive descendants under its root.",
            CurrentPhase = "Discovery"
        });

        Assert.True(sourceResult.IsSuccess);
        Assert.True(targetResult.IsSuccess);

        var sourceProjectId = sourceResult.Value;
        var targetProjectId = targetResult.Value;
        var parentBlock = await workbench.CreateObjectAsync(
            sourceProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Network migration",
                "Source branch",
                "Parent block stays in the source project.",
                $"project:{sourceProjectId}",
                320,
                180,
                null,
                null,
                "computer"));
        var childNote = await workbench.CreateObjectAsync(
            sourceProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Gateway note",
                string.Empty,
                "Move this branch to the target project.",
                parentBlock.Id,
                520,
                240));
        var nestedTask = await workbench.CreateObjectAsync(
            sourceProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Cut over DNS",
                "Nested work",
                "This child should keep its parent within the moved subtree.",
                childNote.Id,
                720,
                300,
                null,
                null,
                "task"));
        var wifiBlock = await workbench.CreateObjectAsync(
            sourceProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Guest WiFi",
                "Subnet",
                "Move this sibling too.",
                parentBlock.Id,
                560,
                380,
                null,
                null,
                "wifi"));
        await workbench.LinkObjectsAsync(sourceProjectId, childNote.Id, wifiBlock.Id, ProjectObjectLinkKind.Uses);

        var transfer = await workbench.MoveDescendantsToProjectAsync(sourceProjectId, parentBlock.Id, targetProjectId);

        Assert.NotNull(transfer);
        Assert.Equal(targetProjectId, transfer!.TargetProjectId);
        Assert.Equal(3, transfer.MovedNodeCount);
        Assert.Equal(2, transfer.MovedRootCount);

        var sourceSurface = await workbench.GetStructureAsync(sourceProjectId);
        Assert.Contains(sourceSurface.Nodes, node => string.Equals(node.Id, parentBlock.Id, StringComparison.Ordinal));
        Assert.DoesNotContain(sourceSurface.Nodes, node => string.Equals(node.Id, childNote.Id, StringComparison.Ordinal));
        Assert.DoesNotContain(sourceSurface.Nodes, node => string.Equals(node.Id, nestedTask.Id, StringComparison.Ordinal));
        Assert.DoesNotContain(sourceSurface.Nodes, node => string.Equals(node.Id, wifiBlock.Id, StringComparison.Ordinal));
        Assert.DoesNotContain(sourceSurface.Links, link =>
            string.Equals(link.SourceId, childNote.Id, StringComparison.Ordinal) ||
            string.Equals(link.TargetId, childNote.Id, StringComparison.Ordinal) ||
            string.Equals(link.SourceId, wifiBlock.Id, StringComparison.Ordinal) ||
            string.Equals(link.TargetId, wifiBlock.Id, StringComparison.Ordinal) ||
            string.Equals(link.SourceId, nestedTask.Id, StringComparison.Ordinal) ||
            string.Equals(link.TargetId, nestedTask.Id, StringComparison.Ordinal));

        var targetSurface = await workbench.GetStructureAsync(targetProjectId);
        var movedNote = Assert.Single(targetSurface.Nodes, node => string.Equals(node.Id, childNote.Id, StringComparison.Ordinal));
        var movedTask = Assert.Single(targetSurface.Nodes, node => string.Equals(node.Id, nestedTask.Id, StringComparison.Ordinal));
        var movedWifi = Assert.Single(targetSurface.Nodes, node => string.Equals(node.Id, wifiBlock.Id, StringComparison.Ordinal));
        Assert.Equal(BuildProjectRootNodeKey(targetProjectId), movedNote.ParentId);
        Assert.Equal(childNote.Id, movedTask.ParentId);
        Assert.Equal(BuildProjectRootNodeKey(targetProjectId), movedWifi.ParentId);
        Assert.Contains(targetSurface.Links, link =>
            string.Equals(link.SourceId, BuildProjectRootNodeKey(targetProjectId), StringComparison.Ordinal) &&
            string.Equals(link.TargetId, childNote.Id, StringComparison.Ordinal) &&
            link.IsUserAuthored);
        Assert.Contains(targetSurface.Links, link =>
            string.Equals(link.SourceId, BuildProjectRootNodeKey(targetProjectId), StringComparison.Ordinal) &&
            string.Equals(link.TargetId, wifiBlock.Id, StringComparison.Ordinal) &&
            link.IsUserAuthored);
        Assert.Contains(targetSurface.Links, link =>
            string.Equals(link.SourceId, childNote.Id, StringComparison.Ordinal) &&
            string.Equals(link.TargetId, wifiBlock.Id, StringComparison.Ordinal) &&
            link.Kind == ProjectObjectLinkKind.Uses &&
            link.IsUserAuthored);
        Assert.DoesNotContain(targetSurface.Links, link =>
            string.Equals(link.SourceId, parentBlock.Id, StringComparison.Ordinal) ||
            string.Equals(link.TargetId, parentBlock.Id, StringComparison.Ordinal));
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = $"{name} description",
            Objective = $"{name} objective",
            CurrentPhase = "Execution"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static string BuildProjectRootNodeKey(Guid projectId)
        => $"project:{projectId}";
}
