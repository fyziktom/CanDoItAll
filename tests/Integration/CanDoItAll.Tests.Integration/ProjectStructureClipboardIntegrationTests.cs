using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed class ProjectStructureClipboardIntegrationTests
{
    [Fact]
    public async Task CopySubtreesAsync_clones_complete_subtree_fields_bindings_references_and_internal_links()
    {
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            ConfigureServices = WrapDbContextFactoryWithSaveCounter
        });
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var saveCounter = scope.ServiceProvider.GetRequiredService<SaveChangesCounter>();
        var projectId = await CreateProjectAsync(projects, "Clipboard copy persistence");
        var projectRootNodeKey = BuildProjectRootNodeKey(projectId);
        var target = await CreateNodeAsync(workbench, projectId, "Paste target", projectRootNodeKey, 900, 300);
        var external = await CreateNodeAsync(workbench, projectId, "External node", projectRootNodeKey, 900, 700);
        var artifactId = Guid.NewGuid();
        var sourceRoot = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Source root",
                "Clipboard source",
                "All editable fields should survive the copy.",
                projectRootNodeKey,
                320,
                280,
                ObjectSubtype: "implementation",
                MetadataJson: "{}",
                ExternalBinding: new ProjectObjectExternalBindingRequest(
                    "/clipboard/source-root",
                    "clipboard-artifact",
                    artifactId),
                Status: "Ready"));
        var sourceChild = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Source child",
                "Nested task",
                "The child carries node references.",
                sourceRoot.Id,
                560,
                340,
                ObjectSubtype: "task"));
        var sourceRepository = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Repository,
                "Source repository",
                "Nested repository",
                "The copied work item references this repository.",
                sourceRoot.Id,
                560,
                520));

        Guid sourceRootRecordId;
        Guid sourceChildRecordId;
        Guid externalRecordId;
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var recordsByNodeKey = await dbContext.Set<ProjectObjectRecord>()
                .Where(node =>
                    node.ProjectId == projectId &&
                    new[] { sourceRoot.Id, sourceChild.Id, sourceRepository.Id, external.Id }.Contains(node.NodeKey))
                .ToDictionaryAsync(node => node.NodeKey, StringComparer.Ordinal);
            sourceRootRecordId = recordsByNodeKey[sourceRoot.Id].Id;
            sourceChildRecordId = recordsByNodeKey[sourceChild.Id].Id;
            externalRecordId = recordsByNodeKey[external.Id].Id;

            var sourceRootRecord = recordsByNodeKey[sourceRoot.Id];
            sourceRootRecord.ProgressMode = "progress";
            sourceRootRecord.ProgressPercent = 67;
            sourceRootRecord.MarkersJson = """[{"icon":"copy","tone":"accent","label":"Clipboard"}]""";
            sourceRootRecord.Priority = 8;
            sourceRootRecord.StartUtc = new DateTimeOffset(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);
            sourceRootRecord.EndUtc = new DateTimeOffset(2026, 7, 20, 13, 30, 0, TimeSpan.Zero);
            sourceRootRecord.DurationSeconds = 5400;
            dbContext.AddRange(
                new ProjectObjectLinkRecord
                {
                    ProjectId = projectId,
                    SourceNodeKey = sourceRoot.Id,
                    TargetNodeKey = sourceChild.Id,
                    LinkKind = ProjectObjectLinkKind.Contains,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                },
                new ProjectObjectLinkRecord
                {
                    ProjectId = projectId,
                    SourceNodeKey = sourceChild.Id,
                    TargetNodeKey = sourceRoot.Id,
                    LinkKind = ProjectObjectLinkKind.BelongsTo,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                });
            await dbContext.SaveChangesAsync();
        }

        var sourceChildMetadata = ProjectObjectMetadataSerializer.Parse(sourceChild.MetadataJson);
        sourceChildMetadata.WorkItem ??= new ProjectWorkItemMetadata
        {
            WorkItemKind = ProjectWorkItemKind.Task
        };
        sourceChildMetadata.WorkItem.AssigneePartyDisplayName = "Projected clipboard assignee";
        var projectedAssigneeNodeId = Guid.NewGuid();
        var sourceReferences = new ProjectNodeReferenceCollection
        {
            Entries =
            [
                new ProjectNodeReferenceEntry("clipboard.external-node", externalRecordId.ToString("D"), 0)
            ]
        };
        sourceReferences.WorkItemRepositoryResourceId = ParseCustomNodeId(sourceRepository.Id);
        sourceReferences.WorkItemAssigneeNodeId = projectedAssigneeNodeId;
        await workbench.UpdateObjectAsync(
            projectId,
            sourceChild.Id,
            new ProjectObjectEditRequest(
                sourceChild.Title,
                sourceChild.Subtitle,
                sourceChild.Notes,
                sourceChild.StartUtc,
                sourceChild.EndUtc,
                ProjectObjectMetadataSerializer.Serialize(sourceChildMetadata),
                sourceChild.DurationSeconds,
                sourceReferences));
        await workbench.LinkObjectsAsync(
            projectId,
            sourceRoot.Id,
            sourceChild.Id,
            ProjectObjectLinkKind.DependsOn);
        await workbench.LinkObjectsAsync(
            projectId,
            sourceRoot.Id,
            external.Id,
            ProjectObjectLinkKind.Uses);

        saveCounter.Reset();
        var result = await workbench.CopySubtreesAsync(
            projectId,
            [sourceRoot.Id],
            target.Id);

        Assert.Equal(1, saveCounter.SaveChangesCount);
        Assert.Equal(new[] { result.NodeIdMap[sourceRoot.Id] }, result.RootNodeIds);
        Assert.Equal(3, result.NodeIdMap.Count);
        var copiedRootNodeKey = result.NodeIdMap[sourceRoot.Id];
        var copiedChildNodeKey = result.NodeIdMap[sourceChild.Id];
        var copiedRepositoryNodeKey = result.NodeIdMap[sourceRepository.Id];
        Assert.NotEqual(sourceRoot.Id, copiedRootNodeKey);
        Assert.NotEqual(sourceChild.Id, copiedChildNodeKey);

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var copiedRootRecord = await verificationContext.Set<ProjectObjectRecord>()
            .SingleAsync(node => node.ProjectId == projectId && node.NodeKey == copiedRootNodeKey);
        var copiedChildRecord = await verificationContext.Set<ProjectObjectRecord>()
            .SingleAsync(node => node.ProjectId == projectId && node.NodeKey == copiedChildNodeKey);
        var persistedSourceRootRecord = await verificationContext.Set<ProjectObjectRecord>()
            .AsNoTracking()
            .SingleAsync(node => node.Id == sourceRootRecordId);
        Assert.NotEqual(sourceRootRecordId, copiedRootRecord.Id);
        Assert.NotEqual(sourceChildRecordId, copiedChildRecord.Id);
        Assert.Equal(target.Id, copiedRootRecord.ParentNodeKey);
        Assert.Equal(copiedRootNodeKey, copiedChildRecord.ParentNodeKey);
        Assert.True(await verificationContext.Set<ProjectObjectRecord>().AnyAsync(node =>
            node.ProjectId == projectId &&
            node.NodeKey == copiedRepositoryNodeKey &&
            node.ParentNodeKey == copiedRootNodeKey));
        AssertCopiedScalarFields(persistedSourceRootRecord, copiedRootRecord);

        var sourceBinding = await verificationContext.Set<ProjectNodeBindingRecord>()
            .SingleAsync(binding => binding.ProjectObjectId == sourceRootRecordId);
        var copiedBinding = await verificationContext.Set<ProjectNodeBindingRecord>()
            .SingleAsync(binding => binding.ProjectObjectId == copiedRootRecord.Id);
        Assert.Equal(sourceBinding.Route, copiedBinding.Route);
        Assert.Equal(sourceBinding.ExternalArtifactKind, copiedBinding.ExternalArtifactKind);
        Assert.Equal(sourceBinding.ExternalArtifactId, copiedBinding.ExternalArtifactId);
        Assert.Equal(sourceBinding.StorageObjectReferenceJson, copiedBinding.StorageObjectReferenceJson);

        var copiedReferences = await verificationContext.Set<ProjectNodeReferenceRecord>()
            .Where(reference => reference.ProjectObjectId == copiedChildRecord.Id)
            .ToListAsync();
        Assert.Contains(
            copiedReferences,
            reference =>
                reference.ReferenceKind == ProjectNodeReferenceKinds.WorkItemRepositoryResource &&
                reference.ReferenceId == ParseCustomNodeId(copiedRepositoryNodeKey).ToString("D"));
        Assert.Contains(
            copiedReferences,
            reference =>
                reference.ReferenceKind == "clipboard.external-node" &&
                reference.ReferenceId == externalRecordId.ToString("D"));
        Assert.DoesNotContain(
            copiedReferences,
            reference => reference.ReferenceKind == ProjectNodeReferenceKinds.WorkItemAssigneeParticipant);
        var copiedChildMetadata = ProjectObjectMetadataSerializer.Parse(copiedChildRecord.MetadataJson);
        Assert.Equal(string.Empty, copiedChildMetadata.WorkItem?.AssigneePartyDisplayName);

        Assert.True(await verificationContext.Set<ProjectObjectLinkRecord>().AnyAsync(link =>
            link.ProjectId == projectId &&
            link.SourceNodeKey == copiedRootNodeKey &&
            link.TargetNodeKey == copiedChildNodeKey &&
            link.LinkKind == ProjectObjectLinkKind.DependsOn &&
            !link.IsSystemManaged));
        Assert.False(await verificationContext.Set<ProjectObjectLinkRecord>().AnyAsync(link =>
            link.ProjectId == projectId &&
            link.SourceNodeKey == copiedRootNodeKey &&
            link.TargetNodeKey == external.Id));
        Assert.False(await verificationContext.Set<ProjectObjectLinkRecord>().AnyAsync(link =>
            link.ProjectId == projectId &&
            (link.SourceNodeKey == copiedRootNodeKey || link.SourceNodeKey == copiedChildNodeKey) &&
            (link.TargetNodeKey == copiedRootNodeKey || link.TargetNodeKey == copiedChildNodeKey) &&
            (link.LinkKind == ProjectObjectLinkKind.Contains || link.LinkKind == ProjectObjectLinkKind.BelongsTo)));
    }

    [Fact]
    public async Task CopySubtreesAsync_preserves_workflow_configuration_and_clears_execution_state()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectId = await CreateProjectAsync(projects, "Clipboard workflow reset");
        var projectRootNodeKey = BuildProjectRootNodeKey(projectId);
        var target = await CreateNodeAsync(workbench, projectId, "Workflow target", projectRootNodeKey, 980, 320);
        var selectedInputNode = await CreateNodeAsync(workbench, projectId, "Workflow selected input", projectRootNodeKey, 320, 640);
        var workflowId = WorkflowId.New();
        var workflowVersionId = WorkflowVersionId.New();
        var lastRunId = WorkflowRunId.New();
        var lastStartedAtUtc = new DateTimeOffset(2026, 7, 17, 11, 0, 0, TimeSpan.Zero);
        var lastUpdatedAtUtc = lastStartedAtUtc.AddMinutes(12);
        var metadata = new ProjectObjectMetadataEnvelope
        {
            WorkflowProjectWrite = new ProjectWorkflowProjectWriteMetadata
            {
                IdempotencyKey = "clipboard-runtime-key",
                BatchIdempotencyKey = "clipboard-runtime-batch"
            },
            Workflow = new ProjectWorkflowNodeMetadata
            {
                WorkflowId = workflowId,
                WorkflowVersionId = workflowVersionId,
                WorkflowName = "Clipboard workflow",
                WorkflowDescription = "Configuration must survive the copy.",
                InputSettings = new ProjectStructureWorkflowInputSettings
                {
                    IncludeParentSubtree = true,
                    SelectedNodeIds = [selectedInputNode.Id],
                    AdditionalSources =
                    [
                        new ProjectStructureWorkflowInputSource(
                            ProjectStructureWorkflowInputSourceKind.SelectedNode,
                            "selected-input",
                            "Selected input",
                            selectedInputNode.Id),
                        new ProjectStructureWorkflowInputSource(
                            ProjectStructureWorkflowInputSourceKind.FilePath,
                            "specification",
                            "Specification",
                            "docs/specification.md")
                    ],
                    ManualInputJson = """{"mode":"clipboard"}"""
                },
                LastRunId = lastRunId,
                LastRunState = WorkflowRunState.Completed,
                LastRunSummary = "Runtime result that must not survive.",
                LastCreatedNodeIds = ["custom:runtime-node"],
                LastCreatedAssetIds = ["runtime-asset"],
                LastCreatedFilePaths = ["runtime/output.txt"],
                LastStepIndex = 4,
                LastStepCount = 4,
                LastStartedAtUtc = lastStartedAtUtc,
                LastUpdatedAtUtc = lastUpdatedAtUtc
            }
        };
        var source = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkflowDefinition,
                "Workflow source",
                "Completed workflow",
                "The copy should be ready for a new run.",
                projectRootNodeKey,
                320,
                320,
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(metadata),
                Status: "Completed"));

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var sourceRecord = await dbContext.Set<ProjectObjectRecord>()
                .SingleAsync(node => node.ProjectId == projectId && node.NodeKey == source.Id);
            sourceRecord.ProgressMode = "complete";
            sourceRecord.ProgressPercent = 100;
            sourceRecord.MarkersJson = """[{"icon":"complete","tone":"success","label":"Completed"},{"icon":"alert","tone":"danger","label":"Failed"}]""";
            await dbContext.SaveChangesAsync();
        }

        var result = await workbench.CopySubtreesAsync(projectId, [source.Id, selectedInputNode.Id], target.Id);

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var copiedRecord = await verificationContext.Set<ProjectObjectRecord>()
            .SingleAsync(node => node.ProjectId == projectId && node.NodeKey == result.NodeIdMap[source.Id]);
        var copiedMetadata = ProjectObjectMetadataSerializer.Parse(copiedRecord.MetadataJson);
        var copiedWorkflow = Assert.IsType<ProjectWorkflowNodeMetadata>(copiedMetadata.Workflow);
        Assert.Equal("Ready", copiedRecord.Status);
        Assert.Equal("progress", copiedRecord.ProgressMode);
        Assert.Equal(0, copiedRecord.ProgressPercent);
        var copiedMarkers = ProjectNodeMarkerState.Parse(copiedRecord.MarkersJson);
        Assert.Contains(copiedMarkers, marker => marker.Icon == "complete");
        Assert.DoesNotContain(copiedMarkers, marker => marker.Icon == "alert");
        Assert.Equal(workflowId, copiedWorkflow.WorkflowId);
        Assert.Equal(workflowVersionId, copiedWorkflow.WorkflowVersionId);
        Assert.Equal("Clipboard workflow", copiedWorkflow.WorkflowName);
        Assert.Equal("Configuration must survive the copy.", copiedWorkflow.WorkflowDescription);
        Assert.True(copiedWorkflow.InputSettings.IncludeParentSubtree);
        Assert.Equal(
            new[] { result.NodeIdMap[selectedInputNode.Id] },
            copiedWorkflow.InputSettings.SelectedNodeIds);
        Assert.Contains(
            copiedWorkflow.InputSettings.AdditionalSources,
            source =>
                source.Kind == ProjectStructureWorkflowInputSourceKind.SelectedNode &&
                source.Value == result.NodeIdMap[selectedInputNode.Id]);
        Assert.Contains(
            copiedWorkflow.InputSettings.AdditionalSources,
            source =>
                source.Kind == ProjectStructureWorkflowInputSourceKind.FilePath &&
                source.Value == "docs/specification.md");
        Assert.Equal("""{"mode":"clipboard"}""", copiedWorkflow.InputSettings.ManualInputJson);
        Assert.Null(copiedWorkflow.LastRunId);
        Assert.Null(copiedWorkflow.LastRunState);
        Assert.Equal(string.Empty, copiedWorkflow.LastRunSummary);
        Assert.Empty(copiedWorkflow.LastCreatedNodeIds);
        Assert.Empty(copiedWorkflow.LastCreatedAssetIds);
        Assert.Empty(copiedWorkflow.LastCreatedFilePaths);
        Assert.Equal(0, copiedWorkflow.LastStepIndex);
        Assert.Equal(0, copiedWorkflow.LastStepCount);
        Assert.Null(copiedWorkflow.LastStartedAtUtc);
        Assert.Null(copiedWorkflow.LastUpdatedAtUtc);
        Assert.Null(copiedMetadata.WorkflowProjectWrite);
    }

    [Fact]
    public async Task CopySubtreesAsync_clears_completed_deferred_provenance_and_rejects_incomplete_operations()
    {
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            ConfigureServices = WrapDbContextFactoryWithSaveCounter
        });
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var saveCounter = scope.ServiceProvider.GetRequiredService<SaveChangesCounter>();
        var projectId = await CreateProjectAsync(projects, "Clipboard deferred completion");
        var projectRootNodeKey = BuildProjectRootNodeKey(projectId);
        var target = await CreateNodeAsync(workbench, projectId, "Deferred target", projectRootNodeKey, 980, 320);
        var completed = await CreateDeferredImageNodeAsync(
            workbench,
            projectId,
            "Completed image",
            projectRootNodeKey,
            ProjectStructureDeferredNodeCompletionState.Completed);
        var incompleteNodes = new[]
        {
            await CreateDeferredImageNodeAsync(
                workbench,
                projectId,
                "Queued image",
                projectRootNodeKey,
                ProjectStructureDeferredNodeCompletionState.Queued),
            await CreateDeferredImageNodeAsync(
                workbench,
                projectId,
                "Running image",
                projectRootNodeKey,
                ProjectStructureDeferredNodeCompletionState.Running),
            await CreateDeferredImageNodeAsync(
                workbench,
                projectId,
                "Failed image",
                projectRootNodeKey,
                ProjectStructureDeferredNodeCompletionState.Failed)
        };

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var completedRecord = await dbContext.Set<ProjectObjectRecord>()
                .SingleAsync(node => node.ProjectId == projectId && node.NodeKey == completed.Id);
            completedRecord.ProgressMode = "complete";
            completedRecord.ProgressPercent = 100;
            completedRecord.MarkersJson = """[{"icon":"image","tone":"success","label":"Generated"}]""";
            await dbContext.SaveChangesAsync();
        }

        saveCounter.Reset();
        var completedCopy = await workbench.CopySubtreesAsync(projectId, [completed.Id], target.Id);
        Assert.Equal(1, saveCounter.SaveChangesCount);

        await using (var verificationContext = await dbContextFactory.CreateDbContextAsync())
        {
            var sourceRecord = await verificationContext.Set<ProjectObjectRecord>()
                .SingleAsync(node => node.NodeKey == completed.Id);
            var copiedRecord = await verificationContext.Set<ProjectObjectRecord>()
                .SingleAsync(node => node.NodeKey == completedCopy.NodeIdMap[completed.Id]);
            var sourceBinding = await verificationContext.Set<ProjectNodeBindingRecord>()
                .SingleAsync(binding => binding.ProjectObjectId == sourceRecord.Id);
            var copiedBinding = await verificationContext.Set<ProjectNodeBindingRecord>()
                .SingleAsync(binding => binding.ProjectObjectId == copiedRecord.Id);
            Assert.Equal("Generated image ready", copiedRecord.Status);
            Assert.Equal("complete", copiedRecord.ProgressMode);
            Assert.Equal(100, copiedRecord.ProgressPercent);
            Assert.Equal("""[{"icon":"image","tone":"success","label":"Generated"}]""", copiedRecord.MarkersJson);
            Assert.Null(ProjectObjectMetadataSerializer.Parse(copiedRecord.MetadataJson).DeferredCompletion);
            Assert.False(string.IsNullOrWhiteSpace(sourceBinding.MediaRelativePath));
            Assert.Equal(sourceBinding.MediaRelativePath, copiedBinding.MediaRelativePath);
            Assert.Equal(sourceBinding.MediaContentType, copiedBinding.MediaContentType);
            Assert.Equal(sourceBinding.MediaOriginalFileName, copiedBinding.MediaOriginalFileName);
        }

        foreach (var incompleteNode in incompleteNodes)
        {
            saveCounter.Reset();
            var exception = await Assert.ThrowsAsync<ProjectStructureClipboardMutationInputException>(async () =>
                await workbench.CopySubtreesAsync(projectId, [incompleteNode.Id], target.Id));
            Assert.Contains("before completion", exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, saveCounter.SaveChangesCount);
        }
    }

    [Fact]
    public async Task CopySubtreesAsync_prunes_selected_descendants_and_copies_multiple_roots_once()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var projectId = await CreateProjectAsync(projects, "Clipboard multi-root copy");
        var projectRootNodeKey = BuildProjectRootNodeKey(projectId);
        var firstRoot = await CreateNodeAsync(workbench, projectId, "First root", projectRootNodeKey, 300, 260);
        var firstChild = await CreateNodeAsync(workbench, projectId, "First child", firstRoot.Id, 520, 280);
        var secondRoot = await CreateNodeAsync(workbench, projectId, "Second root", projectRootNodeKey, 300, 620);
        var secondChild = await CreateNodeAsync(workbench, projectId, "Second child", secondRoot.Id, 520, 640);

        var result = await workbench.CopySubtreesAsync(
            projectId,
            [firstChild.Id, firstRoot.Id, secondRoot.Id],
            projectRootNodeKey);

        Assert.Equal(
            new[] { result.NodeIdMap[firstRoot.Id], result.NodeIdMap[secondRoot.Id] },
            result.RootNodeIds);
        Assert.Equal(4, result.NodeIdMap.Count);

        await using var verificationContext = await dbContextFactory.CreateDbContextAsync();
        var copiedNodeKeys = result.NodeIdMap.Values.ToArray();
        var copiedNodes = await verificationContext.Set<ProjectObjectRecord>()
            .Where(node => copiedNodeKeys.Contains(node.NodeKey))
            .ToDictionaryAsync(node => node.NodeKey, StringComparer.Ordinal);
        Assert.Equal(4, copiedNodes.Count);
        Assert.Equal(projectRootNodeKey, copiedNodes[result.NodeIdMap[firstRoot.Id]].ParentNodeKey);
        Assert.Equal(result.NodeIdMap[firstRoot.Id], copiedNodes[result.NodeIdMap[firstChild.Id]].ParentNodeKey);
        Assert.Equal(projectRootNodeKey, copiedNodes[result.NodeIdMap[secondRoot.Id]].ParentNodeKey);
        Assert.Equal(result.NodeIdMap[secondRoot.Id], copiedNodes[result.NodeIdMap[secondChild.Id]].ParentNodeKey);
    }

    [Fact]
    public async Task Clipboard_mutations_reject_projected_and_noncanonical_project_destinations_without_writes()
    {
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            ConfigureServices = WrapDbContextFactoryWithSaveCounter
        });
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var saveCounter = scope.ServiceProvider.GetRequiredService<SaveChangesCounter>();
        var projectId = await CreateProjectAsync(projects, "Clipboard projected target");
        var subprojectId = await CreateProjectAsync(projects, "Clipboard projected subproject");
        Assert.True((await projects.AddSubprojectAsync(projectId, subprojectId)).IsSuccess);
        var source = await CreateNodeAsync(
            workbench,
            projectId,
            "Projected target source",
            BuildProjectRootNodeKey(projectId),
            360,
            320);
        var before = await LoadNodeSnapshotsAsync(dbContextFactory, projectId, [source.Id]);
        var rejectedTargetNodeKeys = new[]
        {
            $"project-child:{subprojectId:D}",
            BuildProjectRootNodeKey(projectId).ToUpperInvariant()
        };

        foreach (var rejectedTargetNodeKey in rejectedTargetNodeKeys)
        {
            saveCounter.Reset();
            var copyException = await Assert.ThrowsAsync<ProjectStructureClipboardMutationInputException>(async () =>
                await workbench.CopySubtreesAsync(projectId, [source.Id], rejectedTargetNodeKey));
            Assert.Contains("projected project node", copyException.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, saveCounter.SaveChangesCount);

            saveCounter.Reset();
            var cutException = await Assert.ThrowsAsync<ProjectStructureClipboardMutationInputException>(async () =>
                await workbench.ReparentSubtreesAsync(projectId, [source.Id], rejectedTargetNodeKey));
            Assert.Contains("projected project node", cutException.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, saveCounter.SaveChangesCount);
        }

        var after = await LoadNodeSnapshotsAsync(dbContextFactory, projectId, [source.Id]);
        Assert.Equal(before[source.Id], after[source.Id]);
    }

    [Fact]
    public async Task ReparentSubtreesAsync_moves_pruned_forests_atomically_and_rejects_cycles_and_missing_targets()
    {
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            ConfigureServices = WrapDbContextFactoryWithSaveCounter
        });
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var saveCounter = scope.ServiceProvider.GetRequiredService<SaveChangesCounter>();
        var projectId = await CreateProjectAsync(projects, "Clipboard cut persistence");
        var projectRootNodeKey = BuildProjectRootNodeKey(projectId);
        var sourceParent = await CreateNodeAsync(workbench, projectId, "Source parent", projectRootNodeKey, 280, 380);
        var targetParent = await CreateNodeAsync(workbench, projectId, "Target parent", projectRootNodeKey, 1120, 420);
        var firstRoot = await CreateNodeAsync(workbench, projectId, "First moved root", sourceParent.Id, 480, 300);
        var firstChild = await CreateNodeAsync(workbench, projectId, "First child", firstRoot.Id, 700, 340);
        var firstGrandchild = await CreateNodeAsync(workbench, projectId, "First grandchild", firstChild.Id, 900, 380);
        var secondRoot = await CreateNodeAsync(workbench, projectId, "Second moved root", sourceParent.Id, 480, 640);
        var secondChild = await CreateNodeAsync(workbench, projectId, "Second child", secondRoot.Id, 700, 680);

        Dictionary<string, ProjectObjectRecord> beforeByNodeKey;
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var nodeKeys = new[]
            {
                firstRoot.Id,
                firstChild.Id,
                firstGrandchild.Id,
                secondRoot.Id,
                secondChild.Id
            };
            beforeByNodeKey = await dbContext.Set<ProjectObjectRecord>()
                .AsNoTracking()
                .Where(node => node.ProjectId == projectId && nodeKeys.Contains(node.NodeKey))
                .ToDictionaryAsync(node => node.NodeKey, StringComparer.Ordinal);
        }

        saveCounter.Reset();
        var movedRoots = await workbench.ReparentSubtreesAsync(
            projectId,
            [firstRoot.Id, firstChild.Id, secondRoot.Id],
            targetParent.Id);

        Assert.Equal(2, saveCounter.SaveChangesCount);
        Assert.Equal(new[] { firstRoot.Id, secondRoot.Id }, movedRoots.Select(node => node.Id));
        Assert.All(movedRoots, node => Assert.Equal(targetParent.Id, node.ParentId));

        await using (var verificationContext = await dbContextFactory.CreateDbContextAsync())
        {
            var movedByNodeKey = await verificationContext.Set<ProjectObjectRecord>()
                .Where(node => beforeByNodeKey.Keys.Contains(node.NodeKey))
                .ToDictionaryAsync(node => node.NodeKey, StringComparer.Ordinal);
            Assert.Equal(targetParent.Id, movedByNodeKey[firstRoot.Id].ParentNodeKey);
            Assert.Equal(firstRoot.Id, movedByNodeKey[firstChild.Id].ParentNodeKey);
            Assert.Equal(firstChild.Id, movedByNodeKey[firstGrandchild.Id].ParentNodeKey);
            Assert.Equal(targetParent.Id, movedByNodeKey[secondRoot.Id].ParentNodeKey);
            Assert.Equal(secondRoot.Id, movedByNodeKey[secondChild.Id].ParentNodeKey);

            foreach (var nodeKey in beforeByNodeKey.Keys)
            {
                Assert.Equal(beforeByNodeKey[nodeKey].Id, movedByNodeKey[nodeKey].Id);
            }

            Assert.Equal(
                beforeByNodeKey[firstChild.Id].PositionX - beforeByNodeKey[firstRoot.Id].PositionX,
                movedByNodeKey[firstChild.Id].PositionX - movedByNodeKey[firstRoot.Id].PositionX,
                6);
            Assert.Equal(
                beforeByNodeKey[firstChild.Id].PositionY - beforeByNodeKey[firstRoot.Id].PositionY,
                movedByNodeKey[firstChild.Id].PositionY - movedByNodeKey[firstRoot.Id].PositionY,
                6);
            Assert.Equal(
                beforeByNodeKey[secondChild.Id].PositionX - beforeByNodeKey[secondRoot.Id].PositionX,
                movedByNodeKey[secondChild.Id].PositionX - movedByNodeKey[secondRoot.Id].PositionX,
                6);
        }

        var persistedBeforeRejectedMoves = await LoadNodeSnapshotsAsync(
            dbContextFactory,
            projectId,
            [firstRoot.Id, firstChild.Id, firstGrandchild.Id, secondRoot.Id, secondChild.Id]);

        saveCounter.Reset();
        var cycleException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await workbench.ReparentSubtreesAsync(
                projectId,
                [firstRoot.Id, secondRoot.Id],
                firstGrandchild.Id));
        Assert.Contains("cycle", cycleException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, saveCounter.SaveChangesCount);

        saveCounter.Reset();
        var missingTargetException = await Assert.ThrowsAsync<ProjectStructureClipboardMutationInputException>(async () =>
            await workbench.ReparentSubtreesAsync(
                projectId,
                [firstRoot.Id, secondRoot.Id],
                "custom:missing-target"));
        Assert.Contains("not found", missingTargetException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, saveCounter.SaveChangesCount);

        saveCounter.Reset();
        var staleSourceException = await Assert.ThrowsAsync<ProjectStructureClipboardMutationInputException>(async () =>
            await workbench.ReparentSubtreesAsync(
                projectId,
                [firstRoot.Id, "custom:stale-source", secondRoot.Id],
                targetParent.Id));
        Assert.Contains("not a persisted editable node", staleSourceException.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, saveCounter.SaveChangesCount);

        var persistedAfterRejectedMoves = await LoadNodeSnapshotsAsync(
            dbContextFactory,
            projectId,
            persistedBeforeRejectedMoves.Keys.ToList());
        foreach (var nodeKey in persistedBeforeRejectedMoves.Keys)
        {
            Assert.Equal(persistedBeforeRejectedMoves[nodeKey], persistedAfterRejectedMoves[nodeKey]);
        }
    }

    private static async Task<ProjectStructureNode> CreateNodeAsync(
        ProjectWorkbenchService workbench,
        Guid projectId,
        string title,
        string parentNodeKey,
        double x,
        double y)
    {
        return await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                title,
                string.Empty,
                $"{title} notes.",
                parentNodeKey,
                x,
                y));
    }

    private static async Task<ProjectStructureNode> CreateDeferredImageNodeAsync(
        ProjectWorkbenchService workbench,
        Guid projectId,
        string title,
        string parentNodeKey,
        ProjectStructureDeferredNodeCompletionState state)
    {
        var createdAtUtc = new DateTimeOffset(2026, 7, 18, 12, 0, 0, TimeSpan.Zero);
        var media = state == ProjectStructureDeferredNodeCompletionState.Completed
            ? new ProjectObjectMediaPayload(
                "completed-image.png",
                "image/png",
                "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=")
            : null;
        return await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ImageAsset,
                title,
                state.ToString(),
                $"{title} notes.",
                parentNodeKey,
                Media: media,
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
                {
                    File = new ProjectFileMetadata
                    {
                        FileSubtype = ProjectFileSubtype.Image,
                        SourceHint = "Generated image"
                    },
                    DeferredCompletion = new ProjectStructureDeferredCompletionMetadata
                    {
                        OperationId = Guid.NewGuid(),
                        Kind = ProjectStructureDeferredNodeCompletionKind.GeneratedImageAsset,
                        State = state,
                        CreatedAtUtc = createdAtUtc,
                        UpdatedAtUtc = createdAtUtc
                    }
                }),
                Status: state == ProjectStructureDeferredNodeCompletionState.Completed
                    ? "Generated image ready"
                    : $"Image generation {state.ToString().ToLowerInvariant()}"));
    }

    private static void AssertCopiedScalarFields(ProjectObjectRecord source, ProjectObjectRecord copy)
    {
        Assert.Equal(source.ProjectId, copy.ProjectId);
        Assert.Equal(source.ObjectType, copy.ObjectType);
        Assert.Equal(source.Title, copy.Title);
        Assert.Equal(source.Subtitle, copy.Subtitle);
        Assert.Equal(source.Status, copy.Status);
        Assert.Equal(source.Notes, copy.Notes);
        Assert.Equal(source.ObjectSubtype, copy.ObjectSubtype);
        Assert.Equal(source.ProgressMode, copy.ProgressMode);
        Assert.Equal(source.ProgressPercent, copy.ProgressPercent);
        Assert.Equal(source.MarkersJson, copy.MarkersJson);
        Assert.Equal(source.Priority, copy.Priority);
        Assert.Equal(source.MetadataJson, copy.MetadataJson);
        Assert.Equal(source.StartUtc, copy.StartUtc);
        Assert.Equal(source.EndUtc, copy.EndUtc);
        Assert.Equal(source.DurationSeconds, copy.DurationSeconds);
        Assert.Equal(source.IsSystemManaged, copy.IsSystemManaged);
    }

    private static async Task<Dictionary<string, NodeSnapshot>> LoadNodeSnapshotsAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        Guid projectId,
        IReadOnlyCollection<string> nodeKeys)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        return await dbContext.Set<ProjectObjectRecord>()
            .Where(node => node.ProjectId == projectId && nodeKeys.Contains(node.NodeKey))
            .ToDictionaryAsync(
                node => node.NodeKey,
                node => new NodeSnapshot(
                    node.Id,
                    node.ParentNodeKey,
                    node.PositionX,
                    node.PositionY),
                StringComparer.Ordinal);
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projects, string name)
    {
        var result = await projects.SaveAsync(new ProjectEditorModel
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
    {
        return $"project:{projectId}";
    }

    private static Guid ParseCustomNodeId(string nodeKey)
    {
        const string customNodeKeyPrefix = "custom:";
        Assert.StartsWith(customNodeKeyPrefix, nodeKey, StringComparison.Ordinal);
        Assert.True(Guid.TryParse(nodeKey[customNodeKeyPrefix.Length..], out var nodeId));
        return nodeId;
    }

    private static void WrapDbContextFactoryWithSaveCounter(IServiceCollection services)
    {
        services.AddSingleton<SaveChangesCounter>();

        var factoryDescriptor = services.Last(descriptor => descriptor.ServiceType == typeof(IDbContextFactory<AppDbContext>));
        services.Remove(factoryDescriptor);
        services.Add(new ServiceDescriptor(
            typeof(IDbContextFactory<AppDbContext>),
            serviceProvider =>
            {
                var innerFactory = (IDbContextFactory<AppDbContext>)CreateService(serviceProvider, factoryDescriptor);
                var counter = serviceProvider.GetRequiredService<SaveChangesCounter>();
                return new CountingDbContextFactory(innerFactory, counter);
            },
            factoryDescriptor.Lifetime));
    }

    private static object CreateService(IServiceProvider serviceProvider, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is not null)
        {
            return descriptor.ImplementationInstance;
        }

        if (descriptor.ImplementationFactory is not null)
        {
            return descriptor.ImplementationFactory(serviceProvider);
        }

        if (descriptor.ImplementationType is not null)
        {
            return ActivatorUtilities.GetServiceOrCreateInstance(serviceProvider, descriptor.ImplementationType);
        }

        throw new InvalidOperationException(
            $"Service descriptor for '{descriptor.ServiceType}' does not expose an implementation.");
    }

    private sealed class CountingDbContextFactory(
        IDbContextFactory<AppDbContext> innerFactory,
        SaveChangesCounter counter) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext()
        {
            var dbContext = innerFactory.CreateDbContext();
            AttachSaveCounter(dbContext);
            return dbContext;
        }

        public async Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            var dbContext = await innerFactory.CreateDbContextAsync(cancellationToken);
            AttachSaveCounter(dbContext);
            return dbContext;
        }

        private void AttachSaveCounter(AppDbContext dbContext)
        {
            dbContext.SavedChanges += (_, _) => counter.Increment();
        }
    }

    private sealed class SaveChangesCounter
    {
        private int saveChangesCount;

        public int SaveChangesCount => saveChangesCount;

        public void Increment()
        {
            Interlocked.Increment(ref saveChangesCount);
        }

        public void Reset()
        {
            Interlocked.Exchange(ref saveChangesCount, 0);
        }
    }

    private sealed record NodeSnapshot(
        Guid Id,
        string? ParentNodeKey,
        double PositionX,
        double PositionY);
}
