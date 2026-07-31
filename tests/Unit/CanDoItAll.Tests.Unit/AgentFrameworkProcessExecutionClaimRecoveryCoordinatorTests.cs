using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class AgentFrameworkProcessExecutionClaimRecoveryCoordinatorTests
{
    [Fact]
    public async Task BlockRecoveredExecutionClaimAsync_rejects_empty_execution_identity_without_mutating_claim()
    {
        var now = new DateTimeOffset(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);
        var runId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var stepId = ProcessStepInstanceId.New();
        var slotId = ArtifactSlotId.New();
        var claimToken = DispatchClaimToken.New();
        var assignment = CreateAssignment(runId, planId, stepId, slotId, now);
        var state = CreateClaimedState(
            runId,
            planId,
            stepId,
            slotId,
            claimToken,
            new DispatcherOwnerId("claim-recovery-guard-test"),
            now);
        var executionRun = CreateCompletedExecutionRun(
            runId,
            stepId,
            claimToken,
            now.AddSeconds(1));
        var runtimeStore = new RecordingRuntimeStore(state);
        var assignmentStore = new SingleAssignmentStore(assignment);
        var planStore = new SinglePlanStore(CreatePlan(planId, now));

        await using var projectionDbContext = CreateProjectionDbContext();
        var clock = new FixedProcessProjectionClock(now.AddSeconds(4));
        var projectionCatchupService = CreateProjectionCatchupService(
            projectionDbContext,
            clock);
        var coordinator = CreateCoordinator(
            new TestWorkspaceFactory(CreateWorkspaceService(executionRun)),
            clock,
            runtimeStore,
            planStore,
            assignmentStore,
            projectionCatchupService,
            CreateCompletionCoordinator(new WorkspaceFileService(Path.GetTempPath())));
        var stateBeforeAttempt = runtimeStore.State;
        var executionWithoutIdentity = executionRun with
        {
            Id = Guid.Empty
        };

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            coordinator.BlockRecoveredExecutionClaimAsync(
                executionWithoutIdentity,
                runId,
                stepId,
                "claim-recovery-guard-test"));

        Assert.Equal("executionRun", exception.ParamName);
        Assert.Same(stateBeforeAttempt, runtimeStore.State);
        var retainedClaim = Assert.Single(runtimeStore.State.Claims);
        Assert.Equal(claimToken, retainedClaim.ClaimToken);
        Assert.Equal(DispatchClaimStatus.Claimed, retainedClaim.Status);
    }

    [Fact]
    public async Task BlockRecoveredExecutionClaimAsync_consumes_interrupted_claim_without_replay()
    {
        var now = new DateTimeOffset(2026, 7, 30, 12, 30, 0, TimeSpan.Zero);
        var runId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var stepId = ProcessStepInstanceId.New();
        var slotId = ArtifactSlotId.New();
        var claimToken = DispatchClaimToken.New();
        var assignment = CreateAssignment(runId, planId, stepId, slotId, now);
        var state = CreateClaimedState(
            runId,
            planId,
            stepId,
            slotId,
            claimToken,
            new DispatcherOwnerId("claim-recovery-block-test"),
            now);
        var executionRun = CreateCompletedExecutionRun(
            runId,
            stepId,
            claimToken,
            now.AddSeconds(1)) with
        {
            State = ExecutionState.Failed,
            Outcome = RunOutcome.Cancelled,
            ResultSummary = "Execution interrupted because the host restarted."
        };
        var runtimeStore = new RecordingRuntimeStore(state);
        var assignmentStore = new SingleAssignmentStore(assignment);
        var planStore = new SinglePlanStore(CreatePlan(planId, now));

        await using var projectionDbContext = CreateProjectionDbContext();
        var clock = new FixedProcessProjectionClock(now.AddSeconds(4));
        var coordinator = CreateCoordinator(
            new TestWorkspaceFactory(CreateWorkspaceService(executionRun)),
            clock,
            runtimeStore,
            planStore,
            assignmentStore,
            CreateProjectionCatchupService(projectionDbContext, clock),
            CreateCompletionCoordinator(new WorkspaceFileService(Path.GetTempPath())));

        var blocked = await coordinator.BlockRecoveredExecutionClaimAsync(
            executionRun,
            runId,
            stepId,
            "claim-recovery-block-test");

        Assert.True(blocked);
        Assert.Equal(ProcessRuntimeStatus.Active, runtimeStore.State.Status);
        var blockedStep = Assert.Single(runtimeStore.State.Steps);
        Assert.Equal(ProcessRuntimeStepStatus.Blocked, blockedStep.Status);
        Assert.Null(blockedStep.ActiveClaimToken);
        Assert.Equal(
            DispatchClaimStatus.Completed,
            Assert.Single(runtimeStore.State.Claims).Status);
        var receipt = Assert.Single(runtimeStore.State.AppliedResults);
        Assert.Equal(StrategyOutcome.NeedsManager, receipt.Outcome);
        Assert.Equal(ProcessRuntimeStepStatus.Blocked, receipt.AppliedStepStatus);
        Assert.Equal(executionRun.Id, receipt.ExecutionRunId?.Value);
        var diagnostic = Assert.Single(receipt.Diagnostics);
        Assert.Equal(
            ProcessExecutionAdapterDiagnosticCodes.AgentInterruptedExecutionReplayUnsafe,
            diagnostic.Code);
        Assert.Equal(ProcessDiagnosticRetrySafety.UnsafeToRetry, diagnostic.RetrySafety);
        Assert.Equal(
            ProcessDiagnosticIdempotencyClassification.Unknown,
            diagnostic.Idempotency);
    }

    [Fact]
    public async Task SubmitRecoveredExecutionResultAsync_materializes_and_accepts_missing_managed_artifact()
    {
        var now = new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);
        var runId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var stepId = ProcessStepInstanceId.New();
        var slotId = ArtifactSlotId.New();
        var claimToken = DispatchClaimToken.New();
        var ownerId = new DispatcherOwnerId("claim-recovery-test");
        var assignment = CreateAssignment(runId, planId, stepId, slotId, now);
        var state = CreateClaimedState(
            runId,
            planId,
            stepId,
            slotId,
            claimToken,
            ownerId,
            now);
        var executionRun = CreateCompletedExecutionRun(
            runId,
            stepId,
            claimToken,
            now.AddSeconds(1));
        var workspaceService = CreateWorkspaceService(executionRun);
        var workspaceFactory = new TestWorkspaceFactory(workspaceService);
        var runtimeStore = new RecordingRuntimeStore(state);
        var assignmentStore = new SingleAssignmentStore(assignment);
        var planStore = new SinglePlanStore(CreatePlan(planId, now));
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.ClaimRecovery.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            await using var projectionDbContext = CreateProjectionDbContext();
            var clock = new FixedProcessProjectionClock(now.AddSeconds(4));
            var projectionCatchupService = CreateProjectionCatchupService(
                projectionDbContext,
                clock);
            var completionCoordinator = CreateCompletionCoordinator(
                new WorkspaceFileService(workspaceRoot));
            var coordinator = CreateCoordinator(
                workspaceFactory,
                clock,
                runtimeStore,
                planStore,
                assignmentStore,
                projectionCatchupService,
                completionCoordinator);

            var recovered = await coordinator.SubmitRecoveredExecutionResultAsync(
                executionRun,
                runId,
                stepId,
                "claim-recovery-test");

            Assert.True(recovered);
            var appliedResult = Assert.Single(runtimeStore.State.AppliedResults);
            Assert.Equal(StrategyOutcome.Succeeded, appliedResult.Outcome);
            Assert.Equal(ProcessRuntimeStepStatus.Completed, appliedResult.AppliedStepStatus);
            var producedArtifact = Assert.Single(appliedResult.ProducedArtifacts);
            Assert.Equal(slotId, producedArtifact.SlotId);
            Assert.DoesNotContain(
                appliedResult.Diagnostics,
                diagnostic =>
                    diagnostic.Code ==
                    ProcessCompletionDiagnosticCodes.ManagedArtifactWriteReceiptMissing);

            var primaryRef =
                $"artifacts/process-runs/{runId.Value:D}/steps/artifact-producer.md";
            var primaryPath = Path.Combine(
                workspaceRoot,
                primaryRef.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(File.Exists(primaryPath));
            var content = await File.ReadAllTextAsync(primaryPath);
            Assert.Contains(
                ProcessManagedArtifactService.ManagedOutcomeArtifactCapturedHeading,
                content,
                StringComparison.Ordinal);
            Assert.Contains(
                ProcessManagedArtifactService.ManagedOutcomeArtifactAcceptedHeading,
                content,
                StringComparison.Ordinal);
            Assert.Equal(ComputeHash(content), producedArtifact.ContentHash);
        }
        finally
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task SubmitRecoveredExecutionResultAsync_rejects_execution_completed_after_claim_expiry_without_mutating_runtime_state()
    {
        var now = new DateTimeOffset(2026, 7, 29, 13, 0, 0, TimeSpan.Zero);
        var runId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var stepId = ProcessStepInstanceId.New();
        var slotId = ArtifactSlotId.New();
        var claimToken = DispatchClaimToken.New();
        var ownerId = new DispatcherOwnerId("claim-recovery-expiry-test");
        var assignment = CreateAssignment(runId, planId, stepId, slotId, now);
        var state = CreateClaimedState(
            runId,
            planId,
            stepId,
            slotId,
            claimToken,
            ownerId,
            now);
        var completedAfterClaimExpiry = now.AddMinutes(10).AddTicks(1);
        var executionRun = CreateCompletedExecutionRun(
            runId,
            stepId,
            claimToken,
            now.AddSeconds(1)) with
        {
            UpdatedAtUtc = completedAfterClaimExpiry,
            CompletedAtUtc = completedAfterClaimExpiry
        };
        var workspaceFactory = new TestWorkspaceFactory(
            CreateWorkspaceService(executionRun));
        var runtimeStore = new RecordingRuntimeStore(state);
        var assignmentStore = new SingleAssignmentStore(assignment);
        var planStore = new SinglePlanStore(CreatePlan(planId, now));
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.ClaimRecoveryExpired.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            await using var projectionDbContext = CreateProjectionDbContext();
            var clock = new FixedProcessProjectionClock(completedAfterClaimExpiry);
            var coordinator = CreateCoordinator(
                workspaceFactory,
                clock,
                runtimeStore,
                planStore,
                assignmentStore,
                CreateProjectionCatchupService(projectionDbContext, clock),
                CreateCompletionCoordinator(new WorkspaceFileService(workspaceRoot)));
            var stateBeforeAttempt = runtimeStore.State;

            var recovered = await coordinator.SubmitRecoveredExecutionResultAsync(
                executionRun,
                runId,
                stepId,
                "claim-recovery-expiry-test");

            Assert.False(recovered);
            Assert.Same(stateBeforeAttempt, runtimeStore.State);
            Assert.Empty(runtimeStore.State.AppliedResults);
            var retainedClaim = Assert.Single(runtimeStore.State.Claims);
            Assert.Equal(claimToken, retainedClaim.ClaimToken);
            Assert.Equal(DispatchClaimStatus.Claimed, retainedClaim.Status);
            var retainedStep = Assert.Single(runtimeStore.State.Steps);
            Assert.Equal(ProcessRuntimeStepStatus.Running, retainedStep.Status);
            Assert.Equal(claimToken, retainedStep.ActiveClaimToken);
            Assert.False(File.Exists(Path.Combine(
                workspaceRoot,
                "artifacts",
                "process-runs",
                runId.Value.ToString("D"),
                "steps",
                "artifact-producer.md")));
        }
        finally
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Recovery_rejects_execution_bound_to_different_claim_without_mutating_runtime_state()
    {
        var now = new DateTimeOffset(2026, 7, 30, 13, 0, 0, TimeSpan.Zero);
        var runId = ProcessRunId.New();
        var planId = ProcessInstancePlanId.New();
        var stepId = ProcessStepInstanceId.New();
        var slotId = ArtifactSlotId.New();
        var activeClaimToken = DispatchClaimToken.New();
        var assignment = CreateAssignment(runId, planId, stepId, slotId, now);
        var state = CreateClaimedState(
            runId,
            planId,
            stepId,
            slotId,
            activeClaimToken,
            new DispatcherOwnerId("claim-recovery-mismatch-test"),
            now);
        var executionRun = CreateCompletedExecutionRun(
            runId,
            stepId,
            DispatchClaimToken.New(),
            now.AddSeconds(1));
        var runtimeStore = new RecordingRuntimeStore(state);
        var assignmentStore = new SingleAssignmentStore(assignment);
        var planStore = new SinglePlanStore(CreatePlan(planId, now));
        var workspaceRoot = Path.Combine(
            Path.GetTempPath(),
            $"CanDoItAll.ClaimRecoveryMismatch.{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspaceRoot);

        try
        {
            await using var projectionDbContext = CreateProjectionDbContext();
            var clock = new FixedProcessProjectionClock(now.AddSeconds(4));
            var projectionCatchupService = CreateProjectionCatchupService(
                projectionDbContext,
                clock);
            var coordinator = CreateCoordinator(
                new TestWorkspaceFactory(CreateWorkspaceService(executionRun)),
                clock,
                runtimeStore,
                planStore,
                assignmentStore,
                projectionCatchupService,
                CreateCompletionCoordinator(new WorkspaceFileService(workspaceRoot)));

            var failedExecution = executionRun with
            {
                State = ExecutionState.Failed,
                Outcome = RunOutcome.Cancelled
            };
            var blocked = await coordinator.BlockRecoveredExecutionClaimAsync(
                failedExecution,
                runId,
                stepId,
                "claim-recovery-mismatch-test");
            var submitted = await coordinator.SubmitRecoveredExecutionResultAsync(
                executionRun,
                runId,
                stepId,
                "claim-recovery-mismatch-test");
            var wrongStepExecution = CreateCompletedExecutionRun(
                runId,
                ProcessStepInstanceId.New(),
                activeClaimToken,
                now.AddSeconds(1));
            var wrongStepBlocked = await coordinator.BlockRecoveredExecutionClaimAsync(
                wrongStepExecution with
                {
                    State = ExecutionState.Failed,
                    Outcome = RunOutcome.Cancelled
                },
                runId,
                stepId,
                "claim-recovery-mismatch-test");
            var wrongStepSubmitted = await coordinator.SubmitRecoveredExecutionResultAsync(
                wrongStepExecution,
                runId,
                stepId,
                "claim-recovery-mismatch-test");

            Assert.False(blocked);
            Assert.False(submitted);
            Assert.False(wrongStepBlocked);
            Assert.False(wrongStepSubmitted);
            Assert.Empty(runtimeStore.State.AppliedResults);
            var retainedClaim = Assert.Single(runtimeStore.State.Claims);
            Assert.Equal(activeClaimToken, retainedClaim.ClaimToken);
            Assert.Equal(DispatchClaimStatus.Claimed, retainedClaim.Status);
            Assert.False(File.Exists(Path.Combine(
                workspaceRoot,
                "artifacts",
                "process-runs",
                runId.Value.ToString("D"),
                "steps",
                "artifact-producer.md")));
        }
        finally
        {
            if (Directory.Exists(workspaceRoot))
            {
                Directory.Delete(workspaceRoot, recursive: true);
            }
        }
    }

    private static AgentFrameworkProcessExecutionClaimRecoveryCoordinator CreateCoordinator(
        ICanDoItAllAgentWorkspaceFactory workspaceFactory,
        IProcessProjectionClock clock,
        RecordingRuntimeStore runtimeStore,
        SinglePlanStore planStore,
        SingleAssignmentStore assignmentStore,
        ProcessRuntimeProjectionCatchupService projectionCatchupService,
        ProcessStepCompletionCoordinator completionCoordinator)
    {
        var branchSignalRouter = new ProcessRuntimeBranchSignalApplicationService(
            clock,
            runtimeStore,
            runtimeStore,
            assignmentStore,
            projectionCatchupService);
        return new AgentFrameworkProcessExecutionClaimRecoveryCoordinator(
            workspaceFactory,
            clock,
            runtimeStore,
            planStore,
            assignmentStore,
            runtimeStore,
            new ProcessRuntimeDispatchQueue(),
            branchSignalRouter,
            projectionCatchupService,
            completionCoordinator,
            NullLogger<AgentFrameworkProcessExecutionClaimRecoveryCoordinator>.Instance);
    }

    private static ProcessRuntimeProjectionCatchupService CreateProjectionCatchupService(
        ProcessPersistenceDbContext projectionDbContext,
        IProcessProjectionClock clock)
    {
        var projectionStore = new EfProcessProjectionStore(projectionDbContext);
        return new ProcessRuntimeProjectionCatchupService(
            new EmptyRuntimeEventReplayStore(),
            projectionStore,
            new ProcessRuntimeProjectionProjector(
                projectionStore,
                ProcessProjectionJsonCodec.Default,
                clock,
                new EfProcessRunRecordStore(projectionDbContext)),
            clock);
    }

    private static ProcessRuntimeStepAssignment CreateAssignment(
        ProcessRunId runId,
        ProcessInstancePlanId planId,
        ProcessStepInstanceId stepId,
        ArtifactSlotId slotId,
        DateTimeOffset createdAtUtc)
    {
        return new ProcessRuntimeStepAssignment(
            runId,
            planId,
            stepId,
            "artifact-producer",
            "process-agent",
            "process-agent",
            "Process agent",
            ProcessLaunchExecutorKinds.Agent,
            Guid.NewGuid().ToString("D"),
            "Process Agent",
            "Produce current-run managed process evidence.",
            "sha256:claim-recovery-readiness",
            "Selected for generic artifact production.",
            [slotId],
            [],
            [ProcessOperationContractNames.WriteManagedProcessArtifacts],
            ProcessOperationContractNames.ExternalProductTargetReadOnly,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            BranchGate: null,
            createdAtUtc);
    }

    private static ProcessRuntimeStateSnapshot CreateClaimedState(
        ProcessRunId runId,
        ProcessInstancePlanId planId,
        ProcessStepInstanceId stepId,
        ArtifactSlotId slotId,
        DispatchClaimToken claimToken,
        DispatcherOwnerId ownerId,
        DateTimeOffset createdAtUtc)
    {
        return new ProcessRuntimeStateSnapshot(
            runId,
            runId,
            planId,
            "sha256:claim-recovery-plan",
            ProcessRuntimeStatus.Active,
            [
                new ProcessRuntimeStepState(
                    stepId,
                    ProcessStepDefinitionId.New(),
                    ProcessRuntimeStepStatus.Running,
                    IsExecutable: true,
                    AttemptNumber: 1,
                    DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
                    RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
                    ActiveClaimToken: claimToken,
                    CompletedResultKey: null)
                {
                    ProducedArtifactSlots = new HashSet<ArtifactSlotId> { slotId }
                }
            ],
            [
                new DispatchClaimState(
                    claimToken,
                    stepId,
                    ownerId,
                    DispatchClaimStatus.Claimed,
                    AttemptNumber: 1,
                    createdAtUtc,
                    createdAtUtc.AddMinutes(10),
                    RenewedAtUtc: null,
                    ResultIdempotencyKey: null)
            ],
            [],
            new HashSet<ArtifactSlotId>(),
            createdAtUtc);
    }

    private static ExecutionRunRecord CreateCompletedExecutionRun(
        ProcessRunId runId,
        ProcessStepInstanceId stepId,
        DispatchClaimToken claimToken,
        DateTimeOffset createdAtUtc)
    {
        var outcome = new ProcessStepOutcomeResult
        {
            Status = ProcessStepOutcomeStatus.Completed,
            Reason = "The current execution produced independently grounded completion proof.",
            EvidenceRefs = [$"execution://{Guid.NewGuid():D}"],
            NextActions = [],
            HumanReadableSummaryMarkdown =
                "The typed recovered outcome is ready for runtime-managed artifact materialization."
        };
        var resultSummary = JsonSerializer.Serialize(
            outcome,
            AgentOutputJson.SerializerOptions);

        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            ChatSessionId: null,
            Title: "Recovered process step",
            SourceKind: "process-step",
            SourceId: "artifact-producer",
            CorrelationId: Guid.NewGuid().ToString("N"),
            CausationId: Guid.NewGuid().ToString("N"),
            RequestedBy: "process-runtime",
            RequestedByKind: "system",
            MetadataJson: JsonSerializer.Serialize(
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProcessDispatchClaimExecutionMetadata.MetadataKey] =
                        claimToken.Value.ToString("D")
                },
                AgentOutputJson.SerializerOptions),
            InputSummary: "Produce current-run process evidence.",
            ResultSummary: resultSummary,
            ProviderName: "test-provider",
            Model: "test-model",
            State: ExecutionState.Completed,
            Outcome: RunOutcome.Succeeded,
            CreatedAtUtc: createdAtUtc,
            UpdatedAtUtc: createdAtUtc.AddSeconds(2),
            StartedAtUtc: createdAtUtc,
            CompletedAtUtc: createdAtUtc.AddSeconds(2),
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: [],
            ProcessRunId: runId.Value.ToString("D"),
            ProcessStepId: stepId.Value.ToString("D"));
    }

    private static ProcessStepCompletionCoordinator CreateCompletionCoordinator(
        IWorkspaceFileService workspaceFiles)
    {
        var toolReceiptPolicies = new ProcessToolReceiptPolicyCatalog(
        [
            new GenericWorkspaceToolReceiptPolicyContribution()
        ]);
        var completionIssueResultFactory = new ProcessCompletionIssueResultFactory(
            workspaceFiles,
            ProcessCompletionDefectEvidenceCatalog.Empty);
        var completionGateEvaluator = new ProcessCompletionGateFactory(
                toolReceiptPolicies,
                new ProcessToolReceiptEvidenceGate(workspaceFiles, []),
                [],
                completionIssueResultFactory,
                new ProcessOutcomeGroundingValidator(workspaceFiles))
            .CreateCompletionGateEvaluator();
        return new ProcessStepCompletionCoordinator(
            completionIssueResultFactory,
            new ProcessManagedArtifactService(workspaceFiles),
            new ProcessOutcomeGroundingValidator(workspaceFiles),
            completionGateEvaluator,
            new ProcessExecutionResultConverter(
                completionGateEvaluator,
                toolReceiptPolicies,
                completionIssueResultFactory),
            NullLogger<ProcessStepCompletionCoordinator>.Instance);
    }

    private static ProcessInstancePlan CreatePlan(
        ProcessInstancePlanId planId,
        DateTimeOffset createdAtUtc)
    {
        return new ProcessInstancePlan(
            new ProcessInstancePlanHeader(
                planId,
                planId,
                ParentPlanId: null,
                ParentStepId: null,
                "processes.instance-plan.v1",
                createdAtUtc,
                HierarchyDepth: 0),
            new ResolvedProcessDefinitionSnapshot(
                ProcessDefinitionId.New(),
                ProcessDefinitionVersionId.New(),
                "sha256:claim-recovery-definition",
                "template/1",
                "template/1",
                [],
                [],
                []),
            new DriverStackSnapshot([]),
            new StrategyBindingSet([], [], [], []),
            [],
            new ArtifactPlan([], []),
            new BranchRouteTable([]),
            [],
            new ManagerPlan("sha256:manager-policy", null, [], []),
            new BudgetPlan([]),
            new MonitoringPlan(true, "sha256:monitoring"),
            new SecurityPlan("sha256:security", []),
            "sha256:claim-recovery-plan");
    }

    private static IAgentFrameworkWorkspaceService CreateWorkspaceService(
        ExecutionRunRecord executionRun)
    {
        var service = DispatchProxy.Create<
            IAgentFrameworkWorkspaceService,
            ExecutionHistoryWorkspaceProxy>();
        var proxy = (ExecutionHistoryWorkspaceProxy)(object)service;
        proxy.ExecutionRun = executionRun;
        proxy.ExecutionDetail = new ExecutionRunDetail(
            executionRun,
            ChatSession: null,
            ExecutionLog: [],
            Metrics: [])
        {
            ToolReceipts = []
        };
        return service;
    }

    private static ProcessPersistenceDbContext CreateProjectionDbContext()
    {
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>()
            .UseInMemoryDatabase($"claim-recovery-projection-{Guid.NewGuid():N}")
            .Options;
        return new ProcessPersistenceDbContext(options);
    }

    private static string ComputeHash(string content)
        => "sha256:" +
           Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)))
               .ToLowerInvariant();

    private sealed class RecordingRuntimeStore(ProcessRuntimeStateSnapshot state) :
        IProcessRuntimeStateStore,
        IProcessRuntimeUnitOfWork
    {
        public ProcessRuntimeStateSnapshot State { get; private set; } = state;

        public Task<ProcessRuntimeStateSnapshot?> LoadAsync(
            ProcessRunId runId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<ProcessRuntimeStateSnapshot?>(
                runId == State.RunId ? State : null);
        }

        public Task<ProcessRuntimeCommitResult> CommitAsync(
            ProcessRuntimeCommitRequest request,
            CancellationToken cancellationToken = default)
        {
            State = request.Mutation.State;
            return Task.FromResult(
                ProcessRuntimeCommitResult.FromMutation(request.Mutation));
        }
    }

    private sealed class SingleAssignmentStore(ProcessRuntimeStepAssignment assignment) :
        IProcessRuntimeStepAssignmentStore
    {
        public ValueTask SaveAsync(
            IReadOnlyList<ProcessRuntimeStepAssignment> assignments,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> LoadByRunAsync(
            ProcessRunId runId,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ProcessRuntimeStepAssignment> result =
                runId == assignment.RunId ? [assignment] : [];
            return ValueTask.FromResult(result);
        }

        public ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>>
            FindByLaunchVariablesAsync(
                IReadOnlyDictionary<string, string> requiredVariables,
                CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<ProcessRuntimeStepAssignment>>([]);

        public ValueTask<ProcessRuntimeStepAssignment?> LoadAsync(
            ProcessRunId runId,
            ProcessStepInstanceId stepInstanceId,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<ProcessRuntimeStepAssignment?>(
                runId == assignment.RunId &&
                stepInstanceId == assignment.StepInstanceId
                    ? assignment
                    : null);
        }
    }

    private sealed class SinglePlanStore(ProcessInstancePlan plan) :
        IProcessInstancePlanStore
    {
        public ValueTask<PersistedProcessInstancePlan> PersistAsync(
            ProcessInstancePlan processPlan,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(
                new PersistedProcessInstancePlan(
                    processPlan.Header.PlanId,
                    processPlan.PlanHash));
        }

        public ValueTask<ProcessInstancePlan?> LoadAsync(
            ProcessInstancePlanId planId,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<ProcessInstancePlan?>(
                planId == plan.Header.PlanId ? plan : null);
        }
    }

    private sealed class TestWorkspaceFactory(
        IAgentFrameworkWorkspaceService workspaceService) :
        ICanDoItAllAgentWorkspaceFactory
    {
        public IAgentFrameworkWorkspaceService GetOrganizationWorkspaceService()
            => workspaceService;

        public IAgentFrameworkWorkspaceService GetWorkspaceService(
            WorkspaceScopeDescriptor scope)
            => workspaceService;

        public WorkspaceScopeDescriptor GetOrganizationScope()
            => WorkspaceScopeDescriptor.Organization("claim-recovery-test");

        public string GetWorkspaceRoot()
            => Path.GetTempPath();
    }

    private class ExecutionHistoryWorkspaceProxy : DispatchProxy
    {
        public required ExecutionRunRecord ExecutionRun { get; set; }

        public required ExecutionRunDetail ExecutionDetail { get; set; }

        protected override object? Invoke(
            MethodInfo? targetMethod,
            object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            args ??= [];

            if (targetMethod.Name ==
                nameof(IAgentFrameworkWorkspaceService.GetExecutionRunDetailAsync))
            {
                Assert.Equal(ExecutionRun.Id, Assert.IsType<Guid>(args[0]));
                return Task.FromResult(ExecutionDetail);
            }

            if (targetMethod.Name ==
                nameof(IAgentFrameworkWorkspaceService.ListExecutionRunsAsync))
            {
                var query = Assert.IsType<ExecutionRunQuery>(args[0]);
                Assert.Equal(ExecutionRun.ProcessRunId, query.ProcessRunId);
                Assert.Equal(ExecutionRun.ProcessStepId, query.ProcessStepId);
                return Task.FromResult<IReadOnlyList<ExecutionRunRecord>>(
                    [ExecutionRun]);
            }

            throw new NotSupportedException(
                $"Unexpected workspace call '{targetMethod.Name}'.");
        }
    }

    private sealed class EmptyRuntimeEventReplayStore :
        IProcessRuntimeEventReplayStore
    {
        public Task<IReadOnlyList<ProcessStoredRuntimeEvent>>
            ReadAfterGlobalSequenceAsync(
                long globalSequenceExclusive,
                int take,
                CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProcessStoredRuntimeEvent>>([]);

        public Task<IReadOnlyList<ProcessStoredRuntimeEvent>> ReadByRootRunAsync(
            ProcessRunId rootRunId,
            long rootSequenceExclusive,
            int take,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProcessStoredRuntimeEvent>>([]);
    }

    private sealed class FixedProcessProjectionClock(DateTimeOffset utcNow) :
        IProcessProjectionClock
    {
        public DateTimeOffset GetUtcNow() => utcNow;
    }
}
