using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class PersistentWorkflowExternalRequestBoundaryStore(
    IDbContextFactory<AppDbContext> dbContextFactory) :
    IWorkflowExternalRequestBoundaryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDbContextFactory<AppDbContext> dbContextFactory =
        dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));

    public async Task<WorkflowExternalRequestBoundarySaveResult> UpsertAsync(
        WorkflowExternalRequestBoundaryRecord boundary,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(boundary);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        using var mutationLease = await WorkflowPersistenceProvider.EnterInMemoryMutationAsync(
            dbContext,
            cancellationToken);
        var isInMemory = WorkflowPersistenceProvider.IsInMemory(dbContext);
        await using var transaction = isInMemory
            ? null
            : await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var externalRequest = isInMemory
            ? await dbContext.Set<WorkflowExternalRequestRecordEntity>()
                .SingleOrDefaultAsync(
                    current => current.Id == boundary.RequestId.Value,
                    cancellationToken)
            : await dbContext.Set<WorkflowExternalRequestRecordEntity>()
                .FromSqlInterpolated(
                    $"""
                    SELECT *
                    FROM "AgentFramework_WorkflowExternalRequests"
                    WHERE "Id" = {boundary.RequestId.Value}
                    FOR UPDATE
                    """)
                .SingleOrDefaultAsync(cancellationToken);
        if (externalRequest is null)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            return new WorkflowExternalRequestBoundarySaveResult(
                WorkflowExternalRequestBoundarySaveOutcome.RequestNotFound,
                Boundary: null);
        }

        var run = await dbContext.Set<WorkflowRunRecordEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.RunId == externalRequest.RunId, cancellationToken);
        if (run is null)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            return new WorkflowExternalRequestBoundarySaveResult(
                WorkflowExternalRequestBoundarySaveOutcome.RequestNotFound,
                Boundary: null);
        }

        var requestPayloadHash = Convert.ToHexStringLower(SHA256.HashData(
            Encoding.UTF8.GetBytes(externalRequest.RequestJson)));
        if (boundary.Continuation.Request.ExternalRequestId != boundary.RequestId ||
            !string.Equals(
                boundary.RequestPayloadHash.Value,
                requestPayloadHash,
                StringComparison.Ordinal))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            return new WorkflowExternalRequestBoundarySaveResult(
                WorkflowExternalRequestBoundarySaveOutcome.VersionConflict,
                Boundary: null);
        }

        var linkOutcome = await WorkflowNativeCheckpointRequestLinker.LinkAsync(
            dbContext,
            boundary,
            new WorkflowRunId(run.RunId),
            new WorkflowId(run.WorkflowId),
            new WorkflowVersionId(run.VersionId),
            run.Backend,
            cancellationToken);
        if (linkOutcome is not (
            WorkflowNativeCheckpointRequestLinkOutcome.Linked or
            WorkflowNativeCheckpointRequestLinkOutcome.AlreadyLinked))
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            return new WorkflowExternalRequestBoundarySaveResult(
                WorkflowExternalRequestBoundarySaveOutcome.VersionConflict,
                Boundary: null);
        }

        var entity = await dbContext.Set<WorkflowExternalRequestBoundaryEntity>()
            .SingleOrDefaultAsync(
                current => current.RequestId == boundary.RequestId.Value,
                cancellationToken);
        var outcome = WorkflowExternalRequestBoundarySaveOutcome.Created;
        if (entity is null)
        {
            entity = new WorkflowExternalRequestBoundaryEntity
            {
                RequestId = boundary.RequestId.Value
            };
            dbContext.Set<WorkflowExternalRequestBoundaryEntity>().Add(entity);
        }
        else
        {
            var existing = ToRecord(entity);
            if (entity.RequestVersion != boundary.RequestVersion.Value ||
                !HasSameImmutableBoundary(existing, boundary) ||
                !CanTransition(existing.State, boundary.State))
            {
                if (transaction is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                return new WorkflowExternalRequestBoundarySaveResult(
                    WorkflowExternalRequestBoundarySaveOutcome.VersionConflict,
                    existing);
            }

            outcome = WorkflowExternalRequestBoundarySaveOutcome.Updated;
        }

        Apply(entity, boundary);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }
        }
        catch (DbUpdateConcurrencyException)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            return new WorkflowExternalRequestBoundarySaveResult(
                WorkflowExternalRequestBoundarySaveOutcome.VersionConflict,
                Boundary: null);
        }

        return new WorkflowExternalRequestBoundarySaveResult(outcome, boundary);
    }

    public async Task<WorkflowExternalRequestBoundaryReadResult> ReadAsync(
        WorkflowExternalRequestId requestId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var requestExists = await dbContext.Set<WorkflowExternalRequestRecordEntity>()
            .AsNoTracking()
            .AnyAsync(request => request.Id == requestId.Value, cancellationToken);
        if (!requestExists)
        {
            return new WorkflowExternalRequestBoundaryReadResult(
                WorkflowExternalRequestBoundaryReadOutcome.RequestNotFound,
                Boundary: null);
        }

        var boundary = await dbContext.Set<WorkflowExternalRequestBoundaryEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(current => current.RequestId == requestId.Value, cancellationToken);
        return boundary is null
            ? new WorkflowExternalRequestBoundaryReadResult(
                WorkflowExternalRequestBoundaryReadOutcome.LegacyNonResumable,
                Boundary: null)
            : new WorkflowExternalRequestBoundaryReadResult(
                WorkflowExternalRequestBoundaryReadOutcome.Found,
                ToRecord(boundary));
    }

    internal static WorkflowExternalRequestBoundaryRecord ToRecord(
        WorkflowExternalRequestBoundaryEntity entity)
    {
        var responseContract = DeserializeRequired<WorkflowExternalResponseContract>(
            entity.ResponseContractJson,
            entity.RequestId,
            nameof(entity.ResponseContractJson));
        var continuation = DeserializeRequired<WorkflowExternalRequestContinuation>(
            entity.ContinuationJson,
            entity.RequestId,
            nameof(entity.ContinuationJson));
        var record = new WorkflowExternalRequestBoundaryRecord(
            new WorkflowExternalRequestId(entity.RequestId),
            new WorkflowExternalRequestVersion(entity.RequestVersion),
            (WorkflowExternalRequestState)entity.State,
            responseContract,
            continuation,
            new WorkflowExternalRequestPayloadHash(entity.RequestPayloadHash),
            entity.CreatedAtUtc);
        return record with
        {
            AuthorizationPolicy = string.IsNullOrWhiteSpace(entity.AuthorizationPolicyJson)
                ? null
                : DeserializeRequired<WorkflowExternalRequestAuthorizationPolicySnapshot>(
                    entity.AuthorizationPolicyJson,
                    entity.RequestId,
                    nameof(entity.AuthorizationPolicyJson))
        };
    }

    internal static WorkflowExternalRequestRecord HydrateRequest(
        WorkflowExternalRequestRecordEntity request,
        WorkflowExternalRequestBoundaryEntity? boundary)
    {
        ArgumentNullException.ThrowIfNull(request);
        var legacyRequest = request.ToRequest();
        if (boundary is null)
        {
            return legacyRequest;
        }

        var boundaryRecord = ToRecord(boundary);
        if (boundaryRecord.RequestId != legacyRequest.Id)
        {
            throw new InvalidOperationException(
                $"Workflow request '{legacyRequest.Id}' has a mismatched native boundary.");
        }

        return legacyRequest with
        {
            Version = boundaryRecord.RequestVersion,
            State = boundaryRecord.State,
            ResponseContract = boundaryRecord.ResponseContract,
            Continuation = boundaryRecord.Continuation,
            AuthorizationPolicy = boundaryRecord.AuthorizationPolicy
        };
    }

    internal static void Apply(
        WorkflowExternalRequestBoundaryEntity entity,
        WorkflowExternalRequestBoundaryRecord boundary)
    {
        entity.RequestVersion = boundary.RequestVersion.Value;
        entity.State = (int)boundary.State;
        entity.ResponseContractJson = JsonSerializer.Serialize(boundary.ResponseContract, JsonOptions);
        entity.ContinuationJson = JsonSerializer.Serialize(boundary.Continuation, JsonOptions);
        entity.RequestPayloadHash = boundary.RequestPayloadHash.Value;
        entity.AuthorizationPolicyJson = boundary.AuthorizationPolicy is null
            ? null
            : JsonSerializer.Serialize(boundary.AuthorizationPolicy, JsonOptions);
        entity.CreatedAtUtc = boundary.CreatedAtUtc;
    }

    private static T DeserializeRequired<T>(string json, Guid requestId, string fieldName)
        where T : class
        => JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException(
                $"Workflow external request boundary '{requestId}' has invalid {fieldName} data.");

    private static bool HasSameImmutableBoundary(
        WorkflowExternalRequestBoundaryRecord existing,
        WorkflowExternalRequestBoundaryRecord requested)
        => existing with { State = requested.State } == requested;

    private static bool CanTransition(
        WorkflowExternalRequestState current,
        WorkflowExternalRequestState next)
        => current == next || (current, next) switch
        {
            (WorkflowExternalRequestState.Pending, WorkflowExternalRequestState.ResponseClaimed) => true,
            (WorkflowExternalRequestState.Pending, WorkflowExternalRequestState.Responded) => true,
            (WorkflowExternalRequestState.Pending, WorkflowExternalRequestState.Denied) => true,
            (WorkflowExternalRequestState.Pending, WorkflowExternalRequestState.Superseded) => true,
            (WorkflowExternalRequestState.Pending, WorkflowExternalRequestState.Cancelled) => true,
            (WorkflowExternalRequestState.ResponseClaimed, WorkflowExternalRequestState.Responded) => true,
            (WorkflowExternalRequestState.ResponseClaimed, WorkflowExternalRequestState.Denied) => true,
            (WorkflowExternalRequestState.ResponseClaimed, WorkflowExternalRequestState.Cancelled) => true,
            _ => false
        };
}
