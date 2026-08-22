using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class PersistentWorkflowExternalResponseOperationStore :
    IWorkflowExternalResponseOperationStore
{
    private const string AttemptLimitReachedSafeMessage =
        "The workflow response could not be resumed within the allowed attempt limit.";

    internal const string DataProtectionPurpose =
        "CanDoItAll.Modules.AgentFramework.WorkflowExternalResponsePayload.v1";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDbContextFactory<AppDbContext> dbContextFactory;
    private readonly IDataProtector responseProtector;

    public PersistentWorkflowExternalResponseOperationStore(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IDataProtectionProvider dataProtectionProvider)
    {
        this.dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        responseProtector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);
    }

    public async Task<WorkflowExternalResponseOperationCreateResult> CreateOrReplayAsync(
        WorkflowExternalResponseOperationCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actualPayloadHash = Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(request.Fingerprint.CanonicalPayload.Json)));
        if (!string.Equals(
            actualPayloadHash,
            request.Fingerprint.PayloadHash.Value,
            StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Workflow external response fingerprint does not match its canonical payload.",
                nameof(request));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        using var mutationLease = await WorkflowPersistenceProvider.EnterInMemoryMutationAsync(
            dbContext,
            cancellationToken);
        await using var transaction = WorkflowPersistenceProvider.IsInMemory(dbContext)
            ? null
            : await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var externalRequest = await LockRequestAsync(dbContext, request.RequestId, cancellationToken);
        if (externalRequest is null)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return CreateResult(WorkflowExternalResponseOperationCreateOutcome.RequestNotFound);
        }

        var existing = await LockOperationByRequestAsync(
            dbContext,
            request.RequestId,
            cancellationToken);
        if (existing is not null)
        {
            var result = ResolveReplay(existing, request);
            if (result.Outcome == WorkflowExternalResponseOperationCreateOutcome.Replayed)
            {
                existing.ReplayCount++;
                existing.LastReplayedAtUtc = request.AcceptedAtUtc;
                await dbContext.SaveChangesAsync(cancellationToken);
                await WorkflowPersistenceProvider.CommitAsync(transaction, cancellationToken);
                return result with
                {
                    Operation = ToRecord(existing),
                    Replay = new WorkflowExternalResponseOperationReplay(
                        new WorkflowExternalResponseOperationId(existing.Id),
                        (WorkflowExternalResponseOperationState)existing.State,
                        DeserializeFinalResult(existing),
                        request.AcceptedAtUtc)
                };
            }

            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return result;
        }

        var run = await dbContext.Set<WorkflowRunRecordEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(record => record.RunId == request.RunId.Value, cancellationToken);
        if (run is null)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return CreateResult(WorkflowExternalResponseOperationCreateOutcome.RunNotFound);
        }

        if (externalRequest.RunId != request.RunId.Value)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return CreateResult(WorkflowExternalResponseOperationCreateOutcome.ActiveOperationConflict);
        }

        var boundary = await dbContext.Set<WorkflowExternalRequestBoundaryEntity>()
            .SingleOrDefaultAsync(
                current => current.RequestId == request.RequestId.Value,
                cancellationToken);
        if (boundary is null)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return CreateResult(WorkflowExternalResponseOperationCreateOutcome.LegacyNonResumable);
        }

        if (boundary.RequestVersion != request.ExpectedRequestVersion.Value)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return CreateResult(WorkflowExternalResponseOperationCreateOutcome.RequestVersionMismatch);
        }

        if ((WorkflowExternalRequestState)boundary.State != WorkflowExternalRequestState.Pending ||
            externalRequest.RespondedAtUtc.HasValue)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return CreateResult(WorkflowExternalResponseOperationCreateOutcome.RequestNotPending);
        }

        if (run.State != WorkflowRunState.WaitingForInput)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return CreateResult(WorkflowExternalResponseOperationCreateOutcome.RunNotWaiting);
        }

        var entity = ToEntity(request);
        dbContext.Set<WorkflowExternalResponseOperationEntity>().Add(entity);
        boundary.State = (int)WorkflowExternalRequestState.ResponseClaimed;
        await dbContext.SaveChangesAsync(cancellationToken);
        await WorkflowPersistenceProvider.CommitAsync(transaction, cancellationToken);
        return new WorkflowExternalResponseOperationCreateResult(
            WorkflowExternalResponseOperationCreateOutcome.Created,
            ToRecord(entity));
    }

    public async Task<WorkflowExternalResponseOperationRecord?> GetAsync(
        WorkflowExternalResponseOperationId operationId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await dbContext.Set<WorkflowExternalResponseOperationEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(operation => operation.Id == operationId.Value, cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<IReadOnlyList<WorkflowExternalResponseOperationRecord>> ListRecoverableAsync(
        DateTimeOffset asOfUtc,
        int maximumCount,
        CancellationToken cancellationToken = default)
    {
        if (maximumCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCount));
        }

        var accepted = (int)WorkflowExternalResponseOperationState.Accepted;
        var retryable = (int)WorkflowExternalResponseOperationState.FailedRetryable;
        var claimed = (int)WorkflowExternalResponseOperationState.Claimed;
        var resuming = (int)WorkflowExternalResponseOperationState.Resuming;
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await dbContext.Set<WorkflowExternalResponseOperationEntity>()
            .AsNoTracking()
            .Where(operation =>
                operation.State == accepted ||
                operation.State == retryable ||
                ((operation.State == claimed || operation.State == resuming) &&
                 (operation.LeaseExpiresAtUtc == null || operation.LeaseExpiresAtUtc <= asOfUtc)))
            .OrderBy(operation => operation.LeaseExpiresAtUtc ?? operation.AcceptedAtUtc)
            .ThenBy(operation => operation.AcceptedAtUtc)
            .ThenBy(operation => operation.Id)
            .Take(maximumCount)
            .ToArrayAsync(cancellationToken);
        return entities.Select(ToRecord).ToArray();
    }

    public async Task<WorkflowExternalResponseOperationClaimResult> TryClaimAsync(
        WorkflowExternalResponseOperationClaimRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.LeaseExpiresAtUtc <= request.ClaimedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Workflow external response lease must expire after it is claimed.");
        }

        if (request.MaximumAttempts <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        using var mutationLease = await WorkflowPersistenceProvider.EnterInMemoryMutationAsync(
            dbContext,
            cancellationToken);
        var isInMemory = WorkflowPersistenceProvider.IsInMemory(dbContext);
        await using var transaction = isInMemory
            ? null
            : await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var identity = await dbContext.Set<WorkflowExternalResponseOperationEntity>()
            .AsNoTracking()
            .Where(operation => operation.Id == request.OperationId.Value)
            .Select(operation => new { operation.RequestId, operation.RunId })
            .SingleOrDefaultAsync(cancellationToken);
        if (identity is null)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return new WorkflowExternalResponseOperationClaimResult(
                WorkflowExternalResponseOperationClaimOutcome.NotFound,
                Operation: null,
                Claim: null);
        }

        var externalRequest = await LockRequestAsync(
            dbContext,
            new WorkflowExternalRequestId(identity.RequestId),
            cancellationToken);
        var entity = await LockOperationAsync(dbContext, request.OperationId, cancellationToken);
        if (entity is null)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return new WorkflowExternalResponseOperationClaimResult(
                WorkflowExternalResponseOperationClaimOutcome.NotFound,
                Operation: null,
                Claim: null);
        }

        var boundary = await dbContext.Set<WorkflowExternalRequestBoundaryEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.RequestId == identity.RequestId, cancellationToken);
        var run = isInMemory
            ? await dbContext.Set<WorkflowRunRecordEntity>()
                .SingleOrDefaultAsync(item => item.RunId == identity.RunId, cancellationToken)
            : await dbContext.Set<WorkflowRunRecordEntity>()
                .FromSqlInterpolated(
                    $"""
                    SELECT *
                    FROM "AgentFramework_WorkflowRuns"
                    WHERE "RunId" = {identity.RunId}
                    FOR UPDATE
                    """)
                .SingleOrDefaultAsync(cancellationToken);
        if (externalRequest is null ||
            externalRequest.RespondedAtUtc.HasValue ||
            boundary is null ||
            (WorkflowExternalRequestState)boundary.State != WorkflowExternalRequestState.ResponseClaimed ||
            run?.State != WorkflowRunState.WaitingForInput)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return ClaimResult(WorkflowExternalResponseOperationClaimOutcome.InvalidState, entity);
        }

        if (entity.OperationVersion != request.ExpectedVersion.Value)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return ClaimResult(WorkflowExternalResponseOperationClaimOutcome.ConcurrencyConflict, entity);
        }

        var state = (WorkflowExternalResponseOperationState)entity.State;
        if (HasActiveLease(entity, request.ClaimedAtUtc))
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return ClaimResult(WorkflowExternalResponseOperationClaimOutcome.ActiveLease, entity);
        }

        if (!CanClaim(state))
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return ClaimResult(WorkflowExternalResponseOperationClaimOutcome.InvalidState, entity);
        }

        if (entity.Attempt >= request.MaximumAttempts)
        {
            entity.State = (int)WorkflowExternalResponseOperationState.FailedTerminal;
            entity.OperationVersion++;
            entity.OutcomeCode = (int)WorkflowExternalResponseOperationOutcomeCode.AttemptLimitReached;
            entity.SafeMessage = AttemptLimitReachedSafeMessage;
            entity.CompletedAtUtc = request.ClaimedAtUtc;
            ClearLease(entity);
            await dbContext.SaveChangesAsync(cancellationToken);
            await WorkflowPersistenceProvider.CommitAsync(transaction, cancellationToken);
            return ClaimResult(WorkflowExternalResponseOperationClaimOutcome.AttemptLimitReached, entity);
        }

        var claimed = (int)WorkflowExternalResponseOperationState.Claimed;
        var resuming = (int)WorkflowExternalResponseOperationState.Resuming;
        var anotherActiveRunClaim = await dbContext.Set<WorkflowExternalResponseOperationEntity>()
            .AsNoTracking()
            .AnyAsync(
                operation =>
                    operation.Id != entity.Id &&
                    operation.RunId == entity.RunId &&
                    (operation.State == claimed || operation.State == resuming) &&
                    operation.LeaseExpiresAtUtc > request.ClaimedAtUtc,
                cancellationToken);
        if (anotherActiveRunClaim)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return ClaimResult(WorkflowExternalResponseOperationClaimOutcome.ActiveLease, entity);
        }

        var recovery = state is WorkflowExternalResponseOperationState.Claimed or
            WorkflowExternalResponseOperationState.Resuming
            ? WorkflowExternalResponseOperationRecoveryRules.CreateExpiredLeaseRecovery(state)
            : null;
        if (state == WorkflowExternalResponseOperationState.Resuming)
        {
            entity.State = (int)WorkflowExternalResponseOperationState.FailedRetryable;
        }

        entity.State = (int)WorkflowExternalResponseOperationState.Claimed;
        entity.Attempt++;
        entity.OperationVersion++;
        entity.LeaseOwnerId = request.LeaseOwnerId.Value;
        entity.LeaseEpoch++;
        entity.LeaseAcquiredAtUtc = request.ClaimedAtUtc;
        entity.LeaseExpiresAtUtc = request.LeaseExpiresAtUtc;
        entity.StartedAtUtc = null;
        await dbContext.SaveChangesAsync(cancellationToken);
        await WorkflowPersistenceProvider.CommitAsync(transaction, cancellationToken);

        var operation = ToRecord(entity);
        var lease = operation.Lease
            ?? throw new InvalidOperationException("A claimed workflow response operation must have a lease.");
        return new WorkflowExternalResponseOperationClaimResult(
            WorkflowExternalResponseOperationClaimOutcome.Claimed,
            operation,
            new WorkflowExternalResponseOperationClaim(
                operation.Id,
                lease,
                operation.Attempt,
                operation.ConcurrencyVersion)
            {
                Recovery = recovery
            });
    }

    public Task<WorkflowExternalResponseOperationMutationResult> TryRenewLeaseAsync(
        WorkflowExternalResponseOperationLeaseRenewalRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.LeaseExpiresAtUtc <= request.RenewedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Renewed workflow response lease must expire after the renewal time.");
        }

        return MutateWithLeaseAsync(
            request.OperationId,
            request.ExpectedVersion,
            request.LeaseOwnerId,
            request.LeaseEpoch,
            request.RenewedAtUtc,
            [
                WorkflowExternalResponseOperationState.Claimed,
                WorkflowExternalResponseOperationState.Resuming
            ],
            entity => entity.LeaseExpiresAtUtc = request.LeaseExpiresAtUtc,
            cancellationToken);
    }

    public Task<WorkflowExternalResponseOperationMutationResult> TryMarkResumingAsync(
        WorkflowExternalResponseOperationMarkResumingRequest request,
        CancellationToken cancellationToken = default)
        => MutateWithLeaseAsync(
            request.OperationId,
            request.ExpectedVersion,
            request.LeaseOwnerId,
            request.LeaseEpoch,
            request.StartedAtUtc,
            [WorkflowExternalResponseOperationState.Claimed],
            entity =>
            {
                entity.State = (int)WorkflowExternalResponseOperationState.Resuming;
                entity.StartedAtUtc = request.StartedAtUtc;
            },
            cancellationToken);

    public Task<WorkflowExternalResponseOperationMutationResult> TryCompleteAsync(
        WorkflowExternalResponseOperationCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        return MutateWithLeaseAsync(
            request.OperationId,
            request.ExpectedVersion,
            request.LeaseOwnerId,
            request.LeaseEpoch,
            request.CompletedAtUtc,
            [WorkflowExternalResponseOperationState.Resuming],
            entity =>
            {
                if (!WorkflowExternalResponseOperationTransitionRules.CanTransition(
                    (WorkflowExternalResponseOperationState)entity.State,
                    request.FinalResult.State))
                {
                    throw new WorkflowExternalResponseInvalidTransitionException();
                }

                entity.State = (int)request.FinalResult.State;
                entity.OutcomeCode = (int)request.FinalResult.OutcomeCode;
                entity.SafeMessage = request.FinalResult.SafeMessage;
                entity.FinalResultJson = JsonSerializer.Serialize(request.FinalResult, JsonOptions);
                entity.CompletedAtUtc = request.CompletedAtUtc;
                ClearLease(entity);
            },
            cancellationToken);
    }

    public Task<WorkflowExternalResponseOperationMutationResult> TryFailAsync(
        WorkflowExternalResponseOperationFailureRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.FailureState is not (
            WorkflowExternalResponseOperationState.FailedRetryable or
            WorkflowExternalResponseOperationState.FailedTerminal))
        {
            return Task.FromResult(MutationResult(
                WorkflowExternalResponseOperationMutationOutcome.InvalidTransition));
        }

        return MutateWithLeaseAsync(
            request.OperationId,
            request.ExpectedVersion,
            request.LeaseOwnerId,
            request.LeaseEpoch,
            request.FailedAtUtc,
            [
                WorkflowExternalResponseOperationState.Claimed,
                WorkflowExternalResponseOperationState.Resuming
            ],
            entity =>
            {
                if (!WorkflowExternalResponseOperationTransitionRules.CanTransition(
                    (WorkflowExternalResponseOperationState)entity.State,
                    request.FailureState))
                {
                    throw new WorkflowExternalResponseInvalidTransitionException();
                }

                entity.State = (int)request.FailureState;
                entity.OutcomeCode = (int)request.OutcomeCode;
                entity.SafeMessage = request.SafeMessage;
                entity.CompletedAtUtc = request.FailureState == WorkflowExternalResponseOperationState.FailedRetryable
                    ? null
                    : request.FailedAtUtc;
                ClearLease(entity);
            },
            cancellationToken);
    }

    public Task<WorkflowExternalResponseOperationMutationResult> TryReleaseLeaseAsync(
        WorkflowExternalResponseOperationLeaseReleaseRequest request,
        CancellationToken cancellationToken = default)
        => MutateWithLeaseAsync(
            request.OperationId,
            request.ExpectedVersion,
            request.LeaseOwnerId,
            request.LeaseEpoch,
            request.ReleasedAtUtc,
            [
                WorkflowExternalResponseOperationState.Claimed,
                WorkflowExternalResponseOperationState.Resuming
            ],
            ClearLease,
            cancellationToken,
            requireUnexpiredLease: false);

    private async Task<WorkflowExternalResponseOperationMutationResult> MutateWithLeaseAsync(
        WorkflowExternalResponseOperationId operationId,
        WorkflowExternalResponseOperationConcurrencyVersion expectedVersion,
        WorkflowExternalResponseLeaseOwnerId leaseOwnerId,
        WorkflowExternalResponseLeaseEpoch leaseEpoch,
        DateTimeOffset operationAtUtc,
        IReadOnlyCollection<WorkflowExternalResponseOperationState> allowedStates,
        Action<WorkflowExternalResponseOperationEntity> mutation,
        CancellationToken cancellationToken,
        bool requireUnexpiredLease = true)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        using var mutationLease = await WorkflowPersistenceProvider.EnterInMemoryMutationAsync(
            dbContext,
            cancellationToken);
        await using var transaction = WorkflowPersistenceProvider.IsInMemory(dbContext)
            ? null
            : await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var entity = await LockOperationAsync(dbContext, operationId, cancellationToken);
        if (entity is null)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return MutationResult(WorkflowExternalResponseOperationMutationOutcome.NotFound);
        }

        if (entity.OperationVersion != expectedVersion.Value)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return MutationResult(WorkflowExternalResponseOperationMutationOutcome.ConcurrencyConflict, entity);
        }

        if (!string.Equals(entity.LeaseOwnerId, leaseOwnerId.Value, StringComparison.Ordinal) ||
            entity.LeaseEpoch != leaseEpoch.Value)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return MutationResult(WorkflowExternalResponseOperationMutationOutcome.LeaseConflict, entity);
        }

        if (entity.LeaseExpiresAtUtc is not { } leaseExpiresAtUtc ||
            requireUnexpiredLease && leaseExpiresAtUtc <= operationAtUtc)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return MutationResult(WorkflowExternalResponseOperationMutationOutcome.LeaseExpired, entity);
        }

        if (!allowedStates.Contains((WorkflowExternalResponseOperationState)entity.State))
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return MutationResult(WorkflowExternalResponseOperationMutationOutcome.InvalidTransition, entity);
        }

        try
        {
            mutation(entity);
        }
        catch (WorkflowExternalResponseInvalidTransitionException)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return MutationResult(WorkflowExternalResponseOperationMutationOutcome.InvalidTransition, entity);
        }
        entity.OperationVersion++;
        await dbContext.SaveChangesAsync(cancellationToken);
        await WorkflowPersistenceProvider.CommitAsync(transaction, cancellationToken);
        return MutationResult(WorkflowExternalResponseOperationMutationOutcome.Updated, entity);
    }

    private static Task<WorkflowExternalRequestRecordEntity?> LockRequestAsync(
        AppDbContext dbContext,
        WorkflowExternalRequestId requestId,
        CancellationToken cancellationToken)
        => WorkflowPersistenceProvider.IsInMemory(dbContext)
            ? dbContext.Set<WorkflowExternalRequestRecordEntity>()
                .SingleOrDefaultAsync(item => item.Id == requestId.Value, cancellationToken)
            : dbContext.Set<WorkflowExternalRequestRecordEntity>()
                .FromSqlInterpolated(
                    $"""
                    SELECT *
                    FROM "AgentFramework_WorkflowExternalRequests"
                    WHERE "Id" = {requestId.Value}
                    FOR UPDATE
                    """)
                .SingleOrDefaultAsync(cancellationToken);

    private static Task<WorkflowExternalResponseOperationEntity?> LockOperationByRequestAsync(
        AppDbContext dbContext,
        WorkflowExternalRequestId requestId,
        CancellationToken cancellationToken)
        => WorkflowPersistenceProvider.IsInMemory(dbContext)
            ? dbContext.Set<WorkflowExternalResponseOperationEntity>()
                .SingleOrDefaultAsync(item => item.RequestId == requestId.Value, cancellationToken)
            : dbContext.Set<WorkflowExternalResponseOperationEntity>()
                .FromSqlInterpolated(
                    $"""
                    SELECT *
                    FROM "AgentFramework_WorkflowExternalResponseOperations"
                    WHERE "RequestId" = {requestId.Value}
                    FOR UPDATE
                    """)
                .SingleOrDefaultAsync(cancellationToken);

    internal static Task<WorkflowExternalResponseOperationEntity?> LockOperationAsync(
        AppDbContext dbContext,
        WorkflowExternalResponseOperationId operationId,
        CancellationToken cancellationToken)
        => WorkflowPersistenceProvider.IsInMemory(dbContext)
            ? dbContext.Set<WorkflowExternalResponseOperationEntity>()
                .SingleOrDefaultAsync(item => item.Id == operationId.Value, cancellationToken)
            : dbContext.Set<WorkflowExternalResponseOperationEntity>()
                .FromSqlInterpolated(
                    $"""
                    SELECT *
                    FROM "AgentFramework_WorkflowExternalResponseOperations"
                    WHERE "Id" = {operationId.Value}
                    FOR UPDATE
                    """)
                .SingleOrDefaultAsync(cancellationToken);

    internal WorkflowExternalResponseOperationRecord ToRecord(
        WorkflowExternalResponseOperationEntity entity)
    {
        var payloadJson = UnprotectAndValidateResponse(responseProtector, entity);

        var record = new WorkflowExternalResponseOperationRecord(
            new WorkflowExternalResponseOperationId(entity.Id),
            new WorkflowExternalRequestId(entity.RequestId),
            new WorkflowRunId(entity.RunId),
            new WorkflowExternalRequestVersion(entity.ExpectedRequestVersion),
            new WorkflowExternalResponseIdempotencyKeyHash(entity.IdempotencyKeyHash),
            new WorkflowExternalResponsePayloadHash(entity.ResponsePayloadHash),
            new WorkflowExternalResponseActorScopeFingerprint(entity.ActorScopeFingerprint),
            new WorkflowExternalResponsePayload(payloadJson),
            new WorkflowLaunchActor(
                (WorkflowLaunchActorKind)entity.ActorKind,
                entity.ActorSubjectId),
            new WorkflowLaunchCorrelationId(entity.CorrelationId),
            (WorkflowExternalResponseOperationState)entity.State,
            entity.Attempt,
            new WorkflowExternalResponseOperationConcurrencyVersion(entity.OperationVersion),
            entity.AcceptedAtUtc)
        {
            Lease = ToLease(entity),
            StartedAtUtc = entity.StartedAtUtc,
            CompletedAtUtc = entity.CompletedAtUtc,
            OutcomeCode = (WorkflowExternalResponseOperationOutcomeCode)entity.OutcomeCode,
            SafeMessage = entity.SafeMessage,
            FinalResult = DeserializeFinalResult(entity)
        };
        return record;
    }

    internal static string UnprotectAndValidateResponse(
        IDataProtector protector,
        WorkflowExternalResponseOperationEntity entity)
    {
        try
        {
            var payloadJson = protector.Unprotect(entity.ProtectedResponsePayload);
            var actualHash = SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson));
            var expectedHash = Convert.FromHexString(entity.ResponsePayloadHash);
            if (actualHash.Length != expectedHash.Length ||
                !CryptographicOperations.FixedTimeEquals(actualHash, expectedHash))
            {
                throw new WorkflowExternalResponsePayloadCorruptException(entity.Id);
            }

            return payloadJson;
        }
        catch (WorkflowExternalResponsePayloadCorruptException)
        {
            throw;
        }
        catch (Exception exception) when (exception is
            CryptographicException or
            FormatException or
            ArgumentException)
        {
            throw new WorkflowExternalResponsePayloadCorruptException(entity.Id, exception);
        }
    }

    private WorkflowExternalResponseOperationEntity ToEntity(
        WorkflowExternalResponseOperationCreateRequest request)
        => new()
        {
            Id = request.OperationId.Value,
            RequestId = request.RequestId.Value,
            RunId = request.RunId.Value,
            ExpectedRequestVersion = request.ExpectedRequestVersion.Value,
            IdempotencyKeyHash = request.Fingerprint.IdempotencyKeyHash.Value,
            ResponsePayloadHash = request.Fingerprint.PayloadHash.Value,
            ActorScopeFingerprint = request.Fingerprint.ActorScopeFingerprint.Value,
            ProtectedResponsePayload = responseProtector.Protect(request.Fingerprint.CanonicalPayload.Json),
            ActorKind = (int)request.Actor.Kind,
            ActorSubjectId = request.Actor.SubjectId,
            CorrelationId = request.CorrelationId.Value,
            State = (int)WorkflowExternalResponseOperationState.Accepted,
            OperationVersion = WorkflowExternalResponseOperationConcurrencyVersion.Initial.Value,
            AcceptedAtUtc = request.AcceptedAtUtc,
            OutcomeCode = (int)WorkflowExternalResponseOperationOutcomeCode.None
        };

    private WorkflowExternalResponseOperationCreateResult ResolveReplay(
        WorkflowExternalResponseOperationEntity existing,
        WorkflowExternalResponseOperationCreateRequest request)
    {
        var operation = ToRecord(existing);
        if (!string.Equals(
                existing.IdempotencyKeyHash,
                request.Fingerprint.IdempotencyKeyHash.Value,
                StringComparison.Ordinal) ||
            !string.Equals(
                existing.ActorScopeFingerprint,
                request.Fingerprint.ActorScopeFingerprint.Value,
                StringComparison.Ordinal))
        {
            return new WorkflowExternalResponseOperationCreateResult(
                WorkflowExternalResponseOperationCreateOutcome.ActiveOperationConflict,
                operation);
        }

        if (!string.Equals(
            existing.ResponsePayloadHash,
            request.Fingerprint.PayloadHash.Value,
            StringComparison.Ordinal))
        {
            return new WorkflowExternalResponseOperationCreateResult(
                WorkflowExternalResponseOperationCreateOutcome.IdempotencyConflict,
                operation);
        }

        return new WorkflowExternalResponseOperationCreateResult(
            WorkflowExternalResponseOperationCreateOutcome.Replayed,
            operation,
            new WorkflowExternalResponseOperationReplay(
                operation.Id,
                operation.State,
                operation.FinalResult,
                request.AcceptedAtUtc));
    }

    private static WorkflowExternalResponseLease? ToLease(
        WorkflowExternalResponseOperationEntity entity)
    {
        if (string.IsNullOrWhiteSpace(entity.LeaseOwnerId))
        {
            return null;
        }

        if (entity.LeaseEpoch <= 0 ||
            entity.LeaseAcquiredAtUtc is not { } acquiredAtUtc ||
            entity.LeaseExpiresAtUtc is not { } expiresAtUtc)
        {
            throw new InvalidOperationException(
                $"Workflow external response operation '{entity.Id}' has incomplete lease data.");
        }

        return new WorkflowExternalResponseLease(
            new WorkflowExternalResponseLeaseOwnerId(entity.LeaseOwnerId),
            new WorkflowExternalResponseLeaseEpoch(entity.LeaseEpoch),
            acquiredAtUtc,
            expiresAtUtc);
    }

    internal static WorkflowExternalResponseOperationFinalResult? DeserializeFinalResult(
        WorkflowExternalResponseOperationEntity entity)
        => string.IsNullOrWhiteSpace(entity.FinalResultJson)
            ? null
            : JsonSerializer.Deserialize<WorkflowExternalResponseOperationFinalResult>(
                entity.FinalResultJson,
                JsonOptions)
              ?? throw new InvalidOperationException(
                  $"Workflow external response operation '{entity.Id}' has an invalid final result.");

    private static bool HasActiveLease(
        WorkflowExternalResponseOperationEntity entity,
        DateTimeOffset atUtc)
        => !string.IsNullOrWhiteSpace(entity.LeaseOwnerId) &&
           entity.LeaseExpiresAtUtc > atUtc;

    private static bool CanClaim(WorkflowExternalResponseOperationState state)
        => state is WorkflowExternalResponseOperationState.Accepted or
            WorkflowExternalResponseOperationState.FailedRetryable or
            WorkflowExternalResponseOperationState.Claimed or
            WorkflowExternalResponseOperationState.Resuming;

    internal static void ClearLease(WorkflowExternalResponseOperationEntity entity)
    {
        entity.LeaseOwnerId = null;
        entity.LeaseAcquiredAtUtc = null;
        entity.LeaseExpiresAtUtc = null;
    }

    private static WorkflowExternalResponseOperationCreateResult CreateResult(
        WorkflowExternalResponseOperationCreateOutcome outcome)
        => new(outcome, Operation: null);

    private WorkflowExternalResponseOperationClaimResult ClaimResult(
        WorkflowExternalResponseOperationClaimOutcome outcome,
        WorkflowExternalResponseOperationEntity entity)
        => new(outcome, ToRecord(entity), Claim: null);

    private static WorkflowExternalResponseOperationMutationResult MutationResult(
        WorkflowExternalResponseOperationMutationOutcome outcome)
        => new(outcome, Operation: null);

    private WorkflowExternalResponseOperationMutationResult MutationResult(
        WorkflowExternalResponseOperationMutationOutcome outcome,
        WorkflowExternalResponseOperationEntity entity)
        => new(outcome, ToRecord(entity));

    private sealed class WorkflowExternalResponseInvalidTransitionException : Exception
    {
    }
}
