using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Modules.Workspace;

public static class SharedProviderImportTransitions
{
    public static SharedProviderImport Create(
        Guid sourceId,
        Guid providerProfileId,
        SharedProviderRemotePublicationState remotePublication,
        DateTimeOffset timestampUtc)
    {
        SharedProviderStateGuard.NonEmpty(sourceId, nameof(sourceId));
        SharedProviderStateGuard.NonEmpty(providerProfileId, nameof(providerProfileId));
        ValidateRemotePublication(remotePublication);
        SharedProviderStateGuard.Utc(timestampUtc, nameof(timestampUtc));

        var import = new SharedProviderImport
        {
            SourceId = sourceId,
            ProviderProfileId = providerProfileId,
            SelectionState = SharedProviderSelectionState.Selected,
            AvailabilityState = SharedProviderAvailabilityState.Available,
            LastSeenAtUtc = timestampUtc,
            LastSyncAtUtc = timestampUtc,
            CreatedAtUtc = timestampUtc,
            UpdatedAtUtc = timestampUtc
        };
        ApplyRemotePublication(import, remotePublication);
        return import;
    }

    public static void ReconcileAvailable(
        SharedProviderImport import,
        SharedProviderRemotePublicationState remotePublication,
        DateTimeOffset timestampUtc)
    {
        Validate(import);
        ValidateRemotePublication(remotePublication);
        if (import.RemotePublicationId != remotePublication.PublicationId)
        {
            throw new ArgumentException(
                "A remote publication cannot be reconciled into a different import identity.",
                nameof(remotePublication));
        }

        SharedProviderStateGuard.TransitionTimestamp(
            timestampUtc,
            import.UpdatedAtUtc,
            nameof(timestampUtc));
        ApplyRemotePublication(import, remotePublication);
        import.AvailabilityState = SharedProviderAvailabilityState.Available;
        import.LastSeenAtUtc = timestampUtc;
        import.LastSyncAtUtc = timestampUtc;
        import.UpdatedAtUtc = timestampUtc;
    }

    public static void MarkAuthoritativelyAbsent(
        SharedProviderImport import,
        SharedProviderAvailabilityState state,
        DateTimeOffset timestampUtc)
    {
        Validate(import);
        if (state is not (SharedProviderAvailabilityState.Unpublished or
            SharedProviderAvailabilityState.Missing))
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                state,
                "Only a successful authoritative catalog can mark an import unpublished or missing.");
        }

        SharedProviderStateGuard.TransitionTimestamp(
            timestampUtc,
            import.UpdatedAtUtc,
            nameof(timestampUtc));
        import.AvailabilityState = state;
        import.LastSyncAtUtc = timestampUtc;
        import.UpdatedAtUtc = timestampUtc;
    }

    public static void MarkTransientlyUnavailable(
        SharedProviderImport import,
        SharedProviderAvailabilityState state,
        DateTimeOffset timestampUtc)
    {
        Validate(import);
        if (state is not (SharedProviderAvailabilityState.SourceOffline or
            SharedProviderAvailabilityState.AuthorizationFailed or
            SharedProviderAvailabilityState.SourceIdentityMismatch or
            SharedProviderAvailabilityState.IncompatibleContract))
        {
            throw new ArgumentOutOfRangeException(
                nameof(state),
                state,
                "The requested import state is not a transient or trust failure.");
        }

        SharedProviderStateGuard.TransitionTimestamp(
            timestampUtc,
            import.UpdatedAtUtc,
            nameof(timestampUtc));
        import.AvailabilityState = state;
        import.LastSyncAtUtc = timestampUtc;
        import.UpdatedAtUtc = timestampUtc;
    }

    public static void Retire(SharedProviderImport import, DateTimeOffset timestampUtc)
    {
        Validate(import);
        SharedProviderStateGuard.TransitionTimestamp(
            timestampUtc,
            import.UpdatedAtUtc,
            nameof(timestampUtc));
        if (import.SelectionState == SharedProviderSelectionState.Retired)
        {
            return;
        }

        import.SelectionState = SharedProviderSelectionState.Retired;
        import.UpdatedAtUtc = timestampUtc;
    }

    public static void Reactivate(SharedProviderImport import, DateTimeOffset timestampUtc)
    {
        Validate(import);
        SharedProviderStateGuard.TransitionTimestamp(
            timestampUtc,
            import.UpdatedAtUtc,
            nameof(timestampUtc));
        if (import.SelectionState == SharedProviderSelectionState.Selected)
        {
            return;
        }

        import.SelectionState = SharedProviderSelectionState.Selected;
        import.UpdatedAtUtc = timestampUtc;
    }

    private static void ApplyRemotePublication(
        SharedProviderImport import,
        SharedProviderRemotePublicationState remotePublication)
    {
        import.RemotePublicationId = remotePublication.PublicationId;
        import.RemoteDisplayName = remotePublication.DisplayName;
        import.RemoteRevision = remotePublication.Revision;
        import.RemotePurpose = remotePublication.Purpose;
        import.RemoteTransport = remotePublication.Transport;
        import.RemoteDefaultModelId = remotePublication.DefaultModelId;
        import.RemoteCatalogSnapshotJson = remotePublication.CatalogSnapshotJson;
    }

    private static void Validate(SharedProviderImport import)
    {
        ArgumentNullException.ThrowIfNull(import);
        SharedProviderStateGuard.NonEmpty(import.Id, nameof(import));
        SharedProviderStateGuard.NonEmpty(import.SourceId, nameof(import));
        SharedProviderStateGuard.NonEmpty(import.ProviderProfileId, nameof(import));
        SharedProviderStateGuard.PublicationId(import.RemotePublicationId, nameof(import));
        if (!SharedProviderPublicRevision.TryParse(import.RemoteRevision.Value, out _) ||
            !SharedProviderRoutingModelIdCodec.TryParse(
                import.RemoteDefaultModelId.Value,
                out _,
                out var route) ||
            route.PublicationId != import.RemotePublicationId ||
            !Enum.IsDefined(import.SelectionState) ||
            !Enum.IsDefined(import.AvailabilityState))
        {
            throw new ArgumentException("The shared-provider import state is invalid.", nameof(import));
        }

        SharedProviderStateGuard.Utc(import.CreatedAtUtc, nameof(import));
        SharedProviderStateGuard.Utc(import.UpdatedAtUtc, nameof(import));
    }

    private static void ValidateRemotePublication(
        SharedProviderRemotePublicationState remotePublication)
    {
        ArgumentNullException.ThrowIfNull(remotePublication);
        SharedProviderStateGuard.PublicationId(
            remotePublication.PublicationId,
            nameof(remotePublication));
        if (!SharedProviderPublicRevision.TryParse(remotePublication.Revision.Value, out _) ||
            !SharedProviderRoutingModelIdCodec.TryParse(
                remotePublication.DefaultModelId.Value,
                out _,
                out var route) ||
            route.PublicationId != remotePublication.PublicationId ||
            string.IsNullOrEmpty(remotePublication.CatalogSnapshotJson))
        {
            throw new ArgumentException(
                "The remote publication state is invalid.",
                nameof(remotePublication));
        }
    }
}
