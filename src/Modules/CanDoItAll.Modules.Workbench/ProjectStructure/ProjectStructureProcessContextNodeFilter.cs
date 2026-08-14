using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureProcessContextNodeFilter
{
    private static readonly string[] EvidenceMarkers =
    [
        "browser-proof",
        "browser proof",
        "runtime-proof",
        "runtime proof",
        "run-proof",
        "run proof",
        "test-proof",
        "test proof",
        "qa-proof",
        "qa proof",
        "process-run",
        "execution-report",
        "execution report",
        "validation-result",
        "validation result",
        "validation-report",
        "validation report",
        "test-output",
        "test output",
        "run-output",
        "run output",
        "handoff-packet",
        "handoff packet",
        "file-summary",
        "file summary",
        "generated summary",
        "runtime-log",
        "runtime log"
    ];

    private static readonly string[] SourceContextMarkers =
    [
        "acceptance",
        "brief",
        "design",
        "input",
        "mockup",
        "proposal",
        "requirement",
        "source",
        "spec",
        "target",
        "ui",
        "wireframe"
    ];

    public static bool ShouldIncludeInProcessContext(ProjectStructureNode node)
        => !IsGeneratedProcessEvidence(
            node.Id,
            node.ParentId,
            node.ObjectType,
            node.ObjectSubtype,
            node.Title,
            node.Subtitle,
            node.Notes,
            node.Route,
            node.ArtifactKind,
            node.MediaRelativePath,
            node.MediaOriginalFileName);

    public static bool ShouldIncludeInProcessContext(ProjectStructureNodeSummary node)
        => !IsGeneratedProcessEvidence(
            node.Id,
            node.ParentId,
            node.ObjectType,
            node.ObjectSubtype,
            node.Title,
            node.Subtitle,
            node.Notes,
            node.Route,
            node.ArtifactKind,
            node.MediaRelativePath,
            node.MediaOriginalFileName);

    private static bool IsGeneratedProcessEvidence(
        string nodeId,
        string? parentId,
        ProjectObjectType objectType,
        string objectSubtype,
        string title,
        string subtitle,
        string? notes,
        string route,
        string artifactKind,
        string? mediaRelativePath,
        string? mediaOriginalFileName)
    {
        if (IsGeneratedProcessRunNodeKey(nodeId) ||
            IsGeneratedProcessRunNodeKey(parentId))
        {
            return true;
        }

        if (objectType is ProjectObjectType.ProcessRun or ProjectObjectType.ValidationRun or ProjectObjectType.TestEvidence)
        {
            return true;
        }

        if (artifactKind.StartsWith("process-run", StringComparison.OrdinalIgnoreCase) ||
            ContainsProcessRunPath(route) ||
            ContainsProcessRunPath(mediaRelativePath) ||
            ContainsProcessRunPath(notes))
        {
            return true;
        }

        if (objectType is not (ProjectObjectType.File or ProjectObjectType.ImageAsset or ProjectObjectType.VideoAsset))
        {
            return false;
        }

        if (string.Equals(objectSubtype, "screenshot", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(objectSubtype, "log", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(objectSubtype, "run-output", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var searchableText = string.Join(
            ' ',
            title,
            subtitle,
            notes,
            route,
            artifactKind,
            objectSubtype,
            mediaRelativePath,
            mediaOriginalFileName);
        if (EvidenceMarkers.Any(marker => searchableText.Contains(marker, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return objectType == ProjectObjectType.File &&
               (string.IsNullOrWhiteSpace(mediaRelativePath) || IsManagedProjectMediaFile(mediaRelativePath)) &&
               !HasSourceContextSignal(searchableText);
    }

    private static bool ContainsProcessRunPath(string? value)
        => !string.IsNullOrWhiteSpace(value) &&
           value.Replace('\\', '/').Contains("/process-runs/", StringComparison.OrdinalIgnoreCase);

    private static bool IsGeneratedProcessRunNodeKey(string? nodeKey)
        => !string.IsNullOrWhiteSpace(nodeKey) &&
           (nodeKey.StartsWith(ProjectStructureProcessNodeKeys.ProcessRunPrefix, StringComparison.Ordinal) ||
            nodeKey.StartsWith(ProjectStructureProcessNodeKeys.ProcessRunOutputPrefix, StringComparison.Ordinal) ||
            nodeKey.StartsWith(ProjectStructureProcessNodeKeys.ProcessRunRuntimePrefix, StringComparison.Ordinal) ||
            nodeKey.StartsWith(ProjectStructureProcessNodeKeys.ProcessRunSummaryPrefix, StringComparison.Ordinal) ||
            nodeKey.StartsWith(ProjectStructureProcessNodeKeys.ProcessRunScreenshotPrefix, StringComparison.Ordinal));

    private static bool IsManagedProjectMediaFile(string? mediaRelativePath)
        => !string.IsNullOrWhiteSpace(mediaRelativePath) &&
           mediaRelativePath.Replace('\\', '/').Contains("managed-files/project-media/", StringComparison.OrdinalIgnoreCase);

    private static bool HasSourceContextSignal(string text)
        => SourceContextMarkers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));
}
