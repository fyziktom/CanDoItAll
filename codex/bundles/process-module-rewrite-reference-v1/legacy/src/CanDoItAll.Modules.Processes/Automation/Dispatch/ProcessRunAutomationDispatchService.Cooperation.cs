using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static ProcessRunAssignment? ResolveDispatchCurrentAssignment(
        ProcessStepRun stepRun,
        IReadOnlyList<ProcessStepRoleAssignmentRequirement> stepRoleRequirements,
        IReadOnlyList<ProcessRunAssignment> runAssignments)
    {
        return ProcessDispatchAssignmentRouteHelper.ResolveCurrentAssignment(
            stepRun,
            stepRoleRequirements,
            runAssignments);
    }

    private static bool HasDispatchExecutableTarget(ProcessRunAssignment assignment)
    {
        return ProcessDispatchAssignmentRouteHelper.HasDispatchExecutableTarget(assignment);
    }

    private static AgentProcessCooperationMetadata ResolveProcessCooperationMetadata(
        ProcessStepRun stepRun,
        ProcessWorkBrief? workBrief,
        ProcessRoleRequirement? role,
        ProcessRunAssignment? assignment,
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts,
        IReadOnlyList<DispatchArtifactInput> artifactInputs,
        IReadOnlyList<DispatchBranchOutcome> branchOutcomes,
        AgentEditorModel agent)
    {
        return ProcessDispatchCooperationMetadataResolver.ResolveProcessCooperationMetadata(
            stepRun,
            workBrief,
            role,
            assignment,
            expectedArtifacts,
            artifactInputs,
            branchOutcomes,
            agent);
    }

    private static AgentProcessCooperationMode ResolveCooperationMode(
        bool hasEnabledHandoff,
        bool hasEnabledA2AEndpoints,
        bool hasProcessArtifactHandoff)
    {
        return ProcessDispatchCooperationMetadataResolver.ResolveCooperationMode(
            hasEnabledHandoff,
            hasEnabledA2AEndpoints,
            hasProcessArtifactHandoff);
    }

    private static AgentWorkspaceToolProfileKind ResolveWorkspaceToolProfile(
        ProcessStepRun stepRun,
        ProcessWorkBrief? workBrief,
        ProcessRoleRequirement? role,
        ProcessRunAssignment? assignment,
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts)
    {
        return ProcessDispatchCooperationMetadataResolver.ResolveWorkspaceToolProfile(
            stepRun,
            workBrief,
            role,
            assignment,
            expectedArtifacts);
    }
}
