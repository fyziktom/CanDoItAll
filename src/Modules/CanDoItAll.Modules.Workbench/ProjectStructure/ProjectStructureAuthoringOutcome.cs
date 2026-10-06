using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

internal enum ProjectStructureAuthoringOperation { AddSubproject, ReconnectSubproject, ConvertNode, TransferDescendants, CreateProject, CreateNode, EditNode, CopyNodes, MoveNodes, ConnectNodes, DisconnectNodes, RecomposeNodes, SummaryStatus, ExportWorkbook, ExportGantt, ExportCanvasImage, CreateTranscript, TranscriptAnalysis, GenerateImage }
internal enum ProjectStructureAuthoringResultKind { Rejected, Committed, Unconfirmed, Compensated, PartialCommit }
internal enum ProjectStructureExternalEffectState { NotStarted, Dispatched, Completed }

internal sealed record ProjectStructureAuthoringOutcome(
    Guid OpeningId,
    Guid SubmissionId,
    ProjectWriteAdmission Project,
    ProjectStructureAuthoringOperation Operation,
    ProjectStructureAuthoringResultKind Kind,
    string Message) {
    public IReadOnlyList<ProjectWriteAdmission> HierarchyProjects { get; init; } = [];
    public ProjectStructureNode? Node { get; init; }
    public IReadOnlyList<ProjectStructureNode> Nodes { get; init; } = [];
    public IReadOnlyList<string> NodeIds { get; init; } = [];
    public IReadOnlyList<ProjectNodeMoveRequest> Positions { get; init; } = [];
    public ProjectStructureClipboardCopyResult? ClipboardCopy { get; init; }
    public ProjectStructureLink? Link { get; init; }
    public ProjectStructureSubtreeRecompositionResult? Recomposition { get; init; }
    public string? SourceNodeId { get; init; }
    public Guid? TargetProjectId { get; init; }
    public ProjectStructureCreatedSubprojectTransferResult? Transfer { get; init; }
    public ProjectCreationReceipt? Creation { get; init; }
    public ProjectEditorAcknowledgement? Editor { get; init; }
    public Exception? Failure { get; init; }
    public ProjectStructureExternalEffectState ExternalEffect { get; init; }
    public Guid? ProviderId { get; init; }
    public ProjectStructureContentMediaReceipt? StoredMedia { get; init; }
}
