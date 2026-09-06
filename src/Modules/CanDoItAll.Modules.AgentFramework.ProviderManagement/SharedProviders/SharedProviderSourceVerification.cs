using System.Collections.Frozen;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public enum SharedProviderSourceMutationKind { Create, Update, Enablement, Delete, Test, Synchronize }

public sealed record SharedProviderSourceMutationAttempt {
    public SharedProviderSourceMutationAttempt(
        Guid sourceId,
        SharedProviderSourceMutationKind kind,
        SharedProviderSourceManagementSnapshot? before = null,
        SharedProviderSourceEditorRequest? request = null,
        bool? intendedEnabled = null,
        IEnumerable<SharedProviderPublicationId>? selection = null) {
        AttemptId = Guid.NewGuid();
        SourceId = sourceId;
        Kind = kind;
        Before = before is null ? null : before with { Imports = Array.AsReadOnly(before.Imports.ToArray()) };
        Request = request;
        IntendedEnabled = intendedEnabled;
        Selection = (selection ?? []).ToFrozenSet();
    }

    public Guid AttemptId { get; }
    public Guid SourceId { get; }
    public SharedProviderSourceMutationKind Kind { get; }
    public SharedProviderSourceManagementSnapshot? Before { get; }
    public SharedProviderSourceEditorRequest? Request { get; }
    public bool? IntendedEnabled { get; }
    public IReadOnlySet<SharedProviderPublicationId> Selection { get; }
}

public sealed record SharedProviderSourceVerificationResult(
    ProviderVerificationDisposition Disposition,
    Guid SourceId,
    IReadOnlyList<SharedProviderSourceManagementSnapshot> Sources,
    SharedProviderChange? Change = null);

public static class SharedProviderSourceVerification {
    public static SharedProviderSourceVerificationResult Evaluate(
        SharedProviderSourceMutationAttempt attempt, IReadOnlyList<SharedProviderSourceManagementSnapshot> sources) {
        var current = sources.SingleOrDefault(item => item.Source.Id == attempt.SourceId);
        var before = attempt.Before;
        var disposition = ProviderVerificationDisposition.StillUnconfirmed;
        if (attempt.Kind == SharedProviderSourceMutationKind.Create) {
            disposition = current is null ? ProviderVerificationDisposition.DefinitelyNotCommitted
                : attempt.Request is { } request && Matches(current.Source, request)
                    ? ProviderVerificationDisposition.Committed : ProviderVerificationDisposition.StillUnconfirmed;
        } else if (attempt.Kind == SharedProviderSourceMutationKind.Delete && current is null) {
            disposition = ProviderVerificationDisposition.Committed;
        } else if (current is not null && before is not null) {
            if (current.Source.ConcurrencyToken == before.Source.ConcurrencyToken) {
                disposition = ProviderVerificationDisposition.DefinitelyNotCommitted;
            } else {
                var postcondition = attempt.Kind switch {
                    SharedProviderSourceMutationKind.Update => attempt.Request is { } request && Matches(current.Source, request),
                    SharedProviderSourceMutationKind.Enablement => current.Source.IsEnabled == attempt.IntendedEnabled,
                    SharedProviderSourceMutationKind.Test => current.Source.LastSyncAtUtc > before.Source.LastSyncAtUtc.GetValueOrDefault()
                        && current.Source.Status != SharedProviderSourceStatus.NeverSynchronized,
                    SharedProviderSourceMutationKind.Synchronize => current.Source.LastSyncAtUtc > before.Source.LastSyncAtUtc.GetValueOrDefault()
                        && current.Source.Status != SharedProviderSourceStatus.NeverSynchronized
                        && attempt.Selection.SetEquals(current.Imports.Where(import =>
                            import.SelectionState == SharedProviderSelectionState.Selected).Select(import => import.RemotePublicationId)),
                    _ => false
                };
                if (postcondition) {
                    disposition = ProviderVerificationDisposition.Committed;
                }
            }
        }
        SharedProviderChange? change = null;
        if (disposition == ProviderVerificationDisposition.Committed) {
            var affected = (before?.Imports ?? []).Select(import => import.ProviderProfileId)
                .Concat((current?.Imports ?? []).Select(import => import.ProviderProfileId));
            var retired = (current?.Imports ?? []).Where(import => import.SelectionState == SharedProviderSelectionState.Retired)
                .Select(import => import.ProviderProfileId);
            var kind = attempt.Kind switch {
                SharedProviderSourceMutationKind.Create or SharedProviderSourceMutationKind.Update => SharedProviderChangeKind.SourceConfiguration,
                SharedProviderSourceMutationKind.Enablement => SharedProviderChangeKind.SourceEnablement,
                SharedProviderSourceMutationKind.Delete => SharedProviderChangeKind.SourceDeleted,
                SharedProviderSourceMutationKind.Test => SharedProviderChangeKind.SourceAvailability,
                _ => SharedProviderChangeKind.Reconciliation
            };
            change = new(kind, affected, retired,
                remoteOwnedFieldsChanged: attempt.Kind == SharedProviderSourceMutationKind.Synchronize,
                catalogMembershipMayHaveChanged: attempt.Kind is SharedProviderSourceMutationKind.Synchronize or SharedProviderSourceMutationKind.Delete);
        }
        return new(disposition, attempt.SourceId, sources, change);
    }

    public static bool Matches(SharedProviderSourceSnapshot source, SharedProviderSourceEditorRequest request) =>
        source.Name == request.Name.Trim() &&
        source.BaseUri.AbsoluteUri.TrimEnd('/') == request.BaseUri.AbsoluteUri.TrimEnd('/') &&
        source.ApiTokenSecretId == request.ApiTokenSecretId &&
        source.IsEnabled == request.IsEnabled &&
        (source.NetworkPolicy == SharedProviderSourceNetworkPolicy.AllowPrivateNetwork) == request.AllowInsecurePrivateNetwork;
}
