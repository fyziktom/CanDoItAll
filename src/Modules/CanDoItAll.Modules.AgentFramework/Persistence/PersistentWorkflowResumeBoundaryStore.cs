using System.Security.Cryptography;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class PersistentWorkflowResumeBoundaryStore : IWorkflowResumeBoundaryStore
{
    private readonly IDbContextFactory<AppDbContext> dbContextFactory;
    private readonly IDataProtector responseProtector;
    private readonly IDataProtector checkpointPayloadProtector;
    private readonly WorkflowHistoryProjection historyProjection;

    public PersistentWorkflowResumeBoundaryStore(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IDataProtectionProvider dataProtectionProvider,
        WorkflowHistoryProjection historyProjection)
    {
        this.dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        this.historyProjection = historyProjection ?? throw new ArgumentNullException(nameof(historyProjection));
        responseProtector = dataProtectionProvider.CreateProtector(
            PersistentWorkflowExternalResponseOperationStore.DataProtectionPurpose);
        checkpointPayloadProtector = dataProtectionProvider.CreateProtector(
            PersistentWorkflowBackendCheckpointPayloadStore.DataProtectionPurpose);
    }

    public async Task<WorkflowResumeBoundaryLoadResult> LoadAsync(
        WorkflowResumeBoundaryLoadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var operationEntity = await dbContext.Set<WorkflowExternalResponseOperationEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(operation => operation.Id == request.OperationId.Value, cancellationToken);
        if (operationEntity is null)
        {
            return LoadResult(WorkflowResumeBoundaryLoadOutcome.OperationNotFound);
        }

        var runEntity = await dbContext.Set<WorkflowRunRecordEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(run => run.RunId == operationEntity.RunId, cancellationToken);
        if (runEntity is null)
        {
            return LoadResult(WorkflowResumeBoundaryLoadOutcome.RunNotFound);
        }

        var requestEntity = await dbContext.Set<WorkflowExternalRequestRecordEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == operationEntity.RequestId, cancellationToken);
        if (requestEntity is null)
        {
            return LoadResult(WorkflowResumeBoundaryLoadOutcome.RequestNotFound);
        }

        var boundaryEntity = await dbContext.Set<WorkflowExternalRequestBoundaryEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(boundary => boundary.RequestId == operationEntity.RequestId, cancellationToken);
        if (boundaryEntity is null)
        {
            return LoadResult(WorkflowResumeBoundaryLoadOutcome.LegacyNonResumable);
        }

        WorkflowExternalRequestBoundaryRecord boundary;
        try
        {
            boundary = PersistentWorkflowExternalRequestBoundaryStore.ToRecord(boundaryEntity);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return LoadResult(WorkflowResumeBoundaryLoadOutcome.LinkageMismatch);
        }

        if (operationEntity.RunId != requestEntity.RunId ||
            runEntity.RunId != requestEntity.RunId ||
            operationEntity.ExpectedRequestVersion != boundaryEntity.RequestVersion)
        {
            return LoadResult(WorkflowResumeBoundaryLoadOutcome.LinkageMismatch);
        }

        var boundaryOutcome = await ClassifyResumableBoundaryAsync(
            dbContext,
            runEntity,
            requestEntity,
            boundary,
            cancellationToken);
        if (boundaryOutcome != WorkflowResumeBoundaryLoadOutcome.Found)
        {
            return LoadResult(boundaryOutcome);
        }

        if ((WorkflowExternalRequestState)boundaryEntity.State != WorkflowExternalRequestState.ResponseClaimed ||
            requestEntity.RespondedAtUtc.HasValue)
        {
            return LoadResult(WorkflowResumeBoundaryLoadOutcome.RequestNotPending);
        }

        if (runEntity.State != WorkflowRunState.WaitingForInput)
        {
            return LoadResult(WorkflowResumeBoundaryLoadOutcome.RunNotWaiting);
        }

        var operation = ToOperationRecord(operationEntity, UnprotectResponse(operationEntity));
        return new WorkflowResumeBoundaryLoadResult(
            WorkflowResumeBoundaryLoadOutcome.Found,
            new WorkflowResumableExternalRequestContext(
                operation,
                runEntity.ToSnapshot(),
                HydrateRequest(requestEntity, boundary) with
                {
                    State = WorkflowExternalRequestState.Pending
                },
                boundary));
    }

    public async Task<WorkflowResumeBoundaryCommitResult> TryCommitAsync(
        WorkflowResumeBoundaryCommitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        using var mutationLease = await WorkflowPersistenceProvider.EnterInMemoryMutationAsync(
            dbContext,
            cancellationToken);
        var isInMemory = WorkflowPersistenceProvider.IsInMemory(dbContext);
        await using var transaction = isInMemory
            ? null
            : await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var operationIdentity = await dbContext.Set<WorkflowExternalResponseOperationEntity>()
            .AsNoTracking()
            .Where(item => item.Id == request.OperationId.Value)
            .Select(item => new { item.Id, item.RequestId })
            .SingleOrDefaultAsync(cancellationToken);
        if (operationIdentity is null)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return CommitResult(WorkflowResumeBoundaryCommitOutcome.OperationNotFound);
        }

        var sourceRequest = isInMemory
            ? await dbContext.Set<WorkflowExternalRequestRecordEntity>()
                .SingleOrDefaultAsync(item => item.Id == operationIdentity.RequestId, cancellationToken)
            : await dbContext.Set<WorkflowExternalRequestRecordEntity>()
                .FromSqlInterpolated(
                    $"""
                    SELECT *
                    FROM "AgentFramework_WorkflowExternalRequests"
                    WHERE "Id" = {operationIdentity.RequestId}
                    FOR UPDATE
                    """)
                .SingleOrDefaultAsync(cancellationToken);
        if (sourceRequest is null)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return CommitResult(WorkflowResumeBoundaryCommitOutcome.RequestNotFound);
        }

        var operation = await PersistentWorkflowExternalResponseOperationStore.LockOperationAsync(
            dbContext,
            request.OperationId,
            cancellationToken);
        if (operation is null)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return CommitResult(WorkflowResumeBoundaryCommitOutcome.OperationNotFound);
        }

        var sourceRun = isInMemory
            ? await dbContext.Set<WorkflowRunRecordEntity>()
                .SingleOrDefaultAsync(item => item.RunId == operation.RunId, cancellationToken)
            : await dbContext.Set<WorkflowRunRecordEntity>()
                .FromSqlInterpolated(
                    $"""
                    SELECT *
                    FROM "AgentFramework_WorkflowRuns"
                    WHERE "RunId" = {operation.RunId}
                    FOR UPDATE
                    """)
                .SingleOrDefaultAsync(cancellationToken);
        if (sourceRun is null)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return CommitResult(WorkflowResumeBoundaryCommitOutcome.RunNotFound);
        }

        var boundary = await dbContext.Set<WorkflowExternalRequestBoundaryEntity>()
            .SingleOrDefaultAsync(item => item.RequestId == operation.RequestId, cancellationToken);
        if (boundary is null || boundary.RequestVersion != request.ExpectedRequestVersion.Value)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return CommitResult(WorkflowResumeBoundaryCommitOutcome.RequestVersionConflict);
        }

        var guardOutcome = ValidateCommitGuard(operation, boundary, sourceRun, sourceRequest, request);
        if (guardOutcome.HasValue)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return CommitResult(guardOutcome.Value);
        }

        WorkflowExternalRequestBoundaryRecord sourceBoundary;
        try
        {
            sourceBoundary = PersistentWorkflowExternalRequestBoundaryStore.ToRecord(boundary);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return CommitResult(WorkflowResumeBoundaryCommitOutcome.InvalidResultBoundary);
        }

        if (await ClassifyResumableBoundaryAsync(
                dbContext,
                sourceRun,
                sourceRequest,
                sourceBoundary,
                cancellationToken) != WorkflowResumeBoundaryLoadOutcome.Found)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return CommitResult(WorkflowResumeBoundaryCommitOutcome.InvalidResultBoundary);
        }

        if (!WorkflowResumeBoundaryPersistenceValidator.IsValid(
            operation,
            sourceRun,
            sourceRequest,
            request))
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return CommitResult(WorkflowResumeBoundaryCommitOutcome.InvalidResultBoundary);
        }

        foreach (var externalRequest in request.BackendResult.ExternalRequests)
        {
            if (!WorkflowExternalRequestBoundaryRecord.TryCreate(externalRequest, out var nextBoundary) ||
                nextBoundary is null)
            {
                await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
                return CommitResult(WorkflowResumeBoundaryCommitOutcome.InvalidResultBoundary);
            }

            var linkOutcome = await WorkflowNativeCheckpointRequestLinker.LinkAsync(
                dbContext,
                nextBoundary,
                request.BackendResult.Run.RunId,
                request.BackendResult.Run.WorkflowId,
                request.BackendResult.Run.VersionId,
                request.BackendResult.Run.Backend,
                cancellationToken);
            if (linkOutcome is not (
                WorkflowNativeCheckpointRequestLinkOutcome.Linked or
                WorkflowNativeCheckpointRequestLinkOutcome.AlreadyLinked))
            {
                await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
                return CommitResult(WorkflowResumeBoundaryCommitOutcome.InvalidResultBoundary);
            }
        }

        if (!await NativeCheckpointLinksExistAsync(dbContext, request.BackendResult, cancellationToken))
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return CommitResult(WorkflowResumeBoundaryCommitOutcome.InvalidResultBoundary);
        }

        var responseJson = UnprotectResponse(operation);
        sourceRequest.ResponseJson = string.Empty;
        sourceRequest.RespondedAtUtc = request.CommittedAtUtc;
        boundary.State = (int)MapRequestState(request.FinalResult.State);
        UpdateRun(sourceRun, request.BackendResult.Run, request.CommittedAtUtc);
        await MarkSourceCheckpointResumedAsync(
            dbContext,
            sourceRequest.Id,
            request.CommittedAtUtc,
            cancellationToken);
        await AddBackendResultRecordsAsync(dbContext, request.BackendResult, cancellationToken);
        CompleteOperation(operation, request.FinalResult, request.CommittedAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
        await WorkflowPersistenceProvider.CommitAsync(transaction, cancellationToken);

        var nextRequest = request.BackendResult.ExternalRequests.SingleOrDefault();
        return new WorkflowResumeBoundaryCommitResult(
            WorkflowResumeBoundaryCommitOutcome.Committed,
            ToOperationRecord(operation, responseJson),
            sourceRun.ToSnapshot(),
            nextRequest);
    }

    public async Task<WorkflowResumeBoundaryCancellationResult> TryCancelAsync(
        WorkflowResumeBoundaryCancellationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        using var mutationLease = await WorkflowPersistenceProvider.EnterInMemoryMutationAsync(
            dbContext,
            cancellationToken);
        var isInMemory = WorkflowPersistenceProvider.IsInMemory(dbContext);
        await using var transaction = isInMemory
            ? null
            : await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var requestEntity = isInMemory
            ? await dbContext.Set<WorkflowExternalRequestRecordEntity>()
                .SingleOrDefaultAsync(item => item.Id == request.RequestId.Value, cancellationToken)
            : await dbContext.Set<WorkflowExternalRequestRecordEntity>()
                .FromSqlInterpolated(
                    $"""
                    SELECT *
                    FROM "AgentFramework_WorkflowExternalRequests"
                    WHERE "Id" = {request.RequestId.Value}
                    FOR UPDATE
                    """)
                .SingleOrDefaultAsync(cancellationToken);
        if (requestEntity is null)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return CancellationResult(WorkflowResumeBoundaryCancellationOutcome.RequestNotFound);
        }

        var operation = isInMemory
            ? await dbContext.Set<WorkflowExternalResponseOperationEntity>()
                .SingleOrDefaultAsync(item => item.RequestId == request.RequestId.Value, cancellationToken)
            : await dbContext.Set<WorkflowExternalResponseOperationEntity>()
                .FromSqlInterpolated(
                    $"""
                    SELECT *
                    FROM "AgentFramework_WorkflowExternalResponseOperations"
                    WHERE "RequestId" = {request.RequestId.Value}
                    FOR UPDATE
                    """)
                .SingleOrDefaultAsync(cancellationToken);
        var run = isInMemory
            ? await dbContext.Set<WorkflowRunRecordEntity>()
                .SingleOrDefaultAsync(item => item.RunId == request.RunId.Value, cancellationToken)
            : await dbContext.Set<WorkflowRunRecordEntity>()
                .FromSqlInterpolated(
                    $"""
                    SELECT *
                    FROM "AgentFramework_WorkflowRuns"
                    WHERE "RunId" = {request.RunId.Value}
                    FOR UPDATE
                    """)
                .SingleOrDefaultAsync(cancellationToken);
        if (run is null || requestEntity.RunId != run.RunId)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return CancellationResult(WorkflowResumeBoundaryCancellationOutcome.RunNotFound);
        }

        var boundary = await dbContext.Set<WorkflowExternalRequestBoundaryEntity>()
            .SingleOrDefaultAsync(item => item.RequestId == request.RequestId.Value, cancellationToken);
        if (boundary is null || boundary.RequestVersion != request.ExpectedRequestVersion.Value)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return CancellationResult(WorkflowResumeBoundaryCancellationOutcome.RequestVersionConflict);
        }

        if (operation is not null &&
            (operation.State == (int)WorkflowExternalResponseOperationState.Claimed ||
             operation.State == (int)WorkflowExternalResponseOperationState.Resuming) &&
            operation.LeaseExpiresAtUtc > request.CancelledAtUtc)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return CancellationResult(
                WorkflowResumeBoundaryCancellationOutcome.ActiveResume,
                run,
                requestEntity,
                boundary,
                operation);
        }

        if (run.State is WorkflowRunState.Completed or WorkflowRunState.Failed or WorkflowRunState.Cancelled)
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return CancellationResult(
                WorkflowResumeBoundaryCancellationOutcome.AlreadyTerminal,
                run,
                requestEntity,
                boundary,
                operation);
        }

        var preserveTerminalFailure = operation?.State ==
            (int)WorkflowExternalResponseOperationState.FailedTerminal;
        if (operation is not null &&
            !preserveTerminalFailure &&
            !WorkflowExternalResponseOperationTransitionRules.CanTransition(
                (WorkflowExternalResponseOperationState)operation.State,
                WorkflowExternalResponseOperationState.Cancelled))
        {
            await WorkflowPersistenceProvider.RollbackAsync(transaction, cancellationToken);
            return CancellationResult(
                WorkflowResumeBoundaryCancellationOutcome.AlreadyTerminal,
                run,
                requestEntity,
                boundary,
                operation);
        }

        run.State = WorkflowRunState.Cancelled;
        run.Summary = request.SafeReason;
        run.UpdatedAtUtc = request.CancelledAtUtc;
        run.TerminalAtUtc = request.CancelledAtUtc;
        requestEntity.RespondedAtUtc = request.CancelledAtUtc;
        boundary.State = (int)WorkflowExternalRequestState.Cancelled;
        if (operation is not null && !preserveTerminalFailure)
        {
            operation.State = (int)WorkflowExternalResponseOperationState.Cancelled;
            operation.OperationVersion++;
            operation.OutcomeCode = (int)WorkflowExternalResponseOperationOutcomeCode.Cancelled;
            operation.SafeMessage = request.SafeReason;
            operation.CompletedAtUtc = request.CancelledAtUtc;
            PersistentWorkflowExternalResponseOperationStore.ClearLease(operation);
        }

        dbContext.Set<WorkflowEventRecordEntity>().Add(new WorkflowEventRecordEntity
        {
            Id = Guid.NewGuid(),
            RunId = run.RunId,
            Kind = WorkflowEventKind.Cancelled,
            Message = request.SafeReason,
            PayloadJson = "{}",
            CreatedAtUtc = request.CancelledAtUtc
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await WorkflowPersistenceProvider.CommitAsync(transaction, cancellationToken);
        return CancellationResult(
            WorkflowResumeBoundaryCancellationOutcome.Cancelled,
            run,
            requestEntity,
            boundary,
            operation);
    }

    private static WorkflowResumeBoundaryCommitOutcome? ValidateCommitGuard(
        WorkflowExternalResponseOperationEntity operation,
        WorkflowExternalRequestBoundaryEntity boundary,
        WorkflowRunRecordEntity run,
        WorkflowExternalRequestRecordEntity externalRequest,
        WorkflowResumeBoundaryCommitRequest request)
    {
        if (operation.OperationVersion != request.ExpectedOperationVersion.Value)
        {
            return WorkflowResumeBoundaryCommitOutcome.ConcurrencyConflict;
        }

        if (!string.Equals(operation.LeaseOwnerId, request.LeaseOwnerId.Value, StringComparison.Ordinal) ||
            operation.LeaseEpoch != request.LeaseEpoch.Value ||
            operation.LeaseExpiresAtUtc <= request.CommittedAtUtc ||
            operation.State != (int)WorkflowExternalResponseOperationState.Resuming)
        {
            return WorkflowResumeBoundaryCommitOutcome.LeaseConflict;
        }

        if ((WorkflowExternalRequestState)boundary.State == WorkflowExternalRequestState.Cancelled ||
            run.State == WorkflowRunState.Cancelled)
        {
            return WorkflowResumeBoundaryCommitOutcome.CancellationWon;
        }

        return (WorkflowExternalRequestState)boundary.State != WorkflowExternalRequestState.ResponseClaimed ||
               externalRequest.RespondedAtUtc.HasValue ||
               run.State != WorkflowRunState.WaitingForInput
            ? WorkflowResumeBoundaryCommitOutcome.InvalidResultBoundary
            : null;
    }

    private static async Task<bool> NativeCheckpointLinksExistAsync(
        AppDbContext dbContext,
        WorkflowBackendStartResult result,
        CancellationToken cancellationToken)
    {
        foreach (var externalRequest in result.ExternalRequests)
        {
            var continuation = externalRequest.Continuation!;
            var nativeCheckpoint = await dbContext.Set<WorkflowBackendCheckpointPayloadEntity>()
                .SingleOrDefaultAsync(
                    checkpoint =>
                        checkpoint.Id == continuation.Checkpoint.CheckpointId.Value &&
                        checkpoint.SessionId == continuation.Checkpoint.SessionId.Value,
                    cancellationToken);
            if (nativeCheckpoint is null ||
                nativeCheckpoint.ExternalRequestId != externalRequest.Id.Value ||
                !string.Equals(
                    nativeCheckpoint.PayloadHash,
                    continuation.CheckpointPayloadHash.Value,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    nativeCheckpoint.BackendRequestId,
                    continuation.Request.BackendRequestId.Value,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    nativeCheckpoint.BackendRequestPortId,
                    continuation.Request.BackendRequestPortId.Value,
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<WorkflowResumeBoundaryLoadOutcome> ClassifyResumableBoundaryAsync(
        AppDbContext dbContext,
        WorkflowRunRecordEntity run,
        WorkflowExternalRequestRecordEntity request,
        WorkflowExternalRequestBoundaryRecord boundary,
        CancellationToken cancellationToken)
    {
        var requestPayloadHash = Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(request.RequestJson)));
        if (boundary.RequestId.Value != request.Id ||
            boundary.Continuation.Request.ExternalRequestId.Value != request.Id ||
            !string.Equals(
                boundary.RequestPayloadHash.Value,
                requestPayloadHash,
                StringComparison.Ordinal))
        {
            return WorkflowResumeBoundaryLoadOutcome.LinkageMismatch;
        }

        var continuation = boundary.Continuation;
        var checkpoint = await dbContext.Set<WorkflowBackendCheckpointPayloadEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == continuation.Checkpoint.CheckpointId.Value,
                cancellationToken);
        if (checkpoint is null)
        {
            return WorkflowResumeBoundaryLoadOutcome.CheckpointMissing;
        }

        if (!string.Equals(
            checkpoint.SessionId,
            continuation.Checkpoint.SessionId.Value,
            StringComparison.Ordinal))
        {
            return WorkflowResumeBoundaryLoadOutcome.CheckpointIncompatible;
        }

        var session = await dbContext.Set<WorkflowBackendCheckpointSessionEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == checkpoint.SessionId,
                cancellationToken);
        if (session is null)
        {
            return WorkflowResumeBoundaryLoadOutcome.CheckpointMissing;
        }

        if (session.WorkflowVersionId != run.VersionId)
        {
            return WorkflowResumeBoundaryLoadOutcome.WorkflowVersionMismatch;
        }

        if (session.CompilerContractVersion != continuation.CompilerContractVersion.Value ||
            session.Backend != (int)run.Backend ||
            session.FormatVersion <= 0 ||
            string.IsNullOrWhiteSpace(session.Format))
        {
            return WorkflowResumeBoundaryLoadOutcome.CheckpointIncompatible;
        }

        if (!string.Equals(
            session.TopologyFingerprint,
            continuation.TopologyFingerprint.Value,
            StringComparison.Ordinal))
        {
            return WorkflowResumeBoundaryLoadOutcome.TopologyMismatch;
        }

        if (checkpoint.ExternalRequestId != request.Id ||
            !string.Equals(
                checkpoint.BackendRequestId,
                continuation.Request.BackendRequestId.Value,
                StringComparison.Ordinal) ||
            !string.Equals(
                checkpoint.BackendRequestPortId,
                continuation.Request.BackendRequestPortId.Value,
                StringComparison.Ordinal) ||
            session.RunId != run.RunId ||
            session.WorkflowId != run.WorkflowId ||
            session.Id != continuation.Checkpoint.SessionId.Value)
        {
            return WorkflowResumeBoundaryLoadOutcome.LinkageMismatch;
        }

        if (!FixedTimeHashEquals(
            checkpoint.PayloadHash,
            continuation.CheckpointPayloadHash.Value))
        {
            return WorkflowResumeBoundaryLoadOutcome.CheckpointCorrupt;
        }

        string payloadJson;
        try
        {
            payloadJson = checkpointPayloadProtector.Unprotect(checkpoint.ProtectedPayload);
        }
        catch (CryptographicException)
        {
            return WorkflowResumeBoundaryLoadOutcome.CheckpointCorrupt;
        }

        var actualPayloadHash = SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson));
        if (!FixedTimeHashEquals(checkpoint.PayloadHash, actualPayloadHash))
        {
            return WorkflowResumeBoundaryLoadOutcome.CheckpointCorrupt;
        }

        var publicCheckpoints = await dbContext.Set<WorkflowCheckpointRecordEntity>()
            .AsNoTracking()
            .Where(
                item =>
                    item.RunId == run.RunId &&
                    item.WorkflowId == run.WorkflowId &&
                    item.ExternalRequestId == request.Id &&
                    item.ResumeAvailability == WorkflowResumeAvailability.Available)
            .ToArrayAsync(cancellationToken);
        if (publicCheckpoints.Length == 0)
        {
            return WorkflowResumeBoundaryLoadOutcome.CheckpointMissing;
        }

        if (publicCheckpoints.Length != 1)
        {
            return WorkflowResumeBoundaryLoadOutcome.LinkageMismatch;
        }

        var publicCheckpoint = publicCheckpoints[0];
        if (publicCheckpoint.VersionId != run.VersionId)
        {
            return WorkflowResumeBoundaryLoadOutcome.WorkflowVersionMismatch;
        }

        if (publicCheckpoint.Backend != run.Backend)
        {
            return WorkflowResumeBoundaryLoadOutcome.CheckpointIncompatible;
        }

        if (!string.Equals(
                publicCheckpoint.BackendCheckpointId,
                continuation.Checkpoint.CheckpointId.Value,
                StringComparison.Ordinal))
        {
            return WorkflowResumeBoundaryLoadOutcome.LinkageMismatch;
        }

        return FixedTimeHashEquals(
            publicCheckpoint.PayloadHash,
            continuation.CheckpointPayloadHash.Value)
            ? WorkflowResumeBoundaryLoadOutcome.Found
            : WorkflowResumeBoundaryLoadOutcome.CheckpointCorrupt;
    }

    private static bool FixedTimeHashEquals(string left, string right)
    {
        try
        {
            return FixedTimeHashEquals(left, Convert.FromHexString(right));
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool FixedTimeHashEquals(string expected, byte[] actual)
    {
        try
        {
            var expectedBytes = Convert.FromHexString(expected);
            return expectedBytes.Length == actual.Length &&
                CryptographicOperations.FixedTimeEquals(expectedBytes, actual);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private async Task AddBackendResultRecordsAsync(
        AppDbContext dbContext,
        WorkflowBackendStartResult result,
        CancellationToken cancellationToken)
    {
        var eventIds = result.Events.Select(workflowEvent => workflowEvent.Id).ToArray();
        var existingEventIds = await dbContext.Set<WorkflowEventRecordEntity>()
            .Where(workflowEvent => eventIds.Contains(workflowEvent.Id))
            .Select(workflowEvent => workflowEvent.Id)
            .ToArrayAsync(cancellationToken);
        dbContext.Set<WorkflowEventRecordEntity>().AddRange(result.Events
            .Where(workflowEvent => !existingEventIds.Contains(workflowEvent.Id))
            .Select(WorkflowEventRecordEntity.FromEvent));

        foreach (var externalRequest in result.ExternalRequests)
        {
            dbContext.Set<WorkflowExternalRequestRecordEntity>().Add(
                WorkflowExternalRequestRecordEntity.FromRequest(externalRequest));
            var boundary = CreateBoundary(externalRequest);
            var boundaryEntity = new WorkflowExternalRequestBoundaryEntity
            {
                RequestId = externalRequest.Id.Value
            };
            PersistentWorkflowExternalRequestBoundaryStore.Apply(boundaryEntity, boundary);
            dbContext.Set<WorkflowExternalRequestBoundaryEntity>().Add(boundaryEntity);
        }

        var checkpointIds = result.Checkpoints.Select(checkpoint => checkpoint.Id.Value).ToArray();
        var existingCheckpointIds = await dbContext.Set<WorkflowCheckpointRecordEntity>()
            .Where(checkpoint => checkpointIds.Contains(checkpoint.Id))
            .Select(checkpoint => checkpoint.Id)
            .ToArrayAsync(cancellationToken);
        dbContext.Set<WorkflowCheckpointRecordEntity>().AddRange(result.Checkpoints
            .Where(checkpoint => !existingCheckpointIds.Contains(checkpoint.Id.Value))
            .Select(WorkflowCheckpointRecordEntity.FromCheckpoint));

        var artifactIds = result.Artifacts.Select(artifact => artifact.Id.Value).ToArray();
        var existingArtifactIds = await dbContext.Set<WorkflowArtifactRecordEntity>()
            .Where(artifact => artifactIds.Contains(artifact.Id))
            .Select(artifact => artifact.Id)
            .ToArrayAsync(cancellationToken);
        dbContext.Set<WorkflowArtifactRecordEntity>().AddRange(result.Artifacts
            .Where(artifact => !existingArtifactIds.Contains(artifact.Id.Value))
            .Select(WorkflowArtifactRecordEntity.FromArtifact));

        var usageIds = result.UsageObservations.Select(observation => observation.Id.Value).ToArray();
        var existingUsageIds = await dbContext.Set<WorkflowUsageObservationRecordEntity>()
            .Where(observation => usageIds.Contains(observation.Id))
            .Select(observation => observation.Id)
            .ToArrayAsync(cancellationToken);
        var newUsage = result.UsageObservations
            .Where(observation => !existingUsageIds.Contains(observation.Id.Value)).ToArray();
        dbContext.Set<WorkflowUsageObservationRecordEntity>().AddRange(
            newUsage.Select(WorkflowUsageObservationRecordEntity.FromObservation));
        if (newUsage.Length > 0) {
            await historyProjection.StageAsync(dbContext, newUsage, cancellationToken);
        }
    }

    private static WorkflowExternalRequestBoundaryRecord CreateBoundary(
        WorkflowExternalRequestRecord request)
        => WorkflowExternalRequestBoundaryRecord.TryCreate(request, out var boundary) &&
           boundary is not null
            ? boundary
            : throw new InvalidOperationException(
                $"Workflow external request '{request.Id}' does not contain a resumable boundary.");

    private static void UpdateRun(
        WorkflowRunRecordEntity target,
        WorkflowRunSnapshot result,
        DateTimeOffset committedAtUtc)
    {
        target.WorkflowId = result.WorkflowId.Value;
        target.VersionId = result.VersionId.Value;
        target.State = result.State;
        target.Backend = result.Backend;
        target.BackendRunId = result.BackendRunId;
        target.Summary = result.Summary;
        target.UpdatedAtUtc = committedAtUtc;
        target.TerminalAtUtc = result.State is WorkflowRunState.Completed or
            WorkflowRunState.Failed or WorkflowRunState.Cancelled
            ? committedAtUtc
            : null;
        target.OriginJson = WorkflowRunRecordEntity.SerializeOrigin(result.Origin);
        target.SetOriginProjection(result.Origin);
    }

    private static async Task MarkSourceCheckpointResumedAsync(
        AppDbContext dbContext,
        Guid requestId,
        DateTimeOffset resumedAtUtc,
        CancellationToken cancellationToken)
    {
        var checkpoints = await dbContext.Set<WorkflowCheckpointRecordEntity>()
            .Where(item => item.ExternalRequestId == requestId && item.ResumedAtUtc == null)
            .ToArrayAsync(cancellationToken);
        foreach (var checkpoint in checkpoints)
        {
            checkpoint.ResumedAtUtc = resumedAtUtc;
        }
    }

    private static void CompleteOperation(
        WorkflowExternalResponseOperationEntity operation,
        WorkflowExternalResponseOperationFinalResult finalResult,
        DateTimeOffset committedAtUtc)
    {
        operation.State = (int)finalResult.State;
        operation.OperationVersion++;
        operation.OutcomeCode = (int)finalResult.OutcomeCode;
        operation.SafeMessage = finalResult.SafeMessage;
        operation.FinalResultJson = System.Text.Json.JsonSerializer.Serialize(
            finalResult,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        operation.CompletedAtUtc = committedAtUtc;
        PersistentWorkflowExternalResponseOperationStore.ClearLease(operation);
    }

    private string UnprotectResponse(WorkflowExternalResponseOperationEntity operation)
        => PersistentWorkflowExternalResponseOperationStore.UnprotectAndValidateResponse(
            responseProtector,
            operation);

    private WorkflowExternalResponseOperationRecord ToOperationRecord(
        WorkflowExternalResponseOperationEntity entity,
        string responseJson)
    {
        var lease = string.IsNullOrWhiteSpace(entity.LeaseOwnerId)
            ? null
            : new WorkflowExternalResponseLease(
                new WorkflowExternalResponseLeaseOwnerId(entity.LeaseOwnerId),
                new WorkflowExternalResponseLeaseEpoch(entity.LeaseEpoch),
                entity.LeaseAcquiredAtUtc!.Value,
                entity.LeaseExpiresAtUtc!.Value);
        return new WorkflowExternalResponseOperationRecord(
            new WorkflowExternalResponseOperationId(entity.Id),
            new WorkflowExternalRequestId(entity.RequestId),
            new WorkflowRunId(entity.RunId),
            new WorkflowExternalRequestVersion(entity.ExpectedRequestVersion),
            new WorkflowExternalResponseIdempotencyKeyHash(entity.IdempotencyKeyHash),
            new WorkflowExternalResponsePayloadHash(entity.ResponsePayloadHash),
            new WorkflowExternalResponseActorScopeFingerprint(entity.ActorScopeFingerprint),
            new WorkflowExternalResponsePayload(responseJson),
            new WorkflowLaunchActor((WorkflowLaunchActorKind)entity.ActorKind, entity.ActorSubjectId),
            new WorkflowLaunchCorrelationId(entity.CorrelationId),
            (WorkflowExternalResponseOperationState)entity.State,
            entity.Attempt,
            new WorkflowExternalResponseOperationConcurrencyVersion(entity.OperationVersion),
            entity.AcceptedAtUtc)
        {
            Lease = lease,
            StartedAtUtc = entity.StartedAtUtc,
            CompletedAtUtc = entity.CompletedAtUtc,
            OutcomeCode = (WorkflowExternalResponseOperationOutcomeCode)entity.OutcomeCode,
            SafeMessage = entity.SafeMessage,
            FinalResult = PersistentWorkflowExternalResponseOperationStore.DeserializeFinalResult(entity)
        };
    }

    private static WorkflowExternalRequestRecord HydrateRequest(
        WorkflowExternalRequestRecordEntity entity,
        WorkflowExternalRequestBoundaryRecord boundary)
        => entity.ToRequest() with
        {
            Version = boundary.RequestVersion,
            State = boundary.State,
            ResponseContract = boundary.ResponseContract,
            Continuation = boundary.Continuation,
            AuthorizationPolicy = boundary.AuthorizationPolicy
        };

    private static WorkflowExternalRequestState MapRequestState(
        WorkflowExternalResponseOperationState operationState)
        => operationState switch
        {
            WorkflowExternalResponseOperationState.Denied => WorkflowExternalRequestState.Denied,
            WorkflowExternalResponseOperationState.Cancelled => WorkflowExternalRequestState.Cancelled,
            _ => WorkflowExternalRequestState.Responded
        };

    private static WorkflowResumeBoundaryLoadResult LoadResult(
        WorkflowResumeBoundaryLoadOutcome outcome)
        => new(outcome, Context: null);

    private static WorkflowResumeBoundaryCommitResult CommitResult(
        WorkflowResumeBoundaryCommitOutcome outcome)
        => new(outcome, Operation: null, Run: null, NextRequest: null);

    private WorkflowResumeBoundaryCancellationResult CancellationResult(
        WorkflowResumeBoundaryCancellationOutcome outcome,
        WorkflowRunRecordEntity? run = null,
        WorkflowExternalRequestRecordEntity? request = null,
        WorkflowExternalRequestBoundaryEntity? boundary = null,
        WorkflowExternalResponseOperationEntity? operation = null)
    {
        var boundaryRecord = boundary is null
            ? null
            : PersistentWorkflowExternalRequestBoundaryStore.ToRecord(boundary);
        var operationRecord = operation is null
            ? null
            : ToOperationRecord(operation, UnprotectResponse(operation));
        return new WorkflowResumeBoundaryCancellationResult(
            outcome,
            run?.ToSnapshot(),
            request is null
                ? null
                : boundaryRecord is null
                    ? request.ToRequest()
                    : HydrateRequest(request, boundaryRecord),
            operationRecord);
    }
}
