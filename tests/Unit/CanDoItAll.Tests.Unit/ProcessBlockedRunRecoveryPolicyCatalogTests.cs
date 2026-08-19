using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Tests.Unit.Processes;

public sealed class ProcessBlockedRunRecoveryPolicyCatalogTests
{
    private const string Fingerprint = "sha256:policy-catalog-diagnostic";
    private const string SubprocessChildNoGo = "process.adapter.subprocess_child_nogo_output";
    private const string SubprocessChildAcceptedOutputMissing =
        "process.adapter.subprocess_child_accepted_output_missing";
    private const string SubprocessChildForwardedContextUnavailable =
        "process.adapter.forwarded_context_unavailable";
    private const string SubprocessChildFailed = "process.adapter.subprocess_child_failed";
    private const string LegacyAgentTransientExecutionRetry =
        "process.adapter.agent_transient_execution_retry";
    private const string OtherRetryableDiagnostic = "process.adapter.retryable_transport";

    private static readonly ProcessRunId RunId = ProcessRunId.New();
    private static readonly ProcessInstancePlanId PlanId = ProcessInstancePlanId.New();
    private static readonly ProcessStepInstanceId StepId = ProcessStepInstanceId.New();
    private static readonly ProcessStepInstanceId OtherStepId = ProcessStepInstanceId.New();
    private static readonly ProcessStepDefinitionId StepDefinitionId = ProcessStepDefinitionId.New();
    private static readonly Guid AgentId = new("ec155761-fac7-4be9-838f-90f45e7b172f");
    private static readonly StrategyId StrategyId = new("strategy.policy-catalog-test");
    private static readonly ProcessStrategyBindingSnapshot ExecutionBinding = new(
        new DriverId("driver.policy-catalog-test"),
        StrategyId,
        "1.0.0",
        "factory.1.0.0",
        "runtime.1",
        "runtime.1",
        "sha256:policy-catalog-binding",
        []);

    [Fact]
    public void Child_recovery_requires_exact_canonical_blocked_diagnostic()
    {
        var childRunId = ProcessRunId.New();
        var receipt = CreateReceipt(
            ProcessExecutionAdapterDiagnosticCodes.SubprocessChildBlocked,
            ProcessFailureCategory.ChildRunBlocked,
            ProcessDiagnosticRetrySafety.UnsafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent,
            ProcessRecoveryRouteKind.ChildRunPropagation,
            relatedChildRunId: childRunId);

        var policy = Resolve(receipt);

        Assert.Equal(ProcessBlockedRunRecoveryPolicy.CompletedChildConsumerRework, policy);
    }

    [Theory]
    [InlineData(SubprocessChildNoGo)]
    [InlineData(SubprocessChildAcceptedOutputMissing)]
    [InlineData(SubprocessChildForwardedContextUnavailable)]
    [InlineData(SubprocessChildFailed)]
    public void Child_recovery_rejects_non_blocked_child_diagnostic(string diagnosticCode)
    {
        var receipt = CreateReceipt(
            diagnosticCode,
            ProcessFailureCategory.ChildRunBlocked,
            ProcessDiagnosticRetrySafety.UnsafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent,
            ProcessRecoveryRouteKind.ChildRunPropagation,
            relatedChildRunId: ProcessRunId.New());

        var policy = Resolve(receipt);

        Assert.Equal(ProcessBlockedRunRecoveryPolicy.None, policy);
    }

    [Fact]
    public void Child_recovery_rejects_mixed_diagnostics()
    {
        var childRunId = ProcessRunId.New();
        var receipt = CreateReceipt(
            ProcessExecutionAdapterDiagnosticCodes.SubprocessChildBlocked,
            ProcessFailureCategory.ChildRunBlocked,
            ProcessDiagnosticRetrySafety.UnsafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent,
            ProcessRecoveryRouteKind.ChildRunPropagation,
            relatedChildRunId: childRunId);
        receipt = receipt with
        {
            Diagnostics =
            [
                .. receipt.Diagnostics,
                CreateDiagnostic(
                    SubprocessChildNoGo,
                    ProcessDiagnosticRetrySafety.UnsafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent,
                    relatedChildRunId: childRunId)
            ]
        };

        var policy = Resolve(receipt);

        Assert.Equal(ProcessBlockedRunRecoveryPolicy.None, policy);
    }

