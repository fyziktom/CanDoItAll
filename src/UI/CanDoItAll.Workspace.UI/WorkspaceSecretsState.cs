using CanDoItAll.Modules.Security;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Workspace.UI;

public sealed class WorkspaceSecretDraft(SecretEditorModel model) {
    public Guid Origin { get; } = Guid.NewGuid();
    public SecretEditorModel Model { get; } = model;
    public EditContext EditContext { get; } = new(model);
    public long Revision { get; private set; }
    public long ReplacementRevision { get; private set; }
    public SecretEditorMetadata? Baseline { get; private set; } = model.ExpectedUpdatedAtUtc.HasValue ? SecretEditorMetadata.Capture(model) : null;

    public void Edited() => Revision++;

    public void SetReplacement(string value) {
        if (Model.SecretValue == value) {
            return;
        }
        Model.SecretValue = value;
        ReplacementRevision++;
        Edited();
    }

    public void Accept(Guid id, SecretEditorModel submitted, long replacementRevision) {
        Model.Id = id;
        Model.ExpectedUpdatedAtUtc = submitted.ExpectedUpdatedAtUtc;
        Baseline = SecretEditorMetadata.Capture(submitted);
        if (ReplacementRevision == replacementRevision && Model.SecretValue == submitted.SecretValue) {
            SetReplacement(string.Empty);
        }
    }

    public void Apply(WorkspaceSecretReview review) {
        Model.Name = review.Name.Result;
        (Model.Kind, Model.Scope) = review.Use.Result;
        (Model.RotationNote, Model.MetadataJson) = review.PrivateMetadata.Result;
        if (review.Replacement is { Result: false }) {
            SetReplacement(string.Empty);
        }
        Model.ExpectedUpdatedAtUtc = review.Current.UpdatedAtUtc;
        Baseline = review.Saved;
        Edited();
    }

    public void Retire() {
        Baseline = null;
        Model.SecretValue = string.Empty;
        Model.MetadataJson = string.Empty;
    }
}

public interface IWorkspaceSecretsState {
    event Action? Changed;
    WorkspaceSecretDraft Draft { get; }
    IReadOnlyList<SecretListItem> Secrets { get; }
    IReadOnlyList<SettingsReceipt> Receipts { get; }
    string Search { get; set; }
    bool CanMutate { get; }
    bool CanEdit { get; }
    bool CanCompare { get; }
    bool RequiresComparison { get; }
    WorkspaceSecretReview? Comparison { get; }
    bool IsLoading { get; }
    string? Error { get; }
    Task RefreshAsync();
    Task SelectAsync(Guid id);
    Task SaveAsync();
    Task DeleteAsync();
    Task CompareAsync();
    void ApplyComparison();
    void CloseComparison();
    Task ReviewAsync(SettingsReceipt receipt);
    Task ReviewIdentityAsync(SettingsReceipt receipt, Guid id);
    void New();
}
