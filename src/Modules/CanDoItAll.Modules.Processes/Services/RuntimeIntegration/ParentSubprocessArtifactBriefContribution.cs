using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal static class ParentSubprocessArtifactBriefContribution
{
    public static string Build(IReadOnlyDictionary<string, string> launchVariables)
    {
        ArgumentNullException.ThrowIfNull(launchVariables);

        if (!ProcessRuntimeLaunchVariables.TryReadParentRequiredArtifactRefs(
                launchVariables,
                out var artifactRefs))
        {
            return "No inherited parent-step artifact refs.";
        }

        return string.Join(
            Environment.NewLine,
            new[]
            {
                "These exact managed refs were required inputs of the parent subprocess step. The process adapter loads every ref into a runtime-hydrated inherited artifact section before invoking the agent, so use that content for diagnosis, mutation, review, or acceptance. If a hydrated section is marked truncated, call workspace_read_file on its exact ref for additional detail. The refs may contain runtime-appended gate findings that are not present in the original agent summary."
            }.Concat(artifactRefs.Select(artifactRef => $"- {artifactRef}")));
    }
}
