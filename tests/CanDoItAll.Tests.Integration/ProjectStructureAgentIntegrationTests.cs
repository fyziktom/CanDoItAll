using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectStructureAgentIntegrationTests
{
    private static readonly ProjectStructureAgentContext DefaultAgent = new(
        "integration-agent",
        "Integration Agent",
        "integration-machine",
        IntegrationTestPaths.RepositoryRoot,
        "tests/project-structure",
        Guid.NewGuid().ToString("N"));

    private static readonly ProjectStructureRuntimeAgentContext DefaultRuntimeAgent = new(
        DefaultAgent.AgentId,
        DefaultAgent.AgentName,
        DefaultAgent.MachineName,
        DefaultAgent.RepositoryRoot,
        DefaultAgent.BranchName,
        DefaultAgent.SessionId);

    [Fact]
    public async Task LeaseService_AcquireAsync_reports_conflict_details_for_other_agents()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var leaseService = scope.ServiceProvider.GetRequiredService<ProjectStructureLeaseService>();

        var initialLease = await leaseService.AcquireAsync(
            new ProjectStructureLeaseAcquireRequest(ProjectStructureLeaseScopeKind.Project, "project:alpha", "Initial mutation", 15),
            DefaultAgent);

        Assert.True(initialLease.IsActive);

        var competitor = DefaultAgent with
        {
            AgentId = "other-agent",
            AgentName = "Other Agent",
            MachineName = "other-machine"
        };

        var conflict = await Assert.ThrowsAsync<ProjectStructureLeaseConflictException>(() =>
            leaseService.AcquireAsync(
                new ProjectStructureLeaseAcquireRequest(ProjectStructureLeaseScopeKind.Project, "project:alpha", "Competing mutation", 15),
                competitor));

        Assert.Equal(ProjectStructureLeaseScopeKind.Project, conflict.Conflict.ScopeKind);
        Assert.Equal("project:alpha", conflict.Conflict.ScopeKey);
        Assert.Equal(DefaultAgent.AgentId, conflict.Conflict.AgentId);
        Assert.Equal(DefaultAgent.AgentName, conflict.Conflict.AgentName);
        Assert.Equal(DefaultAgent.MachineName, conflict.Conflict.MachineName);
    }

    [Fact]
    public async Task LeaseService_RunWithProjectMutationLeaseAsync_preserves_existing_owned_lease()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var leaseService = scope.ServiceProvider.GetRequiredService<ProjectStructureLeaseService>();

        var projectId = Guid.NewGuid();
        var initialLease = await leaseService.AcquireAsync(
            new ProjectStructureLeaseAcquireRequest(ProjectStructureLeaseScopeKind.Project, projectId.ToString(), "Long-lived validation lease", 30),
            DefaultAgent);

        var result = await leaseService.RunWithProjectMutationLeaseAsync(
            projectId,
            null,
            DefaultAgent,
            "Temporary mutation without explicit token",
            _ => Task.FromResult("ok"));

        var preservedLease = await leaseService.ValidateOwnedLeaseAsync(
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            initialLease.LeaseToken,
            DefaultAgent);

        Assert.Equal("ok", result);
        Assert.NotNull(preservedLease);
        Assert.Equal(initialLease.LeaseToken, preservedLease!.LeaseToken);
    }

    [Fact]
    public async Task RuntimeGateway_CreateAssetAsync_replays_duplicate_idempotency_key_without_duplicate_node()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var gateway = scope.ServiceProvider.GetRequiredService<IProjectStructureRuntimeGateway>();

        var projectId = await CreateProjectAsync(projects, "Runtime asset idempotency");
        var idempotencyKey = "office365:runtime-message-1:summary";
        var first = await gateway.CreateAssetAsync(
            projectId,
            new ProjectStructureRuntimeAssetCreateRequest(
                ProjectObjectType.File,
                "Watched email summary",
                "Generated from Office365 email",
                "First delivery should create the markdown asset.",
                CreateRuntimeMediaPayload("summary.md", "text/markdown", "# Summary"),
                ParentNodeKey: $"project:{projectId:D}",
                ObjectSubtype: "md",
                IdempotencyKey: idempotencyKey,
                IdempotencyBatchKey: idempotencyKey),
            DefaultRuntimeAgent);
        var replayed = await gateway.CreateAssetAsync(
            projectId,
            new ProjectStructureRuntimeAssetCreateRequest(
                ProjectObjectType.File,
                "Watched email summary duplicate",
                "Generated from Office365 email",
                "A retry after mark-processed failure must not create a second asset.",
                CreateRuntimeMediaPayload("summary-retry.md", "text/markdown", "# Duplicate"),
                ParentNodeKey: $"project:{projectId:D}",
                ObjectSubtype: "md",
                IdempotencyKey: idempotencyKey,
                IdempotencyBatchKey: idempotencyKey),
            DefaultRuntimeAgent);

        var surface = await workbench.GetStructureAsync(projectId);
        var matchingNodes = surface.Nodes
            .Where(node => HasRuntimeIdempotencyKey(node, idempotencyKey))
            .ToList();

        Assert.Equal(first.Id, replayed.Id);
        Assert.Single(matchingNodes);
        Assert.Equal("Watched email summary", matchingNodes[0].Title);
    }

    [Fact]
    public async Task RuntimeGateway_CreateNodeAsync_serializes_concurrent_duplicate_idempotency_key()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var gateway = scope.ServiceProvider.GetRequiredService<IProjectStructureRuntimeGateway>();

        var projectId = await CreateProjectAsync(projects, "Runtime task idempotency");
        var idempotencyKey = "office365:runtime-message-2:tasks:001";
        var batchKey = "office365:runtime-message-2:tasks";
        var firstRequest = new ProjectStructureRuntimeNodeCreateRequest(
            ProjectObjectType.WorkItem,
            "Confirm renewal scope",
            "Office365 task",
            "Task extracted from a watched email.",
            $"project:{projectId:D}",
            ObjectSubtype: "task",
            IdempotencyKey: idempotencyKey,
            IdempotencyBatchKey: batchKey);
        var duplicateRequest = firstRequest with
        {
            Title = "Confirm renewal scope duplicate",
            Notes = "Concurrent retry should replay the original node."
        };

        var results = await Task.WhenAll(
            gateway.CreateNodeAsync(projectId, firstRequest, DefaultRuntimeAgent),
            gateway.CreateNodeAsync(projectId, duplicateRequest, DefaultRuntimeAgent));
        var surface = await workbench.GetStructureAsync(projectId);
        var matchingNodes = surface.Nodes
            .Where(node => HasRuntimeIdempotencyKey(node, idempotencyKey))
            .ToList();

        Assert.Equal(results[0].Id, results[1].Id);
        Assert.Single(matchingNodes);
        Assert.Equal("Confirm renewal scope", matchingNodes[0].Title);
    }

    [Fact]
    public async Task ChecklistService_GetChecklistAsync_propagates_child_priority_and_stops_at_paused_parent()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var checklistService = scope.ServiceProvider.GetRequiredService<ProjectStructureChecklistService>();

        var projectId = await CreateProjectAsync(projects, "Checklist propagation");
        var grandparent = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Delivery branch",
                string.Empty,
                "Top-level delivery branch.",
                $"project:{projectId}",
                360,
                220,
                null,
                null,
                "delivery"));
        var parent = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Execution lane",
                string.Empty,
                "Mid-level branch.",
                grandparent.Id,
                540,
                320,
                null,
                null,
                "implementation"));
        var child = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Ship release",
                string.Empty,
                "Highest priority task.",
                parent.Id,
                760,
                440,
                null,
                null,
                "task"));

        await workbench.UpdateObjectPriorityAsync(projectId, [child.Id], 1);

        var checklist = await checklistService.GetChecklistAsync(projectId, new ProjectStructureChecklistRequest(IncludePaused: true));
        var grandparentItem = Assert.Single(checklist.Items, item => item.NodeId == grandparent.Id);
        var parentItem = Assert.Single(checklist.Items, item => item.NodeId == parent.Id);
        var childItem = Assert.Single(checklist.Items, item => item.NodeId == child.Id);

        Assert.Equal(1, grandparentItem.EffectivePriority);
        Assert.Equal(1, parentItem.EffectivePriority);
        Assert.Equal(1, childItem.EffectivePriority);
        Assert.Contains(childItem.Prerequisites, prerequisite => prerequisite.NodeId == parent.Id && prerequisite.Reason == "parent");
        Assert.Contains(childItem.Prerequisites, prerequisite => prerequisite.NodeId == grandparent.Id && prerequisite.Reason == "parent");

        await workbench.UpdateObjectMarkerAsync(projectId, [parent.Id], "pause", "warn", "Paused");

        var pausedChecklist = await checklistService.GetChecklistAsync(projectId, new ProjectStructureChecklistRequest(IncludePaused: true));
        var pausedGrandparent = Assert.Single(pausedChecklist.Items, item => item.NodeId == grandparent.Id);
        var pausedParent = Assert.Single(pausedChecklist.Items, item => item.NodeId == parent.Id);
        var pausedChild = Assert.Single(pausedChecklist.Items, item => item.NodeId == child.Id);

        Assert.Equal(0, pausedGrandparent.EffectivePriority);
        Assert.Equal(0, pausedParent.EffectivePriority);
        Assert.Equal(1, pausedChild.EffectivePriority);
    }

    [Fact]
    public async Task AgentService_GetDependenciesAsync_reports_readiness_and_default_durations()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();

        var projectId = await CreateProjectAsync(projects, "Dependency readiness");
        var note = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Architect note",
                string.Empty,
                "This note must be finished first.",
                $"project:{projectId}",
                360,
                240));
        var task = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Implement feature",
                string.Empty,
                "Blocked by the architect note.",
                $"project:{projectId}",
                620,
                360,
                new DateTimeOffset(2026, 4, 3, 8, 0, 0, TimeSpan.Zero),
                null,
                "task",
                null,
                null,
                7200));

        await workbench.LinkObjectsAsync(projectId, task.Id, note.Id, ProjectObjectLinkKind.DependsOn);

        var beforeCompletion = await agentService.GetDependenciesAsync(
            projectId,
            new ProjectStructureDependencyQueryRequest(DefaultDurationSeconds: 5400));
        var noteItem = Assert.Single(beforeCompletion.Items, item => item.NodeId == note.Id);
        var taskItem = Assert.Single(beforeCompletion.Items, item => item.NodeId == task.Id);

        Assert.True(noteItem.CanExecute);
        Assert.Null(noteItem.DurationSeconds);
        Assert.Equal(5400, noteItem.EffectiveDurationSeconds);
        Assert.False(taskItem.CanExecute);
        Assert.Equal(7200, taskItem.DurationSeconds);
        Assert.Equal(new DateTimeOffset(2026, 4, 3, 10, 0, 0, TimeSpan.Zero), taskItem.EndUtc);
        Assert.Contains(taskItem.Prerequisites, prerequisite => prerequisite.NodeId == note.Id && prerequisite.Reason == "depends-on" && !prerequisite.IsFinished);

        await workbench.UpdateObjectProgressAsync(projectId, [note.Id], "complete", 100);

        var afterCompletion = await agentService.GetDependenciesAsync(
            projectId,
            new ProjectStructureDependencyQueryRequest(DefaultDurationSeconds: 5400));
        var readyTask = Assert.Single(afterCompletion.Items, item => item.NodeId == task.Id);

        Assert.True(readyTask.CanExecute);
        Assert.Contains(readyTask.Prerequisites, prerequisite => prerequisite.NodeId == note.Id && prerequisite.IsFinished);
        Assert.Contains(noteItem.Dependents, dependent => dependent.NodeId == task.Id && dependent.Reason == "required-for");
    }

    [Fact]
    public async Task AgentService_MoveNodesToNewSubprojectAsync_creates_subproject_and_preserves_dependency_links()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();

        var sourceProjectId = await CreateProjectAsync(projects, "Selected nodes source");
        var parentBlock = await workbench.CreateObjectAsync(
            sourceProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Implementation lane",
                string.Empty,
                "Parent block remains in the source project.",
                $"project:{sourceProjectId}",
                320,
                220,
                null,
                null,
                "implementation"));
        var prerequisiteTask = await workbench.CreateObjectAsync(
            sourceProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Prepare API contract",
                string.Empty,
                "Move this selected task.",
                parentBlock.Id,
                520,
                280,
                null,
                null,
                "task"));
        var dependentTask = await workbench.CreateObjectAsync(
            sourceProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Implement endpoint",
                string.Empty,
                "Move this selected task and preserve its dependency.",
                parentBlock.Id,
                720,
                340,
                null,
                null,
                "task"));
        var childNote = await workbench.CreateObjectAsync(
            sourceProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Endpoint notes",
                string.Empty,
                "Descendant should move with the selected task.",
                dependentTask.Id,
                900,
                420));
        await workbench.LinkObjectsAsync(sourceProjectId, dependentTask.Id, prerequisiteTask.Id, ProjectObjectLinkKind.DependsOn);

        var result = await agentService.MoveNodesToNewSubprojectAsync(
            sourceProjectId,
            new ProjectStructureNodesToSubprojectInput(
                "Extracted endpoint work",
                [prerequisiteTask.Id, dependentTask.Id],
                IncludeDescendants: true),
            DefaultAgent);

        Assert.Equal("Extracted endpoint work", result.TargetProjectName);
        Assert.Equal(3, result.MovedNodeCount);
        Assert.Equal(2, result.MovedRootCount);
        Assert.Contains(prerequisiteTask.Id, result.MovedNodeIds);
        Assert.Contains(dependentTask.Id, result.MovedNodeIds);
        Assert.Contains(childNote.Id, result.MovedNodeIds);

        var hierarchy = await projects.GetHierarchyAsync(sourceProjectId);
        Assert.Contains(hierarchy.ChildProjects, project => project.Id == result.TargetProjectId);

        var sourceSurface = await workbench.GetStructureAsync(sourceProjectId);
        Assert.Contains(sourceSurface.Nodes, node => node.Id == parentBlock.Id);
        Assert.DoesNotContain(sourceSurface.Nodes, node => node.Id == prerequisiteTask.Id);
        Assert.DoesNotContain(sourceSurface.Nodes, node => node.Id == dependentTask.Id);

        var targetSurface = await workbench.GetStructureAsync(result.TargetProjectId);
        var movedPrerequisite = Assert.Single(targetSurface.Nodes, node => node.Id == prerequisiteTask.Id);
        var movedDependent = Assert.Single(targetSurface.Nodes, node => node.Id == dependentTask.Id);
        var movedChildNote = Assert.Single(targetSurface.Nodes, node => node.Id == childNote.Id);

        Assert.Equal($"project:{result.TargetProjectId}", movedPrerequisite.ParentId);
        Assert.Equal($"project:{result.TargetProjectId}", movedDependent.ParentId);
        Assert.Equal(dependentTask.Id, movedChildNote.ParentId);
        Assert.Contains(targetSurface.Links, link =>
            link.SourceId == dependentTask.Id &&
            link.TargetId == prerequisiteTask.Id &&
            link.Kind == ProjectObjectLinkKind.DependsOn);
    }

    [Fact]
    public async Task AgentService_MoveNodesToNewSubprojectAsync_without_descendants_reparents_left_behind_children()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();

        var sourceProjectId = await CreateProjectAsync(projects, "Selected node without descendants");
        var parentBlock = await workbench.CreateObjectAsync(
            sourceProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Source parent",
                string.Empty,
                "Parent should keep child when selected node moves alone.",
                $"project:{sourceProjectId}",
                320,
                220,
                null,
                null,
                "implementation"));
        var selectedTask = await workbench.CreateObjectAsync(
            sourceProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Move task only",
                string.Empty,
                "Move without descendants.",
                parentBlock.Id,
                520,
                280,
                null,
                null,
                "task"));
        var childNote = await workbench.CreateObjectAsync(
            sourceProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Left behind child",
                string.Empty,
                "This child should not keep a cross-project parent.",
                selectedTask.Id,
                720,
                340));

        var result = await agentService.MoveNodesToNewSubprojectAsync(
            sourceProjectId,
            new ProjectStructureNodesToSubprojectInput(
                "Task only subproject",
                [selectedTask.Id],
                IncludeDescendants: false),
            DefaultAgent);

        Assert.Equal(1, result.MovedNodeCount);
        Assert.Contains(selectedTask.Id, result.MovedNodeIds);
        Assert.DoesNotContain(childNote.Id, result.MovedNodeIds);

        var sourceSurface = await workbench.GetStructureAsync(sourceProjectId);
        var leftBehindChild = Assert.Single(sourceSurface.Nodes, node => node.Id == childNote.Id);
        Assert.Equal(parentBlock.Id, leftBehindChild.ParentId);
        Assert.DoesNotContain(sourceSurface.Nodes, node => node.Id == selectedTask.Id);

        var targetSurface = await workbench.GetStructureAsync(result.TargetProjectId);
        var movedTask = Assert.Single(targetSurface.Nodes, node => node.Id == selectedTask.Id);
        Assert.Equal($"project:{result.TargetProjectId}", movedTask.ParentId);
    }

    [Fact]
    public async Task AgentService_UpdateNodeAsync_reclassifies_placeholder_nodes_into_typed_blocks()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var leaseService = scope.ServiceProvider.GetRequiredService<ProjectStructureLeaseService>();

        var projectId = await CreateProjectAsync(projects, "Node reclassification");
        var placeholder = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Features",
                "Scratch",
                "Placeholder note that should become a typed block.",
                $"project:{projectId}",
                420,
                220));
        var lease = await leaseService.AcquireAsync(
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString(),
                "Reclassify structure node",
                15),
            DefaultAgent);

        var updated = await agentService.UpdateNodeAsync(
            projectId,
            placeholder.Id,
            new ProjectStructureNodeEditInput(
                "Features",
                "Feature area",
                "Promoted into a typed feature block.",
                ObjectType: ProjectObjectType.ProjectBlock,
                ObjectSubtype: "feature",
                LeaseToken: lease.LeaseToken),
            DefaultAgent);

        var surface = await workbench.GetStructureAsync(projectId);
        var updatedNode = Assert.Single(surface.Nodes, node => node.Id == placeholder.Id);

        Assert.Equal(ProjectObjectType.ProjectBlock, updated.ObjectType);
        Assert.Equal("feature", updated.ObjectSubtype);
        Assert.Equal(ProjectObjectType.ProjectBlock, updatedNode.ObjectType);
        Assert.Equal("feature", updatedNode.ObjectSubtype);
        Assert.Equal("Feature area", updatedNode.Subtitle);
    }

    [Fact]
    public async Task AgentService_MoveNodeAsync_updates_canvas_coordinates()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var leaseService = scope.ServiceProvider.GetRequiredService<ProjectStructureLeaseService>();

        var projectId = await CreateProjectAsync(projects, "Node move");
        var node = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Features",
                string.Empty,
                "Move this branch away from overlap.",
                $"project:{projectId}",
                420,
                220,
                null,
                null,
                "feature"));
        var lease = await leaseService.AcquireAsync(
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.Project,
                projectId.ToString(),
                "Move structure node",
                15),
            DefaultAgent);

        await agentService.MoveNodeAsync(
            projectId,
            new ProjectStructureNodeMoveInput(node.Id, 980, 540, lease.LeaseToken),
            DefaultAgent);

        var surface = await workbench.GetStructureAsync(projectId);
        var movedNode = Assert.Single(surface.Nodes, item => item.Id == node.Id);

        Assert.Equal(980d, movedNode.X);
        Assert.Equal(540d, movedNode.Y);
    }

    [Fact]
    public async Task AgentService_CreateAssetRevisionAsync_creates_child_asset_and_derivedfrom_link()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();

        var projectId = await CreateProjectAsync(projects, "Asset revision");
        var original = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.File,
                "Delivery packet",
                "Original PDF",
                "Seed original document.",
                $"project:{projectId}",
                420,
                240,
                null,
                null,
                "pdf",
                CreateMediaPayload("delivery-packet.pdf", "application/pdf", "%PDF-1.4 original packet"),
                null));

        var revision = await agentService.CreateAssetRevisionAsync(
            projectId,
            original.Id,
            new ProjectStructureAssetRevisionRequest(
                "Delivery packet v2",
                "Revised PDF",
                "Create a revised document node.",
                CreateMediaPayload("delivery-packet-v2.pdf", "application/pdf", "%PDF-1.4 revised packet"),
                "pdf",
                null,
                null),
            DefaultAgent);

        Assert.Equal(projectId, revision.ProjectId);
        Assert.Equal(original.Id, revision.RevisionParentNodeId);

        var surface = await workbench.GetStructureAsync(projectId);
        var revisionNode = Assert.Single(surface.Nodes, node => node.Id == revision.NodeId);
        Assert.Equal(original.Id, revisionNode.ParentId);
        Assert.Equal("delivery-packet-v2.pdf", revisionNode.MediaOriginalFileName);
        Assert.Contains(surface.Links, link =>
            string.Equals(link.SourceId, revision.NodeId, StringComparison.Ordinal) &&
            string.Equals(link.TargetId, original.Id, StringComparison.Ordinal) &&
            link.Kind == ProjectObjectLinkKind.DerivedFrom);
    }

    [Fact]
    public async Task AgentService_CreateAssetAsync_accepts_workspace_source_path()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var workspacePaths = scope.ServiceProvider.GetRequiredService<IWorkspacePathResolutionService>();

        var projectId = await CreateProjectAsync(projects, "Source path image asset");
        var sourceRelativePath = $"artifacts/process-runs/{Guid.NewGuid():N}/inventory.png";
        var sourceResolution = workspacePaths.ResolveFilePath(sourceRelativePath, allowMissing: true);
        Directory.CreateDirectory(Path.GetDirectoryName(sourceResolution.FullPath)!);
        await File.WriteAllBytesAsync(sourceResolution.FullPath, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A]);

        var created = await agentService.CreateAssetAsync(
            projectId,
            new ProjectStructureAssetCreateInput(
                ProjectObjectType.ImageAsset,
                "Inventory screenshot",
                "Captured /inventory route",
                "Accepted screenshot from the process-run artifact path.",
                null,
                $"project:{projectId}",
                "screenshot",
                null,
                null,
                sourceRelativePath,
                "inventory.png",
                "image/png"),
            DefaultAgent);

        var surface = await workbench.GetStructureAsync(projectId);
        var node = Assert.Single(surface.Nodes, item => item.Id == created.Id);

        Assert.Equal(ProjectObjectType.ImageAsset, node.ObjectType);
        Assert.Equal("screenshot", node.ObjectSubtype);
        Assert.Equal("inventory.png", node.MediaOriginalFileName);
        Assert.Equal("image/png", node.MediaContentType);
        Assert.False(string.IsNullOrWhiteSpace(node.MediaRelativePath));
        Assert.StartsWith("artifacts/scopes/organization/", sourceResolution.RelativePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AgentService_CreateAssetAsync_accepts_external_source_url()
    {
        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.7 external brochure");
        var handler = new DelegateHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://assets.example.test/pax/A35-PINpad-PAX-EMEA-February2026.pdf", request.RequestUri?.ToString());
            return CreateBinaryResponse(pdfBytes, "application/pdf");
        });

        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            ConfigureServices = services =>
            {
                services.RemoveAll<IHttpClientFactory>();
                services.AddSingleton<IHttpClientFactory>(_ => new StaticHttpClientFactory(handler));
            }
        });
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var workspacePathResolver = scope.ServiceProvider.GetRequiredService<IWorkspacePathResolver>();

        var projectId = await CreateProjectAsync(projects, "External source PDF asset");
        var created = await agentService.CreateAssetAsync(
            projectId,
            new ProjectStructureAssetCreateInput(
                ProjectObjectType.File,
                "A35 PINpad brochure",
                "Downloaded PDF",
                "Asset should be downloaded from a public URL.",
                null,
                $"project:{projectId}",
                "pdf",
                null,
                null,
                null,
                "A35-PINpad-PAX-EMEA-February2026.pdf",
                null,
                "https://assets.example.test/pax/A35-PINpad-PAX-EMEA-February2026.pdf"),
            DefaultAgent);

        var surface = await workbench.GetStructureAsync(projectId);
        var node = Assert.Single(surface.Nodes, item => item.Id == created.Id);

        Assert.Equal(ProjectObjectType.File, node.ObjectType);
        Assert.Equal("pdf", node.ObjectSubtype);
        Assert.Equal("A35-PINpad-PAX-EMEA-February2026.pdf", node.MediaOriginalFileName);
        Assert.Equal("application/pdf", node.MediaContentType);
        Assert.False(string.IsNullOrWhiteSpace(node.MediaRelativePath));

        var storedBytes = await File.ReadAllBytesAsync(Path.Combine(workspacePathResolver.ResolveWorkspaceRoot(), node.MediaRelativePath));
        Assert.Equal(pdfBytes, storedBytes);
    }

    [Fact]
    public async Task AgentService_CreateAssetAsync_downloads_http_source_workspace_path_as_compatibility_fallback()
    {
        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.7 compatibility path");
        var handler = new DelegateHttpMessageHandler(_ => CreateBinaryResponse(pdfBytes, "application/pdf"));

        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            ConfigureServices = services =>
            {
                services.RemoveAll<IHttpClientFactory>();
                services.AddSingleton<IHttpClientFactory>(_ => new StaticHttpClientFactory(handler));
            }
        });
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();

        var projectId = await CreateProjectAsync(projects, "External URL in sourceWorkspacePath");
        var created = await agentService.CreateAssetAsync(
            projectId,
            new ProjectStructureAssetCreateInput(
                ProjectObjectType.File,
                "A8900 mobile brochure",
                "Downloaded PDF",
                "Asset should download even when an agent supplies the URL in sourceWorkspacePath.",
                null,
                $"project:{projectId}",
                "pdf",
                null,
                null,
                "https://assets.example.test/pax/A8900-Mobile-PAX-EMEA-July2024.pdf",
                "A8900-Mobile-PAX-EMEA-July2024.pdf",
                "application/pdf"),
            DefaultAgent);

        var surface = await workbench.GetStructureAsync(projectId);
        var node = Assert.Single(surface.Nodes, item => item.Id == created.Id);

        Assert.Equal("pdf", node.ObjectSubtype);
        Assert.Equal("A8900-Mobile-PAX-EMEA-July2024.pdf", node.MediaOriginalFileName);
        Assert.Equal("application/pdf", node.MediaContentType);
    }

    [Fact]
    public async Task AgentService_CreateAssetAsync_rejects_loopback_external_source_url()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();

        var projectId = await CreateProjectAsync(projects, "Blocked loopback PDF asset");
        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            agentService.CreateAssetAsync(
                projectId,
                new ProjectStructureAssetCreateInput(
                    ProjectObjectType.File,
                    "Internal PDF",
                    "Blocked",
                    "Loopback downloads should not be allowed from agent asset creation.",
                    null,
                    $"project:{projectId}",
                    "pdf",
                    null,
                    null,
                    null,
                    "internal.pdf",
                    "application/pdf",
                    "http://127.0.0.1/internal.pdf"),
                DefaultAgent));

        Assert.Equal("SourceUrlNotAllowed", exception.ErrorCode);
    }

    [Fact]
    public async Task AgentService_ImportAsync_accepts_mermaid_mindmap()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();

        var projectId = await CreateProjectAsync(projects, "Mermaid import");
        var result = await agentService.ImportAsync(
            new ProjectStructureImportRequest(
                projectId,
                null,
                ProjectStructureImportSourceKind.Mermaid,
                "Imported Mermaid",
                """
                mindmap
                  Root
                    Delivery
                      Checklist
                """),
            DefaultAgent);

        Assert.Contains(result.Warnings, warning => warning.Contains("indentation", StringComparison.OrdinalIgnoreCase));

        var surface = await workbench.GetStructureAsync(projectId);
        Assert.Contains(surface.Nodes, node => node.Title == "Imported Mermaid");
        Assert.Contains(surface.Nodes, node => node.Title == "Root");
        Assert.Contains(surface.Nodes, node => node.Title == "Delivery");
        Assert.Contains(surface.Nodes, node => node.Title == "Checklist");
    }

    [Fact]
    public async Task AgentService_ImportAsync_accepts_docx_headings()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();

        var projectId = await CreateProjectAsync(projects, "Docx import");
        var docxPayload = CreateMediaPayload(
            "outline.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            BuildDocx("Launch plan", ("Heading2", "Checklist"), ("Heading2", "Evidence")));

        var result = await agentService.ImportAsync(
            new ProjectStructureImportRequest(
                projectId,
                null,
                ProjectStructureImportSourceKind.DocxOutline,
                "Imported DOCX",
                null,
                docxPayload),
            DefaultAgent);

        Assert.NotEmpty(result.CreatedNodeIds);

        var surface = await workbench.GetStructureAsync(projectId);
        Assert.Contains(surface.Nodes, node => node.Title == "Imported DOCX");
        Assert.Contains(surface.Nodes, node => node.Title == "Launch plan");
        Assert.Contains(surface.Nodes, node => node.Title == "Checklist");
        Assert.Contains(surface.Nodes, node => node.Title == "Evidence");
    }

    [Fact]
    public async Task AgentService_ImportAsync_accepts_xmind_json_packages()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();

        var projectId = await CreateProjectAsync(projects, "XMind import");
        var xmindPayload = CreateMediaPayload(
            "outline.xmind",
            "application/octet-stream",
            BuildXmindJsonPackage());

        var result = await agentService.ImportAsync(
            new ProjectStructureImportRequest(
                projectId,
                null,
                ProjectStructureImportSourceKind.XmindMap,
                "Imported XMind",
                null,
                xmindPayload),
            DefaultAgent);

        Assert.NotEmpty(result.CreatedNodeIds);

        var surface = await workbench.GetStructureAsync(projectId);
        Assert.Contains(surface.Nodes, node => node.Title == "Imported XMind");
        Assert.Contains(surface.Nodes, node => node.Title == "Roadmap");
        Assert.Contains(surface.Nodes, node => node.Title == "Execution");
        Assert.Contains(surface.Nodes, node => node.Title == "Validation");
    }

    [Fact]
    public async Task AgentService_ImportAsync_accepts_xmind_xml_packages_across_all_sheets()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();

        var projectId = await CreateProjectAsync(projects, "XMind xml import");
        var xmindPayload = CreateMediaPayload(
            "outline.xmind",
            "application/octet-stream",
            BuildXmindXmlPackage());

        var result = await agentService.ImportAsync(
            new ProjectStructureImportRequest(
                projectId,
                null,
                ProjectStructureImportSourceKind.XmindMap,
                "Imported XMind XML",
                null,
                xmindPayload),
            DefaultAgent);

        Assert.NotEmpty(result.CreatedNodeIds);

        var surface = await workbench.GetStructureAsync(projectId);
        Assert.Contains(surface.Nodes, node => node.Title == "Imported XMind XML");
        Assert.Contains(surface.Nodes, node => node.Title == "Features");
        Assert.Contains(surface.Nodes, node => node.Title == "Management of projects");
        Assert.Contains(surface.Nodes, node => node.Title == "Implementation");
        Assert.Contains(surface.Nodes, node => node.Title == "Shared");
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

    private static ProjectObjectMediaPayload CreateMediaPayload(string fileName, string contentType, string textContent)
    {
        return new ProjectObjectMediaPayload(
            fileName,
            contentType,
            Convert.ToBase64String(Encoding.UTF8.GetBytes(textContent)));
    }

    private static ProjectStructureRuntimeMediaPayload CreateRuntimeMediaPayload(string fileName, string contentType, string textContent)
    {
        return new ProjectStructureRuntimeMediaPayload(
            fileName,
            contentType,
            Convert.ToBase64String(Encoding.UTF8.GetBytes(textContent)));
    }

    private static bool HasRuntimeIdempotencyKey(ProjectStructureNode node, string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(node.MetadataJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(node.MetadataJson);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty(ProjectStructureRuntimeIdempotencyMetadata.MetadataPropertyName, out var runtimeMetadata) &&
                   runtimeMetadata.ValueKind == JsonValueKind.Object &&
                   runtimeMetadata.TryGetProperty(ProjectStructureRuntimeIdempotencyMetadata.IdempotencyKeyPropertyName, out var key) &&
                   key.ValueKind == JsonValueKind.String &&
                   string.Equals(key.GetString(), idempotencyKey, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static ProjectObjectMediaPayload CreateMediaPayload(string fileName, string contentType, byte[] bytes)
    {
        return new ProjectObjectMediaPayload(
            fileName,
            contentType,
            Convert.ToBase64String(bytes));
    }

    private static HttpResponseMessage CreateBinaryResponse(byte[] bytes, string contentType)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
        };
        response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        return response;
    }

    private sealed class StaticHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class DelegateHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }

    private static byte[] BuildDocx(string rootHeading, params (string Style, string Text)[] children)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("word/document.xml");
            using var entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream, Encoding.UTF8, leaveOpen: false);
            writer.WriteLine(
                """
                <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
                  <w:body>
                """);
            WriteParagraph(writer, "Heading1", rootHeading);
            foreach (var (style, text) in children)
            {
                WriteParagraph(writer, style, text);
            }

            writer.WriteLine(
                """
                  </w:body>
                </w:document>
                """);
        }

        return stream.ToArray();
    }

    private static byte[] BuildXmindJsonPackage()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("content.json");
            using var entryStream = entry.Open();
            using var writer = new Utf8JsonWriter(entryStream);
            writer.WriteStartArray();
            writer.WriteStartObject();
            writer.WritePropertyName("rootTopic");
            writer.WriteStartObject();
            writer.WriteString("title", "Roadmap");
            writer.WritePropertyName("children");
            writer.WriteStartObject();
            writer.WritePropertyName("attached");
            writer.WriteStartArray();
            WriteXmindChild(writer, "Execution");
            WriteXmindChild(writer, "Validation");
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.WriteEndArray();
            writer.Flush();
        }

        return stream.ToArray();
    }

    private static byte[] BuildXmindXmlPackage()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("content.xml");
            using var entryStream = entry.Open();
            using var writer = new StreamWriter(entryStream, Encoding.UTF8, leaveOpen: false);
            writer.Write(
                """
                <xmap-content xmlns="urn:xmind:xmap:xmlns:content:2.0">
                  <sheet>
                    <topic>
                      <title>Features</title>
                      <children>
                        <topics>
                          <topic>
                            <title>Management of projects</title>
                          </topic>
                        </topics>
                      </children>
                    </topic>
                  </sheet>
                  <sheet>
                    <topic>
                      <title>Implementation</title>
                      <children>
                        <topics>
                          <topic>
                            <title>Shared</title>
                          </topic>
                        </topics>
                      </children>
                    </topic>
                  </sheet>
                </xmap-content>
                """);
        }

        return stream.ToArray();
    }

    private static void WriteParagraph(StreamWriter writer, string style, string text)
    {
        writer.WriteLine(
            $"""
                <w:p>
                  <w:pPr>
                    <w:pStyle w:val="{style}" />
                  </w:pPr>
                  <w:r>
                    <w:t>{System.Security.SecurityElement.Escape(text)}</w:t>
                  </w:r>
                </w:p>
            """);
    }

    private static void WriteXmindChild(Utf8JsonWriter writer, string title)
    {
        writer.WriteStartObject();
        writer.WriteString("title", title);
        writer.WriteEndObject();
    }
}
