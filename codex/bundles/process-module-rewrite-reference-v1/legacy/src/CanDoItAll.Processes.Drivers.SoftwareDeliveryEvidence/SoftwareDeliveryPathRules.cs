using System.Text.RegularExpressions;

namespace CanDoItAll.Processes.Drivers.SoftwareDeliveryEvidence;

public static class SoftwareDeliveryPathRules
{
    private const string ExternalTargetAliasRoot = "external-target";

    private static readonly Regex WorkspacePathInToolRequestRegex = new(
        @"(?<path>[A-Za-z]:\\[^`""'\r\n\s]+|external-target[\\/][^\s`""']+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ManagedWorkspacePathRegex = new(
        @"(?<path>(?:artifacts|output|integration-map|data)/(?:scopes/[^\s`""']+|[^\s`""']+))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool IsConcreteProductMutationReceipt(
        bool requiresCurrentAttemptProductMutation,
        bool requiresSourceOrProjectImplementationProof,
        IReadOnlyList<string> allowedExternalTargetAliases,
        SoftwareDeliveryToolReceiptSnapshot receipt)
    {
        return IsConcreteProductMutationReceipt(
            requiresCurrentAttemptProductMutation,
            requiresSourceOrProjectImplementationProof,
            receipt) &&
            IsWithinCurrentRunExternalMutationBoundary(allowedExternalTargetAliases, receipt);
    }

    public static bool HasConcreteProductPath(SoftwareDeliveryToolReceiptSnapshot receipt)
    {
        return receipt.WorkspacePaths.Any(IsConcreteProductPath);
    }

    public static bool HasConcreteProductDeliverableOrSourcePath(SoftwareDeliveryToolReceiptSnapshot receipt)
    {
        return receipt.WorkspacePaths.Any(IsConcreteProductDeliverableOrSourcePath);
    }

    public static bool HasConcreteProductImplementationPath(
        bool requiresSourceOrProjectImplementationProof,
        SoftwareDeliveryToolReceiptSnapshot receipt)
    {
        return requiresSourceOrProjectImplementationProof
            ? HasConcreteProductSourceOrProjectPath(receipt)
            : HasConcreteProductDeliverableOrSourcePath(receipt);
    }

    public static bool HasConcreteProductSourceOrProjectPath(SoftwareDeliveryToolReceiptSnapshot receipt)
    {
        return receipt.WorkspacePaths.Any(IsConcreteProductSourceOrProjectPath);
    }

    public static IReadOnlyList<string> ResolveWorkspacePathsFromToolRequest(string requestSummary)
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
            var candidatePath = NormalizeRelativePath(match.Groups["path"].Value);
            if (IsManagedProcessRunProductOutputPath(candidatePath))
            {
                paths.Add(candidatePath);
            }
        }

        return paths.ToList();
    }

    public static bool TryMapWorkspacePathForPrompt(string path, out string promptPath)
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

    public static bool IsConcreteProductDeliverableOrSourcePath(string promptPath)
    {
        if (!IsConcreteProductPath(promptPath))
        {
            return false;
        }

        return IsImplementationDeliverableOrSourceExtension(Path.GetExtension(promptPath));
    }

    public static bool IsConcreteProductSourceOrProjectPath(string promptPath)
    {
        return IsConcreteProductPath(promptPath) &&
               IsCodeOrProjectExtension(Path.GetExtension(promptPath));
    }

    public static bool IsImplementationDeliverableOrSourceExtension(string extension)
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

    public static bool IsConcreteProductPath(string promptPath)
    {
        var normalized = NormalizeRelativePath(promptPath);
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

        if (IsManagedRootSegment(segments[0]))
        {
            return IsManagedProcessRunProductOutputPath(segments);
        }

        return !segments.Any(IsNonProductPathSegment);
    }

    public static bool IsManagedProcessRunProductOutputPath(string path)
    {
        var normalized = NormalizeRelativePath(path);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return IsManagedProcessRunProductOutputPath(segments);
    }

    public static bool IsManagedProcessRunProductOutputPath(IReadOnlyList<string> segments)
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

    public static bool IsManagedProcessRunNonProductPathSegment(string segment)
    {
        return string.Equals(segment, "bin", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(segment, "obj", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(segment, ".playwright-mcp", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsExternalTargetAliasWithinManagedWorkspace(IReadOnlyList<string> segments)
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

    public static bool IsExternalTargetNonProductPathSegment(string segment)
    {
        return string.Equals(segment, ".playwright-mcp", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsNonProductPathSegment(string segment)
    {
        return IsManagedRootSegment(segment) ||
               string.Equals(segment, ".playwright-mcp", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsExternalTargetAliasPath(string normalizedRelativePath)
    {
        return string.Equals(normalizedRelativePath, ExternalTargetAliasRoot, StringComparison.OrdinalIgnoreCase) ||
               normalizedRelativePath.StartsWith(ExternalTargetAliasRoot + "/", StringComparison.OrdinalIgnoreCase);
    }

    public static string NormalizeRelativePath(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Trim().Replace('\\', '/').Trim('/');
    }

    private static bool IsConcreteProductMutationReceipt(
        bool requiresCurrentAttemptProductMutation,
        bool requiresSourceOrProjectImplementationProof,
        SoftwareDeliveryToolReceiptSnapshot receipt)
    {
        var toolName = SoftwareDeliveryEvidencePolicy.NormalizeToolToken(receipt.ToolName);
        if (string.Equals(toolName, "workspace_write_file", StringComparison.Ordinal) ||
            string.Equals(toolName, "workspace_append_file", StringComparison.Ordinal))
        {
            return requiresCurrentAttemptProductMutation
                ? HasConcreteProductDeliverableOrSourcePath(receipt)
                : HasConcreteProductImplementationPath(requiresSourceOrProjectImplementationProof, receipt);
        }

        return HasConcreteProductPath(receipt);
    }

    private static bool IsWithinCurrentRunExternalMutationBoundary(
        IReadOnlyList<string> allowedExternalTargetAliases,
        SoftwareDeliveryToolReceiptSnapshot receipt)
    {
        var allowedAliases = allowedExternalTargetAliases
            .Select(NormalizeExternalTargetAlias)
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .ToArray();
        if (allowedAliases.Length == 0)
        {
            return true;
        }

        var externalTargetPaths = receipt.WorkspacePaths
            .Select(NormalizeExternalTargetAlias)
            .Where(IsExternalTargetAliasPath)
            .ToArray();
        if (externalTargetPaths.Length == 0)
        {
            return true;
        }

        return externalTargetPaths.Any(path => IsAliasCoveredByAny(path, allowedAliases));
    }

    public static bool IsCodeOrProjectExtension(string extension)
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

    private static bool IsManagedRootSegment(string segment)
    {
        return string.Equals(segment, "artifacts", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(segment, "output", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(segment, "integration-map", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(segment, "data", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeExternalTargetAlias(string? alias)
    {
        var normalized = NormalizeRelativePath(alias);
        return normalized.StartsWith($"{ExternalTargetAliasRoot}/", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, ExternalTargetAliasRoot, StringComparison.OrdinalIgnoreCase)
            ? normalized
            : string.Empty;
    }

    private static bool IsAliasCoveredByAny(string alias, IReadOnlyCollection<string> roots)
    {
        return roots.Any(root =>
            string.Equals(alias, root, StringComparison.OrdinalIgnoreCase) ||
            alias.StartsWith(root.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase));
    }
}