    [Fact]
    public void Agent_transient_before_side_effects_remains_advisory_without_independent_ledger_verification()
    {
        var receipt = CreateAgentTransientReceipt();
        var assignment = CreateAssignment(
            StepId,
            ProcessLaunchExecutorKinds.Agent,
            nameof(ProcessDefinitionStepOperationKind.MutateProductTarget),
            nameof(ProcessDefinitionStepTargetScopeKind.ExternalProductTargetMutable));

        var policy = Resolve(receipt, assignment);

        Assert.Equal(ProcessBlockedRunRecoveryPolicy.None, policy);
    }

    [Fact]
    public void Agent_transient_before_side_effects_cannot_fall_back_to_generic_artifact_rework()
    {
        var receipt = CreateAgentTransientReceipt();
        var assignment = CreateAssignment(
            StepId,
            ProcessLaunchExecutorKinds.Agent,
            nameof(ProcessDefinitionStepOperationKind.RecoverArtifactsOnly),
            nameof(ProcessDefinitionStepTargetScopeKind.ManagedProcessArtifactsOnly));

        var policy = Resolve(receipt, assignment);

        Assert.Equal(ProcessBlockedRunRecoveryPolicy.None, policy);
    }

    [Fact]
    public void Agent_transient_policy_rejects_legacy_diagnostic_code()
    {
        var receipt = CreateReceipt(
            LegacyAgentTransientExecutionRetry,
            ProcessFailureCategory.AdapterRetryable,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);

        var policy = Resolve(receipt, CreateExternalAgentAssignment());

        Assert.Equal(ProcessBlockedRunRecoveryPolicy.None, policy);
    }

    [Fact]
    public void Agent_transient_policy_rejects_mixed_diagnostics()
    {
        var receipt = CreateAgentTransientReceipt();
        receipt = receipt with
        {
            Diagnostics =
            [
                .. receipt.Diagnostics,
                CreateDiagnostic(
                    OtherRetryableDiagnostic,
                    ProcessDiagnosticRetrySafety.SafeToRetry,
                    ProcessDiagnosticIdempotencyClassification.Idempotent)
            ]
        };

        var policy = Resolve(receipt, CreateExternalAgentAssignment());

        Assert.Equal(ProcessBlockedRunRecoveryPolicy.None, policy);
    }

    [Theory]
    [InlineData(
        ProcessDiagnosticRetrySafety.UnsafeToRetry,
        ProcessDiagnosticIdempotencyClassification.Idempotent)]
    [InlineData(
        ProcessDiagnosticRetrySafety.Unknown,
        ProcessDiagnosticIdempotencyClassification.Idempotent)]
    [InlineData(
        ProcessDiagnosticRetrySafety.SafeToRetry,
        ProcessDiagnosticIdempotencyClassification.Unknown)]
    [InlineData(
        ProcessDiagnosticRetrySafety.SafeToRetry,
        ProcessDiagnosticIdempotencyClassification.NonIdempotent)]
    public void Agent_transient_policy_requires_safe_idempotent_diagnostic(
        ProcessDiagnosticRetrySafety retrySafety,
        ProcessDiagnosticIdempotencyClassification idempotency)
    {
        var receipt = CreateReceipt(
            ProcessExecutionAdapterDiagnosticCodes.AgentTransientExecutionBeforeSideEffects,
            ProcessFailureCategory.AdapterRetryable,
            retrySafety,
            idempotency);

        var policy = Resolve(receipt, CreateExternalAgentAssignment());

        Assert.Equal(ProcessBlockedRunRecoveryPolicy.None, policy);
    }

