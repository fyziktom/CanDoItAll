using System.Runtime.CompilerServices;
using CanDoItAll.Editing;

namespace CanDoItAll.Modules.Projects.Pages.Components;

public sealed class ProjectDefinitionReconciliation(ProjectDefinitionSnapshot? original) {
    private const int MaximumHints = 16;
    private readonly ConditionalWeakTable<ProjectPhaseEditorModel, LocalKey> phaseKeys = new();
    private readonly ConditionalWeakTable<ProjectOptionEditorModel, LocalKey> optionKeys = new();
    private readonly List<EditingChange> pending = [];
    private readonly Queue<Guid> acceptedOperations = [];
    private ProjectEditorSubmission? reviewLocal;
    private ProjectEditorSubmission? beforeMerge;
    private ProjectEditorSubmission? afterMerge;
    private ProjectDefinitionSnapshot? current;
    private long currentHint;
    private long reviewHint;
    private bool resync;
    private EditingCursor? observed;
    public Guid OpeningId { get; } = Guid.NewGuid();
    public ProjectDefinitionSnapshot? Original { get; private set; } = original;
    public ProjectDefinitionReview? Review { get; private set; }
    public ProjectDefinitionReview? PreviousReview { get; private set; }
    public bool Missing { get; private set; }
    public bool Reading { get; set; }
    public long HintRevision { get; private set; }
    public bool IsOutdated => Missing || resync || pending.Count > 0;
    public bool ReviewIsStale => Review is not null && reviewHint != HintRevision;
    public bool CurrentIsStale => current is null || currentHint != HintRevision;
    public bool CanUndo => beforeMerge is not null && Review is null;

    public void Observe(EditingChangeBatch batch) {
        if (Original is null) {
            return;
        }
        if (batch.RequiresResync) {
            resync = true;
            HintRevision++;
        }
        var target = Original?.Expectation.Project;
        foreach (var change in batch.Changes.Where(change => target is not null && change.Resource.Owner == EditingOwner.Project &&
                     change.Resource.Scope.Kind == EditingScopeKind.Dataset && change.Resource.Scope.Id == target.DatabaseProfileId &&
                     change.Resource.ResourceId == target.ProjectId)) {
            if (observed is { } previous && previous.Epoch == change.Cursor.Epoch && previous.Sequence >= change.Cursor.Sequence) {
                continue;
            }
            if (observed is { } prior && prior.Epoch != change.Cursor.Epoch) {
                resync = true;
                HintRevision++;
            }
            observed = change.Cursor;
            if (change.Kind == EditingChangeKind.MetadataChanged || change.OriginEditorId == OpeningId && acceptedOperations.Contains(change.OperationId)) {
                continue;
            }
            if (pending.Count == MaximumHints) {
                pending.Clear();
                resync = true;
            }
            pending.Add(change);
            HintRevision++;
        }
    }

    public void AcceptedOperation(Guid operationId) {
        if (!acceptedOperations.Contains(operationId)) {
            acceptedOperations.Enqueue(operationId);
            if (acceptedOperations.Count > MaximumHints) {
                acceptedOperations.Dequeue();
            }
        }
        pending.RemoveAll(change => change.OriginEditorId == OpeningId && change.OperationId == operationId);
    }

    public void ReadCurrent(ProjectDefinitionSnapshot snapshot, long readHint) {
        if (Original is not null && snapshot.Expectation.Project != Original.Expectation.Project) {
            Missing = true;
            throw new InvalidOperationException("The original Project lifetime is no longer current. Preserve this draft and reopen the current Project explicitly.");
        }
        current = snapshot;
        currentHint = readHint;
    }

    public void Unavailable() => Missing = true;

    public void ReadFailed() {
        current = null;
        resync = true;
        HintRevision++;
    }

    public void Begin(ProjectEditorSubmission local) {
        if (Original is null || current is null || Missing) {
            throw new InvalidOperationException("An actual original and a current observation of the same Project lifetime are required.");
        }
        if (Review?.HasPendingInput == true) {
            throw new InvalidOperationException("Complete or clear the unfinished comparison result before rebuilding it. Your existing result input is retained.");
        }
        var phases = local.Model.Phases.Select((row, index) => new ProjectPhaseValues(PhaseKey(local.PhaseRows[index]),
            row.Name, row.Goal, row.Status, new(row.StartDateUtc, row.EndDateUtc))).ToArray();
        var options = local.Model.Options.Select((row, index) => (Value: row, Key: OptionKey(local.OptionRows[index])))
            .Where(row => !string.IsNullOrWhiteSpace(row.Value.OptionName) || !string.IsNullOrWhiteSpace(row.Value.Notes))
            .Select(row => new ProjectOptionValues(row.Key, row.Value.Category, row.Value.OptionName, row.Value.Notes)).ToArray();
        var previous = Review ?? PreviousReview;
        var next = new ProjectDefinitionReview(Original, ProjectEditorValues.Scalars(local.Model), ProjectEditorValues.Identity(local.Model), phases, options, current);
        if (previous is not null) {
            next.PreserveChoices(previous);
            PreviousReview = previous;
        }
        Review = next;
        reviewLocal = local with { Model = ProjectEditorDraft.Clone(local.Model), PhaseRows = local.PhaseRows.ToArray(), OptionRows = local.OptionRows.ToArray() };
        reviewHint = currentHint;
    }

    public bool CanApply(ProjectEditorModel live, long inputRevision) => !Missing && !ReviewIsStale && Review?.CanApply == true &&
        reviewLocal is not null && reviewLocal.EditRevision == inputRevision && ProjectEditorValues.Same(live, reviewLocal);

