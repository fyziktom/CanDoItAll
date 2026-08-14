using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class MafScriptPolicyInspectionService
{
    private const long MaxPolicyInspectedScriptBytes = 128 * 1024;

    private readonly string workspaceRoot;
    private readonly IPhysicalFileSystemPathPolicy workspacePathPolicy;
    private readonly WorkspaceScopeDescriptor workspaceScope;
    private readonly IExternalTargetPathRegistry externalTargetPathRegistry;

    public MafScriptPolicyInspectionService(
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope,
        IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
        IExternalTargetPathRegistry externalTargetPathRegistry)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new ArgumentException("Workspace root must be provided.", nameof(workspaceRoot));
        }

        workspacePathPolicy = physicalPathPolicyFactory.Create(workspaceRoot);
        this.workspaceRoot = workspacePathPolicy.RootPath;
        this.workspaceScope = workspaceScope ?? throw new ArgumentNullException(nameof(workspaceScope));
        this.externalTargetPathRegistry = externalTargetPathRegistry ??
            throw new ArgumentNullException(nameof(externalTargetPathRegistry));
    }

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
                    .Distinct(ExternalTargetAliasCodec.EqualityComparer)
                    .ToArray();
                if (!AgentWorkspaceToolAccessMetadata.IsExternalTargetAliasAllowed(normalizedAlias, readableAliases))
                {
                    failureMessage = $"script path '{normalizedAlias}' is outside the current run external-target boundary.";
                    return false;
                }
            }

            var resolution = externalTargetPathRegistry.TryResolve(
                normalizedAlias,
                out fullPath,
                out var validationMessage);
            if (resolution == ExternalTargetAliasResolutionKind.Resolved)
            {
                return true;
            }

            failureMessage = resolution == ExternalTargetAliasResolutionKind.NotVersionedAlias
                ? $"script path '{normalizedAlias}' uses a legacy external-target alias that requires migration before execution."
                : $"script path '{normalizedAlias}' could not be resolved: {validationMessage}";
            fullPath = string.Empty;
            return false;
        }

        var expandedPath = MafRuntimePathResolver.ExpandPortablePath(scriptPath.Trim());
        if (!TryValidateNativePathSyntax(expandedPath, scriptPath, out failureMessage))
        {
            return false;
        }

        if (Path.IsPathRooted(expandedPath))
        {
            fullPath = Path.GetFullPath(expandedPath);
            if (!workspacePathPolicy.IsWithinRoot(fullPath))
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
        if (workspacePathPolicy.IsWithinRoot(fullPath))
        {
            return true;
        }

        failureMessage = $"script path '{scriptPath}' resolves outside the workspace root.";
        fullPath = string.Empty;
        return false;
    }

    private static bool TryValidateNativePathSyntax(
        string path,
        string suppliedPath,
        out string failureMessage)
    {
        failureMessage = string.Empty;
        try
        {
            PhysicalPathSyntaxPolicy.EnsureNativeOrRelative(path, "MAF script-policy path");
            return true;
        }
        catch (InvalidOperationException exception)
        {
            failureMessage = $"script path '{suppliedPath}' is not valid on this host: {exception.Message}";
            return false;
        }
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
