using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessRuntimeProviderContextFactory
{
    internal static AgentRuntimeContextIntent Create(ProcessRuntimeStepAssignment assignment)
    {
        ArgumentNullException.ThrowIfNull(assignment);

        return new AgentRuntimeContextIntent(
            SourceKind: "process-step",
            SourceId: assignment.StepKey,
            ProcessRunId: assignment.RunId.Value.ToString("D"),
            ProcessStepId: assignment.StepInstanceId.Value.ToString("D"),
            TargetScope: assignment.OperationTargetScope,
            IsGovernedProcessStep: true,
            BrowserToolsAllowed: false,
            AllowsProductMutation: assignment.AllowedOperations.Any(operation =>
                string.Equals(operation, ProcessOperationContractNames.MutateProductTarget, StringComparison.OrdinalIgnoreCase)),
            WorkspaceToolProfile: null,
            WorkspaceScope: null,
            AllowedOperations: assignment.AllowedOperations,
            RuntimeToolProvidersEnabled: true,
            WorkspaceToolsEnabled: true,
            CapabilityScopeOverride: AgentFrameworkProcessCapabilityScopeTranslator.Translate(assignment.CapabilityScope));
    }
}
