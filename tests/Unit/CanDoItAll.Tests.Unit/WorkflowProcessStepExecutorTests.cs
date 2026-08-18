using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowProcessStepExecutorTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 20, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ExecuteAsync_launches_exact_workflow_with_typed_process_origin_context_and_idempotency()
    {
        var workflowId = WorkflowId.New();
        var versionId = WorkflowVersionId.New();
        var assignment = CreateAssignment(new ProcessWorkflowExecutorBinding(
            new ProcessWorkflowId(workflowId.Value),
            new ProcessWorkflowVersionId(versionId.Value)));
        var launchedRun = CreateWorkflowRun(
            workflowId,
            versionId,
            WorkflowRunState.Completed,
            CreateProcessOrigin(assignment));
        var runtime = new RecordingWorkflowRuntimeManager
        {
            Events = [CreateOutputEvent(launchedRun.RunId, CreateCompletedOutput())]
        };
        var launch = new RecordingWorkflowLaunchService
        {
            Run = launchedRun
        };
        var executor = CreateExecutor(launch, runtime);

        var result = await executor.ExecuteAsync(
            assignment,
            ProcessStepExecutionContract.Empty,
            CancellationToken.None);

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        var intent = Assert.Single(launch.Intents);
        var selection = Assert.IsType<WorkflowDefinitionSelection.ExactSavedVersion>(intent.Selection);
        Assert.Equal(workflowId, selection.WorkflowId);
        Assert.Equal(versionId, selection.VersionId);
        Assert.Equal(WorkflowLaunchMode.Production, intent.Mode);
        Assert.Equal(WorkflowLaunchCompletionPolicy.WaitForStopped, intent.CompletionPolicy);
        Assert.Null(intent.RequestedBackend);
        Assert.False(intent.PreviewSimulationPlan.HasSteps);
        var origin = Assert.IsType<WorkflowLaunchOrigin.ProcessAssignment>(intent.Origin);
        Assert.Equal(new WorkflowProcessRunId(assignment.RunId.Value), origin.ProcessRun);
        Assert.Equal(new WorkflowProcessAssignmentId(assignment.StepInstanceId.Value), origin.Assignment);
        Assert.Equal(assignment.RunId.Value.ToString("D"), origin.CorrelationId.Value);
        var idempotency = Assert.IsType<WorkflowLaunchIdempotency.CallerSupplied>(intent.Idempotency);
        Assert.Equal(
            $"process-assignment:{assignment.RunId.Value:N}:{assignment.StepInstanceId.Value:N}",
            idempotency.Key.Value);
        var input = JsonSerializer.Deserialize<WorkflowProcessAssignmentInputEnvelope>(
            intent.InputJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(input);
        Assert.Equal(WorkflowProcessAssignmentInputEnvelope.CurrentSchemaVersion, input.SchemaVersion);
        Assert.Equal(assignment.RunId.Value, input.ProcessRunId.Value);
        Assert.Equal(assignment.StepInstanceId.Value, input.AssignmentId.Value);
        Assert.Equal(assignment.StepKey, input.StepKey);
        Assert.Equal(assignment.Prompt, input.Prompt);
    }

    [Fact]
    public async Task ExecuteAsync_recovers_completed_typed_origin_child_without_duplicate_launch()
    {
        var binding = new ProcessWorkflowExecutorBinding(new ProcessWorkflowId(Guid.NewGuid()));
        var assignment = CreateAssignment(binding);
        var child = CreateWorkflowRun(
            new WorkflowId(binding.WorkflowId.Value),
            WorkflowVersionId.New(),
            WorkflowRunState.Completed,
            CreateProcessOrigin(assignment));
        var runtime = new RecordingWorkflowRuntimeManager
        {
            Runs = [child],
            Events = [CreateOutputEvent(child.RunId, CreateCompletedOutput())]
        };
        var launch = new RecordingWorkflowLaunchService();
        var executor = CreateExecutor(launch, runtime);

        var result = await executor.ExecuteAsync(
            assignment,
            ProcessStepExecutionContract.Empty,
            CancellationToken.None);

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.Empty(launch.Intents);
        Assert.Equal(new WorkflowId(binding.WorkflowId.Value), runtime.ListedWorkflowId);
    }

    [Theory]
    [InlineData(WorkflowRunState.NotStarted)]
    [InlineData(WorkflowRunState.Running)]
    [InlineData(WorkflowRunState.WaitingForInput)]
    [InlineData(WorkflowRunState.Idle)]
    public async Task ExecuteAsync_defers_when_verified_child_is_still_active(WorkflowRunState state)
    {
        var binding = new ProcessWorkflowExecutorBinding(new ProcessWorkflowId(Guid.NewGuid()));
        var assignment = CreateAssignment(binding);
        var child = CreateWorkflowRun(
            new WorkflowId(binding.WorkflowId.Value),
            WorkflowVersionId.New(),
            state,
            CreateProcessOrigin(assignment));
        var runtime = new RecordingWorkflowRuntimeManager { Runs = [child] };
        var launch = new RecordingWorkflowLaunchService();
        var executor = CreateExecutor(launch, runtime);

        var exception = await Assert.ThrowsAsync<ProcessRuntimeDispatchDeferredException>(() =>
            executor.ExecuteAsync(
                assignment,
                ProcessStepExecutionContract.Empty,
                CancellationToken.None).AsTask());

        Assert.Contains(child.RunId.Value.ToString("D"), exception.Message, StringComparison.Ordinal);
        Assert.Empty(launch.Intents);
    }

    [Theory]
    [InlineData(WorkflowRunState.Failed, StrategyOutcome.Failed)]
    [InlineData(WorkflowRunState.Cancelled, StrategyOutcome.Canceled)]
    public async Task ExecuteAsync_maps_verified_terminal_child_state_without_relaunch(
        WorkflowRunState state,
        StrategyOutcome expectedOutcome)
    {
        var binding = new ProcessWorkflowExecutorBinding(new ProcessWorkflowId(Guid.NewGuid()));
        var assignment = CreateAssignment(binding);
        var child = CreateWorkflowRun(
            new WorkflowId(binding.WorkflowId.Value),
            WorkflowVersionId.New(),
            state,
            CreateProcessOrigin(assignment));
        var runtime = new RecordingWorkflowRuntimeManager { Runs = [child] };
        var launch = new RecordingWorkflowLaunchService();
        var executor = CreateExecutor(launch, runtime);

        var result = await executor.ExecuteAsync(
            assignment,
            ProcessStepExecutionContract.Empty,
            CancellationToken.None);

        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Empty(launch.Intents);
    }

    [Fact]
    public async Task ExecuteAsync_omits_failed_workflow_backend_summary_from_public_result()
    {
        const string protectedBackendSummary = "secret: C:\\private\\workflow\\password=raw-workflow-token";
        var binding = new ProcessWorkflowExecutorBinding(new ProcessWorkflowId(Guid.NewGuid()));
        var assignment = CreateAssignment(binding);
        var child = CreateWorkflowRun(
            new WorkflowId(binding.WorkflowId.Value),
            WorkflowVersionId.New(),
            WorkflowRunState.Failed,
            CreateProcessOrigin(assignment)) with
        {
            Summary = protectedBackendSummary
        };
        var runtime = new RecordingWorkflowRuntimeManager { Runs = [child] };
        var executor = CreateExecutor(new RecordingWorkflowLaunchService(), runtime);

        var result = await executor.ExecuteAsync(
            assignment,
            ProcessStepExecutionContract.Empty,
            CancellationToken.None);

        var publicResult = JsonSerializer.Serialize(result);
        Assert.Equal(StrategyOutcome.Failed, result.Outcome);
        Assert.Contains("restricted workflow evidence", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(protectedBackendSummary, publicResult, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-workflow-token", publicResult, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_omits_validator_messages_from_invalid_workflow_output()
    {
        const string protectedCriterionId = "secret: C:\\private\\criteria\\token=raw-validation-token";
        var binding = new ProcessWorkflowExecutorBinding(new ProcessWorkflowId(Guid.NewGuid()));
        var assignment = CreateAssignment(binding);
        var child = CreateWorkflowRun(
            new WorkflowId(binding.WorkflowId.Value),
            WorkflowVersionId.New(),
            WorkflowRunState.Completed,
            CreateProcessOrigin(assignment));
        var invalidOutput = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "The workflow reported duplicate acceptance evidence.",
            EvidenceRefs = ["workflow://run/completed"],
            AcceptanceCriteriaEvidence =
            [
                new ProcessAcceptanceCriterionEvidence
                {
                    CriterionId = protectedCriterionId,
                    Status = ProcessAcceptanceCriterionEvidenceStatus.Passed,
                    Summary = "First proof.",
                    EvidenceRefs = ["workflow://proof/one"]
                },
                new ProcessAcceptanceCriterionEvidence
                {
                    CriterionId = protectedCriterionId,
                    Status = ProcessAcceptanceCriterionEvidenceStatus.Passed,
                    Summary = "Duplicate proof.",
                    EvidenceRefs = ["workflow://proof/two"]
                }
            ]
        };
        var runtime = new RecordingWorkflowRuntimeManager
        {
            Runs = [child],
            Events = [CreateOutputEvent(child.RunId, invalidOutput)]
        };
        var executor = CreateExecutor(new RecordingWorkflowLaunchService(), runtime);

        var result = await executor.ExecuteAsync(
            assignment,
            ProcessStepExecutionContract.Empty,
            CancellationToken.None);

        var publicResult = JsonSerializer.Serialize(result);
        Assert.Equal(StrategyOutcome.Failed, result.Outcome);
        Assert.Contains("validation rule", result.UserSafeSummary, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(protectedCriterionId, publicResult, StringComparison.Ordinal);
        Assert.DoesNotContain("raw-validation-token", publicResult, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_ignores_same_workflow_run_with_unverified_origin_and_launches_once()
    {
        var binding = new ProcessWorkflowExecutorBinding(new ProcessWorkflowId(Guid.NewGuid()));
        var assignment = CreateAssignment(binding);
        var workflowId = new WorkflowId(binding.WorkflowId.Value);
        var unverified = CreateWorkflowRun(
            workflowId,
            WorkflowVersionId.New(),
            WorkflowRunState.Running,
            new WorkflowLaunchOrigin.Api(
                new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "spoofed-process"),
                new WorkflowLaunchCorrelationId(assignment.RunId.Value.ToString("D"))));
        var launched = CreateWorkflowRun(
            workflowId,
            WorkflowVersionId.New(),
            WorkflowRunState.Completed,
            CreateProcessOrigin(assignment));
        var runtime = new RecordingWorkflowRuntimeManager
        {
            Runs = [unverified],
            Events = [CreateOutputEvent(launched.RunId, CreateCompletedOutput())]
        };
        var launch = new RecordingWorkflowLaunchService { Run = launched };
        var executor = CreateExecutor(launch, runtime);

        var result = await executor.ExecuteAsync(
            assignment,
            ProcessStepExecutionContract.Empty,
            CancellationToken.None);

        Assert.Equal(StrategyOutcome.Succeeded, result.Outcome);
        Assert.Single(launch.Intents);
    }

    [Fact]
    public async Task ExecuteAsync_rejects_launch_result_that_does_not_match_typed_assignment_identity()
    {
        var binding = new ProcessWorkflowExecutorBinding(
            new ProcessWorkflowId(Guid.NewGuid()),
            new ProcessWorkflowVersionId(Guid.NewGuid()));
        var assignment = CreateAssignment(binding);
        var mismatchedRun = CreateWorkflowRun(
            new WorkflowId(binding.WorkflowId.Value),
            WorkflowVersionId.New(),
            WorkflowRunState.Completed,
            CreateProcessOrigin(assignment)) with
        {
            Origin = null
        };
        var launch = new RecordingWorkflowLaunchService { Run = mismatchedRun };
        var runtime = new RecordingWorkflowRuntimeManager
        {
            Events = [CreateOutputEvent(mismatchedRun.RunId, CreateCompletedOutput())]
        };
        var executor = CreateExecutor(launch, runtime);

        var result = await executor.ExecuteAsync(
            assignment,
            ProcessStepExecutionContract.Empty,
            CancellationToken.None);

        Assert.Equal(StrategyOutcome.Failed, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.workflow_launch_identity_mismatch");
        Assert.Single(launch.Intents);
    }

    [Fact]
    public async Task ExecuteAsync_rejects_process_artifact_contract_before_workflow_launch()
    {
        var assignment = CreateAssignment(new ProcessWorkflowExecutorBinding(new ProcessWorkflowId(Guid.NewGuid()))) with
        {
            ProducedArtifactSlotIds = [ArtifactSlotId.New()]
        };
        var launch = new RecordingWorkflowLaunchService();
        var executor = CreateExecutor(launch, new RecordingWorkflowRuntimeManager());

        var result = await executor.ExecuteAsync(
            assignment,
            ProcessStepExecutionContract.Empty,
            CancellationToken.None);

        Assert.Equal(StrategyOutcome.Failed, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.workflow_artifact_mapping_unsupported");
        Assert.Empty(launch.Intents);
    }

    [Fact]
    public async Task ExecuteAsync_rejects_externalized_output_instead_of_fabricating_process_result()
    {
        var binding = new ProcessWorkflowExecutorBinding(new ProcessWorkflowId(Guid.NewGuid()));
        var assignment = CreateAssignment(binding);
        var child = CreateWorkflowRun(
            new WorkflowId(binding.WorkflowId.Value),
            WorkflowVersionId.New(),
            WorkflowRunState.Completed,
            CreateProcessOrigin(assignment));
        var runtime = new RecordingWorkflowRuntimeManager
        {
            Runs = [child],
            Events =
            [
                new WorkflowEventRecord(
                    Guid.NewGuid(),
                    child.RunId,
                    WorkflowEventKind.ExecutorCompleted,
                    new WorkflowNodeId("result"),
                    "Output was externalized.",
                    WorkflowEventPayloads.Serialize(
                        WorkflowEventPayloadSource.CanDoItAllProgress,
                        "WorkflowNodeCompleted",
                        inlineJson: "{\"partial\":true}",
                        reference: "artifact://workflow-output",
                        originalInlineCharacters: 100_000,
                        inlineTruncated: true),
                    Now)
            ]
        };
        var launch = new RecordingWorkflowLaunchService();
        var executor = CreateExecutor(launch, runtime);

        var result = await executor.ExecuteAsync(
            assignment,
            ProcessStepExecutionContract.Empty,
            CancellationToken.None);

        Assert.Equal(StrategyOutcome.Failed, result.Outcome);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Code.Value == "process.adapter.workflow_output_externalized_unsupported");
        Assert.Empty(launch.Intents);
    }

    private static WorkflowProcessStepExecutor CreateExecutor(
        IWorkflowLaunchService launchService,
        IWorkflowRuntimeManager runtimeManager)
    {
        var workspaceFiles = TestWorkspaceServices.CreateFileService(Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.WorkflowProcessStepExecutor.{Guid.NewGuid():N}"));
        var receiptPolicies = new ProcessToolReceiptPolicyCatalog([]);
        var issueFactory = ProcessCompletionTestServices.CreateIssueResultFactory(
            workspaceFiles,
            new ProcessCompletionDefectEvidenceCatalog([]));
        var resultConverter = new ProcessExecutionResultConverter(
            new ProcessCompletionGateEvaluator([_ => null]),
            receiptPolicies,
            issueFactory);
        return new WorkflowProcessStepExecutor(launchService, runtimeManager, resultConverter);
    }

    private static ProcessRuntimeStepAssignment CreateAssignment(ProcessWorkflowExecutorBinding workflowBinding)
    {
        var runId = ProcessRunId.New();
        var stepId = ProcessStepInstanceId.New();
        return new ProcessRuntimeStepAssignment(
            runId,
            ProcessInstancePlanId.New(),
            stepId,
            "execute-workflow",
            "workflow-role",
            "workflow-role",
            "Workflow role",
            ProcessLaunchExecutorKinds.Workflow,
            "legacy-workflow-display-id",
            "Selected workflow",
            "Execute the selected workflow for this process assignment.",
            "sha256:workflow-readiness",
            "Explicit workflow binding.",
            [],
            [],
            [],
            string.Empty,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["ProjectId"] = Guid.NewGuid().ToString("D")
            },
            BranchGate: null,
            Now)
        {
            WorkflowBinding = workflowBinding
        };
    }

    private static WorkflowLaunchOrigin.ProcessAssignment CreateProcessOrigin(
        ProcessRuntimeStepAssignment assignment)
        => new(
            new WorkflowProcessRunId(assignment.RunId.Value),
            new WorkflowProcessAssignmentId(assignment.StepInstanceId.Value),
            new WorkflowLaunchCorrelationId(assignment.RunId.Value.ToString("D")));

    private static WorkflowRunSnapshot CreateWorkflowRun(
        WorkflowId workflowId,
        WorkflowVersionId versionId,
        WorkflowRunState state,
        WorkflowLaunchOrigin origin)
        => new(
            WorkflowRunId.New(),
            workflowId,
            versionId,
            state,
            WorkflowRuntimeBackendKind.InProcess,
            "workflow-process-test",
            state.ToString(),
            Now,
            Now)
        {
            Origin = origin,
            TerminalAtUtc = state is WorkflowRunState.Completed or WorkflowRunState.Failed or WorkflowRunState.Cancelled
                ? Now
                : null
        };

    private static WorkflowEventRecord CreateOutputEvent(
        WorkflowRunId runId,
        ProcessStepOutcomeResult output)
        => new(
            Guid.NewGuid(),
            runId,
            WorkflowEventKind.ExecutorCompleted,
            new WorkflowNodeId("result"),
            "Workflow process output.",
            WorkflowEventPayloads.Serialize(
                WorkflowEventPayloadSource.CanDoItAllProgress,
                "WorkflowNodeCompleted",
                inlineJson: JsonSerializer.Serialize(output, AgentOutputJson.SerializerOptions)),
            Now);

    private static ProcessStepOutcomeResult CreateCompletedOutput()
        => new()
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "The selected workflow completed the process assignment.",
            EvidenceRefs = ["workflow://run/completed"]
        };

    private sealed class RecordingWorkflowLaunchService : IWorkflowLaunchService
    {
        public List<WorkflowLaunchIntent> Intents { get; } = [];

        public WorkflowRunSnapshot? Run { get; init; }

        public Task<WorkflowLaunchResult> LaunchAsync(
            WorkflowLaunchIntent intent,
            CancellationToken cancellationToken = default)
        {
            Intents.Add(intent);
            var run = Run ?? throw new InvalidOperationException("Workflow launch was not expected by this test.");
            var definition = CreateDefinition(run.WorkflowId, run.VersionId);
            return Task.FromResult(new WorkflowLaunchResult(
                run,
                new WorkflowResolvedRuntimeRequest(
                    definition,
                    intent.InputJson,
                    new WorkflowRuntimeBackendDescriptor(
                        run.Backend,
                        "Workflow process test backend",
                        IsDurable: false,
                        SupportsStreaming: false,
                        SupportsExternalRequests: true,
                        SupportsDashboardObservability: false,
                        OperationalNotes: "Test backend."),
                    intent.PreviewSimulationPlan,
                    intent.Mode,
                    intent.Origin,
                    intent.CompletionPolicy,
                    intent.Idempotency,
                    Now),
                WorkflowLaunchIdempotencyDisposition.EnforcedNewRun));
        }

        private static WorkflowDefinition CreateDefinition(
            WorkflowId workflowId,
            WorkflowVersionId versionId)
        {
            var start = new WorkflowNode(
                new WorkflowNodeId("start"),
                WorkflowNodeKind.Start,
                "Start",
                [],
                new WorkflowNodeSettings(
                    ComponentId: null,
                    AgentId: null,
                    SubworkflowId: null,
                    ExternalRequestKind: null,
                    Instructions: string.Empty,
                    InputShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON input"),
                    ResultShape: new WorkflowValueShape(WorkflowValueShapeKind.Json, "{}", "JSON result")));
            return new WorkflowDefinition(
                workflowId,
                versionId,
                "Process workflow",
                "Workflow launched by process assignment tests.",
                WorkflowLifecycleStatus.Active,
                new WorkflowGraph(start.Id, [start], []),
                new WorkflowRuntimePolicy(
                    WorkflowRuntimeBackendKind.InProcess,
                    AllowInProcessPreviewRuns: true,
                    RequireDurableProductionRuns: false,
                    ExposeAzureFunctionsStatusEndpoint: false,
                    ExposeAzureFunctionsMcpTool: false),
                Now,
                Now);
        }
    }

    private sealed class RecordingWorkflowRuntimeManager : IWorkflowRuntimeManager
    {
        public IReadOnlyList<WorkflowRunSnapshot> Runs { get; init; } = [];

        public IReadOnlyList<WorkflowEventRecord> Events { get; init; } = [];

        public WorkflowId? ListedWorkflowId { get; private set; }

        public Task<IReadOnlyList<WorkflowRunSnapshot>> ListRunsAsync(
            WorkflowId? workflowId = null,
            CancellationToken cancellationToken = default)
        {
            ListedWorkflowId = workflowId;
            return Task.FromResult(Runs);
        }

        public Task<IReadOnlyList<WorkflowEventRecord>> ListEventsAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WorkflowEventRecord>>(
                Events.Where(workflowEvent => workflowEvent.RunId == runId).ToArray());

        public Task<WorkflowRunSnapshot?> GetRunAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(Runs.FirstOrDefault(run => run.RunId == runId));

        public Task<WorkflowRunSnapshot> StartAsync(
            WorkflowDefinition definition,
            WorkflowRunStartRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<WorkflowCheckpointRecord>> ListCheckpointsAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowListPage<WorkflowEventRecord>> ListEventPageAsync(
            WorkflowEventPageRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowRunSnapshot> CancelAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowRunCancellationResult> RequestCancellationAsync(
            WorkflowRunId runId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowRunSnapshot> RespondToExternalRequestAsync(
            WorkflowExternalRequestId requestId,
            string responseJson,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<WorkflowExternalResponseResult> SubmitExternalResponseAsync(
            WorkflowExternalRequestId requestId,
            string responseJson,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
