using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Core;

internal sealed class ProjectWorkspaceScopePolicy
{
    private const string AmbiguousProjectStructureBrief = "project-structure-context-brief.md";

    private static readonly HashSet<string> BroadDiscoveryTools = new(StringComparer.OrdinalIgnoreCase)
    {
        ToolContractCatalog.WorkspaceListDirectory,
        ToolContractCatalog.WorkspaceListFiles,
        ToolContractCatalog.WorkspaceSearch
    };

    public ToolInvocationPolicyDecision? EvaluateProjectScope(
        ToolInvocationPolicyContext context,
        string signature)
    {
        if (!IsProjectScopedWorkspaceRead(context))
        {
            return null;
        }

        if (!context.PathArguments.IsComplete)
        {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"Project-scoped tool '{context.ToolName}' supplied unsupported path argument shape(s): {string.Join(", ", context.PathArguments.UnsupportedArgumentNames)}. Paths must be scalar strings, while plural path arguments must contain only strings.");
        }

        var pathArguments = context.PathArguments.Values
            .Where(argument => !string.IsNullOrWhiteSpace(argument.Value))
            .ToArray();
        if (pathArguments.Length == 0)
        {
            return BroadDiscoveryTools.Contains(context.ToolName)
                ? DenyBroadDiscovery(context, signature)
                : null;
        }

        foreach (var pathArgument in pathArguments)
        {
            if (IsForeignPhysicalPath(pathArgument.Value))
            {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    $"Project-scoped tool '{context.ToolName}' cannot use foreign-host physical path syntax. Use a canonical workspace-relative path, a native managed-workspace path, or an independently authorized external-target alias.");
            }

            var normalizedPath = ManagedProjectMediaPath.NormalizeForMatching(pathArgument.Value);
            if (BroadDiscoveryTools.Contains(context.ToolName) &&
                IsBroadWorkspaceRoot(normalizedPath))
            {
                return DenyBroadDiscovery(context, signature);
            }

            if (ManagedProjectMediaPath.HasParentTraversalSegment(pathArgument.Value))
            {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    $"Project-scoped workspace access cannot use path '{normalizedPath}' because relative traversal segments are not allowed. Use an exact current-project managed-media path or an independently authorized external-target alias.");
            }

            if (WorkspacePathPolicy.IsExternalTargetAliasPath(pathArgument.Value))
            {
                continue;
            }

            if (ManagedProjectMediaPath.IsProjectMediaPath(normalizedPath))
            {
                if (ManagedProjectMediaPath.IsForProject(
                        normalizedPath,
                        context.ContextWorkspaceScopeKey))
                {
                    continue;
                }

                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    $"Project-scoped workspace access cannot use managed project media '{normalizedPath}' because it is not owned by project '{context.ContextWorkspaceScopeKey}'. Use project_structure_read to obtain canonical current-project assets, then read only paths under that project's managed media folder.");
            }

            if (ManagedProjectMediaPath.HasProjectMediaMarker(pathArgument.Value))
            {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    $"Project-scoped workspace access cannot use non-canonical managed project media path '{normalizedPath}'. Use the exact current-project path rooted at '{ManagedProjectMediaPath.FilesRoot}'.");
            }

            if (IsAmbiguousSharedBrief(normalizedPath))
            {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    $"Project-scoped workspace access cannot read the shared-root '{AmbiguousProjectStructureBrief}' because it is not project-owned and may be stale. Use project_structure_read or a current-project path under '{ManagedProjectMediaPath.FilesRoot}'.");
            }

            if (BroadDiscoveryTools.Contains(context.ToolName) &&
                Path.IsPathRooted(pathArgument.Value))
            {
                return DenyBroadDiscovery(context, signature);
            }
        }

        return null;
    }

    public bool IsManagedProjectMediaPathForCurrentProject(
        string path,
        ToolInvocationPolicyContext context)
    {
        return IsProjectScope(context) &&
               ManagedProjectMediaPath.IsForProject(
                   path,
                   context.ContextWorkspaceScopeKey);
    }

    private static bool IsProjectScopedWorkspaceRead(ToolInvocationPolicyContext context)
    {
        return context.Classification == ToolInvocationClassification.Read &&
               IsProjectScope(context) &&
               ToolContractCatalog.WorkspaceToolNames.Contains(
                   context.ToolName,
                   StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsProjectScope(ToolInvocationPolicyContext context)
    {
        return string.Equals(
                   context.ContextWorkspaceScopeKind,
                   WorkspaceScopeKind.Project.ToString(),
                   StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(context.ContextWorkspaceScopeKey);
    }

    private static bool IsBroadWorkspaceRoot(string path)
    {
        return string.IsNullOrWhiteSpace(path) ||
               string.Equals(path, ".", StringComparison.Ordinal) ||
               string.Equals(path, "workspace", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(path, "managed-files", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsForeignPhysicalPath(string path)
    {
        return PhysicalPathSyntaxClassifier.Classify(path) switch
        {
            PhysicalPathSyntax.WindowsDriveAbsolute or PhysicalPathSyntax.WindowsUnc => !OperatingSystem.IsWindows(),
            PhysicalPathSyntax.UnixAbsolute => OperatingSystem.IsWindows(),
            _ => false
        };
    }

    private static bool IsAmbiguousSharedBrief(string path)
    {
        return string.Equals(path, AmbiguousProjectStructureBrief, StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith('/' + AmbiguousProjectStructureBrief, StringComparison.OrdinalIgnoreCase);
    }

    private static ToolInvocationPolicyDecision DenyBroadDiscovery(
        ToolInvocationPolicyContext context,
        string signature)
    {
        return ToolInvocationPolicyDecision.Deny(
            signature,
            $"Project-scoped tool '{context.ToolName}' cannot discover the shared workspace root because it can expose stale or foreign project material. Use project_structure_read, an exact current-project managed-media path, or an independently authorized external-target alias.");
    }
}
