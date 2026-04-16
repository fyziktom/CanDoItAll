using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.Validation;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
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
    public async Task GetStructureAsync_projects_process_definitions_and_runs_into_the_structure_surface()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processes = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var commandService = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchCommandService>();

        var projectId = await CreateProjectAsync(projects, "Workbench process projection");
        var definitionResult = await processes.SaveAsync(BuildProcessDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(definitionResult.IsSuccess);
        Assert.True((await processes.PublishAsync(definitionResult.Value)).IsSuccess);

        var runResult = await processes.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = definitionResult.Value,
            ProjectId = projectId,
            RunName = "Workbench process run",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Structure projection validation"
        });

        Assert.True(runResult.IsSuccess);

        var surface = await workbench.GetStructureAsync(projectId);
        var definitionNode = Assert.Single(surface.Nodes, node => string.Equals(node.Id, BuildProcessDefinitionNodeKey(definitionResult.Value), StringComparison.Ordinal));
        var runNode = Assert.Single(surface.Nodes, node => string.Equals(node.Id, BuildProcessRunNodeKey(runResult.Value), StringComparison.Ordinal));

        Assert.Equal(ProjectObjectType.ProcessDefinition, definitionNode.ObjectType);
        Assert.Equal(ProjectObjectType.ProcessRun, runNode.ObjectType);
        Assert.Equal($"project:{projectId}", definitionNode.ParentId);
        Assert.Equal(definitionNode.Id, runNode.ParentId);
        Assert.Equal($"/projects/{projectId}/processes?processId={definitionResult.Value}", definitionNode.Route);
        Assert.Equal($"/projects/{projectId}/processes?runId={runResult.Value}", runNode.Route);
        Assert.Contains(surface.Links, link =>
            string.Equals(link.SourceId, BuildProjectRootNodeKey(projectId), StringComparison.Ordinal) &&
            string.Equals(link.TargetId, definitionNode.Id, StringComparison.Ordinal) &&
            link.Kind == ProjectObjectLinkKind.Contains);
        Assert.Contains(surface.Links, link =>
            string.Equals(link.SourceId, definitionNode.Id, StringComparison.Ordinal) &&
            string.Equals(link.TargetId, runNode.Id, StringComparison.Ordinal) &&
            link.Kind == ProjectObjectLinkKind.Contains);

        var artifact = await commandService.ExecuteNodeCommandAsync(projectId, runNode.Id, ProjectStructureCommandKind.Open);

        Assert.NotNull(artifact);
        Assert.Equal($"/projects/{projectId}/processes?runId={runResult.Value}", artifact!.Route);
        Assert.Equal(WorkbenchTabKinds.Processes, artifact.TabKind);
    }

    [Fact]
    public async Task TransitionStepAsync_completes_process_bound_workbench_nodes_and_rolls_up_parent_progress()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var processes = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projects, "Workbench process rollup");
        var definitionResult = await processes.SaveAsync(BuildProcessDefinitionEditor(projectId, Guid.NewGuid()));

        Assert.True(definitionResult.IsSuccess);
        Assert.True((await processes.PublishAsync(definitionResult.Value)).IsSuccess);

        var runResult = await processes.StartRunAsync(new ProcessRunStartRequest
        {
            ProcessDefinitionId = definitionResult.Value,
            ProjectId = projectId,
            RunName = "Workbench process rollup run",
            OperatingMode = ProcessOperatingMode.AssistedExecution,
            TriggerReason = "Validate process-bound workbench completion rollup"
        });

        Assert.True(runResult.IsSuccess);

        var phaseNode = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Phase,
                "Agent showcase execution",
                "Phase / process rollup validation",
                "Validate parent rollup when the bound feature process completes.",
                ParentNodeKey: null,
                ObjectSubtype: "showcase-phase"));
        var deliveryNode = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Blazor SSR calculator delivery",
                "Delivery block / process rollup validation",
                "Contains the feature node that is bound to the process run.",
                phaseNode.Id,
                ObjectSubtype: "showcase-delivery-block"));
        var featureNode = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Simple calculator feature",
                "Feature / process-bound work item",
                "Track the process run from the workbench feature lane.",
                deliveryNode.Id,
                ObjectSubtype: "showcase-feature"));

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var recordsByNodeKey = await dbContext.Set<ProjectObjectRecord>()
                .Where(item => item.ProjectId == projectId &&
                    (item.NodeKey == phaseNode.Id ||
                     item.NodeKey == deliveryNode.Id ||
                     item.NodeKey == featureNode.Id))
                .ToDictionaryAsync(item => item.NodeKey);
            Assert.Contains(featureNode.Id, recordsByNodeKey.Keys);
            var featureRecord = recordsByNodeKey[featureNode.Id];
            var binding = await dbContext.Set<ProjectNodeBindingRecord>()
                .SingleAsync(item => item.ProjectObjectId == featureRecord.Id);

            binding.Route = $"/projects/{projectId}/processes?processId={definitionResult.Value}&runId={runResult.Value}";
            binding.ExternalArtifactKind = ProjectObjectType.ProcessRun.ToString();
            binding.ExternalArtifactId = runResult.Value;

            foreach (var record in recordsByNodeKey.Values)
            {
                record.Status = "Blocked";
                record.ProgressMode = "progress";
                record.ProgressPercent = 95;
            }

            await dbContext.SaveChangesAsync();
        }

        var stepRuns = await processes.ListStepRunsAsync(runResult.Value);
        var intakeStep = Assert.Single(stepRuns, item => item.Sequence == 0);

        Assert.True((await processes.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = intakeStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Start the feature intake.",
            DecidedBy = "integration-tests"
        })).IsSuccess);
        Assert.True((await processes.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = intakeStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "Complete the feature intake.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        var reviewStep = Assert.Single(
            await processes.ListStepRunsAsync(runResult.Value),
            item => item.Sequence == 1);
        Guid requiredArtifactExpectationId;
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            requiredArtifactExpectationId = await dbContext.Set<ProcessArtifactExpectation>()
                .Where(item => item.StepDefinitionId == reviewStep.StepDefinitionId)
                .Select(item => item.Id)
                .SingleAsync();
        }

        Assert.True((await processes.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = reviewStep.Id,
            TargetStatus = ProcessStepRunStatus.InProgress,
            Reason = "Start the review step.",
            DecidedBy = "integration-tests"
        })).IsSuccess);
        Assert.True((await processes.RecordArtifactAsync(new ProcessArtifactRecordRequest
        {
            ProcessRunId = runResult.Value,
            StepRunId = reviewStep.Id,
            ArtifactExpectationId = requiredArtifactExpectationId,
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = "Projected structure review evidence",
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            ProvenanceSummary = "Recorded to satisfy the bound workbench rollup test.",
            AllowedFutureUsageSummary = "Integration verification only.",
            ReviewSummary = "Required review evidence is present."
        })).IsSuccess);
        Assert.True((await processes.TransitionStepAsync(new ProcessStepTransitionRequest
        {
            StepRunId = reviewStep.Id,
            TargetStatus = ProcessStepRunStatus.Completed,
            Reason = "Complete the review step.",
            DecidedBy = "integration-tests"
        })).IsSuccess);

        var surface = await workbench.GetStructureAsync(projectId);
        var refreshedPhase = Assert.Single(surface.Nodes, item => string.Equals(item.Id, phaseNode.Id, StringComparison.Ordinal));
        var refreshedDelivery = Assert.Single(surface.Nodes, item => string.Equals(item.Id, deliveryNode.Id, StringComparison.Ordinal));
        var refreshedFeature = Assert.Single(surface.Nodes, item => string.Equals(item.Id, featureNode.Id, StringComparison.Ordinal));

        Assert.Equal("Completed", refreshedFeature.Status);
        Assert.Equal("complete", refreshedFeature.ProgressMode);
        Assert.Equal(100, refreshedFeature.ProgressPercent);
        Assert.Equal("Completed", refreshedDelivery.Status);
        Assert.Equal("complete", refreshedDelivery.ProgressMode);
        Assert.Equal(100, refreshedDelivery.ProgressPercent);
        Assert.Equal("Completed", refreshedPhase.Status);
        Assert.Equal("complete", refreshedPhase.ProgressMode);
        Assert.Equal(100, refreshedPhase.ProgressPercent);

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var project = await verificationContext.Set<Project>()
            .SingleAsync(item => item.Id == projectId);

        Assert.Equal(ProjectStatus.Completed, project.Status);
        Assert.Equal("Completed", project.CurrentPhase);
    }

    [Fact]
    public async Task GetStructureAsync_and_GetCalendarAsync_surface_external_artifacts_without_persisting_projection_rows()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var resourcesService = scope.ServiceProvider.GetRequiredService<ResourcesService>();
        var validationService = scope.ServiceProvider.GetRequiredService<ValidationService>();
        var testLabService = scope.ServiceProvider.GetRequiredService<TestLabService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var saveResult = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = "Workbench Projection Assembly",
            Description = "Exercise projection-only structure and calendar assembly.",
            Objective = "Ensure external artifacts appear without mirrored canonical rows.",
            CurrentPhase = "Execution",
            Phases =
            [
                new ProjectPhaseEditorModel
                {
                    Name = "Execution",
                    Goal = "Assemble structure and calendar surfaces in memory.",
                    Status = ProjectPhaseStatus.Active,
                    StartDateUtc = new DateTime(2026, 4, 2),
                    EndDateUtc = new DateTime(2026, 4, 4)
                }
            ]
        });

        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        var resourceResult = await resourcesService.SaveAsync(new ResourceEditorModel
        {
            ProjectId = projectId,
            ConnectorPluginKey = "resource.folder",
            ConfigSchemaVersion = "1.0",
            Name = "Implementation notes",
            Configuration = new ConnectorConfigState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["folderPath"] = @"C:\repositories\CanDoItAll\docs\implementation"
            }),
            ValidationStatus = ResourceValidationStatus.Valid,
            Sensitivity = ResourceSensitivity.Normal,
            SupportsPreview = true,
            SupportsIndexing = true
        });
        Assert.True(resourceResult.IsSuccess);

        var validationResult = await validationService.RunAsync(new ValidationRunEditorModel
        {
            ProjectId = projectId,
            ValidationType = ValidationType.Architecture,
            ArtifactTitle = "Architecture review",
            ArtifactRoute = "/validation",
            SourceContent = "Validate that workbench projections assemble without persisting mirrored read-model rows."
        });
        Assert.True(validationResult.IsSuccess);

        var testPlanResult = await testLabService.SaveAsync(new TestPlanEditorModel
        {
            ProjectId = projectId,
            Title = "Plugin wave proof",
            Phase = "Execution",
            CoverageGoal = "Cover structure and calendar projection assembly."
        });
        Assert.True(testPlanResult.IsSuccess);

        var structure = await workbench.GetStructureAsync(projectId);
        var calendar = await workbench.GetCalendarAsync(projectId);

        Assert.Contains(structure.Nodes, node => node.ObjectType == ProjectObjectType.ProjectRoot);
        Assert.Contains(structure.Nodes, node => node.ObjectType == ProjectObjectType.Phase && node.Title == "Execution");
        Assert.Contains(structure.Nodes, node => node.Title == "Implementation notes" && node.ArtifactKind == "resource");
        Assert.Contains(structure.Nodes, node => node.Title == "Architecture review" && node.ObjectType == ProjectObjectType.ValidationRun);
        Assert.Contains(structure.Nodes, node => node.Title == "Plugin wave proof" && node.ObjectType == ProjectObjectType.TestPlan);
        Assert.Contains(calendar.Events, item => item.Title == "Execution" && item.ObjectType == ProjectObjectType.Phase);
        Assert.Contains(calendar.Events, item => item.Title == "Architecture review" && item.ObjectType == ProjectObjectType.ValidationRun);
        Assert.Contains(calendar.Events, item => item.Title == "Plugin wave proof" && item.ObjectType == ProjectObjectType.TestPlan);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        Assert.Empty(await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == projectId)
            .ToListAsync());
        Assert.Empty(await dbContext.Set<ProjectObjectLinkRecord>()
            .Where(item => item.ProjectId == projectId)
            .ToListAsync());
        Assert.Empty(await dbContext.Set<ProjectStructureProjectionLayoutRecord>()
            .Where(item => item.ProjectId == projectId)
            .ToListAsync());
    }

    [Fact]
    public async Task MoveObjectsAsync_stores_projection_layout_overrides_without_promoting_projection_nodes_to_canonical_rows()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var resourcesService = scope.ServiceProvider.GetRequiredService<ResourcesService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projects, "Workbench Projection Layout");
        var resourceResult = await resourcesService.SaveAsync(new ResourceEditorModel
        {
            ProjectId = projectId,
            ConnectorPluginKey = "resource.folder",
            ConfigSchemaVersion = "1.0",
            Name = "Design archive",
            Configuration = new ConnectorConfigState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["folderPath"] = @"C:\repositories\CanDoItAll\docs\design"
            }),
            ValidationStatus = ResourceValidationStatus.Valid,
            Sensitivity = ResourceSensitivity.Normal,
            SupportsPreview = true,
            SupportsIndexing = true
        });
        Assert.True(resourceResult.IsSuccess);

        var initialSurface = await workbench.GetStructureAsync(projectId);
        var resourceNode = Assert.Single(initialSurface.Nodes, node => node.Title == "Design archive");
        var movedX = resourceNode.X + 180d;
        var movedY = resourceNode.Y + 95d;

        var movedNodeIds = await workbench.MoveObjectsAsync(
            projectId,
            [new ProjectNodeMoveRequest(resourceNode.Id, movedX, movedY)]);

        Assert.Contains(resourceNode.Id, movedNodeIds);

        var updatedSurface = await workbench.GetStructureAsync(projectId);
        var updatedNode = Assert.Single(updatedSurface.Nodes, node => node.Id == resourceNode.Id);
        Assert.Equal(movedX, updatedNode.X);
        Assert.Equal(movedY, updatedNode.Y);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        Assert.Empty(await dbContext.Set<ProjectObjectRecord>()
            .Where(item => item.ProjectId == projectId)
            .ToListAsync());
        var layout = await dbContext.Set<ProjectStructureProjectionLayoutRecord>()
            .SingleAsync(item => item.ProjectId == projectId && item.NodeKey == resourceNode.Id);
        Assert.Equal(movedX, layout.PositionX);
        Assert.Equal(movedY, layout.PositionY);
    }

    [Fact]
    public async Task MoveObjectsAsync_retries_when_sqlite_workspace_is_temporarily_busy()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var resourcesService = scope.ServiceProvider.GetRequiredService<ResourcesService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projects, "Workbench Busy Retry");
        var resourceResult = await resourcesService.SaveAsync(new ResourceEditorModel
        {
            ProjectId = projectId,
            ConnectorPluginKey = "resource.folder",
            ConfigSchemaVersion = "1.0",
            Name = "Local folder",
            Configuration = new ConnectorConfigState(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["folderPath"] = @"C:\repositories\CanDoItAll\src"
            }),
            ValidationStatus = ResourceValidationStatus.Valid,
            Sensitivity = ResourceSensitivity.Normal,
            SupportsPreview = true,
            SupportsIndexing = true
        });
        Assert.True(resourceResult.IsSuccess);

        var initialSurface = await workbench.GetStructureAsync(projectId);
        var resourceNode = Assert.Single(initialSurface.Nodes, node => node.Title == "Local folder");
        var movedX = resourceNode.X + 160d;
        var movedY = resourceNode.Y + 80d;

        await using var lockContext = await dbContextFactory.CreateDbContextAsync();
        await lockContext.Database.OpenConnectionAsync();

        var transactionOpen = false;
        try
        {
            await using var beginCommand = lockContext.Database.GetDbConnection().CreateCommand();
            beginCommand.CommandText = "BEGIN IMMEDIATE TRANSACTION;";
            await beginCommand.ExecuteNonQueryAsync();
            transactionOpen = true;

            var moveTask = workbench.MoveObjectsAsync(
                projectId,
                [new ProjectNodeMoveRequest(resourceNode.Id, movedX, movedY)]);

            await Task.Delay(110);

            await using var commitCommand = lockContext.Database.GetDbConnection().CreateCommand();
            commitCommand.CommandText = "COMMIT;";
            await commitCommand.ExecuteNonQueryAsync();
            transactionOpen = false;

            var movedNodeIds = await moveTask;
            Assert.Contains(resourceNode.Id, movedNodeIds);
        }
        finally
        {
            if (transactionOpen)
            {
                await using var rollbackCommand = lockContext.Database.GetDbConnection().CreateCommand();
                rollbackCommand.CommandText = "ROLLBACK;";
                await rollbackCommand.ExecuteNonQueryAsync();
            }
        }

        var updatedSurface = await workbench.GetStructureAsync(projectId);
        var updatedNode = Assert.Single(updatedSurface.Nodes, node => node.Id == resourceNode.Id);
        Assert.Equal(movedX, updatedNode.X);
        Assert.Equal(movedY, updatedNode.Y);
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
            await dbContext.Database.ExecuteSqlRawAsync("""DROP TABLE IF EXISTS "Workbench_ProjectNodeBindings";""");
            await dbContext.Database.ExecuteSqlRawAsync("""DROP TABLE IF EXISTS "Workbench_ProjectNodeReferences";""");
            await dbContext.Database.ExecuteSqlRawAsync("""DROP TABLE IF EXISTS "Workbench_ProjectProjectionLayouts";""");
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
                  AND "name" IN ('Workbench_ProjectObjects', 'Workbench_ProjectObjectLinks', 'Workbench_ProjectNodeBindings', 'Workbench_ProjectNodeReferences', 'Workbench_ProjectProjectionLayouts', 'Workbench_ViewStates');
                """)
            .ToListAsync();

        Assert.Contains("Workbench_ProjectObjects", tableNames);
        Assert.Contains("Workbench_ProjectObjectLinks", tableNames);
        Assert.Contains("Workbench_ProjectNodeBindings", tableNames);
        Assert.Contains("Workbench_ProjectNodeReferences", tableNames);
        Assert.Contains("Workbench_ProjectProjectionLayouts", tableNames);
        Assert.Contains("Workbench_ViewStates", tableNames);
    }

    [Fact]
    public async Task GetStructureAsync_normalizes_legacy_carrier_payload_without_changing_coordinates_or_markers()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projects, "Legacy carrier normalization");
        var providerId = Guid.NewGuid();
        var nodeKey = Guid.NewGuid().ToString("D");
        var createdAtUtc = new DateTimeOffset(2026, 4, 4, 18, 30, 0, TimeSpan.Zero);
        var metadataJson = JsonSerializer.Serialize(new
        {
            transcript = new
            {
                transcriptText = "Legacy transcript payload.",
                lastProviderProfileId = providerId,
                lastProviderName = "Offline provider"
            }
        });

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            dbContext.Set<ProjectObjectRecord>().Add(new ProjectObjectRecord
            {
                ProjectId = projectId,
                NodeKey = nodeKey,
                ObjectType = ProjectObjectType.Transcript,
                Title = "Legacy transcript",
                Subtitle = "Pre-binding row",
                Status = "Review",
                Notes = "Normalize this row through the structure load seam.",
                ObjectSubtype = string.Empty,
                ProgressMode = "progress",
                ProgressPercent = 35,
                MarkersJson = """[{"icon":"risk","tone":"danger","label":"Critical"}]""",
                MetadataJson = metadataJson,
                ParentNodeKey = BuildProjectRootNodeKey(projectId),
                PositionX = 610,
                PositionY = 345,
                CreatedAtUtc = createdAtUtc,
                UpdatedAtUtc = createdAtUtc
            });
            dbContext.Set<ProjectObjectLinkRecord>().Add(new ProjectObjectLinkRecord
            {
                ProjectId = projectId,
                SourceNodeKey = BuildProjectRootNodeKey(projectId),
                TargetNodeKey = nodeKey,
                LinkKind = ProjectObjectLinkKind.Contains,
                CreatedAtUtc = createdAtUtc
            });
            await dbContext.SaveChangesAsync();
        }

        var surface = await workbench.GetStructureAsync(projectId);
        var normalizedNode = Assert.Single(surface.Nodes, node => node.Id == nodeKey);

        Assert.Equal(610, normalizedNode.X);
        Assert.Equal(345, normalizedNode.Y);
        Assert.Equal("risk", normalizedNode.MarkerIcon);
        Assert.Equal("danger", normalizedNode.MarkerTone);
        Assert.Equal("Critical", normalizedNode.MarkerLabel);
        Assert.Equal($"/projects/{projectId}/structure", normalizedNode.Route);
        Assert.Equal(ProjectObjectType.Transcript.ToString(), normalizedNode.ArtifactKind);

        var normalizedMetadata = ProjectObjectMetadataSerializer.Parse(normalizedNode.MetadataJson);
        Assert.NotNull(normalizedMetadata.Transcript);
        Assert.NotNull(normalizedNode.NodeReferences);
        Assert.Equal(providerId, normalizedNode.NodeReferences!.TranscriptProviderProfileId);
        Assert.Equal("Offline provider", normalizedMetadata.Transcript.LastProviderName);
        using (var normalizedMetadataDocument = JsonDocument.Parse(normalizedNode.MetadataJson))
        {
            var transcriptElement = normalizedMetadataDocument.RootElement.GetProperty("transcript");
            Assert.False(transcriptElement.TryGetProperty("lastProviderProfileId", out _));
        }

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var carrier = await verificationContext.Set<ProjectObjectRecord>()
            .SingleAsync(item => item.ProjectId == projectId && item.NodeKey == nodeKey);
        Assert.Equal(610, carrier.PositionX);
        Assert.Equal(345, carrier.PositionY);
        Assert.Equal("""[{"icon":"risk","tone":"danger","label":"Critical"}]""", carrier.MarkersJson);

        var persistedMetadata = ProjectObjectMetadataSerializer.Parse(carrier.MetadataJson);
        Assert.NotNull(persistedMetadata.Transcript);
        Assert.Equal("Offline provider", persistedMetadata.Transcript.LastProviderName);
        using (var persistedMetadataDocument = JsonDocument.Parse(carrier.MetadataJson))
        {
            var transcriptElement = persistedMetadataDocument.RootElement.GetProperty("transcript");
            Assert.True(transcriptElement.TryGetProperty("lastProviderProfileId", out _));
        }

        Assert.False(await verificationContext.Set<ProjectNodeBindingRecord>()
            .AnyAsync(item => item.ProjectObjectId == carrier.Id));
        Assert.False(await verificationContext.Set<ProjectNodeReferenceRecord>()
            .AnyAsync(item => item.ProjectObjectId == carrier.Id));
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
            var binding = await dbContext.Set<ProjectNodeBindingRecord>()
                .FirstAsync(item => item.ProjectObjectId == record.Id);
            binding.Route = $"/projects/{saveResult.Value}/structure";
            binding.ExternalArtifactKind = ProjectObjectType.PromptFlow.ToString();
            binding.ExternalArtifactId = null;
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
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

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

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var carrier = await dbContext.Set<ProjectObjectRecord>()
            .SingleAsync(item => item.ProjectId == saveResult.Value && item.NodeKey == created.Id);
        Assert.Equal(created.Id, carrier.NodeKey);

        var binding = await dbContext.Set<ProjectNodeBindingRecord>()
            .SingleAsync(item => item.ProjectObjectId == carrier.Id);
        Assert.Equal(created.Route, binding.Route);
        Assert.Equal(created.ArtifactKind, binding.ExternalArtifactKind);
        Assert.Equal(created.MediaRelativePath, binding.MediaRelativePath);
        Assert.Equal(created.MediaContentType, binding.MediaContentType);
        Assert.Equal(created.MediaOriginalFileName, binding.MediaOriginalFileName);
        Assert.Equal(created.StorageObjectReferenceJson, binding.StorageObjectReferenceJson);
    }

    [Fact]
    public async Task CreateObjectAsync_and_ReparentObjectAsync_attach_detached_nodes_to_the_project_root()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

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
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var persistedHierarchyLinks = await dbContext.Set<ProjectObjectLinkRecord>()
                .Where(item =>
                    item.ProjectId == projectId &&
                    item.TargetNodeKey == created.Id &&
                    !item.IsSystemManaged &&
                    (item.LinkKind == ProjectObjectLinkKind.Contains || item.LinkKind == ProjectObjectLinkKind.BelongsTo))
                .ToListAsync();
            Assert.Empty(persistedHierarchyLinks);
        }

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
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var persistedHierarchyLinks = await dbContext.Set<ProjectObjectLinkRecord>()
                .Where(item =>
                    item.ProjectId == projectId &&
                    item.TargetNodeKey == child.Id &&
                    !item.IsSystemManaged &&
                    (item.LinkKind == ProjectObjectLinkKind.Contains || item.LinkKind == ProjectObjectLinkKind.BelongsTo))
                .ToListAsync();
            Assert.Empty(persistedHierarchyLinks);
        }
    }

    [Fact]
    public async Task CreateObjectAsync_rejects_unknown_parent_keys_and_ReparentObjectAsync_rejects_hierarchy_cycles()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var saveResult = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = "Workbench Guardrails",
            Description = "Exercise explicit parent validation and cycle rejection.",
            Objective = "Reject invalid parent mutations.",
            CurrentPhase = "Execution"
        });

        Assert.True(saveResult.IsSuccess);
        var projectId = saveResult.Value;

        await Assert.ThrowsAsync<InvalidOperationException>(() => workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Orphan note",
                string.Empty,
                "Should reject missing parents.",
                "custom:missing-parent",
                320,
                220)));

        var parent = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Parent",
                string.Empty,
                "Cycle root.",
                null,
                420,
                260));
        var child = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Child",
                string.Empty,
                "Cycle child.",
                parent.Id,
                640,
                340));
        var grandchild = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Grandchild",
                string.Empty,
                "Cycle leaf.",
                child.Id,
                860,
                420,
                null,
                null,
                "task"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => workbench.ReparentObjectAsync(projectId, child.Id, child.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => workbench.ReparentObjectAsync(projectId, parent.Id, grandchild.Id));
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
    public async Task LinkObjectsAsync_rejects_cross_project_edges_and_hierarchy_link_kinds()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var firstProjectResult = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = "Workbench Edge Guardrails A",
            Description = "First project for edge validation.",
            Objective = "Reject cross-project links.",
            CurrentPhase = "Execution"
        });
        var secondProjectResult = await projects.SaveAsync(new ProjectEditorModel
        {
            Name = "Workbench Edge Guardrails B",
            Description = "Second project for edge validation.",
            Objective = "Reject invalid hierarchy links.",
            CurrentPhase = "Execution"
        });

        Assert.True(firstProjectResult.IsSuccess);
        Assert.True(secondProjectResult.IsSuccess);

        var firstNode = await workbench.CreateObjectAsync(
            firstProjectResult.Value,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "First project node",
                string.Empty,
                "Source node.",
                null,
                320,
                220));
        var secondNode = await workbench.CreateObjectAsync(
            secondProjectResult.Value,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Second project node",
                string.Empty,
                "Target node.",
                null,
                420,
                220));

        await Assert.ThrowsAsync<InvalidOperationException>(() => workbench.LinkObjectsAsync(
            firstProjectResult.Value,
            firstNode.Id,
            secondNode.Id,
            ProjectObjectLinkKind.DependsOn));

        await Assert.ThrowsAsync<InvalidOperationException>(() => workbench.LinkObjectsAsync(
            firstProjectResult.Value,
            firstNode.Id,
            firstNode.Id,
            ProjectObjectLinkKind.Contains));
    }

    [Fact]
    public async Task UpdateObjectMetadataAsync_persists_transcript_provider_state_and_review_status()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

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
                LastProviderName = "Local llama",
                LastActionKind = ProjectLlmActionKind.FindMyTasks
            }
        });

        var updated = await workbench.UpdateObjectMetadataAsync(
            projectId,
            transcript.Id,
            updatedMetadata,
            notes: "Alice owes the rollout checklist.",
            status: "Review",
            nodeReferences: new ProjectNodeReferenceCollection
            {
                TranscriptProviderProfileId = providerId
            });

        Assert.NotNull(updated);
        Assert.Equal("Review", updated!.Status);
        Assert.Equal("Alice owes the rollout checklist.", updated.Notes);

        var surface = await workbench.GetStructureAsync(projectId);
        var persistedTranscript = Assert.Single(surface.Nodes, node => node.Id == transcript.Id);
        Assert.Equal("Review", persistedTranscript.Status);
        Assert.Equal("Alice owes the rollout checklist.", persistedTranscript.Notes);

        var parsedMetadata = ProjectObjectMetadataSerializer.Parse(persistedTranscript.MetadataJson);
        Assert.NotNull(parsedMetadata.Transcript);
        Assert.NotNull(persistedTranscript.NodeReferences);
        Assert.Equal(providerId, persistedTranscript.NodeReferences!.TranscriptProviderProfileId);
        Assert.Equal("Local llama", parsedMetadata.Transcript.LastProviderName);
        Assert.Equal(ProjectLlmActionKind.FindMyTasks, parsedMetadata.Transcript.LastActionKind);
        Assert.Equal("- Review the rollout checklist", parsedMetadata.Transcript.MyTasksText);
        Assert.Equal("- Alice: rollout checklist", parsedMetadata.Transcript.OthersDeliveriesText);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var carrier = await dbContext.Set<ProjectObjectRecord>()
            .SingleAsync(item => item.ProjectId == projectId && item.NodeKey == transcript.Id);
        var persistedMetadata = ProjectObjectMetadataSerializer.Parse(carrier.MetadataJson);
        Assert.NotNull(persistedMetadata.Transcript);
        Assert.Equal("Local llama", persistedMetadata.Transcript.LastProviderName);
        using (var persistedMetadataDocument = JsonDocument.Parse(carrier.MetadataJson))
        {
            var transcriptElement = persistedMetadataDocument.RootElement.GetProperty("transcript");
            Assert.False(transcriptElement.TryGetProperty("lastProviderProfileId", out _));
        }

        var providerBinding = await dbContext.Set<ProjectNodeReferenceRecord>()
            .SingleAsync(item => item.ProjectObjectId == carrier.Id);
        Assert.Equal(providerId.ToString("D"), providerBinding.ReferenceId);
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
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

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

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var lifecycleEvent = Assert.Single(await dbContext.Set<ProjectNodeLifecycleEventRecord>()
            .Where(item => item.ProjectId == saveResult.Value && item.NodeKey == created.Id)
            .ToListAsync());
        Assert.Equal(ProjectNodeLifecycleTransitionMode.NotePromotion, lifecycleEvent.TransitionMode);
        Assert.Equal(ProjectNodeKindFamily.None, lifecycleEvent.SourceFamily);
        Assert.Equal(ProjectNodeKindFamily.ProjectBlock, lifecycleEvent.TargetFamily);

        using var targetSnapshotDocument = JsonDocument.Parse(lifecycleEvent.TargetSnapshotJson);
        Assert.Equal("Deploy gateway", targetSnapshotDocument.RootElement.GetProperty("Title").GetString());
        Assert.Equal(noteBody, targetSnapshotDocument.RootElement.GetProperty("Notes").GetString());
    }

    [Fact]
    public async Task ReclassifyObjectAsync_promotes_notes_to_tasks_and_records_family_history()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projects, "Workbench note to task");
        const string noteBody = "Confirm the migration path\r\nTrack owners and due date.";
        var created = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Migration note",
                string.Empty,
                noteBody,
                $"project:{projectId}",
                320,
                220));

        var updated = await workbench.ReclassifyObjectAsync(
            projectId,
            created.Id,
            new ProjectObjectReclassificationRequest(
                ProjectObjectType.WorkItem,
                "task",
                "Confirm the migration path",
                string.Empty,
                noteBody,
                "{}"));

        Assert.NotNull(updated);
        Assert.Equal(ProjectObjectType.WorkItem, updated!.ObjectType);
        Assert.Equal("task", updated.ObjectSubtype);

        var metadata = ProjectObjectMetadataSerializer.Parse(updated.MetadataJson);
        Assert.NotNull(metadata.WorkItem);
        Assert.Equal(ProjectWorkItemKind.Task, metadata.WorkItem!.WorkItemKind);
        Assert.Equal(noteBody, metadata.WorkItem.Description);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var lifecycleEvent = Assert.Single(await dbContext.Set<ProjectNodeLifecycleEventRecord>()
            .Where(item => item.ProjectId == projectId && item.NodeKey == created.Id)
            .ToListAsync());
        Assert.Equal(ProjectNodeLifecycleTransitionMode.NotePromotion, lifecycleEvent.TransitionMode);
        Assert.Equal(ProjectNodeKindFamily.WorkItem, lifecycleEvent.TargetFamily);
    }

    [Fact]
    public async Task ReclassifyObjectAsync_promotes_notes_to_decisions_without_carrying_foreign_metadata()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();

        var projectId = await CreateProjectAsync(projects, "Workbench note to decision");
        const string noteBody = "Use the plugin registry before adding another enum.";
        var created = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Registry note",
                string.Empty,
                noteBody,
                $"project:{projectId}",
                340,
                220));

        var updated = await workbench.ReclassifyObjectAsync(
            projectId,
            created.Id,
            new ProjectObjectReclassificationRequest(
                ProjectObjectType.Decision,
                string.Empty,
                "Use the plugin registry before adding another enum.",
                string.Empty,
                noteBody,
                "{}"));

        Assert.NotNull(updated);
        Assert.Equal(ProjectObjectType.Decision, updated!.ObjectType);

        var metadata = ProjectObjectMetadataSerializer.Parse(updated.MetadataJson);
        Assert.Null(metadata.WorkItem);
        Assert.Null(metadata.Participant);
        Assert.Null(metadata.Repository);
    }

    [Fact]
    public async Task ReclassifyObjectAsync_changes_work_item_subtype_and_preserves_work_item_metadata()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projects, "Workbench subtype change");
        var dueUtc = new DateTimeOffset(2026, 4, 12, 14, 30, 0, TimeSpan.Zero);
        var created = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Confirm rollout",
                string.Empty,
                "Original work item description",
                $"project:{projectId}",
                520,
                260,
                null,
                null,
                "task",
                null,
                ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
                {
                    WorkItem = new ProjectWorkItemMetadata
                    {
                        WorkItemKind = ProjectWorkItemKind.Task,
                        Description = "Original work item description",
                        CurrencyCode = "USD",
                        DueUtc = dueUtc
                    }
                })));

        var updated = await workbench.ReclassifyObjectAsync(
            projectId,
            created.Id,
            new ProjectObjectReclassificationRequest(
                ProjectObjectType.WorkItem,
                "issue",
                "Confirm rollout",
                string.Empty,
                "Original work item description",
                "{}"));

        Assert.NotNull(updated);
        Assert.Equal(ProjectObjectType.WorkItem, updated!.ObjectType);
        Assert.Equal("issue", updated.ObjectSubtype);

        var metadata = ProjectObjectMetadataSerializer.Parse(updated.MetadataJson);
        Assert.NotNull(metadata.WorkItem);
        Assert.Equal(ProjectWorkItemKind.Issue, metadata.WorkItem!.WorkItemKind);
        Assert.Equal("Original work item description", metadata.WorkItem.Description);
        Assert.Equal("USD", metadata.WorkItem.CurrencyCode);
        Assert.Equal(dueUtc, metadata.WorkItem.DueUtc);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var lifecycleEvent = Assert.Single(await dbContext.Set<ProjectNodeLifecycleEventRecord>()
            .Where(item => item.ProjectId == projectId && item.NodeKey == created.Id)
            .ToListAsync());
        Assert.Equal(ProjectNodeLifecycleTransitionMode.SubtypeChange, lifecycleEvent.TransitionMode);
        Assert.Equal(ProjectNodeKindFamily.WorkItem, lifecycleEvent.SourceFamily);
        Assert.Equal(ProjectNodeKindFamily.WorkItem, lifecycleEvent.TargetFamily);
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
            link.Kind == ProjectObjectLinkKind.Contains);
        Assert.Contains(targetSurface.Links, link =>
            string.Equals(link.SourceId, BuildProjectRootNodeKey(targetProjectId), StringComparison.Ordinal) &&
            string.Equals(link.TargetId, wifiBlock.Id, StringComparison.Ordinal) &&
            link.Kind == ProjectObjectLinkKind.Contains);
        Assert.Contains(targetSurface.Links, link =>
            string.Equals(link.SourceId, childNote.Id, StringComparison.Ordinal) &&
            string.Equals(link.TargetId, wifiBlock.Id, StringComparison.Ordinal) &&
            link.Kind == ProjectObjectLinkKind.Uses &&
            link.IsUserAuthored);
        Assert.DoesNotContain(targetSurface.Links, link =>
            string.Equals(link.SourceId, parentBlock.Id, StringComparison.Ordinal) ||
            string.Equals(link.TargetId, parentBlock.Id, StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeleteObjectAsync_removes_canonical_node_assignments_for_deleted_subtrees()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var partyDirectory = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projects, "Delete subtree assignments");
        var participantPartyId = await CreatePartyAsync(partyDirectory, PartyType.Person, "Delete subtree participant");
        var workItemPartyId = await CreatePartyAsync(partyDirectory, PartyType.Person, "Delete subtree owner");

        var parentBlock = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Delete branch",
                string.Empty,
                "Deleting this branch must also clean canonical assignments.",
                $"project:{projectId}",
                360,
                220,
                null,
                null,
                "implementation"));
        var participantNode = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Participant,
                "Delete participant",
                string.Empty,
                "Participant node in the deleted subtree.",
                parentBlock.Id,
                560,
                260,
                null,
                null,
                "freelancer"));
        var workItemNode = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Delete work item",
                string.Empty,
                "Work item node in the deleted subtree.",
                participantNode.Id,
                760,
                320,
                null,
                null,
                "task"));

        Assert.True((await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = participantPartyId,
            Role = ProjectPartyAssignmentRole.TeamMember,
            NodeKey = participantNode.Id,
            IsPrimary = true,
            Source = "integration-tests"
        })).IsSuccess);
        Assert.True((await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = workItemPartyId,
            Role = ProjectPartyAssignmentRole.WorkItemAssignee,
            NodeKey = workItemNode.Id,
            IsPrimary = true,
            Source = "integration-tests"
        })).IsSuccess);

        var deletedCount = await workbench.DeleteObjectAsync(projectId, parentBlock.Id);

        Assert.Equal(3, deletedCount);

        var assignments = await bridge.ListAssignmentsDetailedAsync(projectId);
        Assert.DoesNotContain(assignments, item => string.Equals(item.NodeKey, participantNode.Id, StringComparison.Ordinal));
        Assert.DoesNotContain(assignments, item => string.Equals(item.NodeKey, workItemNode.Id, StringComparison.Ordinal));

        await AssertMutationStatusAsync(
            dbContextFactory,
            projectId,
            parentBlock.Id,
            ProjectCrossModuleMutationKind.DeleteSubtree,
            ProjectCrossModuleMutationStatus.Completed);
    }

    [Fact]
    public async Task DeleteObjectAsync_marks_durable_failure_when_assignment_cleanup_fails_after_workbench_commit()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var partyDirectory = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var failingWorkbench = CreateWorkbenchService(
            scope.ServiceProvider,
            new ThrowingProjectPartyIntegrationBridge(bridge, failDeleteForNodes: true, failMoveForNodes: false));

        var projectId = await CreateProjectAsync(projects, "Delete compensation");
        var participantPartyId = await CreatePartyAsync(partyDirectory, PartyType.Person, "Delete compensation owner");
        var parentBlock = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Delete branch",
                string.Empty,
                "Durable failure should not restore this subtree.",
                $"project:{projectId}",
                360,
                220,
                null,
                null,
                "implementation"));
        var participantNode = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Participant,
                "Delete participant",
                string.Empty,
                "Child node should stay deleted when canonical cleanup fails after commit.",
                parentBlock.Id,
                560,
                260,
                null,
                null,
                "freelancer"));

        Assert.True((await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = participantPartyId,
            Role = ProjectPartyAssignmentRole.TeamMember,
            NodeKey = participantNode.Id,
            IsPrimary = true,
            Source = "integration-tests"
        })).IsSuccess);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            failingWorkbench.DeleteObjectAsync(projectId, parentBlock.Id));

        Assert.Contains("committed the Workbench change", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("marked failed for retry", exception.Message, StringComparison.OrdinalIgnoreCase);

        var surface = await workbench.GetStructureAsync(projectId);
        Assert.DoesNotContain(surface.Nodes, node => string.Equals(node.Id, parentBlock.Id, StringComparison.Ordinal));
        Assert.DoesNotContain(surface.Nodes, node => string.Equals(node.Id, participantNode.Id, StringComparison.Ordinal));

        var assignments = await bridge.ListAssignmentsDetailedAsync(projectId);
        Assert.Contains(assignments, item =>
            string.Equals(item.NodeKey, participantNode.Id, StringComparison.Ordinal) &&
            item.PartyId == participantPartyId);

        await AssertMutationStatusAsync(
            dbContextFactory,
            projectId,
            parentBlock.Id,
            ProjectCrossModuleMutationKind.DeleteSubtree,
            ProjectCrossModuleMutationStatus.Failed,
            expectedAttemptCount: 1,
            expectCompletedAtUtc: false);
    }

    [Fact]
    public async Task MoveDescendantsToProjectAsync_moves_canonical_node_assignments_into_target_project()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var partyDirectory = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var sourceProjectId = await CreateProjectAsync(projects, "Source canonical move");
        var targetProjectId = await CreateProjectAsync(projects, "Target canonical move");
        var participantPartyId = await CreatePartyAsync(partyDirectory, PartyType.Person, "Moved participant");
        var workItemPartyId = await CreatePartyAsync(partyDirectory, PartyType.Person, "Moved work owner");

        var parentBlock = await workbench.CreateObjectAsync(
            sourceProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Move branch",
                string.Empty,
                "Root for the moved descendants.",
                $"project:{sourceProjectId}",
                360,
                220,
                null,
                null,
                "implementation"));
        var participantNode = await workbench.CreateObjectAsync(
            sourceProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Participant,
                "Move participant",
                string.Empty,
                "Participant node that should move with its assignment.",
                parentBlock.Id,
                560,
                260,
                null,
                null,
                "freelancer"));
        var workItemNode = await workbench.CreateObjectAsync(
            sourceProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Move work item",
                string.Empty,
                "Work item node that should move with its assignment.",
                participantNode.Id,
                760,
                320,
                null,
                null,
                "task"));

        Assert.True((await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = sourceProjectId,
            PartyId = participantPartyId,
            Role = ProjectPartyAssignmentRole.TeamMember,
            NodeKey = participantNode.Id,
            IsPrimary = true,
            Source = "integration-tests"
        })).IsSuccess);
        Assert.True((await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = sourceProjectId,
            PartyId = workItemPartyId,
            Role = ProjectPartyAssignmentRole.WorkItemAssignee,
            NodeKey = workItemNode.Id,
            IsPrimary = true,
            Source = "integration-tests"
        })).IsSuccess);

        var transfer = await workbench.MoveDescendantsToProjectAsync(sourceProjectId, parentBlock.Id, targetProjectId);

        Assert.NotNull(transfer);
        Assert.Equal(2, transfer!.MovedNodeCount);

        var sourceAssignments = await bridge.ListAssignmentsDetailedAsync(sourceProjectId);
        Assert.DoesNotContain(sourceAssignments, item => string.Equals(item.NodeKey, participantNode.Id, StringComparison.Ordinal));
        Assert.DoesNotContain(sourceAssignments, item => string.Equals(item.NodeKey, workItemNode.Id, StringComparison.Ordinal));

        var targetAssignments = await bridge.ListAssignmentsDetailedAsync(targetProjectId);
        Assert.Contains(targetAssignments, item =>
            string.Equals(item.NodeKey, participantNode.Id, StringComparison.Ordinal) &&
            item.PartyId == participantPartyId &&
            item.Role == ProjectPartyAssignmentRole.TeamMember);
        Assert.Contains(targetAssignments, item =>
            string.Equals(item.NodeKey, workItemNode.Id, StringComparison.Ordinal) &&
            item.PartyId == workItemPartyId &&
            item.Role == ProjectPartyAssignmentRole.WorkItemAssignee);
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var persistedHierarchyLinks = await dbContext.Set<ProjectObjectLinkRecord>()
                .Where(item =>
                    item.ProjectId == targetProjectId &&
                    (item.TargetNodeKey == participantNode.Id || item.TargetNodeKey == workItemNode.Id) &&
                    !item.IsSystemManaged &&
                    (item.LinkKind == ProjectObjectLinkKind.Contains || item.LinkKind == ProjectObjectLinkKind.BelongsTo))
                .ToListAsync();
            Assert.Empty(persistedHierarchyLinks);
        }

        await AssertMutationStatusAsync(
            dbContextFactory,
            sourceProjectId,
            parentBlock.Id,
            ProjectCrossModuleMutationKind.MoveDescendants,
            ProjectCrossModuleMutationStatus.Completed);
    }

    [Fact]
    public async Task MoveDescendantsToProjectAsync_marks_durable_failure_when_assignment_transfer_fails_after_workbench_commit()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var partyDirectory = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var bridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var failingWorkbench = CreateWorkbenchService(
            scope.ServiceProvider,
            new ThrowingProjectPartyIntegrationBridge(bridge, failDeleteForNodes: false, failMoveForNodes: true));

        var sourceProjectId = await CreateProjectAsync(projects, "Move compensation source");
        var targetProjectId = await CreateProjectAsync(projects, "Move compensation target");
        var participantPartyId = await CreatePartyAsync(partyDirectory, PartyType.Person, "Move compensation owner");

        var parentBlock = await workbench.CreateObjectAsync(
            sourceProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Move branch",
                string.Empty,
                "Parent block stays put while descendants move before durable reconciliation.",
                $"project:{sourceProjectId}",
                360,
                220,
                null,
                null,
                "implementation"));
        var participantNode = await workbench.CreateObjectAsync(
            sourceProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Participant,
                "Move participant",
                string.Empty,
                "Moved child should remain in the target project if canonical reconciliation fails after commit.",
                parentBlock.Id,
                560,
                260,
                null,
                null,
                "freelancer"));

        Assert.True((await bridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = sourceProjectId,
            PartyId = participantPartyId,
            Role = ProjectPartyAssignmentRole.TeamMember,
            NodeKey = participantNode.Id,
            IsPrimary = true,
            Source = "integration-tests"
        })).IsSuccess);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            failingWorkbench.MoveDescendantsToProjectAsync(sourceProjectId, parentBlock.Id, targetProjectId));

        Assert.Contains("committed the Workbench change", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("marked failed for retry", exception.Message, StringComparison.OrdinalIgnoreCase);

        var sourceSurface = await workbench.GetStructureAsync(sourceProjectId);
        Assert.DoesNotContain(sourceSurface.Nodes, node => string.Equals(node.Id, participantNode.Id, StringComparison.Ordinal));

        var targetSurface = await workbench.GetStructureAsync(targetProjectId);
        Assert.Contains(targetSurface.Nodes, node => string.Equals(node.Id, participantNode.Id, StringComparison.Ordinal));

        var sourceAssignments = await bridge.ListAssignmentsDetailedAsync(sourceProjectId);
        Assert.Contains(sourceAssignments, item =>
            string.Equals(item.NodeKey, participantNode.Id, StringComparison.Ordinal) &&
            item.PartyId == participantPartyId);

        var targetAssignments = await bridge.ListAssignmentsDetailedAsync(targetProjectId);
        Assert.DoesNotContain(targetAssignments, item => string.Equals(item.NodeKey, participantNode.Id, StringComparison.Ordinal));

        await AssertMutationStatusAsync(
            dbContextFactory,
            sourceProjectId,
            parentBlock.Id,
            ProjectCrossModuleMutationKind.MoveDescendants,
            ProjectCrossModuleMutationStatus.Failed,
            expectedAttemptCount: 1,
            expectCompletedAtUtc: false);
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

    private static ProcessDefinitionEditorModel BuildProcessDefinitionEditor(Guid projectId, Guid managerRoleId)
    {
        var intakeStepId = Guid.NewGuid();

        return new ProcessDefinitionEditorModel
        {
            ProjectId = projectId,
            Name = "Workbench-visible process",
            Summary = "Project the process definition into the structure graph.",
            ValueStatement = "Keep structure and process authoring aligned.",
            CustomerName = "Workbench validation customer",
            OwnerName = "Process architecture reviewer",
            GovernancePolicySummary = "Projected process nodes stay read-only in the structure canvas.",
            ChangeSummary = "Initial workbench projection test definition.",
            ConstitutionRuleSummary = "The role contract remains stable while executors change.",
            OperatingModeSummary = "Assisted execution routed through the project-scoped process workspace.",
            SimulationReadinessSummary = "Safe for integration validation.",
            Roles =
            [
                new ProcessRoleEditorModel
                {
                    Id = managerRoleId,
                    Key = "delivery-owner",
                    DisplayName = "Delivery owner",
                    Purpose = "Own the projected process flow.",
                    StaffingIntent = "Assigned from the project manager lane.",
                    PreferredProjectAssignmentRole = ProjectPartyAssignmentRole.Manager,
                    PreferredExecutorKind = "person",
                    SnapshotSummary = "Delivery owner role snapshot."
                }
            ],
            Steps =
            [
                new ProcessStepEditorModel
                {
                    Id = intakeStepId,
                    Key = "intake",
                    Title = "Capture integration intake",
                    StepKind = ProcessStepKind.Start,
                    InputContractSummary = "Structure-side scope request.",
                    OutputContractSummary = "Typed intake package.",
                    EvidenceContractSummary = "Capture the intake context.",
                    DecisionRightsSummary = "Delivery owner moves the request forward.",
                    ExceptionPolicySummary = "Escalate missing scope or governance details.",
                    TargetLeadHours = 2,
                    CanvasX = 140,
                    CanvasY = 140,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible,
                            RebindPolicySummary = "Rebind to the current delivery owner."
                        }
                    ]
                },
                new ProcessStepEditorModel
                {
                    Key = "review",
                    Title = "Review delivery readiness",
                    StepKind = ProcessStepKind.Work,
                    InputContractSummary = "Typed intake package.",
                    OutputContractSummary = "Ready-to-execute decision.",
                    EvidenceContractSummary = "Decision-ready evidence bundle.",
                    DecisionRightsSummary = "Delivery owner can approve, block, or escalate.",
                    ExceptionPolicySummary = "Block when evidence or staffing is incomplete.",
                    TargetLeadHours = 4,
                    Dependencies =
                    [
                        new ProcessStepDependencyEditorModel
                        {
                            Id = Guid.NewGuid(),
                            DependsOnStepId = intakeStepId
                        }
                    ],
                    CanvasX = 420,
                    CanvasY = 140,
                    RoleAssignments =
                    [
                        new ProcessStepRoleRequirementEditorModel
                        {
                            RoleRequirementId = managerRoleId,
                            ResponsibilityKind = ProcessResponsibilityKind.Responsible,
                            RebindPolicySummary = "Delivery owner remains attached."
                        }
                    ],
                    ArtifactExpectations =
                    [
                        new ProcessArtifactExpectationEditorModel
                        {
                            ArtifactKind = ProcessArtifactKind.Evidence,
                            Title = "Projected structure review evidence",
                            ValidationRequirementSummary = "Human review remains required."
                        }
                    ]
                }
            ]
        };
    }

    private static async Task<Guid> CreatePartyAsync(PartyDirectoryService partyDirectoryService, PartyType partyType, string displayName)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "integration-tests"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static ProjectWorkbenchService CreateWorkbenchService(
        IServiceProvider serviceProvider,
        IProjectPartyIntegrationBridge bridge)
    {
        var dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var clock = serviceProvider.GetRequiredService<IClock>();
        var assemblyService = serviceProvider.GetRequiredService<ProjectStructureAssemblyService>();
        var mutationCoordinator = serviceProvider.GetRequiredService<ProjectCrossModuleMutationCoordinator>();
        var mutationProcessor = new ProjectCrossModuleMutationProcessor(
            dbContextFactory,
            bridge,
            mutationCoordinator);
        var crossModuleMutationService = new ProjectWorkbenchCrossModuleMutationService(
            dbContextFactory,
            clock,
            mutationCoordinator,
            mutationProcessor);
        var relationService = new ProjectWorkbenchRelationService(
            dbContextFactory,
            clock,
            assemblyService);
        var lifecycleService = new ProjectWorkbenchLifecycleService(
            dbContextFactory,
            clock);
        var commandService = new ProjectWorkbenchCommandService(
            dbContextFactory,
            clock,
            serviceProvider.GetRequiredService<PromptFactoryService>(),
            assemblyService);
        return new ProjectWorkbenchService(
            dbContextFactory,
            clock,
            serviceProvider.GetRequiredService<IStoragePlacementService>(),
            assemblyService,
            relationService,
            lifecycleService,
            commandService,
            crossModuleMutationService);
    }

    private static async Task AssertMutationStatusAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        Guid projectId,
        string scopeNodeKey,
        ProjectCrossModuleMutationKind mutationKind,
        ProjectCrossModuleMutationStatus expectedStatus,
        int? expectedAttemptCount = null,
        bool? expectCompletedAtUtc = null)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var mutation = (await dbContext.Set<ProjectCrossModuleMutationRecord>()
            .Where(item =>
                item.ProjectId == projectId &&
                item.ScopeNodeKey == scopeNodeKey &&
                item.MutationKind == mutationKind)
            .ToListAsync())
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault();

        Assert.NotNull(mutation);
        Assert.True(
            mutation!.Status == expectedStatus,
            $"Expected mutation status '{expectedStatus}' but found '{mutation.Status}'. Error: {mutation.ErrorMessage}");
        Assert.NotEqual(DateTimeOffset.MinValue, mutation.CreatedAtUtc);
        Assert.NotEqual(DateTimeOffset.MinValue, mutation.UpdatedAtUtc);
        if (expectedAttemptCount.HasValue)
        {
            Assert.Equal(expectedAttemptCount.Value, mutation.AttemptCount);
        }

        if (expectCompletedAtUtc.HasValue)
        {
            if (expectCompletedAtUtc.Value)
            {
                Assert.NotNull(mutation.CompletedAtUtc);
            }
            else
            {
                Assert.Null(mutation.CompletedAtUtc);
            }
        }
    }

    private static string BuildProjectRootNodeKey(Guid projectId)
        => $"project:{projectId}";

    private static string BuildProcessDefinitionNodeKey(Guid definitionId)
        => $"process-definition:{definitionId}";

    private static string BuildProcessRunNodeKey(Guid runId)
        => $"process-run:{runId}";

    private sealed class ThrowingProjectPartyIntegrationBridge(
        IProjectPartyIntegrationBridge inner,
        bool failDeleteForNodes,
        bool failMoveForNodes) : IProjectPartyIntegrationBridge
    {
        public Task<IReadOnlyDictionary<Guid, ProjectPortfolioPartyContext>> GetPortfolioContextsAsync(
            IReadOnlyCollection<Guid> projectIds,
            CancellationToken cancellationToken = default)
        {
            return inner.GetPortfolioContextsAsync(projectIds, cancellationToken);
        }

        public Task<IReadOnlyList<ProjectPartyOption>> ListPartyOptionsAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return inner.ListPartyOptionsAsync(projectId, cancellationToken);
        }

        public Task<ProjectPartyOption?> GetPartyOptionAsync(
            Guid partyId,
            CancellationToken cancellationToken = default)
        {
            return inner.GetPartyOptionAsync(partyId, cancellationToken);
        }

        public Task<IReadOnlyList<ProjectPartyAssignmentDetail>> ListAssignmentsDetailedAsync(
            Guid projectId,
            CancellationToken cancellationToken = default)
        {
            return inner.ListAssignmentsDetailedAsync(projectId, cancellationToken);
        }

        public Task<Result<Guid>> SaveAssignmentAsync(
            ProjectPartyAssignmentUpsertRequest request,
            CancellationToken cancellationToken = default)
        {
            return inner.SaveAssignmentAsync(request, cancellationToken);
        }

        public Task<Result> ReplaceNodeAssignmentsAsync(
            Guid projectId,
            ProjectNodeReference nodeReference,
            IReadOnlyList<ProjectPartyAssignmentUpsertRequest> desiredAssignments,
            IReadOnlyList<ProjectPartyAssignmentRole> targetRoles,
            CancellationToken cancellationToken = default)
        {
            return inner.ReplaceNodeAssignmentsAsync(projectId, nodeReference, desiredAssignments, targetRoles, cancellationToken);
        }

        public Task DeleteAssignmentAsync(
            Guid assignmentId,
            CancellationToken cancellationToken = default)
        {
            return inner.DeleteAssignmentAsync(assignmentId, cancellationToken);
        }

        public Task DeleteAssignmentsForNodesAsync(
            Guid projectId,
            IReadOnlyCollection<ProjectNodeReference> nodeReferences,
            CancellationToken cancellationToken = default)
        {
            return failDeleteForNodes
                ? Task.FromException(new InvalidOperationException("Simulated canonical delete failure."))
                : inner.DeleteAssignmentsForNodesAsync(projectId, nodeReferences, cancellationToken);
        }

        public Task MoveAssignmentsToProjectAsync(
            Guid sourceProjectId,
            IReadOnlyCollection<ProjectNodeReference> nodeReferences,
            Guid targetProjectId,
            CancellationToken cancellationToken = default)
        {
            return failMoveForNodes
                ? Task.FromException(new InvalidOperationException("Simulated canonical move failure."))
                : inner.MoveAssignmentsToProjectAsync(sourceProjectId, nodeReferences, targetProjectId, cancellationToken);
        }

        public Task<Result<ProjectPartyQuickCreateResult>> CreatePartyAsync(
            ProjectPartyQuickCreateRequest request,
            CancellationToken cancellationToken = default)
        {
            return inner.CreatePartyAsync(request, cancellationToken);
        }
    }
}
