using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Processes.Application;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureProcessRunOutputFolderPolicy
{
    public static string ResolveProjectScopedDirectoryPath(
        Guid projectId,
        ProcessRunArtifactRootResolution outputFolder)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("A project identifier is required.", nameof(projectId));
        }

        ArgumentNullException.ThrowIfNull(outputFolder);
        if (!outputFolder.ShouldProject)
        {
            throw new ArgumentException("A projectable process-run output folder is required.", nameof(outputFolder));
        }

        var projectScope = WorkspaceScopeDescriptor.Project(projectId.ToString("D"));
        (string logicalRoot, string scopedRoot) = outputFolder.Kind switch
        {
            ProcessRunArtifactRootKind.ManagedArtifactRunRoot =>
                (WorkspaceScopeDescriptor.ArtifactManagedRootName, projectScope.ArtifactRootRelativePath),
            ProcessRunArtifactRootKind.ManagedRunRoot or ProcessRunArtifactRootKind.ManagedProductOutputRoot =>
                (WorkspaceScopeDescriptor.OutputManagedRootName, projectScope.OutputRootRelativePath),
            _ => throw new ArgumentException("The process-run output folder kind is not projectable.", nameof(outputFolder))
        };
        string normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(outputFolder.DirectoryPath);
        if (!normalizedPath.StartsWith(logicalRoot + "/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The process-run output folder does not match its managed root.", nameof(outputFolder));
        }

        string suffix = normalizedPath[(logicalRoot.Length + 1)..];
        return WorkspaceScopeDescriptor.NormalizeRelativePath($"{scopedRoot}/{suffix}");
    }

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