    [Theory]
    [InlineData(StrategyDiagnosticSensitivity.Restricted, false)]
    [InlineData(StrategyDiagnosticSensitivity.Normal, true)]
    public void Agent_transient_policy_rejects_restricted_diagnostic(
        StrategyDiagnosticSensitivity sensitivity,
        bool hasRestrictedEvidence)
    {
        var receipt = CreateAgentTransientReceipt();
        var diagnostic = Assert.Single(receipt.Diagnostics);
        receipt = receipt with
        {
            Diagnostics =
            [
                diagnostic with
                {
                    Sensitivity = sensitivity,
                    RestrictedEvidenceReference = hasRestrictedEvidence
                        ? "restricted://agent-transient/1"
                        : null
                }
            ]
        };

        var policy = Resolve(receipt, CreateExternalAgentAssignment());

        Assert.Equal(ProcessBlockedRunRecoveryPolicy.None, policy);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Agent_transient_policy_rejects_related_child_identity(
        bool diagnosticHasRelatedChild,
        bool decisionHasRelatedChild)
    {
        var childRunId = ProcessRunId.New();
        var receipt = CreateAgentTransientReceipt();
        var diagnostic = Assert.Single(receipt.Diagnostics);
        receipt = receipt with
        {
            Diagnostics =
            [
                diagnostic with
                {
                    RelatedChildRunId = diagnosticHasRelatedChild
                        ? childRunId
                        : null
                }
            ],
            RecoveryDecision = receipt.RecoveryDecision! with
            {
                RelatedChildRunId = decisionHasRelatedChild
                    ? childRunId
                    : null
            }
        };

        var policy = Resolve(receipt, CreateExternalAgentAssignment());

        Assert.Equal(ProcessBlockedRunRecoveryPolicy.None, policy);
    }

    [Theory]
    [InlineData(ProcessFailureCategory.Unknown, ProcessRecoveryDecisionKind.ManagerRequired)]
    [InlineData(ProcessFailureCategory.AdapterRetryable, ProcessRecoveryDecisionKind.SafeRetry)]
    public void Agent_transient_policy_requires_retryable_manager_decision(
        ProcessFailureCategory failureCategory,
        ProcessRecoveryDecisionKind decisionKind)
    {
        var receipt = CreateAgentTransientReceipt();
        receipt = receipt with
        {
            RecoveryDecision = receipt.RecoveryDecision! with
            {
                FailureCategory = failureCategory,
                DecisionKind = decisionKind
            }
        };

        var policy = Resolve(receipt, CreateExternalAgentAssignment());

        Assert.Equal(ProcessBlockedRunRecoveryPolicy.None, policy);
    }

    [Fact]
    public void Agent_transient_policy_requires_matching_source_code()
    {
        var receipt = CreateAgentTransientReceipt();
        receipt = receipt with
        {
            RecoveryDecision = receipt.RecoveryDecision! with
            {
                SourceDiagnosticCode = OtherRetryableDiagnostic
            }
        };

        var policy = Resolve(receipt, CreateExternalAgentAssignment());

        Assert.Equal(ProcessBlockedRunRecoveryPolicy.None, policy);
    }

    [Theory]
    [InlineData(ProcessRecoveryRouteKind.CurrentStepRetry, true)]
    [InlineData(ProcessRecoveryRouteKind.ManagerAction, false)]
    public void Agent_transient_policy_requires_manager_route_and_blocked_target(
        ProcessRecoveryRouteKind routeKind,
        bool useBlockedStepAsResponsibleTarget)
    {
        var receipt = CreateAgentTransientReceipt();
        receipt = receipt with
        {
            RecoveryDecision = receipt.RecoveryDecision! with
            {
                RouteKind = routeKind,
                ResponsibleStepInstanceId = useBlockedStepAsResponsibleTarget
                    ? StepId
                    : OtherStepId
            }
        };

        var policy = Resolve(receipt, CreateExternalAgentAssignment());

        Assert.Equal(ProcessBlockedRunRecoveryPolicy.None, policy);
    }

    [Theory]
    [InlineData(ProcessLaunchExecutorKinds.Workflow)]
    [InlineData(ProcessLaunchExecutorKinds.Person)]
    public void Agent_transient_policy_rejects_non_agent_assignment(string executorKind)
    {
        var receipt = CreateAgentTransientReceipt();
        var assignment = CreateAssignment(
            StepId,
            executorKind,
            nameof(ProcessDefinitionStepOperationKind.MutateProductTarget),
            nameof(ProcessDefinitionStepTargetScopeKind.ExternalProductTargetMutable));

        var policy = Resolve(receipt, assignment);

        Assert.Equal(ProcessBlockedRunRecoveryPolicy.None, policy);
    }

    [Fact]
    public void Agent_transient_policy_requires_assignment_for_blocked_step()
    {
        var receipt = CreateAgentTransientReceipt();
        var assignment = CreateAssignment(
            OtherStepId,
            ProcessLaunchExecutorKinds.Agent,
            nameof(ProcessDefinitionStepOperationKind.MutateProductTarget),
            nameof(ProcessDefinitionStepTargetScopeKind.ExternalProductTargetMutable));

        var policy = Resolve(receipt, assignment);

        Assert.Equal(ProcessBlockedRunRecoveryPolicy.None, policy);
    }

    [Theory]
    [InlineData(AgentAttestationMutation.Missing)]
    [InlineData(AgentAttestationMutation.WrongExecutionRun)]
    [InlineData(AgentAttestationMutation.WrongProcessRun)]
    [InlineData(AgentAttestationMutation.WrongStep)]
    [InlineData(AgentAttestationMutation.WrongExecutor)]
    [InlineData(AgentAttestationMutation.WrongAttestor)]
    [InlineData(AgentAttestationMutation.WrongSchema)]
    [InlineData(AgentAttestationMutation.CorruptDurableEvidenceDigest)]
    [InlineData(AgentAttestationMutation.CorruptEvidenceHash)]
    [InlineData(AgentAttestationMutation.EmptyExecutionRunId)]
    public void Agent_transient_policy_rejects_missing_or_malformed_attestation(
        AgentAttestationMutation mutation)
    {
        var receipt = CreateAgentTransientReceipt();
        var diagnostic = Assert.Single(receipt.Diagnostics);
        var attestation = Assert.IsType<ProcessExecutionSafetyAttestation>(
            diagnostic.ExecutionSafetyAttestation);
        diagnostic = diagnostic with
        {
            ExecutionSafetyAttestation = mutation switch
            {
                AgentAttestationMutation.Missing => null,
                AgentAttestationMutation.WrongExecutionRun =>
                    ProcessExecutionSafetyAttestation.FailedBeforeRecordedSideEffects(
                        ProcessExecutionRunId.New(),
                        attestation.ProcessRunId,
                        attestation.StepInstanceId,
                        attestation.ExecutorId,
                        attestation.DurableEvidenceDigest),
                AgentAttestationMutation.WrongProcessRun =>
                    ProcessExecutionSafetyAttestation.FailedBeforeRecordedSideEffects(
                        attestation.ExecutionRunId,
                        ProcessRunId.New(),
                        attestation.StepInstanceId,
                        attestation.ExecutorId,
                        attestation.DurableEvidenceDigest),
                AgentAttestationMutation.WrongStep =>
                    ProcessExecutionSafetyAttestation.FailedBeforeRecordedSideEffects(
                        attestation.ExecutionRunId,
                        attestation.ProcessRunId,
                        OtherStepId,
                        attestation.ExecutorId,
                        attestation.DurableEvidenceDigest),
                AgentAttestationMutation.WrongExecutor =>
                    ProcessExecutionSafetyAttestation.FailedBeforeRecordedSideEffects(
                        attestation.ExecutionRunId,
                        attestation.ProcessRunId,
                        attestation.StepInstanceId,
                        new ProcessExecutionExecutorId(Guid.NewGuid()),
                        attestation.DurableEvidenceDigest),
                AgentAttestationMutation.WrongAttestor => attestation with
                {
                    Attestor = ProcessExecutionSafetyAttestor.None
                },
                AgentAttestationMutation.WrongSchema => attestation with
                {
                    SchemaVersion = ProcessExecutionSafetyAttestation.CurrentSchemaVersion + 1
                },
                AgentAttestationMutation.CorruptDurableEvidenceDigest => attestation with
                {
                    DurableEvidenceDigest = "sha256:" + new string('0', 64)
                },
                AgentAttestationMutation.CorruptEvidenceHash => attestation with
                {
                    EvidenceHash = "sha256:" + new string('0', 64)
                },
                AgentAttestationMutation.EmptyExecutionRunId => attestation with
                {
                    ExecutionRunId = default
                },
                _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null)
            }
        };
        receipt = receipt with
        {
            Diagnostics = [diagnostic]
        };

        var policy = Resolve(receipt, CreateExternalAgentAssignment());

        Assert.Equal(ProcessBlockedRunRecoveryPolicy.None, policy);
    }

