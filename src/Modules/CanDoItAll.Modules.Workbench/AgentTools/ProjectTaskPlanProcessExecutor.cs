using System.Security.Cryptography;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Workbench;

internal sealed class ProjectTaskPlanProcessExecutor(IProcessAcceptedExecutionInputReader inputs,
    IProcessRuntimeStateStore states, ProcessDecisionStepPreparationCoordinator preparation,
    IProcessDecisionChildReader children, IAgentInheritedPlanningWorkspaceService workspace,
    IOptions<ProcessRuntimeDispatchOptions> options, ILogger<ProjectTaskPlanProcessExecutor> logger) : IProcessDecisionStepExecutor {
    private const string RefusedCode = "process.planning.contract_refused";
    private const string ReviewCode = "process.planning.review_required";
    private const string ClosedCode = "process.planning.closed";
    private const string StartupCode = "process.planning.startup_closed";

    public ProcessDecisionExecutionContract Contract => ProjectTaskPlanProcessContract.Execution;

    public async ValueTask<ProcessExecutionAdapterResult> ExecuteAsync(ProcessExecutionAdapterRequest request,
        Func<ProcessRuntimeStepAssignment, string> encodeOriginalMetadata, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(encodeOriginalMetadata);
        if (request.StepId is not { } stepId || stepId.Value == Guid.Empty || request.DispatchClaimIdentity.Value == Guid.Empty) {
            throw new ArgumentException("Task planning requires an exact Process step and dispatch claim.", nameof(request));
        }
        var state = await states.LoadAsync(request.RunId, cancellationToken)
            ?? throw new InvalidDataException("The original Process runtime is unavailable.");
        var input = await inputs.ReadAsync(request.RunId, stepId, cancellationToken);
        try {
            ProcessDecisionSingleActivityAdmission.Require(state, input, Contract);
            RequireSupportedRequest(request, input);
        } catch (NotSupportedException) {
            return Result(request, StrategyOutcome.Failed, RefusedCode,
                "Task planning requires the admitted single activity without tool, artifact, branch or subprocess obligations.");
        }
        var active = state.Claims.SingleOrDefault(claim => claim.StepInstanceId == stepId &&
            claim.ClaimToken.Value == request.DispatchClaimIdentity.Value);
        if (active is null) {
            return Result(request, StrategyOutcome.NeedsManager, ReviewCode,
                "The original Process dispatch claim is unavailable; reconcile the retained decision.");
        }
        ProcessRuntimeDispatchPrecondition claim = new(stepId, active.ClaimToken, active.OwnerId) { DecisionContract = Contract };
        var prepared = await preparation.PrepareAsync(state, claim, options.Value.StepExecutionTimeout, cancellationToken);
        if (prepared.Status == ProcessDecisionStepPreparationStatus.Pending) {
            throw new ProcessDecisionPreparationInProgressException(claim);
        }
        if (prepared.Status != ProcessDecisionStepPreparationStatus.Ready) {
            return Result(request, StrategyOutcome.NeedsManager, ReviewCode,
                "The original planning basis or work requires reconciliation before dispatch.");
        }
        var parent = prepared.Child ?? throw new InvalidDataException("Ready planning preparation has no original child.");
        if (parent.ExecutionContract != Contract || parent.Child.Address.RunId != request.RunId ||
                parent.Child.Address.StepInstanceId != stepId || parent.Child.Address.RootRunId != input.RootRunId) {
            throw new InvalidDataException("Prepared planning work belongs to a different original execution.");
        }
        var inherited = CreateRequest(input, parent, prepared.EnrolledNow, request, encodeOriginalMetadata);
        try {
            var result = await workspace.ExecuteInheritedPlanningAsync(inherited, cancellationToken);
            return MapOutcome(request, parent, result.Run);
        } catch (AgentInheritedPlanningPreparationException failure) {
            logger.LogWarning("Process planning startup closed for run {RunId}, step {StepId}, child {ChildId}, outcome {Outcome}.",
                request.RunId, stepId, parent.Child.Address.ChildId, failure.Failure.Outcome);
            return Result(request, failure.Failure.Outcome == RunOutcome.Cancelled ? StrategyOutcome.Canceled : StrategyOutcome.Failed,
                StartupCode, "The original planning preparation ended before saving a child; its measured work requires owner accounting.", parent)
                with { OwnerEvidence = new ProcessExecutionOwnerEvidence<AgentInheritedPlanningPreparationFailure>(failure.Failure) };
        } catch (AgentToolAdmissionException failure) {
            logger.LogWarning("Process planning requires reconciliation for run {RunId}, step {StepId}, child {ChildId}, code {Code}.",
                request.RunId, stepId, parent.Child.Address.ChildId, failure.Code);
            return Result(request, StrategyOutcome.NeedsManager, ReviewCode,
                "The original planning child could not be admitted or reused; reconcile its retained work before taking another action.", parent);
        }
    }

    private AgentInheritedPlanningWorkspaceRequest CreateRequest(ProcessAcceptedExecutionInput input,
        ProcessDecisionChildMaterialBinding parent, bool enrolledNow, ProcessExecutionAdapterRequest request,
        Func<ProcessRuntimeStepAssignment, string> encodeOriginalMetadata) {
        var assignment = input.Assignment;
        var agentId = Guid.Parse(assignment.ExecutorId);
        var project = input.Source.ProjectAdmission!;
        var envelope = AgentToolProtocolEnvelope.Create(ProcessDecisionChildMaterialBindingCodec.Format,
            ProcessDecisionChildMaterialBindingCodec.Version, ProcessDecisionChildMaterialBindingCodec.Serialize(parent));
        var context = ExecutionInvocationContext.Empty with {
            SourceKind = ProjectTaskPlanProcessContract.SourceKind, SourceId = assignment.StepKey,
            CorrelationId = assignment.RunId.ToString(), CausationId = assignment.StepInstanceId.ToString(),
            RequestedBy = ProjectTaskPlanProcessContract.ProcessRequester, RequestedByKind = ProjectTaskPlanProcessContract.SystemRequesterKind,
            ProcessRunId = assignment.RunId.ToString(), ProcessStepId = assignment.StepInstanceId.ToString(),
            MetadataJson = encodeOriginalMetadata(assignment)
        };
        return new(parent.Child.Address.ChildId.Value,
            new(new(parent.Child.BudgetId, parent.Child.AllocationId, parent.Child.Grant),
                enrolledNow ? AgentInheritedPlanningStartMode.FreshEnrollment : AgentInheritedPlanningStartMode.ExistingOnly),
            agentId, assignment.Prompt, AgentExecutionOperationId.New(), context, AgentPlanningOwner.ProjectTaskPlan,
            WorkspaceScopeDescriptor.Project(project.ProjectId.ToString("D")), envelope, async (run, token) => {
                var child = await children.ReadAsync(parent.Child.Address, token);
                if (child is not { IsCurrent: true, Closure: null, Settlement: null } || child.Admission != parent.Child ||
                        run.Id != parent.Child.Address.ChildId.Value || run.AgentId != agentId ||
                        run.ToolAdmission?.PlanningDecision?.InheritedOrigin?.ParentBinding != envelope) {
                    throw new AgentToolAdmissionException(ReviewCode, "The original Process child enrollment changed before execution.");
                }
            }, candidate => {
                ProcessExecutionClaimEvidence evidence = new(candidate.Id, candidate.AgentId, assignment.RunId,
                    assignment.StepInstanceId, request.DispatchClaimIdentity.Value, assignment.StepKey, candidate.CreatedAtUtc, true);
                return AgentToolProtocolEnvelope.Create(ProcessDecisionInitialSourceCodec.Format, ProcessDecisionInitialSourceCodec.Version,
                    ProcessDecisionInitialSourceCodec.Serialize(evidence));
            });
    }

    private static void RequireSupportedRequest(ProcessExecutionAdapterRequest request, ProcessAcceptedExecutionInput input) {
        var assignment = input.Assignment;
        var contract = request.StepContract;
        if (assignment.RunId != request.RunId || assignment.StepInstanceId != request.StepId ||
                !ProcessLaunchExecutorKinds.CanResolveAsAgent(assignment.ExecutorKind) || assignment.WorkflowBinding is not null ||
                !Guid.TryParse(assignment.ExecutorId, out var agentId) || agentId == Guid.Empty ||
                input.Source is not { CanCreateTasks: true, ProjectAdmission: not null } ||
                contract.RequiredArtifacts.Count != 0 || contract.ExpectedProducedArtifacts.Count != 0 ||
                contract.RequiredRuntimeToolNames.Count != 0 || !ProcessDecisionReviewCompletion.SupportsHostCapabilities(contract.RequiredHostCapabilities) ||
                contract.ArtifactDescriptors.Count != 0 || contract.SubprocessArtifactMappings.Count != 0 ||
                contract.ConfiguredBranchOutcomeIds.Count != 0) {
            throw new NotSupportedException("The supplied task-plan execution does not match the supported original contract.");
        }
    }

    private static ProcessExecutionAdapterResult MapOutcome(ProcessExecutionAdapterRequest request,
        ProcessDecisionChildMaterialBinding parent, ExecutionRunRecord run) {
        var (_, outcome) = ProjectTaskPlanProcessOutcome.Read(run, parent);
        if (outcome is ProcessDecisionChildResult.Pending { Reason: ProcessDecisionChildPendingReason.ActiveWork or
                ProcessDecisionChildPendingReason.ExecutionIncomplete }) {
            throw new ProcessRuntimeDispatchInProgressException(new(run.Id));
        }
        if (outcome is not ProcessDecisionChildResult.Closed closed) {
            return Result(request, StrategyOutcome.NeedsManager, ReviewCode,
                "The original task-plan proposal requires review or reconciliation before publication.", parent);
        }
        var status = closed.Status switch {
            ProcessDecisionChildTerminalStatus.Succeeded => StrategyOutcome.Succeeded,
            ProcessDecisionChildTerminalStatus.Failed => StrategyOutcome.Failed,
            ProcessDecisionChildTerminalStatus.Cancelled => StrategyOutcome.Canceled,
            _ => throw new InvalidDataException("The task-plan owner returned an unsupported terminal status.")
        };
        return Result(request, status, ClosedCode, "The original task-plan child has a verified closed outcome.", parent);
    }

    private static ProcessExecutionAdapterResult Result(ProcessExecutionAdapterRequest request, StrategyOutcome outcome,
        string code, string summary, ProcessDecisionChildMaterialBinding? parent = null) {
        var identity = FormattableString.Invariant($"{code}|{request.RunId}|{request.StepId}|{parent?.Child.Address.ChildId.Value:D}|{(int)outcome}");
        var hash = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
        return new(outcome, [], [], [new(new(code), StrategyDiagnosticSensitivity.Normal, hash, summary, null,
            ProcessDiagnosticRetrySafety.UnsafeToRetry, ProcessDiagnosticIdempotencyClassification.Unknown)], [], summary, hash) {
            ExecutionRunId = parent is null ? null : new(parent.Child.Address.ChildId.Value)
        };
    }
}
