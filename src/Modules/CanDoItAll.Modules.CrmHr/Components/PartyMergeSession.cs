using CanDoItAll.CrmHr.UI.Parties;
using CanDoItAll.Editing;
using CanDoItAll.Infrastructure.Editing;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CrmHr.Components;

public sealed class PartyMergeSession(PartyDirectoryManagementService commands, CrmHrChanges changes,
    IEditingUiAccess access, ILogger<PartyMergeSession> logger) {
    private Guid retainedId;
    private long submittedNoteRevision;
    private CancellationTokenSource? currentRead;
    public PartyMergePresentation Presentation { get; private set; } = new(0, PartyMergePhase.Closed, null, null);
    public PartyDuplicateCandidateModel? Candidate { get; private set; }
    public PartyMergeNoteStatus Note { get; set; } = new(0, false);
    public bool HasLocalChanges => Note.HasInput && (!Presentation.IsAccepted || Note.Revision != submittedNoteRevision);
    public Guid? AcceptedTarget => Presentation.IsAccepted ? Presentation.Preview?.Retained.Id : null;

    public Task OpenAsync(Guid retainedPartyId, PartyDuplicateCandidateModel candidate) {
        Reset();
        retainedId = retainedPartyId;
        Candidate = candidate;
        Presentation = Presentation with { Phase = PartyMergePhase.Ready };
        return RefreshAsync();
    }

    public void Reset() {
        Presentation = new(Presentation.Opening + 1, PartyMergePhase.Closed, null, null);
        currentRead?.Cancel();
        currentRead = null;
        Candidate = null;
        Note = new(0, false);
        submittedNoteRevision = 0;
    }

    public async Task RefreshAsync() {
        if (Presentation.IsBusy || !Presentation.IsOpen || Presentation.IsAccepted || Candidate is null) {
            return;
        }
        var opening = Presentation.Opening;
        var retainedPartyId = retainedId;
        var duplicateId = Candidate.Id;
        var wasUnknown = Presentation.IsUnknown;
        Presentation = Presentation with { Phase = PartyMergePhase.Reading, Message = null };
        using var reading = new CancellationTokenSource();
        currentRead = reading;
        try {
            using var grant = await access.OpenAsync(new(changes.Scope, EditingOwner.CrmHrParty, retainedPartyId), reading.Token);
            using var lifetime = grant.Lifetime.Register(reading.Cancel);
            await access.ValidateAsync(grant, write: true, reading.Token);
            var result = await commands.PreviewMergeAsync(retainedPartyId, duplicateId, reading.Token);
            await access.ValidateAsync(grant, write: true, reading.Token);
            if (opening == Presentation.Opening) {
                Presentation = Presentation with { Phase = PartyMergePhase.Ready, Preview = result.Value,
                    Message = result.IsFailure ? string.Join(" ", result.Errors.Select(error => error.Message)) : null };
            }
        } catch (Exception exception) {
            if (opening == Presentation.Opening) {
                logger.LogWarning("Party merge preview for {RetainedId} and {DuplicateId} failed after {FailureType}.",
                    retainedPartyId, duplicateId, exception.GetType().Name);
                Presentation = Presentation with { Phase = wasUnknown ? PartyMergePhase.Unknown : PartyMergePhase.Ready, Preview = null,
                    Message = "The current merge preview could not be read. Your note is retained; refresh the preview before continuing." };
            }
        } finally {
            if (ReferenceEquals(currentRead, reading)) {
                currentRead = null;
            }
        }
    }

    public async Task<bool> ConfirmAsync(string reason) {
        if (!Presentation.CanConfirm || Presentation.Preview is not { } preview) {
            return false;
        }
        var opening = Presentation.Opening;
        submittedNoteRevision = Note.Revision;
        Presentation = Presentation with { Phase = PartyMergePhase.Applying, Message = null };
        PartyMergeSummaryModel? accepted = null;
        var followupFailed = false;
        try {
            using var grant = await access.OpenAsync(new(changes.Scope, EditingOwner.CrmHrParty, preview.Retained.Id), CancellationToken.None);
            await access.ValidateAsync(grant, write: true, CancellationToken.None);
            using var permission = changes.EnterUiWrite(token => access.ValidateAsync(grant, write: true, token));
            var result = await commands.MergePartyAsync(preview.Retained.Id, preview.Duplicate.Id, grant.Actor, reason,
                original: preview.Original, onCommitted: receipt => accepted = receipt);
            if (opening != Presentation.Opening) {
                return false;
            }
            if (result.IsFailure && accepted is null) {
                Presentation = Presentation with { Phase = PartyMergePhase.Ready, Preview = null,
                    Message = string.Join(" ", result.Errors.Select(error => error.Message)) };
                return false;
            }
            accepted ??= result.Value ?? throw new InvalidOperationException("The merge returned no accepted receipt.");
        } catch (Exception exception) when (accepted is not null) {
            followupFailed = true;
            logger.LogWarning("Party merge committed for {RetainedId} and {DuplicateId}; follow-up failed after {FailureType}.",
                preview.Retained.Id, preview.Duplicate.Id, exception.GetType().Name);
        } catch (Exception exception) {
            logger.LogWarning("Party merge result unavailable for {RetainedId} and {DuplicateId} after {FailureType}.",
                preview.Retained.Id, preview.Duplicate.Id, exception.GetType().Name);
            if (opening == Presentation.Opening) {
                Presentation = Presentation with { Phase = exception is UnauthorizedAccessException ? PartyMergePhase.Ready : PartyMergePhase.Unknown,
                    Preview = null, Message = exception is UnauthorizedAccessException
                        ? "Current CRM write access is required. Refresh the preview with current access."
                        : "The merge result could not be confirmed. Your note is retained. Refresh the preview to read current saved records before another command." };
            }
            return false;
        }
        if (opening != Presentation.Opening || accepted is null) {
            return false;
        }
        Presentation = Presentation with { Phase = PartyMergePhase.Accepted,
            Message = followupFailed ? accepted.Summary + " A follow-up notification failed; refresh the directory after closing." : accepted.Summary };
        return true;
    }
}
