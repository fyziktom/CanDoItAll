using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Workbench.Operators.UI.Parties;

public enum PartyEditorKind { Participant, Meeting }
public enum PartyQuickCreateKind { Person, Organization, OrganizationUnit, AiAgent }
public enum PartySavePhase { NotSubmitted, Rejected, AssignmentsCommitted, NodeCommitted, Observed, Unknown }

public sealed record PartyChoice(Guid Id, string Name, string Kind, string Icon, bool UsesAvatar,
    string PublicContact = "", string PublicPhone = "", bool IsSensitive = false, bool IsMissing = false);
public sealed record PartyQuickCreateInput(PartyQuickCreateKind Kind, string Name, string Email, string Phone, string Summary);
public sealed record PartySelection(bool KeepLocal, Guid? Participant, IReadOnlyList<Guid> MeetingParties);
public sealed record PartyCreated(Guid Id, string Name, string? Warning);
public sealed record PartySaveReceipt(PartySavePhase Phase, IReadOnlyList<Guid> AssignmentIds, string? NodeId, string Message);

public sealed class PartyEditorDraft {
    public bool KeepLocal { get; set; }
    public Guid? Participant { get; set; }
    public HashSet<Guid> MeetingParties { get; } = [];
    public PartyQuickCreateKind Kind { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public PartyQuickCreateInput QuickCreate() => new(Kind, Name, Email, Phone, Summary);
    public PartySelection Selection() => new(KeepLocal, Participant, MeetingParties.ToArray());
}

public sealed class PartyEditorState(PartyEditorKind kind, string title) {
    public Guid OpeningId { get; } = Guid.NewGuid();
    public PartyEditorKind Kind { get; } = kind;
    public string Title { get; set; } = title;
    public PartyEditorDraft Draft { get; } = new();
    public EditContext EditContext => context ??= new(Draft);
    public IReadOnlyList<PartyChoice> Choices { get; set; } = [];
    public IReadOnlyList<Guid> ProjectDefaults { get; set; } = [];
    public bool IsLoading { get; set; } = true;
    public bool IsBusy { get; set; }
    public bool IsUnavailable { get; set; }
    public bool RequiresObservation { get; set; }
    public bool IsRetired { get; set; }
    public string Message { get; set; } = string.Empty;
    public PartyCreated? Created { get; set; }
    public PartySaveReceipt? Receipt { get; set; }
    public bool CanEdit => !IsLoading && !IsBusy && !IsUnavailable && !IsRetired && !RequiresObservation;
    private EditContext? context;
}