    [Fact]
    public void Agent_transient_policy_rejects_mixed_attested_execution_run_ids()
    {
        var receipt = CreateAgentTransientReceipt();
        var diagnostic = Assert.Single(receipt.Diagnostics);
        var attestation = Assert.IsType<ProcessExecutionSafetyAttestation>(
            diagnostic.ExecutionSafetyAttestation);
        receipt = receipt with
        {
            Diagnostics =
            [
                diagnostic,
                diagnostic with
                {
                    ExecutionSafetyAttestation =
                        ProcessExecutionSafetyAttestation.FailedBeforeRecordedSideEffects(
                            ProcessExecutionRunId.New(),
                            attestation.ProcessRunId,
                            attestation.StepInstanceId,
                            attestation.ExecutorId,
                            attestation.DurableEvidenceDigest)
                }
            ]
        };

        var policy = Resolve(receipt, CreateExternalAgentAssignment());

        Assert.Equal(ProcessBlockedRunRecoveryPolicy.None, policy);
    }

    [Fact]
    public void Agent_transient_policy_requires_exactly_one_attested_diagnostic()
    {
        var receipt = CreateAgentTransientReceipt();
        var diagnostic = Assert.Single(receipt.Diagnostics);
        receipt = receipt with
        {
            Diagnostics = [diagnostic, diagnostic]
        };

        var policy = Resolve(receipt, CreateExternalAgentAssignment());

        Assert.Equal(ProcessBlockedRunRecoveryPolicy.None, policy);
    }

