using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class MafScriptPolicyInspectionService(
    string workspaceRoot,
    WorkspaceScopeDescriptor workspaceScope)
{
    private const long MaxPolicyInspectedScriptBytes = 128 * 1024;

    private readonly string workspaceRoot = Path.GetFullPath(workspaceRoot);

    public ScriptContentInspection ResolveScriptContentInspectionForPolicy(
        string functionName,
        IReadOnlyList<KeyValuePair<string, object?>> arguments,
        WorkspaceExecutionAuditContext.WorkspaceExecutionAuditScopeState? auditScope,
        string scriptSideEffectManifestJson)
    {
        if (!IsWorkspaceScriptExecutionTool(functionName))
        {
            return ScriptContentInspection.Empty;
        }

        var scriptPath = TryGetStringArgument(arguments, "path");
        if (string.IsNullOrWhiteSpace(scriptPath))
        {
            return new ScriptContentInspection(
                string.Empty,
                "script invocation did not provide a path argument.");
        }

        if (!TryResolvePolicyReadableScriptPath(scriptPath, auditScope, out var fullPath, out var failureMessage))
        {
            return new ScriptContentInspection(string.Empty, failureMessage);
        }

        try
        {
            var fileInfo = new FileInfo(fullPath);
            if (!fileInfo.Exists)
            {
                return new ScriptContentInspection(
                    string.Empty,
                    $"script path '{scriptPath}' does not exist.");
            }

            if (fileInfo.Length > MaxPolicyInspectedScriptBytes)
            {
                return new ScriptContentInspection(
                    string.Empty,
                    $"script path '{scriptPath}' is larger than the {MaxPolicyInspectedScriptBytes} byte policy inspection limit.");
            }

            var inspectedContent = File.ReadAllText(fullPath);
            if (GovernedScriptSideEffectManifest.TryParse(
                    scriptSideEffectManifestJson,
                    out var manifest,
                    out _) &&
                manifest.DeclaredChildScripts.Length > 0)
            {
                var childInspection = ResolveDeclaredChildScriptInspection(manifest, auditScope);
                if (!string.IsNullOrWhiteSpace(childInspection.FailureMessage))
                {
                    return childInspection;
                }

                inspectedContent = string.Join(
                    Environment.NewLine,
                    inspectedContent,
                    childInspection.Content);
            }

            return new ScriptContentInspection(inspectedContent, string.Empty);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new ScriptContentInspection(
                string.Empty,
                $"script path '{scriptPath}' could not be read for policy inspection: {exception.Message}");
        }
    }

    public static IEnumerable<KeyValuePair<string, object?>> ResolveFunctionInvocationArguments(object context)
    {
        foreach (var propertyName in new[] { "Arguments", "FunctionArguments", "FunctionCallArguments" })
        {
            var property = context.GetType().GetProperty(propertyName);
            if (property?.GetValue(context) is IEnumerable<KeyValuePair<string, object?>> pairs)
            {
                return pairs;
            }
        }

        return [];
    }

    private ScriptContentInspection ResolveDeclaredChildScriptInspection(
        GovernedScriptSideEffectManifest manifest,
        WorkspaceExecutionAuditContext.WorkspaceExecutionAuditScopeState? auditScope)
    {
        var inspectedChildScripts = new List<string>();
        foreach (var childScript in manifest.DeclaredChildScripts)
        {
            if (!TryResolvePolicyReadableScriptPath(childScript, auditScope, out var childFullPath, out var failureMessage))
            {
                return new ScriptContentInspection(
                    string.Empty,
                    $"declared child script '{childScript}' could not be resolved for policy inspection: {failureMessage}");
            }

            try
            {
                var childFileInfo = new FileInfo(childFullPath);
                if (!childFileInfo.Exists)
                {
                    return new ScriptContentInspection(
                        string.Empty,
                        $"declared child script '{childScript}' does not exist.");
                }

                if (childFileInfo.Length > MaxPolicyInspectedScriptBytes)
                {
                    return new ScriptContentInspection(
                        string.Empty,
                        $"declared child script '{childScript}' is larger than the {MaxPolicyInspectedScriptBytes} byte policy inspection limit.");
                }

                inspectedChildScripts.Add(DefaultAgentToolInvocationPolicy.BuildInspectedChildScriptMarker(childScript));
                inspectedChildScripts.Add(File.ReadAllText(childFullPath));
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                return new ScriptContentInspection(
                    string.Empty,
                    $"declared child script '{childScript}' could not be read for policy inspection: {exception.Message}");
            }
        }

        return new ScriptContentInspection(
            string.Join(Environment.NewLine, inspectedChildScripts),
            string.Empty);
    }

    private bool TryResolvePolicyReadableScriptPath(
        string scriptPath,
        WorkspaceExecutionAuditContext.WorkspaceExecutionAuditScopeState? auditScope,
        out string fullPath,
        out string failureMessage)
    {
        fullPath = string.Empty;
        failureMessage = string.Empty;

        var normalizedAlias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(scriptPath);
        if (!string.IsNullOrWhiteSpace(normalizedAlias) &&
            normalizedAlias.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase))
        {
            if (auditScope is not null)
            {
                var readableAliases = auditScope.AllowedExternalTargetAliases
                    .Concat(auditScope.ReadOnlyExternalTargetAliases)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (!AgentWorkspaceToolAccessMetadata.IsExternalTargetAliasAllowed(normalizedAlias, readableAliases))
                {
                    failureMessage = $"script path '{normalizedAlias}' is outside the current run external-target boundary.";
                    return false;
                }
            }

            return TryMapExternalTargetAliasToFullPath(normalizedAlias, out fullPath, out failureMessage);
        }

        var expandedPath = Environment.ExpandEnvironmentVariables(scriptPath.Trim());
        if (Path.IsPathRooted(expandedPath))
        {
            fullPath = Path.GetFullPath(expandedPath);
            if (!MafRuntimePathResolver.IsPathWithinRoot(fullPath, workspaceRoot))
            {
                failureMessage = $"absolute script path '{scriptPath}' is outside the workspace root.";
                fullPath = string.Empty;
                return false;
            }

            return true;
        }

        var scopedRelativePath = ApplyManagedRootScopeForPolicy(WorkspaceScopeDescriptor.NormalizeRelativePath(expandedPath));
        if (string.IsNullOrWhiteSpace(scopedRelativePath))
        {
            failureMessage = "script path resolved to an empty workspace-relative path.";
            return false;
        }

        fullPath = Path.GetFullPath(Path.Combine(
            workspaceRoot,
            scopedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (MafRuntimePathResolver.IsPathWithinRoot(fullPath, workspaceRoot))
        {
            return true;
        }

        failureMessage = $"script path '{scriptPath}' resolves outside the workspace root.";
        fullPath = string.Empty;
        return false;
    }

    private static bool TryMapExternalTargetAliasToFullPath(
        string alias,
        out string fullPath,
        out string failureMessage)
    {
        fullPath = string.Empty;
        failureMessage = string.Empty;

        var segments = alias.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2 ||
            !string.Equals(segments[0], "external-target", StringComparison.OrdinalIgnoreCase) ||
            segments[1].Length != 1 ||
            !char.IsLetter(segments[1][0]))
        {
            failureMessage = $"script path '{alias}' uses invalid external-target syntax.";
            return false;
        }

        var driveRoot = $"{char.ToUpperInvariant(segments[1][0])}:{Path.DirectorySeparatorChar}";
        fullPath = segments.Length == 2
            ? driveRoot
            : Path.GetFullPath(Path.Combine(driveRoot, Path.Combine(segments.Skip(2).ToArray())));
        return true;
    }

    private string ApplyManagedRootScopeForPolicy(string relativePath)
    {
        if (workspaceScope.IsDefaultSandbox)
        {
            return relativePath;
        }

        return TryMapManagedRootForPolicy(relativePath, "artifacts", workspaceScope.ArtifactRootRelativePath)
            ?? TryMapManagedRootForPolicy(relativePath, "output", workspaceScope.OutputRootRelativePath)
            ?? TryMapManagedRootForPolicy(relativePath, "integration-map", workspaceScope.IntegrationMapRootRelativePath)
            ?? TryMapManagedRootForPolicy(relativePath, "data", workspaceScope.DataRootRelativePath)
            ?? relativePath;
    }

    private static string? TryMapManagedRootForPolicy(
        string relativePath,
        string rootName,
        string scopedRootRelativePath)
    {
        if (!MatchesPolicyPathRoot(relativePath, rootName) ||
            MatchesPolicyPathRoot(relativePath, scopedRootRelativePath) ||
            relativePath.StartsWith($"{rootName}/scopes/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var suffix = string.Equals(relativePath, rootName, StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : relativePath[(rootName.Length + 1)..];
        return string.IsNullOrWhiteSpace(suffix)
            ? scopedRootRelativePath
            : WorkspaceScopeDescriptor.NormalizeRelativePath(Path.Combine(scopedRootRelativePath, suffix));
    }

    private static bool MatchesPolicyPathRoot(string relativePath, string rootRelativePath)
    {
        return string.Equals(relativePath, rootRelativePath, StringComparison.OrdinalIgnoreCase) ||
               relativePath.StartsWith(rootRelativePath + "/", StringComparison.OrdinalIgnoreCase);
    }

    public static string? TryGetStringArgument(
        IEnumerable<KeyValuePair<string, object?>> arguments,
        string argumentName)
    {
        foreach (var argument in arguments)
        {
            if (!string.Equals(argument.Key, argumentName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return argument.Value switch
            {
                string value => value,
                JsonElement { ValueKind: JsonValueKind.String } value => value.GetString(),
                null => null,
                _ => argument.Value.ToString()
            };
        }

        return null;
    }

    private static bool IsWorkspaceScriptExecutionTool(string functionName)
    {
        return string.Equals(functionName, AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(functionName, AgentToolInvocationPolicyMetadata.WorkspacePythonRunFile, StringComparison.OrdinalIgnoreCase);
    }
}
