using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessSubprocessContractResolver
{
    public bool TryResolve(
        ProcessRuntimeStepAssignment assignment,
        out ProcessSubprocessContract contract)
    {
        ArgumentNullException.ThrowIfNull(assignment);
        return TryResolve(assignment.LaunchVariables, assignment.StepKey, out contract);
    }

    public bool TryResolve(
        IReadOnlyDictionary<string, string> launchVariables,
        string stepKey,
        out ProcessSubprocessContract contract)
    {
        if (ProcessRuntimeLaunchVariables.TryReadProcessStepSubprocessContract(
                launchVariables,
                out contract))
        {
            return true;
        }

        contract = new ProcessSubprocessContract();
        return false;
    }
}
