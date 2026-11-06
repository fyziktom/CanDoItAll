using CanDoItAll.Editing;
using CanDoItAll.Modules.Security;

namespace CanDoItAll.Workspace.UI;

public sealed class WorkspaceSecretReview {
    private readonly Guid origin;
    private readonly long revision;

    public WorkspaceSecretReview(WorkspaceSecretDraft draft, SecretEditorModel saved) {
        if (draft.Model.Id != saved.Id || saved.Id is not { } id || saved.ExpectedUpdatedAtUtc is null || draft.Baseline is not { } baseline) {
            throw new InvalidOperationException("The saved metadata must belong to the original secret.");
        }
        origin = draft.Origin;
        revision = draft.Revision;
        Current = new(id, saved.ExpectedUpdatedAtUtc);
        Saved = SecretEditorMetadata.Capture(saved);
        var local = SecretEditorMetadata.Capture(draft.Model);
        Name = new(baseline.Name, local.Name, Saved.Name);
        Use = new((baseline.Kind, baseline.Scope), (local.Kind, local.Scope), (Saved.Kind, Saved.Scope));
        PrivateMetadata = new((baseline.RotationNote, baseline.Json), (local.RotationNote, local.Json), (Saved.RotationNote, Saved.Json));
        if (draft.Model.SecretValue.Length > 0) {
            Replacement = new(false, true, false);
            Replacement.RequireChoice();
        }
    }

    public SecretMetadataCondition Current { get; }
    public SecretEditorMetadata Saved { get; }
    public MergeField<string> Name { get; }
    public MergeField<(SecretKind Kind, string Scope)> Use { get; }
    public MergeField<(string? Note, string Json)> PrivateMetadata { get; }
    public MergeField<bool>? Replacement { get; }

    public bool CanApply(WorkspaceSecretDraft draft) => draft.Origin == origin && draft.Revision == revision && draft.Model.Id == Current.SecretId &&
        Name.IsResolved && Use.IsResolved && PrivateMetadata.IsResolved && Replacement?.IsResolved != false;
}
