using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal sealed class DotNetSolutionSetupRuntimeToolPlanGuard : IProcessRuntimeToolPlanGuard
{
    public ProcessRuntimeToolPlanGuardEvaluation Evaluate(ProcessRuntimeStepAssignment assignment)
    {
        var evaluation = DotNetSolutionSetupToolPlanGuard.Evaluate(assignment);
        return new ProcessRuntimeToolPlanGuardEvaluation(
            "dotnet-solution-setup",
            evaluation.Issues);
    }
}
