using CanDoItAll.Modules.AgentFramework.ProviderManagement;

namespace CanDoItAll.Modules.AgentFramework;

public enum SharedProviderTargetMutationKind { Publish, Unpublish, ImportedSettings, Retirement }
public enum SharedProviderTargetVerificationDisposition { Satisfied, NotApplied, StillUnconfirmed }

public abstract record SharedProviderTargetPostcondition {
    public sealed record Publication(bool IsPublished) : SharedProviderTargetPostcondition;
    public sealed record ImportedSettings(Guid ImportId, Guid ProviderId, string LocalAlias, bool IsEnabled) : SharedProviderTargetPostcondition;
    public sealed record Retirement(Guid ImportId, Guid ProviderId) : SharedProviderTargetPostcondition;
}

public sealed record SharedProviderTargetAttempt(
    Guid AttemptId, Guid ProviderId, SharedProviderTargetMutationKind Kind,
    SharedProviderProfileSharingSnapshot Before, SharedProviderTargetPostcondition Intended);

public sealed record SharedProviderTargetVerificationResult(
    SharedProviderTargetVerificationDisposition Disposition, SharedProviderChange? Change = null);

public static class SharedProviderTargetVerification {
    public static SharedProviderTargetPostcondition Capture(SharedProviderTargetMutationKind kind,
        SharedProviderProfileSharingSnapshot before, SharedProviderImportedProfileUpdateRequest? request = null) => kind switch {
        SharedProviderTargetMutationKind.Publish => new SharedProviderTargetPostcondition.Publication(true),
        SharedProviderTargetMutationKind.Unpublish => new SharedProviderTargetPostcondition.Publication(false),
        SharedProviderTargetMutationKind.ImportedSettings when request is not null &&
            before.Import?.ImportId == request.ImportId && before.ProviderProfileId == request.ProviderProfileId =>
            new SharedProviderTargetPostcondition.ImportedSettings(request.ImportId, request.ProviderProfileId,
                SharedProviderLocalAliasPolicy.Normalize(request.LocalAlias), request.IsEnabled),
        SharedProviderTargetMutationKind.Retirement when before.Import is { } import =>
            new SharedProviderTargetPostcondition.Retirement(import.ImportId, import.ProviderProfileId),
        _ => throw new ArgumentException("The sharing attempt requires the exact current target and intended settings.", nameof(request))
    };

    public static SharedProviderTargetVerificationResult Evaluate(SharedProviderTargetAttempt attempt,
        SharedProviderProfileSharingSnapshot current) {
        var before = attempt.Before;
        if (attempt.ProviderId != before.ProviderProfileId || attempt.ProviderId != current.ProviderProfileId ||
            current.Ownership != before.Ownership) {
            return new(SharedProviderTargetVerificationDisposition.StillUnconfirmed);
        }
        if (attempt.Intended is SharedProviderTargetPostcondition.Publication publication) {
            if (current.Ownership != SharedProviderProfileOwnership.Local ||
                current.Publication is { ConcurrencyToken: var currentToken } && currentToken == Guid.Empty ||
                before.Publication is { } prior && (prior.ConcurrencyToken == Guid.Empty ||
                    current.Publication?.Id != prior.Id || current.Publication.PublicId != prior.PublicId)) {
                return new(SharedProviderTargetVerificationDisposition.StillUnconfirmed);
            }
            var unchanged = before.Publication is null ? current.Publication is null
                : current.Publication!.ConcurrencyToken == before.Publication.ConcurrencyToken &&
                    current.Publication.IsPublished == before.Publication.IsPublished;
            var satisfied = current.Publication?.IsPublished == publication.IsPublished;
            return Classify(satisfied, unchanged,
                new(SharedProviderChangeKind.Publication, [attempt.ProviderId]));
        }
        if (before.Import is not { } oldImport || current.Import is not { } importState ||
            current.Ownership != SharedProviderProfileOwnership.Imported ||
            oldImport.ProviderProfileId != attempt.ProviderId || importState.ProviderProfileId != attempt.ProviderId ||
            importState.ImportId != oldImport.ImportId || importState.SourceId != oldImport.SourceId ||
            importState.RemotePublicationId != oldImport.RemotePublicationId ||
            oldImport.ImportConcurrencyToken == Guid.Empty || oldImport.ProviderConcurrencyToken == Guid.Empty ||
            importState.ImportConcurrencyToken == Guid.Empty || importState.ProviderConcurrencyToken == Guid.Empty) {
            return new(SharedProviderTargetVerificationDisposition.StillUnconfirmed);
        }
        var sameBefore = importState.ImportConcurrencyToken == oldImport.ImportConcurrencyToken &&
            importState.ProviderConcurrencyToken == oldImport.ProviderConcurrencyToken &&
            importState.LocalAlias == oldImport.LocalAlias && importState.IsEnabled == oldImport.IsEnabled &&
            importState.SelectionState == oldImport.SelectionState;
        var desired = attempt.Intended switch {
            SharedProviderTargetPostcondition.ImportedSettings settings =>
                settings.ImportId == importState.ImportId && settings.ProviderId == importState.ProviderProfileId &&
                settings.LocalAlias == importState.LocalAlias && settings.IsEnabled == importState.IsEnabled &&
                importState.SelectionState == oldImport.SelectionState,
            SharedProviderTargetPostcondition.Retirement retirement =>
                retirement.ImportId == importState.ImportId && retirement.ProviderId == importState.ProviderProfileId &&
                importState.SelectionState == SharedProviderSelectionState.Retired,
            _ => false
        };
        var retired = attempt.Intended is SharedProviderTargetPostcondition.Retirement;
        return Classify(desired, sameBefore, new(retired ? SharedProviderChangeKind.ImportRetirement : SharedProviderChangeKind.ImportedSettings,
            [attempt.ProviderId], retired ? [attempt.ProviderId] : [], catalogMembershipMayHaveChanged: retired));
    }

    private static SharedProviderTargetVerificationResult Classify(bool satisfied, bool unchanged, SharedProviderChange change) =>
        satisfied ? new(SharedProviderTargetVerificationDisposition.Satisfied, unchanged ? null : change)
        : new(unchanged ? SharedProviderTargetVerificationDisposition.NotApplied : SharedProviderTargetVerificationDisposition.StillUnconfirmed);
}