    [Fact]
    public void Agent_transient_policy_requires_receipt_strategy_from_immutable_plan_step_binding()
    {
        var receipt = CreateAgentTransientReceipt() with
        {
            StrategyId = new StrategyId("strategy.unbound")
        };

        var policy = Resolve(receipt, CreateExternalAgentAssignment());

        Assert.Equal(ProcessBlockedRunRecoveryPolicy.None, policy);
    }

    [Fact]
    public void Agent_transient_policy_requires_assignment_and_state_to_share_immutable_plan_identity()
    {
        var receipt = CreateAgentTransientReceipt();
        var assignment = CreateExternalAgentAssignment() with
        {
            PlanId = ProcessInstancePlanId.New()
        };

        var policy = Resolve(receipt, assignment);

        Assert.Equal(ProcessBlockedRunRecoveryPolicy.None, policy);
    }

    [Fact]
    public void Agent_transient_policy_requires_step_binding_in_canonical_plan_binding_set()
    {
        var receipt = CreateAgentTransientReceipt();
        var plan = CreatePlan() with
        {
            Strategies = new StrategyBindingSet([], [], [], [])
        };
        var state = CreateState(receipt);
        var blockedStep = Assert.Single(state.Steps);

        var policy = new ProcessBlockedRunRecoveryPolicyCatalog().Resolve(
            state,
            plan,
            blockedStep,
            CreateExternalAgentAssignment(),
            receipt,
            receipt.RecoveryDecision!);

        Assert.Equal(ProcessBlockedRunRecoveryPolicy.None, policy);
    }

