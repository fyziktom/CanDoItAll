using System.Reflection;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessRuntimeIntegrationAdapterTests
{
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
            var result = ToAdapterResult(
                CreateProductMutationAssignment(outputRoot),
                new ProcessStepOutcomeResult
                {
                    Status = ProcessStepOutcomeStatus.Completed,
                    Reason = "Implemented the app.",
                    EvidenceRefs = ["artifacts/process-runs/run-001/steps/implementation.md"],
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

    private static ProcessRuntimeStepAssignment CreateControlledExternalActionAssignment(ProcessRunId runId)
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
            Guid.NewGuid().ToString("D"),
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
        AgentWorkspaceToolProfileKind toolProfile)
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
            [],
            tags,
            now,
            now);
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

    private sealed class InMemoryAssignmentStore(params ProcessRuntimeStepAssignment[] assignments) : IProcessRuntimeStepAssignmentStore
    {
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
