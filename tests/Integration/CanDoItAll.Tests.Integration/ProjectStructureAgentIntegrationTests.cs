using System.IO.Compression;
using System.Net;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
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
    private const string LiveComfyUiFluxProofVariable = "CANDOITALL_RUN_LIVE_COMFYUI_FLUX_PROOF";
    private const string LiveComfyUiFluxProofDirectoryVariable = "CANDOITALL_LIVE_COMFYUI_FLUX_PROOF_DIR";

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
    public async Task LeaseService_RunWithProjectMutationLeaseAsync_releases_temporary_lease_after_callback_cancellation()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var leaseService = scope.ServiceProvider.GetRequiredService<ProjectStructureLeaseService>();

        var projectId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            leaseService.RunWithProjectMutationLeaseAsync(
                projectId,
                null,
                DefaultAgent,
                "Cancelled temporary mutation",
                token =>
                {
                    cancellation.Cancel();
                    token.ThrowIfCancellationRequested();
                    return Task.FromResult("unreachable");
                },
                cancellation.Token));

        var activeLease = await leaseService.GetActiveLeaseAsync(
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            CancellationToken.None);

        Assert.Null(activeLease);
    }

    [Fact]
    public async Task LeaseService_RunWithProjectMutationLeaseAsync_waits_once_for_near_expiry_competing_lease()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var leaseService = scope.ServiceProvider.GetRequiredService<ProjectStructureLeaseService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = Guid.NewGuid();
        var competingLease = await leaseService.AcquireAsync(
            new ProjectStructureLeaseAcquireRequest(ProjectStructureLeaseScopeKind.Project, projectId.ToString(), "Competing short mutation", 1),
            DefaultAgent);

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync())
        {
            var leaseRecord = await dbContext.Set<ProjectStructureLeaseRecord>()
                .SingleAsync(item => item.LeaseToken == competingLease.LeaseToken);

            var now = DateTimeOffset.UtcNow;
            leaseRecord.RenewedAtUtc = now;
            leaseRecord.ExpiresAtUtc = now.AddMilliseconds(750);
            await dbContext.SaveChangesAsync();
        }

        var nextAgent = DefaultAgent with
        {
            AgentId = "next-agent",
            AgentName = "Next Agent",
            MachineName = "next-machine"
        };

        var elapsed = Stopwatch.StartNew();
        var result = await leaseService.RunWithProjectMutationLeaseAsync(
            projectId,
            null,
            nextAgent,
            "Wait for near-expiry competing mutation",
            _ => Task.FromResult("ok"));
        elapsed.Stop();

        var activeLease = await leaseService.GetActiveLeaseAsync(
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString(),
            CancellationToken.None);

        Assert.Equal("ok", result);
        Assert.True(elapsed.Elapsed >= TimeSpan.FromMilliseconds(500));
        Assert.Null(activeLease);
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
    public async Task StartProcessNodeAsync_resolves_linked_definition_targets_source_node_and_records_launch_context()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var assignmentStore = scope.ServiceProvider.GetRequiredService<IProcessRuntimeStepAssignmentStore>();
        var workspaceFiles = scope.ServiceProvider.GetRequiredService<IWorkspaceFileService>();

        var projectId = await CreateProjectAsync(projects, "Process launch target context");
        const string outputRoot = @"C:\temp\CanDoItAll\TetrisGame";
        var deliveryNode = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Build TetrisGame",
                "Blazor delivery target",
                "Implement the TetrisGame app in the outputRoot path.",
                $"project:{projectId:D}",
                320,
                220,
                null,
                null,
                "delivery",
                MetadataJson: $$"""{ "outputRoot": "{{outputRoot.Replace(@"\", @"\\", StringComparison.Ordinal)}}" }"""));
        var definitionId = ProcessDefinitionCatalogProjectionService
            .CreateDefinitionId(new ProcessDefinitionCatalogItemKey("software-delivery"))
            .Value;

        var link = await agentService.LinkProcessDefinitionAsync(
            projectId,
            deliveryNode.Id,
            new ProjectStructureProcessDefinitionLinkInput(definitionId),
            DefaultAgent);
        Assert.True(link.Changed);

        var result = await agentService.StartProcessNodeAsync(
            projectId,
            ProjectStructureProcessNodeKeys.BuildProcessDefinitionNodeKey(definitionId),
            new ProjectStructureProcessNodeStartInput(
                RunHrMatch: false,
                Execute: false,
                IncludeLaunchPlan: true,
                RequestedBy: "integration-test"),
            DefaultAgent);

        Assert.Equal(projectId, result.ProjectId);
        Assert.Equal(definitionId, result.ProcessDefinitionId);
        Assert.NotEqual(Guid.Empty, result.LaunchPlanId);
        Assert.NotNull(result.RunId);
        Assert.Equal("Running", result.Stage);
        Assert.StartsWith($"/projects/{projectId:D}/processes/live?runId=", result.Route, StringComparison.Ordinal);
        Assert.IsType<ProcessLaunchPlanView>(result.LaunchPlan);

        var surface = await workbench.GetStructureAsync(projectId);
        var definitionNodeId = ProjectStructureProcessNodeKeys.BuildProcessDefinitionNodeKey(definitionId);
        var runNodeId = ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(result.RunId!.Value);
        var definitionNode = Assert.Single(surface.Nodes, item => string.Equals(item.Id, definitionNodeId, StringComparison.Ordinal));
        Assert.Equal(ProjectObjectType.ProcessDefinition, definitionNode.ObjectType);
        Assert.Equal(deliveryNode.Id, definitionNode.ParentId);
        var runNode = Assert.Single(surface.Nodes, item => string.Equals(item.Id, runNodeId, StringComparison.Ordinal));
        Assert.Equal(ProjectObjectType.ProcessRun, runNode.ObjectType);
        Assert.Equal(deliveryNode.Id, runNode.ParentId);
        Assert.Equal(result.RunId.Value, runNode.ArtifactId);
        Assert.Contains(surface.Links, item =>
            item.IsUserAuthored &&
            string.Equals(item.SourceId, deliveryNode.Id, StringComparison.Ordinal) &&
            string.Equals(item.TargetId, runNodeId, StringComparison.Ordinal) &&
            item.Kind == ProjectObjectLinkKind.Uses);
        var commandGroup = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Run commands",
                "Runtime operations",
                "Runtime command group written by the process.",
                runNodeId,
                2040,
                260,
                null,
                null,
                "operations"));
        Assert.Equal(runNodeId, commandGroup.ParentId);

        var assignments = await assignmentStore.LoadByRunAsync(new ProcessRunId(result.RunId.Value));
        var assignment = Assert.Single(assignments, item => item.StepKey == "feature-intake");
        var currentRunNodeId = ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(result.RunId.Value);
        var currentArtifactRoot = ProcessLaunchApplicationService.BuildManagedProcessArtifactRoot(new ProcessRunId(result.RunId.Value));
        var outputRootAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(outputRoot);
        Assert.Equal(deliveryNode.Id, assignment.LaunchVariables["ProjectNodeId"]);
        Assert.Equal(deliveryNode.Title, assignment.LaunchVariables["ProjectNodeTitle"]);
        Assert.Equal(outputRoot, assignment.LaunchVariables["OutputRoot"]);
        Assert.Equal(outputRoot, assignment.LaunchVariables["ProductRoot"]);
        Assert.Equal(outputRootAlias, assignment.LaunchVariables["ExternalTargetRoot"]);
        Assert.Equal(outputRootAlias, assignment.LaunchVariables["OutputRootAlias"]);
        Assert.Equal(outputRootAlias, assignment.LaunchVariables["ProductRootAlias"]);
        Assert.Equal(outputRootAlias, assignment.LaunchVariables["WorkspaceAlias"]);
        Assert.Equal(result.RunId.Value.ToString("D"), assignment.LaunchVariables["CurrentProcessRunId"]);
        Assert.Equal(result.RunId.Value.ToString("D"), assignment.LaunchVariables["ProcessRunId"]);
        Assert.Equal(result.RunId.Value.ToString("D"), assignment.LaunchVariables["processRunId"]);
        Assert.Equal(currentRunNodeId, assignment.LaunchVariables["CurrentProcessRunNodeId"]);
        Assert.Equal(currentRunNodeId, assignment.LaunchVariables["ProcessRunNodeId"]);
        Assert.Equal(currentArtifactRoot, assignment.LaunchVariables["CurrentManagedArtifactRoot"]);
        Assert.Equal(currentArtifactRoot, assignment.LaunchVariables["ManagedArtifactRoot"]);
        Assert.Equal(currentArtifactRoot, assignment.LaunchVariables["managedArtifactRoot"]);
        var peerReviewAssignment = assignments.Single(item => item.StepKey == "peer-review");
        Assert.Equal("ExternalProductTargetReadOnly", peerReviewAssignment.OperationTargetScope);
        Assert.Contains("RunValidation", peerReviewAssignment.AllowedOperations);
        Assert.DoesNotContain("MutateProductTarget", peerReviewAssignment.AllowedOperations);
        Assert.Equal("ExternalActionControlled", assignments.Single(item => item.StepKey == "record-runtime-commands").OperationTargetScope);
        Assert.Contains("ExecuteExternalAction", assignments.Single(item => item.StepKey == "record-runtime-commands").AllowedOperations);
        var screenshotAssignment = assignments.Single(item => item.StepKey == "capture-ui-screenshots");
        Assert.Equal("ExternalActionControlled", screenshotAssignment.OperationTargetScope);
        Assert.Contains("ExecuteExternalAction", screenshotAssignment.AllowedOperations);
        Assert.Contains("LaunchRuntime", screenshotAssignment.AllowedOperations);
        Assert.Contains("CaptureRuntimeProof", screenshotAssignment.AllowedOperations);

        var artifactRoot = workspaceFiles.StatPath($"artifacts/process-runs/{result.RunId.Value:D}");
        Assert.True(artifactRoot.Succeeded, artifactRoot.Message);
        Assert.Equal("directory", artifactRoot.PathKind);
    }

    [Fact]
    public async Task ProcessLaunchApplicationService_LaunchAsync_promotes_typed_project_scope_into_assignment_variables()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var launchService = scope.ServiceProvider.GetRequiredService<ProcessLaunchApplicationService>();
        var assignmentStore = scope.ServiceProvider.GetRequiredService<IProcessRuntimeStepAssignmentStore>();

        var projectId = await CreateProjectAsync(projects, "Direct project scoped process launch");
        const string projectNodeId = "custom:bd8169fc3fa944dbafd13998fb167fe8";

        var result = await launchService.LaunchAsync(
            new ProcessLaunchRequest(
                DefinitionKey: "software-delivery",
                ProcessDefinitionId: null,
                LiveRunProfileKey: null,
                projectId,
                projectNodeId,
                RequestedBy: "integration-test",
                Variables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["OutputRoot"] = @"C:\temp\CanDoItAll\TetrisGame"
                },
                RunReadiness: false,
                Execute: false));

        Assert.True(result.RunId.HasValue);
        var assignments = await assignmentStore.LoadByRunAsync(result.RunId.Value);
        Assert.NotEmpty(assignments);
        Assert.All(assignments, assignment =>
        {
            Assert.Equal(projectId.ToString("D"), assignment.LaunchVariables["ProjectId"]);
            Assert.Equal(projectNodeId, assignment.LaunchVariables["ProjectNodeId"]);
        });
    }

    [Fact]
    public async Task ProcessLaunchApplicationService_LaunchAsync_normalizes_output_folder_to_product_root_variables()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var launchService = scope.ServiceProvider.GetRequiredService<ProcessLaunchApplicationService>();
        var assignmentStore = scope.ServiceProvider.GetRequiredService<IProcessRuntimeStepAssignmentStore>();

        var projectId = await CreateProjectAsync(projects, "Direct output folder launch");
        const string outputFolder = @"C:\temp\CanDoItAll\OutputFolderOnly";

        var result = await launchService.LaunchAsync(
            new ProcessLaunchRequest(
                DefinitionKey: "software-delivery",
                ProcessDefinitionId: null,
                LiveRunProfileKey: null,
                projectId,
                ProjectNodeId: null,
                RequestedBy: "integration-test",
                Variables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["OutputFolder"] = outputFolder
                },
                RunReadiness: false,
                Execute: false));

        Assert.True(result.RunId.HasValue);
        var assignments = await assignmentStore.LoadByRunAsync(result.RunId.Value);
        Assert.NotEmpty(assignments);
        var outputFolderAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(outputFolder);
        Assert.All(assignments, assignment =>
        {
            Assert.Equal(outputFolder, assignment.LaunchVariables["OutputFolder"]);
            Assert.Equal(outputFolder, assignment.LaunchVariables["OutputRoot"]);
            Assert.Equal(outputFolder, assignment.LaunchVariables["ProductRoot"]);
            Assert.Equal(outputFolderAlias, assignment.LaunchVariables["ExternalTargetRoot"]);
            Assert.Equal(outputFolderAlias, assignment.LaunchVariables["OutputRootAlias"]);
            Assert.Equal(outputFolderAlias, assignment.LaunchVariables["ProductRootAlias"]);
            Assert.Equal(outputFolderAlias, assignment.LaunchVariables["WorkspaceAlias"]);
        });
    }

    [Fact]
    public async Task ProcessLaunchApplicationService_LaunchAsync_projects_run_evidence_and_runtime_nodes_without_seed_link()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var launchService = scope.ServiceProvider.GetRequiredService<ProcessLaunchApplicationService>();

        var projectId = await CreateProjectAsync(projects, "Direct project scoped process projection");
        var productRoot = Path.Combine(application.ActiveProfile.WorkspaceRootPath, "external-output", Guid.NewGuid().ToString("N"));
        var appProjectPath = Path.Combine(productRoot, "src", "TetrisGame", "TetrisGame.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(appProjectPath)!);
        await File.WriteAllTextAsync(
            appProjectPath,
            """
            <Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        var testProjectPath = Path.Combine(productRoot, "tests", "TetrisGame.Tests", "TetrisGame.Tests.csproj");
        Directory.CreateDirectory(Path.GetDirectoryName(testProjectPath)!);
        await File.WriteAllTextAsync(
            testProjectPath,
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        var deliveryNode = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Build TetrisGame",
                "Blazor delivery target",
                "Implement the TetrisGame app.",
                $"project:{projectId:D}",
                320,
                220,
                null,
                null,
                "delivery"));

        var result = await launchService.LaunchAsync(
            new ProcessLaunchRequest(
                DefinitionKey: "software-delivery",
                ProcessDefinitionId: null,
                LiveRunProfileKey: null,
                projectId,
                deliveryNode.Id,
                RequestedBy: "integration-test",
                Variables: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["ProductRoot"] = productRoot,
                    ["OutputRoot"] = productRoot
                },
                RunReadiness: false,
                Execute: false));

        Assert.True(result.RunId.HasValue);
        var runId = result.RunId.Value;
        var runNodeId = ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(runId.Value);
        var managedArtifactRoot = ProcessLaunchApplicationService.BuildManagedProcessArtifactRoot(runId);
        var outputNodeId = ProjectStructureProcessNodeKeys.BuildProcessRunOutputNodeKey(runId.Value, managedArtifactRoot);
        var screenshotRelativePath = $"{managedArtifactRoot}/browser/desktop.png";
        var screenshotFullPath = Path.Combine(application.ActiveProfile.WorkspaceRootPath, screenshotRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(screenshotFullPath)!);
        await File.WriteAllBytesAsync(
            screenshotFullPath,
            Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII="));

        var surface = await workbench.GetStructureAsync(projectId);
        var runNode = Assert.Single(surface.Nodes, node => string.Equals(node.Id, runNodeId, StringComparison.Ordinal));
        Assert.Equal(ProjectObjectType.ProcessRun, runNode.ObjectType);
        Assert.Equal(deliveryNode.Id, runNode.ParentId);
        var outputNode = Assert.Single(surface.Nodes, node => string.Equals(node.Id, outputNodeId, StringComparison.Ordinal));
        Assert.Equal(ProjectObjectType.File, outputNode.ObjectType);
        Assert.Equal("folder", outputNode.ObjectSubtype);
        Assert.Equal(runNodeId, outputNode.ParentId);
        Assert.Equal("process-run-output-folder", outputNode.ArtifactKind);
        Assert.Equal(runId.Value, outputNode.ArtifactId);
        Assert.Contains(managedArtifactRoot, outputNode.Notes, StringComparison.Ordinal);
        var summaryNode = Assert.Single(surface.Nodes, node => string.Equals(node.Id, ProjectStructureProcessNodeKeys.BuildProcessRunSummaryNodeKey(runId.Value), StringComparison.Ordinal));
        Assert.Equal(ProjectObjectType.Note, summaryNode.ObjectType);
        Assert.Equal("process-summary", summaryNode.ObjectSubtype);
        Assert.Equal(runNodeId, summaryNode.ParentId);
        Assert.Equal("process-run-summary", summaryNode.ArtifactKind);
        Assert.Contains("Projected process run summary.", summaryNode.Notes, StringComparison.Ordinal);
        var screenshotNodeId = ProjectStructureProcessNodeKeys.BuildProcessRunScreenshotNodeKey(runId.Value, screenshotRelativePath);
        var screenshotNode = Assert.Single(surface.Nodes, node => string.Equals(node.Id, screenshotNodeId, StringComparison.Ordinal));
        Assert.Equal(ProjectObjectType.ImageAsset, screenshotNode.ObjectType);
        Assert.Equal("screenshot", screenshotNode.ObjectSubtype);
        Assert.Equal(runNodeId, screenshotNode.ParentId);
        Assert.Equal("process-run-screenshot", screenshotNode.ArtifactKind);
        Assert.Equal(screenshotRelativePath, screenshotNode.MediaRelativePath);
        Assert.Equal("image/png", screenshotNode.MediaContentType);
        Assert.False(string.IsNullOrWhiteSpace(screenshotNode.StorageObjectReferenceJson));
        var runtimeNodeId = ProjectStructureProcessNodeKeys.BuildProcessRunRuntimeNodeKey(runId.Value, appProjectPath);
        var runtimeNode = Assert.Single(surface.Nodes, node => string.Equals(node.Id, runtimeNodeId, StringComparison.Ordinal));
        Assert.Equal(ProjectObjectType.Environment, runtimeNode.ObjectType);
        Assert.Equal("dotnet-watch", runtimeNode.ObjectSubtype);
        Assert.Equal(runNodeId, runtimeNode.ParentId);
        Assert.Equal("process-run-runtime", runtimeNode.ArtifactKind);
        Assert.Contains(appProjectPath, runtimeNode.Notes, StringComparison.Ordinal);
        Assert.DoesNotContain(testProjectPath, runtimeNode.Notes, StringComparison.Ordinal);
        var runtimeMetadata = ProjectObjectMetadataSerializer.Parse(runtimeNode.MetadataJson);
        Assert.Equal(ProjectEnvironmentKind.DotNetWatch, runtimeMetadata.Environment?.EnvironmentKind);
        Assert.Equal(appProjectPath, runtimeMetadata.Environment?.ProjectPath);

        var refreshedSurface = await workbench.GetStructureAsync(projectId);
        Assert.Single(refreshedSurface.Nodes, node => string.Equals(node.Id, runNodeId, StringComparison.Ordinal));
        Assert.Single(refreshedSurface.Nodes, node => string.Equals(node.Id, outputNodeId, StringComparison.Ordinal));
        Assert.Single(refreshedSurface.Nodes, node => string.Equals(node.Id, summaryNode.Id, StringComparison.Ordinal));
        Assert.Single(refreshedSurface.Nodes, node => string.Equals(node.Id, screenshotNodeId, StringComparison.Ordinal));
        Assert.Single(refreshedSurface.Nodes, node => string.Equals(node.Id, runtimeNodeId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProjectWorkbenchService_DeleteObjectAsync_hides_projected_process_definition_without_seed_link()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var launchService = scope.ServiceProvider.GetRequiredService<ProcessLaunchApplicationService>();

        var projectId = await CreateProjectAsync(projects, "Direct project scoped process definition removal");
        var deliveryNode = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Build removable process definition",
                "Blazor delivery target",
                "Implement the app.",
                $"project:{projectId:D}",
                320,
                220,
                null,
                null,
                "delivery"));

        var launchResult = await launchService.LaunchAsync(
            new ProcessLaunchRequest(
                DefinitionKey: "software-delivery",
                ProcessDefinitionId: null,
                LiveRunProfileKey: null,
                projectId,
                deliveryNode.Id,
                RequestedBy: "integration-test",
                Variables: new Dictionary<string, string>(StringComparer.Ordinal),
                RunReadiness: false,
                Execute: false));

        Assert.True(launchResult.RunId.HasValue);
        var runNodeId = ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(launchResult.RunId.Value.Value);
        var definitionId = ProcessDefinitionCatalogProjectionService
            .CreateDefinitionId(new ProcessDefinitionCatalogItemKey("software-delivery"))
            .Value;
        var definitionNodeId = ProjectStructureProcessNodeKeys.BuildProcessDefinitionNodeKey(definitionId);
        var initialSurface = await workbench.GetStructureAsync(projectId);
        Assert.Contains(initialSurface.Nodes, node => string.Equals(node.Id, definitionNodeId, StringComparison.Ordinal));
        Assert.Contains(initialSurface.Nodes, node => string.Equals(node.Id, runNodeId, StringComparison.Ordinal));

        var deletedCount = await workbench.DeleteObjectAsync(projectId, definitionNodeId);

        Assert.Equal(1, deletedCount);
        var refreshedSurface = await workbench.GetStructureAsync(projectId);
        Assert.DoesNotContain(refreshedSurface.Nodes, node => string.Equals(node.Id, definitionNodeId, StringComparison.Ordinal));
        Assert.Contains(refreshedSurface.Nodes, node => string.Equals(node.Id, runNodeId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task StartProcessNodeAsync_reports_missing_project_as_project_structure_error()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();

        var projectId = Guid.NewGuid();
        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            agentService.StartProcessNodeAsync(
                projectId,
                "custom:missing",
                new ProjectStructureProcessNodeStartInput(
                    ProcessDefinitionCatalogProjectionService.CreateDefinitionId(
                        new ProcessDefinitionCatalogItemKey("software-delivery")).Value,
                    RunHrMatch: true,
                    Execute: false,
                    IncludeLaunchPlan: true,
                    RequestedBy: "integration-test"),
                DefaultAgent));

        Assert.Equal(404, exception.StatusCode);
        Assert.Equal("ProjectNotFound", exception.ErrorCode);
        Assert.Contains(projectId.ToString("D"), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartProcessNodeAsync_accepts_source_node_with_single_process_definition_link()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var assignmentStore = scope.ServiceProvider.GetRequiredService<IProcessRuntimeStepAssignmentStore>();

        var projectId = await CreateProjectAsync(projects, "Process launch source node");
        const string outputRoot = @"C:\temp\CanDoItAll\TetrisGame";
        var deliveryNode = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Build TetrisGame",
                "Blazor delivery target",
                "Implement the TetrisGame app in the configured output root.",
                $"project:{projectId:D}",
                320,
                220,
                null,
                null,
                "delivery",
                MetadataJson: "{}"));
        var architectureNode = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Main Architecture",
                string.Empty,
                string.Empty,
                $"project:{projectId:D}",
                520,
                220,
                null,
                null,
                "architecture"));
        await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Blazor WASM PWA app shape",
                "Frontend-only web app",
                "Blazor WebAssembly PWA with no backend. Single client app, static-host friendly, offline-friendly shell, and local-first scope.",
                architectureNode.Id,
                700,
                180,
                null,
                null,
                "architecture"));
        await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Output folder",
                string.Empty,
                $"Final app must be placed in {outputRoot}",
                architectureNode.Id,
                700,
                260,
                null,
                null,
                "delivery"));
        var visualTargetAsset = await agentService.CreateAssetAsync(
            projectId,
            new ProjectStructureAssetCreateInput(
                ProjectObjectType.ImageAsset,
                "Application layout proposal",
                "Calculator target look",
                "Generated UI proposal image is the source visual target for implementation and QA.",
                CreateMediaPayload("calculator-target.png", "image/png", [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
                architectureNode.Id,
                "generated"),
            DefaultAgent);
        await agentService.CreateAssetAsync(
            projectId,
            new ProjectStructureAssetCreateInput(
                ProjectObjectType.File,
                "browser-proof-previous-run",
                "Generated browser proof",
                "Prior generated process evidence that must not seed a new process run.",
                CreateMediaPayload("browser-proof-old.md", "text/markdown", "old browser proof"),
                architectureNode.Id,
                "markdown"),
            DefaultAgent);
        await agentService.CreateAssetAsync(
            projectId,
            new ProjectStructureAssetCreateInput(
                ProjectObjectType.File,
                "office365-category-email-summary-old",
                "Generated file summary",
                "Prior file summary from another workflow that must not seed a new process run.",
                CreateMediaPayload("office365-category-email-summary-old.md", "text/markdown", "old unrelated summary"),
                architectureNode.Id,
                "markdown"),
            DefaultAgent);
        await agentService.CreateAssetAsync(
            projectId,
            new ProjectStructureAssetCreateInput(
                ProjectObjectType.ImageAsset,
                "Previous run screenshot",
                "Generated screenshot evidence",
                "Prior generated screenshot evidence that must not replace source design input.",
                CreateMediaPayload("previous-run.png", "image/png", [0x89, 0x50, 0x4E, 0x47]),
                architectureNode.Id,
                "screenshot"),
            DefaultAgent);
        var definitionId = ProcessDefinitionCatalogProjectionService
            .CreateDefinitionId(new ProcessDefinitionCatalogItemKey("software-delivery"))
            .Value;

        await agentService.LinkProcessDefinitionAsync(
            projectId,
            deliveryNode.Id,
            new ProjectStructureProcessDefinitionLinkInput(definitionId),
            DefaultAgent);

        var result = await agentService.StartProcessNodeAsync(
            projectId,
            deliveryNode.Id,
            new ProjectStructureProcessNodeStartInput(
                RunHrMatch: false,
                Execute: false,
                IncludeLaunchPlan: true,
                RequestedBy: "integration-test"),
            DefaultAgent);

        Assert.Equal(definitionId, result.ProcessDefinitionId);
        Assert.NotNull(result.RunId);

        var assignments = await assignmentStore.LoadByRunAsync(new ProcessRunId(result.RunId!.Value));
        var assignment = Assert.Single(assignments, item => item.StepKey == "feature-intake");
        Assert.Equal(deliveryNode.Id, assignment.LaunchVariables["ProjectNodeId"]);
        Assert.Equal(ProjectStructureProcessNodeKeys.BuildProcessDefinitionNodeKey(definitionId), assignment.LaunchVariables["ProcessNodeId"]);
        Assert.Equal(outputRoot, assignment.LaunchVariables["OutputRoot"]);
        var contextSummary = assignment.LaunchVariables["ProjectStructureContextSummary"];
        Assert.Contains("Blazor WASM PWA app shape", contextSummary, StringComparison.Ordinal);
        Assert.Contains(outputRoot, contextSummary, StringComparison.Ordinal);
        Assert.Contains("Visual target assets:", contextSummary, StringComparison.Ordinal);
        Assert.Contains(visualTargetAsset.Id, contextSummary, StringComparison.Ordinal);
        Assert.Contains("calculator-target.png", contextSummary, StringComparison.Ordinal);
        Assert.Contains("Visual target rule: implementation and QA must fetch or analyze the relevant asset content", contextSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("browser-proof-previous-run", contextSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("browser-proof-old.md", contextSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("office365-category-email-summary-old", contextSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("previous-run.png", contextSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ignore generated process evidence from prior runs", assignment.Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartProcessSubprocessAsync_inherits_parent_context_and_links_child_run()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var assignmentStore = scope.ServiceProvider.GetRequiredService<IProcessRuntimeStepAssignmentStore>();
        var stateStore = scope.ServiceProvider.GetRequiredService<IProcessRuntimeStateStore>();

        var projectId = await CreateProjectAsync(projects, "Process subprocess launch");
        var deliveryNode = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Build TetrisGame",
                "Blazor delivery target",
                "Implement the TetrisGame app in the configured output root.",
                $"project:{projectId:D}",
                320,
                220,
                null,
                null,
                "delivery",
                MetadataJson: """{ "outputRoot": "C:\\temp\\CanDoItAll\\TetrisGame" }"""));
        var architectureNode = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Main Architecture",
                string.Empty,
                string.Empty,
                $"project:{projectId:D}",
                520,
                220,
                null,
                null,
                "architecture"));
        await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Blazor WASM PWA app shape",
                "Frontend-only web app",
                "Blazor WebAssembly PWA with no backend. Single client app, static-host friendly, offline-friendly shell, and local-first scope.",
                architectureNode.Id,
                700,
                180,
                null,
                null,
                "architecture"));
        var parentDefinitionId = ProcessDefinitionCatalogProjectionService
            .CreateDefinitionId(new ProcessDefinitionCatalogItemKey("software-delivery"))
            .Value;

        await agentService.LinkProcessDefinitionAsync(
            projectId,
            deliveryNode.Id,
            new ProjectStructureProcessDefinitionLinkInput(parentDefinitionId),
            DefaultAgent);

        var parent = await agentService.StartProcessNodeAsync(
            projectId,
            deliveryNode.Id,
            new ProjectStructureProcessNodeStartInput(
                RunHrMatch: false,
                Execute: false,
                IncludeLaunchPlan: true,
                RequestedBy: "integration-test"),
            DefaultAgent);
        var parentAssignments = await assignmentStore.LoadByRunAsync(new ProcessRunId(parent.RunId!.Value));
        var parentAssignment = Assert.Single(parentAssignments, item => item.StepKey == "architecture-review");

        var subprocess = await agentService.StartProcessSubprocessAsync(
            projectId,
            parent.RunId.Value.ToString("D"),
            parentAssignment.StepInstanceId.ToString(),
            new ProjectStructureProcessSubprocessLaunchInput(
                DefinitionKey: "dotnet-architecture-design-review",
                Variables: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["ProjectNodeId"] = "attempted-scope-escape",
                    ["ChildLaunchReason"] = "integration-test",
                    ["includeLaunchPlan"] = JsonDocument.Parse("true").RootElement.Clone(),
                    ["subprocessPriority"] = JsonDocument.Parse("3").RootElement.Clone()
                },
                RunHrMatch: false,
                Execute: false,
                IncludeLaunchPlan: true,
                RequestedBy: "integration-test"),
            DefaultAgent);

        var childDefinitionId = ProcessDefinitionCatalogProjectionService
            .CreateDefinitionId(new ProcessDefinitionCatalogItemKey("dotnet-architecture-design-review"))
            .Value;
        Assert.Equal(projectId, subprocess.ProjectId);
        Assert.Equal(deliveryNode.Id, subprocess.ProjectNodeId);
        Assert.Equal(parent.RunId.Value.ToString("D"), subprocess.ParentProcessRunId);
        Assert.Equal(parentAssignment.StepInstanceId.ToString(), subprocess.ParentProcessStepId);
        Assert.Equal("architecture-review", subprocess.ParentProcessStepKey);
        Assert.Equal("dotnet-architecture-design-review", subprocess.DefinitionKey);
        Assert.Equal(childDefinitionId, subprocess.ProcessDefinitionId);
        Assert.NotNull(subprocess.RunId);
        Assert.Equal("Running", subprocess.Stage);
        Assert.StartsWith($"/projects/{projectId:D}/processes/live?runId=", subprocess.Route, StringComparison.Ordinal);
        Assert.IsType<ProcessLaunchPlanView>(subprocess.LaunchPlan);
        Assert.Equal($"artifacts/process-runs/{subprocess.RunId.Value:D}", subprocess.ChildManagedArtifactRoot);
        Assert.Equal($"artifacts/process-runs/{subprocess.RunId.Value:D}/steps", subprocess.ChildStepsArtifactRoot);
        Assert.Equal($"/projects/{projectId:D}/processes/live?runId={subprocess.RunId.Value:D}", subprocess.ChildLiveProcessesRoute);
        Assert.Contains($"artifacts/process-runs/{subprocess.RunId.Value:D}/steps/classify-dotnet-application.md", subprocess.ExpectedChildEvidenceRefs);
        Assert.Contains($"artifacts/process-runs/{subprocess.RunId.Value:D}/steps/architecture-handoff.md", subprocess.ExpectedChildEvidenceRefs);

        var parentState = await stateStore.LoadAsync(new ProcessRunId(parent.RunId.Value));
        var childState = await stateStore.LoadAsync(new ProcessRunId(subprocess.RunId.Value));
        Assert.NotNull(parentState);
        Assert.NotNull(childState);
            Assert.Equal(parentState!.RootRunId, childState!.RootRunId);

        var childAssignments = await assignmentStore.LoadByRunAsync(new ProcessRunId(subprocess.RunId.Value));
        Assert.NotEmpty(childAssignments);
        Assert.All(childAssignments, assignment =>
        {
            Assert.Equal("True", assignment.LaunchVariables["includeLaunchPlan"]);
            Assert.Equal("3", assignment.LaunchVariables["subprocessPriority"]);
        });

        var reviewAssignment = Assert.Single(childAssignments, assignment => assignment.StepKey == "review-architecture-design");
        Assert.Contains("Producer step: draft-architecture-design - Draft .NET architecture design", reviewAssignment.Prompt, StringComparison.Ordinal);
        Assert.Contains($"artifacts/process-runs/{subprocess.RunId.Value:D}/steps/draft-architecture-design.md", reviewAssignment.Prompt, StringComparison.Ordinal);
        var classifyAssignment = Assert.Single(childAssignments, assignment => assignment.StepKey == "classify-dotnet-application");
        Assert.Equal("Blazor WebAssembly PWA", classifyAssignment.LaunchVariables["DotNetAppArchetype"]);
        Assert.Contains("AppTemplate: blazorwasm", classifyAssignment.LaunchVariables["DotNetScaffoldContract"], StringComparison.Ordinal);
        Assert.Contains("AllowedTemplateSwitches: --pwa", classifyAssignment.LaunchVariables["DotNetScaffoldContract"], StringComparison.Ordinal);
        Assert.Contains("ScaffoldToolContract: use workspace_dotnet_new with template 'blazorwasm --pwa'", classifyAssignment.LaunchVariables["DotNetScaffoldContract"], StringComparison.Ordinal);
        Assert.Contains("WorkspaceAlias: external-target/C/temp/CanDoItAll/TetrisGame", classifyAssignment.LaunchVariables["DotNetScaffoldContract"], StringComparison.Ordinal);
        Assert.Contains("StructuredWorkspacePathRule: use WorkspaceAlias or external-target/... aliases in workspace_* tool path arguments", classifyAssignment.LaunchVariables["DotNetScaffoldContract"], StringComparison.Ordinal);
        Assert.Contains("ExistingScaffoldRule: existing files are not enough", classifyAssignment.LaunchVariables["DotNetScaffoldContract"], StringComparison.Ordinal);
        Assert.Contains("PackageRule: do not add PackageReference Include=\"Microsoft.AspNetCore.Components.WebAssembly.PWA\"", classifyAssignment.LaunchVariables["DotNetScaffoldContract"], StringComparison.Ordinal);
        Assert.Contains("BlazorWasmTemplateIntegrityRule: Program.cs, App.razor, and _Imports.razor", classifyAssignment.LaunchVariables["DotNetScaffoldContract"], StringComparison.Ordinal);
        Assert.Contains("project-structure-grounded greenfield target", classifyAssignment.Prompt, StringComparison.Ordinal);
        Assert.Contains("absence of .sln, .slnx, or .csproj files", classifyAssignment.Prompt, StringComparison.Ordinal);
        var parentProcessRunNodeId = ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(parent.RunId.Value);
        Assert.All(childAssignments, assignment =>
        {
            Assert.Equal(deliveryNode.Id, assignment.LaunchVariables["ProjectNodeId"]);
            Assert.Equal(parent.RunId.Value.ToString("D"), assignment.LaunchVariables["ParentProcessRunId"]);
            Assert.Equal(parentProcessRunNodeId, assignment.LaunchVariables["ProcessRunNodeId"]);
            Assert.Equal(parentProcessRunNodeId, assignment.LaunchVariables["ParentProcessRunNodeId"]);
            Assert.Equal(parentProcessRunNodeId, assignment.LaunchVariables["TargetProcessRunNodeId"]);
            Assert.Equal(subprocess.RunId!.Value.ToString("D"), assignment.LaunchVariables["CurrentProcessRunId"]);
            Assert.Equal(subprocess.RunId.Value.ToString("D"), assignment.LaunchVariables["ProcessRunId"]);
            Assert.Equal(subprocess.RunId.Value.ToString("D"), assignment.LaunchVariables["processRunId"]);
            Assert.Equal(ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(subprocess.RunId.Value), assignment.LaunchVariables["CurrentProcessRunNodeId"]);
            Assert.Equal(ProcessLaunchApplicationService.BuildManagedProcessArtifactRoot(new ProcessRunId(subprocess.RunId.Value)), assignment.LaunchVariables["CurrentManagedArtifactRoot"]);
            Assert.Equal(ProcessLaunchApplicationService.BuildManagedProcessArtifactRoot(new ProcessRunId(subprocess.RunId.Value)), assignment.LaunchVariables["ManagedArtifactRoot"]);
            Assert.Equal(ProcessLaunchApplicationService.BuildManagedProcessArtifactRoot(new ProcessRunId(subprocess.RunId.Value)), assignment.LaunchVariables["managedArtifactRoot"]);
            Assert.Equal(ProcessLaunchApplicationService.BuildManagedProcessArtifactRoot(new ProcessRunId(parent.RunId.Value)), assignment.LaunchVariables["ParentManagedArtifactRoot"]);
            Assert.Equal(parentAssignment.StepInstanceId.ToString(), assignment.LaunchVariables["ParentProcessStepId"]);
            Assert.Equal("architecture-review", assignment.LaunchVariables["ParentProcessStepKey"]);
            Assert.Equal("dotnet-architecture-design-review", assignment.LaunchVariables["SubprocessDefinitionKey"]);
            Assert.Equal(@"C:\temp\CanDoItAll\TetrisGame", assignment.LaunchVariables["OutputRoot"]);
            Assert.Equal("integration-test", assignment.LaunchVariables["ChildLaunchReason"]);
        });

        var retry = await agentService.StartProcessSubprocessAsync(
            projectId,
            parent.RunId.Value.ToString("D"),
            parentAssignment.StepInstanceId.ToString(),
            new ProjectStructureProcessSubprocessLaunchInput(
                DefinitionKey: "dotnet-architecture-design-review",
                Variables: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["ChildLaunchReason"] = "retry-should-reuse-existing-run"
                },
                RunHrMatch: false,
                Execute: false,
                IncludeLaunchPlan: true,
                RequestedBy: "integration-test"),
            DefaultAgent);
        Assert.Equal(subprocess.RunId, retry.RunId);
        Assert.Contains(retry.Warnings, warning => warning.Contains("Reused existing process run", StringComparison.Ordinal));
        var retryLaunchPlan = Assert.IsType<ProcessLaunchPlanView>(retry.LaunchPlan);
        Assert.Equal("dotnet-architecture-design-review", retryLaunchPlan.DefinitionKey);

        var runNodeScopedRetry = await agentService.StartProcessSubprocessAsync(
            projectId,
            parent.RunId.Value.ToString("D"),
            parentAssignment.StepInstanceId.ToString(),
            new ProjectStructureProcessSubprocessLaunchInput(
                DefinitionKey: "dotnet-architecture-design-review",
                ParentProjectNodeId: ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(parent.RunId.Value),
                RunHrMatch: false,
                Execute: false,
                IncludeLaunchPlan: true,
                RequestedBy: "integration-test"),
            DefaultAgent);
        Assert.Equal(subprocess.RunId, runNodeScopedRetry.RunId);
        Assert.Equal(deliveryNode.Id, runNodeScopedRetry.ProjectNodeId);
        Assert.Contains(runNodeScopedRetry.Warnings, warning => warning.Contains("Ignored subprocess parent project node", StringComparison.Ordinal));
        Assert.Contains(runNodeScopedRetry.Warnings, warning => warning.Contains("Reused existing process run", StringComparison.Ordinal));

        var matchingChildAssignments = await assignmentStore.FindByLaunchVariablesAsync(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ProjectId"] = projectId.ToString("D"),
                ["ProjectNodeId"] = deliveryNode.Id,
                ["ParentProcessRunId"] = parent.RunId.Value.ToString("D"),
                ["ParentProcessStepId"] = parentAssignment.StepInstanceId.ToString(),
                ["SubprocessDefinitionKey"] = "dotnet-architecture-design-review"
            });
        var matchingChildRunId = Assert.Single(matchingChildAssignments
            .Select(assignment => assignment.RunId)
            .Distinct());
        Assert.Equal(new ProcessRunId(subprocess.RunId.Value), matchingChildRunId);

        var surface = await workbench.GetStructureAsync(projectId);
        var parentRunNode = Assert.Single(surface.Nodes, item => string.Equals(item.Id, parentProcessRunNodeId, StringComparison.Ordinal));
        Assert.Equal(ProjectObjectType.ProcessRun, parentRunNode.ObjectType);
        Assert.Equal(deliveryNode.Id, parentRunNode.ParentId);
        Assert.Contains(surface.Links, item =>
            item.IsUserAuthored &&
            string.Equals(item.SourceId, deliveryNode.Id, StringComparison.Ordinal) &&
            string.Equals(item.TargetId, parentProcessRunNodeId, StringComparison.Ordinal) &&
            item.Kind == ProjectObjectLinkKind.Uses);
        var childProcessRunNodeId = ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(subprocess.RunId.Value);
        Assert.DoesNotContain(surface.Nodes, item => string.Equals(item.Id, childProcessRunNodeId, StringComparison.Ordinal));
        Assert.DoesNotContain(surface.Links, item => string.Equals(item.TargetId, childProcessRunNodeId, StringComparison.Ordinal));
    }

    [Fact]
    public async Task StartProcessSubprocessAsync_starts_new_child_after_previous_child_was_cancelled()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var assignmentStore = scope.ServiceProvider.GetRequiredService<IProcessRuntimeStepAssignmentStore>();
        var operatorService = scope.ServiceProvider.GetRequiredService<ProcessRuntimeOperatorApplicationService>();

        var projectId = await CreateProjectAsync(projects, "Process subprocess retry after terminal child");
        var deliveryNode = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Deliver Tetris",
                "Blazor delivery target",
                "Create a Tetris app.",
                $"project:{projectId:D}",
                320,
                220,
                null,
                null,
                "delivery",
                MetadataJson: """{ "outputRoot": "C:\\temp\\CanDoItAll\\TetrisGame" }"""));
        var parentDefinitionId = ProcessDefinitionCatalogProjectionService
            .CreateDefinitionId(new ProcessDefinitionCatalogItemKey("software-delivery"))
            .Value;

        await agentService.LinkProcessDefinitionAsync(
            projectId,
            deliveryNode.Id,
            new ProjectStructureProcessDefinitionLinkInput(parentDefinitionId),
            DefaultAgent);

        var parent = await agentService.StartProcessNodeAsync(
            projectId,
            deliveryNode.Id,
            new ProjectStructureProcessNodeStartInput(
                RunHrMatch: false,
                Execute: false,
                IncludeLaunchPlan: true,
                RequestedBy: "integration-test"),
            DefaultAgent);
        var parentAssignments = await assignmentStore.LoadByRunAsync(new ProcessRunId(parent.RunId!.Value));
        var parentAssignment = Assert.Single(parentAssignments, item => item.StepKey == "architecture-review");

        var firstChild = await agentService.StartProcessSubprocessAsync(
            projectId,
            parent.RunId.Value.ToString("D"),
            parentAssignment.StepInstanceId.ToString(),
            new ProjectStructureProcessSubprocessLaunchInput(
                DefinitionKey: "dotnet-architecture-design-review",
                RunHrMatch: false,
                Execute: false,
                IncludeLaunchPlan: true,
                RequestedBy: "integration-test"),
            DefaultAgent);
        Assert.NotNull(firstChild.RunId);

        var cancellation = await operatorService.RequestCancellationAsync(
            new ProcessRuntimeRunCancellationCommand(
                new ProcessRunId(firstChild.RunId.Value),
                "integration-test",
                "Force a terminal child state so subprocess launch retry behavior can be verified."));
        Assert.True(cancellation.Succeeded);
        Assert.Equal(ProcessRuntimeStatus.Cancelled, cancellation.Status);

        var retryChild = await agentService.StartProcessSubprocessAsync(
            projectId,
            parent.RunId.Value.ToString("D"),
            parentAssignment.StepInstanceId.ToString(),
            new ProjectStructureProcessSubprocessLaunchInput(
                DefinitionKey: "dotnet-architecture-design-review",
                RunHrMatch: false,
                Execute: false,
                IncludeLaunchPlan: true,
                RequestedBy: "integration-test"),
            DefaultAgent);

        Assert.NotNull(retryChild.RunId);
        Assert.NotEqual(firstChild.RunId, retryChild.RunId);
        Assert.DoesNotContain(retryChild.Warnings, warning => warning.Contains("Reused existing process run", StringComparison.Ordinal));

        var matchingChildAssignments = await assignmentStore.FindByLaunchVariablesAsync(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ProjectId"] = projectId.ToString("D"),
                ["ProjectNodeId"] = deliveryNode.Id,
                ["ParentProcessRunId"] = parent.RunId.Value.ToString("D"),
                ["ParentProcessStepId"] = parentAssignment.StepInstanceId.ToString(),
                ["SubprocessDefinitionKey"] = "dotnet-architecture-design-review"
            });
        var matchingChildRunIds = matchingChildAssignments
            .Select(assignment => assignment.RunId)
            .Distinct()
            .ToArray();
        Assert.Equal(2, matchingChildRunIds.Length);
    }

    [Fact]
    public async Task StartProcessSubprocessAsync_starts_new_child_after_previous_child_was_blocked()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var assignmentStore = scope.ServiceProvider.GetRequiredService<IProcessRuntimeStepAssignmentStore>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ProcessPersistenceDbContext>();

        var projectId = await CreateProjectAsync(projects, "Process subprocess retry after blocked child");
        var deliveryNode = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Deliver Tetris",
                "Blazor delivery target",
                "Create a Tetris app.",
                $"project:{projectId:D}",
                320,
                220,
                null,
                null,
                "delivery",
                MetadataJson: """{ "outputRoot": "C:\\temp\\CanDoItAll\\TetrisGame" }"""));
        var parentDefinitionId = ProcessDefinitionCatalogProjectionService
            .CreateDefinitionId(new ProcessDefinitionCatalogItemKey("software-delivery"))
            .Value;

        await agentService.LinkProcessDefinitionAsync(
            projectId,
            deliveryNode.Id,
            new ProjectStructureProcessDefinitionLinkInput(parentDefinitionId),
            DefaultAgent);

        var parent = await agentService.StartProcessNodeAsync(
            projectId,
            deliveryNode.Id,
            new ProjectStructureProcessNodeStartInput(
                RunHrMatch: false,
                Execute: false,
                IncludeLaunchPlan: true,
                RequestedBy: "integration-test"),
            DefaultAgent);
        var parentAssignments = await assignmentStore.LoadByRunAsync(new ProcessRunId(parent.RunId!.Value));
        var parentAssignment = Assert.Single(parentAssignments, item => item.StepKey == "architecture-review");

        var firstChild = await agentService.StartProcessSubprocessAsync(
            projectId,
            parent.RunId.Value.ToString("D"),
            parentAssignment.StepInstanceId.ToString(),
            new ProjectStructureProcessSubprocessLaunchInput(
                DefinitionKey: "dotnet-architecture-design-review",
                RunHrMatch: false,
                Execute: false,
                IncludeLaunchPlan: true,
                RequestedBy: "integration-test"),
            DefaultAgent);
        Assert.NotNull(firstChild.RunId);

        var childState = await dbContext.RuntimeStates
            .SingleAsync(state => state.RunId == firstChild.RunId.Value);
        childState.Status = ProcessRuntimeStatus.Blocked;
        childState.UpdatedAtUtc = DateTimeOffset.UtcNow;
        childState.ConcurrencyToken = Guid.NewGuid();
        var childSteps = await dbContext.RuntimeSteps
            .Where(step => step.RunId == firstChild.RunId.Value)
            .ToArrayAsync();
        foreach (var childStep in childSteps)
        {
            if (ProcessRuntimeTerminalStates.IsStepTerminal(childStep.Status))
            {
                continue;
            }

            childStep.Status = ProcessRuntimeStepStatus.Blocked;
            childStep.ActiveClaimToken = null;
        }

        await dbContext.SaveChangesAsync();

        var retryChild = await agentService.StartProcessSubprocessAsync(
            projectId,
            parent.RunId.Value.ToString("D"),
            parentAssignment.StepInstanceId.ToString(),
            new ProjectStructureProcessSubprocessLaunchInput(
                DefinitionKey: "dotnet-architecture-design-review",
                RunHrMatch: false,
                Execute: false,
                IncludeLaunchPlan: true,
                RequestedBy: "integration-test"),
            DefaultAgent);

        Assert.NotNull(retryChild.RunId);
        Assert.NotEqual(firstChild.RunId, retryChild.RunId);
        Assert.DoesNotContain(retryChild.Warnings, warning => warning.Contains("Reused existing process run", StringComparison.Ordinal));

        var matchingChildAssignments = await assignmentStore.FindByLaunchVariablesAsync(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ProjectId"] = projectId.ToString("D"),
                ["ProjectNodeId"] = deliveryNode.Id,
                ["ParentProcessRunId"] = parent.RunId.Value.ToString("D"),
                ["ParentProcessStepId"] = parentAssignment.StepInstanceId.ToString(),
                ["SubprocessDefinitionKey"] = "dotnet-architecture-design-review"
            });
        var matchingChildRunIds = matchingChildAssignments
            .Select(assignment => assignment.RunId)
            .Distinct()
            .ToArray();
        Assert.Equal(2, matchingChildRunIds.Length);
    }

    [Fact]
    public async Task StartProcessSubprocessAsync_supplies_dotnet_solution_setup_scaffold_contract()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var assignmentStore = scope.ServiceProvider.GetRequiredService<IProcessRuntimeStepAssignmentStore>();

        var projectId = await CreateProjectAsync(projects, "TetrisGame");
        const string outputRoot = @"C:\temp\CanDoItAll\TetrisGame";
        var deliveryNode = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Main App",
                "Delivery target",
                "Implement the TetrisGame app in the configured output root.",
                $"project:{projectId:D}",
                320,
                220,
                null,
                null,
                "delivery",
                MetadataJson: $$"""{ "outputRoot": "{{outputRoot.Replace(@"\", @"\\", StringComparison.Ordinal)}}" }"""));
        var architectureNode = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Main Architecture",
                string.Empty,
                string.Empty,
                $"project:{projectId:D}",
                520,
                220,
                null,
                null,
                "architecture"));
        await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Blazor WASM PWA app shape",
                "Frontend-only web app",
                "Blazor WebAssembly PWA with no backend. Single client app, static-host friendly, offline-friendly shell, and local-first scope.",
                architectureNode.Id,
                700,
                180,
                null,
                null,
                "architecture"));

        var parentDefinitionId = ProcessDefinitionCatalogProjectionService
            .CreateDefinitionId(new ProcessDefinitionCatalogItemKey("software-delivery"))
            .Value;
        await agentService.LinkProcessDefinitionAsync(
            projectId,
            deliveryNode.Id,
            new ProjectStructureProcessDefinitionLinkInput(parentDefinitionId),
            DefaultAgent);

        var parent = await agentService.StartProcessNodeAsync(
            projectId,
            deliveryNode.Id,
            new ProjectStructureProcessNodeStartInput(
                RunHrMatch: false,
                Execute: false,
                IncludeLaunchPlan: true,
                RequestedBy: "integration-test"),
            DefaultAgent);
        var parentAssignments = await assignmentStore.LoadByRunAsync(new ProcessRunId(parent.RunId!.Value));
        var parentAssignment = Assert.Single(parentAssignments, item => item.StepKey == "implementation");

        var subprocess = await agentService.StartProcessSubprocessAsync(
            projectId,
            parent.RunId.Value.ToString("D"),
            parentAssignment.StepInstanceId.ToString(),
            new ProjectStructureProcessSubprocessLaunchInput(
                DefinitionKey: "dotnet-solution-setup",
                RunHrMatch: false,
                Execute: false,
                IncludeLaunchPlan: true,
                RequestedBy: "integration-test"),
            DefaultAgent);

        var childAssignments = await assignmentStore.LoadByRunAsync(new ProcessRunId(subprocess.RunId!.Value));
        var scaffoldAssignment = Assert.Single(childAssignments, assignment => assignment.StepKey == "scaffold-contract");
        Assert.Equal("TetrisGame", scaffoldAssignment.LaunchVariables["DotNetSolutionName"]);
        Assert.Equal("TetrisGame", scaffoldAssignment.LaunchVariables["DotNetAppProjectName"]);
        Assert.Equal(@"C:\temp\CanDoItAll\TetrisGame\src\TetrisGame", scaffoldAssignment.LaunchVariables["DotNetAppProjectDirectory"]);
        Assert.Equal("Blazor WebAssembly PWA", scaffoldAssignment.LaunchVariables["DotNetAppArchetype"]);
        Assert.Equal("blazorwasm", scaffoldAssignment.LaunchVariables["DotNetAppTemplate"]);
        Assert.Equal("--pwa", scaffoldAssignment.LaunchVariables["DotNetAllowedTemplateSwitches"]);
        Assert.Equal("TetrisGame.Tests", scaffoldAssignment.LaunchVariables["DotNetTestProjectName"]);
        Assert.Equal(@"C:\temp\CanDoItAll\TetrisGame\tests\TetrisGame.Tests", scaffoldAssignment.LaunchVariables["DotNetTestProjectDirectory"]);
        Assert.Equal("xunit", scaffoldAssignment.LaunchVariables["DotNetTestTemplate"]);
        Assert.Equal("xUnit", scaffoldAssignment.LaunchVariables["DotNetTestFrameworkPreference"]);
        Assert.Equal("net10.0", scaffoldAssignment.LaunchVariables["DotNetTargetFramework"]);
        Assert.Equal("external-target/C/temp/CanDoItAll/TetrisGame", scaffoldAssignment.LaunchVariables["DotNetWorkspaceAlias"]);
        Assert.Contains("SolutionName: TetrisGame", scaffoldAssignment.LaunchVariables["DotNetScaffoldContract"], StringComparison.Ordinal);
        Assert.Contains("WorkspaceAlias: external-target/C/temp/CanDoItAll/TetrisGame", scaffoldAssignment.LaunchVariables["DotNetScaffoldContract"], StringComparison.Ordinal);
        Assert.Contains("StructuredWorkspacePathRule: use WorkspaceAlias or external-target/... aliases in workspace_* tool path arguments", scaffoldAssignment.LaunchVariables["DotNetScaffoldContract"], StringComparison.Ordinal);
        Assert.Contains("AppTemplate: blazorwasm", scaffoldAssignment.Prompt, StringComparison.Ordinal);
        Assert.Contains("AllowedTemplateSwitches: --pwa", scaffoldAssignment.Prompt, StringComparison.Ordinal);
        Assert.Contains("ScaffoldToolContract: use workspace_dotnet_new with template 'blazorwasm --pwa'", scaffoldAssignment.Prompt, StringComparison.Ordinal);
        Assert.Contains("ExistingScaffoldRule: existing files are not enough", scaffoldAssignment.Prompt, StringComparison.Ordinal);
        Assert.Contains("PackageRule: do not add PackageReference Include=\"Microsoft.AspNetCore.Components.WebAssembly.PWA\"", scaffoldAssignment.Prompt, StringComparison.Ordinal);
        Assert.Contains("BlazorWasmTemplateIntegrityRule: Program.cs, App.razor, and _Imports.razor", scaffoldAssignment.Prompt, StringComparison.Ordinal);
        Assert.Contains("TestTemplate: xunit", scaffoldAssignment.Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartProcessNodeAsync_hr_match_resolves_software_delivery_person_or_agent_roles()
    {
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            ConfigurationOverrides = new Dictionary<string, string?>
            {
                ["AgentFramework:ProcessMockAgents:Enabled"] = "true"
            }
        });
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var assignmentStore = scope.ServiceProvider.GetRequiredService<IProcessRuntimeStepAssignmentStore>();

        var projectId = await CreateProjectAsync(projects, "Process launch HR match");
        var deliveryNode = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Build TetrisGame",
                "Blazor delivery target",
                "Implement the TetrisGame app in the configured output root.",
                $"project:{projectId:D}",
                320,
                220,
                null,
                null,
                "delivery",
                MetadataJson: """{ "outputRoot": "C:\\temp\\CanDoItAll\\TetrisGame" }"""));
        var definitionId = ProcessDefinitionCatalogProjectionService
            .CreateDefinitionId(new ProcessDefinitionCatalogItemKey("software-delivery"))
            .Value;

        await agentService.LinkProcessDefinitionAsync(
            projectId,
            deliveryNode.Id,
            new ProjectStructureProcessDefinitionLinkInput(definitionId),
            DefaultAgent);

        var result = await agentService.StartProcessNodeAsync(
            projectId,
            ProjectStructureProcessNodeKeys.BuildProcessDefinitionNodeKey(definitionId),
            new ProjectStructureProcessNodeStartInput(
                RunHrMatch: true,
                Execute: false,
                IncludeLaunchPlan: true,
                RequestedBy: "integration-test"),
            DefaultAgent);

        var launchPlan = Assert.IsType<ProcessLaunchPlanView>(result.LaunchPlan);
        Assert.Equal("software-delivery", launchPlan.DefinitionKey);
        Assert.Null(launchPlan.LiveRunProfileKey);
        var architectureReview = Assert.Single(launchPlan.Steps, step => step.StepKey == "architecture-review");
        var implementation = Assert.Single(launchPlan.Steps, step => step.StepKey == "implementation");
        Assert.Equal("solution-architect", architectureReview.RoleKey);
        Assert.Equal("solution-architect", architectureReview.RoleResourceKey);
        Assert.Equal("Solution architect", architectureReview.RoleDisplayName);
        Assert.DoesNotContain("Delivery Manager", architectureReview.ExecutorDisplayName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("lead-engineer", implementation.RoleKey);
        Assert.Equal("lead-engineer", implementation.RoleResourceKey);
        Assert.Equal("Lead engineer", implementation.RoleDisplayName);
        Assert.DoesNotContain("Delivery Manager", implementation.ExecutorDisplayName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            launchPlan.ReadinessFindings,
            finding => string.Equals(finding.Code, "process.launch.executor_kind_unsupported", StringComparison.Ordinal));
        Assert.Contains(
            launchPlan.ReadinessFindings,
            finding => finding.Severity == ProcessLaunchReadinessSeverity.Info &&
                       string.Equals(finding.Code, "process.launch.readiness_ok", StringComparison.Ordinal));

        var assignments = await assignmentStore.LoadByRunAsync(new ProcessRunId(result.RunId!.Value));
        Assert.NotEmpty(assignments);
        Assert.All(assignments, assignment =>
        {
            Assert.Equal(ProcessLaunchExecutorKinds.Agent, assignment.ExecutorKind);
            Assert.False(string.IsNullOrWhiteSpace(assignment.ExecutorId));
        });
        var runtimeCommandParentAssignment = Assert.Single(assignments, assignment => assignment.StepKey == "record-runtime-commands");
        Assert.Contains(assignments, assignment => assignment.StepKey == "capture-ui-screenshots");

        var runtimeCommandSubprocess = await agentService.StartProcessSubprocessAsync(
            projectId,
            result.RunId.Value.ToString("D"),
            runtimeCommandParentAssignment.StepInstanceId.ToString(),
            new ProjectStructureProcessSubprocessLaunchInput(
                DefinitionKey: "dotnet-runtime-command-writeback",
                RunHrMatch: true,
                Execute: false,
                IncludeLaunchPlan: true,
                RequestedBy: "integration-test"),
            DefaultAgent);
        var runtimeCommandAssignments = await assignmentStore.LoadByRunAsync(new ProcessRunId(runtimeCommandSubprocess.RunId!.Value));
        var resolveRunCommands = Assert.Single(runtimeCommandAssignments, assignment => assignment.StepKey == "resolve-dotnet-run-commands");
        Assert.Equal("runtime-command-recorder", resolveRunCommands.RoleKey);
        Assert.Equal("delivery-manager", resolveRunCommands.RoleResourceKey);
        Assert.Equal("Runtime command recorder", resolveRunCommands.RoleDisplayName);
        Assert.Contains("Delivery Manager", resolveRunCommands.ExecutorDisplayName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartProcessNodeAsync_hr_match_skips_exact_agent_when_provider_cannot_satisfy_process_output_contract()
    {
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            ConfigurationOverrides = new Dictionary<string, string?>
            {
                ["AgentFramework:ProcessMockAgents:Enabled"] = "false"
            }
        });
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var workspaceService = scope.ServiceProvider
            .GetRequiredService<ICanDoItAllAgentWorkspaceFactory>()
            .GetOrganizationWorkspaceService();

        var existingAgents = await workspaceService.ListAgentsAsync(includeTemplates: false);
        foreach (var existingAgent in existingAgents.Where(agent => agent.Status == AgentLifecycleStatus.Active))
        {
            var editor = await workspaceService.GetAgentEditorAsync(existingAgent.Id);
            editor.Status = AgentLifecycleStatus.Suspended;
            await workspaceService.SaveAgentAsync(editor);
        }

        var unsupportedProviderId = await workspaceService.SaveProviderAsync(new ProviderProfileEditorModel
        {
            Name = "Process regression Ollama",
            Kind = ProviderKind.Ollama,
            BaseUrl = "http://ollama.internal:11434",
            ApiKeyEnvironmentVariable = string.Empty,
            DefaultModel = "gptoss32k:latest",
            Transport = ProviderTransportKind.ChatCompletions,
            Purpose = ProviderProfilePurpose.Chat,
            IsEnabled = true,
            SupportsStreaming = false,
            SupportsTools = true,
            PreferFrameworkManagedChatHistory = true,
            SupportsBackgroundResponses = false,
            ConfigurationJson = "{}",
            Notes = "Regression fixture for process HR matching."
        });
        var structuredProviderId = await workspaceService.SaveProviderAsync(new ProviderProfileEditorModel
        {
            Name = "Process regression OpenAI",
            Kind = ProviderKind.OpenAi,
            BaseUrl = "https://api.openai.com/v1",
            ApiKeyEnvironmentVariable = "OPENAI_API_KEY",
            DefaultModel = "gpt-5.4-mini",
            Transport = ProviderTransportKind.Responses,
            Purpose = ProviderProfilePurpose.Chat,
            IsEnabled = true,
            SupportsStreaming = false,
            SupportsTools = true,
            PreferFrameworkManagedChatHistory = true,
            SupportsBackgroundResponses = false,
            ConfigurationJson = "{}",
            Notes = "Regression fixture for process HR matching."
        });
        var unsupportedAgentId = await workspaceService.SaveAgentAsync(new AgentEditorModel
        {
            Name = "Product owner AI agent",
            RoleTitle = "Product owner",
            Summary = "Exact product owner role fixture backed by a provider that cannot return process structured output.",
            Instructions = "Summarize product owner requests.",
            Status = AgentLifecycleStatus.Active,
            ProviderProfileId = unsupportedProviderId,
            Workload = AgentWorkloadKind.Management,
            ChatHistoryMode = AgentChatHistoryMode.ProviderDefault,
            ConfigurationJson = "{}",
            IsTemplate = false,
            Permissions = AgentPermissionsPolicy.Default,
            Tags =
            [
                "product-owner"
            ]
        });
        var structuredAgentId = await workspaceService.SaveAgentAsync(new AgentEditorModel
        {
            Name = "Business Strategist Structured",
            RoleTitle = "Business Strategist",
            Summary = "Owns product scope, business planning, requirements, and delivery strategy.",
            Instructions = "Convert product goals into process-ready scope and requirements.",
            Status = AgentLifecycleStatus.Active,
            ProviderProfileId = structuredProviderId,
            Workload = AgentWorkloadKind.Management,
            ChatHistoryMode = AgentChatHistoryMode.ProviderDefault,
            ConfigurationJson = "{}",
            WorkspaceToolAccess = WorkspaceAccess(AgentWorkspaceToolProfileKind.BusinessAnalysis),
            IsTemplate = false,
            Permissions = AgentPermissionsPolicy.Default,
            Tags =
            [
                "business",
                "strategy",
                "requirements"
            ]
        });
        await workspaceService.SaveAgentAsync(new AgentEditorModel
        {
            Name = "Delivery Manager Structured",
            RoleTitle = "Delivery Manager",
            Summary = "Coordinates delivery governance, release readiness, and process evidence.",
            Instructions = "Coordinate delivery steps and report blockers.",
            Status = AgentLifecycleStatus.Active,
            ProviderProfileId = structuredProviderId,
            Workload = AgentWorkloadKind.Management,
            ChatHistoryMode = AgentChatHistoryMode.ProviderDefault,
            ConfigurationJson = "{}",
            WorkspaceToolAccess = WorkspaceAccess(AgentWorkspaceToolProfileKind.BusinessAnalysis),
            IsTemplate = false,
            Permissions = AgentPermissionsPolicy.Default,
            Tags =
            [
                "delivery-manager",
                "release-manager",
                "delivery",
                "manager"
            ]
        });
        await workspaceService.SaveAgentAsync(new AgentEditorModel
        {
            Name = ".NET Architect Structured",
            RoleTitle = ".NET Solution Architect",
            Summary = "Reviews .NET application architecture, Blazor boundaries, and implementation readiness.",
            Instructions = "Review architecture and source-of-truth boundaries.",
            Status = AgentLifecycleStatus.Active,
            ProviderProfileId = structuredProviderId,
            Workload = AgentWorkloadKind.Programming,
            ChatHistoryMode = AgentChatHistoryMode.ProviderDefault,
            ConfigurationJson = "{}",
            WorkspaceToolAccess = WorkspaceAccess(AgentWorkspaceToolProfileKind.ArchitectureReview),
            IsTemplate = false,
            Permissions = AgentPermissionsPolicy.Default,
            Tags =
            [
                "solution-architect",
                "architecture",
                "dotnet"
            ]
        });
        await workspaceService.SaveAgentAsync(new AgentEditorModel
        {
            Name = ".NET Developer Structured",
            RoleTitle = ".NET Lead Engineer",
            Summary = "Implements .NET and Blazor changes with build, test, runtime, and scaffold access.",
            Instructions = "Implement and validate .NET changes.",
            Status = AgentLifecycleStatus.Active,
            ProviderProfileId = structuredProviderId,
            Workload = AgentWorkloadKind.Programming,
            ChatHistoryMode = AgentChatHistoryMode.ProviderDefault,
            ConfigurationJson = "{}",
            WorkspaceToolAccess = WorkspaceAccess(AgentWorkspaceToolProfileKind.SoftwareDevelopment),
            IsTemplate = false,
            Permissions = AgentPermissionsPolicy.Default,
            Tags =
            [
                "lead-engineer",
                "software-engineer",
                "dotnet-developer"
            ]
        });
        await workspaceService.SaveAgentAsync(new AgentEditorModel
        {
            Name = "QA Lead Structured",
            RoleTitle = "QA Lead",
            Summary = "Validates .NET test evidence, runtime proof, and repair readiness.",
            Instructions = "Review validation proof and quality risks.",
            Status = AgentLifecycleStatus.Active,
            ProviderProfileId = structuredProviderId,
            Workload = AgentWorkloadKind.Qa,
            ChatHistoryMode = AgentChatHistoryMode.ProviderDefault,
            ConfigurationJson = "{}",
            WorkspaceToolAccess = WorkspaceAccess(AgentWorkspaceToolProfileKind.QualityValidation),
            IsTemplate = false,
            Permissions = AgentPermissionsPolicy.Default,
            Tags =
            [
                "qa-lead",
                "quality",
                "validation"
            ]
        });
        await workspaceService.SaveAgentAsync(new AgentEditorModel
        {
            Name = "Security Reviewer Structured",
            RoleTitle = "Security Reviewer",
            Summary = "Reviews security posture, trust boundaries, and validation evidence.",
            Instructions = "Review security risks and release posture.",
            Status = AgentLifecycleStatus.Active,
            ProviderProfileId = structuredProviderId,
            Workload = AgentWorkloadKind.Research,
            ChatHistoryMode = AgentChatHistoryMode.ProviderDefault,
            ConfigurationJson = "{}",
            WorkspaceToolAccess = WorkspaceAccess(AgentWorkspaceToolProfileKind.SecurityReview),
            IsTemplate = false,
            Permissions = AgentPermissionsPolicy.Default,
            Tags =
            [
                "security-reviewer",
                "security"
            ]
        });

        var projectId = await CreateProjectAsync(projects, "Process launch HR provider filtering");
        var deliveryNode = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Build TetrisGame",
                "Blazor delivery target",
                "Implement the TetrisGame app in the configured output root.",
                $"project:{projectId:D}",
                320,
                220,
                null,
                null,
                "delivery",
                MetadataJson: """{ "outputRoot": "C:\\temp\\CanDoItAll\\TetrisGame" }"""));
        var definitionId = ProcessDefinitionCatalogProjectionService
            .CreateDefinitionId(new ProcessDefinitionCatalogItemKey("software-delivery"))
            .Value;

        await agentService.LinkProcessDefinitionAsync(
            projectId,
            deliveryNode.Id,
            new ProjectStructureProcessDefinitionLinkInput(definitionId),
            DefaultAgent);

        var result = await agentService.StartProcessNodeAsync(
            projectId,
            ProjectStructureProcessNodeKeys.BuildProcessDefinitionNodeKey(definitionId),
            new ProjectStructureProcessNodeStartInput(
                RunHrMatch: true,
                Execute: false,
                IncludeLaunchPlan: true,
                RequestedBy: "integration-test"),
            DefaultAgent);

        var launchPlan = Assert.IsType<ProcessLaunchPlanView>(result.LaunchPlan);
        Assert.DoesNotContain(
            launchPlan.ReadinessFindings,
            finding => string.Equals(finding.StepKey, "feature-intake", StringComparison.Ordinal) &&
                       finding.Severity == ProcessLaunchReadinessSeverity.Error);

        var featureIntake = Assert.Single(launchPlan.Steps, step => step.StepKey == "feature-intake");
        Assert.NotEqual(unsupportedAgentId.ToString("D"), featureIntake.ExecutorId);
        Assert.Equal(structuredAgentId.ToString("D"), featureIntake.ExecutorId);
        Assert.Equal("Business Strategist Structured", featureIntake.ExecutorDisplayName);
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
    [Trait("Category", "LiveComfyUi")]
    public async Task AgentService_CreateAssetAsync_stores_live_comfyui_flux_generated_image()
    {
        if (!IsLiveComfyUiFluxProofEnabled())
        {
            return;
        }

        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workspaceFactory = scope.ServiceProvider.GetRequiredService<ICanDoItAllAgentWorkspaceFactory>();
        var imageGenerationService = scope.ServiceProvider.GetRequiredService<IAgentImageGenerationService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var workspaceService = workspaceFactory.GetOrganizationWorkspaceService();
        var providers = await workspaceService.ListProvidersAsync();
        var provider = Assert.Single(
            providers,
            item => item.Kind == ProviderKind.ComfyUi &&
                    item.Purpose == ProviderProfilePurpose.ImageGeneration &&
                    string.Equals(item.Name, ComfyUiFluxProviderDefaults.ProviderName, StringComparison.Ordinal));
        var projectId = await CreateProjectAsync(projects, "Live ComfyUI Flux project asset proof");
        var prompt = "A compact CanDoItAll project board with a generated image asset card, crisp product illustration, no text.";

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        var generated = await imageGenerationService.GenerateAsync(
            new AgentImageGenerationRequest(
                provider,
                provider.DefaultModel,
                prompt,
                "1024x1024",
                "low",
                AgentGeneratedImageFormat.Png,
                []),
            timeout.Token);
        var image = Assert.Single(generated.Images);

        Assert.NotEmpty(image.Bytes);
        Assert.Equal(ComfyUiFluxProviderDefaults.DefaultModel, generated.Model);

        var created = await agentService.CreateAssetAsync(
            projectId,
            new ProjectStructureAssetCreateInput(
                ProjectObjectType.ImageAsset,
                "Live ComfyUI Flux proof",
                "Generated by the local ComfyUI Flux provider",
                prompt,
                new ProjectObjectMediaPayload(
                    "live-comfyui-flux-proof.png",
                    "image/png",
                    Convert.ToBase64String(image.Bytes)),
                $"project:{projectId:D}",
                "generated",
                JsonSerializer.Serialize(new
                {
                    providerId = provider.Id,
                    providerName = provider.Name,
                    model = generated.Model,
                    source = "IAgentImageGenerationService"
                })),
            DefaultAgent,
            timeout.Token);
        var asset = await agentService.GetAssetAsync(projectId, created.Id, timeout.Token);
        var content = await agentService.GetAssetContentAsync(projectId, created.Id, timeout.Token);
        var storedBytes = Convert.FromBase64String(content.Base64Data);
        var generatedHash = Convert.ToHexString(SHA256.HashData(image.Bytes));
        var storedHash = Convert.ToHexString(SHA256.HashData(storedBytes));

        Assert.Equal(ProjectObjectType.ImageAsset, asset.ObjectType);
        Assert.Equal("generated", asset.ObjectSubtype);
        Assert.Equal("image/png", asset.MediaContentType);
        Assert.Equal("live-comfyui-flux-proof.png", asset.MediaOriginalFileName);
        Assert.Equal(image.Bytes.Length, content.ContentLength);
        Assert.Equal(generatedHash, storedHash);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, storedBytes.Take(8).ToArray());

        await WriteLiveComfyUiFluxProofAsync(
            provider,
            projectId,
            asset,
            prompt,
            generated.Model,
            storedHash,
            storedBytes,
            timeout.Token);
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

    private static AgentWorkspaceToolAccessSettings WorkspaceAccess(AgentWorkspaceToolProfileKind profile)
    {
        return AgentWorkspaceToolAccessProfiles.CreateSettings(profile);
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

    private static bool IsLiveComfyUiFluxProofEnabled()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable(LiveComfyUiFluxProofVariable),
            "1",
            StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WriteLiveComfyUiFluxProofAsync(
        ProviderProfile provider,
        Guid projectId,
        ProjectStructureAssetDescriptor asset,
        string prompt,
        string model,
        string storedHash,
        byte[] storedBytes,
        CancellationToken cancellationToken)
    {
        var configuredProofDirectory = Environment.GetEnvironmentVariable(LiveComfyUiFluxProofDirectoryVariable);
        if (string.IsNullOrWhiteSpace(configuredProofDirectory))
        {
            return;
        }

        var proofDirectory = Path.GetFullPath(configuredProofDirectory);
        Directory.CreateDirectory(proofDirectory);
        var imagePath = Path.Combine(proofDirectory, "project-structure-live-comfyui-flux.png");
        var summaryPath = Path.Combine(proofDirectory, "project-structure-live-comfyui-flux-summary.json");
        await File.WriteAllBytesAsync(imagePath, storedBytes, cancellationToken);
        await File.WriteAllTextAsync(
            summaryPath,
            JsonSerializer.Serialize(
                new
                {
                    provider.Id,
                    ProviderName = provider.Name,
                    ProjectId = projectId,
                    asset.NodeId,
                    asset.Title,
                    asset.ObjectType,
                    asset.ObjectSubtype,
                    asset.MediaContentType,
                    asset.MediaOriginalFileName,
                    asset.MediaRelativePath,
                    Model = model,
                    Prompt = prompt,
                    ContentLength = storedBytes.LongLength,
                    Sha256 = storedHash,
                    ImagePath = imagePath
                },
                new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);
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