    private static ProcessBlockedRunRecoveryPolicy Resolve(
        StrategyResultReceipt receipt,
        ProcessRuntimeStepAssignment? assignment = null)
    {
        var state = CreateState(receipt);
        var blockedStep = Assert.Single(state.Steps);
        return new ProcessBlockedRunRecoveryPolicyCatalog().Resolve(
            state,
            CreatePlan(),
            blockedStep,
            assignment ?? CreateExternalAgentAssignment(),
            receipt,
            receipt.RecoveryDecision!);
    }

    private static StrategyResultReceipt CreateAgentTransientReceipt()
    {
        return CreateReceipt(
            ProcessExecutionAdapterDiagnosticCodes.AgentTransientExecutionBeforeSideEffects,
            ProcessFailureCategory.AdapterRetryable,
            ProcessDiagnosticRetrySafety.SafeToRetry,
            ProcessDiagnosticIdempotencyClassification.Idempotent);
    }

    private static StrategyResultReceipt CreateReceipt(
        string diagnosticCode,
        ProcessFailureCategory failureCategory,
        ProcessDiagnosticRetrySafety retrySafety,
        ProcessDiagnosticIdempotencyClassification idempotency,
        ProcessRecoveryRouteKind routeKind = ProcessRecoveryRouteKind.ManagerAction,
        ProcessRunId? relatedChildRunId = null)
    {
        var diagnostic = CreateDiagnostic(
            diagnosticCode,
            retrySafety,
            idempotency,
            relatedChildRunId: relatedChildRunId);
        if (string.Equals(
                diagnosticCode,
                ProcessExecutionAdapterDiagnosticCodes.AgentTransientExecutionBeforeSideEffects,
                StringComparison.Ordinal))
        {
            diagnostic = diagnostic with
            {
                ExecutionSafetyAttestation =
                    ProcessExecutionSafetyAttestation.FailedBeforeRecordedSideEffects(
                        ProcessExecutionRunId.New(),
                        RunId,
                        StepId,
                        new ProcessExecutionExecutorId(AgentId),
                        "sha256:" + new string('d', 64))
            };
        }

        var receipt = new StrategyResultReceipt(
            StepId,
            StrategyId,
            StrategyResultIdempotencyKey.New(),
            StrategyOutcome.NeedsManager,
            ProcessRuntimeStepStatus.Blocked,
            "sha256:blocked-result",
            [diagnostic],
            recoveryDecision: new ProcessRecoveryDecisionReceipt(
                failureCategory,
                ProcessRecoveryDecisionKind.ManagerRequired,
                diagnosticCode,
                "process.manager-review-required",
                "Typed manager decision required.")
            {
                RouteKind = routeKind,
                ResponsibleStepInstanceId = StepId,
                DiagnosticFingerprint = Fingerprint,
                AutomaticRetryAttempt = 1,
                MaximumAutomaticRetryAttempts = 4,
                SameDiagnosticFingerprintAttempt = 1,
                MaximumSameDiagnosticFingerprintAttempts = 1,
                RelatedChildRunId = relatedChildRunId
            });
        return diagnostic.ExecutionSafetyAttestation is { } attestation
            ? receipt with
            {
                ExecutionRunId = attestation.ExecutionRunId
            }
            : receipt;
    }

    private static StrategyResultDiagnosticReceipt CreateDiagnostic(
        string diagnosticCode,
        ProcessDiagnosticRetrySafety retrySafety,
        ProcessDiagnosticIdempotencyClassification idempotency,
        StrategyDiagnosticSensitivity sensitivity = StrategyDiagnosticSensitivity.Normal,
        string? restrictedEvidenceReference = null,
        ProcessRunId? relatedChildRunId = null)
    {
        return new StrategyResultDiagnosticReceipt(
            diagnosticCode,
            sensitivity,
            "sha256:diagnostic-evidence",
            "Typed diagnostic summary.",
            restrictedEvidenceReference,
            retrySafety,
            idempotency)
        {
            RelatedChildRunId = relatedChildRunId
        };
    }

