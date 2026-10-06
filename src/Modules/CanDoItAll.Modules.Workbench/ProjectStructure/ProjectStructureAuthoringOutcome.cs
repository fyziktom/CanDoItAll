using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

internal enum ProjectStructureAuthoringOperation { AddSubproject, ReconnectSubproject, ConvertNode, TransferDescendants, CreateProject, CreateNode, EditNode, CopyNodes, MoveNodes, ConnectNodes, SaveLayout }
internal enum ProjectStructureAuthoringResultKind { Rejected, Committed, Unconfirmed, Compensated, PartialCommit }

internal sealed record ProjectStructureAuthoringOutcome(
    Guid OpeningId,
    Guid SubmissionId,
    ProjectWriteAdmission Project,
    ProjectStructureAuthoringOperation Operation,
    ProjectStructureAuthoringResultKind Kind,
    string Message) {
    public IReadOnlyList<ProjectWriteAdmission> HierarchyProjects { get; init; } = [];
    public ProjectStructureNode? Node { get; init; }
    public string? SourceNodeId { get; init; }
    public Guid? TargetProjectId { get; init; }
    public ProjectStructureCreatedSubprojectTransferResult? Transfer { get; init; }
    public ProjectCreationReceipt? Creation { get; init; }
    public ProjectEditorAcknowledgement? Editor { get; init; }
    public Exception? Failure { get; init; }
}
