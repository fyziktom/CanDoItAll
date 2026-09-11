using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Core;

public static class ToolInvocationWorkspacePathFacts {
    private static readonly HashSet<string> ExternalTargetManagedWorkspaceIsolationTools = new(StringComparer.OrdinalIgnoreCase) {
        ToolContractCatalog.WorkspaceListDirectory,
        ToolContractCatalog.WorkspaceListFiles,
        ToolContractCatalog.WorkspaceSearch,
        ToolContractCatalog.WorkspaceReadFile,
        ToolContractCatalog.WorkspaceStatPath,
        ToolContractCatalog.WorkspaceHashPath,
        ToolContractCatalog.WorkspaceDiffText,
        ToolContractCatalog.WorkspaceCreateDirectory,
        ToolContractCatalog.WorkspaceWriteFile,
        ToolContractCatalog.WorkspaceAppendFile,
        ToolContractCatalog.WorkspaceCopyPath,
        ToolContractCatalog.WorkspaceMovePath,
        ToolContractCatalog.WorkspaceDeletePath,
        ToolContractCatalog.WorkspaceZipPath,
        ToolContractCatalog.WorkspaceUnzipArchive,
        ToolContractCatalog.WorkspaceDotNetNew,
        ToolContractCatalog.WorkspaceDotNetRestore,
        ToolContractCatalog.WorkspaceDotNetBuild,
        ToolContractCatalog.WorkspaceDotNetTest,
        ToolContractCatalog.WorkspaceDotNetRun,
        ToolContractCatalog.WorkspaceDotNetStop,
        ToolContractCatalog.WorkspacePowerShellRunScript,
        ToolContractCatalog.WorkspacePythonRunFile,
        ToolContractCatalog.WorkspaceInspectImage,
        ToolContractCatalog.WorkspaceAnalyzeImage,
        ToolContractCatalog.WorkspaceAnalyzeImages
    };

    private static readonly HashSet<string> WorkspaceFileMutationTools = new(StringComparer.OrdinalIgnoreCase) {
        ToolContractCatalog.WorkspaceCreateDirectory,
        ToolContractCatalog.WorkspaceWriteFile,
        ToolContractCatalog.WorkspaceAppendFile,
        ToolContractCatalog.WorkspaceCopyPath,
        ToolContractCatalog.WorkspaceMovePath,
        ToolContractCatalog.WorkspaceDeletePath,
        ToolContractCatalog.WorkspaceDotNetNew
    };

    public static bool IsWorkspaceFileMutationTool(string toolName) => WorkspaceFileMutationTools.Contains(toolName);

    public static bool IsWorkspaceBoundaryTool(string toolName) => ExternalTargetManagedWorkspaceIsolationTools.Contains(toolName);

