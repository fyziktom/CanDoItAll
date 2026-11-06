namespace CanDoItAll.CrmHr.UI.Parties;

public enum PartyImportPhase { Editing, Previewing, Applying, Unknown }

public sealed record PartyImportStatus(long InputRevision, bool HasInput, PartyImportPhase Phase) {
    public bool IsBusy => Phase is PartyImportPhase.Previewing or PartyImportPhase.Applying;
    public bool IsUnknown => Phase == PartyImportPhase.Unknown;
}