    private static ProcessRuntimeStateSnapshot CreateState(StrategyResultReceipt receipt)
    {
        return new ProcessRuntimeStateSnapshot(
            RunId,
            RunId,
            PlanId,
            "sha256:plan",
            ProcessRuntimeStatus.Blocked,
            [
                new ProcessRuntimeStepState(
                    StepId,
                    StepDefinitionId,
                    ProcessRuntimeStepStatus.Blocked,
                    IsExecutable: true,
                    AttemptNumber: 1,
                    DependencyStepIds: new HashSet<ProcessStepInstanceId>(),
                    RequiredArtifactSlots: new HashSet<ArtifactSlotId>(),
                    ActiveClaimToken: null,
                    CompletedResultKey: null)
            ],
            [],
            [receipt],
            new HashSet<ArtifactSlotId>(),
            DateTimeOffset.UtcNow);
    }

    private static ProcessRuntimeStepAssignment CreateExternalAgentAssignment()
    {
        return CreateAssignment(
            StepId,
            ProcessLaunchExecutorKinds.Agent,
            nameof(ProcessDefinitionStepOperationKind.MutateProductTarget),
            nameof(ProcessDefinitionStepTargetScopeKind.ExternalProductTargetMutable));
    }

    private static ProcessRuntimeStepAssignment CreateAssignment(
        ProcessStepInstanceId stepInstanceId,
        string executorKind,
        string operation,
        string targetScope)
    {
        return new ProcessRuntimeStepAssignment(
            RunId,
            PlanId,
            stepInstanceId,
            $"step-{stepInstanceId.Value:N}",
            "enterprise-worker",
            "enterprise-worker",
            "Enterprise worker",
            executorKind,
            AgentId.ToString("D"),
            "Enterprise worker",
            "Execute the assigned process step.",
            "sha256:readiness",
            "Unit-test assignment.",
            [],
            [],
            [operation],
            targetScope,
            new Dictionary<string, string>(StringComparer.Ordinal),
            BranchGate: null,
            DateTimeOffset.UtcNow);
    }

    private static ProcessInstancePlan CreatePlan()
    {
        return new ProcessInstancePlan(
            new ProcessInstancePlanHeader(
                PlanId,
                PlanId,
                ParentPlanId: null,
                ParentStepId: null,
                "processes.instance-plan.v1",
                DateTimeOffset.UtcNow,
                HierarchyDepth: 0),
            new ResolvedProcessDefinitionSnapshot(
                ProcessDefinitionId.New(),
                ProcessDefinitionVersionId.New(),
                "sha256:definition",
                "template/1",
                "template/1",
                [],
                [],
                []),
            new DriverStackSnapshot([]),
            new StrategyBindingSet([ExecutionBinding], [], [], []),
            [
                new StepInstancePlan(
                    StepId,
                    StepDefinitionId,
                    $"step-{StepId.Value:N}",
                    ProcessStepKind.Activity,
                    IsExecutable: true,
                    StartsSubprocess: false,
                    ExecutionStrategyBinding: ExecutionBinding)
            ],
            new ArtifactPlan([], []),
            new BranchRouteTable([]),
            [],
            new ManagerPlan("sha256:manager-policy", null, [], []),
            new BudgetPlan([]),
            new MonitoringPlan(true, "sha256:monitoring"),
            new SecurityPlan("sha256:security", []),
            "sha256:plan");
    }

    public enum AgentAttestationMutation
    {
        Missing,
        WrongExecutionRun,
        WrongProcessRun,
        WrongStep,
        WrongExecutor,
        WrongAttestor,
        WrongSchema,
        CorruptDurableEvidenceDigest,
        CorruptEvidenceHash,
        EmptyExecutionRunId
    }
}
