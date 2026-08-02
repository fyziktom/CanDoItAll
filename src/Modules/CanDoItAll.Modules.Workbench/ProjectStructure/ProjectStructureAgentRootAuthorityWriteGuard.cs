using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureAgentRootAuthorityWriteGuard
{
    internal const string FailureCode = "ProjectBlockRootOutsideExecutionScope";

    public static void EnsureAllowed(
        string? metadataJson,
        string workspaceRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);
        if (!ProjectWorkbenchObjectModeling.HasMeaningfulMetadata(metadataJson))
        {
            return;
        }

        ProjectBlockMetadata? projectBlock;
        try
        {
            projectBlock = ProjectObjectMetadataSerializer.Parse(metadataJson).ProjectBlock;
            projectBlock ??= ReadLegacyProjectBlockMetadata(metadataJson!);
        }
        catch (Exception exception) when (exception is InvalidOperationException or JsonException)
        {
            throw CreateFailure("metadata");
        }

        if (projectBlock is null)
        {
            return;
        }

        var auditScope = WorkspaceExecutionAuditContext.Current;
        var executionAliases = auditScope is null
            ? []
            : auditScope.AllowedExternalTargetAliases
                .Concat(auditScope.ReadOnlyExternalTargetAliases)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        foreach (var (fieldName, root) in EnumerateRoots(projectBlock))
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            if (!IsManagedWorkspacePath(root, workspaceRoot) &&
                (auditScope is null ||
                 !AgentWorkspaceToolAccessMetadata.IsExternalTargetAliasAllowed(
                     root,
                     executionAliases)))
            {
                throw CreateFailure(fieldName);
            }
        }
    }

    public static void EnsureParentAllowed(
        IReadOnlyCollection<ProjectStructureNode> nodes,
        string parentNodeKey,
        string workspaceRoot)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentException.ThrowIfNullOrWhiteSpace(parentNodeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceRoot);

        var nodeGroups = nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.Id))
            .GroupBy(node => node.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Take(2).ToArray(), StringComparer.Ordinal);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var currentNodeKey = parentNodeKey.Trim();
        while (!string.IsNullOrWhiteSpace(currentNodeKey))
        {
            if (!visited.Add(currentNodeKey) ||
                !nodeGroups.TryGetValue(currentNodeKey, out var matches) ||
                matches.Length != 1)
            {
                throw CreateFailure("parentNodeKey");
            }

            var node = matches[0];
            if (node.ObjectType == ProjectObjectType.ProjectBlock)
            {
                EnsureAllowed(node.MetadataJson, workspaceRoot);
                return;
            }

            currentNodeKey = node.ParentId?.Trim() ?? string.Empty;
        }
    }

    private static bool IsManagedWorkspacePath(
        string candidate,
        string workspaceRoot)
    {
        if (!Path.IsPathRooted(candidate) &&
            !string.IsNullOrWhiteSpace(
                AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(candidate)))
        {
            return false;
        }

        try
        {
            var normalizedWorkspaceRoot = Path.GetFullPath(workspaceRoot)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var normalizedCandidate = Path.IsPathRooted(candidate)
                ? Path.GetFullPath(candidate)
                : Path.GetFullPath(candidate, normalizedWorkspaceRoot);
            return string.Equals(
                       normalizedCandidate,
                       normalizedWorkspaceRoot,
                       StringComparison.OrdinalIgnoreCase) ||
                   normalizedCandidate.StartsWith(
                       normalizedWorkspaceRoot + Path.DirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static ProjectStructureAgentException CreateFailure(string fieldName)
        => ProjectStructureAgentException.CreateAgentVisible(
            403,
            FailureCode,
            $"request.metadataJson projectBlock.{fieldName} is outside the managed workspace and this execution's external-target scope. Retry with a managed-workspace path or a root already authorized for this run, omit the root metadata, or ask an operator to make the authority-changing edit.",
            canRetryWithCorrectedInput: true);

    private static ProjectBlockMetadata? ReadLegacyProjectBlockMetadata(
        string metadataJson)
    {
        using var document = JsonDocument.Parse(metadataJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var outputRoot = ReadString(document.RootElement, "outputRoot");
        var productRoot = ReadString(document.RootElement, "productRoot");
        var targetRoot = FirstNonEmpty(
            ReadString(document.RootElement, "targetRoot"),
            ReadString(document.RootElement, "targetPath"));
        var repositoryRoot = ReadString(document.RootElement, "repositoryRoot");
        var workspaceRoot = ReadString(document.RootElement, "workspaceRoot");
        if (new[] { outputRoot, productRoot, targetRoot, repositoryRoot, workspaceRoot }
            .All(string.IsNullOrWhiteSpace))
        {
            return null;
        }

        return new ProjectBlockMetadata
        {
            OutputRoot = outputRoot ?? string.Empty,
            ProductRoot = productRoot ?? string.Empty,
            TargetRoot = targetRoot ?? string.Empty,
            RepositoryRoot = repositoryRoot ?? string.Empty,
            WorkspaceRoot = workspaceRoot ?? string.Empty
        };
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase) &&
                property.Value.ValueKind == JsonValueKind.String)
            {
                return property.Value.GetString()?.Trim();
            }
        }

        return null;
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static IEnumerable<(string FieldName, string? Root)> EnumerateRoots(
        ProjectBlockMetadata projectBlock)
    {
        yield return ("outputRoot", projectBlock.OutputRoot);
        yield return ("productRoot", projectBlock.ProductRoot);
        yield return ("targetRoot", projectBlock.TargetRoot);
        yield return ("repositoryRoot", projectBlock.RepositoryRoot);
        yield return ("workspaceRoot", projectBlock.WorkspaceRoot);
    }
}
