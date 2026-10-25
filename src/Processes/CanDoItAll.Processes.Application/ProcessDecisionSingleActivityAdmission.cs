using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public static class ProcessDecisionSingleActivityAdmission {
    public static void Require(ProcessRuntimeStateSnapshot state, ProcessAcceptedExecutionInput input,
        ProcessDecisionExecutionContract contract) {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(input);
        var assignment = input.Assignment;
        var requirements = input.RequireRequirements();
        var step = state.Steps.SingleOrDefault(item => item.StepInstanceId == assignment.StepInstanceId)
            ?? throw new InvalidDataException("The accepted decision input has no original runtime step.");
        if (input.RootRunId != state.RootRunId || input.AdmissionId != state.LaunchAdmissionId ||
                assignment.RunId != state.RunId || assignment.PlanId != state.PlanId ||
                requirements.StepDefinitionId != step.StepDefinitionId) {
            throw new InvalidDataException("The accepted decision input belongs to a different run, plan, admission or step definition.");
        }
        var policy = assignment.DecisionDataPolicy
            ?? throw new InvalidDataException("The accepted decision input has no original policy.");
        policy.RequireExecutionContract(contract);
        if (policy.StepInstanceId != step.StepInstanceId) {
            throw new InvalidDataException("The accepted decision policy belongs to a different step.");
        }
        if (requirements.Kind != ProcessStepKind.Activity || requirements.StartsSubprocess ||
                requirements.RequiredRuntimeToolNames.Count != 0 || !ProcessDecisionReviewCompletion.SupportsHostCapabilities(requirements.RequiredHostCapabilities) ||
                requirements.ConfiguredBranchOutcomeIds.Count != 0 || requirements.ArtifactDescriptors.Count != 0 ||
                requirements.SubprocessArtifactMappings.Count != 0 || assignment.RequiredArtifactSlotIds.Count != 0 ||
                assignment.ProducedArtifactSlotIds.Count != 0 || assignment.BranchGate is not null ||
                !ProcessDecisionReviewCompletion.SupportsRuntimeContract(step)) {
            throw new NotSupportedException("The registered decision contract supports a single activity with its managed execution port and no additional artifact, tool, host, branch or subprocess obligations.");
        }
    }
}
