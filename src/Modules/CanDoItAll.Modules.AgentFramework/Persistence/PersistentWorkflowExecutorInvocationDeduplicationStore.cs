using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class PersistentWorkflowExecutorInvocationDeduplicationStore :
    IWorkflowExecutorInvocationDeduplicationStore
{
    internal const string DataProtectionPurpose =
        "CanDoItAll.Modules.AgentFramework.WorkflowExecutorInvocationResult.v1";
    private const int MaximumClaimContentionRetries = 5;
    private readonly IDbContextFactory<AppDbContext> dbContextFactory;
    private readonly IDataProtector resultProtector;

    public PersistentWorkflowExecutorInvocationDeduplicationStore(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IDataProtectionProvider dataProtectionProvider)
    {
        this.dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        resultProtector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);
    }

    public async Task<WorkflowExecutorInvocationClaimResult> TryClaimAsync(
        WorkflowExecutorInvocationClaimRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateClaimRequest(request);
        await using (var providerContext = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            if (WorkflowPersistenceProvider.IsInMemory(providerContext))
            {
                return await TryClaimInMemoryAsync(request, cancellationToken);
            }

            WorkflowPersistenceProvider.EnsureRelational(providerContext);
        }

        if (await TryInsertClaimAsync(request, cancellationToken))
        {
            var inserted = WorkflowExecutorInvocationRecordEntity.CreateClaimed(request);
            return Claimed(ToRecord(inserted));
        }

        for (var retry = 0; retry < MaximumClaimContentionRetries; retry++)
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var existing = await FindByScopeAsync(
                dbContext,
                request.Identity.ScopeKey,
                cancellationToken);
            if (existing is null)
            {
                if (await TryInsertClaimAsync(request, cancellationToken))
                {
                    var inserted = WorkflowExecutorInvocationRecordEntity.CreateClaimed(request);
                    return Claimed(ToRecord(inserted));
                }

                continue;
            }

            if (!HasSameInvocation(existing, request.Identity))
            {
                return new WorkflowExecutorInvocationClaimResult(
                    WorkflowExecutorInvocationClaimOutcome.InputMismatch,
                    ToRecord(existing),
                    Claim: null);
            }

            var record = ToRecord(existing);
            if (existing.State == WorkflowExecutorInvocationState.Completed)
            {
                return new WorkflowExecutorInvocationClaimResult(
                    WorkflowExecutorInvocationClaimOutcome.ReplayedCompleted,
                    record,
                    Claim: null);
            }

            if (existing.State == WorkflowExecutorInvocationState.FailedTerminal)
            {
                return new WorkflowExecutorInvocationClaimResult(
                    WorkflowExecutorInvocationClaimOutcome.FailedTerminal,
                    record,
                    Claim: null);
            }

            if (existing.State == WorkflowExecutorInvocationState.Claimed &&
                existing.LeaseExpiresAtUtc > request.ClaimedAtUtc)
            {
                return new WorkflowExecutorInvocationClaimResult(
                    WorkflowExecutorInvocationClaimOutcome.ActiveLease,
                    record,
                    Claim: null);
            }

            if (existing.Attempt >= request.MaximumAttempts)
            {
                var exhaustedAffected = await dbContext.Set<WorkflowExecutorInvocationRecordEntity>()
                    .Where(item =>
                        item.Id == existing.Id &&
                        item.ConcurrencyVersion == existing.ConcurrencyVersion &&
                        (item.State == WorkflowExecutorInvocationState.FailedRetryable ||
                            (item.State == WorkflowExecutorInvocationState.Claimed &&
                                item.LeaseExpiresAtUtc <= request.ClaimedAtUtc)))
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(item => item.State, WorkflowExecutorInvocationState.FailedTerminal)
                        .SetProperty(item => item.ConcurrencyVersion, item => item.ConcurrencyVersion + 1)
                        .SetProperty(item => item.LeaseOwnerId, (string?)null)
                        .SetProperty(item => item.LeaseAcquiredAtUtc, (DateTimeOffset?)null)
                        .SetProperty(item => item.LeaseExpiresAtUtc, (DateTimeOffset?)null)
                        .SetProperty(
                            item => item.FailureCode,
                            WorkflowExecutorInvocationFailureCode.AttemptLimitReached.Value)
                        .SetProperty(
                            item => item.SafeMessage,
                            "The governed executor invocation exhausted its bounded recovery attempts.")
                        .SetProperty(item => item.UpdatedAtUtc, request.ClaimedAtUtc),
                        cancellationToken);
                if (exhaustedAffected == 1)
                {
                    return new WorkflowExecutorInvocationClaimResult(
                        WorkflowExecutorInvocationClaimOutcome.AttemptLimitReached,
                        record with
                        {
                            State = WorkflowExecutorInvocationState.FailedTerminal,
                            ConcurrencyVersion = record.ConcurrencyVersion.Next(),
                            UpdatedAtUtc = request.ClaimedAtUtc,
                            Lease = null,
                            FailureCode = WorkflowExecutorInvocationFailureCode.AttemptLimitReached,
                            SafeMessage = "The governed executor invocation exhausted its bounded recovery attempts."
                        },
                        Claim: null);
                }

                continue;
            }

            var affected = await dbContext.Set<WorkflowExecutorInvocationRecordEntity>()
                .Where(item =>
                    item.Id == existing.Id &&
                    item.ConcurrencyVersion == existing.ConcurrencyVersion &&
                    (item.State == WorkflowExecutorInvocationState.FailedRetryable ||
                        (item.State == WorkflowExecutorInvocationState.Claimed &&
                            item.LeaseExpiresAtUtc <= request.ClaimedAtUtc)))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.State, WorkflowExecutorInvocationState.Claimed)
                    .SetProperty(item => item.Attempt, item => item.Attempt + 1)
                    .SetProperty(item => item.ConcurrencyVersion, item => item.ConcurrencyVersion + 1)
                    .SetProperty(item => item.LeaseOwnerId, request.LeaseOwnerId.Value)
                    .SetProperty(item => item.LeaseEpoch, item => item.LeaseEpoch + 1)
                    .SetProperty(item => item.LeaseAcquiredAtUtc, request.ClaimedAtUtc)
                    .SetProperty(item => item.LeaseExpiresAtUtc, request.LeaseExpiresAtUtc)
                    .SetProperty(item => item.FailureCode, string.Empty)
                    .SetProperty(item => item.SafeMessage, string.Empty)
                    .SetProperty(item => item.UpdatedAtUtc, request.ClaimedAtUtc),
                    cancellationToken);
            if (affected == 1)
            {
                var claimed = record with
                {
                    State = WorkflowExecutorInvocationState.Claimed,
                    Attempt = existing.Attempt + 1,
                    ConcurrencyVersion = record.ConcurrencyVersion.Next(),
                    UpdatedAtUtc = request.ClaimedAtUtc,
                    Lease = new WorkflowExecutorInvocationLease(
                        request.LeaseOwnerId,
                        new WorkflowExecutorInvocationLeaseEpoch(checked(existing.LeaseEpoch + 1)),
                        request.ClaimedAtUtc,
                        request.LeaseExpiresAtUtc),
                    FailureCode = null,
                    SafeMessage = string.Empty
                };
                return Claimed(claimed);
            }
        }

        return new WorkflowExecutorInvocationClaimResult(
            WorkflowExecutorInvocationClaimOutcome.ConcurrencyConflict,
            Record: null,
            Claim: null);
    }

    public async Task<WorkflowExecutorInvocationRecord?> GetAsync(
        WorkflowExecutorInvocationKey key,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<WorkflowExecutorInvocationRecordEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.InvocationKey == key.Value, cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<WorkflowExecutorInvocationMutationResult> TryRenewLeaseAsync(
        WorkflowExecutorInvocationLeaseRenewalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.LeaseExpiresAtUtc <= request.RenewedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Workflow executor invocation lease must expire after renewal time.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (WorkflowPersistenceProvider.IsInMemory(dbContext))
        {
            return await MutateInMemoryAsync(
                dbContext,
                request.Key,
                expectedInputHash: null,
                request.ExpectedVersion,
                request.LeaseOwnerId,
                request.LeaseEpoch,
                request.RenewedAtUtc,
                additionalGuard: null,
                entity =>
                {
                    entity.ConcurrencyVersion++;
                    entity.LeaseExpiresAtUtc = request.LeaseExpiresAtUtc;
                    entity.UpdatedAtUtc = request.RenewedAtUtc;
                },
                cancellationToken);
        }

        WorkflowPersistenceProvider.EnsureRelational(dbContext);
        var affected = await dbContext.Set<WorkflowExecutorInvocationRecordEntity>()
            .Where(item =>
                item.InvocationKey == request.Key.Value &&
                item.State == WorkflowExecutorInvocationState.Claimed &&
                item.ConcurrencyVersion == request.ExpectedVersion.Value &&
                item.LeaseOwnerId == request.LeaseOwnerId.Value &&
                item.LeaseEpoch == request.LeaseEpoch.Value &&
                item.LeaseExpiresAtUtc > request.RenewedAtUtc)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.ConcurrencyVersion, item => item.ConcurrencyVersion + 1)
                .SetProperty(item => item.LeaseExpiresAtUtc, request.LeaseExpiresAtUtc)
                .SetProperty(item => item.UpdatedAtUtc, request.RenewedAtUtc),
                cancellationToken);
        return affected == 1
            ? await UpdatedAsync(request.Key, cancellationToken)
            : await ClassifyMutationFailureAsync(
                request.Key,
                expectedInputHash: null,
                request.ExpectedVersion,
                request.LeaseOwnerId,
                request.LeaseEpoch,
                request.RenewedAtUtc,
                cancellationToken);
    }

    public async Task<WorkflowExecutorInvocationMutationResult> TryCompleteAsync(
        WorkflowExecutorInvocationCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.StoredResult);
        var storedResult = CanonicalizeForPostgreSql(request.StoredResult);
        var storedResultJson = WorkflowExecutorJson.Serialize(storedResult);
        var storedResultHash = ComputeSha256(storedResultJson);
        var protectedStoredResult = resultProtector.Protect(storedResultJson);
        var completedAtUtc = storedResult.CompletedAtUtc;
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (WorkflowPersistenceProvider.IsInMemory(dbContext))
        {
            return await MutateInMemoryAsync(
                dbContext,
                request.Key,
                request.ExpectedInputHash,
                request.ExpectedVersion,
                request.LeaseOwnerId,
                request.LeaseEpoch,
                completedAtUtc,
                entity => string.Equals(
                    entity.NodeId,
                    request.StoredResult.Result.NodeId.Value,
                    StringComparison.Ordinal),
                entity =>
                {
                    entity.State = WorkflowExecutorInvocationState.Completed;
                    entity.ConcurrencyVersion++;
                    entity.ProtectedStoredResult = protectedStoredResult;
                    entity.StoredResultHash = storedResultHash;
                    entity.CompletedAtUtc = completedAtUtc;
                    ClearLease(entity);
                    entity.FailureCode = string.Empty;
                    entity.SafeMessage = string.Empty;
                    entity.UpdatedAtUtc = completedAtUtc;
                },
                cancellationToken);
        }

        WorkflowPersistenceProvider.EnsureRelational(dbContext);
        var affected = await dbContext.Set<WorkflowExecutorInvocationRecordEntity>()
            .Where(item =>
                item.InvocationKey == request.Key.Value &&
                item.InputHash == request.ExpectedInputHash.Value &&
                item.State == WorkflowExecutorInvocationState.Claimed &&
                item.ConcurrencyVersion == request.ExpectedVersion.Value &&
                item.LeaseOwnerId == request.LeaseOwnerId.Value &&
                item.LeaseEpoch == request.LeaseEpoch.Value &&
                item.LeaseExpiresAtUtc > completedAtUtc &&
                item.NodeId == request.StoredResult.Result.NodeId.Value)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.State, WorkflowExecutorInvocationState.Completed)
                .SetProperty(item => item.ConcurrencyVersion, item => item.ConcurrencyVersion + 1)
                .SetProperty(item => item.ProtectedStoredResult, protectedStoredResult)
                .SetProperty(item => item.StoredResultHash, storedResultHash)
                .SetProperty(item => item.CompletedAtUtc, completedAtUtc)
                .SetProperty(item => item.LeaseOwnerId, (string?)null)
                .SetProperty(item => item.LeaseAcquiredAtUtc, (DateTimeOffset?)null)
                .SetProperty(item => item.LeaseExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(item => item.FailureCode, string.Empty)
                .SetProperty(item => item.SafeMessage, string.Empty)
                .SetProperty(item => item.UpdatedAtUtc, completedAtUtc),
                cancellationToken);
        return affected == 1
            ? await UpdatedAsync(request.Key, cancellationToken)
            : await ClassifyMutationFailureAsync(
                request.Key,
                request.ExpectedInputHash,
                request.ExpectedVersion,
                request.LeaseOwnerId,
                request.LeaseEpoch,
                completedAtUtc,
                cancellationToken);
    }

    public async Task<WorkflowExecutorInvocationMutationResult> TryFailAsync(
        WorkflowExecutorInvocationFailureRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.FailureState is not (
            WorkflowExecutorInvocationState.FailedRetryable or
            WorkflowExecutorInvocationState.FailedTerminal))
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Workflow executor invocation failure must be retryable or terminal.");
        }

        var failureCode = RequireBoundedText(
            request.FailureCode.Value,
            128,
            nameof(request.FailureCode));
        var safeMessage = RequireBoundedText(request.SafeMessage, 1024, nameof(request.SafeMessage));
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (WorkflowPersistenceProvider.IsInMemory(dbContext))
        {
            return await MutateInMemoryAsync(
                dbContext,
                request.Key,
                request.ExpectedInputHash,
                request.ExpectedVersion,
                request.LeaseOwnerId,
                request.LeaseEpoch,
                request.FailedAtUtc,
                additionalGuard: null,
                entity =>
                {
                    entity.State = request.FailureState;
                    entity.ConcurrencyVersion++;
                    entity.FailureCode = failureCode;
                    entity.SafeMessage = safeMessage;
                    ClearLease(entity);
                    entity.UpdatedAtUtc = request.FailedAtUtc;
                },
                cancellationToken);
        }

        WorkflowPersistenceProvider.EnsureRelational(dbContext);
        var affected = await dbContext.Set<WorkflowExecutorInvocationRecordEntity>()
            .Where(item =>
                item.InvocationKey == request.Key.Value &&
                item.InputHash == request.ExpectedInputHash.Value &&
                item.State == WorkflowExecutorInvocationState.Claimed &&
                item.ConcurrencyVersion == request.ExpectedVersion.Value &&
                item.LeaseOwnerId == request.LeaseOwnerId.Value &&
                item.LeaseEpoch == request.LeaseEpoch.Value &&
                item.LeaseExpiresAtUtc > request.FailedAtUtc)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.State, request.FailureState)
                .SetProperty(item => item.ConcurrencyVersion, item => item.ConcurrencyVersion + 1)
                .SetProperty(item => item.FailureCode, failureCode)
                .SetProperty(item => item.SafeMessage, safeMessage)
                .SetProperty(item => item.LeaseOwnerId, (string?)null)
                .SetProperty(item => item.LeaseAcquiredAtUtc, (DateTimeOffset?)null)
                .SetProperty(item => item.LeaseExpiresAtUtc, (DateTimeOffset?)null)
                .SetProperty(item => item.UpdatedAtUtc, request.FailedAtUtc),
                cancellationToken);
        return affected == 1
            ? await UpdatedAsync(request.Key, cancellationToken)
            : await ClassifyMutationFailureAsync(
                request.Key,
                request.ExpectedInputHash,
                request.ExpectedVersion,
                request.LeaseOwnerId,
                request.LeaseEpoch,
                request.FailedAtUtc,
                cancellationToken);
    }

    private async Task<WorkflowExecutorInvocationClaimResult> TryClaimInMemoryAsync(
        WorkflowExecutorInvocationClaimRequest request,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        using var mutationLease = await WorkflowPersistenceProvider.EnterInMemoryMutationAsync(
            dbContext,
            cancellationToken);
        var existing = await dbContext.Set<WorkflowExecutorInvocationRecordEntity>()
            .SingleOrDefaultAsync(
                item => item.ScopeKey == request.Identity.ScopeKey.Value,
                cancellationToken);
        if (existing is null)
        {
            var inserted = WorkflowExecutorInvocationRecordEntity.CreateClaimed(request);
            dbContext.Set<WorkflowExecutorInvocationRecordEntity>().Add(inserted);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Claimed(ToRecord(inserted));
        }

        if (!HasSameInvocation(existing, request.Identity))
        {
            return new WorkflowExecutorInvocationClaimResult(
                WorkflowExecutorInvocationClaimOutcome.InputMismatch,
                ToRecord(existing),
                Claim: null);
        }

        var record = ToRecord(existing);
        if (existing.State == WorkflowExecutorInvocationState.Completed)
        {
            return new WorkflowExecutorInvocationClaimResult(
                WorkflowExecutorInvocationClaimOutcome.ReplayedCompleted,
                record,
                Claim: null);
        }

        if (existing.State == WorkflowExecutorInvocationState.FailedTerminal)
        {
            return new WorkflowExecutorInvocationClaimResult(
                WorkflowExecutorInvocationClaimOutcome.FailedTerminal,
                record,
                Claim: null);
        }

        if (existing.State == WorkflowExecutorInvocationState.Claimed &&
            existing.LeaseExpiresAtUtc > request.ClaimedAtUtc)
        {
            return new WorkflowExecutorInvocationClaimResult(
                WorkflowExecutorInvocationClaimOutcome.ActiveLease,
                record,
                Claim: null);
        }

        var canRecover = existing.State == WorkflowExecutorInvocationState.FailedRetryable ||
            existing.State == WorkflowExecutorInvocationState.Claimed &&
            existing.LeaseExpiresAtUtc <= request.ClaimedAtUtc;
        if (!canRecover)
        {
            return new WorkflowExecutorInvocationClaimResult(
                WorkflowExecutorInvocationClaimOutcome.ConcurrencyConflict,
                record,
                Claim: null);
        }

        if (existing.Attempt >= request.MaximumAttempts)
        {
            existing.State = WorkflowExecutorInvocationState.FailedTerminal;
            existing.ConcurrencyVersion++;
            ClearLease(existing);
            existing.FailureCode = WorkflowExecutorInvocationFailureCode.AttemptLimitReached.Value;
            existing.SafeMessage =
                "The governed executor invocation exhausted its bounded recovery attempts.";
            existing.UpdatedAtUtc = request.ClaimedAtUtc;
            await dbContext.SaveChangesAsync(cancellationToken);
            return new WorkflowExecutorInvocationClaimResult(
                WorkflowExecutorInvocationClaimOutcome.AttemptLimitReached,
                ToRecord(existing),
                Claim: null);
        }

        existing.State = WorkflowExecutorInvocationState.Claimed;
        existing.Attempt++;
        existing.ConcurrencyVersion++;
        existing.LeaseOwnerId = request.LeaseOwnerId.Value;
        existing.LeaseEpoch++;
        existing.LeaseAcquiredAtUtc = request.ClaimedAtUtc;
        existing.LeaseExpiresAtUtc = request.LeaseExpiresAtUtc;
        existing.FailureCode = string.Empty;
        existing.SafeMessage = string.Empty;
        existing.UpdatedAtUtc = request.ClaimedAtUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Claimed(ToRecord(existing));
    }

    private async Task<WorkflowExecutorInvocationMutationResult> MutateInMemoryAsync(
        AppDbContext dbContext,
        WorkflowExecutorInvocationKey key,
        WorkflowExecutorInputHash? expectedInputHash,
        WorkflowExecutorInvocationConcurrencyVersion expectedVersion,
        WorkflowExecutorInvocationLeaseOwnerId leaseOwnerId,
        WorkflowExecutorInvocationLeaseEpoch leaseEpoch,
        DateTimeOffset observedAtUtc,
        Func<WorkflowExecutorInvocationRecordEntity, bool>? additionalGuard,
        Action<WorkflowExecutorInvocationRecordEntity> mutation,
        CancellationToken cancellationToken)
    {
        using var mutationLease = await WorkflowPersistenceProvider.EnterInMemoryMutationAsync(
            dbContext,
            cancellationToken);
        var entity = await dbContext.Set<WorkflowExecutorInvocationRecordEntity>()
            .SingleOrDefaultAsync(item => item.InvocationKey == key.Value, cancellationToken);
        if (entity is null)
        {
            return new WorkflowExecutorInvocationMutationResult(
                WorkflowExecutorInvocationMutationOutcome.NotFound,
                Record: null);
        }

        var record = ToRecord(entity);
        var failure = expectedInputHash is { } inputHash && record.Identity.InputHash != inputHash
            ? WorkflowExecutorInvocationMutationOutcome.InputMismatch
            : record.State != WorkflowExecutorInvocationState.Claimed
                ? WorkflowExecutorInvocationMutationOutcome.InvalidState
                : record.ConcurrencyVersion != expectedVersion
                    ? WorkflowExecutorInvocationMutationOutcome.ConcurrencyConflict
                    : record.Lease is null ||
                      record.Lease.OwnerId != leaseOwnerId ||
                      record.Lease.Epoch != leaseEpoch
                        ? WorkflowExecutorInvocationMutationOutcome.LeaseConflict
                        : record.Lease.ExpiresAtUtc <= observedAtUtc
                            ? WorkflowExecutorInvocationMutationOutcome.LeaseExpired
                            : additionalGuard is not null && !additionalGuard(entity)
                                ? WorkflowExecutorInvocationMutationOutcome.ConcurrencyConflict
                                : (WorkflowExecutorInvocationMutationOutcome?)null;
        if (failure.HasValue)
        {
            return new WorkflowExecutorInvocationMutationResult(failure.Value, record);
        }

        mutation(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new WorkflowExecutorInvocationMutationResult(
            WorkflowExecutorInvocationMutationOutcome.Updated,
            ToRecord(entity));
    }

    private static void ClearLease(WorkflowExecutorInvocationRecordEntity entity)
    {
        entity.LeaseOwnerId = null;
        entity.LeaseAcquiredAtUtc = null;
        entity.LeaseExpiresAtUtc = null;
    }

    private async Task<bool> TryInsertClaimAsync(
        WorkflowExecutorInvocationClaimRequest request,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        WorkflowPersistenceProvider.EnsureRelational(dbContext);
        dbContext.Set<WorkflowExecutorInvocationRecordEntity>().Add(
            WorkflowExecutorInvocationRecordEntity.CreateClaimed(request));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return false;
        }
    }

    private async Task<WorkflowExecutorInvocationMutationResult> UpdatedAsync(
        WorkflowExecutorInvocationKey key,
        CancellationToken cancellationToken)
    {
        var record = await GetAsync(key, cancellationToken) ?? throw new InvalidOperationException(
            "Updated workflow executor invocation record could not be reloaded.");
        return new WorkflowExecutorInvocationMutationResult(
            WorkflowExecutorInvocationMutationOutcome.Updated,
            record);
    }

    private async Task<WorkflowExecutorInvocationMutationResult> ClassifyMutationFailureAsync(
        WorkflowExecutorInvocationKey key,
        WorkflowExecutorInputHash? expectedInputHash,
        WorkflowExecutorInvocationConcurrencyVersion expectedVersion,
        WorkflowExecutorInvocationLeaseOwnerId leaseOwnerId,
        WorkflowExecutorInvocationLeaseEpoch leaseEpoch,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        var record = await GetAsync(key, cancellationToken);
        var outcome = record switch
        {
            null => WorkflowExecutorInvocationMutationOutcome.NotFound,
            _ when expectedInputHash is { } inputHash && record.Identity.InputHash != inputHash =>
                WorkflowExecutorInvocationMutationOutcome.InputMismatch,
            _ when record.State != WorkflowExecutorInvocationState.Claimed =>
                WorkflowExecutorInvocationMutationOutcome.InvalidState,
            _ when record.ConcurrencyVersion != expectedVersion =>
                WorkflowExecutorInvocationMutationOutcome.ConcurrencyConflict,
            _ when record.Lease is null ||
                record.Lease.OwnerId != leaseOwnerId ||
                record.Lease.Epoch != leaseEpoch =>
                WorkflowExecutorInvocationMutationOutcome.LeaseConflict,
            _ when record.Lease.ExpiresAtUtc <= observedAtUtc =>
                WorkflowExecutorInvocationMutationOutcome.LeaseExpired,
            _ => WorkflowExecutorInvocationMutationOutcome.ConcurrencyConflict
        };
        return new WorkflowExecutorInvocationMutationResult(outcome, record);
    }

    private static async Task<WorkflowExecutorInvocationRecordEntity?> FindByScopeAsync(
        AppDbContext dbContext,
        WorkflowExecutorInvocationScopeKey scopeKey,
        CancellationToken cancellationToken)
        => await dbContext.Set<WorkflowExecutorInvocationRecordEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.ScopeKey == scopeKey.Value, cancellationToken);

    private static WorkflowExecutorInvocationClaimResult Claimed(
        WorkflowExecutorInvocationRecord record)
    {
        var lease = record.Lease ?? throw new InvalidOperationException(
            "Claimed workflow executor invocation record has no lease.");
        return new WorkflowExecutorInvocationClaimResult(
            WorkflowExecutorInvocationClaimOutcome.Claimed,
            record,
            new WorkflowExecutorInvocationClaim(
                record.Identity,
                lease,
                record.Attempt,
                record.ConcurrencyVersion));
    }

    private WorkflowExecutorInvocationRecord ToRecord(
        WorkflowExecutorInvocationRecordEntity entity)
    {
        var identity = new WorkflowExecutorInvocationIdentity(
            new WorkflowExecutorInvocationScopeKey(entity.ScopeKey),
            new WorkflowExecutorInvocationKey(entity.InvocationKey),
            new WorkflowExecutorInvocationIdempotencyKey(entity.IdempotencyKey),
            new WorkflowRunId(entity.RunId),
            new WorkflowVersionId(entity.WorkflowVersionId),
            new WorkflowNodeId(entity.NodeId),
            new WorkflowExecutorId(entity.ExecutorId),
            new WorkflowExecutorContractVersion(entity.ExecutorContractVersion),
            new WorkflowExternalRequestId(entity.CausationRequestId),
            new WorkflowExternalRequestVersion(entity.CausationRequestVersion),
            new WorkflowExternalResponseOperationId(entity.CausationOperationId),
            new WorkflowExecutorInvocationGeneration(entity.LogicalGeneration),
            new WorkflowExecutorInputHash(entity.InputHash));
        var record = new WorkflowExecutorInvocationRecord(
            identity,
            entity.State,
            entity.Attempt,
            new WorkflowExecutorInvocationConcurrencyVersion(entity.ConcurrencyVersion),
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc)
        {
            FailureCode = string.IsNullOrWhiteSpace(entity.FailureCode)
                ? null
                : new WorkflowExecutorInvocationFailureCode(entity.FailureCode),
            SafeMessage = entity.SafeMessage
        };
        if (entity.State == WorkflowExecutorInvocationState.Claimed)
        {
            if (string.IsNullOrWhiteSpace(entity.LeaseOwnerId) ||
                !entity.LeaseAcquiredAtUtc.HasValue ||
                !entity.LeaseExpiresAtUtc.HasValue)
            {
                throw new InvalidOperationException(
                    $"Claimed workflow executor invocation '{entity.InvocationKey}' has incomplete lease state.");
            }

            record = record with
            {
                Lease = new WorkflowExecutorInvocationLease(
                    new WorkflowExecutorInvocationLeaseOwnerId(entity.LeaseOwnerId),
                    new WorkflowExecutorInvocationLeaseEpoch(entity.LeaseEpoch),
                    entity.LeaseAcquiredAtUtc.Value,
                    entity.LeaseExpiresAtUtc.Value)
            };
        }

        if (entity.State == WorkflowExecutorInvocationState.Completed)
        {
            if (string.IsNullOrWhiteSpace(entity.ProtectedStoredResult) ||
                string.IsNullOrWhiteSpace(entity.StoredResultHash) ||
                !entity.CompletedAtUtc.HasValue)
            {
                throw new InvalidOperationException(
                    $"Completed workflow executor invocation '{entity.InvocationKey}' has no replayable result.");
            }

            var storedResult = UnprotectStoredResult(entity);
            if (storedResult.CompletedAtUtc != entity.CompletedAtUtc.Value)
            {
                throw new InvalidOperationException(
                    $"Completed workflow executor invocation '{entity.InvocationKey}' has inconsistent completion time.");
            }

            record = record with { StoredResult = storedResult };
        }

        return record;
    }

    private WorkflowExecutorInvocationStoredResult UnprotectStoredResult(
        WorkflowExecutorInvocationRecordEntity entity)
    {
        try
        {
            var storedResultJson = resultProtector.Unprotect(entity.ProtectedStoredResult);
            var actualHash = SHA256.HashData(Encoding.UTF8.GetBytes(storedResultJson));
            var expectedHash = Convert.FromHexString(entity.StoredResultHash);
            if (actualHash.Length != expectedHash.Length ||
                !CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
            {
                throw new InvalidOperationException("The protected workflow executor result hash is invalid.");
            }

            return WorkflowExecutorJson.Deserialize<WorkflowExecutorInvocationStoredResult>(
                storedResultJson);
        }
        catch (Exception exception) when (exception is
            CryptographicException or
            FormatException or
            ArgumentException or
            JsonException or
            InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Completed workflow executor invocation '{entity.InvocationKey}' has a corrupt protected result.",
                exception);
        }
    }

    private static string ComputeSha256(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private static WorkflowExecutorInvocationStoredResult CanonicalizeForPostgreSql(
        WorkflowExecutorInvocationStoredResult storedResult)
    {
        var completedAtUtc = storedResult.CompletedAtUtc.ToUniversalTime();
        return storedResult with
        {
            CompletedAtUtc = new DateTimeOffset(
                completedAtUtc.Ticks - completedAtUtc.Ticks % TimeSpan.TicksPerMicrosecond,
                TimeSpan.Zero)
        };
    }

    private static bool HasSameInvocation(
        WorkflowExecutorInvocationRecordEntity existing,
        WorkflowExecutorInvocationIdentity requested)
        => string.Equals(existing.InvocationKey, requested.Key.Value, StringComparison.Ordinal) &&
           string.Equals(existing.InputHash, requested.InputHash.Value, StringComparison.Ordinal) &&
           string.Equals(existing.IdempotencyKey, requested.IdempotencyKey.Value, StringComparison.Ordinal) &&
           existing.RunId == requested.RunId.Value &&
           existing.WorkflowVersionId == requested.WorkflowVersionId.Value &&
           string.Equals(existing.NodeId, requested.NodeId.Value, StringComparison.Ordinal) &&
           string.Equals(existing.ExecutorId, requested.ExecutorId.Value, StringComparison.Ordinal) &&
           string.Equals(
               existing.ExecutorContractVersion,
               requested.ExecutorContractVersion.Value,
               StringComparison.Ordinal) &&
           existing.CausationRequestId == requested.CausationRequestId.Value &&
           existing.CausationRequestVersion == requested.CausationRequestVersion.Value &&
           existing.CausationOperationId == requested.CausationOperationId.Value &&
           existing.LogicalGeneration == requested.LogicalGeneration.Value;

    private static void ValidateClaimRequest(WorkflowExecutorInvocationClaimRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Identity);
        if (request.MaximumAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Workflow executor invocation maximum attempts must be positive.");
        }

        if (request.LeaseExpiresAtUtc <= request.ClaimedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Workflow executor invocation lease must expire after claim time.");
        }
    }

    private static string RequireBoundedText(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"Value cannot exceed {maximumLength} characters.");
        }

        return normalized;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return true;
            }
        }

        return false;
    }
}
