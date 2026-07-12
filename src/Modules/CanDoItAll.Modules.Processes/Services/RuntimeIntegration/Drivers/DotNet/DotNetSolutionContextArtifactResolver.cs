using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

internal sealed class DotNetSolutionContextArtifactResolver(
    IWorkspaceFileService workspaceFiles,
    DotNetSolutionContextParser parser)
{
    public bool TryResolve(
        ProcessLaunchDriverArtifactBinding binding,
        IDictionary<string, string> variables,
        out DotNetSolutionContext context,
        out string issue)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(variables);

        context = null!;
        issue = string.Empty;
        var readOnlyVariables = variables as IReadOnlyDictionary<string, string>
            ?? new Dictionary<string, string>(variables, StringComparer.OrdinalIgnoreCase);
        if (!ProcessRuntimeLaunchVariables.TryReadParentRequiredArtifactBindings(readOnlyVariables, out var parentBindings))
        {
            issue = $"The .NET solution setup launch requires parent artifact binding '{binding.BindingKey}', but no structured parent artifact bindings were supplied.";
            return false;
        }

        var matches = parentBindings
            .Where(candidate =>
                string.Equals(candidate.SourceStepKey, binding.SourceStepKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(candidate.ArtifactExpectationKey, binding.ArtifactExpectationKey, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matches.Length != 1)
        {
            issue = matches.Length == 0
                ? $"The .NET solution setup launch requires parent artifact binding '{binding.BindingKey}' from '{binding.SourceStepKey}/{binding.ArtifactExpectationKey}', but it was not supplied."
                : $"The .NET solution setup launch found multiple parent artifacts for binding '{binding.BindingKey}' from '{binding.SourceStepKey}/{binding.ArtifactExpectationKey}'.";
            return false;
        }

        var readResult = workspaceFiles.ReadTextFile(matches[0].ArtifactRef, maxCharacters: 100000);
        if (!readResult.Succeeded)
        {
            issue = $"The .NET solution setup launch could not read bound artifact '{binding.BindingKey}': {readResult.Message}";
            return false;
        }

        return parser.TryParse(readResult.Content, out context, out issue);
    }
}
