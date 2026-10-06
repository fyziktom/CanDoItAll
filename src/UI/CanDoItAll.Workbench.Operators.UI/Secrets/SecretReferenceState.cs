using CanDoItAll.Modules.Security;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Workbench.Operators.UI.Secrets;

public enum SecretReferencePhase { NotSubmitted, Refused, VaultCommitted, ReferenceCommitted, Observed, Unknown }
public sealed record SecretReferenceReceipt(SecretReferencePhase Phase, Guid? SecretId, string? NodeId, string Message);
public sealed record SecretReferenceInput(Guid? SecretId, string Purpose, string Note);

public sealed class SecretReferenceDraft {
    public Guid? SelectedId { get; set; }
    public string Search { get; set; } = string.Empty;
    public string Purpose { get; set; } = "Project structure reference";
    public string Note { get; set; } = string.Empty;
    public SecretEditorModel Create { get; } = new() { Kind = SecretKind.Generic, Scope = "project-structure", RotationNote = "Project structure reference" };
    public SecretReferenceInput Capture() => new(SelectedId, Purpose, Note);
}

public sealed class SecretReferenceState(bool isEdit) {
    public Guid OpeningId { get; } = Guid.NewGuid();
    public bool IsEdit { get; } = isEdit;
    public SecretReferenceDraft Draft { get; } = new();
    public EditContext EditContext => context ??= new(Draft);
    public IReadOnlyList<SecretListItem> Items { get; set; } = [];
    public bool IsLoading { get; set; } = true;
    public bool IsBusy { get; set; }
    public bool IsUnavailable { get; set; }
    public bool IsRetired { get; private set; }
    public bool RequiresObservation { get; set; }
    public bool CanFinishReference { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? VaultWarning { get; set; }
    public SecretReferenceReceipt? Receipt { get; set; }
    public bool CanEdit => !IsLoading && !IsBusy && !IsUnavailable && !IsRetired && !RequiresObservation && Receipt?.SecretId is null;
    public void Retire() {
        IsRetired = true;
        Draft.Create.SecretValue = string.Empty;
    }
    private EditContext? context;
}
