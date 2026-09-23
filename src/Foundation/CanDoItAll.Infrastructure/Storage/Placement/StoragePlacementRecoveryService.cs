using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Infrastructure.Storage;

public sealed partial class StoragePlacementRecoveryService(StorageStablePlacementService storage,
    IStoragePlacementRecoveryAccess access, ICanonicalRuntimeDatabase canonical,
    IDatabaseRuntimeState runtime, IDatabaseRuntimeWriteFence profileFence, CoordinatedDatabaseTransaction transactions) : IStoragePlacementRecovery {
    public async Task<StoragePlacementRecoveryContext> GetCurrentContextAsync(CancellationToken cancellationToken = default) {
        var authorization = await access.AuthorizeAsync(StoragePlacementRecoveryOperation.Read, cancellationToken);
        var context = new StoragePlacementRecoveryContext(canonical.Profile.Profile.Id, canonical.Generation);
        RequireContext(context);
        await access.EnsureCurrentAsync(authorization, cancellationToken);
        return context;
    }

    public async Task<StoragePlacementRecoveryPage> ListPendingAsync(StoragePlacementRecoveryQuery query,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(query);
        if (query.Take is < 1 or > 128 || query.Offset < 0 || query.Offset > int.MaxValue - 129 ||
            query.ProjectId == Guid.Empty || query.StorageId == Guid.Empty) {
            throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.InvalidRequest);
        }
        var authorization = await access.AuthorizeAsync(StoragePlacementRecoveryOperation.Read, cancellationToken);
        RequireContext(query.Context);
        var rows = await storage.ListRecoveryAsync(query, cancellationToken);
        var page = rows.Take(query.Take).ToArray();
        var owners = await access.InspectOwnersAsync(authorization, page.Select(row => row.Identity).ToArray(), cancellationToken);
        var items = page.Select(row => Project(query.Context, row, Owner(owners, row.Identity), authorization)).ToArray();
        await access.EnsureCurrentAsync(authorization, cancellationToken);
        RequireContext(query.Context);
        return new(items, rows.Count > query.Take ? query.Offset + query.Take : null);
    }

    public async Task<StoragePlacementRecoveryItem> GetAsync(StoragePlacementRecoveryCommand request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        var authorization = await access.AuthorizeAsync(StoragePlacementRecoveryOperation.Read, cancellationToken);
        RequireContext(request.Context);
        return await ReadAsync(request, authorization, cancellationToken);
    }

    public Task<StoragePlacementRecoveryItem> ReconcileAsync(StoragePlacementRecoveryCommand request,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(request, StoragePlacementRecoveryOperation.Reconcile, cancellationToken);

    public Task<StoragePlacementRecoveryItem> RecordOperatorVerifiedExternalDispatchTerminationAsync(
        StoragePlacementExternalTerminationVerification request, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.VerifiedExternalDispatchStopped) {
            throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.InvalidRequest);
        }
        return ExecuteAsync(new(request.Context, request.IntentId), StoragePlacementRecoveryOperation.VerifyExternalTermination,
            cancellationToken);
    }

    private async Task<StoragePlacementRecoveryItem> ExecuteAsync(StoragePlacementRecoveryCommand request,
        StoragePlacementRecoveryOperation operation, CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);
        var authorization = await access.AuthorizeAsync(operation, cancellationToken);
        var expected = RequireContext(request.Context);
        try {
            return await profileFence.ExecuteAsync(expected, async token => {
                var item = await ReadAsync(request, authorization, token);
                var requiredAction = operation == StoragePlacementRecoveryOperation.Reconcile
                    ? StoragePlacementRecoveryAction.Reconcile : StoragePlacementRecoveryAction.VerifyExternalTermination;
                if (item.AvailableAction != requiredAction) {
                    throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.Blocked);
                }
                var guard = new StorageRecoveryMutationGuard(
                    current => RequireBeforeProviderAsync(request, authorization, current),
                    async (database, row, current) => {
                        RequireContext(request.Context);
                        var identity = await StorageStablePlacementService.RequireOriginalRecoveryTargetAsync(database, row, current);
                        if (identity != item.Identity) {
                            throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.Blocked);
                        }
                        using (transactions.Enter(database)) {
                            await access.RequireOriginalOwnerForMutationAsync(authorization, identity, current);
                        }
                    });
                if (operation == StoragePlacementRecoveryOperation.Reconcile) {
                    await storage.ReconcileForRecoveryAsync(request.IntentId, guard, token);
                } else {
                    await storage.VerifyTerminationForRecoveryAsync(request.IntentId, guard, token);
                }
                return await ReadAsync(request, authorization, token);
            }, cancellationToken);
        } catch (DatabaseRuntimeProfileChangedException) {
            throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.StaleContext);
        }
    }

    private async Task RequireBeforeProviderAsync(StoragePlacementRecoveryCommand request,
        StoragePlacementRecoveryAuthorization authorization, CancellationToken cancellationToken) {
        var item = await ReadAsync(request, authorization, cancellationToken);
        if (item.Block != StoragePlacementRecoveryBlock.None) {
            throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.Blocked);
        }
    }

    private async Task<StoragePlacementRecoveryItem> ReadAsync(StoragePlacementRecoveryCommand request,
        StoragePlacementRecoveryAuthorization authorization, CancellationToken cancellationToken) {
        if (request.IntentId.Value == Guid.Empty) {
            throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.InvalidRequest);
        }
        var row = await storage.FindRecoveryAsync(request.IntentId, cancellationToken)
            ?? throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.NotFound);
        var owners = await access.InspectOwnersAsync(authorization, [row.Identity], cancellationToken);
        var result = Project(request.Context, row, Owner(owners, row.Identity), authorization);
        await access.EnsureCurrentAsync(authorization, cancellationToken);
        RequireContext(request.Context);
        return result;
    }

    private DatabaseRuntimeSnapshot RequireContext(StoragePlacementRecoveryContext context) {
        if (context is null || context.DatabaseProfileId == Guid.Empty || context.Generation < 0) {
            throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.InvalidRequest);
        }
        var current = runtime.GetSnapshot();
        if (context.DatabaseProfileId != canonical.Profile.Profile.Id || context.Generation != canonical.Generation ||
            current.ActiveProfileId != context.DatabaseProfileId || current.Generation != context.Generation ||
            current.ActiveFingerprint != canonical.Profile.Profile.Runtime.Fingerprint) {
            throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.StaleContext);
        }
        return current;
    }

    private static StoragePlacementRecoveryOwnerObservation Owner(
        IReadOnlyDictionary<Guid, StoragePlacementRecoveryOwnerObservation> owners, StoragePlacementRecoveryIdentity identity)
        => owners.GetValueOrDefault(identity.IntentId.Value)
            ?? new(StoragePlacementRecoveryOwner.Unknown, false, StoragePlacementRecoveryBlock.MissingOwnerAssociation);

    private static StoragePlacementRecoveryItem Project(StoragePlacementRecoveryContext context, StoragePlacementRecoverySnapshot row,
        StoragePlacementRecoveryOwnerObservation owner, StoragePlacementRecoveryAuthorization authorization) {
        var block = owner.Block != StoragePlacementRecoveryBlock.None ? owner.Block : row.StorageBlock;
        var action = StoragePlacementRecoveryAction.None;
        if (block == StoragePlacementRecoveryBlock.None) {
            if (row.DeletionRequested || row.State == StorageStablePlacementState.Deleted) {
                block = StoragePlacementRecoveryBlock.DeletionRequested;
            } else if (row.State == StorageStablePlacementState.Prepared) {
                block = StoragePlacementRecoveryBlock.DispatchNotStarted;
            } else if (row.State == StorageStablePlacementState.Conflict) {
                block = StoragePlacementRecoveryBlock.RetainedConflict;
            } else if (row.State is StorageStablePlacementState.Dispatching or StorageStablePlacementState.Uncertain) {
                var requiresVerification = row.Identity.OriginalProvider == StorageProviderKind.Ftp &&
                    !row.WriteAcknowledged && !row.ExternalDispatchConfirmedStopped;
                if (!authorization.CanReconcile || requiresVerification && !authorization.CanVerifyExternalTermination) {
                    block = StoragePlacementRecoveryBlock.ReadOnlyAuthority;
                } else {
                    action = requiresVerification ? StoragePlacementRecoveryAction.VerifyExternalTermination
                        : StoragePlacementRecoveryAction.Reconcile;
                }
            }
        }
        return new(context, row.Identity, row.State, row.CreatedAtUtc, row.UpdatedAtUtc, row.ReceiptPresent,
            owner.Owner, owner.NativeReceiptPresent, action, block);
    }
}
