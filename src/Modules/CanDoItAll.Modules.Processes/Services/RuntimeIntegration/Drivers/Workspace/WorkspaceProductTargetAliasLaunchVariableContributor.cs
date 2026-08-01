using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Application;

namespace CanDoItAll.Modules.Processes;

internal sealed class WorkspaceProductTargetAliasLaunchVariableContributor : IProcessLaunchVariableContributor
{
    public void Enrich(
        ProcessLaunchPreparationContext context,
        IDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(variables);

        var productRoot = FirstNonEmpty(
            ResolveVariable(variables, "ProductRoot"),
            ResolveVariable(variables, "OutputRoot"));
        if (string.IsNullOrWhiteSpace(productRoot))
        {
            return;
        }

        var externalTargetAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(productRoot);
        if (string.IsNullOrWhiteSpace(externalTargetAlias))
        {
            return;
        }

        variables["ExternalTargetRoot"] = externalTargetAlias;
        variables["OutputRootAlias"] = externalTargetAlias;
        variables["ProductRootAlias"] = externalTargetAlias;
        variables["WorkspaceAlias"] = externalTargetAlias;
    }

    private static string ResolveVariable(IDictionary<string, string> variables, string key)
        => variables.TryGetValue(key, out var value)
            ? value?.Trim() ?? string.Empty
            : string.Empty;

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