    public static bool IsManagedProjectMediaPathForCurrentProject(string path, ToolInvocationPolicyContext context)
        => string.Equals(context.ContextWorkspaceScopeKind, WorkspaceScopeKind.Project.ToString(), StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(context.ContextWorkspaceScopeKey)
            && ManagedProjectMediaPath.IsForProject(path, context.ContextWorkspaceScopeKey);

    private static readonly Regex ExternalTargetAliasRegex = new(
        @"\bexternal-target/(?:v1/[0-9a-f]{24}|[A-Za-z])(?:/[^\s,;`""'\]\)}]+)?",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex ConsecutiveSlashRegex = new(
        "/{2,}",
        RegexOptions.CultureInvariant);

    private static readonly HashSet<string> WorkspaceScriptExecutionTools = new(StringComparer.OrdinalIgnoreCase) {
        AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript,
        AgentToolInvocationPolicyMetadata.WorkspacePythonRunFile
    };

    public static IReadOnlyList<string> ResolveScriptDeclaredOutputPaths(
        ToolInvocationPolicyContext context) {
        return ResolveManagedWorkspacePathArguments(context)
            .Where(argument => argument.Name.Contains("output", StringComparison.OrdinalIgnoreCase))
            .Select(argument => argument.Value)
            .ToArray();
    }

    public static IReadOnlyList<string> ResolveExternalTargetAliasesFromText(string? text) {
        if (string.IsNullOrWhiteSpace(text)) {
            return [];
        }

        return ExternalTargetAliasRegex
            .Matches(text)
            .Select(match => NormalizeExternalTargetAlias(match.Value))
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .ToArray();
    }

    public static IReadOnlyList<string> ResolveExternalTargetAliasesFromManifest(
        GovernedScriptSideEffectManifest? manifest) {
        if (manifest is null) {
            return [];
        }

        return manifest.DeclaredReadPaths
            .Concat(manifest.DeclaredWritePaths)
            .Concat(manifest.DeclaredChildScripts)
            .SelectMany(ResolveExternalTargetAliasesFromText)
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .ToArray();
    }

    public static IReadOnlyList<string> ResolveReferencedExternalTargetAliases(
        ToolInvocationPolicyContext context) {
        return ResolveExternalTargetAliasesFromArguments(
            context,
            argumentName =>
                ToolInvocationPathArgumentResolver.IsPathLikeArgumentName(argumentName) ||
                (WorkspaceScriptExecutionTools.Contains(context.ToolName) &&
                 ToolInvocationPathArgumentResolver.IsScriptArgumentsArgumentName(argumentName)));
    }

    public static IReadOnlyList<string> ResolveExternalTargetAliasesFromArguments(
        ToolInvocationPolicyContext context,
        Func<string, bool> argumentNamePredicate) {
        return ResolveManagedWorkspacePathArguments(context)
            .Where(argument => argumentNamePredicate(argument.Name))
            .SelectMany(argument => ExternalTargetAliasRegex
                .Matches(argument.Value ?? string.Empty)
                .Select(match => NormalizeExternalTargetAlias(match.Value)))
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .ToArray();
    }

    public static IReadOnlyList<string> NormalizeAllowedExternalTargetAliases(
        IReadOnlyList<string>? aliases) {
        return aliases?
            .Select(NormalizeExternalTargetAlias)
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .OrderByDescending(alias => alias.Length)
            .ToArray() ?? [];
    }

    public static bool IsAllowedExternalTargetAlias(
        string referencedAlias,
        IReadOnlyList<string> allowedAliases) {
        return allowedAliases.Any(allowedAlias =>
            ExternalTargetAliasCodec.IsAliasWithinRoot(referencedAlias, allowedAlias));
    }

    public static string NormalizeExternalTargetAlias(string? alias) {
        return AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(alias) ?? string.Empty;
    }

    public static IReadOnlyList<ToolInvocationPathArgument> ResolveManagedWorkspacePathArguments(
        ToolInvocationPolicyContext context) {
        return context.PathArguments.Values
            .Where(argument => !string.IsNullOrWhiteSpace(argument.Value))
            .ToArray();
    }

    public static bool IsPathLikeArgumentName(string argumentName) {
        return ToolInvocationPathArgumentResolver.IsPathLikeArgumentName(argumentName);
    }

    public static bool IsExternalTargetAliasPath(string normalizedPath) {
        return normalizedPath.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsManagedOutputPath(string normalizedPath) {
        return string.Equals(normalizedPath, "output", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith("output/", StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeManagedWorkspacePath(string? path) {
        if (string.IsNullOrWhiteSpace(path)) {
            return string.Empty;
        }

        var syntax = PhysicalPathSyntaxClassifier.Classify(path);
        if (syntax is PhysicalPathSyntax.WindowsDriveAbsolute or PhysicalPathSyntax.WindowsUnc) {
            return OperatingSystem.IsWindows()
                ? path.Replace('\\', '/')
                : string.Empty;
        }

        if (syntax == PhysicalPathSyntax.UnixAbsolute) {
            return OperatingSystem.IsWindows()
                ? string.Empty
                : path;
        }

        var normalizedPath = path
            .Trim()
            .Trim('`', '"', '\'')
            .TrimEnd('.', ',', ';', ':', ')', ']', '}');
        normalizedPath = normalizedPath.Replace('\\', '/');
        normalizedPath = ConsecutiveSlashRegex.Replace(normalizedPath, "/");

        while (normalizedPath.StartsWith("./", StringComparison.Ordinal)) {
            normalizedPath = normalizedPath[2..];
        }

        return normalizedPath.TrimStart('/');
    }

    public static bool IsWorkspaceScriptExecutionTool(string toolName)
        => WorkspaceScriptExecutionTools.Contains(toolName);
}
