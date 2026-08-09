using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Application;
using CanDoItAll.SharedKernel;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Modules.Processes;

internal sealed class WorkspaceProductTargetFilesystemStateLaunchVariableContributor(
    IWorkspaceFileService workspaceFiles,
    IExternalTargetPathRegistry externalTargetPathRegistry) : IProcessLaunchVariableContributor
{
    internal const string VariableName = "ProductTargetFilesystemState";

    private const string ProductRootAliasVariableName = "ProductRootAlias";
    private const string ExternalTargetRootVariableName = "ExternalTargetRoot";
    private const string OutputRootAliasVariableName = "OutputRootAlias";
    private const string ProductRootVariableName = "ProductRoot";
    private const string OutputRootVariableName = "OutputRoot";

    public void Enrich(
        ProcessLaunchPreparationContext context,
        IDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(variables);

        var target = FirstNonEmpty(
            ResolveVariable(variables, ProductRootAliasVariableName),
            ResolveVariable(variables, ExternalTargetRootVariableName),
            ResolveVariable(variables, OutputRootAliasVariableName),
            ResolveVariable(variables, ProductRootVariableName),
            ResolveVariable(variables, OutputRootVariableName));
        var targetAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(
            target,
            externalTargetPathRegistry);
        if (!string.IsNullOrWhiteSpace(target) && string.IsNullOrWhiteSpace(targetAlias))
        {
            variables[VariableName] = "unavailable";
            return;
        }

        if (string.IsNullOrWhiteSpace(targetAlias))
        {
            return;
        }

        var stat = workspaceFiles.StatPath(targetAlias);
        variables[VariableName] = ResolveState(stat);
    }

    private static string ResolveState(WorkspacePathStatResult stat)
    {
        if (stat.IsKnownMissing())
        {
            return "missing";
        }

        if (!stat.Succeeded)
        {
            return "unavailable";
        }

        if (!stat.Exists)
        {
            return "missing";
        }

        if (string.Equals(stat.PathKind, "file", StringComparison.OrdinalIgnoreCase))
        {
            return "not-directory";
        }

        if (!string.Equals(stat.PathKind, "directory", StringComparison.OrdinalIgnoreCase))
        {
            return "unavailable";
        }

        return stat.ChildCount switch
        {
            0 => "empty",
            > 0 => "populated",
            _ => "unavailable"
        };
    }

    private static string ResolveVariable(IDictionary<string, string> variables, string key)
        => variables.TryGetValue(key, out var value)
            ? value?.Trim() ?? string.Empty
            : string.Empty;

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
