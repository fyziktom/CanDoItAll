using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal sealed class DotNetSolutionSetupRuntimeToolPlanGuard(
    IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory) : IProcessRuntimeToolPlanGuard
{
    public ProcessRuntimeToolPlanGuardEvaluation Evaluate(ProcessRuntimeStepAssignment assignment)
    {
        var evaluation = DotNetSolutionSetupToolPlanGuard.Evaluate(assignment, physicalPathPolicyFactory);
        return new ProcessRuntimeToolPlanGuardEvaluation(
            "dotnet-solution-setup",
            evaluation.Issues);
    }
}
