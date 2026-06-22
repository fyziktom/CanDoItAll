using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessRuntimeIntegrationAdapterTests
{
    private static readonly ProcessStrategyBindingSnapshot Binding = new(
        new DriverId("driver.runtime"),
        new StrategyId("strategy.execute"),
        "1.0.0",
        "factory.1.0.0",
        "runtime.1",
        "runtime.1",
        "sha256:binding",
        []);

    [Fact]
    public void Product_mutation_completion_requires_evidence_refs()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(outputRoot, "TetrisGame.csproj"), "<Project />");
            var result = ToAdapterResult(
                CreateProductMutationAssignment(outputRoot),
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "Implemented the app.",
                    EvidenceRefs = [],
                    NextActions = []
                });

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code.Value == "process.adapter.product_output_evidence_missing");
            Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == "process.adapter.product_output_evidence_missing");
            Assert.Empty(result.ProducedArtifacts);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Product_mutation_completion_requires_product_files_in_output_root()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            var result = ToAdapterResult(
                CreateProductMutationAssignment(outputRoot),
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "Implemented the app.",
                    EvidenceRefs = ["artifacts/process-runs/run-001/steps/implementation.md"],
                    NextActions = []
                });

            Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
            Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code.Value == "process.adapter.product_output_missing");
            Assert.Contains(outputRoot, result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(result.ProducedArtifacts);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Product_mutation_completion_succeeds_when_output_root_contains_product_file_and_evidence()
    {
        var outputRoot = CreateTempProductRoot();
        try
        {
            File.WriteAllText(Path.Combine(outputRoot, "TetrisGame.csproj"), "<Project />");
            var assignment = CreateProductMutationAssignment(outputRoot);
            var result = ToAdapterResult(
                assignment,
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "Implemented the app.",
                    EvidenceRefs = [BuildStepArtifactRef(assignment)],
                    NextActions = []
                });

            Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
            Assert.NotEmpty(result.ProducedArtifacts);
            Assert.Empty(result.Diagnostics);
        }
        finally
        {
            DeleteDirectory(outputRoot);
        }
    }

    [Fact]
    public void Managed_artifact_completion_requires_evidence_for_produced_slot()
    {
        var assignment = CreateManagedArtifactAssignment("review-architecture-design");
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Reviewed the architecture.",
                EvidenceRefs = [$"artifacts/process-runs/{assignment.RunId.Value:D}/steps/other-step.md"],
                NextActions = []
            });

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code.Value == "process.adapter.produced_artifact_evidence_missing");
        Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == "process.adapter.produced_artifact_evidence_missing");
        Assert.Contains(result.RequestedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
        Assert.Empty(result.ProducedArtifacts);
    }

    [Fact]
    public void Managed_artifact_completion_accepts_step_directory_evidence()
    {
        var assignment = CreateManagedArtifactAssignment("review-architecture-design");
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Reviewed the architecture.",
                EvidenceRefs = [BuildStepDirectoryArtifactRef(assignment, "architecture-review-findings.md")],
                NextActions = []
            });

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Managed_artifact_completion_accepts_scoped_workspace_step_evidence()
    {
        var assignment = CreateManagedArtifactAssignment("review-architecture-design");
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Completed,
                Reason = "Reviewed the architecture.",
                EvidenceRefs = [$"artifacts/scopes/organization/e5df9ad633dbc6974a0678a74976013c/process-runs/{assignment.RunId.Value:D}/steps/{assignment.StepKey}.md"],
                NextActions = []
            });

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.Contains(result.ProducedArtifacts, artifact => artifact.SlotId == assignment.ProducedArtifactSlotIds[0]);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void Runtime_readiness_rejects_delivery_manager_for_implementation_step()
    {
        var deliveryManager = NewAgent(
            "Delivery Manager",
            "Delivery Manager",
            AgentWorkloadKind.Management,
            [
                "delivery-manager",
                "process-mock-role:delivery-manager"
            ],
            AgentWorkspaceToolProfileKind.ReadOnly);

        var readiness = AgentProcessReadinessEvaluator.Evaluate(
            deliveryManager,
            new AgentProcessRoleReadinessRequest(
                "implement-code-change",
                "implement-code-change",
                "delivery-manager",
                "delivery-manager",
                "Delivery Manager",
                [ProcessOperationContractNames.MutateProductTarget],
                ProcessOperationContractNames.ExternalProductTargetMutable));

        Assert.False(readiness.HasRoleFit);
        Assert.False(readiness.IsExecutionReady);
        Assert.Contains(readiness.Findings, finding => finding.Code == "agent.readiness.role-family-mismatch");
        Assert.Contains(readiness.Findings, finding => finding.Code == "agent.readiness.workspace-write-files-missing");
    }

    [Fact]
    public void Runtime_readiness_accepts_delivery_manager_for_local_runtime_command_role()
    {
        var deliveryManager = NewAgent(
            "Delivery Manager",
            "Delivery Manager",
            AgentWorkloadKind.Management,
            [
                "delivery-manager",
                "process-mock-role:delivery-manager"
            ],
            AgentWorkspaceToolProfileKind.BusinessAnalysis);
        var assignment = new ProcessRuntimeStepAssignment(
            ProcessRunId.New(),
            ProcessInstancePlanId.New(),
            ProcessStepInstanceId.New(),
            "resolve-dotnet-run-commands",
            "runtime-command-recorder",
            "delivery-manager",
            "Runtime command recorder",
            ProcessLaunchExecutorKinds.Agent,
            deliveryManager.Id.ToString("D"),
            deliveryManager.Name,
            "Resolve runtime commands.",
            "sha256:readiness",
            "Resolved from role fit.",
            [ArtifactSlotId.New()],
            [],
            [ProcessOperationContractNames.WriteManagedProcessArtifacts],
            ProcessOperationContractNames.ExternalProductTargetReadOnly,
            new Dictionary<string, string>(),
            BranchGate: null,
            DateTimeOffset.UtcNow);

        var request = CreateRuntimeReadinessRequest(assignment);
        var readiness = AgentProcessReadinessEvaluator.Evaluate(deliveryManager, request);

        Assert.Equal("runtime-command-recorder", request.RoleKey);
        Assert.Equal("delivery-manager", request.RoleResourceKey);
        Assert.Equal("Runtime command recorder", request.RoleDisplayName);
        Assert.True(readiness.HasRoleFit);
        Assert.True(readiness.IsExecutionReady);
    }

    [Fact]
    public void Runtime_readiness_rejects_qa_review_lead_for_solution_architect_role()
    {
        var qaReviewLead = NewAgent(
            ".NET QA Review Lead",
            ".NET QA Review Lead",
            AgentWorkloadKind.Qa,
            [
                "qa-lead",
                "dotnet",
                "architecture",
                "review"
            ],
            AgentWorkspaceToolProfileKind.SoftwareDevelopment);
        var architect = NewAgent(
            ".NET Architect",
            ".NET Architect",
            AgentWorkloadKind.Programming,
            [
                "solution-architect",
                "dotnet-architect",
                "architecture"
            ],
            AgentWorkspaceToolProfileKind.SoftwareDevelopment);
        var request = new AgentProcessRoleReadinessRequest(
            "architecture-review",
            "Run .NET architecture design and review subprocess",
            "solution-architect",
            "solution-architect",
            "Solution architect",
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadProjectStructure,
                ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.WriteManagedProcessArtifacts,
                ProcessOperationContractNames.ExecuteExternalAction,
                ProcessOperationContractNames.LaunchRuntime,
                ProcessOperationContractNames.CaptureRuntimeProof
            ],
            ProcessOperationContractNames.ExternalActionControlled);

        var qaReadiness = AgentProcessReadinessEvaluator.Evaluate(qaReviewLead, request);
        var architectReadiness = AgentProcessReadinessEvaluator.Evaluate(architect, request);

        Assert.False(qaReadiness.HasRoleFit);
        Assert.False(qaReadiness.IsExecutionReady);
        Assert.Contains(qaReadiness.Findings, finding => finding.Code == "agent.readiness.role-family-mismatch");
        Assert.True(architectReadiness.HasRoleFit);
        Assert.True(architectReadiness.IsExecutionReady);
        Assert.True(architectReadiness.Score > qaReadiness.Score);
    }

    [Fact]
    public void Runtime_readiness_rejects_generic_code_reviewer_capability_for_solution_architect_role()
    {
        var codeReviewLead = NewAgent(
            "Code Review Lead",
            "Code reviewer",
            AgentWorkloadKind.Qa,
            [
                "review",
                "code",
                "quality"
            ],
            AgentWorkspaceToolProfileKind.QualityValidation,
            [
                new AgentCapabilityAssignment(
                    Guid.NewGuid(),
                    "architecture-source-rag",
                    CapabilityKind.Rag,
                    CapabilityProofStatus.Verified,
                    DateTimeOffset.UtcNow,
                    "Available for architecture source lookup.")
            ]);
        var dotnetArchitect = NewAgent(
            ".NET Solution Architect",
            ".NET architecture specialist",
            AgentWorkloadKind.Programming,
            [
                "dotnet",
                "architecture",
                "blazor"
            ],
            AgentWorkspaceToolProfileKind.ArchitectureReview);
        var request = new AgentProcessRoleReadinessRequest(
            "architecture-review",
            "Run .NET architecture design and review subprocess",
            "solution-architect",
            "solution-architect",
            ".NET Architect",
            [
                ProcessOperationContractNames.ReadProcessContext,
                ProcessOperationContractNames.ReadProjectStructure,
                ProcessOperationContractNames.ReadUpstreamArtifacts,
                ProcessOperationContractNames.WriteManagedProcessArtifacts,
                ProcessOperationContractNames.ExecuteExternalAction
            ],
            ProcessOperationContractNames.ExternalActionControlled);

        var reviewerReadiness = AgentProcessReadinessEvaluator.Evaluate(codeReviewLead, request);
        var architectReadiness = AgentProcessReadinessEvaluator.Evaluate(dotnetArchitect, request);

        Assert.False(reviewerReadiness.HasRoleFit);
        Assert.False(reviewerReadiness.IsExecutionReady);
        Assert.Contains(reviewerReadiness.Findings, finding => finding.Code == "agent.readiness.role-family-mismatch");
        Assert.True(architectReadiness.HasRoleFit);
        Assert.True(architectReadiness.IsExecutionReady);
        Assert.True(architectReadiness.Score > reviewerReadiness.Score);
    }

    [Fact]
    public void Blocked_missing_tool_result_adds_manager_rights_request_signal()
    {
        var assignment = CreateControlledExternalActionAssignment(ProcessRunId.New());
        var result = ToAdapterResult(
            assignment,
            new ProcessStepOutcomeResult
            {
                Status = ProcessStepOutcomeStatus.Blocked,
                Reason = "PolicyDenied: Tool 'workspace_read_file' was denied for this governed process step because the external-target path is outside the workspace boundary.",
                EvidenceRefs = [],
                NextActions =
                [
                    "Manager action: grant workspace_read_file access to the assigned agent or reassign the step."
                ]
            });

        Assert.Equal(StrategyOutcome.NeedsManager, result.Outcome);
        Assert.Contains(result.ManagerSignals, signal => signal.Code.Value == "process.adapter.agent_rights_request");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Code.Value == "process.adapter.agent_rights_request");
        Assert.Contains(".NET Solution Architect", result.UserSafeSummary, StringComparison.Ordinal);
        Assert.Contains("workspace_read_file", result.UserSafeSummary, StringComparison.Ordinal);
        Assert.Contains(ProcessOperationContractNames.ExecuteExternalAction, result.UserSafeSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pending_child_run_detection_defers_blocked_controlled_subprocess_step()
    {
        var parentRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var assignment = CreateControlledExternalActionAssignment(parentRunId);
        var stateStore = new InMemoryRuntimeStateStore(
            NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active),
            NewRuntimeState(parentRunId, childRunId, ProcessRuntimeStatus.Active));
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Blocked,
            Reason = $"Child process run {childRunId} is still producing architecture evidence.",
            EvidenceRefs = [$"artifacts/process-runs/{childRunId}/steps/classify-dotnet-application.md"],
            NextActions = []
        };

        var pendingRunId = await AgentFrameworkProcessExecutionAdapter.TryResolvePendingChildRunAsync(
            assignment,
            output,
            stateStore);

        Assert.Equal(childRunId, pendingRunId);
    }

    [Fact]
    public async Task Pending_child_run_detection_ignores_terminal_child_run()
    {
        var parentRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var assignment = CreateControlledExternalActionAssignment(parentRunId);
        var stateStore = new InMemoryRuntimeStateStore(
            NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active),
            NewRuntimeState(parentRunId, childRunId, ProcessRuntimeStatus.Completed));
        var output = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Blocked,
            Reason = $"Child process run {childRunId} completed without required evidence.",
            EvidenceRefs = [$"artifacts/process-runs/{childRunId}/steps/architecture-handoff.md"],
            NextActions = []
        };

        var pendingRunId = await AgentFrameworkProcessExecutionAdapter.TryResolvePendingChildRunAsync(
            assignment,
            output,
            stateStore);

        Assert.Null(pendingRunId);
    }

    [Fact]
    public async Task Existing_child_run_detection_defers_before_reinvoking_parent_agent()
    {
        var parentRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var assignment = CreateControlledExternalActionAssignment(parentRunId);
        var childAssignment = CreateChildAssignment(
            childRunId,
            parentRunId,
            assignment.StepInstanceId,
            assignment.StepKey);
        var assignmentStore = new InMemoryAssignmentStore(childAssignment);
        var stateStore = new InMemoryRuntimeStateStore(
            NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active),
            NewRuntimeState(parentRunId, childRunId, ProcessRuntimeStatus.Active));

        var pendingRunId = await AgentFrameworkProcessExecutionAdapter.TryResolveExistingPendingChildRunAsync(
            assignment,
            assignmentStore,
            stateStore);

        Assert.Equal(childRunId, pendingRunId);
    }

    [Fact]
    public async Task ExecuteAsync_defers_when_agent_execution_fails_after_child_run_was_created()
    {
        var parentRunId = ProcessRunId.New();
        var childRunId = ProcessRunId.New();
        var agent = NewAgent(
            ".NET Solution Architect",
            ".NET architecture specialist",
            AgentWorkloadKind.Programming,
            [
                "solution-architect",
                "dotnet",
                "architecture"
            ],
            AgentWorkspaceToolProfileKind.ArchitectureReview);
        var parentAssignment = CreateControlledExternalActionAssignment(parentRunId, agent.Id);
        var childAssignment = CreateChildAssignment(
            childRunId,
            parentRunId,
            parentAssignment.StepInstanceId,
            parentAssignment.StepKey);
        var assignmentStore = new InMemoryAssignmentStore(parentAssignment);
        var workspace = new ThrowingWorkspaceService(
            agent,
            new InvalidOperationException("Provider runtime failed after provider activity."),
            () => assignmentStore.Add(childAssignment));
        var adapter = new AgentFrameworkProcessExecutionAdapter(
            new FakeWorkspaceFactory(workspace),
            assignmentStore,
            new InMemoryRuntimeStateStore(
                NewRuntimeState(parentRunId, parentRunId, ProcessRuntimeStatus.Active),
                NewRuntimeState(parentRunId, childRunId, ProcessRuntimeStatus.Active)));

        var exception = await Assert.ThrowsAsync<ProcessRuntimeDispatchDeferredException>(() =>
            adapter.ExecuteAsync(
                new ProcessExecutionAdapterRequest(
                    parentRunId,
                    parentAssignment.StepInstanceId,
                    ProcessExecutionAdapterKind.Workflow,
                    new ProcessExecutionAdapterOperationKey("execute"),
                    Binding,
                    [],
                    [])).AsTask());

        Assert.Equal(childRunId, exception.DeferredRunId);
        Assert.True(workspace.ExecuteRunCalled);
    }

    private static ProcessExecutionAdapterResult ToAdapterResult(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        var adapterType = typeof(ProcessesModuleServiceCollectionExtensions)
            .Assembly
            .GetType("CanDoItAll.Modules.Processes.AgentFrameworkProcessExecutionAdapter")
            ?? throw new InvalidOperationException("Process execution adapter type was not found.");
        var method = adapterType.GetMethod(
            "ToAdapterResult",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Process execution result mapper was not found.");

        return Assert.IsType<ProcessExecutionAdapterResult>(method.Invoke(null, [assignment, output, "sha256:raw"]));
    }

    private static AgentProcessRoleReadinessRequest CreateRuntimeReadinessRequest(ProcessRuntimeStepAssignment assignment)
    {
        var adapterType = typeof(ProcessesModuleServiceCollectionExtensions)
            .Assembly
            .GetType("CanDoItAll.Modules.Processes.AgentFrameworkProcessExecutionAdapter")
            ?? throw new InvalidOperationException("Process execution adapter type was not found.");
        var method = adapterType.GetMethod(
            "CreateRuntimeReadinessRequest",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Process runtime readiness request builder was not found.");

        return Assert.IsType<AgentProcessRoleReadinessRequest>(method.Invoke(null, [assignment]));
    }

    private static ProcessRuntimeStepAssignment CreateProductMutationAssignment(string outputRoot)
    {
        return new ProcessRuntimeStepAssignment(
            ProcessRunId.New(),
            ProcessInstancePlanId.New(),
            ProcessStepInstanceId.New(),
            "implement-dotnet-app",
            "dotnet-developer",
            "dotnet-developer",
            ".NET developer",
            ProcessLaunchExecutorKinds.Agent,
            Guid.NewGuid().ToString("D"),
            ".NET Developer",
            "Implement the app in the configured output root.",
            "sha256:readiness",
            "Resolved from role fit.",
            [ArtifactSlotId.New()],
            [],
            [ProcessOperationContractNames.MutateProductTarget],
            ProcessOperationContractNames.ExternalProductTargetMutable,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["OutputRoot"] = outputRoot,
                ["ProductRoot"] = outputRoot,
                ["ExternalTargetRoot"] = outputRoot
            },
            BranchGate: null,
            DateTimeOffset.UtcNow);
    }

    private static ProcessRuntimeStepAssignment CreateManagedArtifactAssignment(string stepKey)
    {
        return new ProcessRuntimeStepAssignment(
            ProcessRunId.New(),
            ProcessInstancePlanId.New(),
            ProcessStepInstanceId.New(),
            stepKey,
            "solution-architect",
            "solution-architect",
            "Solution architect",
            ProcessLaunchExecutorKinds.Agent,
            Guid.NewGuid().ToString("D"),
            ".NET Solution Architect",
            "Produce managed process evidence.",
            "sha256:readiness",
            "Resolved from role fit.",
            [ArtifactSlotId.New()],
            [],
            [ProcessOperationContractNames.WriteManagedProcessArtifacts],
            ProcessOperationContractNames.ExternalProductTargetReadOnly,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            BranchGate: null,
            DateTimeOffset.UtcNow);
    }

    private static string BuildStepArtifactRef(ProcessRuntimeStepAssignment assignment)
        => $"artifacts/process-runs/{assignment.RunId.Value:D}/steps/{assignment.StepKey}.md";

    private static string BuildStepDirectoryArtifactRef(
        ProcessRuntimeStepAssignment assignment,
        string fileName)
        => $"artifacts/process-runs/{assignment.RunId.Value:D}/{assignment.StepKey}/{fileName}";

    private static ProcessRuntimeStepAssignment CreateControlledExternalActionAssignment(
        ProcessRunId runId,
        Guid? agentId = null)
    {
        return new ProcessRuntimeStepAssignment(
            runId,
            ProcessInstancePlanId.New(),
            ProcessStepInstanceId.New(),
            "architecture-review",
            "solution-architect",
            "solution-architect",
            "Solution architect",
            ProcessLaunchExecutorKinds.Agent,
            (agentId ?? Guid.NewGuid()).ToString("D"),
            ".NET Solution Architect",
            "Launch and observe the governed architecture subprocess.",
            "sha256:readiness",
            "Resolved from role fit.",
            [],
            [],
            [ProcessOperationContractNames.ExecuteExternalAction],
            ProcessOperationContractNames.ExternalActionControlled,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            BranchGate: null,
            DateTimeOffset.UtcNow);
    }

    private static ProcessRuntimeStepAssignment CreateChildAssignment(
        ProcessRunId childRunId,
        ProcessRunId parentRunId,
        ProcessStepInstanceId parentStepId,
        string parentStepKey)
    {
        return new ProcessRuntimeStepAssignment(
            childRunId,
            ProcessInstancePlanId.New(),
            ProcessStepInstanceId.New(),
            "classify-dotnet-application",
            "architecture-designer",
            "solution-architect",
            "Architecture designer",
            ProcessLaunchExecutorKinds.Agent,
            Guid.NewGuid().ToString("D"),
            ".NET Solution Architect",
            "Classify the child run.",
            "sha256:readiness",
            "Resolved from role fit.",
            [],
            [],
            [ProcessOperationContractNames.ReadProjectStructure],
            ProcessOperationContractNames.ExternalProductTargetReadOnly,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ParentProcessRunId"] = parentRunId.ToString(),
                ["ParentProcessStepId"] = parentStepId.ToString(),
                ["ParentProcessStepKey"] = parentStepKey
            },
            BranchGate: null,
            DateTimeOffset.UtcNow);
    }

    private static ProcessRuntimeStateSnapshot NewRuntimeState(
        ProcessRunId rootRunId,
        ProcessRunId runId,
        ProcessRuntimeStatus status)
    {
        return new ProcessRuntimeStateSnapshot(
            rootRunId,
            runId,
            ProcessInstancePlanId.New(),
            "sha256:plan",
            status,
            [],
            [],
            [],
            new HashSet<ArtifactSlotId>(),
            DateTimeOffset.UtcNow);
    }

    private static string CreateTempProductRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), $"CanDoItAll.ProductMutation.{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static AgentDefinition NewAgent(
        string name,
        string roleTitle,
        AgentWorkloadKind workload,
        IReadOnlyList<string> tags,
        AgentWorkspaceToolProfileKind toolProfile,
        IReadOnlyList<AgentCapabilityAssignment>? capabilities = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Guid.NewGuid(),
            name,
            roleTitle,
            $"{name} test agent.",
            "Test instructions.",
            AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            Model: "test-model",
            workload,
            AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            AgentWorkspaceToolAccessMetadata.Write(
                "{}",
                new AgentWorkspaceToolAccessSettings
                {
                    Profile = toolProfile
                }),
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            capabilities ?? [],
            tags,
            now,
            now);
    }

    private sealed class FakeWorkspaceFactory(IAgentFrameworkWorkspaceService workspaceService) : ICanDoItAllAgentWorkspaceFactory
    {
        public IAgentFrameworkWorkspaceService GetOrganizationWorkspaceService()
            => workspaceService;

        public IAgentFrameworkWorkspaceService GetWorkspaceService(WorkspaceScopeDescriptor scope)
            => workspaceService;

        public WorkspaceScopeDescriptor GetOrganizationScope()
            => WorkspaceScopeDescriptor.Organization("unit-test");

        public string GetWorkspaceRoot()
            => Path.GetTempPath();
    }

    private sealed class ThrowingWorkspaceService(
        AgentDefinition agent,
        Exception executeException,
        Action? beforeThrow = null) : IAgentFrameworkWorkspaceService
    {
        public event EventHandler<ExecutionLogEntry>? ExecutionUpdated
        {
            add { }
            remove { }
        }

        public bool ExecuteRunCalled { get; private set; }

        public Task<IReadOnlyList<AgentDefinition>> ListAgentsAsync(
            bool includeTemplates = true,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AgentDefinition>>([agent]);

        public Task<ExecutionRunResult> ExecuteRunAsync(
            ExecutionRunRequest request,
            CancellationToken cancellationToken = default)
        {
            ExecuteRunCalled = true;
            beforeThrow?.Invoke();
            throw executeException;
        }

        public Task<SandboxDashboardSnapshot> GetDashboardAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentOverviewSnapshot> GetAgentOverviewAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentUsageDetailSnapshot> GetAgentUsageDetailsAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderUsageDetailSnapshot> GetProviderUsageDetailsAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<ModelUsageDetailSnapshot> GetModelUsageDetailsAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentEditorModel> GetAgentEditorAsync(Guid? agentId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveAgentAsync(AgentEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteAgentAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<AgentTeamDefinition>> ListAgentTeamsAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentTeamEditorModel> GetAgentTeamEditorAsync(Guid? teamId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveAgentTeamAsync(AgentTeamEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentTeamDefinition> UpdateAgentTeamMembersAsync(
            Guid teamId,
            IReadOnlyList<Guid> agentIds,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteAgentTeamAsync(Guid teamId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> CloneAgentAsync(Guid agentId, string cloneName, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> ConvertToTemplateAsync(Guid agentId, string templateKey, CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentExportResult> ExportAgentAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> ImportAgentAsync(string packagePath, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderProfileEditorModel> GetProviderEditorAsync(Guid? providerId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveProviderAsync(ProviderProfileEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteProviderAsync(Guid providerId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderHealthResult> TestProviderAsync(Guid providerId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderTestChatResult> RunProviderTestChatAsync(
            Guid providerId,
            ProviderTestChatRequest request,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<ProviderModelMaintenanceEditorResult> CreateOrUpdateProviderModelAsync(
            Guid providerId,
            ProviderModelMaintenanceEditorRequest request,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<CapabilityCatalogItem>> ListCapabilitiesAsync(CancellationToken cancellationToken = default) => throw Unused();

        public Task<CapabilityEditorModel> GetCapabilityEditorAsync(Guid? capabilityId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveCapabilityAsync(CapabilityEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteCapabilityAsync(Guid capabilityId, CancellationToken cancellationToken = default) => throw Unused();

        public Task VerifyCapabilityAsync(Guid agentId, Guid capabilityId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ChatSessionRecord>> ListChatSessionsAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatPageBootstrapSnapshot> GetChatPageBootstrapAsync(bool includeTemplates = false, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatAgentWorkspaceSnapshot> GetChatAgentWorkspaceAsync(
            Guid agentId,
            Guid? preferredSessionId = null,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatSessionRecord> GetOrCreateChatSessionAsync(
            Guid agentId,
            Guid? chatSessionId = null,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatSessionRecord> RenameChatSessionAsync(
            Guid agentId,
            Guid chatSessionId,
            string title,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<ExecutionRunResult> ContinueExecutionRunAsync(
            Guid executionRunId,
            bool approved,
            bool autoApprovePendingToolCalls = false,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentChatRunResult> SendMessageAsync(Guid agentId, Guid? chatSessionId, string prompt, CancellationToken cancellationToken = default) => throw Unused();

        public Task<AgentChatRunResult> RespondToPendingApprovalsAsync(
            Guid agentId,
            Guid chatSessionId,
            bool approved,
            bool autoApprovePendingToolCalls = false,
            CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionLogEntry>> ListExecutionLogAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ChatRuntimeSnapshot> GetChatRuntimeSnapshotAsync(Guid agentId, Guid? chatSessionId = null, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<AgentRunMetric>> ListMetricsAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<AgentMemoryRecord>> ListMemoryAsync(Guid agentId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<Guid> SaveMemoryAsync(MemoryEditorModel model, CancellationToken cancellationToken = default) => throw Unused();

        public Task DeleteMemoryAsync(Guid memoryId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionRunRecord>> ListExecutionRunsAsync(ExecutionRunQuery query, CancellationToken cancellationToken = default) => throw Unused();

        public Task<ExecutionRunDetail> GetExecutionRunDetailAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionArtifactRecord>> ListExecutionArtifactsAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ExecutionWorkflowCheckpointRecord>> ListExecutionWorkflowCheckpointsAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();

        public Task<IReadOnlyList<ToolExecutionReceiptRecord>> ListToolExecutionReceiptsAsync(Guid executionRunId, CancellationToken cancellationToken = default) => throw Unused();

        private static InvalidOperationException Unused()
            => new("This fake workspace method is not used by the adapter test.");
    }

    private sealed class InMemoryRuntimeStateStore(params ProcessRuntimeStateSnapshot[] states) : IProcessRuntimeStateStore
    {
        private readonly IReadOnlyDictionary<ProcessRunId, ProcessRuntimeStateSnapshot> stateByRunId =
            states.ToDictionary(state => state.RunId);

        public Task<ProcessRuntimeStateSnapshot?> LoadAsync(
            ProcessRunId runId,
            CancellationToken cancellationToken = default)
        {
            stateByRunId.TryGetValue(runId, out var state);
            return Task.FromResult(state);
        }
    }

    private sealed class InMemoryAssignmentStore(params ProcessRuntimeStepAssignment[] initialAssignments) : IProcessRuntimeStepAssignmentStore
    {
        private readonly List<ProcessRuntimeStepAssignment> assignments = [.. initialAssignments];

        public void Add(ProcessRuntimeStepAssignment assignment)
        {
            assignments.Add(assignment);
        }

        public ValueTask SaveAsync(
            IReadOnlyList<ProcessRuntimeStepAssignment> assignments,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> LoadByRunAsync(
            ProcessRunId runId,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<ProcessRuntimeStepAssignment>>(
                assignments.Where(assignment => assignment.RunId == runId).ToArray());

        public ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> FindByLaunchVariablesAsync(
            IReadOnlyDictionary<string, string> requiredVariables,
            CancellationToken cancellationToken = default)
        {
            var matches = assignments
                .Where(assignment => requiredVariables.All(required =>
                    assignment.LaunchVariables.TryGetValue(required.Key, out var value) &&
                    string.Equals(value, required.Value, StringComparison.Ordinal)))
                .ToArray();

            return ValueTask.FromResult<IReadOnlyList<ProcessRuntimeStepAssignment>>(matches);
        }

        public ValueTask<ProcessRuntimeStepAssignment?> LoadAsync(
            ProcessRunId runId,
            ProcessStepInstanceId stepInstanceId,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(assignments.FirstOrDefault(assignment =>
                assignment.RunId == runId &&
                assignment.StepInstanceId == stepInstanceId));
    }
}
