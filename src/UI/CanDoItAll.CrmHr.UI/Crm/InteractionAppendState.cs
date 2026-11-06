using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.CrmHr.UI.Crm;

public sealed class InteractionAppendState {
    private CrmInteractionEditorModel empty = new();
    private InteractionSubmission? pending;
    private long cleanInputRevision;
    public CrmInteractionEditorModel Draft { get; private set; } = new();
    public Guid AccountPartyId { get; private set; }
    public long Opening { get; private set; }
    public long InputRevision { get; private set; }
    public bool IsSaving => pending is not null;
    public bool IsUnknown { get; private set; }
    public CrmInteractionAppendReceipt? Accepted { get; private set; }
    public InteractionSubmission? UnknownSubmission { get; private set; }
    public bool CanSubmit => AccountPartyId != Guid.Empty && !IsSaving && !IsUnknown && !string.IsNullOrWhiteSpace(Draft.Subject);
    public bool IsDirty => InputRevision != cleanInputRevision || !Same(Draft, empty);

    public void Open(Guid accountPartyId, CrmInteractionEditorModel initial) {
        Opening++;
        AccountPartyId = accountPartyId;
        Draft = initial.Snapshot();
        empty = initial.Snapshot();
        InputRevision = 0;
        cleanInputRevision = 0;
        pending = null;
        IsUnknown = false;
        Accepted = null;
        UnknownSubmission = null;
    }

    public void Input() => InputRevision++;

    public InteractionSubmission? Begin() {
        if (!CanSubmit) {
            return null;
        }
        Accepted = null;
        pending = new(AccountPartyId, Opening, InputRevision, Draft.Snapshot());
        return pending;
    }

    public bool IsCurrent(InteractionSubmission submitted) => ReferenceEquals(pending, submitted) && submitted.Opening == Opening && submitted.AccountPartyId == AccountPartyId;
    public bool HasLaterInput(InteractionSubmission submitted) => submitted.InputRevision != InputRevision || !Same(submitted.Model, Draft);

    public void Accept(InteractionSubmission submitted, CrmInteractionAppendReceipt receipt) {
        if (!IsCurrent(submitted)) {
            return;
        }
        if (receipt.AccountPartyId != submitted.AccountPartyId || receipt.InteractionId == Guid.Empty) {
            Unknown(submitted);
            throw new InvalidOperationException("The interaction receipt does not identify its submitted account and committed entry.");
        }
        Accepted = receipt with { Accepted = receipt.Accepted.Snapshot() };
        if (!HasLaterInput(submitted)) {
            Copy(Draft, empty);
            cleanInputRevision = InputRevision;
        }
    }

    public void Unknown(InteractionSubmission submitted) {
        if (IsCurrent(submitted) && Accepted is null) {
            IsUnknown = true;
            UnknownSubmission = submitted;
        }
    }

    public void Complete(InteractionSubmission submitted) {
        if (IsCurrent(submitted)) {
            pending = null;
        }
    }

    public void SetOpportunityContext(Guid? opportunityId) {
        if (!IsDirty && !IsSaving && !IsUnknown) {
            Draft.RelatedOpportunityId = opportunityId;
            empty.RelatedOpportunityId = opportunityId;
        }
    }

    private static bool Same(CrmInteractionEditorModel a, CrmInteractionEditorModel b) =>
        (a.InteractionType, a.Subject, a.OccurredOn, a.Summary, a.Notes, a.NextActionText, a.NextActionOwnerPartyId, a.NextActionDueOn, a.RelatedOpportunityId) ==
        (b.InteractionType, b.Subject, b.OccurredOn, b.Summary, b.Notes, b.NextActionText, b.NextActionOwnerPartyId, b.NextActionDueOn, b.RelatedOpportunityId) &&
        a.ParticipantPartyIds.ToHashSet().SetEquals(b.ParticipantPartyIds);

    private static void Copy(CrmInteractionEditorModel target, CrmInteractionEditorModel source) {
        target.InteractionType = source.InteractionType;
        target.Subject = source.Subject;
        target.OccurredOn = source.OccurredOn;
        target.Summary = source.Summary;
        target.Notes = source.Notes;
        target.NextActionText = source.NextActionText;
        target.NextActionOwnerPartyId = source.NextActionOwnerPartyId;
        target.NextActionDueOn = source.NextActionDueOn;
        target.RelatedOpportunityId = source.RelatedOpportunityId;
        target.ParticipantPartyIds = source.ParticipantPartyIds.ToList();
    }
}

public sealed record InteractionSubmission(Guid AccountPartyId, long Opening, long InputRevision, CrmInteractionEditorModel Model);
