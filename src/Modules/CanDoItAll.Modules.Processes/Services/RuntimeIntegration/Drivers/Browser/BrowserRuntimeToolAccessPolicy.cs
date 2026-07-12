using CanDoItAll.Processes.Runtime;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal static class BrowserRuntimeToolAccessPolicy
{
    internal static bool AllowsBrowserTools(ProcessRuntimeStepAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        return assignment.AllowedOperations.Contains(
            ProcessOperationContractNames.CaptureRuntimeProof,
            StringComparer.OrdinalIgnoreCase);
    }
}
