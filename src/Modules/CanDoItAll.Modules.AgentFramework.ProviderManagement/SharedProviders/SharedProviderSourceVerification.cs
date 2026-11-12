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
    SharedProviderChange? Change = null) {
    public bool CurrentStateMatchesIntent { get; init; }
}

public static class SharedProviderSourceVerification {
    public static SharedProviderSourceVerificationResult Evaluate(
        SharedProviderSourceMutationAttempt attempt, IReadOnlyList<SharedProviderSourceManagementSnapshot> sources) {
        var current = sources.SingleOrDefault(item => item.Source.Id == attempt.SourceId);
        var satisfies = attempt.Kind switch {
            SharedProviderSourceMutationKind.Create or SharedProviderSourceMutationKind.Update =>
                current is not null && attempt.Request is { } request && Matches(current.Source, request),
            SharedProviderSourceMutationKind.Enablement => current is not null && current.Source.IsEnabled == attempt.IntendedEnabled,
            SharedProviderSourceMutationKind.Delete => current is null,
            SharedProviderSourceMutationKind.Test => current is not null && attempt.Before is { } before &&
                current.Source.LastSyncAtUtc > before.Source.LastSyncAtUtc.GetValueOrDefault() &&
                current.Source.Status != SharedProviderSourceStatus.NeverSynchronized,
            SharedProviderSourceMutationKind.Synchronize => current is not null && attempt.Before is { } original &&
                current.Source.LastSyncAtUtc > original.Source.LastSyncAtUtc.GetValueOrDefault() &&
                current.Source.Status != SharedProviderSourceStatus.NeverSynchronized &&
                attempt.Selection.SetEquals(current.Imports.Where(import => import.SelectionState == SharedProviderSelectionState.Selected)
                    .Select(import => import.RemotePublicationId)),
            _ => false
        };
        return new(ProviderVerificationDisposition.StillUnconfirmed, attempt.SourceId, sources) {
            CurrentStateMatchesIntent = satisfies
        };
    }

    public static bool Matches(SharedProviderSourceSnapshot source, SharedProviderSourceEditorRequest request) =>
        source.Name == request.Name.Trim() &&
        source.BaseUri.AbsoluteUri.TrimEnd('/') == request.BaseUri.AbsoluteUri.TrimEnd('/') &&
        source.ApiTokenSecretId == request.ApiTokenSecretId &&
        source.IsEnabled == request.IsEnabled &&
        (source.NetworkPolicy == SharedProviderSourceNetworkPolicy.AllowPrivateNetwork) == request.AllowInsecurePrivateNetwork;
}
