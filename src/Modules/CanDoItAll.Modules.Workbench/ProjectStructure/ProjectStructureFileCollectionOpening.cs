using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.AppComponents.FileTools;

namespace CanDoItAll.Modules.Workbench;

public sealed record ProjectStructureFileCollectionOpening(
    ProjectStructureFileCollectionRequest Request,
    Func<CancellationToken, Task> EnsureCurrentAsync) {
    public Guid Id { get; } = Guid.NewGuid();
}

public enum ProjectStructureFileActionOutcomeKind { Accepted, Rejected, Unconfirmed }

public sealed record ProjectStructureFileActionOutcome(
    Guid OpeningId,
    ProjectStructureFileCollectionRequest Collection,
    FileBrowserItemKey Item,
    FileToolsHostAction Action,
    ProjectStructureFileActionOutcomeKind Kind,
    string Message);
