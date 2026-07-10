using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessToolReceiptTextEvidenceRule(
    string PolicyKey,
    string ToolName,
    string ArtifactPathArgumentName,
    IReadOnlyList<string> ForbiddenContentMarkers,
    string RejectionSummary);

internal interface IProcessToolReceiptEvidencePolicyContribution
{
    IEnumerable<ProcessToolReceiptTextEvidenceRule> ResolveRules(
        ProcessRuntimeStepAssignment assignment,
        ProcessStepOutcomeResult output);
}
