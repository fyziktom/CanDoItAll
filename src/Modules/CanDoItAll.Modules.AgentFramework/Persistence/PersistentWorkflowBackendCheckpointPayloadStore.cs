using System.Security.Cryptography;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class PersistentWorkflowBackendCheckpointPayloadStore :
    IWorkflowBackendCheckpointPayloadStore
{
    internal const string DataProtectionPurpose =
        "CanDoItAll.Modules.AgentFramework.WorkflowBackendCheckpointPayload.v1";

    private readonly IDbContextFactory<AppDbContext> dbContextFactory;
    private readonly IDataProtector payloadProtector;
    private readonly TimeProvider timeProvider;

    public PersistentWorkflowBackendCheckpointPayloadStore(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IDataProtectionProvider dataProtectionProvider,
        TimeProvider timeProvider)
    {
        this.dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        ArgumentNullException.ThrowIfNull(dataProtectionProvider);
        payloadProtector = dataProtectionProvider.CreateProtector(DataProtectionPurpose);
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<WorkflowBackendCheckpointCreateResult> CreateAsync(
        WorkflowBackendCheckpointCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!request.Payload.HasValidHash)
        {
            return new WorkflowBackendCheckpointCreateResult(
                WorkflowBackendCheckpointCreateOutcome.PayloadCorrupt,
                Checkpoint: null);
        }

        if (request.Parent is { } parent && parent.SessionId != request.Session.Id)
        {
            return new WorkflowBackendCheckpointCreateResult(
                WorkflowBackendCheckpointCreateOutcome.ParentSessionMismatch,
                Checkpoint: null);
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await EnsureSessionExistsAsync(dbContext, request.Session, cancellationToken);
        var session = await LockSessionAsync(dbContext, request.Session.Id, cancellationToken);
        if (!HasMatchingMetadata(session, request.Session))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new WorkflowBackendCheckpointCreateResult(
                WorkflowBackendCheckpointCreateOutcome.SessionMetadataMismatch,
                Checkpoint: null);
        }

        if (request.Parent is { } parentLink &&
            !await CheckpointExistsAsync(dbContext, parentLink, cancellationToken))
        {
            await transaction.RollbackAsync(cancellationToken);
            return new WorkflowBackendCheckpointCreateResult(
                WorkflowBackendCheckpointCreateOutcome.ParentNotFound,
                Checkpoint: null);
        }

        var checkpointId = WorkflowBackendCheckpointId.New();
        var commitOrdinal = new WorkflowCheckpointCommitOrdinal(session.NextCommitOrdinal);
        var createdAtUtc = timeProvider.GetUtcNow();
        session.NextCommitOrdinal++;
        dbContext.Set<WorkflowBackendCheckpointPayloadEntity>().Add(
            ToEntity(request, checkpointId, commitOrdinal, createdAtUtc));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new WorkflowBackendCheckpointCreateResult(
            WorkflowBackendCheckpointCreateOutcome.Created,
            ToRecord(
                ToSession(session),
                checkpointId,
                request.Parent,
                commitOrdinal,
                createdAtUtc,
                request.Payload,
                request.ExternalRequestLink));
    }

    public async Task<WorkflowBackendCheckpointListResult> ListIndexAsync(
        WorkflowBackendSessionId sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (!await dbContext.Set<WorkflowBackendCheckpointSessionEntity>()
            .AsNoTracking()
            .AnyAsync(session => session.Id == sessionId.Value, cancellationToken))
        {
            return new WorkflowBackendCheckpointListResult(
                WorkflowBackendCheckpointListOutcome.SessionNotFound,
                []);
        }

        var checkpoints = await dbContext.Set<WorkflowBackendCheckpointPayloadEntity>()
            .AsNoTracking()
            .Where(checkpoint => checkpoint.SessionId == sessionId.Value)
            .OrderBy(checkpoint => checkpoint.CommitOrdinal)
            .Select(checkpoint => ToIndexEntry(checkpoint))
            .ToArrayAsync(cancellationToken);
        return new WorkflowBackendCheckpointListResult(
            WorkflowBackendCheckpointListOutcome.Found,
            checkpoints);
    }

    public async Task<WorkflowBackendCheckpointReadResult> ReadAsync(
        WorkflowBackendCheckpointLink link,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var checkpoint = await dbContext.Set<WorkflowBackendCheckpointPayloadEntity>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                record => record.Id == link.CheckpointId.Value,
                cancellationToken);
        if (checkpoint is null)
        {
            return new WorkflowBackendCheckpointReadResult(
                WorkflowBackendCheckpointReadOutcome.NotFound,
                Checkpoint: null);
        }

        if (!string.Equals(checkpoint.SessionId, link.SessionId.Value, StringComparison.Ordinal))
        {
            return new WorkflowBackendCheckpointReadResult(
                WorkflowBackendCheckpointReadOutcome.SessionMismatch,
                Checkpoint: null);
        }

        var session = await dbContext.Set<WorkflowBackendCheckpointSessionEntity>()
            .AsNoTracking()
            .SingleAsync(record => record.Id == checkpoint.SessionId, cancellationToken);
        WorkflowBackendCheckpointPayload payload;
        try
        {
            var payloadJson = payloadProtector.Unprotect(checkpoint.ProtectedPayload);
            payload = new WorkflowBackendCheckpointPayload(
                payloadJson,
                new WorkflowBackendCheckpointPayloadHash(checkpoint.PayloadHash));
        }
        catch (Exception exception) when (
            exception is CryptographicException or FormatException or ArgumentException)
        {
            return new WorkflowBackendCheckpointReadResult(
                WorkflowBackendCheckpointReadOutcome.PayloadCorrupt,
                Checkpoint: null);
        }

        if (!payload.HasValidHash)
        {
            return new WorkflowBackendCheckpointReadResult(
                WorkflowBackendCheckpointReadOutcome.PayloadCorrupt,
                Checkpoint: null);
        }

        try
        {
            return new WorkflowBackendCheckpointReadResult(
                WorkflowBackendCheckpointReadOutcome.Found,
                ToRecord(session, checkpoint, payload));
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return new WorkflowBackendCheckpointReadResult(
                WorkflowBackendCheckpointReadOutcome.PayloadCorrupt,
                Checkpoint: null);
        }
    }

    private static async Task EnsureSessionExistsAsync(
        AppDbContext dbContext,
        WorkflowBackendCheckpointSession session,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "AgentFramework_WorkflowBackendCheckpointSessions"
                ("Id", "RunId", "WorkflowId", "WorkflowVersionId", "Backend", "Format", "FormatVersion", "CompilerContractVersion", "TopologyFingerprint", "NextCommitOrdinal")
            VALUES
                ({session.Id.Value}, {session.RunId.Value}, {session.WorkflowId.Value}, {session.WorkflowVersionId.Value}, {(int)session.Backend}, {session.Format.Value}, {session.FormatVersion.Value}, {session.CompilerContractVersion.Value}, {session.TopologyFingerprint.Value}, {0L})
            ON CONFLICT ("Id") DO NOTHING
            """,
            cancellationToken);
    }

    private static Task<WorkflowBackendCheckpointSessionEntity> LockSessionAsync(
        AppDbContext dbContext,
        WorkflowBackendSessionId sessionId,
        CancellationToken cancellationToken)
        => dbContext.Set<WorkflowBackendCheckpointSessionEntity>()
            .FromSqlInterpolated(
                $"""
                SELECT *
                FROM "AgentFramework_WorkflowBackendCheckpointSessions"
                WHERE "Id" = {sessionId.Value}
                FOR UPDATE
                """)
            .SingleAsync(cancellationToken);

    private static Task<bool> CheckpointExistsAsync(
        AppDbContext dbContext,
        WorkflowBackendCheckpointLink parent,
        CancellationToken cancellationToken)
        => dbContext.Set<WorkflowBackendCheckpointPayloadEntity>()
            .AnyAsync(
                checkpoint =>
                    checkpoint.Id == parent.CheckpointId.Value &&
                    checkpoint.SessionId == parent.SessionId.Value,
                cancellationToken);

    private WorkflowBackendCheckpointPayloadEntity ToEntity(
        WorkflowBackendCheckpointCreateRequest request,
        WorkflowBackendCheckpointId checkpointId,
        WorkflowCheckpointCommitOrdinal commitOrdinal,
        DateTimeOffset createdAtUtc)
        => new()
        {
            Id = checkpointId.Value,
            SessionId = request.Session.Id.Value,
            ParentCheckpointId = request.Parent?.CheckpointId.Value,
            CommitOrdinal = commitOrdinal.Value,
            ProtectedPayload = payloadProtector.Protect(request.Payload.Json),
            PayloadHash = request.Payload.Sha256.Value,
            ExternalRequestId = request.ExternalRequestLink?.ExternalRequestId.Value,
            BackendRequestId = request.ExternalRequestLink?.BackendRequestId.Value,
            BackendRequestPortId = request.ExternalRequestLink?.BackendRequestPortId.Value,
            CreatedAtUtc = createdAtUtc
        };

    private static bool HasMatchingMetadata(
        WorkflowBackendCheckpointSessionEntity persisted,
        WorkflowBackendCheckpointSession expected)
        => persisted.RunId == expected.RunId.Value &&
           persisted.WorkflowId == expected.WorkflowId.Value &&
           persisted.WorkflowVersionId == expected.WorkflowVersionId.Value &&
           persisted.Backend == (int)expected.Backend &&
           string.Equals(persisted.Format, expected.Format.Value, StringComparison.Ordinal) &&
           persisted.FormatVersion == expected.FormatVersion.Value &&
           persisted.CompilerContractVersion == expected.CompilerContractVersion.Value &&
           string.Equals(
               persisted.TopologyFingerprint,
               expected.TopologyFingerprint.Value,
               StringComparison.Ordinal);

    private static WorkflowBackendCheckpointPayloadRecord ToRecord(
        WorkflowBackendCheckpointSessionEntity session,
        WorkflowBackendCheckpointPayloadEntity checkpoint,
        WorkflowBackendCheckpointPayload payload)
        => ToRecord(
            ToSession(session),
            new WorkflowBackendCheckpointId(checkpoint.Id),
            string.IsNullOrWhiteSpace(checkpoint.ParentCheckpointId)
                ? null
                : new WorkflowBackendCheckpointLink(
                    new WorkflowBackendSessionId(checkpoint.SessionId),
                    new WorkflowBackendCheckpointId(checkpoint.ParentCheckpointId)),
            new WorkflowCheckpointCommitOrdinal(checkpoint.CommitOrdinal),
            checkpoint.CreatedAtUtc,
            payload,
            ToExternalRequestLink(checkpoint));

    private static WorkflowBackendCheckpointPayloadRecord ToRecord(
        WorkflowBackendCheckpointSession session,
        WorkflowBackendCheckpointId checkpointId,
        WorkflowBackendCheckpointLink? parent,
        WorkflowCheckpointCommitOrdinal commitOrdinal,
        DateTimeOffset createdAtUtc,
        WorkflowBackendCheckpointPayload payload,
        WorkflowBackendExternalRequestLink? externalRequestLink)
        => new(
            session,
            new WorkflowBackendCheckpointIndexEntry(
                new WorkflowBackendCheckpointLink(session.Id, checkpointId),
                parent,
                commitOrdinal,
                createdAtUtc),
            payload,
            externalRequestLink);

    private static WorkflowBackendCheckpointSession ToSession(
        WorkflowBackendCheckpointSessionEntity session)
        => new(
            new WorkflowBackendSessionId(session.Id),
            new WorkflowRunId(session.RunId),
            new WorkflowId(session.WorkflowId),
            new WorkflowVersionId(session.WorkflowVersionId),
            (WorkflowRuntimeBackendKind)session.Backend,
            new WorkflowBackendCheckpointFormat(session.Format),
            new WorkflowBackendCheckpointFormatVersion(session.FormatVersion),
            new WorkflowCompilerContractVersion(session.CompilerContractVersion),
            new WorkflowTopologyFingerprint(session.TopologyFingerprint));

    private static WorkflowBackendCheckpointIndexEntry ToIndexEntry(
        WorkflowBackendCheckpointPayloadEntity checkpoint)
        => new(
            new WorkflowBackendCheckpointLink(
                new WorkflowBackendSessionId(checkpoint.SessionId),
                new WorkflowBackendCheckpointId(checkpoint.Id)),
            string.IsNullOrWhiteSpace(checkpoint.ParentCheckpointId)
                ? null
                : new WorkflowBackendCheckpointLink(
                    new WorkflowBackendSessionId(checkpoint.SessionId),
                    new WorkflowBackendCheckpointId(checkpoint.ParentCheckpointId)),
            new WorkflowCheckpointCommitOrdinal(checkpoint.CommitOrdinal),
            checkpoint.CreatedAtUtc);

    private static WorkflowBackendExternalRequestLink? ToExternalRequestLink(
        WorkflowBackendCheckpointPayloadEntity checkpoint)
    {
        if (!checkpoint.ExternalRequestId.HasValue)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(checkpoint.BackendRequestId) ||
            string.IsNullOrWhiteSpace(checkpoint.BackendRequestPortId))
        {
            throw new InvalidOperationException(
                $"Workflow checkpoint '{checkpoint.Id}' has incomplete external-request linkage.");
        }

        return new WorkflowBackendExternalRequestLink(
            new WorkflowExternalRequestId(checkpoint.ExternalRequestId.Value),
            new WorkflowBackendRequestId(checkpoint.BackendRequestId),
            new WorkflowBackendRequestPortId(checkpoint.BackendRequestPortId));
    }
}
