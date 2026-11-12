using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.CrmHr.UI.Parties;

public enum PartyMergePhase { Closed, Reading, Ready, Applying, Unknown, Accepted }

public sealed record PartyMergePresentation(long Opening, PartyMergePhase Phase, PartyMergePreviewModel? Preview, string? Message) {
    public bool IsOpen => Phase != PartyMergePhase.Closed;
    public bool IsBusy => Phase is PartyMergePhase.Reading or PartyMergePhase.Applying;
    public bool IsUnknown => Phase == PartyMergePhase.Unknown;
    public bool IsAccepted => Phase == PartyMergePhase.Accepted;
    public bool CanConfirm => Phase == PartyMergePhase.Ready && Preview is not null;
}

public sealed record PartyMergeNoteStatus(long Revision, bool HasInput);
