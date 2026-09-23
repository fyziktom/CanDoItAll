using CanDoItAll.Processes.Application;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureProcessRunOutputFolderPolicy
{
    public static bool TryResolve(
        ProjectStructureNode node,
        out ProcessRunArtifactRootResolution outputFolder)
    {
        ArgumentNullException.ThrowIfNull(node);
        return TryResolve(
            node.Id,
            node.ParentId,
            node.ObjectType,
            node.ObjectSubtype,
            node.ArtifactKind,
            node.ArtifactId,
            node.MetadataJson,
            node.IsSystemManaged,
            out outputFolder);
    }

    public static bool TryResolve(
        ProjectObjectRecord node,
        out ProcessRunArtifactRootResolution outputFolder)
    {
        ArgumentNullException.ThrowIfNull(node);
        return TryResolve(
            node.NodeKey,
            node.ParentNodeKey,
            node.ObjectType,
            node.ObjectSubtype,
            node.Binding.ExternalArtifactKind,
            node.Binding.ExternalArtifactId,
            node.MetadataJson,
            node.IsSystemManaged,
            out outputFolder);
    }

    private static bool TryResolve(
        string nodeKey,
        string? parentNodeKey,
        ProjectObjectType objectType,
        string objectSubtype,
        string artifactKind,
        Guid? artifactId,
        string metadataJson,
        bool isSystemManaged,
        out ProcessRunArtifactRootResolution outputFolder)
    {
        outputFolder = ProcessRunArtifactRootResolution.Ignored("The node is not an authorized process-run output folder.");
        if (!isSystemManaged ||
            objectType != ProjectObjectType.File ||
            !string.Equals(objectSubtype, "folder", StringComparison.Ordinal) ||
            !string.Equals(
                artifactKind,
                ProjectStructureProcessNodeKeys.ProcessRunOutputFolderArtifactKind,
                StringComparison.Ordinal) ||
            !ProjectStructureProcessNodeKeys.TryParseProcessRunOutputNodeKey(nodeKey, out Guid runId) ||
            artifactId != runId ||
            !string.Equals(
                parentNodeKey,
                ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(runId),
                StringComparison.Ordinal))
        {
            return false;
        }

        ProjectObjectMetadataEnvelope metadata;
        try
        {
            metadata = ProjectObjectMetadataSerializer.Parse(metadataJson);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        if (metadata.File is not { FileSubtype: ProjectFileSubtype.Folder } file)
        {
            return false;
        }

        string candidate = (file.ExternalPath ?? string.Empty)
            .Trim()
            .Replace('\\', '/')
            .Trim('/');
        outputFolder = ProcessRunArtifactRootPolicy.Resolve(candidate, runId);
        return outputFolder.ShouldProject &&
               string.Equals(candidate, outputFolder.DirectoryPath, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(
                   nodeKey,
                   ProjectStructureProcessNodeKeys.BuildProcessRunOutputNodeKey(runId, outputFolder.DirectoryPath),
                   StringComparison.Ordinal);
    }
}
