using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Maf;

internal static class EffectiveExternalTargetContextBuilder
{
    internal const int MaximumRenderedTargetCount = 64;
    internal const int MaximumRenderedTargetCharacters = 12_000;

    public static string Build(
        EffectiveExternalTargetAccessScope accessScope,
        bool recursiveFileDiscoveryAvailable,
        bool writeOperationsAvailable)
    {
        ArgumentNullException.ThrowIfNull(accessScope);
        if (!recursiveFileDiscoveryAvailable ||
            accessScope.WritableAliases.Count == 0 && accessScope.ReadOnlyAliases.Count == 0)
        {
            return string.Empty;
        }

        var aliases = accessScope.WritableAliases
            .Concat(accessScope.ReadOnlyAliases)
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .OrderBy(alias => alias, StringComparer.Ordinal)
            .ToArray();
        var targetLines = new List<string>();
        var renderedTargetCharacters = 0;
        foreach (var alias in aliases)
        {
            if (targetLines.Count >= MaximumRenderedTargetCount)
            {
                break;
            }

            var accessLabel = ResolveAccessLabel(accessScope, alias, writeOperationsAvailable);
            var line = $"- {RenderAlias(alias)} ({accessLabel})";
            var separatorCharacters = targetLines.Count == 0 ? 0 : Environment.NewLine.Length;
            if (renderedTargetCharacters + separatorCharacters + line.Length > MaximumRenderedTargetCharacters)
            {
                break;
            }

            targetLines.Add(line);
            renderedTargetCharacters += separatorCharacters + line.Length;
        }

        var omittedTargetCount = aliases.Length - targetLines.Count;
        if (omittedTargetCount > 0)
        {
            targetLines.Add(
                $"- Context limit reached: {omittedTargetCount} additional authorized target alias " +
                $"{(omittedTargetCount == 1 ? "entry was" : "entries were")} omitted. " +
                "Runtime authorization is unchanged; do not guess omitted aliases or probe parent roots.");
        }

        return $"""
Effective external workspace targets for this invocation are listed below. Runtime policy enforces the complete effective scope. Use only exact aliases you already know; do not broaden, infer omitted aliases, or probe parent roots.
{string.Join(Environment.NewLine, targetLines)}

More-specific alias entries override broader entries. Labels reflect both scope and currently attached tools; every operation remains subject to runtime policy.

Project structure is not a recursive filesystem index. Absence of a `.csproj` node does not prove that no `.csproj` exists under an independently authorized target above. When an implementation file is required and canonical project-structure reads do not identify it, use `workspace_list_directory` on the exact alias when that tool is available and the folder shape is unknown. Then call `workspace_list_files` with `relativePath` set to that alias and a bounded glob such as `**/*.csproj`. Use `**/*.sln` or `**/*.slnx` when solution discovery is required. Perform this authorized discovery before asking the user for a path.
""";
    }

    private static string ResolveAccessLabel(
        EffectiveExternalTargetAccessScope accessScope,
        string alias,
        bool writeOperationsAvailable)
    {
        if (!accessScope.CanWrite(alias))
        {
            return "read-only scope";
        }

        return writeOperationsAvailable
            ? "read/write scope"
            : "read-only with currently attached tools";
    }

    private static string RenderAlias(string alias)
    {
        return JsonSerializer.Serialize(alias)
            .Replace("`", "\\u0060", StringComparison.Ordinal);
    }
}