    public void Apply(ProjectEditorModel live, long inputRevision) {
        if (!CanApply(live, inputRevision)) {
            throw new InvalidOperationException("This Project comparison is unresolved or stale. Read current again; the live draft is unchanged.");
        }
        var review = Review!;
        var candidate = review.Candidate();
        beforeMerge = reviewLocal;
        var phases = live.Phases.ToDictionary(PhaseKey);
        var options = live.Options.ToDictionary(OptionKey);
        ProjectEditorValues.CopyScalars(candidate, live);
        live.Phases.Clear();
        foreach (var value in review.PhaseCandidates()) {
            var target = phases.GetValueOrDefault(value.Key) ?? value.Model();
            ProjectEditorValues.Copy(value.Model(), target);
            if (target.Id is null && !phaseKeys.TryGetValue(target, out _)) {
                phaseKeys.Add(target, new(value.Key));
            }
            live.Phases.Add(target);
        }
        live.Options.Clear();
        foreach (var value in review.Options.Select(row => row.Candidate()).OfType<ProjectOptionValues>().OrderBy(row => row.Category).ThenBy(row => row.Key.Value)) {
            var target = options.GetValueOrDefault(value.Key) ?? value.Model();
            ProjectEditorValues.Copy(value.Model(), target);
            if (target.Id is null && !optionKeys.TryGetValue(target, out _)) {
                optionKeys.Add(target, new(value.Key));
            }
            live.Options.Add(target);
        }
        foreach (var category in Enum.GetValues<ProjectOptionCategory>().Where(category => category != ProjectOptionCategory.Other)) {
            if (live.Options.Count < ProjectDefinitionReview.MaximumRows && live.Options.All(row => row.Category != category)) {
                live.Options.Add(new() { Category = category });
            }
        }
        Original = review.Current;
        pending.Clear();
        resync = false;
        Cancel();
    }

    public void Applied(ProjectEditorSubmission state) => afterMerge = state;

    public bool CanUndoFor(ProjectEditorModel live, long inputRevision) => CanUndo && afterMerge is not null &&
        inputRevision == afterMerge.EditRevision && ProjectEditorValues.Same(live, afterMerge);

    public void Undo(ProjectEditorModel live, long inputRevision) {
        if (!CanUndoFor(live, inputRevision)) {
            throw new InvalidOperationException("No unapplied Project merge can be undone.");
        }
        var previous = beforeMerge!;
        var basis = Original ?? throw new InvalidOperationException("Undo requires the accepted current comparison basis.");
        ProjectEditorValues.CopyScalars(previous.Model, live);
        live.Id = basis.Expectation.Project.ProjectId;
        live.ExpectedDefinition = basis.Expectation;
        live.ExpectedProjectAdmission = basis.Expectation.Project;
        live.ExpectedLifetimeId = basis.Expectation.Project.LifetimeId;
        if (basis.ExternalIdentity is { } identity) {
            live.ExternalNamespace = identity.Namespace;
            live.ExternalKey = identity.Key;
        }
        var phaseIds = basis.Phases.Select(row => row.Id).ToHashSet();
        var optionIds = basis.Options.Select(row => row.Id).ToHashSet();
        live.Phases.Clear();
        for (var index = 0; index < previous.PhaseRows.Count; index++) {
            var row = previous.PhaseRows[index];
            ProjectEditorValues.Copy(previous.Model.Phases[index], row);
            if (row.Id is { } id && !phaseIds.Contains(id)) {
                row.Id = null;
            }
            live.Phases.Add(row);
        }
        live.Options.Clear();
        for (var index = 0; index < previous.OptionRows.Count; index++) {
            var row = previous.OptionRows[index];
            ProjectEditorValues.Copy(previous.Model.Options[index], row);
            if (row.Id is { } id && !optionIds.Contains(id)) {
                row.Id = null;
            }
            live.Options.Add(row);
        }
        beforeMerge = null;
        afterMerge = null;
    }

    public void Cancel() {
        PreviousReview = Review ?? PreviousReview;
        Review = null;
        reviewLocal = null;
    }

    public void Accept(ProjectEditorAcknowledgement accepted) {
        Cancel();
        PreviousReview = null;
        beforeMerge = null;
        afterMerge = null;
        Original = new(accepted.Definition,
            new(accepted.Name, accepted.Description, accepted.Objective, accepted.Status, accepted.CurrentPhase, accepted.TargetDateUtc),
            accepted.ExternalIdentity,
            accepted.Phases.OrderBy(row => row.SubmittedIndex).Select(row => new ProjectDefinitionPhase(
                row.Id, row.Name, row.Goal, row.Status, row.StartDateUtc, row.EndDateUtc)).ToArray(),
            accepted.Options.OrderBy(row => row.Category).ThenBy(row => row.Id).Select(row => new ProjectDefinitionOption(
                row.Id, row.Category, row.OptionName, row.Notes)).ToArray());
    }

    private ProjectDefinitionRowKey PhaseKey(ProjectPhaseEditorModel row)
        => row.Id is { } id ? ProjectDefinitionRowKey.Stored(id) : phaseKeys.GetValue(row, static _ => new(ProjectDefinitionRowKey.Local())).Key;

    private ProjectDefinitionRowKey OptionKey(ProjectOptionEditorModel row)
        => row.Id is { } id ? ProjectDefinitionRowKey.Stored(id) : optionKeys.GetValue(row, static _ => new(ProjectDefinitionRowKey.Local())).Key;

    private sealed record LocalKey(ProjectDefinitionRowKey Key);
}
