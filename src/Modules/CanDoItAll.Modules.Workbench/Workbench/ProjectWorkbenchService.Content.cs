using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

public sealed partial class ProjectWorkbenchService {
    public Task<ProjectStructureNode?> UpdateContentMetadataAsync(ProjectWriteAdmission admission,
        ProjectStructureNode expectedNode, string metadataJson, string? status = null,
        ProjectNodeReferenceCollection? nodeReferences = null, CancellationToken cancellationToken = default)
        => UpdateObjectMetadataCoreAsync(admission.ProjectId, expectedNode.Id, metadataJson, null,
            null, status, nodeReferences, cancellationToken, admission, expectedContent: expectedNode);

    public Task<ProjectStructureNode?> ReplaceContentMediaAsync(ProjectWriteAdmission admission,
        ProjectStructureNode expectedNode, ProjectObjectMediaPayload media, string metadataJson,
        string status, CancellationToken cancellationToken = default)
        => ReplaceObjectMediaCoreAsync(admission.ProjectId, expectedNode.Id, media, metadataJson,
            null, status, cancellationToken, admission, expectedNode);

    public Task<IReadOnlyList<ProjectStructureNode>> UpdateContentStatusesAsync(ProjectWriteAdmission admission,
        IReadOnlyCollection<ProjectStructureNode> expectedNodes, string status, CancellationToken cancellationToken = default)
        => UpdateObjectStatusesCoreAsync(admission.ProjectId, expectedNodes.Select(node => node.Id).ToArray(),
            status, cancellationToken, admission, expectedNodes: expectedNodes);

    public async Task RequireContentCurrentAsync(ProjectWriteAdmission admission, ProjectStructureNode expectedNode,
        CancellationToken cancellationToken = default) {
        var current = await GetStructureAsync(admission.ProjectId, cancellationToken);
        if (current.ExpectedProjectAdmission != admission) {
            throw new ProjectWriteAdmissionRejectedException(admission);
        }
        var node = current.Nodes.FirstOrDefault(node => node.Id == expectedNode.Id);
        if (node is null || node.RecordId != expectedNode.RecordId || node.ObjectType != expectedNode.ObjectType ||
            node.ObjectSubtype != expectedNode.ObjectSubtype || node.ParentId != expectedNode.ParentId ||
            node.MetadataJson != expectedNode.MetadataJson || node.Notes != expectedNode.Notes ||
            node.StorageObjectReferenceJson != expectedNode.StorageObjectReferenceJson ||
            !(node.NodeReferences?.Entries ?? []).SequenceEqual(expectedNode.NodeReferences?.Entries ?? [])) {
            throw new ProjectStructureEditConflictException();
        }
    }
}
