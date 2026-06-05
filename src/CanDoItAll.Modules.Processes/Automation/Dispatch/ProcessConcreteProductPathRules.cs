using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessConcreteProductPathRules
{
    private const string ExternalTargetAliasRoot = "external-target";
    private static readonly Regex WorkspacePathInToolRequestRegex = new(
        @"(?<path>[A-Za-z]:\\[^`""'\r\n\s]+|external-target[\\/][^\s`""']+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ManagedWorkspacePathRegex = new(
        @"(?<path>(?:artifacts|output|integration-map|data)/(?:scopes/[^\s`""']+|[^\s`""']+))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static bool HasConcreteProductPath(ProcessAutomationToolExecutionReceipt receipt)
    {
        return ResolveWorkspacePathsFromReceipt(receipt)
            .Any(IsConcreteProductPath);
    }

    internal static bool HasConcreteProductDeliverableOrSourcePath(ProcessAutomationToolExecutionReceipt receipt)
    {
        return ResolveWorkspacePathsFromReceipt(receipt)
            .Any(IsConcreteProductDeliverableOrSourcePath);
    }

    internal static bool HasConcreteProductImplementationPath(
        bool requiresSourceOrProjectImplementationProof,
        ProcessAutomationToolExecutionReceipt receipt)
    {
        return requiresSourceOrProjectImplementationProof
            ? HasConcreteProductSourceOrProjectPath(receipt)
            : HasConcreteProductDeliverableOrSourcePath(receipt);
    }

    internal static bool HasConcreteProductSourceOrProjectPath(ProcessAutomationToolExecutionReceipt receipt)
    {
        return ResolveWorkspacePathsFromReceipt(receipt)
            .Any(IsConcreteProductSourceOrProjectPath);
    }

    internal static IReadOnlyList<string> ResolveWorkspacePathsFromReceipt(ProcessAutomationToolExecutionReceipt receipt)
    {
        var paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in ResolveWorkspacePathsFromToolRequest(receipt.RequestSummary))
        {
            paths.Add(path);
        }

        if (TryMapWorkspacePathForPrompt(receipt.WorkingDirectory, out var workingDirectory))
        {
            paths.Add(workingDirectory);
        }

        return paths.ToList();
    }

    internal static IReadOnlyList<string> ResolveWorkspacePathsFromToolRequest(string requestSummary)
    {
        if (string.IsNullOrWhiteSpace(requestSummary))
        {
            return [];
        }

        var paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in WorkspacePathInToolRequestRegex.Matches(requestSummary))
        {
            var candidatePath = match.Groups["path"].Value;
            if (TryMapWorkspacePathForPrompt(candidatePath, out var promptPath))
            {
                paths.Add(promptPath);
            }
        }

        foreach (Match match in ManagedWorkspacePathRegex.Matches(requestSummary))
        {
            var candidatePath = WorkspaceScopeDescriptor.NormalizeRelativePath(match.Groups["path"].Value);
            if (IsManagedProcessRunProductOutputPath(candidatePath))
            {
                paths.Add(candidatePath);
            }
        }

        return paths.ToList();
    }

    internal static bool TryMapWorkspacePathForPrompt(string path, out string promptPath)
    {
        promptPath = string.Empty;
        var normalized = path.Trim().TrimEnd(',', ';', '.', ')', ']', '}').Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (normalized.StartsWith($"{ExternalTargetAliasRoot}/", StringComparison.OrdinalIgnoreCase))
        {
            promptPath = normalized;
            return true;
        }

        if (normalized.Length < 3 || !char.IsLetter(normalized[0]) || normalized[1] != ':' || normalized[2] != '/')
        {
            return false;
        }

        var driveLetter = char.ToUpperInvariant(normalized[0]);
        var remainder = normalized.Length == 3
            ? string.Empty
            : normalized[3..].Trim('/');
        promptPath = string.IsNullOrWhiteSpace(remainder)
            ? $"{ExternalTargetAliasRoot}/{driveLetter}"
            : $"{ExternalTargetAliasRoot}/{driveLetter}/{remainder}";
        return true;
    }

    internal static bool IsConcreteProductDeliverableOrSourcePath(string promptPath)
    {
        if (!IsConcreteProductPath(promptPath))
        {
            return false;
        }

        return IsImplementationDeliverableOrSourceExtension(Path.GetExtension(promptPath));
    }

    internal static bool IsConcreteProductSourceOrProjectPath(string promptPath)
    {
        return IsConcreteProductPath(promptPath) &&
               IsCodeOrProjectExtension(Path.GetExtension(promptPath));
    }

    internal static bool IsImplementationDeliverableOrSourceExtension(string extension)
    {
        return IsCodeOrProjectExtension(extension) ||
               extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".docx", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".xls", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".tsv", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".pptx", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".ppt", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".yml", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsCodeOrProjectExtension(string extension)
    {
        return extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".razor", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".cshtml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".html", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".css", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".js", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".mjs", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".cjs", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jsx", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".ts", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".tsx", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".props", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".targets", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".json", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsConcreteProductPath(string promptPath)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(promptPath);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (IsExternalTargetAliasPath(normalized))
        {
            return segments.Length >= 2 &&
                   !IsExternalTargetAliasWithinManagedWorkspace(segments) &&
                   !segments.Any(IsExternalTargetNonProductPathSegment);
        }

        if (segments.Length == 0)
        {
            return false;
        }

        if (ProcessArtifactPathValidationRules.IsManagedRootSegment(segments[0]))
        {
            return IsManagedProcessRunProductOutputPath(segments);
        }

        return !segments.Any(IsNonProductPathSegment);
    }

    internal static bool IsManagedProcessRunProductOutputPath(string path)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return IsManagedProcessRunProductOutputPath(segments);
    }

    internal static bool IsManagedProcessRunProductOutputPath(IReadOnlyList<string> segments)
    {
        if (segments.Count == 0 ||
            !string.Equals(segments[0], "output", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var processRunsIndex = -1;
        for (var index = 1; index < segments.Count; index++)
        {
            if (string.Equals(segments[index], "process-runs", StringComparison.OrdinalIgnoreCase))
            {
                processRunsIndex = index;
                break;
            }
        }

        if (processRunsIndex < 0 || segments.Count <= processRunsIndex + 2)
        {
            return false;
        }

        return segments
            .Skip(processRunsIndex + 2)
            .All(segment => !IsManagedProcessRunNonProductPathSegment(segment));
    }

    internal static bool IsManagedProcessRunNonProductPathSegment(string segment)
    {
        return string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(segment, ".playwright-mcp", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool RequiresSourceOrProjectImplementationProof(bool containsRunnableApplicationContractSignal)
    {
        return containsRunnableApplicationContractSignal;
    }

    internal static bool IsExternalTargetAliasWithinManagedWorkspace(IReadOnlyList<string> segments)
    {
        var hasCanDoItAllControlPlanePrefix = false;
        for (var index = 0; index < segments.Count; index++)
        {
            if (string.Equals(segments[index], "CanDoItAll", StringComparison.OrdinalIgnoreCase))
            {
                hasCanDoItAllControlPlanePrefix = segments
                    .Skip(index + 1)
                    .Take(3)
                    .Any(segment => string.Equals(segment, "control-plane", StringComparison.OrdinalIgnoreCase));
            }

            if (!hasCanDoItAllControlPlanePrefix ||
                !string.Equals(segments[index], "workspace", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    internal static bool IsExternalTargetNonProductPathSegment(string segment)
    {
        return string.Equals(segment, ".playwright-mcp", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsNonProductPathSegment(string segment)
    {
        return ProcessArtifactPathValidationRules.IsManagedRootSegment(segment) ||
               string.Equals(segment, ".playwright-mcp", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsExternalTargetAliasPath(string normalizedRelativePath)
    {
        return string.Equals(normalizedRelativePath, ExternalTargetAliasRoot, StringComparison.OrdinalIgnoreCase) ||
               normalizedRelativePath.StartsWith(ExternalTargetAliasRoot + "/", StringComparison.OrdinalIgnoreCase);
    }
}
