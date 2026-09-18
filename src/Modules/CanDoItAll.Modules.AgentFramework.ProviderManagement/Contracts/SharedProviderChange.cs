using System.Collections.Frozen;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public enum SharedProviderChangeKind {
    SourceConfiguration, SourceEnablement, SourceAvailability, SourceDeleted,
    Reconciliation, ImportedSettings, ImportRetirement, Publication
}

public enum SharedProviderCommitState { Committed, Unconfirmed }

public sealed record SharedProviderChange {
    public SharedProviderChange(
        SharedProviderChangeKind kind,
        IEnumerable<Guid> affectedProviderProfileIds,
        IEnumerable<Guid>? retiredProviderProfileIds = null,
        bool remoteOwnedFieldsChanged = false,
        bool catalogMembershipMayHaveChanged = false,
        SharedProviderCommitState commitState = SharedProviderCommitState.Committed,
        bool unknownScope = false,
        string? warning = null) {
        Kind = kind;
        AffectedProviderProfileIds = affectedProviderProfileIds.ToFrozenSet();
        RetiredProviderProfileIds = (retiredProviderProfileIds ?? []).ToFrozenSet();
        RemoteOwnedFieldsChanged = remoteOwnedFieldsChanged;
        CatalogMembershipMayHaveChanged = catalogMembershipMayHaveChanged;
        CommitState = commitState;
        UnknownScope = unknownScope;
        Warning = warning;
    }

    public SharedProviderChangeKind Kind { get; }
    public IReadOnlySet<Guid> AffectedProviderProfileIds { get; }
    public IReadOnlySet<Guid> RetiredProviderProfileIds { get; }
    public bool RemoteOwnedFieldsChanged { get; }
    public bool CatalogMembershipMayHaveChanged { get; }
    public SharedProviderCommitState CommitState { get; }
    public bool UnknownScope { get; }
    public string? Warning { get; init; }
}

public sealed class SharedProviderCommittedException(SharedProviderChange change, Exception innerException)
    : Exception("The shared-provider change is saved, but refreshed state is unavailable.", innerException) {
    public SharedProviderChange Change { get; } = change with {
        Warning = change.Warning ?? "The shared-provider change is saved, but refreshed state is unavailable."
    };
}

internal static class SharedProviderCommitEffects {
    public static async Task<SharedProviderChange> CompleteAsync(SharedProviderChange change, Func<Task> effect) {
        try {
            await effect();
            return change;
        } catch (Exception) {
            return change with { Warning = "The shared-provider change is saved, but a secondary update needs reconciliation." };
        }
    }

    public static async Task<SharedProviderChange> NotifySavedAsync(
        SharedProviderChange change, IEnumerable<IProviderProfileCommitObserver> observers) {
        foreach (var id in change.AffectedProviderProfileIds.Order()) {
            foreach (var observer in observers) {
                change = await CompleteAsync(change, () => observer.ProviderSavedAsync(id, CancellationToken.None));
            }
        }
        return change;
    }
}
