using System.IO.Compression;
using System.Net;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.ProjectStructure;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
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
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var leaseService = scope.ServiceProvider.GetRequiredService<ProjectStructureLeaseService>();
        var projectId = await CreateProjectAsync(projects, "Lease conflict project");
        var projectScopeKey = projectId.ToString("D");

        var initialLease = await leaseService.AcquireAsync(
            new ProjectStructureLeaseAcquireRequest(ProjectStructureLeaseScopeKind.Project, projectScopeKey, "Initial mutation", 15),
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
                new ProjectStructureLeaseAcquireRequest(ProjectStructureLeaseScopeKind.Project, projectScopeKey, "Competing mutation", 15),
                competitor));

        Assert.Equal(ProjectStructureLeaseScopeKind.Project, conflict.Conflict.ScopeKind);
        Assert.Equal(projectScopeKey, conflict.Conflict.ScopeKey);
        Assert.Equal(DefaultAgent.AgentId, conflict.Conflict.AgentId);
        Assert.Equal(DefaultAgent.AgentName, conflict.Conflict.AgentName);
        Assert.Equal(DefaultAgent.MachineName, conflict.Conflict.MachineName);
    }

    [Fact]
    public async Task LeaseService_RunWithProjectMutationLeaseAsync_preserves_existing_owned_lease()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var leaseService = scope.ServiceProvider.GetRequiredService<ProjectStructureLeaseService>();

        var projectId = await CreateProjectAsync(projects, "Existing owned lease project");
        var initialLease = await leaseService.AcquireAsync(
            new ProjectStructureLeaseAcquireRequest(ProjectStructureLeaseScopeKind.Project, projectId.ToString("D"), "Long-lived validation lease", 30),
            DefaultAgent);

        var result = await leaseService.RunWithProjectMutationLeaseAsync(
            projectId,
            null,
            DefaultAgent,
            "Temporary mutation without explicit token",
            _ => Task.FromResult("ok"));

        var preservedLease = await leaseService.ValidateOwnedLeaseAsync(
            ProjectStructureLeaseScopeKind.Project,
            projectId.ToString("D"),
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
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var leaseService = scope.ServiceProvider.GetRequiredService<ProjectStructureLeaseService>();

        var projectId = await CreateProjectAsync(projects, "Cancelled temporary lease project");
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
            projectId.ToString("D"),
            CancellationToken.None);

        Assert.Null(activeLease);
    }

    [Fact]
    public async Task LeaseService_RunWithProjectMutationLeaseAsync_waits_once_for_near_expiry_competing_lease()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var leaseService = scope.ServiceProvider.GetRequiredService<ProjectStructureLeaseService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();

        var projectId = await CreateProjectAsync(projects, "Near-expiry lease project");
        var competingLease = await leaseService.AcquireAsync(
            new ProjectStructureLeaseAcquireRequest(ProjectStructureLeaseScopeKind.Project, projectId.ToString("D"), "Competing short mutation", 1),
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
            projectId.ToString("D"),
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
    public async Task RuntimeGateway_CreateNodeAsync_rejects_a_wrapped_dotnet_script_before_persistence()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var gateway = scope.ServiceProvider.GetRequiredService<IProjectStructureRuntimeGateway>();

        var projectId = await CreateProjectAsync(projects, "Runtime gateway validation boundary");
        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            gateway.CreateNodeAsync(
                projectId,
                new ProjectStructureRuntimeNodeCreateRequest(
                    ProjectObjectType.Script,
                    "Invalid gateway runtime",
                    "PowerShell",
                    "The workflow gateway must not bypass runtime validation.",
                    $"project:{projectId:D}",
                    ObjectSubtype: "powershell",
                    MetadataJson: CreateScriptRuntimeMetadata(
                        "pwsh",
                        "-NoProfile -Command \"dotnet watch --project Calculator.csproj run\"")),
                DefaultRuntimeAgent));

        Assert.Equal("InvalidRuntimeMetadata", exception.ErrorCode);
        Assert.True(exception.IsSafeToExpose);
        Assert.True(exception.CanRetryWithCorrectedInput);
        Assert.Contains("typed Environment node", exception.Message, StringComparison.Ordinal);

        var surface = await workbench.GetStructureAsync(projectId);
        Assert.DoesNotContain(surface.Nodes, node => node.Title == "Invalid gateway runtime");
    }

    [Fact]
    public async Task RuntimeGateway_CreateNodeAsync_rejects_an_unscoped_project_block_root_in_an_audited_run()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var gateway = scope.ServiceProvider.GetRequiredService<IProjectStructureRuntimeGateway>();
        var workspaceRoot = scope.ServiceProvider.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();
        var unscopedRoot = Path.Combine(
            Path.GetDirectoryName(workspaceRoot) ?? Path.GetPathRoot(workspaceRoot)!,
            $"unscoped-project-root-{Guid.NewGuid():N}");
        var metadataJson = ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
        {
            ProjectBlock = new ProjectBlockMetadata
            {
                OutputRoot = unscopedRoot
            }
        });
        var projectId = await CreateProjectAsync(projects, "Runtime gateway root authority");
        using var auditScope = WorkspaceExecutionAuditContext.BeginScope(
            CreateAuditedExecutionRun([]));

        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            gateway.CreateNodeAsync(
                projectId,
                new ProjectStructureRuntimeNodeCreateRequest(
                    ProjectObjectType.ProjectBlock,
                    "Unscoped delivery root",
                    "Rejected",
                    "An audited workflow cannot mint its own external target authority.",
                    $"project:{projectId:D}",
                    ObjectSubtype: "delivery",
                    MetadataJson: metadataJson),
                DefaultRuntimeAgent));

        Assert.Equal(ProjectStructureAgentRootAuthorityWriteGuard.FailureCode, exception.ErrorCode);
        Assert.True(exception.IsSafeToExpose);
        Assert.True(exception.CanRetryWithCorrectedInput);
        Assert.DoesNotContain(unscopedRoot, exception.Message, StringComparison.OrdinalIgnoreCase);

        var surface = await workbench.GetStructureAsync(projectId);
        Assert.DoesNotContain(surface.Nodes, node => node.Title == "Unscoped delivery root");
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
                "Blazor WebAssembly delivery target",
                "Implement the TetrisGame app in the outputRoot path as a Blazor WebAssembly browser app.",
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
        var runtimeCommandAssignment = assignments.Single(item => item.StepKey == "record-runtime-commands");
        Assert.Contains("ExecuteExternalAction", runtimeCommandAssignment.AllowedOperations);
        var qaAssignment = assignments.Single(item => item.StepKey == "qa-validation");
        Assert.Contains(
            qaAssignment.CapabilityScope.RequiredReceipts,
            receipt => string.Equals(receipt.ToolName, "workspace_dotnet_run", StringComparison.Ordinal));
        Assert.Contains(
            qaAssignment.CapabilityScope.RequiredReceipts,
            receipt => string.Equals(receipt.ToolName, "browser_take_screenshot", StringComparison.Ordinal));
        var screenshotAssignment = assignments.Single(item => item.StepKey == "capture-ui-screenshots");
        Assert.Equal("ExternalActionControlled", screenshotAssignment.OperationTargetScope);
        Assert.Contains("ExecuteExternalAction", screenshotAssignment.AllowedOperations);
        Assert.DoesNotContain("LaunchRuntime", screenshotAssignment.AllowedOperations);
        Assert.DoesNotContain("CaptureRuntimeProof", screenshotAssignment.AllowedOperations);

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
        var imageAnalysisService = new RecordingImageAnalysisService();
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            ConfigureServices = services =>
            {
                services.RemoveAll<IAgentImageAnalysisService>();
                services.AddSingleton<IAgentImageAnalysisService>(imageAnalysisService);
            }
        });
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var launchService = scope.ServiceProvider.GetRequiredService<ProcessLaunchApplicationService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var fileInteractionCoordinator = scope.ServiceProvider
            .GetRequiredService<ProjectStructureKnownFileInteractionCoordinator>();
        var nodeFileScopeProvider = scope.ServiceProvider
            .GetRequiredService<IProjectStructureNodeFileScopeProvider>();
        var nodeStorageBindingSource = scope.ServiceProvider
            .GetServices<IFileToolsStorageBindingSource>()
            .Single(source => source.ScopeKind == FileToolsSemanticScopeKind.ProjectNode);
        var runtimeToolProvider = scope.ServiceProvider
            .GetServices<IAgentRuntimeToolProvider>()
            .OfType<ProjectStructureAgentRuntimeToolProvider>()
            .Single();

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
        byte[] screenshotBytes = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=");
        await File.WriteAllBytesAsync(screenshotFullPath, screenshotBytes);
        string oversizedScreenshotRelativePath = $"{managedArtifactRoot}/browser/oversized.png";
        string oversizedScreenshotPath = Path.Combine(
            application.ActiveProfile.WorkspaceRootPath,
            oversizedScreenshotRelativePath.Replace('/', Path.DirectorySeparatorChar));
        await using (var oversizedScreenshot = new FileStream(
                         oversizedScreenshotPath,
                         FileMode.CreateNew,
                         FileAccess.Write,
                         FileShare.None))
        {
            oversizedScreenshot.SetLength(ProjectStructureAssetUploadLimits.MaximumFileBytes + 1);
        }

        string externalRoot = TestFileSystem.CreateTemporaryRoot("process-screenshot-link-target");
        string externalScreenshotPath = Path.Combine(externalRoot, "escaped.png");
        string linkedScreenshotRelativePath = $"{managedArtifactRoot}/browser/escaped.png";
        string linkedScreenshotPath = Path.Combine(
            application.ActiveProfile.WorkspaceRootPath,
            linkedScreenshotRelativePath.Replace('/', Path.DirectorySeparatorChar));
        try
        {
            await File.WriteAllBytesAsync(externalScreenshotPath, screenshotBytes);
            File.CreateSymbolicLink(linkedScreenshotPath, externalScreenshotPath);

            ProjectStructureSurface reparseSurface = await workbench.GetStructureAsync(projectId);
            Assert.DoesNotContain(
                reparseSurface.Nodes,
                node => string.Equals(
                    node.MediaRelativePath,
                    linkedScreenshotRelativePath,
                    StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(linkedScreenshotPath);
            TestFileSystem.DeleteDirectoryWithRetry(externalRoot);
        }

        var surface = await workbench.GetStructureAsync(projectId);
        var runNode = Assert.Single(surface.Nodes, node => string.Equals(node.Id, runNodeId, StringComparison.Ordinal));
        Assert.Equal(ProjectObjectType.ProcessRun, runNode.ObjectType);
        Assert.Equal(deliveryNode.Id, runNode.ParentId);
        var outputNode = Assert.Single(surface.Nodes, node => string.Equals(node.Id, outputNodeId, StringComparison.Ordinal));
        Assert.Equal(ProjectObjectType.File, outputNode.ObjectType);
        Assert.Equal("folder", outputNode.ObjectSubtype);
        Assert.Equal(runNodeId, outputNode.ParentId);
        Assert.Equal(ProjectStructureProcessNodeKeys.ProcessRunOutputFolderArtifactKind, outputNode.ArtifactKind);
        Assert.Equal(runId.Value, outputNode.ArtifactId);
        Assert.Contains(managedArtifactRoot, outputNode.Notes, StringComparison.Ordinal);
        ProjectObjectMetadataEnvelope outputMetadata = ProjectObjectMetadataSerializer.Parse(outputNode.MetadataJson);
        Assert.Equal(ProjectFileSubtype.Folder, outputMetadata.File?.FileSubtype);
        Assert.Equal(managedArtifactRoot, outputMetadata.File?.ExternalPath);
        FileToolsSemanticScope outputScope = await nodeFileScopeProvider.ResolveNodeCollectionAsync(
            projectId,
            outputNode.Id);
        FileToolsStorageBinding outputBinding = Assert.Single(
            await nodeStorageBindingSource.ResolveAsync(outputScope));
        Assert.Equal(managedArtifactRoot, outputBinding.Root.Value);
        var summaryNode = Assert.Single(surface.Nodes, node => string.Equals(node.Id, ProjectStructureProcessNodeKeys.BuildProcessRunSummaryNodeKey(runId.Value), StringComparison.Ordinal));
        Assert.Equal(ProjectObjectType.Note, summaryNode.ObjectType);
        Assert.Equal("process-summary", summaryNode.ObjectSubtype);
        Assert.Equal(runNodeId, summaryNode.ParentId);
        Assert.Equal("process-run-summary", summaryNode.ArtifactKind);
        Assert.Contains(
            "The process is still active; this node contains live projection data.",
            summaryNode.Notes,
            StringComparison.Ordinal);
        var screenshotNodeId = ProjectStructureProcessNodeKeys.BuildProcessRunScreenshotNodeKey(runId.Value, screenshotRelativePath);
        var screenshotNode = Assert.Single(surface.Nodes, node => string.Equals(node.Id, screenshotNodeId, StringComparison.Ordinal));
        Assert.Equal(ProjectObjectType.ImageAsset, screenshotNode.ObjectType);
        Assert.Equal("screenshot", screenshotNode.ObjectSubtype);
        Assert.Equal(runNodeId, screenshotNode.ParentId);
        Assert.Equal("process-run-screenshot", screenshotNode.ArtifactKind);
        Assert.Equal(screenshotRelativePath, screenshotNode.MediaRelativePath);
        Assert.Equal("image/png", screenshotNode.MediaContentType);
        Assert.False(string.IsNullOrWhiteSpace(screenshotNode.StorageObjectReferenceJson));
        string oversizedScreenshotNodeId = ProjectStructureProcessNodeKeys.BuildProcessRunScreenshotNodeKey(
            runId.Value,
            oversizedScreenshotRelativePath);
        Assert.Contains(
            surface.Nodes,
            node => string.Equals(node.Id, oversizedScreenshotNodeId, StringComparison.Ordinal));
        ProjectStructureAgentException oversizedException = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            agentService.GetAssetContentAsync(projectId, oversizedScreenshotNodeId));
        Assert.Equal(413, oversizedException.StatusCode);
        Assert.Equal("AssetContentTooLarge", oversizedException.ErrorCode);
        ProjectStructureAssetContentDescriptor assetContent = await agentService.GetAssetContentAsync(
            projectId,
            screenshotNodeId);
        Assert.Equal(screenshotBytes, Convert.FromBase64String(assetContent.Base64Data));
        AgentDefinition runtimeAgent = CreateProjectStructureRuntimeAgent(projectId);
        IReadOnlyList<AITool> runtimeTools = await runtimeToolProvider.CreateToolsAsync(
            CreateProjectStructureRuntimeContext(runtimeAgent, projectId),
            CancellationToken.None);
        AIFunction analyzeImage = Assert.IsAssignableFrom<AIFunction>(Assert.Single(
            runtimeTools,
            tool => string.Equals(
                tool.Name,
                AgentToolInvocationPolicyMetadata.ProjectStructureAssetImageAnalyze,
                StringComparison.Ordinal)));
        object? analysisResult = await analyzeImage.InvokeAsync(new AIFunctionArguments
        {
            ["projectId"] = projectId,
            ["nodeId"] = screenshotNodeId,
            ["prompt"] = "Describe the visible screenshot."
        });
        var resultJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        resultJsonOptions.Converters.Add(new JsonStringEnumConverter());
        var analysis = analysisResult switch
        {
            ProjectStructureAssetImageAnalysisDescriptor descriptor => descriptor,
            JsonElement json => JsonSerializer.Deserialize<ProjectStructureAssetImageAnalysisDescriptor>(
                json.GetRawText(),
                resultJsonOptions) ?? throw new InvalidOperationException("Image analysis tool returned null JSON."),
            _ => throw new InvalidOperationException(
                $"Image analysis tool returned unexpected type '{analysisResult?.GetType().FullName ?? "<null>"}'.")
        };
        AgentImageAnalysisRequest imageRequest = Assert.Single(imageAnalysisService.Requests);
        AgentImageAnalysisSource imageSource = Assert.Single(imageRequest.Sources);
        Assert.Equal("vision-model", analysis.Model);
        Assert.Equal("Visible calculator screenshot", analysis.Analysis);
        Assert.Equal("desktop.png", imageSource.Name);
        Assert.Equal("image/png", imageSource.ContentType);
        Assert.Equal(screenshotBytes, imageSource.Bytes);
        await using ProjectStructureKnownFileInteraction interaction =
            await fileInteractionCoordinator.OpenAsync(projectId, screenshotNodeId);
        await using FileContentLease content = await interaction.Session.ContentSource.OpenReadAsync(
            new FileContentReadRequest(interaction.Session.File));
        using var reopenedScreenshot = new MemoryStream();
        await content.Stream.CopyToAsync(reopenedScreenshot);
        Assert.Equal(screenshotBytes, reopenedScreenshot.ToArray());
        Assert.Equal("desktop.png", interaction.Request.FileName);
        Assert.Equal("image/png", interaction.Request.MediaType);
        Assert.False(interaction.CanEdit);
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

        ProjectStructureNode[] projectedChildren = [outputNode, summaryNode, screenshotNode, runtimeNode];
        Assert.All(projectedChildren, child => AssertNodeIsOnOutgoingSide(deliveryNode, runNode, child));
        AssertNodesDoNotOverlap(projectedChildren);

        var refreshedSurface = await workbench.GetStructureAsync(projectId);
        ProjectStructureNode[] projectedNodes = [runNode, .. projectedChildren];
        foreach (var projectedNode in projectedNodes)
        {
            var refreshedNode = Assert.Single(
                refreshedSurface.Nodes,
                node => string.Equals(node.Id, projectedNode.Id, StringComparison.Ordinal));
            Assert.Equal(projectedNode.X, refreshedNode.X);
            Assert.Equal(projectedNode.Y, refreshedNode.Y);
        }

        string ordinaryAssetNodeId = await CreatePersistedAssetReferencingPathAsync(
            dbContextFactory,
            projectId,
            runId.Value,
            screenshotRelativePath,
            screenshotBytes.LongLength);
        ProjectStructureAgentException exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            agentService.GetAssetContentAsync(projectId, ordinaryAssetNodeId));
        Assert.Equal("AssetContentNotFound", exception.ErrorCode);
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
                MetadataJson: CreateProjectBlockMetadata(outputRoot)));
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
        Assert.DoesNotContain(outputRoot, contextSummary, StringComparison.OrdinalIgnoreCase);
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
        var parentLaunchVariables = new Dictionary<string, string>(parentAssignment.LaunchVariables, StringComparer.Ordinal)
        {
            ["ProductTargetFilesystemState"] = "forged-parent-state",
            ["OperationTargetScope"] = ProcessOperationContractNames.ExternalActionControlled,
            ["AllowedOperations"] = ProcessOperationContractNames.ExecuteExternalAction,
            ["ProcessStepAllowedOperations"] = ProcessOperationContractNames.ExecuteExternalAction,
            ["ProcessStepTargetScope"] = ProcessOperationContractNames.ExternalActionControlled,
            ["ProcessStepAllowsProductMutation"] = bool.FalseString,
            [ProcessRuntimeLaunchVariables.ProcessStepKind] = ProcessTemplateStepKinds.Subprocess,
            [ProcessRuntimeLaunchVariables.ProcessStepSubprocessDefinitionKey] = "stale-parent-subprocess",
            ["agentProcessStepAllowedOperations"] = ProcessOperationContractNames.ExecuteExternalAction,
            ["agentProcessStepTargetScope"] = ProcessOperationContractNames.ExternalActionControlled,
            ["agentProcessStepAllowsProductMutation"] = bool.FalseString
        };
        parentAssignment = parentAssignment with
        {
            LaunchVariables = parentLaunchVariables
        };
        await ReplacePersistedAssignmentLaunchVariablesAsync(
            scope.ServiceProvider,
            parentAssignment);

        await EnsureParentStepRunningAsync(
            scope.ServiceProvider,
            new ProcessRunId(parent.RunId.Value),
            parentAssignment.StepInstanceId);
        var subprocess = await agentService.StartProcessSubprocessAsync(
            projectId,
            parent.RunId.Value.ToString("D"),
            parentAssignment.StepInstanceId.ToString(),
            new ProjectStructureProcessSubprocessLaunchInput(
                DefinitionKey: "dotnet-architecture-design-review",
                Variables: new Dictionary<string, object?>(StringComparer.Ordinal)
                {
                    ["ProductTargetFilesystemState"] = "forged-request-state",
                    ["ProjectNodeId"] = "attempted-scope-escape",
                    ["OperationTargetScope"] = ProcessOperationContractNames.ExternalActionControlled,
                    ["AllowedOperations"] = ProcessOperationContractNames.ExecuteExternalAction,
                    ["ProcessStepAllowedOperations"] = ProcessOperationContractNames.ExecuteExternalAction,
                    ["ProcessStepTargetScope"] = ProcessOperationContractNames.ExternalActionControlled,
                    [ProcessRuntimeLaunchVariables.ProcessStepKind] = "InjectedParentStepKind",
                    [ProcessRuntimeLaunchVariables.ProcessStepSubprocessDefinitionKey] = "injected-parent-subprocess",
                    ["agentProcessStepAllowedOperations"] = ProcessOperationContractNames.ExecuteExternalAction,
                    ["agentProcessStepTargetScope"] = ProcessOperationContractNames.ExternalActionControlled,
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
            Assert.True(assignment.LaunchVariables.TryGetValue("ProductTargetFilesystemState", out var targetFilesystemState));
            Assert.NotEqual("forged-parent-state", targetFilesystemState);
            Assert.NotEqual("forged-request-state", targetFilesystemState);
            Assert.Contains(
                targetFilesystemState,
                ["missing", "empty", "populated", "not-directory", "unavailable"],
                StringComparer.OrdinalIgnoreCase);
            Assert.Equal("True", assignment.LaunchVariables["includeLaunchPlan"]);
            Assert.Equal("3", assignment.LaunchVariables["subprocessPriority"]);
            Assert.DoesNotContain("OperationTargetScope", assignment.LaunchVariables.Keys);
            Assert.DoesNotContain("AllowedOperations", assignment.LaunchVariables.Keys);
            Assert.DoesNotContain("ProcessStepAllowedOperations", assignment.LaunchVariables.Keys);
            Assert.DoesNotContain("ProcessStepTargetScope", assignment.LaunchVariables.Keys);
            Assert.DoesNotContain("ProcessStepAllowsProductMutation", assignment.LaunchVariables.Keys);
            if (assignment.LaunchVariables.TryGetValue(ProcessRuntimeLaunchVariables.ProcessStepKind, out var processStepKind))
            {
                Assert.NotEqual(ProcessTemplateStepKinds.Subprocess, processStepKind);
                Assert.NotEqual("InjectedParentStepKind", processStepKind);
            }

            Assert.False(
                assignment.LaunchVariables.TryGetValue(ProcessRuntimeLaunchVariables.ProcessStepSubprocessDefinitionKey, out var childSubprocessDefinitionKey) &&
                (string.Equals(childSubprocessDefinitionKey, "stale-parent-subprocess", StringComparison.Ordinal) ||
                 string.Equals(childSubprocessDefinitionKey, "injected-parent-subprocess", StringComparison.Ordinal)));
            Assert.DoesNotContain("agentProcessStepAllowedOperations", assignment.LaunchVariables.Keys);
            Assert.DoesNotContain("agentProcessStepTargetScope", assignment.LaunchVariables.Keys);
            Assert.DoesNotContain("agentProcessStepAllowsProductMutation", assignment.LaunchVariables.Keys);
            Assert.DoesNotContain("OperationTargetScope: ExternalActionControlled", assignment.Prompt, StringComparison.Ordinal);
            Assert.DoesNotContain("AllowedOperations: ExecuteExternalAction", assignment.Prompt, StringComparison.Ordinal);
            Assert.DoesNotContain("ProcessStepSubprocessDefinitionKey: stale-parent-subprocess", assignment.Prompt, StringComparison.Ordinal);
            Assert.DoesNotContain("ProcessStepSubprocessDefinitionKey: injected-parent-subprocess", assignment.Prompt, StringComparison.Ordinal);
        });

        var reviewAssignment = Assert.Single(childAssignments, assignment => assignment.StepKey == "review-architecture-design");
        Assert.Contains("Producer step: draft-architecture-design - Draft .NET architecture design", reviewAssignment.Prompt, StringComparison.Ordinal);
        Assert.Contains($"artifacts/process-runs/{subprocess.RunId.Value:D}/steps/draft-architecture-design.md", reviewAssignment.Prompt, StringComparison.Ordinal);
        var classifyAssignment = Assert.Single(childAssignments, assignment => assignment.StepKey == "classify-dotnet-application");
        Assert.DoesNotContain("DotNetAppArchetype", classifyAssignment.LaunchVariables.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.False(classifyAssignment.LaunchVariables.ContainsKey("DotNetScaffoldContract"));
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

        await EnsureParentStepRunningAsync(
            scope.ServiceProvider,
            new ProcessRunId(parent.RunId.Value),
            parentAssignment.StepInstanceId);
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

        await EnsureParentStepRunningAsync(
            scope.ServiceProvider,
            new ProcessRunId(parent.RunId.Value),
            parentAssignment.StepInstanceId);
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
    public async Task StartProcessSubprocessAsync_launches_screenshot_writeback_with_readiness_enabled()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var assignmentStore = scope.ServiceProvider.GetRequiredService<IProcessRuntimeStepAssignmentStore>();
        var stateStore = scope.ServiceProvider.GetRequiredService<IProcessRuntimeStateStore>();

        var projectId = await CreateProjectAsync(projects, "Screenshot subprocess launch");
        const string outputRoot = @"C:\temp\CanDoItAll\Calculator";
        var deliveryNode = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Build Calculator",
                "Blazor delivery target",
                "Implement the Calculator app in the configured output root.",
                $"project:{projectId:D}",
                320,
                220,
                null,
                null,
                "delivery",
                MetadataJson: $$"""{ "outputRoot": "{{outputRoot.Replace(@"\", @"\\", StringComparison.Ordinal)}}" }"""));
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
        var parentAssignment = Assert.Single(parentAssignments, item => item.StepKey == "capture-ui-screenshots");

        await EnsureParentStepRunningAsync(
            scope.ServiceProvider,
            new ProcessRunId(parent.RunId.Value),
            parentAssignment.StepInstanceId);
        var subprocess = await agentService.StartProcessSubprocessAsync(
            projectId,
            parent.RunId.Value.ToString("D"),
            parentAssignment.StepInstanceId.ToString(),
            new ProjectStructureProcessSubprocessLaunchInput(
                DefinitionKey: "dotnet-ui-screenshot-writeback",
                RunHrMatch: true,
                Execute: false,
                IncludeLaunchPlan: true,
                RequestedBy: "integration-test"),
            DefaultAgent);

        Assert.NotNull(subprocess.RunId);
        Assert.Equal("Running", subprocess.Stage);
        Assert.DoesNotContain(subprocess.Warnings, warning => warning.Contains("without starting a child run", StringComparison.OrdinalIgnoreCase));
        Assert.StartsWith($"/projects/{projectId:D}/processes/live?runId=", subprocess.Route, StringComparison.Ordinal);
        Assert.Contains($"artifacts/process-runs/{subprocess.RunId.Value:D}/steps/capture-ui-screenshots.md", subprocess.ExpectedChildEvidenceRefs);
        Assert.Contains($"artifacts/process-runs/{subprocess.RunId.Value:D}/steps/store-ui-screenshots.md", subprocess.ExpectedChildEvidenceRefs);

        var childState = await stateStore.LoadAsync(new ProcessRunId(subprocess.RunId.Value));
        Assert.NotNull(childState);
        Assert.Equal(new ProcessRunId(parent.RunId.Value), childState!.RootRunId);

        var childAssignments = await assignmentStore.LoadByRunAsync(new ProcessRunId(subprocess.RunId.Value));
        var captureAssignment = Assert.Single(childAssignments, assignment => assignment.StepKey == "capture-ui-screenshots");
        var storeAssignment = Assert.Single(childAssignments, assignment => assignment.StepKey == "store-ui-screenshots");

        Assert.Contains(ProcessOperationContractNames.LaunchRuntime, captureAssignment.AllowedOperations);
        Assert.Contains(ProcessOperationContractNames.CaptureRuntimeProof, captureAssignment.AllowedOperations);
        Assert.Contains(ProcessOperationContractNames.ExecuteExternalAction, storeAssignment.AllowedOperations);
        Assert.Equal(parent.RunId.Value.ToString("D"), captureAssignment.LaunchVariables["ParentProcessRunId"]);
        Assert.Equal(parentAssignment.StepInstanceId.ToString(), captureAssignment.LaunchVariables["ParentProcessStepId"]);
        Assert.Equal("capture-ui-screenshots", captureAssignment.LaunchVariables["ParentProcessStepKey"]);
        Assert.Equal("dotnet-ui-screenshot-writeback", captureAssignment.LaunchVariables["SubprocessDefinitionKey"]);
        Assert.DoesNotContain(ProcessRuntimeLaunchVariables.ProcessStepSubprocessDefinitionKey, captureAssignment.LaunchVariables.Keys);
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

        await EnsureParentStepRunningAsync(
            scope.ServiceProvider,
            new ProcessRunId(parent.RunId.Value),
            parentAssignment.StepInstanceId);
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

        await EnsureParentStepRunningAsync(
            scope.ServiceProvider,
            new ProcessRunId(parent.RunId.Value),
            parentAssignment.StepInstanceId);
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
    public async Task StartProcessSubprocessAsync_returns_blocked_child_instead_of_relaunching_after_previous_child_was_blocked()
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

        await EnsureParentStepRunningAsync(
            scope.ServiceProvider,
            new ProcessRunId(parent.RunId.Value),
            parentAssignment.StepInstanceId);
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

        await EnsureParentStepRunningAsync(
            scope.ServiceProvider,
            new ProcessRunId(parent.RunId.Value),
            parentAssignment.StepInstanceId);
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
        Assert.Equal(firstChild.RunId, retryChild.RunId);
        Assert.Equal("Blocked", retryChild.Stage);
        Assert.Contains(retryChild.Warnings, warning => warning.Contains("did not start a replacement child", StringComparison.Ordinal));
        Assert.Contains(firstChild.RunId.Value.ToString("D"), retryChild.ParentDeferredOutcomeJson, StringComparison.Ordinal);
        Assert.Contains("must not launch a replacement child automatically", retryChild.ParentDeferredOutcomeJson, StringComparison.Ordinal);

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
        var matchingChildRunId = Assert.Single(matchingChildRunIds);
        Assert.Equal(firstChild.RunId.Value, matchingChildRunId.Value);
    }

    [Fact]
    public async Task StartProcessSubprocessAsync_returns_completed_parent_outcome_after_previous_child_completed()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var assignmentStore = scope.ServiceProvider.GetRequiredService<IProcessRuntimeStepAssignmentStore>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ProcessPersistenceDbContext>();

        var projectId = await CreateProjectAsync(projects, "Process subprocess retry after completed child");
        var deliveryNode = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Deliver Calculator",
                "Blazor delivery target",
                "Create a calculator app.",
                $"project:{projectId:D}",
                320,
                220,
                null,
                null,
                "delivery",
                MetadataJson: """{ "outputRoot": "C:\\temp\\CanDoItAll\\Calculator" }"""));
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

        await EnsureParentStepRunningAsync(
            scope.ServiceProvider,
            new ProcessRunId(parent.RunId.Value),
            parentAssignment.StepInstanceId);
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
        childState.Status = ProcessRuntimeStatus.Completed;
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

            childStep.Status = ProcessRuntimeStepStatus.Completed;
            childStep.ActiveClaimToken = null;
        }

        await dbContext.SaveChangesAsync();

        await EnsureParentStepRunningAsync(
            scope.ServiceProvider,
            new ProcessRunId(parent.RunId.Value),
            parentAssignment.StepInstanceId);
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
        Assert.Equal(firstChild.RunId, retryChild.RunId);
        Assert.Equal("Completed", retryChild.Stage);
        Assert.DoesNotContain(retryChild.Warnings, warning => warning.Contains("replacement child", StringComparison.Ordinal));
        Assert.Contains("\"status\":\"Completed\"", retryChild.ParentDeferredOutcomeJson, StringComparison.Ordinal);
        Assert.Contains(firstChild.RunId.Value.ToString("D"), retryChild.ParentDeferredOutcomeJson, StringComparison.Ordinal);
        Assert.Contains("complete the parent step from child evidence", retryChild.ParentDeferredOutcomeInstruction, StringComparison.Ordinal);

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
        var matchingChildRunId = Assert.Single(matchingChildRunIds);
        Assert.Equal(firstChild.RunId.Value, matchingChildRunId.Value);
    }

    [Fact]
    public async Task StartProcessSubprocessAsync_supplies_dotnet_solution_setup_scaffold_contract_from_bound_solution_context()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var assignmentStore = scope.ServiceProvider.GetRequiredService<IProcessRuntimeStepAssignmentStore>();
        var stateStore = scope.ServiceProvider.GetRequiredService<IProcessRuntimeStateStore>();
        var workspaceFiles = scope.ServiceProvider.GetRequiredService<IWorkspaceFileService>();

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
            .CreateDefinitionId(new ProcessDefinitionCatalogItemKey("dotnet-development-slice"))
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
        var parentAssignment = Assert.Single(parentAssignments, item => item.StepKey == "prepare-solution-skeleton");
        var parentState = await stateStore.LoadAsync(new ProcessRunId(parent.RunId.Value));
        Assert.NotNull(parentState);
        var parentStepState = Assert.Single(
            parentState!.Steps,
            step => step.StepInstanceId == parentAssignment.StepInstanceId);
        var solutionContextDescriptor = Assert.Single(
            parentStepState.ArtifactDescriptors,
            descriptor => parentStepState.RequiredArtifactSlots.Contains(descriptor.SlotId) &&
                          string.Equals(descriptor.StepKey, "slice-architecture-check", StringComparison.Ordinal) &&
                          string.Equals(descriptor.ArtifactExpectationKey, "dotnet-solution-context", StringComparison.Ordinal));
        var solutionContextWrite = workspaceFiles.WriteTextFile(
            solutionContextDescriptor.PrimaryManagedRef,
            """
            TetrisGame solution context

            ```json
            {
              "schema": "dotnet.solution-context/v1",
              "provisioningMode": "initialize",
              "solution": {
                "file": "TetrisGame.slnx",
                "candidateFiles": ["TetrisGame.slnx", "TetrisGame.sln"]
              },
              "requiredProjectFiles": [
                "src/TetrisGame/TetrisGame.csproj",
                "tests/TetrisGame.Tests/TetrisGame.Tests.csproj"
              ],
              "testProjectFiles": ["tests/TetrisGame.Tests/TetrisGame.Tests.csproj"],
              "initialization": {
                "solutionName": "TetrisGame",
                "application": {
                  "name": "TetrisGame",
                  "directory": "src/TetrisGame",
                  "file": "src/TetrisGame/TetrisGame.csproj",
                  "template": "blazorwasm",
                  "templateOptions": ["--pwa"],
                  "archetype": "Blazor WebAssembly PWA"
                },
                "tests": {
                  "name": "TetrisGame.Tests",
                  "directory": "tests/TetrisGame.Tests",
                  "file": "tests/TetrisGame.Tests/TetrisGame.Tests.csproj",
                  "template": "xunit",
                  "frameworkPreference": "xUnit"
                },
                "targetFramework": "net10.0"
              }
            }
            ```
            """);
        Assert.True(solutionContextWrite.Succeeded, solutionContextWrite.Message);

        var genericParentLaunchVariables = parentAssignment.LaunchVariables
            .Where(item =>
                !item.Key.Contains("Root", StringComparison.OrdinalIgnoreCase) &&
                !item.Key.Contains("Alias", StringComparison.OrdinalIgnoreCase) &&
                !item.Key.StartsWith("DotNet", StringComparison.OrdinalIgnoreCase) &&
                !item.Key.StartsWith(ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths, StringComparison.OrdinalIgnoreCase) &&
                !item.Key.StartsWith(ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts, StringComparison.OrdinalIgnoreCase) &&
                !item.Key.StartsWith(ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);
        parentAssignment = parentAssignment with
        {
            LaunchVariables = genericParentLaunchVariables
        };
        await ReplacePersistedAssignmentLaunchVariablesAsync(
            scope.ServiceProvider,
            parentAssignment);
        Assert.DoesNotContain("ProductRoot", parentAssignment.LaunchVariables.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("OutputRoot", parentAssignment.LaunchVariables.Keys, StringComparer.OrdinalIgnoreCase);

        await EnsureParentStepRunningAsync(
            scope.ServiceProvider,
            new ProcessRunId(parent.RunId.Value),
            parentAssignment.StepInstanceId);
        var subprocess = await agentService.StartProcessSubprocessAsync(
            projectId,
            parent.RunId.Value.ToString("D"),
            parentAssignment.StepInstanceId.ToString(),
            new ProjectStructureProcessSubprocessLaunchInput(
                DefinitionKey: "dotnet-solution-setup",
                Variables: new Dictionary<string, object?>
                {
                    ["ProductRoot"] = @"C:\stale\wrong",
                    ["OutputRoot"] = @"C:\stale\wrong",
                    ["WorkspaceAlias"] = "external-target/C/stale/wrong",
                    ["ProjectStructureContextSummary"] = "Stale unrelated context from another project.",
                    ["ScopeSummary"] = "Build an unrelated payroll dashboard.",
                    ["ChildScopeMvp"] = "Smallest observable path for an unrelated payroll dashboard.",
                    ["SourceCitations"] = "[\"managed-files/project-media/files/stale/old-summary.md\"]",
                    ["SourceOfTruth"] = "stale-import"
                },
                RunHrMatch: false,
                Execute: false,
                IncludeLaunchPlan: true,
                RequestedBy: "integration-test"),
            DefaultAgent);

        var childAssignments = await assignmentStore.LoadByRunAsync(new ProcessRunId(subprocess.RunId!.Value));
        var scaffoldAssignment = Assert.Single(childAssignments, assignment => assignment.StepKey == "scaffold-contract");
        var createProjectAssignment = Assert.Single(childAssignments, assignment => assignment.StepKey == "create-dotnet-project");
        var addTestProjectAssignment = Assert.Single(childAssignments, assignment => assignment.StepKey == "add-test-project");
        var validateFirstBuildAssignment = Assert.Single(childAssignments, assignment => assignment.StepKey == "validate-first-build");
        var revalidateFirstBuildAssignment = Assert.Single(childAssignments, assignment => assignment.StepKey == "validate-first-build-after-repair");
        var expectedManagedArtifactRoot = ProcessLaunchApplicationService.BuildManagedProcessArtifactRoot(new ProcessRunId(subprocess.RunId!.Value));
        var expectedCreateProjectScriptRef = $"{expectedManagedArtifactRoot}/scripts/create-dotnet-project.wire-solution.ps1";
        var expectedAddTestProjectScriptRef = $"{expectedManagedArtifactRoot}/scripts/add-test-project.wire-solution.ps1";
        Assert.DoesNotContain(ProcessOperationContractNames.RunValidation, createProjectAssignment.AllowedOperations);
        Assert.DoesNotContain(ProcessOperationContractNames.RunValidation, addTestProjectAssignment.AllowedOperations);
        Assert.Contains(ProcessOperationContractNames.RunValidation, validateFirstBuildAssignment.AllowedOperations);
        Assert.Contains(ProcessOperationContractNames.RunValidation, revalidateFirstBuildAssignment.AllowedOperations);
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
        Assert.DoesNotContain("ScopeSummary", scaffoldAssignment.LaunchVariables.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("ChildScopeMvp", scaffoldAssignment.LaunchVariables.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("SourceCitations", scaffoldAssignment.LaunchVariables.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("SourceOfTruth", scaffoldAssignment.LaunchVariables.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("TetrisGame", scaffoldAssignment.LaunchVariables["ProjectStructureContextSummary"], StringComparison.Ordinal);
        Assert.Contains("Blazor WASM PWA app shape", scaffoldAssignment.LaunchVariables["ProjectStructureContextSummary"], StringComparison.Ordinal);
        Assert.DoesNotContain("unrelated payroll", scaffoldAssignment.LaunchVariables["ProjectStructureContextSummary"], StringComparison.OrdinalIgnoreCase);
        Assert.False(scaffoldAssignment.LaunchVariables.ContainsKey("DotNetScaffoldContract"));
        using var createPlanDocument = JsonDocument.Parse(scaffoldAssignment.LaunchVariables["DotNetCreateProjectExecutionPlan"]);
        Assert.Equal("dotnet.create-project", createPlanDocument.RootElement.GetProperty("PlanKey").GetString());
        Assert.True(createPlanDocument.RootElement.GetProperty("RequiresScaffold").GetBoolean());
        Assert.Equal(
            expectedCreateProjectScriptRef,
            scaffoldAssignment.LaunchVariables["DotNetCreateProjectScriptRef"]);
        Assert.DoesNotContain("{CurrentProcessRunId}", scaffoldAssignment.LaunchVariables["DotNetCreateProjectExecutionPlan"], StringComparison.Ordinal);
        Assert.Contains("DotNetCreateProjectScript", scaffoldAssignment.LaunchVariables.Keys, StringComparer.Ordinal);
        Assert.Contains("DotNetCreateProjectSideEffectManifest", scaffoldAssignment.LaunchVariables.Keys, StringComparer.Ordinal);
        Assert.Equal(
            expectedAddTestProjectScriptRef,
            scaffoldAssignment.LaunchVariables["DotNetAddTestProjectScriptRef"]);
        using var addTestPlanDocument = JsonDocument.Parse(scaffoldAssignment.LaunchVariables["DotNetAddTestProjectExecutionPlan"]);
        Assert.Equal("dotnet.add-test-project", addTestPlanDocument.RootElement.GetProperty("PlanKey").GetString());
        Assert.False(addTestPlanDocument.RootElement.GetProperty("RequiresScaffold").GetBoolean());
        Assert.DoesNotContain("{CurrentProcessRunId}", scaffoldAssignment.LaunchVariables["DotNetAddTestProjectExecutionPlan"], StringComparison.Ordinal);
        Assert.Contains("$SolutionCandidates", scaffoldAssignment.LaunchVariables["DotNetAddTestProjectScript"], StringComparison.Ordinal);
        Assert.Contains("$newTestProjectArguments = @('new', $TestTemplate", scaffoldAssignment.LaunchVariables["DotNetAddTestProjectScript"], StringComparison.Ordinal);
        Assert.Contains("$newTestProjectArguments += @('--framework', $TargetFramework)", scaffoldAssignment.LaunchVariables["DotNetAddTestProjectScript"], StringComparison.Ordinal);
        Assert.Contains("Test-SolutionContainsProject $SolutionFile $AppProjectFile", scaffoldAssignment.LaunchVariables["DotNetAddTestProjectScript"], StringComparison.Ordinal);
        Assert.Contains("Invoke-Dotnet @('sln', $SolutionFile, 'add', $AppProjectFile)", scaffoldAssignment.LaunchVariables["DotNetAddTestProjectScript"], StringComparison.Ordinal);
        Assert.Contains("Verified solution membership and ProjectReference", scaffoldAssignment.LaunchVariables["DotNetAddTestProjectScript"], StringComparison.Ordinal);
        Assert.Contains("\"mode\":\"ProductMutation\"", scaffoldAssignment.LaunchVariables["DotNetAddTestProjectSideEffectManifest"], StringComparison.Ordinal);
        Assert.Contains(@"C:\\temp\\CanDoItAll\\TetrisGame\\tests\\TetrisGame.Tests", scaffoldAssignment.LaunchVariables["DotNetAddTestProjectSideEffectManifest"], StringComparison.Ordinal);
        Assert.Contains("DotNetCreateProjectExecutionPlan", createProjectAssignment.LaunchVariables.Keys, StringComparer.Ordinal);
        Assert.Contains("DotNetCreateProjectScriptRef", createProjectAssignment.LaunchVariables.Keys, StringComparer.Ordinal);
        Assert.Equal(expectedCreateProjectScriptRef, createProjectAssignment.LaunchVariables["DotNetCreateProjectScriptRef"]);
        Assert.Contains("DotNetCreateProjectScript", createProjectAssignment.LaunchVariables.Keys, StringComparer.Ordinal);
        Assert.Contains("DotNetCreateProjectSideEffectManifest", createProjectAssignment.LaunchVariables.Keys, StringComparer.Ordinal);
        Assert.DoesNotContain("DotNetAddTestProjectScriptRef", createProjectAssignment.LaunchVariables.Keys, StringComparer.Ordinal);
        Assert.DoesNotContain("DotNetAddTestProjectScript", createProjectAssignment.LaunchVariables.Keys, StringComparer.Ordinal);
        Assert.DoesNotContain("DotNetAddTestProjectExecutionPlan", createProjectAssignment.LaunchVariables.Keys, StringComparer.Ordinal);
        Assert.DoesNotContain("DotNetAddTestProjectSideEffectManifest", createProjectAssignment.LaunchVariables.Keys, StringComparer.Ordinal);
        Assert.Contains("DotNetAddTestProjectScriptRef", addTestProjectAssignment.LaunchVariables.Keys, StringComparer.Ordinal);
        Assert.Equal(expectedAddTestProjectScriptRef, addTestProjectAssignment.LaunchVariables["DotNetAddTestProjectScriptRef"]);
        Assert.Contains("DotNetAddTestProjectScript", addTestProjectAssignment.LaunchVariables.Keys, StringComparer.Ordinal);
        Assert.Contains("DotNetAddTestProjectExecutionPlan", addTestProjectAssignment.LaunchVariables.Keys, StringComparer.Ordinal);
        Assert.Contains("DotNetAddTestProjectSideEffectManifest", addTestProjectAssignment.LaunchVariables.Keys, StringComparer.Ordinal);
        Assert.DoesNotContain("DotNetCreateProjectScriptRef", addTestProjectAssignment.LaunchVariables.Keys, StringComparer.Ordinal);
        Assert.DoesNotContain("DotNetCreateProjectScript", addTestProjectAssignment.LaunchVariables.Keys, StringComparer.Ordinal);
        Assert.DoesNotContain("DotNetCreateProjectExecutionPlan", addTestProjectAssignment.LaunchVariables.Keys, StringComparer.Ordinal);
        Assert.DoesNotContain("DotNetCreateProjectSideEffectManifest", addTestProjectAssignment.LaunchVariables.Keys, StringComparer.Ordinal);
        Assert.DoesNotContain(ProcessRuntimeLaunchVariables.ProcessStepScopedLaunchVariablePrefixesByStep, createProjectAssignment.LaunchVariables.Keys, StringComparer.Ordinal);
        Assert.Contains("dotnet.create-project", createProjectAssignment.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet.add-test-project", createProjectAssignment.Prompt, StringComparison.Ordinal);
        Assert.Contains("dotnet.add-test-project", addTestProjectAssignment.Prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet.create-project", addTestProjectAssignment.Prompt, StringComparison.Ordinal);
        Assert.False(scaffoldAssignment.LaunchVariables.ContainsKey("DotNetScaffoldContract"));
        Assert.DoesNotContain(
            "C:/temp/CanDoItAll/TetrisGame/TetrisGame.slnx",
            createProjectAssignment.LaunchVariables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths],
            StringComparison.Ordinal);
        Assert.Contains(
            "C:/temp/CanDoItAll/TetrisGame/src/TetrisGame/TetrisGame.csproj",
            createProjectAssignment.LaunchVariables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths],
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "C:/temp/CanDoItAll/TetrisGame/TetrisGame.slnx",
            addTestProjectAssignment.LaunchVariables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths],
            StringComparison.Ordinal);
        Assert.Contains(
            "C:/temp/CanDoItAll/TetrisGame/src/TetrisGame/TetrisGame.csproj",
            addTestProjectAssignment.LaunchVariables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths],
            StringComparison.Ordinal);
        Assert.Contains(
            "C:/temp/CanDoItAll/TetrisGame/tests/TetrisGame.Tests/TetrisGame.Tests.csproj",
            addTestProjectAssignment.LaunchVariables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredPaths],
            StringComparison.Ordinal);
        Assert.Contains(
            "template=sln",
            createProjectAssignment.LaunchVariables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts],
            StringComparison.Ordinal);
        Assert.Contains(
            "template=blazorwasm",
            createProjectAssignment.LaunchVariables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts],
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "workspace_dotnet_new",
            createProjectAssignment.LaunchVariables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts],
            StringComparison.Ordinal);
        Assert.Contains(
            "workspace_pwsh_run_script",
            createProjectAssignment.LaunchVariables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts],
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "workspace_dotnet_new",
            addTestProjectAssignment.LaunchVariables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts],
            StringComparison.Ordinal);
        Assert.Contains(
            "workspace_pwsh_run_script",
            addTestProjectAssignment.LaunchVariables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts],
            StringComparison.Ordinal);
        Assert.Contains(
            "workspace_dotnet_restore",
            validateFirstBuildAssignment.LaunchVariables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts],
            StringComparison.Ordinal);
        Assert.Contains(
            "workspace_dotnet_build",
            validateFirstBuildAssignment.LaunchVariables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts],
            StringComparison.Ordinal);
        Assert.Contains(
            "workspace_dotnet_test",
            validateFirstBuildAssignment.LaunchVariables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts],
            StringComparison.Ordinal);
        Assert.Contains(
            "workspace_dotnet_restore",
            revalidateFirstBuildAssignment.LaunchVariables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts],
            StringComparison.Ordinal);
        Assert.Contains(
            "workspace_dotnet_build",
            revalidateFirstBuildAssignment.LaunchVariables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts],
            StringComparison.Ordinal);
        Assert.Contains(
            "workspace_dotnet_test",
            revalidateFirstBuildAssignment.LaunchVariables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredToolReceipts],
            StringComparison.Ordinal);
        Assert.Contains(
            "src/TetrisGame/TetrisGame.csproj",
            createProjectAssignment.LaunchVariables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks],
            StringComparison.Ordinal);
        Assert.Contains(
            "tests/TetrisGame.Tests/TetrisGame.Tests.csproj",
            addTestProjectAssignment.LaunchVariables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks],
            StringComparison.Ordinal);
        Assert.Contains(
            "../../src/TetrisGame/TetrisGame.csproj",
            addTestProjectAssignment.LaunchVariables[ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks],
            StringComparison.Ordinal);
        const string expectedTestProjectFile = "C:/temp/CanDoItAll/TetrisGame/tests/TetrisGame.Tests/TetrisGame.Tests.csproj";
        const string expectedAppReferencePath = "../../src/TetrisGame/TetrisGame.csproj";
        var expectedAppReferencePaths = new[]
        {
            expectedAppReferencePath,
            @"..\..\src\TetrisGame\TetrisGame.csproj"
        };
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var scaffoldChecksByStep = Assert.IsType<Dictionary<string, ProductCompletionRequiredFileContentCheckJson[]>>(
            JsonSerializer.Deserialize<Dictionary<string, ProductCompletionRequiredFileContentCheckJson[]>>(
                scaffoldAssignment.LaunchVariables[
                    ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecksByStep],
                jsonOptions));
        var scaffoldAddTestProjectChecks = Assert.Contains("add-test-project", scaffoldChecksByStep);
        var directCreateProjectChecks = Assert.IsType<ProductCompletionRequiredFileContentCheckJson[]>(
            JsonSerializer.Deserialize<ProductCompletionRequiredFileContentCheckJson[]>(
                createProjectAssignment.LaunchVariables[
                    ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks],
                jsonOptions));
        var directAddTestProjectChecks = Assert.IsType<ProductCompletionRequiredFileContentCheckJson[]>(
            JsonSerializer.Deserialize<ProductCompletionRequiredFileContentCheckJson[]>(
                addTestProjectAssignment.LaunchVariables[
                    ProcessRuntimeLaunchVariables.ProductCompletionRequiredFileContentChecks],
                jsonOptions));
        var solutionMembershipCheck = Assert.Single(
            directCreateProjectChecks,
            check =>
                check.PathCandidates.Contains(
                    @"C:\temp\CanDoItAll\TetrisGame\TetrisGame.slnx",
                    StringComparer.Ordinal) &&
                check.PathCandidates.Contains(
                    @"C:\temp\CanDoItAll\TetrisGame\TetrisGame.sln",
                    StringComparer.Ordinal));
        Assert.Contains(
            solutionMembershipCheck.RequiredTextAnyGroups,
            group => group.Contains("src/TetrisGame/TetrisGame.csproj", StringComparer.Ordinal));

        foreach (var checks in new[] { scaffoldAddTestProjectChecks, directAddTestProjectChecks })
        {
            var testProjectCheck = Assert.Single(
                checks,
                check => check.PathCandidates.Contains(expectedTestProjectFile, StringComparer.Ordinal));
            var appReferenceGroup = Assert.Single(
                testProjectCheck.RequiredTextAnyGroups,
                group => group.Contains(expectedAppReferencePath, StringComparer.Ordinal));
            Assert.Equal(expectedAppReferencePaths, appReferenceGroup);
        }

        Assert.Contains("ProductCompletionRequiredPaths", createProjectAssignment.Prompt, StringComparison.Ordinal);
        Assert.Contains("ProductCompletionRequiredToolReceipts", createProjectAssignment.Prompt, StringComparison.Ordinal);
        Assert.Contains("ProductCompletionRequiredFileContentChecks", createProjectAssignment.Prompt, StringComparison.Ordinal);
        Assert.Contains("C:/temp/CanDoItAll/TetrisGame/TetrisGame.slnx", createProjectAssignment.Prompt, StringComparison.Ordinal);
        Assert.Contains("C:/temp/CanDoItAll/TetrisGame/src/TetrisGame/TetrisGame.csproj", createProjectAssignment.Prompt, StringComparison.Ordinal);
        Assert.Contains("ProductCompletionRequiredToolReceipts", validateFirstBuildAssignment.Prompt, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_restore", validateFirstBuildAssignment.Prompt, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_build", validateFirstBuildAssignment.Prompt, StringComparison.Ordinal);
        Assert.Contains("workspace_dotnet_test", validateFirstBuildAssignment.Prompt, StringComparison.Ordinal);
        Assert.Contains("sideEffectManifest", addTestProjectAssignment.Prompt, StringComparison.Ordinal);
        Assert.Contains("dotnet sln <solution-file> list", addTestProjectAssignment.Prompt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StartProcessNodeAsync_hr_match_resolves_software_delivery_and_runtime_command_roles()
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
        Assert.Contains(assignments, assignment => assignment.StepKey == "capture-ui-screenshots");

        var runtimeCommandDefinitionId = ProcessDefinitionCatalogProjectionService
            .CreateDefinitionId(new ProcessDefinitionCatalogItemKey("dotnet-runtime-command-writeback"))
            .Value;
        await agentService.LinkProcessDefinitionAsync(
            projectId,
            deliveryNode.Id,
            new ProjectStructureProcessDefinitionLinkInput(runtimeCommandDefinitionId),
            DefaultAgent);
        var runtimeCommandRun = await agentService.StartProcessNodeAsync(
            projectId,
            ProjectStructureProcessNodeKeys.BuildProcessDefinitionNodeKey(runtimeCommandDefinitionId),
            new ProjectStructureProcessNodeStartInput(
                RunHrMatch: true,
                Execute: false,
                IncludeLaunchPlan: true,
                RequestedBy: "integration-test"),
            DefaultAgent);
        var runtimeCommandAssignments = await assignmentStore.LoadByRunAsync(new ProcessRunId(runtimeCommandRun.RunId!.Value));
        var resolveRunCommands = Assert.Single(runtimeCommandAssignments, assignment => assignment.StepKey == "resolve-dotnet-run-commands");
        Assert.Equal("runtime-command-recorder", resolveRunCommands.RoleKey);
        Assert.Equal("delivery-manager", resolveRunCommands.RoleResourceKey);
        Assert.Equal("Runtime command recorder", resolveRunCommands.RoleDisplayName);
        Assert.Contains("Delivery Manager", resolveRunCommands.ExecutorDisplayName, StringComparison.OrdinalIgnoreCase);
        var writeRunCommandNodes = Assert.Single(runtimeCommandAssignments, assignment => assignment.StepKey == "write-run-command-nodes");
        Assert.Contains(
            writeRunCommandNodes.CapabilityScope.RequiredReceipts,
            receipt => string.Equals(receipt.ToolName, "project_structure_node_create", StringComparison.Ordinal) &&
                       receipt.Activation == ProcessRequiredToolReceiptActivation.Always);
        Assert.Contains(
            writeRunCommandNodes.CapabilityScope.RequiredReceipts,
            receipt => string.Equals(receipt.ToolName, "project_structure_read", StringComparison.Ordinal) &&
                       receipt.Activation == ProcessRequiredToolReceiptActivation.Always);
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
        var secretService = scope.ServiceProvider.GetRequiredService<SecretService>();
        var secretResult = await secretService.SaveAsync(new SecretEditorModel
        {
            Name = "Process HR matching provider key",
            Kind = SecretKind.ApiKey,
            SecretValue = "integration-test-provider-key",
            Scope = "workspace"
        });
        Assert.True(secretResult.IsSuccess);
        var providerSecretReference = $"secret:{secretResult.Value:D}";

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
            ApiKeyEnvironmentVariable = providerSecretReference,
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
            ApiKeyEnvironmentVariable = providerSecretReference,
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
    public async Task AgentService_MoveNodesToNewSubprojectAsync_rejects_duplicate_reserved_target_id_without_mutation()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var leaseService = scope.ServiceProvider.GetRequiredService<ProjectStructureLeaseService>();

        var sourceProjectId = await CreateProjectAsync(projects, "Duplicate reserved target source");
        var sourceNote = await workbench.CreateObjectAsync(
            sourceProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Source note",
                string.Empty,
                "This note must remain in the source project after the duplicate id is rejected.",
                $"project:{sourceProjectId:D}"));
        var reservedTargetProjectId = Guid.NewGuid();
        var initialTargetCreate = await projects.CreateAsync(
            reservedTargetProjectId,
            new ProjectEditorModel
            {
                Name = "Existing reserved target",
                Description = "Claims the reserved target id before the move operation.",
                Objective = "Prove that a reserved project id cannot be created twice.",
                CurrentPhase = "Execution"
            });
        Assert.True(initialTargetCreate.IsSuccess);

        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            agentService.MoveNodesToNewSubprojectAsync(
                sourceProjectId,
                reservedTargetProjectId,
                new ProjectStructureNodesToSubprojectInput(
                    "Rejected duplicate child",
                    [sourceNote.Id]),
                DefaultAgent));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("ProjectCreationRejected", exception.ErrorCode);
        var errors = Assert.IsAssignableFrom<IReadOnlyList<Error>>(exception.Details);
        Assert.Contains(errors, error => error.Code == "projects.reserved-id-conflict");
        var sourceAfter = await workbench.GetStructureAsync(sourceProjectId);
        Assert.Contains(sourceAfter.Nodes, node => node.Id == sourceNote.Id);
        var targetAfter = await workbench.GetStructureAsync(reservedTargetProjectId);
        Assert.DoesNotContain(targetAfter.Nodes, node => node.Id == sourceNote.Id);
        var hierarchy = await projects.GetHierarchyAsync(sourceProjectId);
        Assert.DoesNotContain(hierarchy.ChildProjects, child => child.Id == reservedTargetProjectId);
        Assert.Null(await leaseService.GetActiveLeaseAsync(
            ProjectStructureLeaseScopeKind.Project,
            sourceProjectId.ToString("D")));
        Assert.Null(await leaseService.GetActiveLeaseAsync(
            ProjectStructureLeaseScopeKind.Project,
            reservedTargetProjectId.ToString("D")));
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
    public async Task AgentService_MoveNodesToNewSubprojectAsync_preserves_managed_asset_content_and_hierarchy()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var sourceProjectId = await CreateProjectAsync(projects, "Managed asset transfer source");
        var group = await workbench.CreateObjectAsync(
            sourceProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Planning assets",
                string.Empty,
                "Move this group and every managed asset below it.",
                $"project:{sourceProjectId:D}",
                ObjectSubtype: "planning"));
        var specifications = new[]
        {
            new
            {
                ObjectType = ProjectObjectType.File,
                Title = "Planning brief",
                ObjectSubtype = "md",
                FileName = "planning-brief.md",
                ContentType = "text/markdown",
                Bytes = Encoding.UTF8.GetBytes("# Planning brief\n\nExact managed content.")
            },
            new
            {
                ObjectType = ProjectObjectType.File,
                Title = "Planning flow",
                ObjectSubtype = "mermaid",
                FileName = "planning-flow.mmd",
                ContentType = ProjectStructureFileInteractionPolicy.MermaidMediaType,
                Bytes = Encoding.UTF8.GetBytes("flowchart LR\n    Idea --> Delivery")
            },
            new
            {
                ObjectType = ProjectObjectType.File,
                Title = "Planning workbook",
                ObjectSubtype = "xlsx",
                FileName = "planning-budget.xlsx",
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                Bytes = new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x06, 0x00, 0x58, 0x4C, 0x53, 0x58 }
            },
            new
            {
                ObjectType = ProjectObjectType.ImageAsset,
                Title = "Planning preview",
                ObjectSubtype = "generated",
                FileName = "planning-preview.png",
                ContentType = "image/png",
                Bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x01, 0x02, 0x03 }
            }
        };
        var assets = new List<(ProjectStructureNodeSummary Node, string FileName, string ContentType, byte[] Bytes)>();
        foreach (var specification in specifications)
        {
            var node = await agentService.CreateAssetAsync(
                sourceProjectId,
                new ProjectStructureAssetCreateInput(
                    specification.ObjectType,
                    specification.Title,
                    "Managed transfer acceptance asset",
                    "The descriptor and bytes must survive a subproject transfer unchanged.",
                    CreateMediaPayload(specification.FileName, specification.ContentType, specification.Bytes),
                    group.Id,
                    specification.ObjectSubtype),
                DefaultAgent);
            assets.Add((node, specification.FileName, specification.ContentType, specification.Bytes));
        }

        var result = await agentService.MoveNodesToNewSubprojectAsync(
            sourceProjectId,
            new ProjectStructureNodesToSubprojectInput(
                "Extracted planning assets",
                [group.Id],
                IncludeDescendants: true),
            DefaultAgent);
        var expectedMovedNodeIds = assets
            .Select(asset => asset.Node.Id)
            .Append(group.Id)
            .OrderBy(nodeId => nodeId, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedMovedNodeIds, result.MovedNodeIds);
        Assert.Equal(expectedMovedNodeIds.Length, result.MovedNodeCount);
        Assert.Equal(1, result.MovedRootCount);

        var sourceSurface = await workbench.GetStructureAsync(sourceProjectId);
        Assert.DoesNotContain(sourceSurface.Nodes, node => expectedMovedNodeIds.Contains(node.Id, StringComparer.Ordinal));

        var targetSurface = await workbench.GetStructureAsync(result.TargetProjectId);
        var movedGroup = Assert.Single(targetSurface.Nodes, node => node.Id == group.Id);
        Assert.Equal($"project:{result.TargetProjectId:D}", movedGroup.ParentId);
        var systemManagedNodeIds = targetSurface.Nodes
            .Where(node => node.IsSystemManaged)
            .Select(node => node.Id)
            .ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain(result.MovedNodeIds, systemManagedNodeIds.Contains);

        foreach (var expected in assets)
        {
            var movedAssetNode = Assert.Single(targetSurface.Nodes, node => node.Id == expected.Node.Id);
            Assert.Equal(group.Id, movedAssetNode.ParentId);

            var descriptor = await agentService.GetAssetAsync(result.TargetProjectId, expected.Node.Id);
            var content = await agentService.GetAssetContentAsync(result.TargetProjectId, expected.Node.Id);
            var actualBytes = Convert.FromBase64String(content.Base64Data);

            Assert.Equal(result.TargetProjectId, descriptor.ProjectId);
            Assert.Equal(expected.FileName, descriptor.MediaOriginalFileName);
            Assert.Equal(expected.ContentType, descriptor.MediaContentType);
            Assert.Equal(expected.Bytes.Length, content.ContentLength);
            Assert.Equal(expected.Bytes, actualBytes);
            Assert.Equal(
                Convert.ToHexString(SHA256.HashData(expected.Bytes)),
                Convert.ToHexString(SHA256.HashData(actualBytes)));
        }
    }

    [Fact]
    public async Task Workbench_MoveNodesToProjectAsync_returns_only_exact_transactional_node_ids()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var sourceProjectId = await CreateProjectAsync(projects, "Exact move source");
        var targetProjectId = await CreateProjectAsync(projects, "Exact move target");
        var sourceGroup = await workbench.CreateObjectAsync(
            sourceProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Moved group",
                string.Empty,
                string.Empty,
                $"project:{sourceProjectId:D}",
                ObjectSubtype: "planning"));
        var sourceChild = await workbench.CreateObjectAsync(
            sourceProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Moved child",
                string.Empty,
                string.Empty,
                sourceGroup.Id));
        var existingTargetNode = await workbench.CreateObjectAsync(
            targetProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Existing target note",
                string.Empty,
                string.Empty,
                $"project:{targetProjectId:D}"));
        var targetEditor = await projects.GetAsync(targetProjectId);
        targetEditor.Phases.Add(new ProjectPhaseEditorModel
        {
            Name = "Existing target phase",
            Goal = "Proves projected nodes are not reported as moved.",
            Status = ProjectPhaseStatus.Active
        });
        Assert.True((await projects.SaveAsync(targetEditor)).IsSuccess);
        var targetBefore = await workbench.GetStructureAsync(targetProjectId);
        var projectedTargetNode = Assert.Single(targetBefore.Nodes, node =>
            node.IsSystemManaged &&
            node.ObjectType == ProjectObjectType.Phase &&
            node.Title == "Existing target phase");

        var result = await workbench.MoveNodesToProjectAsync(
            sourceProjectId,
            [sourceGroup.Id],
            targetProjectId,
            includeDescendants: true);
        var transfer = Assert.IsType<ProjectStructureSubprojectTransferResult>(result);
        var expectedNodeIds = new[] { sourceGroup.Id, sourceChild.Id }
            .OrderBy(nodeId => nodeId, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedNodeIds, transfer.MovedNodeIds);
        Assert.Equal(expectedNodeIds.Length, transfer.MovedNodeCount);
        Assert.DoesNotContain(existingTargetNode.Id, transfer.MovedNodeIds);
        Assert.DoesNotContain(projectedTargetNode.Id, transfer.MovedNodeIds);
    }

    [Fact]
    public async Task LeaseService_RunWithProjectMutationLeasesAsync_holds_both_projects_and_releases_internally_acquired_leases()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var leaseService = scope.ServiceProvider.GetRequiredService<ProjectStructureLeaseService>();
        var sourceProjectId = await CreateProjectAsync(projects, "Dual lease source");
        var targetProjectId = await CreateProjectAsync(projects, "Dual lease target");
        var callbackExecuted = false;

        var result = await leaseService.RunWithProjectMutationLeasesAsync(
            [
                new ProjectStructureProjectMutationLeaseRequest(targetProjectId),
                new ProjectStructureProjectMutationLeaseRequest(sourceProjectId)
            ],
            DefaultAgent,
            "integration-dual-project-mutation",
            async cancellationToken =>
            {
                var sourceLease = await leaseService.GetActiveLeaseAsync(
                    ProjectStructureLeaseScopeKind.Project,
                    sourceProjectId.ToString("D"),
                    cancellationToken);
                var targetLease = await leaseService.GetActiveLeaseAsync(
                    ProjectStructureLeaseScopeKind.Project,
                    targetProjectId.ToString("D"),
                    cancellationToken);

                Assert.NotNull(sourceLease);
                Assert.NotNull(targetLease);
                Assert.Equal(DefaultAgent.AgentId, sourceLease.AgentId);
                Assert.Equal(DefaultAgent.AgentId, targetLease.AgentId);
                callbackExecuted = true;
                return true;
            });

        Assert.True(result);
        Assert.True(callbackExecuted);
        Assert.Null(await leaseService.GetActiveLeaseAsync(
            ProjectStructureLeaseScopeKind.Project,
            sourceProjectId.ToString("D")));
        Assert.Null(await leaseService.GetActiveLeaseAsync(
            ProjectStructureLeaseScopeKind.Project,
            targetProjectId.ToString("D")));
    }

    [Fact]
    public async Task AgentService_MoveDescendantsToProjectAsync_reports_removed_boundary_links_and_preserves_internal_links()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var leaseService = scope.ServiceProvider.GetRequiredService<ProjectStructureLeaseService>();
        var sourceProjectId = await CreateProjectAsync(projects, "Boundary link source");
        var targetProjectId = await CreateProjectAsync(projects, "Boundary link target");
        var transferScope = await workbench.CreateObjectAsync(
            sourceProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Transfer scope",
                string.Empty,
                "Its descendants move to the target.",
                $"project:{sourceProjectId}",
                320,
                220,
                null,
                null,
                "implementation"));
        var movedTask = await workbench.CreateObjectAsync(
            sourceProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.WorkItem,
                "Moved task",
                string.Empty,
                "Moves with the scope descendants.",
                transferScope.Id,
                520,
                280,
                null,
                null,
                "task"));
        var movedNote = await workbench.CreateObjectAsync(
            sourceProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Moved note",
                string.Empty,
                "Moves as the task descendant.",
                movedTask.Id,
                720,
                340));
        var sourceOnlyNote = await workbench.CreateObjectAsync(
            sourceProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Source-only note",
                string.Empty,
                "Stays in the source project.",
                $"project:{sourceProjectId}",
                900,
                420));
        await workbench.LinkObjectsAsync(
            sourceProjectId,
            movedTask.Id,
            movedNote.Id,
            ProjectObjectLinkKind.DependsOn);
        await workbench.LinkObjectsAsync(
            sourceProjectId,
            movedTask.Id,
            sourceOnlyNote.Id,
            ProjectObjectLinkKind.Validates);
        var sourceBefore = await workbench.GetStructureAsync(sourceProjectId);
        var boundaryLinkBefore = Assert.Single(sourceBefore.Links, link =>
            link.SourceId == movedTask.Id &&
            link.TargetId == sourceOnlyNote.Id &&
            link.Kind == ProjectObjectLinkKind.Validates);
        var boundaryLinkId = Assert.IsType<Guid>(boundaryLinkBefore.RecordId);

        var result = await agentService.MoveDescendantsToProjectAsync(
            sourceProjectId,
            transferScope.Id,
            new ProjectStructureSubtreeTransferInput(targetProjectId),
            DefaultAgent);

        Assert.Equal(targetProjectId, result.TargetProjectId);
        Assert.Equal(2, result.MovedNodeCount);
        Assert.Equal(1, result.MovedRootCount);
        Assert.Equal(1, result.MovedLinkCount);
        var removedBoundaryLink = Assert.Single(result.RemovedBoundaryLinks);
        Assert.Equal(boundaryLinkId, removedBoundaryLink.LinkId);
        Assert.Equal(movedTask.Id, removedBoundaryLink.SourceNodeId);
        Assert.Equal(sourceOnlyNote.Id, removedBoundaryLink.TargetNodeId);
        Assert.Equal(ProjectObjectLinkKind.Validates, removedBoundaryLink.LinkKind);

        var sourceAfter = await workbench.GetStructureAsync(sourceProjectId);
        Assert.Contains(sourceAfter.Nodes, node => node.Id == transferScope.Id);
        Assert.Contains(sourceAfter.Nodes, node => node.Id == sourceOnlyNote.Id);
        Assert.DoesNotContain(sourceAfter.Nodes, node => node.Id == movedTask.Id);
        Assert.DoesNotContain(sourceAfter.Links, link => link.RecordId == boundaryLinkId);

        var targetAfter = await workbench.GetStructureAsync(targetProjectId);
        Assert.Contains(targetAfter.Nodes, node => node.Id == movedTask.Id);
        Assert.Contains(targetAfter.Nodes, node => node.Id == movedNote.Id);
        Assert.Contains(targetAfter.Links, link =>
            link.SourceId == movedTask.Id &&
            link.TargetId == movedNote.Id &&
            link.Kind == ProjectObjectLinkKind.DependsOn);
        Assert.DoesNotContain(targetAfter.Links, link => link.RecordId == boundaryLinkId);
        Assert.Null(await leaseService.GetActiveLeaseAsync(
            ProjectStructureLeaseScopeKind.Project,
            sourceProjectId.ToString("D")));
        Assert.Null(await leaseService.GetActiveLeaseAsync(
            ProjectStructureLeaseScopeKind.Project,
            targetProjectId.ToString("D")));
    }

    [Fact]
    public async Task AgentService_MoveNodesToNewSubprojectAsync_rejects_projected_nodes_before_creating_child()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var leaseService = scope.ServiceProvider.GetRequiredService<ProjectStructureLeaseService>();
        var sourceProjectId = await CreateProjectAsync(projects, "Compensated transfer source");
        var sourceEditor = await projects.GetAsync(sourceProjectId);
        sourceEditor.Phases.Add(new ProjectPhaseEditorModel
        {
            Name = "Projected phase",
            Goal = "Supplies a system-managed node that cannot be moved as an editable object.",
            Status = ProjectPhaseStatus.Active
        });
        var sourceSaveResult = await projects.SaveAsync(sourceEditor);
        Assert.True(sourceSaveResult.IsSuccess);
        var sourceSurface = await workbench.GetStructureAsync(sourceProjectId);
        var projectedPhaseNode = Assert.Single(sourceSurface.Nodes, node =>
            node.IsSystemManaged &&
            node.ObjectType == ProjectObjectType.Phase &&
            node.Title == "Projected phase");
        var reservedTargetProjectId = Guid.NewGuid();

        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            agentService.MoveNodesToNewSubprojectAsync(
                sourceProjectId,
                reservedTargetProjectId,
                new ProjectStructureNodesToSubprojectInput(
                    "Must be compensated",
                    [projectedPhaseNode.Id]),
                DefaultAgent));

        Assert.Equal("SelectedNodesNotFound", exception.ErrorCode);
        Assert.Equal(404, exception.StatusCode);
        var allProjects = await projects.ListAsync();
        Assert.Contains(allProjects, project => project.Id == sourceProjectId);
        Assert.DoesNotContain(allProjects, project => project.Id == reservedTargetProjectId);
        var hierarchy = await projects.GetHierarchyAsync(sourceProjectId);
        Assert.DoesNotContain(hierarchy.ChildProjects, project => project.Id == reservedTargetProjectId);
        var sourceAfterFailure = await workbench.GetStructureAsync(sourceProjectId);
        Assert.Contains(sourceAfterFailure.Nodes, node => node.Id == projectedPhaseNode.Id);
        Assert.Null(await leaseService.GetActiveLeaseAsync(
            ProjectStructureLeaseScopeKind.Project,
            sourceProjectId.ToString("D")));
        Assert.Null(await leaseService.GetActiveLeaseAsync(
            ProjectStructureLeaseScopeKind.Project,
            reservedTargetProjectId.ToString("D")));
    }

    [Fact]
    public async Task AgentService_MoveNodesToNewSubprojectAsync_reports_post_commit_recovery_and_retains_child()
    {
        var bridge = DispatchProxy.Create<IProjectPartyIntegrationBridge, FailingProjectPartyIntegrationBridgeProxy>();
        var bridgeProxy = (FailingProjectPartyIntegrationBridgeProxy)(object)bridge;
        bridgeProxy.Failure = new InvalidOperationException("Expected assignment reconciliation failure.");
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions
        {
            ConfigureServices = services =>
            {
                services.RemoveAll<IProjectPartyIntegrationBridge>();
                services.AddSingleton(bridge);
            }
        });
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var leaseService = scope.ServiceProvider.GetRequiredService<ProjectStructureLeaseService>();
        var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var sourceProjectId = await CreateProjectAsync(projects, "Partial commit source");
        var reservedTargetProjectId = Guid.NewGuid();
        var sourceGroup = await workbench.CreateObjectAsync(
            sourceProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Committed transfer group",
                string.Empty,
                "This group remains in the target when assignment reconciliation fails.",
                $"project:{sourceProjectId:D}",
                ObjectSubtype: "planning"));
        var sourceChild = await workbench.CreateObjectAsync(
            sourceProjectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Note,
                "Committed transfer child",
                string.Empty,
                string.Empty,
                sourceGroup.Id));

        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            agentService.MoveNodesToNewSubprojectAsync(
                sourceProjectId,
                reservedTargetProjectId,
                new ProjectStructureNodesToSubprojectInput(
                    "Retained partial-commit child",
                    [sourceGroup.Id],
                    IncludeDescendants: true),
                DefaultAgent));

        Assert.Equal(409, exception.StatusCode);
        Assert.Equal("ProjectStructureTransferPartialCommit", exception.ErrorCode);
        Assert.True(exception.IsSafeToExpose);
        Assert.False(exception.CanRetryWithCorrectedInput);
        var recovery = Assert.IsType<ProjectStructureTransferRecovery>(exception.Details);
        Assert.Equal(reservedTargetProjectId, recovery.TargetProjectId);
        Assert.NotEqual(Guid.Empty, recovery.DurableMutationId);
        Assert.Equal(ProjectStructureTransferReconciliationStatus.Failed, recovery.DurableMutationStatus);
        Assert.Equal(ProjectStructureTransferCommitState.WorkbenchCommitted, recovery.CommitState);
        Assert.Contains("Do not repeat the node move", recovery.RetryGuidance, StringComparison.Ordinal);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        Assert.True(await dbContext.Set<ProjectHierarchyLink>().AnyAsync(
            link => link.ParentProjectId == sourceProjectId && link.ChildProjectId == reservedTargetProjectId));
        var sourceAfter = await workbench.GetStructureAsync(sourceProjectId);
        Assert.DoesNotContain(sourceAfter.Nodes, node => node.Id == sourceGroup.Id || node.Id == sourceChild.Id);
        var targetAfter = await workbench.GetStructureAsync(reservedTargetProjectId);
        Assert.Equal(sourceGroup.Id, Assert.Single(targetAfter.Nodes, node => node.Id == sourceGroup.Id).Id);
        Assert.Equal(sourceGroup.Id, Assert.Single(targetAfter.Nodes, node => node.Id == sourceChild.Id).ParentId);

        var mutation = await dbContext.Set<ProjectCrossModuleMutationRecord>()
            .SingleAsync(record => record.Id == recovery.DurableMutationId);
        Assert.Equal(ProjectCrossModuleMutationStatus.Failed, mutation.Status);
        Assert.Null(await leaseService.GetActiveLeaseAsync(
            ProjectStructureLeaseScopeKind.Project,
            sourceProjectId.ToString("D")));
        Assert.Null(await leaseService.GetActiveLeaseAsync(
            ProjectStructureLeaseScopeKind.Project,
            reservedTargetProjectId.ToString("D")));
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

        var originalReadback = await agentService.GetAssetAsync(projectId, original.Id);
        var revisionReadback = await agentService.GetAssetAsync(projectId, revision.NodeId);

        Assert.Null(originalReadback.RevisionParentNodeId);
        Assert.Equal(original.Id, revisionReadback.RevisionParentNodeId);

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
        Assert.True(exception.IsSafeToExpose);
        Assert.True(exception.CanRetryWithCorrectedInput);
    }

    [Fact]
    public async Task AgentService_CreateAssetAsync_rejects_public_redirect_to_loopback_without_graph_mutation()
    {
        var requestCount = 0;
        var handler = new DelegateHttpMessageHandler(request =>
        {
            Interlocked.Increment(ref requestCount);
            Assert.Equal("https://assets.example.test/start.pdf", request.RequestUri?.ToString());
            return new HttpResponseMessage(HttpStatusCode.Redirect)
            {
                Headers =
                {
                    Location = new Uri("http://127.0.0.1/internal.pdf")
                }
            };
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

        var projectId = await CreateProjectAsync(projects, "Blocked redirected asset");
        var before = await workbench.GetStructureAsync(projectId);

        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            agentService.CreateAssetAsync(
                projectId,
                new ProjectStructureAssetCreateInput(
                    ObjectType: ProjectObjectType.File,
                    Title: "Redirected internal asset",
                    Subtitle: "Blocked",
                    Notes: "A public URL must not redirect to a loopback service.",
                    Media: null,
                    ParentNodeKey: $"project:{projectId}",
                    ObjectSubtype: "pdf",
                    SourceFileName: "internal.pdf",
                    SourceContentType: "application/pdf",
                    SourceUrl: "https://assets.example.test/start.pdf"),
                DefaultAgent));

        var after = await workbench.GetStructureAsync(projectId);
        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("SourceUrlNotAllowed", exception.ErrorCode);
        Assert.True(exception.IsSafeToExpose);
        Assert.True(exception.CanRetryWithCorrectedInput);
        Assert.Equal(1, requestCount);
        AssertStructureIdentityUnchanged(before, after);
    }

    [Fact]
    public async Task AgentService_CreateAssetAsync_rejects_excessive_redirects_without_graph_mutation()
    {
        var requestCount = 0;
        var handler = new DelegateHttpMessageHandler(_ =>
        {
            var sequence = Interlocked.Increment(ref requestCount);
            return new HttpResponseMessage(HttpStatusCode.Redirect)
            {
                Headers =
                {
                    Location = new Uri($"https://assets.example.test/redirect/{sequence}")
                }
            };
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

        var projectId = await CreateProjectAsync(projects, "Redirect limit asset");
        var before = await workbench.GetStructureAsync(projectId);

        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            agentService.CreateAssetAsync(
                projectId,
                new ProjectStructureAssetCreateInput(
                    ObjectType: ProjectObjectType.File,
                    Title: "Redirect loop asset",
                    Subtitle: "Blocked",
                    Notes: "The download must stop at the bounded redirect limit.",
                    Media: null,
                    ParentNodeKey: $"project:{projectId}",
                    ObjectSubtype: "pdf",
                    SourceFileName: "redirect-loop.pdf",
                    SourceContentType: "application/pdf",
                    SourceUrl: "https://assets.example.test/redirect/0"),
                DefaultAgent));

        var after = await workbench.GetStructureAsync(projectId);
        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("SourceUrlNotAllowed", exception.ErrorCode);
        Assert.True(exception.IsSafeToExpose);
        Assert.True(exception.CanRetryWithCorrectedInput);
        Assert.Equal(ProjectStructureExternalAssetSourcePolicy.MaximumRedirects + 1, requestCount);
        AssertStructureIdentityUnchanged(before, after);
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

    [Fact]
    public async Task AgentService_UpdateNodeMetadataAsync_rejects_an_unverified_dotnet_target_without_mutating_the_node()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var workspaceRoot = scope.ServiceProvider.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();

        var validProjectDirectory = Path.Combine(workspaceRoot, "runtime-validation", "Calculator");
        Directory.CreateDirectory(validProjectDirectory);
        var validProjectPath = Path.Combine(validProjectDirectory, "Calculator.csproj");
        await File.WriteAllTextAsync(validProjectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var projectId = await CreateProjectAsync(projects, "Runtime metadata validation");
        var created = await agentService.CreateNodeAsync(
            projectId,
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.Environment,
                "Start Calculator",
                "dotnet watch",
                "Runs the Calculator application.",
                $"project:{projectId}",
                ObjectSubtype: "dotnet-watch",
                MetadataJson: CreateDotNetRuntimeMetadata(validProjectPath)),
            DefaultAgent);

        var invalidSolutionRoot = Path.Combine(workspaceRoot, "runtime-validation", "solution-root");
        var nestedProjectDirectory = Path.Combine(invalidSolutionRoot, "Calculator");
        Directory.CreateDirectory(nestedProjectDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(nestedProjectDirectory, "Calculator.csproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        await File.WriteAllTextAsync(Path.Combine(invalidSolutionRoot, "Calculator.slnx"), "<Solution />");

        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            agentService.UpdateNodeMetadataAsync(
                projectId,
                created.Id,
                new ProjectStructureNodeMetadataInput(CreateDotNetRuntimeMetadata(invalidSolutionRoot)),
                DefaultAgent));

        Assert.Equal("InvalidRuntimeMetadata", exception.ErrorCode);
        Assert.True(exception.IsSafeToExpose);
        Assert.True(exception.CanRetryWithCorrectedInput);
        Assert.Contains("exact application project file", exception.Message, StringComparison.OrdinalIgnoreCase);

        var surface = await workbench.GetStructureAsync(projectId);
        var persistedNode = Assert.Single(surface.Nodes, node => node.Id == created.Id);
        var persistedMetadata = ProjectObjectMetadataSerializer.Parse(persistedNode.MetadataJson);
        Assert.Equal(validProjectPath, persistedMetadata.Environment!.ProjectPath);
    }

    [Fact]
    public async Task AgentService_CreateNodeAsync_persists_the_exact_dotnet_project_and_its_directory()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var workspaceRoot = scope.ServiceProvider.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();

        var projectDirectory = Path.Combine(workspaceRoot, "runtime-canonicalization", "Calculator");
        Directory.CreateDirectory(projectDirectory);
        var projectPath = Path.Combine(projectDirectory, "Calculator.csproj");
        await File.WriteAllTextAsync(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var requestedMetadata = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            CreateDotNetRuntimeMetadata(projectDirectory))!;
        requestedMetadata["runtimeAuditExtension"] = JsonSerializer.SerializeToElement(
            new Dictionary<string, string>
            {
                ["correlationId"] = "runtime-repair-42"
            });

        var projectId = await CreateProjectAsync(projects, "Canonical runtime metadata");
        var created = await agentService.CreateNodeAsync(
            projectId,
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.Environment,
                "Start Calculator",
                "dotnet watch",
                "Runs the exact Calculator project.",
                $"project:{projectId}",
                ObjectSubtype: "dotnet-watch",
                MetadataJson: JsonSerializer.Serialize(requestedMetadata)),
            DefaultAgent);

        var surface = await workbench.GetStructureAsync(projectId);
        var persistedNode = Assert.Single(surface.Nodes, node => node.Id == created.Id);
        var persistedMetadata = ProjectObjectMetadataSerializer.Parse(persistedNode.MetadataJson);
        Assert.Equal(projectPath, persistedMetadata.Environment!.ProjectPath);
        Assert.Equal(projectDirectory, persistedMetadata.Environment.WorkingDirectory);
        using var persistedJson = JsonDocument.Parse(persistedNode.MetadataJson);
        Assert.True(
            persistedJson.RootElement.TryGetProperty("runtimeAuditExtension", out var persistedExtension),
            persistedNode.MetadataJson);
        Assert.True(
            persistedExtension.TryGetProperty("correlationId", out var persistedCorrelationId),
            persistedNode.MetadataJson);
        Assert.Equal(
            "runtime-repair-42",
            persistedCorrelationId.GetString());
    }

    [Fact]
    public async Task AgentService_CreateNodeAsync_rejects_an_unaudited_external_runtime_target()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var externalRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.AgentRuntime.Unaudited.{Guid.NewGuid():N}");
        Directory.CreateDirectory(externalRoot);
        var projectPath = Path.Combine(externalRoot, "Calculator.csproj");
        await File.WriteAllTextAsync(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        try
        {
            var projectId = await CreateProjectAsync(projects, "Unaudited external runtime authority");
            var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
                agentService.CreateNodeAsync(
                    projectId,
                    new ProjectStructureNodeCreateInput(
                        ProjectObjectType.Environment,
                        "Rejected external Calculator",
                        "dotnet watch",
                        "An agent request without audited path authority must fail closed.",
                        $"project:{projectId:D}",
                        ObjectSubtype: "dotnet-watch",
                        MetadataJson: CreateDotNetRuntimeMetadata(projectPath)),
                    DefaultAgent));

            Assert.Equal("InvalidRuntimeMetadata", exception.ErrorCode);
            Assert.Contains("not authorized for this agent execution", exception.Message, StringComparison.Ordinal);
            var surface = await workbench.GetStructureAsync(projectId);
            Assert.DoesNotContain(surface.Nodes, node => node.Title == "Rejected external Calculator");
        }
        finally
        {
            Directory.Delete(externalRoot, recursive: true);
        }
    }

    [Fact]
    public async Task AgentService_CreateNodeAsync_accepts_an_external_runtime_target_selected_by_the_audited_execution()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var externalRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.AgentRuntime.Audited.{Guid.NewGuid():N}");
        Directory.CreateDirectory(externalRoot);
        var projectPath = Path.Combine(externalRoot, "Calculator.csproj");
        await File.WriteAllTextAsync(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        try
        {
            var projectId = await CreateProjectAsync(projects, "Audited external runtime authority");
            var externalAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(externalRoot);
            Assert.False(string.IsNullOrWhiteSpace(externalAlias));
            using var auditScope = WorkspaceExecutionAuditContext.BeginScope(
                CreateAuditedExecutionRun([externalAlias!]));

            var created = await agentService.CreateNodeAsync(
                projectId,
                new ProjectStructureNodeCreateInput(
                    ProjectObjectType.Environment,
                    "Authorized external Calculator",
                    "dotnet watch",
                    "The selected external target is readable in this audited execution.",
                    $"project:{projectId:D}",
                    ObjectSubtype: "dotnet-watch",
                    MetadataJson: CreateDotNetRuntimeMetadata(projectPath)),
                DefaultAgent);

            var surface = await workbench.GetStructureAsync(projectId);
            var persistedNode = Assert.Single(surface.Nodes, node => node.Id == created.Id);
            var persistedMetadata = ProjectObjectMetadataSerializer.Parse(persistedNode.MetadataJson);
            Assert.Equal(projectPath, persistedMetadata.Environment!.ProjectPath);
            Assert.Equal(externalRoot, persistedMetadata.Environment.WorkingDirectory);
        }
        finally
        {
            Directory.Delete(externalRoot, recursive: true);
        }
    }

    [Fact]
    public async Task AgentService_GetStructureAsync_gates_external_runtime_capabilities_by_execution_authority()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var externalRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.AgentRuntime.ReadAuthority.{Guid.NewGuid():N}");
        Directory.CreateDirectory(externalRoot);
        var projectPath = Path.Combine(externalRoot, "Calculator.csproj");
        await File.WriteAllTextAsync(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        try
        {
            var projectId = await CreateProjectAsync(projects, "External runtime read authority");
            var created = await workbench.CreateObjectAsync(
                projectId,
                new ProjectObjectCreateRequest(
                    ProjectObjectType.Environment,
                    "Operator-selected external Calculator",
                    "dotnet watch",
                    "The operator selected this external runtime before the agent read.",
                    $"project:{projectId:D}",
                    ObjectSubtype: "dotnet-watch",
                    MetadataJson: CreateDotNetRuntimeMetadata(projectPath)));

            var unaudited = await agentService.GetStructureAsync(
                projectId,
                new ProjectStructureReadRequest(IncludeMetadata: true));
            var unauditedNode = Assert.Single(unaudited.Nodes, node => node.Id == created.Id);
            Assert.Equal(
                projectPath,
                ProjectObjectMetadataSerializer.Parse(unauditedNode.MetadataJson).Environment!.ProjectPath);
            Assert.NotNull(unauditedNode.ActionCapabilities);
            Assert.False(unauditedNode.ActionCapabilities!.CanRunNormally);
            Assert.False(unauditedNode.ActionCapabilities.CanRunAsAdministrator);
            Assert.Contains(
                unauditedNode.ActionCapabilities.Guidance,
                guidance => guidance.Contains(
                    "not authorized for this agent execution",
                    StringComparison.Ordinal));

            var externalAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(externalRoot);
            Assert.False(string.IsNullOrWhiteSpace(externalAlias));
            using var auditScope = WorkspaceExecutionAuditContext.BeginScope(
                CreateAuditedExecutionRun([externalAlias!]));
            var audited = await agentService.GetStructureAsync(
                projectId,
                new ProjectStructureReadRequest(IncludeMetadata: true));
            var auditedNode = Assert.Single(audited.Nodes, node => node.Id == created.Id);
            Assert.NotNull(auditedNode.ActionCapabilities);
            Assert.True(auditedNode.ActionCapabilities!.CanRunNormally);
            Assert.True(auditedNode.ActionCapabilities.CanRunAsAdministrator);
            Assert.Equal(externalRoot, auditedNode.ActionCapabilities.RuntimeWorkingDirectory);
            Assert.Contains(projectPath, auditedNode.ActionCapabilities.RuntimeDisplayCommand, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(externalRoot, recursive: true);
        }
    }

    [Fact]
    public async Task AgentService_CreateNodeAsync_rejects_a_subtype_and_environment_kind_mismatch()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();

        var projectId = await CreateProjectAsync(projects, "Runtime kind consistency");
        var mismatchedMetadata = ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
        {
            Environment = new ProjectEnvironmentMetadata
            {
                EnvironmentKind = ProjectEnvironmentKind.DotNetRuntime,
                ProjectPath = "Calculator.csproj"
            }
        });

        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            agentService.CreateNodeAsync(
                projectId,
                new ProjectStructureNodeCreateInput(
                    ProjectObjectType.Environment,
                    "Mismatched runtime",
                    "dotnet watch",
                    "Kind and subtype must agree.",
                    $"project:{projectId}",
                    ObjectSubtype: "dotnet-watch",
                    MetadataJson: mismatchedMetadata),
                DefaultAgent));

        Assert.Equal("InvalidRuntimeMetadata", exception.ErrorCode);
        Assert.True(exception.IsSafeToExpose);
        Assert.True(exception.CanRetryWithCorrectedInput);
        Assert.Contains("does not match objectSubtype 'dotnet-watch'", exception.Message, StringComparison.Ordinal);

        var surface = await workbench.GetStructureAsync(projectId);
        Assert.DoesNotContain(surface.Nodes, node => node.Title == "Mismatched runtime");
    }

    [Fact]
    public async Task AgentService_CreateNodeAsync_rejects_a_docker_runtime_without_a_command()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var workspaceRoot = scope.ServiceProvider.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();
        var metadataJson = ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
        {
            Infrastructure = new ProjectInfrastructureMetadata
            {
                InfrastructureKind = ProjectInfrastructureKind.DockerMode,
                WorkingDirectory = workspaceRoot
            }
        });
        var projectId = await CreateProjectAsync(projects, "Docker runtime validation");

        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            agentService.CreateNodeAsync(
                projectId,
                new ProjectStructureNodeCreateInput(
                    ProjectObjectType.Infrastructure,
                    "Invalid Docker runtime",
                    "Missing command",
                    "Docker runtime metadata must be launch-ready.",
                    $"project:{projectId}",
                    ObjectSubtype: "docker-mode",
                    MetadataJson: metadataJson),
                DefaultAgent));

        Assert.Equal("InvalidRuntimeMetadata", exception.ErrorCode);
        Assert.True(exception.IsSafeToExpose);
        Assert.True(exception.CanRetryWithCorrectedInput);
        Assert.Contains("runtime command", exception.Message, StringComparison.OrdinalIgnoreCase);

        var surface = await workbench.GetStructureAsync(projectId);
        Assert.DoesNotContain(surface.Nodes, node => node.Title == "Invalid Docker runtime");
    }

    [Fact]
    public async Task AgentService_UpdateNodeMetadataAsync_rejects_dotnet_watch_as_an_opaque_script_repair()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();

        var projectId = await CreateProjectAsync(projects, "Opaque runtime repair validation");
        var originalMetadata = CreateScriptRuntimeMetadata("Write-Output", "ready");
        var created = await agentService.CreateNodeAsync(
            projectId,
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.Script,
                "Start Calculator",
                "PowerShell",
                "A script node that must be reclassified for dotnet watch.",
                $"project:{projectId}",
                ObjectSubtype: "powershell",
                MetadataJson: originalMetadata),
            DefaultAgent);

        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            agentService.UpdateNodeMetadataAsync(
                projectId,
                created.Id,
                new ProjectStructureNodeMetadataInput(
                    CreateScriptRuntimeMetadata(
                        "dotnet watch",
                        "--project C:\\unverified\\calculator-e2e-test run")),
                DefaultAgent));

        Assert.Equal("InvalidRuntimeMetadata", exception.ErrorCode);
        Assert.True(exception.IsSafeToExpose);
        Assert.True(exception.CanRetryWithCorrectedInput);
        Assert.Contains("typed Environment node", exception.Message, StringComparison.Ordinal);
        Assert.Contains("project_structure_node_update", exception.Message, StringComparison.Ordinal);

        var surface = await workbench.GetStructureAsync(projectId);
        var persistedNode = Assert.Single(surface.Nodes, node => node.Id == created.Id);
        var persistedMetadata = ProjectObjectMetadataSerializer.Parse(persistedNode.MetadataJson);
        Assert.Equal("Write-Output", persistedMetadata.Script!.Command);
        Assert.Equal("ready", persistedMetadata.Script.Arguments);
    }

    [Fact]
    public async Task AgentService_UpdateNodeAsync_atomically_reclassifies_a_script_to_a_verified_dotnet_watch_environment()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var workspaceRoot = scope.ServiceProvider.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();

        var projectDirectory = Path.Combine(workspaceRoot, "runtime-reclassification", "Calculator");
        Directory.CreateDirectory(projectDirectory);
        var projectPath = Path.Combine(projectDirectory, "Calculator.csproj");
        await File.WriteAllTextAsync(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var projectId = await CreateProjectAsync(projects, "Runtime reclassification");
        var created = await agentService.CreateNodeAsync(
            projectId,
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.Script,
                "Start Calculator",
                "PowerShell",
                "Legacy runtime script.",
                $"project:{projectId}",
                ObjectSubtype: "powershell",
                MetadataJson: CreateScriptRuntimeMetadata("Write-Output", "legacy")),
            DefaultAgent);
        var startUtc = new DateTimeOffset(2026, 8, 1, 18, 0, 0, TimeSpan.Zero);

        var updated = await agentService.UpdateNodeAsync(
            projectId,
            created.Id,
            new ProjectStructureNodeEditInput(
                "Start Calculator",
                "dotnet watch",
                "Runs the verified Calculator application project.",
                ProjectObjectType.Environment,
                "dotnet-watch",
                StartUtc: startUtc,
                MetadataJson: CreateDotNetRuntimeMetadata(projectPath),
                DurationSeconds: 90),
            DefaultAgent);

        Assert.Equal(ProjectObjectType.Environment, updated.ObjectType);
        Assert.Equal("dotnet-watch", updated.ObjectSubtype);
        Assert.Equal(startUtc, updated.StartUtc);
        Assert.Equal(startUtc.AddSeconds(90), updated.EndUtc);
        Assert.Equal(90, updated.DurationSeconds);

        var surface = await workbench.GetStructureAsync(projectId);
        var persistedNode = Assert.Single(surface.Nodes, node => node.Id == created.Id);
        var persistedMetadata = ProjectObjectMetadataSerializer.Parse(persistedNode.MetadataJson);
        Assert.Equal(projectPath, persistedMetadata.Environment!.ProjectPath);
        Assert.Null(persistedMetadata.Script);
        Assert.Equal(startUtc, persistedNode.StartUtc);
        Assert.Equal(startUtc.AddSeconds(90), persistedNode.EndUtc);
        Assert.Equal(90, persistedNode.DurationSeconds);
    }

    [Fact]
    public async Task ProjectWorkbenchService_CreateObjectAsync_rejects_wrapped_dotnet_watch_without_persisting()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projects, "Direct runtime create validation");

        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            workbench.CreateObjectAsync(
                projectId,
                new ProjectObjectCreateRequest(
                    ProjectObjectType.Script,
                    "Invalid direct runtime",
                    "PowerShell",
                    "The direct workbench path must enforce the runtime boundary.",
                    $"project:{projectId:D}",
                    ObjectSubtype: "powershell",
                    MetadataJson: CreateScriptRuntimeMetadata(
                        "pwsh",
                        "-NoProfile -Command \"dotnet watch --project Calculator.csproj run\""))));

        Assert.Equal("InvalidRuntimeMetadata", exception.ErrorCode);
        Assert.Contains("typed Environment node", exception.Message, StringComparison.Ordinal);

        var surface = await workbench.GetStructureAsync(projectId);
        Assert.DoesNotContain(surface.Nodes, node => node.Title == "Invalid direct runtime");
    }

    [Fact]
    public async Task ProjectWorkbenchService_UpdateObjectAsync_rejects_wrapped_dotnet_watch_without_mutating_metadata()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projects, "Direct runtime update validation");
        var originalMetadata = CreateScriptRuntimeMetadata("Write-Output", "ready");
        var created = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.Script,
                "Valid direct runtime",
                "PowerShell",
                "A direct workbench runtime node.",
                $"project:{projectId:D}",
                ObjectSubtype: "powershell",
                MetadataJson: originalMetadata));

        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            workbench.UpdateObjectAsync(
                projectId,
                created.Id,
                new ProjectObjectEditRequest(
                    created.Title,
                    created.Subtitle,
                    created.Notes ?? string.Empty,
                    created.StartUtc,
                    created.EndUtc,
                    CreateScriptRuntimeMetadata(
                        "pwsh",
                        "-NoProfile -Command \"dotnet watch --project Calculator.csproj run\""))));

        Assert.Equal("InvalidRuntimeMetadata", exception.ErrorCode);
        Assert.Contains("typed Environment node", exception.Message, StringComparison.Ordinal);

        var surface = await workbench.GetStructureAsync(projectId);
        var persistedNode = Assert.Single(surface.Nodes, node => node.Id == created.Id);
        var persistedMetadata = ProjectObjectMetadataSerializer.Parse(persistedNode.MetadataJson);
        Assert.Equal("Write-Output", persistedMetadata.Script!.Command);
        Assert.Equal("ready", persistedMetadata.Script.Arguments);
    }

    [Fact]
    public async Task AgentService_ProjectBlock_mutations_reject_unaudited_external_roots_without_mutating()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var agentService = scope.ServiceProvider.GetRequiredService<ProjectStructureAgentService>();
        var workspaceRoot = scope.ServiceProvider.GetRequiredService<IWorkspacePathResolver>().ResolveWorkspaceRoot();
        var projectId = await CreateProjectAsync(projects, "Agent ProjectBlock root authority");
        var externalMetadata = CreateProjectBlockMetadata(@"C:\operator\private\unselected-project");

        var createException = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            agentService.CreateNodeAsync(
                projectId,
                new ProjectStructureNodeCreateInput(
                    ProjectObjectType.ProjectBlock,
                    "Rejected external root",
                    "Implementation",
                    "An unaudited agent request cannot mint external root authority.",
                    $"project:{projectId:D}",
                    ObjectSubtype: "implementation",
                    MetadataJson: externalMetadata),
                DefaultAgent));

        Assert.Equal(ProjectStructureAgentRootAuthorityWriteGuard.FailureCode, createException.ErrorCode);

        var managedMetadata = CreateProjectBlockMetadata(
            Path.Combine(workspaceRoot, "agent-root-authority", "managed-output"));
        var created = await agentService.CreateNodeAsync(
            projectId,
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.ProjectBlock,
                "Managed root",
                "Implementation",
                "This root stays inside the managed workspace.",
                $"project:{projectId:D}",
                ObjectSubtype: "implementation",
                MetadataJson: managedMetadata),
            DefaultAgent);

        var updateException = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            agentService.UpdateNodeAsync(
                projectId,
                created.Id,
                new ProjectStructureNodeEditInput(
                    created.Title,
                    created.Subtitle,
                    created.Notes ?? string.Empty,
                    ProjectObjectType.ProjectBlock,
                    created.ObjectSubtype,
                    created.StartUtc,
                    created.EndUtc,
                    externalMetadata),
                DefaultAgent));
        Assert.Equal(ProjectStructureAgentRootAuthorityWriteGuard.FailureCode, updateException.ErrorCode);

        var metadataException = await Assert.ThrowsAsync<ProjectStructureAgentException>(() =>
            agentService.UpdateNodeMetadataAsync(
                projectId,
                created.Id,
                new ProjectStructureNodeMetadataInput(externalMetadata),
                DefaultAgent));
        Assert.Equal(ProjectStructureAgentRootAuthorityWriteGuard.FailureCode, metadataException.ErrorCode);

        var surface = await workbench.GetStructureAsync(projectId);
        Assert.DoesNotContain(surface.Nodes, node => node.Title == "Rejected external root");
        var persistedNode = Assert.Single(surface.Nodes, node => node.Id == created.Id);
        var persistedMetadata = ProjectObjectMetadataSerializer.Parse(persistedNode.MetadataJson);
        Assert.Equal(
            Path.Combine(workspaceRoot, "agent-root-authority", "managed-output"),
            persistedMetadata.ProjectBlock!.OutputRoot);
    }

    [Fact]
    public async Task ProjectWorkbenchService_CreateObjectAsync_allows_an_operator_selected_external_project_block_root()
    {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var projectId = await CreateProjectAsync(projects, "Operator ProjectBlock root authority");
        const string externalRoot = @"C:\operator\chosen\external-project";

        var created = await workbench.CreateObjectAsync(
            projectId,
            new ProjectObjectCreateRequest(
                ProjectObjectType.ProjectBlock,
                "Operator-selected root",
                "Implementation",
                "Operator UI writes remain an explicit authority-changing path.",
                $"project:{projectId:D}",
                ObjectSubtype: "implementation",
                MetadataJson: CreateProjectBlockMetadata(externalRoot)));

        var metadata = ProjectObjectMetadataSerializer.Parse(created.MetadataJson);
        Assert.Equal(externalRoot, metadata.ProjectBlock!.OutputRoot);
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

    private static string CreateDotNetRuntimeMetadata(string projectPath)
        => ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
        {
            Environment = new ProjectEnvironmentMetadata
            {
                EnvironmentKind = ProjectEnvironmentKind.DotNetWatch,
                ProjectPath = projectPath,
                WorkingDirectory = Path.GetDirectoryName(projectPath) ?? string.Empty
            }
        });

    private static string CreateScriptRuntimeMetadata(string command, string arguments)
        => ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
        {
            Script = new ProjectScriptMetadata
            {
                ScriptKind = ProjectScriptKind.PowerShell,
                Command = command,
                Arguments = arguments
            }
        });

    private static string CreateProjectBlockMetadata(string outputRoot)
        => ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
        {
            ProjectBlock = new ProjectBlockMetadata
            {
                OutputRoot = outputRoot
            }
        });

    private static ExecutionRunRecord CreateAuditedExecutionRun(
        IReadOnlyList<string> readOnlyExternalTargetAliases)
    {
        var now = DateTimeOffset.UtcNow;
        var metadataJson = JsonSerializer.Serialize(
            new Dictionary<string, IReadOnlyList<string>>
            {
                [ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey] =
                    readOnlyExternalTargetAliases
            });
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            ChatSessionId: null,
            Title: "Project-structure runtime gateway audit",
            SourceKind: "test",
            SourceId: "project-structure-runtime-gateway",
            CorrelationId: Guid.NewGuid().ToString("D"),
            CausationId: string.Empty,
            RequestedBy: "integration-test",
            RequestedByKind: "system",
            MetadataJson: metadataJson,
            InputSummary: string.Empty,
            ResultSummary: string.Empty,
            ProviderName: "test",
            Model: "test",
            State: ExecutionState.Running,
            Outcome: null,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            StartedAtUtc: now,
            CompletedAtUtc: null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: []);
    }

    private static async Task<string> CreatePersistedAssetReferencingPathAsync(
        IDbContextFactory<AppDbContext> dbContextFactory,
        Guid projectId,
        Guid runId,
        string relativePath,
        long contentLength)
    {
        string nodeKey = $"image:{Guid.NewGuid():N}";
        var node = new ProjectObjectRecord
        {
            ProjectId = projectId,
            NodeKey = nodeKey,
            ObjectType = ProjectObjectType.ImageAsset,
            ObjectSubtype = "screenshot",
            Title = "Ordinary persisted screenshot",
            MetadataJson = "{}",
            IsSystemManaged = false
        };
        var reference = StorageJson.CreateLegacyManagedFileReference(
            relativePath,
            "image/png",
            "desktop.png",
            contentLength);

        await using AppDbContext dbContext = await dbContextFactory.CreateDbContextAsync();
        dbContext.Set<ProjectObjectRecord>().Add(node);
        dbContext.Set<ProjectNodeBindingRecord>().Add(new ProjectNodeBindingRecord
        {
            ProjectObjectId = node.Id,
            ExternalArtifactKind = ProjectStructureProcessNodeKeys.ProcessRunScreenshotArtifactKind,
            ExternalArtifactId = runId,
            MediaRelativePath = relativePath,
            MediaContentType = "image/png",
            MediaOriginalFileName = "desktop.png",
            StorageObjectReferenceJson = StorageJson.SerializeReference(reference)
        });
        await dbContext.SaveChangesAsync();
        return nodeKey;
    }

    private static async Task ReplacePersistedAssignmentLaunchVariablesAsync(
        IServiceProvider serviceProvider,
        ProcessRuntimeStepAssignment assignment)
    {
        var dbContext = serviceProvider.GetRequiredService<ProcessPersistenceDbContext>();
        var entity = await dbContext.RuntimeStepAssignments.SingleAsync(candidate =>
            candidate.RunId == assignment.RunId.Value &&
            candidate.StepInstanceId == assignment.StepInstanceId.Value);
        var normalized = assignment.LaunchVariables
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .OrderBy(item => item.Key, StringComparer.Ordinal)
            .ToDictionary(
                item => item.Key.Trim(),
                item => item.Value?.Trim() ?? string.Empty,
                StringComparer.Ordinal);
        entity.LaunchVariablesJson = JsonSerializer.Serialize(normalized);
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
    }

    private static async Task EnsureParentStepRunningAsync(
        IServiceProvider serviceProvider,
        ProcessRunId runId,
        ProcessStepInstanceId stepInstanceId)
    {
        var stateStore = serviceProvider.GetRequiredService<IProcessRuntimeStateStore>();
        var planStore = serviceProvider.GetRequiredService<IProcessInstancePlanStore>();
        var unitOfWork = serviceProvider.GetRequiredService<IProcessRuntimeUnitOfWork>();
        var state = Assert.IsType<ProcessRuntimeStateSnapshot>(
            await stateStore.LoadAsync(runId));

        var nowUtc = DateTimeOffset.UtcNow;
        Assert.Equal(ProcessRuntimeStatus.Active, state.Status);
        var parentStep = Assert.Single(
            state.Steps,
            step => step.StepInstanceId == stepInstanceId);
        if (parentStep is
            {
                Status: ProcessRuntimeStepStatus.Running,
                ActiveClaimToken: { } activeClaimToken
            })
        {
            var activeClaim = Assert.Single(
                state.Claims,
                claim => claim.ClaimToken == activeClaimToken);
            Assert.True(
                activeClaim.Status is
                    DispatchClaimStatus.Claimed or
                    DispatchClaimStatus.LeaseRenewed or
                    DispatchClaimStatus.Reclaimed);
            Assert.True(activeClaim.ExpiresAtUtc > nowUtc);
            return;
        }

        Assert.True(parentStep.IsExecutable);
        Assert.Equal(ProcessRuntimeStepStatus.Pending, parentStep.Status);
        Assert.Null(parentStep.ActiveClaimToken);

        var actor = new ProcessEventActor(
            ProcessEventActorKind.System,
            new ProcessActorId("project-structure-integration-test"));
        var correlationId = new ProcessCorrelationId(Guid.NewGuid().ToString("N"));
        var readyState = state with
        {
            Steps = state.Steps
                .Select(step => step.StepInstanceId == stepInstanceId
                    ? step with { Status = ProcessRuntimeStepStatus.Ready }
                    : step)
                .ToArray(),
            UpdatedAtUtc = nowUtc
        };
        var stepReadyEvent = new ProcessRuntimeEventEnvelope(
            RuntimeEventId.New(),
            state.RootRunId,
            state.RunId,
            correlationId,
            null,
            actor,
            ProcessContractVersions.RuntimeEventEnvelopeV1,
            ProcessEventSensitivity.Normal,
            nowUtc,
            ProcessRuntimeEventTypes.StepReady,
            stepInstanceId.ToString());
        var readyMutation = new ProcessRuntimeMutation(
            ProcessRuntimeTransitionOutcome.Applied,
            readyState,
            [stepReadyEvent],
            [
                new ProcessOutboxMessage(
                    RuntimeOutboxMessageId.New(),
                    stepReadyEvent.EventId,
                    ProcessOutboxSubscriberKind.RuntimeProjection,
                    stepReadyEvent.PayloadHash)
            ],
            [],
            []);
        var ready = await unitOfWork.CommitAsync(
            new ProcessRuntimeCommitRequest(
                RuntimeCommandId.New(),
                state,
                readyMutation));
        Assert.Equal(ProcessRuntimeTransitionOutcome.Applied, ready.Outcome);
        state = Assert.IsType<ProcessRuntimeStateSnapshot>(
            await stateStore.LoadAsync(runId));
        nowUtc = nowUtc.AddMilliseconds(1);

        var plan = await planStore.LoadAsync(state.PlanId);
        Assert.NotNull(plan);
        var workItem = Assert.Single(
            new ProcessRuntimeScheduler().CalculateReadyWork(state, plan!, nowUtc),
            item => item.StepInstanceId == stepInstanceId);
        var claimToken = DispatchClaimToken.New();
        var ownerId = new DispatcherOwnerId("project-structure-integration-test");
        var engine = new ProcessRuntimeEngine(unitOfWork);
        var claim = await engine.CreateClaimAsync(
            state,
            new RuntimeCommandContext(
                RuntimeCommandId.New(),
                actor,
                correlationId,
                nowUtc),
            new CreateDispatchClaimCommand(
                workItem,
                ownerId,
                claimToken,
                nowUtc.AddMinutes(30)));
        Assert.Equal(ProcessRuntimeTransitionOutcome.Applied, claim.Outcome);

        var claimedState = Assert.IsType<ProcessRuntimeStateSnapshot>(
            await stateStore.LoadAsync(runId));
        var running = await engine.MarkClaimRunningAsync(
            claimedState,
            new RuntimeCommandContext(
                RuntimeCommandId.New(),
                actor,
                correlationId,
                nowUtc.AddMilliseconds(1)),
            stepInstanceId,
            claimToken);
        Assert.Equal(ProcessRuntimeTransitionOutcome.Applied, running.Outcome);
        var runningStep = Assert.Single(
            running.State.Steps,
            step => step.StepInstanceId == stepInstanceId);
        Assert.Equal(ProcessRuntimeStepStatus.Running, runningStep.Status);
        Assert.Equal(claimToken, runningStep.ActiveClaimToken);
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

    private static void AssertStructureIdentityUnchanged(
        ProjectStructureSurface before,
        ProjectStructureSurface after)
    {
        Assert.Equal(
            before.Nodes.Select(node => node.Id).OrderBy(nodeId => nodeId, StringComparer.Ordinal),
            after.Nodes.Select(node => node.Id).OrderBy(nodeId => nodeId, StringComparer.Ordinal));
        Assert.Equal(
            before.Links
                .Select(link => (link.SourceId, link.TargetId, link.Kind, link.IsUserAuthored, link.RecordId))
                .OrderBy(link => link.SourceId, StringComparer.Ordinal)
                .ThenBy(link => link.TargetId, StringComparer.Ordinal)
                .ThenBy(link => link.Kind),
            after.Links
                .Select(link => (link.SourceId, link.TargetId, link.Kind, link.IsUserAuthored, link.RecordId))
                .OrderBy(link => link.SourceId, StringComparer.Ordinal)
                .ThenBy(link => link.TargetId, StringComparer.Ordinal)
                .ThenBy(link => link.Kind));
    }

    private sealed record ProductCompletionRequiredFileContentCheckJson(
        string[] PathCandidates,
        string[][] RequiredTextAnyGroups);

    private class FailingProjectPartyIntegrationBridgeProxy : DispatchProxy
    {
        public Exception Failure { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IProjectPartyIntegrationBridge.MoveAssignmentsToProjectAsync))
            {
                return Task.FromException(Failure);
            }

            throw new NotSupportedException($"Unexpected project-party bridge call '{targetMethod.Name}'.");
        }
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

    private static void AssertNodeIsOnOutgoingSide(
        ProjectStructureNode sourceNode,
        ProjectStructureNode runNode,
        ProjectStructureNode childNode)
    {
        var outgoingDeltaX = runNode.X - sourceNode.X;
        var outgoingDeltaY = runNode.Y - sourceNode.Y;
        Assert.True(
            Math.Abs(outgoingDeltaX) > 0.5d || Math.Abs(outgoingDeltaY) > 0.5d,
            $"Process run '{runNode.Id}' must not occupy the same position as its source node '{sourceNode.Id}'.");

        var runSize = EstimateProjectStructureCardSize(runNode);
        var childSize = EstimateProjectStructureCardSize(childNode);
        var isHorizontal = Math.Abs(outgoingDeltaX) >= Math.Abs(outgoingDeltaY);
        var outgoingClearance = isHorizontal
            ? outgoingDeltaX > 0d
                ? (childNode.X - (childSize.Width / 2d)) - (runNode.X + (runSize.Width / 2d))
                : (runNode.X - (runSize.Width / 2d)) - (childNode.X + (childSize.Width / 2d))
            : outgoingDeltaY > 0d
                ? (childNode.Y - (childSize.Height / 2d)) - (runNode.Y + (runSize.Height / 2d))
                : (runNode.Y - (runSize.Height / 2d)) - (childNode.Y + (childSize.Height / 2d));

        Assert.True(
            outgoingClearance >= -0.5d,
            $"Projected child '{childNode.Id}' is not on the outgoing side of process run '{runNode.Id}'.");
    }

    private static void AssertNodesDoNotOverlap(IReadOnlyList<ProjectStructureNode> nodes)
    {
        for (var firstIndex = 0; firstIndex < nodes.Count; firstIndex++)
        {
            var first = nodes[firstIndex];
            var firstSize = EstimateProjectStructureCardSize(first);
            for (var secondIndex = firstIndex + 1; secondIndex < nodes.Count; secondIndex++)
            {
                var second = nodes[secondIndex];
                var secondSize = EstimateProjectStructureCardSize(second);
                var separatedHorizontally = Math.Abs(first.X - second.X) >=
                                            ((firstSize.Width + secondSize.Width) / 2d) - 0.5d;
                var separatedVertically = Math.Abs(first.Y - second.Y) >=
                                          ((firstSize.Height + secondSize.Height) / 2d) - 0.5d;

                Assert.True(
                    separatedHorizontally || separatedVertically,
                    $"Projected nodes '{first.Id}' and '{second.Id}' overlap.");
            }
        }
    }

    private static (double Width, double Height) EstimateProjectStructureCardSize(ProjectStructureNode node)
    {
        return node.ObjectType switch
        {
            ProjectObjectType.ProjectRoot => (288d, 210d),
            ProjectObjectType.Phase or
                ProjectObjectType.PromptSession or
                ProjectObjectType.PromptFlow or
                ProjectObjectType.ProjectBlock or
                ProjectObjectType.ProcessDefinition => (272d, 196d),
            ProjectObjectType.ProcessRun or
                ProjectObjectType.ValidationRun or
                ProjectObjectType.TestPlan or
                ProjectObjectType.Decision or
                ProjectObjectType.SecretReference => (248d, 178d),
            _ => (256d, 190d)
        };
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

    private static AgentDefinition CreateProjectStructureRuntimeAgent(Guid projectId)
    {
        string configurationJson = AgentWorkspaceToolAccessMetadata.Write(
            "{}",
            AgentWorkspaceToolAccessProfiles.CreateSettings(
                AgentWorkspaceToolProfileKind.ArchitectureReview));
        configurationJson = AgentProjectStructureAccessMetadata.Write(
            configurationJson,
            new AgentProjectStructureAccessSettings
            {
                CanRead = true,
                AllowAllProjects = false,
                AllowedProjectIds = [projectId]
            });
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Guid.NewGuid(),
            "Projected Screenshot Reader",
            "Project structure vision agent",
            "Reads project-authorized screenshots by node id.",
            "Analyze only the selected project screenshot.",
            AgentLifecycleStatus.Active,
            Guid.NewGuid(),
            "gpt-5-mini",
            AgentWorkloadKind.General,
            AgentChatHistoryMode.ProviderDefault,
            0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            configurationJson,
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            [],
            [],
            now,
            now);
    }

    private static AgentRuntimeToolProviderContext CreateProjectStructureRuntimeContext(
        AgentDefinition agent,
        Guid projectId)
    {
        var provider = new ProviderProfile(
            agent.ProviderProfileId!.Value,
            "Integration vision provider",
            ProviderKind.OpenAi,
            "https://api.openai.com",
            "OPENAI_API_KEY",
            agent.Model,
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: string.Empty,
            Notes: string.Empty,
            HealthStatus: string.Empty,
            LastCheckedAtUtc: null,
            SuggestedModels: []);
        var intent = AgentRuntimeContextIntent.Empty with
        {
            SourceKind = ProjectStructureAgentChatContextBuilder.SourceKind,
            SourceId = projectId.ToString("D"),
            WorkspaceScope = WorkspaceScopeDescriptor.Project(projectId.ToString("D"))
        };

        return new AgentRuntimeToolProviderContext(
            agent,
            provider,
            [],
            SuppressApprovalRequirements: false,
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            RuntimeSessionKey: $"project-structure-projected-image:{projectId:D}",
            intent,
            Tags: new Dictionary<string, string>());
    }

    private sealed class RecordingImageAnalysisService : IAgentImageAnalysisService
    {
        public List<AgentImageAnalysisRequest> Requests { get; } = [];

        public Task<AgentImageAnalysisResult> AnalyzeAsync(
            AgentImageAnalysisRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(new AgentImageAnalysisResult(
                "vision-model",
                "Visible calculator screenshot",
                12,
                4));
        }
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
