using System.Text.Json.Serialization;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Persistence;

public enum ProcessAcceptedSourceVersion { LegacyPreparedLaunch, OriginalProjection }

internal sealed record ProcessAcceptedSourceHeader(
    ProcessLaunchAdmissionId AdmissionId, ProcessRunId RunId, ProcessRunId RootRunId,
    ProcessInstancePlanId PlanId, string PlanHash, ProcessDefinitionVersionId DefinitionVersionId,
    ProcessProjectAdmission? ProjectAdmission, ProcessLaunchAuthority? Authority, string PreparationFingerprint);

internal sealed record ProcessAssignmentSource(
    ProcessRunId RunId, ProcessInstancePlanId PlanId, ProcessStepInstanceId StepInstanceId, string StepKey,
    string ExecutorKind, string ExecutorId, string ReadinessHash, DateTimeOffset CreatedAtUtc,
    IReadOnlyList<string> AllowedOperations, string OperationTargetScope, IReadOnlyDictionary<string, string> LaunchVariables,
    ProcessCapabilityScope CapabilityScope,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ProcessDecisionDataPolicyAdmission? DecisionDataPolicy,
    ProcessWorkflowExecutorBinding? WorkflowBinding) {
    internal static ProcessAssignmentSource From(ProcessRuntimeStepAssignment assignment)
        => new(assignment.RunId, assignment.PlanId, assignment.StepInstanceId, assignment.StepKey,
            assignment.ExecutorKind, assignment.ExecutorId, assignment.ReadinessHash, assignment.CreatedAtUtc,
            assignment.AllowedOperations, assignment.OperationTargetScope, assignment.LaunchVariables,
            assignment.CapabilityScope, assignment.DecisionDataPolicy, assignment.WorkflowBinding);
}

internal sealed record ProcessAcceptedSource(ProcessAcceptedSourceHeader Header, ProcessAssignmentSource Assignment);

internal sealed record ProcessAcceptedSourceEnvelope(string PayloadJson, string Fingerprint);

internal sealed record ProcessAcceptedSourceProjection(ProcessAcceptedSourceEnvelope Header,
    IReadOnlyDictionary<ProcessStepInstanceId, ProcessAcceptedSourceEnvelope> Assignments);
