using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessSubprocessContractRequest(
    string ParentProcessKey,
    string ParentStepKey,
    string ChildProcessKey);

internal interface IProcessSubprocessContractProvider
{
    bool TryResolve(
        ProcessSubprocessContractRequest request,
        out ProcessSubprocessContract contract);
}

internal sealed class ProcessSubprocessContractResolver(
    IEnumerable<IProcessSubprocessContractProvider> providers)
{
    private readonly IReadOnlyList<IProcessSubprocessContractProvider> providers = providers.ToArray();

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

        var request = new ProcessSubprocessContractRequest(
            ResolveLaunchVariable(launchVariables, ProcessRuntimeLaunchVariables.ProcessDefinitionKey),
            stepKey,
            ResolveLaunchVariable(launchVariables, ProcessRuntimeLaunchVariables.ProcessStepSubprocessDefinitionKey));
        var matches = providers
            .Select(provider => provider.TryResolve(request, out var candidate) ? candidate : null)
            .Where(candidate => candidate is not null)
            .ToArray();
        switch (matches.Length)
        {
            case 0:
                contract = new ProcessSubprocessContract();
                return false;

            case 1:
                contract = matches[0]!;
                return true;

            default:
                throw new InvalidOperationException(
                    $"Multiple subprocess contract providers handled '{request.ParentProcessKey}:{request.ParentStepKey}:{request.ChildProcessKey}'. Provider ownership must be unambiguous.");
        }
    }

    private static string ResolveLaunchVariable(
        IReadOnlyDictionary<string, string> launchVariables,
        string key)
        => launchVariables.TryGetValue(key, out var value) ? value.Trim() : string.Empty;
}
