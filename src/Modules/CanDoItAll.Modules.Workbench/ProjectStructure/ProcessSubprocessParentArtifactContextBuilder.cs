using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

internal static class ProcessSubprocessParentArtifactContextBuilder
{

    public static void Apply(
        IDictionary<string, string> launchVariables,
        ProcessRuntimeStateSnapshot parentState,
        ProcessStepInstanceId parentStepId)
        => Apply(launchVariables, parentState, parentStepId, workspaceFiles: null);

    public static void Apply(
        IDictionary<string, string> launchVariables,
        ProcessRuntimeStateSnapshot parentState,
        ProcessStepInstanceId parentStepId,
        IWorkspaceFileService? workspaceFiles)
    {
        ArgumentNullException.ThrowIfNull(launchVariables);
        ArgumentNullException.ThrowIfNull(parentState);

        launchVariables.Remove(ProcessRuntimeLaunchVariables.ParentRequiredArtifactRefs);
        launchVariables.Remove(ProcessRuntimeLaunchVariables.ParentRequiredArtifactBindings);

        var requiredDescriptors = ResolveRequiredArtifactDescriptors(parentState, parentStepId);
        if (requiredDescriptors.Count == 0)
        {
            return;
        }

        var bindings = ResolveRequiredArtifactBindings(parentState, parentStepId);
        if (bindings.Count > 0)
        {
            launchVariables[ProcessRuntimeLaunchVariables.ParentRequiredArtifactBindings] =
                ProcessRuntimeLaunchVariables.SerializeParentRequiredArtifactBindings(bindings);
        }

        var artifactRefs = ResolveRequiredArtifactRefs(requiredDescriptors, workspaceFiles);
        if (artifactRefs.Count > 0)
        {
            launchVariables[ProcessRuntimeLaunchVariables.ParentRequiredArtifactRefs] =
                ProcessRuntimeLaunchVariables.SerializeParentRequiredArtifactRefs(artifactRefs);
        }
    }

    internal static IReadOnlyList<string> ResolveRequiredArtifactRefs(
        ProcessRuntimeStateSnapshot parentState,
        ProcessStepInstanceId parentStepId,
        IWorkspaceFileService? workspaceFiles = null)
    {
        ArgumentNullException.ThrowIfNull(parentState);

        return ResolveRequiredArtifactRefs(
            ResolveRequiredArtifactDescriptors(parentState, parentStepId),
            workspaceFiles);
    }

    internal static IReadOnlyList<ProcessParentArtifactBindingRef> ResolveRequiredArtifactBindings(
        ProcessRuntimeStateSnapshot parentState,
        ProcessStepInstanceId parentStepId)
    {
        ArgumentNullException.ThrowIfNull(parentState);

        return ResolveRequiredArtifactDescriptors(parentState, parentStepId)
            .Where(descriptor => !string.IsNullOrWhiteSpace(descriptor.PrimaryManagedRef))
            .Select(descriptor => new ProcessParentArtifactBindingRef(
                descriptor.StepKey,
                descriptor.ArtifactExpectationKey,
                descriptor.PrimaryManagedRef))
            .Distinct()
            .OrderBy(binding => binding.SourceStepKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(binding => binding.ArtifactExpectationKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(binding => binding.ArtifactRef, StringComparer.OrdinalIgnoreCase)
            .Take(32)
            .ToArray();
    }

    private static IReadOnlyList<ProcessArtifactSlotDescriptor> ResolveRequiredArtifactDescriptors(
        ProcessRuntimeStateSnapshot parentState,
        ProcessStepInstanceId parentStepId)
    {
        ArgumentNullException.ThrowIfNull(parentState);

        var parentStep = parentState.Steps.FirstOrDefault(step => step.StepInstanceId == parentStepId);
        if (parentStep is null || parentStep.RequiredArtifactSlots.Count == 0)
        {
            return [];
        }

        var requiredDescriptors = parentStep.ArtifactDescriptors
            .Where(descriptor => parentStep.RequiredArtifactSlots.Contains(descriptor.SlotId))
            .ToArray();
        return requiredDescriptors;
    }

    private static IReadOnlyList<string> ResolveRequiredArtifactRefs(
        IReadOnlyList<ProcessArtifactSlotDescriptor> requiredDescriptors,
        IWorkspaceFileService? _)
    {
        var directRefs = requiredDescriptors
            .Select(descriptor => descriptor.PrimaryManagedRef)
            .Where(artifactRef => !string.IsNullOrWhiteSpace(artifactRef))
            .ToArray();
        return NormalizeRefs(directRefs);
    }

    private static IReadOnlyList<string> NormalizeRefs(IEnumerable<string> artifactRefs)
        => artifactRefs
            .Where(artifactRef => !string.IsNullOrWhiteSpace(artifactRef))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(artifactRef => artifactRef, StringComparer.OrdinalIgnoreCase)
            .Take(32)
            .ToArray();
}
