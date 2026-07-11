using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal sealed class DotNetSubprocessNoGoEvidencePolicyContribution : IProcessToolReceiptEvidencePolicyContribution
{
    internal const string FeatureRepairEscalationMarker = "feature-repair-escalation.md";
    private const string DevelopmentSliceDefinitionKey = "dotnet-development-slice";
    private const string SliceValidationStepKey = "add-tests-and-proof";
    private const string SliceRecheckStepKey = "add-tests-recheck";
    private const string SliceAcceptedBranchOutcomeKey = "slice-accepted";
    private const string WorkspaceReadFileToolName = "workspace_read_file";
    private const string ArtifactPathArgumentName = "path";
    private const string ImplementationCoordinatorArtifactFragment = "/steps/implement-code-change.md";
    private const string RepairCoordinatorArtifactFragment = "/steps/slice-repair-code-change.md";

    public IEnumerable<ProcessToolReceiptTextEvidenceRule> ResolveRules(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output)
    {
        if (!string.Equals(
                assignment.LaunchVariables.GetValueOrDefault(ProcessRuntimeLaunchVariables.ProcessDefinitionKey),
                DevelopmentSliceDefinitionKey,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(output.BranchOutcomeKey, SliceAcceptedBranchOutcomeKey, StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var artifactFragment = assignment.StepKey switch
        {
            SliceValidationStepKey => ImplementationCoordinatorArtifactFragment,
            SliceRecheckStepKey => RepairCoordinatorArtifactFragment,
            _ => string.Empty
        };
        if (artifactFragment.Length == 0)
        {
            return [];
        }

        return
        [
            new ProcessToolReceiptTextEvidenceRule(
                "dotnet.development-slice-accepted-child-evidence",
                WorkspaceReadFileToolName,
                ArtifactPathArgumentName,
                [FeatureRepairEscalationMarker, "implementation-attempt-incomplete", "repair-attempt-incomplete"],
                "The implementation coordinator contains a typed child repair-escalation no-go or incomplete output; green generic build or test receipts cannot convert that child outcome into an accepted slice.",
                artifactFragment)
        ];
    }
}
