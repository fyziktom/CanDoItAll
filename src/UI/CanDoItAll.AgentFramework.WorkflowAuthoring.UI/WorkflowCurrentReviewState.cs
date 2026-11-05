using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.WorkflowAuthoring.UI;

internal sealed class WorkflowCurrentReviewState {
    private WorkflowCanvasDocument? occurrence;
    private long readGeneration;
    public WorkflowDefinitionReview? Review { get; private set; }
    public WorkflowDefinitionReview? Previous { get; private set; }
    public bool IsReading { get; private set; }
    public bool IsDeleted { get; private set; }
    public string Message { get; private set; } = string.Empty;

    public async Task ReadAsync(WorkflowCanvasDocument document,
        Func<WorkflowId, CancellationToken, Task<WorkflowDefinition?>> read,
        Func<bool> owns, CancellationToken cancellationToken) {
        if (document.Baseline is not { } original || document.DefinitionId is not { } id) {
            Message = "Save this new workflow before checking its current version.";
            return;
        }
        var generation = ++readGeneration;
        var revision = document.EditRevision;
        var version = document.VersionId;
        IsReading = true;
        Message = string.Empty;
        try {
            var current = await read(id, cancellationToken);
            if (!owns() || generation != readGeneration || cancellationToken.IsCancellationRequested) {
                return;
            }
            if (document.EditRevision != revision || document.VersionId != version) {
                Message = "Your draft changed while the current workflow was being read. Check again.";
                return;
            }
            if (current is null) {
                IsDeleted = true;
                Previous = Review ?? Previous;
                Review = null;
                Message = "The saved workflow was deleted. Your local draft is retained; it cannot replace the deleted workflow.";
                return;
            }
            var local = WorkflowCanvasDefinitionMapper.ToDefinition(document);
            var raw = document.Nodes.Any(node => node.RawInputs.Count > 0 || node.PendingConfiguration is not null || node.SettingsError.Length > 0);
            var review = new WorkflowDefinitionReview(original, local, current, revision, raw);
            Previous = Review ?? Previous;
            Review = review;
            occurrence = document;
            IsDeleted = false;
        } finally {
            if (generation == readGeneration) {
                IsReading = false;
            }
        }
    }

    public bool CanApply(WorkflowCanvasDocument document)
        => !IsReading && ReferenceEquals(occurrence, document) && Review is { IsResolved: true } review &&
            document.EditRevision == review.Revision && document.VersionId == review.Original.VersionId;

    public WorkflowDefinition Apply(WorkflowCanvasDocument document) {
        if (!CanApply(document)) {
            throw new InvalidOperationException("The workflow comparison no longer matches this draft.");
        }
        var review = Review!;
        var result = review.Result();
        if (!review.KeepsLocalGraph) {
            var replacement = WorkflowCanvasDefinitionMapper.FromDefinition(result, []);
            document.Nodes.Clear();
            foreach (var node in replacement.Nodes) {
                document.Nodes.Add(node);
            }
            document.Edges.Clear();
            foreach (var edge in replacement.Edges) {
                document.Edges.Add(edge);
            }
        }
        document.Baseline = review.Current;
        document.DefinitionId = review.Current.Id;
        document.VersionId = review.Current.VersionId;
        document.CreatedAtUtc = review.Current.CreatedAtUtc;
        document.UpdatedAtUtc = review.Current.UpdatedAtUtc;
        document.Name = result.Name;
        document.Description = result.Description;
        document.Status = result.Status;
        document.RuntimePolicy = result.RuntimePolicy;
        document.StartNodeId = result.Graph.StartNodeId;
        document.Changes.Touch(WorkflowDraftField.Nodes);
        var current = review.Current;
        Clear();
        return current;
    }

    public void Close() {
        readGeneration++;
        IsReading = false;
        Review = null;
        Previous = null;
        occurrence = null;
    }

    public void Clear() {
        IsDeleted = false;
        Message = string.Empty;
        Close();
    }
}
