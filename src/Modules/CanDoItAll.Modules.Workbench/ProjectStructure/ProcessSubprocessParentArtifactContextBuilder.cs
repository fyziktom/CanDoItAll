using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Workbench;

internal static class ProcessSubprocessParentArtifactContextBuilder
{
    private static readonly Regex ManagedChildStepArtifactRefRegex = new(
        @"\bartifacts/process-runs/(?<runId>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})/steps/[A-Za-z0-9_-]+\.md\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MatchingChildRunIdRegex = new(
        @"\bMatching child process run ['`]?(?<runId>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})['`]?\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
        var artifactRefs = ResolveRequiredArtifactRefs(parentState, parentStepId, workspaceFiles);
        if (artifactRefs.Count == 0)
        {
            return;
        }

        launchVariables[ProcessRuntimeLaunchVariables.ParentRequiredArtifactRefs] =
            ProcessRuntimeLaunchVariables.SerializeParentRequiredArtifactRefs(artifactRefs);
    }

    internal static IReadOnlyList<string> ResolveRequiredArtifactRefs(
        ProcessRuntimeStateSnapshot parentState,
        ProcessStepInstanceId parentStepId,
        IWorkspaceFileService? workspaceFiles = null)
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
        var directRefs = requiredDescriptors
            .Select(descriptor => descriptor.PrimaryManagedRef)
            .Where(artifactRef => !string.IsNullOrWhiteSpace(artifactRef))
            .ToArray();
        if (workspaceFiles is null)
        {
            return NormalizeRefs(directRefs);
        }

        var bridgedChildRefs = requiredDescriptors
            .SelectMany(descriptor => ReadBridgedChildRefs(workspaceFiles, descriptor.PrimaryManagedRef));
        return NormalizeRefs(directRefs.Concat(bridgedChildRefs));
    }

    private static IEnumerable<string> ReadBridgedChildRefs(
        IWorkspaceFileService workspaceFiles,
        string parentArtifactRef)
    {
        if (string.IsNullOrWhiteSpace(parentArtifactRef))
        {
            yield break;
        }

        var readResult = workspaceFiles.ReadTextFile(parentArtifactRef, maxCharacters: 100000);
        if (!readResult.Succeeded)
        {
            yield break;
        }

        var childRunMatch = MatchingChildRunIdRegex.Match(readResult.Content);
        if (!childRunMatch.Success ||
            !readResult.Content.Contains("## Subprocess handoff completed", StringComparison.Ordinal) ||
            !readResult.Content.Contains("## Child evidence", StringComparison.Ordinal) ||
            !readResult.Content.Contains("## Runtime Accepted Completion Gates", StringComparison.Ordinal))
        {
            yield break;
        }

        foreach (Match match in ManagedChildStepArtifactRefRegex.Matches(readResult.Content))
        {
            if (string.Equals(
                    match.Groups["runId"].Value,
                    childRunMatch.Groups["runId"].Value,
                    StringComparison.OrdinalIgnoreCase))
            {
                yield return match.Value;
            }
        }
    }

    private static IReadOnlyList<string> NormalizeRefs(IEnumerable<string> artifactRefs)
        => artifactRefs
            .Where(artifactRef => !string.IsNullOrWhiteSpace(artifactRef))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(artifactRef => artifactRef, StringComparer.OrdinalIgnoreCase)
            .Take(32)
            .ToArray();
}
