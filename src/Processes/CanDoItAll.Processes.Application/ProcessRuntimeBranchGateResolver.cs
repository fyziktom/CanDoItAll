using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

internal static class ProcessRuntimeBranchGateResolver
{
    internal static ProcessRuntimeBranchGateResolution Resolve(ProcessTemplateDefinitionStepDocument step)
    {
        ArgumentNullException.ThrowIfNull(step);

        var branchDependencies = ProcessTemplateKernelBuilder.EnumerateDependencies(step)
            .Where(dependency => !string.IsNullOrWhiteSpace(dependency.BranchOutcomeKey))
            .DistinctBy(
                dependency => $"{dependency.StepKey}\u001f{dependency.BranchOutcomeKey}",
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (branchDependencies.Length > 1)
        {
            return new ProcessRuntimeBranchGateResolution(
                BranchGate: null,
                $"Step '{step.Key}' declares multiple branch-conditioned dependencies ({string.Join(", ", branchDependencies.Select(dependency => $"{dependency.StepKey}:{dependency.BranchOutcomeKey}"))}), but the runtime assignment contract supports one branch gate per step. Express prerequisite branch decisions transitively or split the step before launch.");
        }

        var branchDependency = branchDependencies.FirstOrDefault();
        return new ProcessRuntimeBranchGateResolution(
            string.IsNullOrWhiteSpace(branchDependency?.StepKey)
                ? null
                : new ProcessRuntimeBranchGate(branchDependency.StepKey, branchDependency.BranchOutcomeKey),
            Error: null);
    }
}

internal sealed record ProcessRuntimeBranchGateResolution(
    ProcessRuntimeBranchGate? BranchGate,
    string? Error)
{
    internal bool IsSupported => Error is null;
}
