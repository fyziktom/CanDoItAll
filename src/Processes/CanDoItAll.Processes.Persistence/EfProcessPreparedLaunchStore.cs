using System.Buffers.Binary;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessPreparedLaunchStore(
    IDbContextFactory<ProcessPersistenceDbContext> factory,
    DbContextOptions<ProcessPersistenceDbContext> contextOptions,
    TimeProvider? timeProvider = null,
    CoordinatedDatabaseTransaction? coordinatedTransaction = null,
    IProcessToolLaunchAdmissionPolicy? toolLaunchPolicy = null) : IProcessPreparedLaunchStore, IProcessLaunchLinkReceiptStore {
    private static readonly TimeSpan ContinuationLease = TimeSpan.FromMinutes(2);
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public async Task<ProcessPreparedLaunchSnapshot?> FindByIntentAsync(ProcessLaunchIntentId intentId, CancellationToken cancellationToken = default) {
        await using var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.PreparedLaunches.AsNoTracking()
            .SingleOrDefaultAsync(item => item.CallerIntentId == intentId.Value, cancellationToken).ConfigureAwait(false);
        return entity is null ? null : ProcessPreparedLaunchCodec.Read(entity);
    }

    public async Task<ProcessPreparedLaunchSnapshot?> GetAsync(ProcessLaunchAdmissionId admissionId, CancellationToken cancellationToken = default) {
        await using var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.PreparedLaunches.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == admissionId.Value, cancellationToken).ConfigureAwait(false);
        return entity is null ? null : ProcessPreparedLaunchCodec.Read(entity);
    }

    public async Task<ProcessPreparedLaunchSnapshot?> FindByRunAsync(ProcessRunId runId, CancellationToken cancellationToken = default) {
        await using var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var entity = await context.PreparedLaunches.AsNoTracking()
            .SingleOrDefaultAsync(item => item.RunId == runId.Value, cancellationToken).ConfigureAwait(false);
        return entity is null ? null : ProcessPreparedLaunchCodec.Read(entity);
    }

    public async Task<ProcessPreparedLaunchSnapshot> PrepareAsync(ProcessPreparedLaunch preparation, CancellationToken cancellationToken = default) {
        var entity = ProcessPreparedLaunchCodec.ToEntity(preparation);
        if (preparation.ToolSource is not null && preparation.CallerIntentId is { } toolIntent &&
                await FindByIntentAsync(toolIntent, cancellationToken) is { } retained) {
            if (retained.Preparation.RequestFingerprint != entity.RequestFingerprint) {
                throw new ProcessLaunchIntentConflictException(toolIntent,
                    "The Process tool intent was already prepared with different content, source or target.");
            }
            return retained;
        }
        await using var source = preparation.ToolSource is null ? null
            : await (toolLaunchPolicy ?? throw new InvalidOperationException("Process tool preparation requires its owner admission policy."))
                .AcquireAsync(preparation, cancellationToken);
        await using var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        RequireSupportedProvider(context);
        if (source is not null && (coordinatedTransaction is null || !context.Database.IsNpgsql())) {
            throw new InvalidOperationException("Process tool preparation requires coordinated PostgreSQL owner persistence.");
        }
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false)
            : null;
        using var coordination = source is null ? null : coordinatedTransaction!.Enter(context);
        if (source is not null) {
            await source.RequireForMutationAsync(preparation.LinkTarget, cancellationToken);
        }
        if (preparation.CallerIntentId is { } intentId) {
            if (context.Database.IsNpgsql()) {
                var lockBytes = SHA256.HashData(Encoding.UTF8.GetBytes("process-launch-intent:" + intentId.Value.ToString("D")));
                var lockKey = BinaryPrimitives.ReadInt64LittleEndian(lockBytes);
                await context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockKey})", cancellationToken).ConfigureAwait(false);
            }
            var existing = await context.PreparedLaunches.AsNoTracking()
                .SingleOrDefaultAsync(item => item.CallerIntentId == intentId.Value, cancellationToken).ConfigureAwait(false);
            if (existing is not null) {
                if (existing.RequestFingerprint != entity.RequestFingerprint) {
                    throw new ProcessLaunchIntentConflictException(intentId, "The process launch intent was already prepared with different requested content or target.");
                }
                return ProcessPreparedLaunchCodec.Read(existing);
            }
        }
        context.PreparedLaunches.Add(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (source is not null) {
            await source.RequireForMutationAsync(preparation.LinkTarget, cancellationToken);
        }
        if (transaction is not null) {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        coordination?.Dispose();
        if (source is not null) {
            await source.DisposeAsync();
        }
        return ProcessPreparedLaunchCodec.Read(entity);
    }

    public async Task<ProcessLaunchContinuationClaim?> ClaimContinuationAsync(ProcessLaunchAdmissionId admissionId,
        CancellationToken cancellationToken = default) {
        await using var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        RequireSupportedProvider(context);
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false)
            : null;
        var entity = await RequireLockedAsync(context, admissionId, cancellationToken).ConfigureAwait(false);
        var now = UtcNow();
        if (entity.AcceptedAtUtc is null ||
                entity.State is not (ProcessLaunchContinuationState.Accepted or ProcessLaunchContinuationState.Continuing) ||
                entity.State == ProcessLaunchContinuationState.Continuing && entity.ContinuationLeaseExpiresAtUtc > now) {
            return null;
        }
        entity.State = ProcessLaunchContinuationState.Continuing;
        entity.ContinuationOwner = Guid.NewGuid();
        entity.ContinuationGeneration++;
        entity.ContinuationLeaseExpiresAtUtc = now + ContinuationLease;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (transaction is not null) {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        return new(ProcessPreparedLaunchCodec.Read(entity), entity.ContinuationOwner.Value, entity.ContinuationGeneration);
    }

    public async Task<bool> RenewContinuationAsync(ProcessLaunchContinuationClaim claim, CancellationToken cancellationToken = default) {
        await using var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        RequireSupportedProvider(context);
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false)
            : null;
        var entity = await RequireLockedAsync(context, claim.Snapshot.Preparation.AdmissionId, cancellationToken).ConfigureAwait(false);
        var now = UtcNow();
        if (!OwnsContinuation(entity, claim, now)) {
            return false;
        }
        entity.ContinuationLeaseExpiresAtUtc = now + ContinuationLease;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (transaction is not null) {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        return true;
    }

    public async Task CompleteContinuationAsync(ProcessLaunchContinuationClaim claim, ProcessLaunchContinuationState state,
        string? publicFailure, CancellationToken cancellationToken = default) {
        if (state is not (ProcessLaunchContinuationState.Started or ProcessLaunchContinuationState.ReconciliationRequired or ProcessLaunchContinuationState.Failed)) {
            throw new ArgumentOutOfRangeException(nameof(state));
        }
        if (publicFailure?.Length > 2_000) {
            throw new ArgumentOutOfRangeException(nameof(publicFailure));
        }
        await using var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        RequireSupportedProvider(context);
        await using var transaction = context.Database.IsRelational()
            ? await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken).ConfigureAwait(false)
            : null;
        var entity = await RequireLockedAsync(context, claim.Snapshot.Preparation.AdmissionId, cancellationToken).ConfigureAwait(false);
        if (!OwnsContinuation(entity, claim, UtcNow())) {
            throw new InvalidOperationException("The process launch continuation no longer owns its durable lease.");
        }
        entity.State = state;
        entity.PublicFailure = publicFailure;
        entity.ContinuationOwner = null;
        entity.ContinuationLeaseExpiresAtUtc = null;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (transaction is not null) {
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<ProcessLaunchAdmissionId>> ListPendingContinuationsAsync(int take, CancellationToken cancellationToken = default) {
        if (take is < 1 or > 100) {
            throw new ArgumentOutOfRangeException(nameof(take));
        }
        var now = UtcNow();
        await using var context = await factory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var ids = await context.PreparedLaunches.AsNoTracking()
            .Where(item => item.AcceptedAtUtc != null &&
                (item.State == ProcessLaunchContinuationState.Accepted ||
                    item.State == ProcessLaunchContinuationState.Continuing && item.ContinuationLeaseExpiresAtUtc <= now))
            .OrderBy(item => item.AdmissionSequence).Take(take).Select(item => item.Id)
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        return ids.Select(id => new ProcessLaunchAdmissionId(id)).ToArray();
    }

    public async Task<ProcessPreparedLaunchSnapshot> RequireForDeliveryAsync(ProcessLaunchAdmissionId admissionId,
        string preparationFingerprint, CancellationToken cancellationToken = default) {
        await using var context = await CreateEnlistedAsync(cancellationToken).ConfigureAwait(false);
        var entity = await RequireLockedAsync(context, admissionId, cancellationToken).ConfigureAwait(false);
        return RequireDelivery(entity, preparationFingerprint);
    }

    public async Task<ProcessLaunchLinkReceipt> StageDeliveredAsync(ProcessLaunchAdmissionId admissionId, string preparationFingerprint,
        Guid linkId, CancellationToken cancellationToken = default) {
        if (linkId == Guid.Empty) {
            throw new ArgumentException("A native process link identifier is required.", nameof(linkId));
        }
        await using var context = await CreateEnlistedAsync(cancellationToken).ConfigureAwait(false);
        var entity = await RequireLockedAsync(context, admissionId, cancellationToken).ConfigureAwait(false);
        RequireDelivery(entity, preparationFingerprint);
        if (entity.LinkDeliveryState == ProcessLaunchLinkDeliveryState.Pending) {
            entity.LinkDeliveryState = ProcessLaunchLinkDeliveryState.Delivered;
            entity.DeliveredLinkId = linkId;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        } else if (entity.LinkDeliveryState == ProcessLaunchLinkDeliveryState.Delivered && entity.DeliveredLinkId != linkId) {
            throw new ProcessLaunchIntentConflictException(entity.CallerIntentId is { } intent ? new(intent) : null,
                "The accepted process launch was already delivered to a different native link.");
        }
        return new(admissionId, entity.LinkDeliveryState, entity.DeliveredLinkId) { ConflictReason = entity.LinkConflictReason };
    }

    public async Task<ProcessLaunchLinkReceipt> StageConflictAsync(ProcessLaunchAdmissionId admissionId, string preparationFingerprint,
        ProcessLaunchLinkConflictReason reason, CancellationToken cancellationToken = default) {
        if (!Enum.IsDefined(reason)) {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }
        await using var context = await CreateEnlistedAsync(cancellationToken).ConfigureAwait(false);
        var entity = await RequireLockedAsync(context, admissionId, cancellationToken).ConfigureAwait(false);
        RequireDelivery(entity, preparationFingerprint);
        if (entity.LinkDeliveryState == ProcessLaunchLinkDeliveryState.Pending) {
            entity.LinkDeliveryState = ProcessLaunchLinkDeliveryState.Conflict;
            entity.LinkConflictReason = reason;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        return new(admissionId, entity.LinkDeliveryState, entity.DeliveredLinkId) { ConflictReason = entity.LinkConflictReason };
    }

    public async Task<ProcessLaunchLinkReceipt> StageRemovedAsync(ProcessLaunchAdmissionId admissionId, string preparationFingerprint,
        CancellationToken cancellationToken = default) {
        await using var context = await CreateEnlistedAsync(cancellationToken).ConfigureAwait(false);
        var entity = await RequireLockedAsync(context, admissionId, cancellationToken).ConfigureAwait(false);
        RequireDelivery(entity, preparationFingerprint);
        if (entity.LinkDeliveryState == ProcessLaunchLinkDeliveryState.Pending) {
            throw new InvalidOperationException("A process link without a delivery receipt cannot be marked as a removed committed link.");
        }
        if (entity.LinkDeliveryState == ProcessLaunchLinkDeliveryState.Delivered) {
            entity.LinkDeliveryState = ProcessLaunchLinkDeliveryState.Removed;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        return new(admissionId, entity.LinkDeliveryState, entity.DeliveredLinkId) { ConflictReason = entity.LinkConflictReason };
    }

    internal static async Task<ProcessPreparedLaunchEntity> RequireLockedAsync(ProcessPersistenceDbContext context,
        ProcessLaunchAdmissionId admissionId, CancellationToken cancellationToken) {
        RequireSupportedProvider(context);
        var query = context.Database.IsNpgsql()
            ? context.PreparedLaunches.FromSqlInterpolated($"SELECT * FROM process_prepared_launches WHERE \"Id\" = {admissionId.Value} FOR UPDATE")
            : context.PreparedLaunches.Where(item => item.Id == admissionId.Value);
        return await query.SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Process launch admission '{admissionId.Value:D}' was not found.");
    }

    private Task<ProcessPersistenceDbContext> CreateEnlistedAsync(CancellationToken cancellationToken) {
        if (coordinatedTransaction is null) {
            throw new InvalidOperationException("Process link delivery requires explicit coordination with the native owner transaction.");
        }
        return coordinatedTransaction.CreateEnlistedAsync(contextOptions, options => new ProcessPersistenceDbContext(options), cancellationToken);
    }

    private static ProcessPreparedLaunchSnapshot RequireDelivery(ProcessPreparedLaunchEntity entity, string fingerprint) {
        var snapshot = ProcessPreparedLaunchCodec.Read(entity);
        if (entity.PreparationFingerprint != fingerprint || entity.AcceptedAtUtc is null || snapshot.Preparation.LinkTarget is null) {
            throw new InvalidOperationException("The process link delivery does not match an accepted immutable target.");
        }
        return snapshot;
    }

    private static bool OwnsContinuation(ProcessPreparedLaunchEntity entity, ProcessLaunchContinuationClaim claim, DateTimeOffset now)
        => entity.State == ProcessLaunchContinuationState.Continuing && entity.ContinuationOwner == claim.Owner &&
            entity.ContinuationGeneration == claim.Generation && entity.ContinuationLeaseExpiresAtUtc > now;

    private DateTimeOffset UtcNow() => ProcessPreparedLaunchCodec.NormalizeTimestamp(clock.GetUtcNow());

    private static void RequireSupportedProvider(ProcessPersistenceDbContext context) {
        if (context.Database.IsRelational() && !context.Database.IsNpgsql()) {
            throw new NotSupportedException("Durable process launch admission requires PostgreSQL; nonrelational fixtures do not provide transaction or concurrent-admission guarantees.");
        }
    }
}
