using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessManagerControlLoopTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly ProcessRunId RootRunId = new(new Guid("7ef9b46f-f257-4f26-a2a0-e0ebb9848107"));
    private static readonly ProcessRunId RunId = new(new Guid("719c3bed-33f2-43ec-8238-0dff8e988580"));
    private static readonly ProcessStepInstanceId StepInstanceId = new(new Guid("0d65d702-5bbf-4908-b850-081db93537a7"));
    private static readonly ProcessStepDefinitionId StepDefinitionId = new(new Guid("cf1aab1d-a8c0-4b3d-a67b-25355586a558"));
    private static readonly ArtifactSlotId ArtifactSlotId = new(new Guid("764bd81e-4c8d-4da6-b4b4-157ff298f28f"));
    private static readonly ArtifactSlotId ChildArtifactSlotId = new(new Guid("cc7f618f-caa5-41e0-9550-9d67885cf77e"));
    private static readonly RuntimeEventId SourceEventId = new(new Guid("946b526b-6624-4c35-9d10-402074dc623d"));
    private static readonly ProcessCorrelationId CorrelationId = new("correlation.manager");

    [Fact]
    public async Task Missing_artifact_recovery_records_sanitized_incident_and_dispatch_handoff()
    {
        var stores = new ManagerTestStores(new AllowingRecoveryPolicy());
        var manager = new ProcessManagerControlLoop(stores.Dependencies);

        var incidentResult = await manager.RaiseIncidentAsync(MissingArtifactSignal("token=restricted-value"));
        var recoveryResult = await manager.EvaluateRecoveryAsync(NewRecoveryRequest(incidentResult.Incident.IncidentId));

        Assert.True(incidentResult.Succeeded);
        Assert.Equal(ProcessIncidentStatus.AwaitingPolicy, incidentResult.Incident.Status);
        Assert.Equal(ProcessIncidentClassification.MissingArtifact, incidentResult.Incident.Classification);
        Assert.Equal(ProcessRuntimeEventTypes.ManagerIncidentRaised, incidentResult.DecisionEvent.EventType);
        Assert.DoesNotContain("restricted-value", incidentResult.Incident.SafeContent.Summary, StringComparison.Ordinal);
        Assert.Single(stores.Diagnostics.Evidence);

        Assert.True(recoveryResult.Succeeded);
        Assert.Equal(ProcessRecoveryRequestStatus.Scheduled, recoveryResult.RecoveryRequest.Status);
        Assert.NotNull(recoveryResult.DispatchHandoff);
        Assert.Equal(ProcessRuntimeEventTypes.ManagerRecoveryApproved, recoveryResult.DecisionEvent.EventType);
        Assert.Equal(1, stores.LoopBudgets.ConsumptionCount(recoveryResult.RecoveryRequest.LoopFingerprintId));
    }

    [Fact]
    public async Task Stale_artifact_incident_uses_stale_classification_and_restricted_reference()
    {
        var stores = new ManagerTestStores(new AllowingRecoveryPolicy());
        var manager = new ProcessManagerControlLoop(stores.Dependencies);

        var result = await manager.RaiseIncidentAsync(MissingArtifactSignal("previous version expired") with
        {
            IncidentId = ProcessIncidentId.New(),
            Classification = ProcessIncidentClassification.StaleArtifact,
            SafeContent = new ProcessIncidentSafeContent("Artifact needs review", "The artifact is stale and needs revalidation.")
        });

        Assert.True(result.Succeeded);
        Assert.Equal(ProcessIncidentClassification.StaleArtifact, result.Incident.Classification);
        Assert.Equal(ProcessEventSensitivity.Restricted, result.Incident.DiagnosticReference.Sensitivity);
        Assert.DoesNotContain("previous version expired", result.Incident.SafeContent.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Branch_decision_is_idempotent_and_does_not_consume_loop_twice()
    {
        var stores = new ManagerTestStores(new AllowingRecoveryPolicy());
        var manager = new ProcessManagerControlLoop(stores.Dependencies);
        var request = BackwardBranchRequest(new ProcessManagerIdempotencyKey("branch-repeat"));

        var first = await manager.RecordBranchDecisionAsync(new ProcessBranchDecisionCommand(
            request,
            new BranchOutcomeId("selected"),
            0.91m,
            null,
            Now));
        var second = await manager.RecordBranchDecisionAsync(new ProcessBranchDecisionCommand(
            request,
            new BranchOutcomeId("selected"),
            0.91m,
            null,
            Now.AddSeconds(1)));

        Assert.True(first.Succeeded);
        Assert.True(second.IsDuplicate);
        Assert.Equal(first.BranchDecision.DecisionId, second.BranchDecision.DecisionId);
        Assert.NotNull(first.RouteHandoff);
        Assert.Equal(ProcessRouteTargetKind.PreviousStep, first.RouteHandoff.RouteTarget.Kind);
        Assert.Equal(1, stores.LoopBudgets.ConsumptionCount(first.BranchDecision.LoopFingerprintId!.Value));
        Assert.Single(stores.BranchDecisions.Decisions);
    }

    [Fact]
    public async Task Backward_branch_escalates_when_loop_budget_is_exhausted()
    {
        var stores = new ManagerTestStores(new AllowingRecoveryPolicy());
        var manager = new ProcessManagerControlLoop(stores.Dependencies);

        var first = await manager.RecordBranchDecisionAsync(new ProcessBranchDecisionCommand(
            BackwardBranchRequest(new ProcessManagerIdempotencyKey("branch-first"), maximumRepeats: 1),
            new BranchOutcomeId("selected"),
            0.80m,
            null,
            Now));
        var second = await manager.RecordBranchDecisionAsync(new ProcessBranchDecisionCommand(
            BackwardBranchRequest(new ProcessManagerIdempotencyKey("branch-second"), maximumRepeats: 1),
            new BranchOutcomeId("selected"),
            0.80m,
            null,
            Now.AddSeconds(1)));

        Assert.True(first.Succeeded);
        Assert.False(second.Succeeded);
        Assert.Equal(ProcessBranchDecisionStatus.Escalated, second.BranchDecision.Status);
        Assert.Equal(ProcessRouteTargetKind.Escalate, second.BranchDecision.RouteTarget.Kind);
        Assert.Equal(ProcessRuntimeEventTypes.ManagerLoopBudgetEscalated, second.DecisionEvent.EventType);
    }

    [Fact]
    public async Task Subprocess_artifact_projection_message_is_durable_and_correlated()
    {
        var stores = new ManagerTestStores(new AllowingRecoveryPolicy());
        var manager = new ProcessManagerControlLoop(stores.Dependencies);
        var childRunId = ProcessRunId.New();
        var draft = new ProcessSubprocessMessageDraft(
            ProcessSubprocessMessageId.New(),
            ProcessSubprocessMessageKind.ArtifactProjectionRequest,
            ProcessSubprocessMessageDirection.ParentToChild,
            RootRunId,
            childRunId,
            StepInstanceId,
            CorrelationId,
            SourceEventId,
            "subprocess-message.v1",
            ProcessEventSensitivity.Normal,
            [
                new ProcessArtifactProjectionReference(
                    ArtifactSlotId,
                    ChildArtifactSlotId,
                    ProcessArtifactScope.Parent,
                    ProcessArtifactScope.Child,
                    "sha256:artifact")
            ],
            new ProcessManagerIdempotencyKey("subprocess-message"),
            "sha256:subprocess-message",
            Now);

        var result = await manager.SendSubprocessMessageAsync(draft);

        Assert.True(result.Succeeded);
        Assert.Equal(ProcessRuntimeEventTypes.ManagerSubprocessMessageQueued, result.DecisionEvent.EventType);
        Assert.Single(stores.SubprocessMessages.Messages);
        Assert.Empty(stores.Incidents.Incidents);
        var workItem = Assert.Single(stores.Queue.Items);
        Assert.Equal(ProcessManagerWorkItemKind.SubprocessMessage, workItem.Kind);
        Assert.Equal(SourceEventId, workItem.CausationId);
    }

    [Fact]
    public async Task Incident_projection_content_never_contains_restricted_diagnostic_detail()
    {
        const string restrictedDetail = "DATABASE_URL=postgres://secret";
        var stores = new ManagerTestStores(new AllowingRecoveryPolicy());
        var manager = new ProcessManagerControlLoop(stores.Dependencies);

        var result = await manager.RaiseIncidentAsync(MissingArtifactSignal(restrictedDetail));

        Assert.True(result.Succeeded);
        Assert.DoesNotContain("DATABASE_URL", result.Incident.SafeContent.Title, StringComparison.Ordinal);
        Assert.DoesNotContain("DATABASE_URL", result.Incident.SafeContent.Summary, StringComparison.Ordinal);
        Assert.Equal("sha256:diagnostic", result.Incident.DiagnosticReference.EvidenceHash);
    }

    [Fact]
    public async Task Recovery_policy_denial_records_denial_without_dispatch_or_budget_consumption()
    {
        var stores = new ManagerTestStores(new DenyingRecoveryPolicy());
        var manager = new ProcessManagerControlLoop(stores.Dependencies);
        var incident = await manager.RaiseIncidentAsync(MissingArtifactSignal("policy denies recovery"));

        var result = await manager.EvaluateRecoveryAsync(NewRecoveryRequest(incident.Incident.IncidentId));

        Assert.False(result.Succeeded);
        Assert.Null(result.DispatchHandoff);
        Assert.Equal(ProcessRecoveryRequestStatus.Denied, result.RecoveryRequest.Status);
        Assert.Equal(ProcessRuntimeEventTypes.ManagerRecoveryDenied, result.DecisionEvent.EventType);
        Assert.Equal(0, stores.LoopBudgets.ConsumptionCount(result.RecoveryRequest.LoopFingerprintId));
    }

    [Fact]
    public void Control_loop_dependencies_do_not_expose_runtime_mutation_ports()
    {
        var dependencyTypes = typeof(ProcessManagerRuntimeDependencies)
            .GetProperties()
            .Select(property => property.PropertyType)
            .ToArray();

        Assert.DoesNotContain(typeof(IProcessRuntimeUnitOfWork), dependencyTypes);
        Assert.DoesNotContain(typeof(IProcessRuntimeStateStore), dependencyTypes);
    }

    private static ProcessIncidentSignal MissingArtifactSignal(string restrictedDetail)
    {
        return new ProcessIncidentSignal(
            ProcessIncidentId.New(),
            RootRunId,
            RunId,
            SourceEventId,
            StepInstanceId,
            ArtifactSlotId,
            ProcessIncidentClassification.MissingArtifact,
            ProcessIncidentSeverity.Error,
            new ProcessRestrictedDiagnosticEvidence(
                restrictedDetail,
                "sha256:diagnostic",
                ProcessEventSensitivity.Restricted),
            new ProcessIncidentSafeContent(
                "Artifact missing",
                "A required artifact is missing and recovery may resupply it."),
            new HashSet<ProcessRecoveryActionKind> { ProcessRecoveryActionKind.ResupplyArtifact },
            new ProcessManagerIdempotencyKey($"incident-{Guid.NewGuid():N}"),
            CorrelationId,
            Now,
            "sha256:incident");
    }

    private static ProcessRecoveryEvaluationRequest NewRecoveryRequest(ProcessIncidentId incidentId)
    {
        return new ProcessRecoveryEvaluationRequest(
            ProcessRecoveryRequestId.New(),
            incidentId,
            ProcessRecoveryActionKind.ResupplyArtifact,
            new ProcessManagerIdempotencyKey($"recovery-{Guid.NewGuid():N}"),
            new ProcessLoopFingerprintId("loop-missing-artifact"),
            2,
            ApprovalGranted: true,
            StrategyAllowsRepeat: true,
            Now,
            "sha256:recovery");
    }

    private static ProcessBranchDecisionRequest BackwardBranchRequest(
        ProcessManagerIdempotencyKey idempotencyKey,
        int maximumRepeats = 2)
    {
        var loopBudget = new LoopBudgetDefinition(
            maximumRepeats,
            new LoopFingerprintPolicyId("path-and-evidence"),
            new ProcessRouteTarget(ProcessRouteTargetKind.Escalate));
        return new ProcessBranchDecisionRequest(
            ProcessBranchDecisionRequestId.New(),
            RootRunId,
            RunId,
            StepInstanceId,
            StepDefinitionId,
            new BranchFamilyId("typed-family"),
            [
                new BranchOutcomeDefinition(
                    new BranchOutcomeId("selected"),
                    "User-facing label can change",
                    BranchOutcomeCategory.Repeat,
                    new ProcessRouteTarget(ProcessRouteTargetKind.PreviousStep),
                    loopBudget)
            ],
            ["artifact:764bd81e"],
            idempotencyKey,
            CorrelationId,
            SourceEventId,
            "sha256:branch-request");
    }

    private sealed class ManagerTestStores(IProcessRecoveryPolicy recoveryPolicy)
    {
        public RecordingDiagnosticEvidenceStore Diagnostics { get; } = new();
        public RecordingIncidentStore Incidents { get; } = new();
        public RecordingManagerQueue Queue { get; } = new();
        public RecordingRecoveryRequestStore RecoveryRequests { get; } = new();
        public RecordingBranchDecisionStore BranchDecisions { get; } = new();
        public RecordingLoopBudgetLedger LoopBudgets { get; } = new();
        public RecordingSubprocessMessageStore SubprocessMessages { get; } = new();
        public RecordingManagerDecisionStore Decisions { get; } = new();

        public ProcessManagerRuntimeDependencies Dependencies => new(
            Diagnostics,
            Incidents,
            Queue,
            recoveryPolicy,
            RecoveryRequests,
            BranchDecisions,
            LoopBudgets,
            SubprocessMessages,
            Decisions);
    }

    private sealed class RecordingDiagnosticEvidenceStore : IProcessDiagnosticEvidenceStore
    {
        public List<ProcessRestrictedDiagnosticEvidence> Evidence { get; } = [];

        public Task<ProcessDiagnosticReference> StoreAsync(
            ProcessRunId runId,
            RuntimeEventId sourceEventId,
            ProcessRestrictedDiagnosticEvidence evidence,
            CancellationToken cancellationToken = default)
        {
            Evidence.Add(evidence);
            return Task.FromResult(new ProcessDiagnosticReference(
                ProcessDiagnosticReferenceId.New(),
                evidence.Sensitivity,
                evidence.EvidenceHash,
                $"diagnostic://{sourceEventId}"));
        }
    }

    private sealed class RecordingIncidentStore : IProcessIncidentStore
    {
        public List<ProcessIncident> Incidents { get; } = [];

        public Task<ProcessIncident?> FindByIdempotencyKeyAsync(
            ProcessRunId runId,
            ProcessManagerIdempotencyKey idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Incidents.SingleOrDefault(incident =>
                incident.RunId == runId &&
                incident.IdempotencyKey == idempotencyKey));
        }

        public Task<ProcessIncident?> LoadAsync(
            ProcessIncidentId incidentId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Incidents.SingleOrDefault(incident => incident.IncidentId == incidentId));
        }

        public Task SaveAsync(
            ProcessIncident incident,
            CancellationToken cancellationToken = default)
        {
            Incidents.Add(incident);
            return Task.CompletedTask;
        }

        public Task UpdateStatusAsync(
            ProcessIncidentId incidentId,
            ProcessIncidentStatus status,
            RuntimeEventId? resolutionEventId,
            CancellationToken cancellationToken = default)
        {
            var index = Incidents.FindIndex(incident => incident.IncidentId == incidentId);
            Incidents[index] = Incidents[index] with
            {
                Status = status,
                ResolutionEventId = resolutionEventId
            };
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingManagerQueue : IProcessManagerQueue
    {
        public List<ProcessManagerWorkItem> Items { get; } = [];

        public Task<ProcessManagerWorkItem?> FindByIdempotencyKeyAsync(
            ProcessRunId runId,
            ProcessManagerIdempotencyKey idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Items.SingleOrDefault(item =>
                item.RunId == runId &&
                item.IdempotencyKey == idempotencyKey));
        }

        public Task EnqueueAsync(
            ProcessManagerWorkItem item,
            CancellationToken cancellationToken = default)
        {
            Items.Add(item);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingRecoveryRequestStore : IProcessRecoveryRequestStore
    {
        public List<ProcessRecoveryRequest> Requests { get; } = [];

        public Task<ProcessRecoveryRequest?> FindByIdempotencyKeyAsync(
            ProcessRunId runId,
            ProcessManagerIdempotencyKey idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Requests.SingleOrDefault(request =>
                request.RunId == runId &&
                request.IdempotencyKey == idempotencyKey));
        }

        public Task SaveAsync(
            ProcessRecoveryRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingBranchDecisionStore : IProcessBranchDecisionStore
    {
        public List<ProcessBranchDecision> Decisions { get; } = [];

        public Task<ProcessBranchDecision?> FindByIdempotencyKeyAsync(
            ProcessRunId runId,
            ProcessBranchDecisionRequestId requestId,
            ProcessManagerIdempotencyKey idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Decisions.SingleOrDefault(decision =>
                decision.RunId == runId &&
                decision.RequestId == requestId &&
                decision.IdempotencyKey == idempotencyKey));
        }

        public Task SaveAsync(
            ProcessBranchDecision decision,
            CancellationToken cancellationToken = default)
        {
            Decisions.Add(decision);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingLoopBudgetLedger : IProcessLoopBudgetLedger
    {
        private readonly Dictionary<ProcessLoopFingerprintId, int> counts = [];
        private readonly HashSet<ProcessManagerIdempotencyKey> consumedKeys = [];

        public int ConsumptionCount(ProcessLoopFingerprintId fingerprintId)
        {
            return counts.TryGetValue(fingerprintId, out var count) ? count : 0;
        }

        public Task<ProcessLoopBudgetConsumptionResult> ConsumeAsync(
            ProcessLoopBudgetConsumption consumption,
            CancellationToken cancellationToken = default)
        {
            if (consumedKeys.Contains(consumption.IdempotencyKey))
            {
                return Task.FromResult(new ProcessLoopBudgetConsumptionResult(
                    ProcessLoopBudgetOutcome.Duplicate,
                    consumption.FingerprintId,
                    ConsumptionCount(consumption.FingerprintId),
                    consumption.MaximumRepeats));
            }

            var current = ConsumptionCount(consumption.FingerprintId);
            if (current >= consumption.MaximumRepeats)
            {
                return Task.FromResult(new ProcessLoopBudgetConsumptionResult(
                    ProcessLoopBudgetOutcome.Exhausted,
                    consumption.FingerprintId,
                    current,
                    consumption.MaximumRepeats));
            }

            consumedKeys.Add(consumption.IdempotencyKey);
            counts[consumption.FingerprintId] = current + 1;
            return Task.FromResult(new ProcessLoopBudgetConsumptionResult(
                ProcessLoopBudgetOutcome.Consumed,
                consumption.FingerprintId,
                current + 1,
                consumption.MaximumRepeats));
        }
    }

    private sealed class RecordingSubprocessMessageStore : IProcessSubprocessMessageStore
    {
        public List<ProcessSubprocessControlMessage> Messages { get; } = [];

        public Task<ProcessSubprocessControlMessage?> FindByIdempotencyKeyAsync(
            ProcessRunId parentRunId,
            ProcessRunId childRunId,
            ProcessManagerIdempotencyKey idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Messages.SingleOrDefault(message =>
                message.ParentRunId == parentRunId &&
                message.ChildRunId == childRunId &&
                message.IdempotencyKey == idempotencyKey));
        }

        public Task SaveAsync(
            ProcessSubprocessControlMessage message,
            CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingManagerDecisionStore : IProcessManagerDecisionStore
    {
        public List<ProcessManagerDecision> Decisions { get; } = [];

        public Task SaveAsync(
            ProcessManagerDecision decision,
            CancellationToken cancellationToken = default)
        {
            Decisions.Add(decision);
            return Task.CompletedTask;
        }
    }

    private sealed class AllowingRecoveryPolicy : IProcessRecoveryPolicy
    {
        public ValueTask<ProcessRecoveryPolicyResult> EvaluateAsync(
            ProcessRecoveryPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new ProcessRecoveryPolicyResult(
                ProcessRecoveryPolicyDecision.Allowed,
                ProcessRecoveryPolicyDenial.None,
                null));
        }
    }

    private sealed class DenyingRecoveryPolicy : IProcessRecoveryPolicy
    {
        public ValueTask<ProcessRecoveryPolicyResult> EvaluateAsync(
            ProcessRecoveryPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(new ProcessRecoveryPolicyResult(
                ProcessRecoveryPolicyDecision.Denied,
                ProcessRecoveryPolicyDenial.AccessDenied,
                new ProcessEscalationOwnerId("operator")));
        }
    }
}
