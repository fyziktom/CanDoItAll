using System.Data;
using System.Data.Common;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workspace;

public sealed class ConnectorCommandProcessor(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    IEnumerable<IConnectorCommandHandler> handlers,
    ILogger<ConnectorCommandProcessor> logger)
{
    private const int MaxAttempts = 3;

    public async Task<ConnectorCommandStatus?> ProcessAsync(
        Guid commandId,
        string? leaseToken = null,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ConnectorCommandSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var now = clock.GetUtcNow();
        var command = await dbContext.Set<ConnectorCommandRecord>()
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == commandId, cancellationToken);
        if (command is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(leaseToken))
        {
            logger.LogWarning(
                "Connector command {CommandId} processing was requested without a lease token; canonical state was not updated.",
                commandId);
            return command.Status;
        }

        if (!string.Equals(command.LeaseToken, leaseToken, StringComparison.Ordinal) ||
            command.LeaseExpiresAtUtc is null ||
            command.LeaseExpiresAtUtc <= now)
        {
            return null;
        }

        if (command.Status is ConnectorCommandStatus.Completed or ConnectorCommandStatus.DeadLettered or ConnectorCommandStatus.Rejected)
        {
            await ReleaseClaimedLeaseAsync(dbContext, command.Id, leaseToken, now, cancellationToken);
            return command.Status;
        }

        if (command.ApprovalState == ConnectorCommandApprovalState.Pending)
        {
            await ReleaseClaimedLeaseAsync(dbContext, command.Id, leaseToken, now, cancellationToken);
            return command.Status;
        }

        if (command.NextAttemptAtUtc.HasValue && command.NextAttemptAtUtc.Value > now)
        {
            await ReleaseClaimedLeaseAsync(dbContext, command.Id, leaseToken, now, cancellationToken);
            return command.Status;
        }

        command.AttemptCount++;
        if (!await TryStartClaimedAttemptAsync(dbContext, command, leaseToken, now, cancellationToken))
        {
            return null;
        }

        dbContext.Set<ConnectorCommandAuditRecord>().Add(new ConnectorCommandAuditRecord
        {
            ConnectorCommandId = command.Id,
            ProjectId = command.ProjectId,
            EventKind = ConnectorCommandAuditEventKind.AttemptStarted,
            Actor = "system",
            Message = $"Starting connector command attempt {command.AttemptCount}.",
            DetailsJson = "{}",
            CreatedAtUtc = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        var handler = handlers.LastOrDefault(candidate => candidate.CanHandle(command.ConnectorPluginKey, command.CommandKey));
        ConnectorCommandExecutionResult executionResult;
        if (handler is null)
        {
            executionResult = ConnectorCommandExecutionResult.PermanentFailure(
                "No connector command handler is registered for the queued operation.");
        }
        else
        {
            try
            {
                executionResult = await handler.ExecuteAsync(
                    new ConnectorCommandExecutionRequest(
                        command.Id,
                        command.ProjectId,
                        command.ConnectorPluginKey,
                        command.CommandKey,
                        command.PayloadJson,
                        command.IdempotencyKey,
                        command.AttemptCount),
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                executionResult = ConnectorCommandExecutionResult.RetryableFailure(ex.Message);
            }
        }

        now = clock.GetUtcNow();
        var finalization = BuildFinalization(command, executionResult, now);
        if (await TryFinalizeCommandAsync(dbContext, command, leaseToken, finalization, now, cancellationToken))
        {
            return finalization.Status;
        }

        logger.LogWarning(
            "Connector command {CommandId} lost lease token {LeaseToken} during finalization; canonical state was not updated.",
            command.Id,
            MaskLeaseToken(leaseToken));
        await RecordLeaseLostAuditAsync(command, leaseToken, now, cancellationToken);
        return null;
    }

    private async Task<bool> TryStartClaimedAttemptAsync(
        AppDbContext dbContext,
        ConnectorCommandRecord command,
        string leaseToken,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var updatedRows = await dbContext.Set<ConnectorCommandRecord>()
            .Where(item => item.Id == command.Id)
            .Where(item => item.Status == ConnectorCommandStatus.Pending)
            .Where(item => item.ApprovalState != ConnectorCommandApprovalState.Pending)
            .Where(item => item.NextAttemptAtUtc == null || item.NextAttemptAtUtc <= now)
            .Where(item => item.LeaseToken == leaseToken)
            .Where(item => item.LeaseExpiresAtUtc != null && item.LeaseExpiresAtUtc > now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.AttemptCount, item => item.AttemptCount + 1)
                    .SetProperty(item => item.LastAttemptAtUtc, now)
                    .SetProperty(item => item.UpdatedAtUtc, now),
                cancellationToken);
        if (updatedRows > 0)
        {
            return true;
        }

        logger.LogWarning(
            "Connector command {CommandId} lost lease token {LeaseToken} before the attempt could start.",
            command.Id,
            MaskLeaseToken(leaseToken));
        return false;
    }

    private static async Task ReleaseClaimedLeaseAsync(
        AppDbContext dbContext,
        Guid commandId,
        string leaseToken,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await dbContext.Set<ConnectorCommandRecord>()
            .Where(item => item.Id == commandId)
            .Where(item => item.LeaseToken == leaseToken)
            .Where(item => item.LeaseExpiresAtUtc != null && item.LeaseExpiresAtUtc > now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.LeaseToken, string.Empty)
                    .SetProperty(item => item.LeaseExpiresAtUtc, (DateTimeOffset?)null)
                    .SetProperty(item => item.UpdatedAtUtc, now),
                cancellationToken);
    }

    private static ConnectorCommandFinalization BuildFinalization(
        ConnectorCommandRecord command,
        ConnectorCommandExecutionResult executionResult,
        DateTimeOffset now)
    {
        return executionResult.Outcome switch
        {
            ConnectorCommandExecutionOutcome.Completed => new ConnectorCommandFinalization(
                ConnectorCommandStatus.Completed,
                now,
                null,
                string.Empty,
                NormalizeJson(executionResult.ResultJson),
                ConnectorCommandAuditEventKind.Completed,
                "Connector command completed successfully.",
                NormalizeJson(executionResult.ResultJson)),
            ConnectorCommandExecutionOutcome.RetryableFailure when command.AttemptCount >= MaxAttempts =>
                BuildDeadLetteredFinalization(
                    command.ResultJson,
                    NormalizeError(executionResult.ErrorMessage, "Connector command exhausted all retry attempts.")),
            ConnectorCommandExecutionOutcome.RetryableFailure => new ConnectorCommandFinalization(
                ConnectorCommandStatus.Pending,
                null,
                now.Add(ComputeBackoff(command.AttemptCount)),
                NormalizeError(executionResult.ErrorMessage, "Connector command failed and will be retried."),
                command.ResultJson,
                ConnectorCommandAuditEventKind.AttemptFailed,
                "Connector command failed and was scheduled for retry.",
                BuildFailureDetailsJson(
                    NormalizeError(executionResult.ErrorMessage, "Connector command failed and will be retried."),
                    now.Add(ComputeBackoff(command.AttemptCount)))),
            ConnectorCommandExecutionOutcome.PermanentFailure =>
                BuildDeadLetteredFinalization(
                    command.ResultJson,
                    NormalizeError(executionResult.ErrorMessage, "Connector command failed permanently.")),
            _ => throw new InvalidOperationException($"Unsupported execution outcome '{executionResult.Outcome}'.")
        };
    }

    private static ConnectorCommandFinalization BuildDeadLetteredFinalization(
        string resultJson,
        string errorMessage)
    {
        return new ConnectorCommandFinalization(
            ConnectorCommandStatus.DeadLettered,
            null,
            null,
            errorMessage,
            resultJson,
            ConnectorCommandAuditEventKind.DeadLettered,
            "Connector command was moved to dead-letter state.",
            BuildFailureDetailsJson(errorMessage, null));
    }

    private static async Task<bool> TryFinalizeCommandAsync(
        AppDbContext dbContext,
        ConnectorCommandRecord command,
        string leaseToken,
        ConnectorCommandFinalization finalization,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var updatedRows = await dbContext.Set<ConnectorCommandRecord>()
            .Where(item => item.Id == command.Id)
            .Where(item => item.Status == ConnectorCommandStatus.Pending)
            .Where(item => item.LeaseToken == leaseToken)
            .Where(item => item.LeaseExpiresAtUtc != null && item.LeaseExpiresAtUtc > now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.Status, finalization.Status)
                    .SetProperty(item => item.CompletedAtUtc, finalization.CompletedAtUtc)
                    .SetProperty(item => item.NextAttemptAtUtc, finalization.NextAttemptAtUtc)
                    .SetProperty(item => item.LastError, finalization.LastError)
                    .SetProperty(item => item.ResultJson, finalization.ResultJson)
                    .SetProperty(item => item.LeaseToken, string.Empty)
                    .SetProperty(item => item.LeaseExpiresAtUtc, (DateTimeOffset?)null)
                    .SetProperty(item => item.UpdatedAtUtc, now),
                cancellationToken);
        if (updatedRows == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        dbContext.Set<ConnectorCommandAuditRecord>().Add(new ConnectorCommandAuditRecord
        {
            ConnectorCommandId = command.Id,
            ProjectId = command.ProjectId,
            EventKind = finalization.AuditEventKind,
            Actor = "system",
            Message = finalization.AuditMessage,
            DetailsJson = finalization.AuditDetailsJson,
            CreatedAtUtc = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task RecordLeaseLostAuditAsync(
        ConnectorCommandRecord command,
        string leaseToken,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ConnectorCommandSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        dbContext.Set<ConnectorCommandAuditRecord>().Add(new ConnectorCommandAuditRecord
        {
            ConnectorCommandId = command.Id,
            ProjectId = command.ProjectId,
            EventKind = ConnectorCommandAuditEventKind.LeaseLost,
            Actor = "system",
            Message = "Connector command lease was lost before canonical finalization.",
            DetailsJson = $$"""{"leaseToken":"{{EscapeJson(MaskLeaseToken(leaseToken))}}"}""",
            CreatedAtUtc = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static TimeSpan ComputeBackoff(int attemptCount)
    {
        var minutes = Math.Min(1 << Math.Max(0, attemptCount - 1), 15);
        return TimeSpan.FromMinutes(minutes);
    }

    private static string BuildFailureDetailsJson(string errorMessage, DateTimeOffset? nextAttemptAtUtc)
    {
        return $$"""
                 {
                   "error":"{{EscapeJson(errorMessage)}}",
                   "nextAttemptAtUtc":"{{(nextAttemptAtUtc.HasValue ? nextAttemptAtUtc.Value.ToString("O") : string.Empty)}}"
                 }
                 """;
    }

    private static string EscapeJson(string value)
    {
        return (value ?? string.Empty)
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    private static string NormalizeError(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }

    private static string NormalizeJson(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "{}"
            : value.Trim();
    }

    private static string MaskLeaseToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "(empty)";
        }

        var normalized = value.Trim();
        return normalized.Length <= 8
            ? "***"
            : $"{normalized[..4]}...{normalized[^4..]}";
    }

    private sealed record ConnectorCommandFinalization(
        ConnectorCommandStatus Status,
        DateTimeOffset? CompletedAtUtc,
        DateTimeOffset? NextAttemptAtUtc,
        string LastError,
        string ResultJson,
        ConnectorCommandAuditEventKind AuditEventKind,
        string AuditMessage,
        string AuditDetailsJson);
}

public sealed class ConnectorOutboxService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    ConnectorPluginRegistry connectorPluginRegistry,
    ConnectorCommandProcessor commandProcessor)
{
    private static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromMinutes(2);

    public async Task<ConnectorCommandEnqueueResult> EnqueueAsync(
        ConnectorCommandEnqueueRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ConnectorPluginKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CommandKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.IdempotencyKey);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ConnectorCommandSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        _ = connectorPluginRegistry.Resolve(request.ConnectorPluginKey);

        var normalizedPluginKey = request.ConnectorPluginKey.Trim();
        var normalizedCommandKey = request.CommandKey.Trim();
        var normalizedIdempotencyKey = request.IdempotencyKey.Trim();
        var normalizedPayload = string.IsNullOrWhiteSpace(request.PayloadJson)
            ? "{}"
            : request.PayloadJson.Trim();
        var normalizedActor = NormalizeActor(request.RequestedBy);
        var existing = await dbContext.Set<ConnectorCommandRecord>()
            .FirstOrDefaultAsync(item =>
                    item.ProjectId == request.ProjectId &&
                    item.ConnectorPluginKey == normalizedPluginKey &&
                    item.CommandKey == normalizedCommandKey &&
                    item.IdempotencyKey == normalizedIdempotencyKey,
                cancellationToken);
        if (existing is not null)
        {
            dbContext.Set<ConnectorCommandAuditRecord>().Add(new ConnectorCommandAuditRecord
            {
                ConnectorCommandId = existing.Id,
                ProjectId = existing.ProjectId,
                EventKind = ConnectorCommandAuditEventKind.IdempotencyHit,
                Actor = normalizedActor,
                Message = "Duplicate connector command enqueue returned the existing durable command.",
                DetailsJson = "{}",
                CreatedAtUtc = clock.GetUtcNow()
            });
            await dbContext.SaveChangesAsync(cancellationToken);
            return new ConnectorCommandEnqueueResult(
                existing.Id,
                true,
                existing.Status,
                existing.ApprovalState);
        }

        var now = clock.GetUtcNow();
        var approvalState = request.RequiresApproval
            ? ConnectorCommandApprovalState.Pending
            : ConnectorCommandApprovalState.NotRequired;
        var command = new ConnectorCommandRecord
        {
            ProjectId = request.ProjectId,
            ConnectorPluginKey = normalizedPluginKey,
            CommandKey = normalizedCommandKey,
            IdempotencyKey = normalizedIdempotencyKey,
            PayloadJson = normalizedPayload,
            Status = ConnectorCommandStatus.Pending,
            ApprovalState = approvalState,
            RequestedBy = normalizedActor,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await dbContext.Set<ConnectorCommandRecord>().AddAsync(command, cancellationToken);
        dbContext.Set<ConnectorCommandAuditRecord>().Add(new ConnectorCommandAuditRecord
        {
            ConnectorCommandId = command.Id,
            ProjectId = command.ProjectId,
            EventKind = ConnectorCommandAuditEventKind.Enqueued,
            Actor = normalizedActor,
            Message = "Connector command was queued for durable execution.",
            DetailsJson = "{}",
            CreatedAtUtc = now
        });
        if (approvalState == ConnectorCommandApprovalState.Pending)
        {
            dbContext.Set<ConnectorCommandAuditRecord>().Add(new ConnectorCommandAuditRecord
            {
                ConnectorCommandId = command.Id,
                ProjectId = command.ProjectId,
                EventKind = ConnectorCommandAuditEventKind.ApprovalRequested,
                Actor = normalizedActor,
                Message = "Connector command is waiting for manual approval.",
                DetailsJson = "{}",
                CreatedAtUtc = now
            });
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var duplicate = await TryResolveDuplicateEnqueueAsync(
                request.ProjectId,
                normalizedPluginKey,
                normalizedCommandKey,
                normalizedIdempotencyKey,
                normalizedActor,
                cancellationToken);
            if (duplicate is not null)
            {
                return duplicate;
            }

            throw;
        }

        return new ConnectorCommandEnqueueResult(
            command.Id,
            false,
            command.Status,
            command.ApprovalState);
    }

    public Task<ConnectorCommandStatus?> ProcessAsync(
        Guid commandId,
        CancellationToken cancellationToken = default)
    {
        return ProcessDirectAsync(commandId, DefaultLeaseDuration, cancellationToken);
    }

    public async Task<int> ProcessPendingAsync(
        int take = 20,
        TimeSpan? leaseDuration = null,
        int? maxParallelism = null,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return 0;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ConnectorCommandSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var now = clock.GetUtcNow();
        var effectiveLeaseDuration = leaseDuration is null || leaseDuration.Value <= TimeSpan.Zero
            ? DefaultLeaseDuration
            : leaseDuration.Value;
        if (dbContext.Database.IsNpgsql())
        {
            var claimedCommands = await ClaimPendingCommandsPostgreSqlAsync(
                dbContext,
                take,
                now,
                effectiveLeaseDuration,
                cancellationToken);

            return await ProcessClaimedPostgreSqlBatchAsync(
                claimedCommands,
                take,
                maxParallelism,
                cancellationToken);
        }

        var commandIds = await dbContext.Set<ConnectorCommandRecord>()
            .Where(item => item.Status == ConnectorCommandStatus.Pending)
            .Where(item => item.ApprovalState != ConnectorCommandApprovalState.Pending)
            .Where(item => item.NextAttemptAtUtc == null || item.NextAttemptAtUtc <= now)
            .Where(item => item.LeaseExpiresAtUtc == null || item.LeaseExpiresAtUtc <= now)
            .OrderBy(item => item.NextAttemptAtUtc ?? item.CreatedAtUtc)
            .ThenBy(item => item.CreatedAtUtc)
            .Take(take)
            .Select(item => item.Id)
            .ToListAsync(cancellationToken);

        var processedCount = 0;
        foreach (var commandId in commandIds)
        {
            var leaseToken = await TryClaimCommandAsync(commandId, effectiveLeaseDuration, cancellationToken);
            if (leaseToken is null)
            {
                continue;
            }

            if (await commandProcessor.ProcessAsync(commandId, leaseToken, cancellationToken) is not null)
            {
                processedCount++;
            }
        }

        return processedCount;
    }

    private static async Task<IReadOnlyList<ClaimedConnectorCommand>> ClaimPendingCommandsPostgreSqlAsync(
        AppDbContext dbContext,
        int take,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;
        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                WITH due AS (
                    SELECT c."Id"
                    FROM "Workspace_ConnectorCommands" AS c
                    WHERE c."Status" = @pendingStatus
                      AND c."ApprovalState" <> @pendingApproval
                      AND (c."NextAttemptAtUtc" IS NULL OR c."NextAttemptAtUtc" <= @now)
                      AND (c."LeaseExpiresAtUtc" IS NULL OR c."LeaseExpiresAtUtc" <= @now)
                    ORDER BY COALESCE(c."NextAttemptAtUtc", c."CreatedAtUtc"), c."CreatedAtUtc"
                    FOR UPDATE SKIP LOCKED
                    LIMIT @take
                )
                UPDATE "Workspace_ConnectorCommands" AS c
                SET "LeaseToken" = concat(@tokenPrefix, replace(c."Id"::text, '-', '')),
                    "LeaseExpiresAtUtc" = @leaseExpiresAtUtc,
                    "UpdatedAtUtc" = @now
                FROM due
                WHERE c."Id" = due."Id"
                RETURNING c."Id", c."LeaseToken", c."ProjectId", c."ConnectorPluginKey", c."CommandKey";
                """;
            AddParameter(command, "@pendingStatus", (int)ConnectorCommandStatus.Pending);
            AddParameter(command, "@pendingApproval", (int)ConnectorCommandApprovalState.Pending);
            AddParameter(command, "@now", now);
            AddParameter(command, "@leaseExpiresAtUtc", now.Add(leaseDuration));
            AddParameter(command, "@take", take);
            AddParameter(command, "@tokenPrefix", $"{Guid.NewGuid():N}:");

            var claims = new List<ClaimedConnectorCommand>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                claims.Add(new ClaimedConnectorCommand(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    BuildConnectorPartitionKey(
                        reader.IsDBNull(2) ? null : reader.GetGuid(2),
                        reader.GetString(3),
                        reader.GetString(4))));
            }

            return claims;
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<bool> ApproveAsync(
        Guid commandId,
        string approvedBy,
        string? note = null,
        CancellationToken cancellationToken = default)
    {
        return await UpdateApprovalStateAsync(
            commandId,
            ConnectorCommandApprovalState.Approved,
            NormalizeActor(approvedBy),
            string.IsNullOrWhiteSpace(note)
                ? "Connector command was approved."
                : note.Trim(),
            ConnectorCommandAuditEventKind.Approved,
            ConnectorCommandStatus.Pending,
            cancellationToken);
    }

    public async Task<bool> RejectAsync(
        Guid commandId,
        string rejectedBy,
        string? note = null,
        CancellationToken cancellationToken = default)
    {
        return await UpdateApprovalStateAsync(
            commandId,
            ConnectorCommandApprovalState.Rejected,
            NormalizeActor(rejectedBy),
            string.IsNullOrWhiteSpace(note)
                ? "Connector command was rejected."
                : note.Trim(),
            ConnectorCommandAuditEventKind.Rejected,
            ConnectorCommandStatus.Rejected,
            cancellationToken);
    }

    public async Task<bool> ReplayAsync(
        Guid commandId,
        string replayedBy,
        string? note = null,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ConnectorCommandSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var command = await dbContext.Set<ConnectorCommandRecord>()
            .FirstOrDefaultAsync(item => item.Id == commandId, cancellationToken);
        if (command is null || command.Status != ConnectorCommandStatus.DeadLettered)
        {
            return false;
        }

        var now = clock.GetUtcNow();
        command.Status = ConnectorCommandStatus.Pending;
        command.NextAttemptAtUtc = now;
        command.CompletedAtUtc = null;
        command.LastError = string.Empty;
        command.LeaseToken = string.Empty;
        command.LeaseExpiresAtUtc = null;
        command.UpdatedAtUtc = now;
        dbContext.Set<ConnectorCommandAuditRecord>().Add(new ConnectorCommandAuditRecord
        {
            ConnectorCommandId = command.Id,
            ProjectId = command.ProjectId,
            EventKind = ConnectorCommandAuditEventKind.Replayed,
            Actor = NormalizeActor(replayedBy),
            Message = string.IsNullOrWhiteSpace(note)
                ? "Connector command was replayed from dead-letter state."
                : note.Trim(),
            DetailsJson = "{}",
            CreatedAtUtc = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ConnectorCommandSnapshot?> GetAsync(
        Guid commandId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ConnectorCommandSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var command = await dbContext.Set<ConnectorCommandRecord>()
            .FirstOrDefaultAsync(item => item.Id == commandId, cancellationToken);
        return command is null
            ? null
            : MapSnapshot(command);
    }

    public async Task<IReadOnlyList<ConnectorCommandAuditEntry>> ListAuditAsync(
        Guid commandId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ConnectorCommandSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var audit = await dbContext.Set<ConnectorCommandAuditRecord>()
            .Where(item => item.ConnectorCommandId == commandId)
            .Select(item => new ConnectorCommandAuditEntry(
                item.Id,
                item.ConnectorCommandId,
                item.ProjectId,
                item.EventKind,
                item.Actor,
                item.Message,
                item.DetailsJson,
                item.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return audit
            .OrderBy(item => item.CreatedAtUtc)
            .ToList();
    }

    private async Task<bool> UpdateApprovalStateAsync(
        Guid commandId,
        ConnectorCommandApprovalState approvalState,
        string actor,
        string message,
        ConnectorCommandAuditEventKind auditEventKind,
        ConnectorCommandStatus status,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ConnectorCommandSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var command = await dbContext.Set<ConnectorCommandRecord>()
            .FirstOrDefaultAsync(item => item.Id == commandId, cancellationToken);
        if (command is null)
        {
            return false;
        }

        var now = clock.GetUtcNow();
        command.ApprovalState = approvalState;
        command.Status = status;
        command.LeaseToken = string.Empty;
        command.LeaseExpiresAtUtc = null;
        command.UpdatedAtUtc = now;
        if (approvalState == ConnectorCommandApprovalState.Approved)
        {
            command.NextAttemptAtUtc = now;
        }
        else
        {
            command.CompletedAtUtc = null;
            command.NextAttemptAtUtc = null;
        }

        dbContext.Set<ConnectorCommandAuditRecord>().Add(new ConnectorCommandAuditRecord
        {
            ConnectorCommandId = command.Id,
            ProjectId = command.ProjectId,
            EventKind = auditEventKind,
            Actor = actor,
            Message = message,
            DetailsJson = "{}",
            CreatedAtUtc = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static ConnectorCommandSnapshot MapSnapshot(ConnectorCommandRecord command)
    {
        return new ConnectorCommandSnapshot(
            command.Id,
            command.ProjectId,
            command.ConnectorPluginKey,
            command.CommandKey,
            command.IdempotencyKey,
            command.Status,
            command.ApprovalState,
            command.AttemptCount,
            command.LastAttemptAtUtc,
            command.NextAttemptAtUtc,
            command.CompletedAtUtc,
            command.LastError,
            command.ResultJson,
            command.RequestedBy,
            command.CreatedAtUtc,
            command.UpdatedAtUtc);
    }

    private static string NormalizeActor(string actor)
    {
        return string.IsNullOrWhiteSpace(actor)
            ? "system"
            : actor.Trim();
    }

    private async Task<string?> TryClaimCommandAsync(
        Guid commandId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ConnectorCommandSchemaInitializer.EnsureAsync(dbContext, cancellationToken);

        var now = clock.GetUtcNow();
        var leaseToken = Guid.NewGuid().ToString("N");
        var leaseExpiresAtUtc = now.Add(leaseDuration);
        var updatedRows = await dbContext.Set<ConnectorCommandRecord>()
            .Where(item => item.Id == commandId)
            .Where(item => item.Status == ConnectorCommandStatus.Pending)
            .Where(item => item.ApprovalState != ConnectorCommandApprovalState.Pending)
            .Where(item => item.NextAttemptAtUtc == null || item.NextAttemptAtUtc <= now)
            .Where(item => item.LeaseExpiresAtUtc == null || item.LeaseExpiresAtUtc <= now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.LeaseToken, leaseToken)
                .SetProperty(item => item.LeaseExpiresAtUtc, leaseExpiresAtUtc)
                .SetProperty(item => item.UpdatedAtUtc, now), cancellationToken);

        return updatedRows == 0
            ? null
            : leaseToken;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private async Task<int> ProcessClaimedPostgreSqlBatchAsync(
        IReadOnlyList<ClaimedConnectorCommand> claimedCommands,
        int take,
        int? maxParallelism,
        CancellationToken cancellationToken)
    {
        if (claimedCommands.Count == 0)
        {
            return 0;
        }

        var boundedParallelism = ResolveBatchParallelism(maxParallelism, take);
        using var throttler = new SemaphoreSlim(boundedParallelism, boundedParallelism);
        var processedCount = 0;
        var tasks = claimedCommands
            .GroupBy(command => command.PartitionKey, StringComparer.Ordinal)
            .Select(async group =>
            {
                await throttler.WaitAsync(cancellationToken);
                try
                {
                    foreach (var command in group)
                    {
                        if (await commandProcessor.ProcessAsync(command.Id, command.LeaseToken, cancellationToken) is not null)
                        {
                            Interlocked.Increment(ref processedCount);
                        }
                    }
                }
                finally
                {
                    throttler.Release();
                }
            })
            .ToArray();

        await Task.WhenAll(tasks);
        return processedCount;
    }

    private static int ResolveBatchParallelism(int? maxParallelism, int take)
    {
        var requested = maxParallelism.GetValueOrDefault(1);
        if (requested <= 0)
        {
            requested = 1;
        }

        return Math.Clamp(requested, 1, Math.Max(1, take));
    }

    private static string BuildConnectorPartitionKey(Guid? projectId, string connectorPluginKey, string commandKey)
    {
        var projectPartition = projectId?.ToString("N") ?? "global";
        return $"{projectPartition}:{connectorPluginKey}:{commandKey}";
    }

    private sealed record ClaimedConnectorCommand(Guid Id, string LeaseToken, string PartitionKey);

    private async Task<ConnectorCommandStatus?> ProcessDirectAsync(
        Guid commandId,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        var leaseToken = await TryClaimCommandAsync(commandId, leaseDuration, cancellationToken);
        if (leaseToken is null)
        {
            var snapshot = await GetAsync(commandId, cancellationToken);
            return snapshot?.Status;
        }

        return await commandProcessor.ProcessAsync(commandId, leaseToken, cancellationToken);
    }

    private async Task<ConnectorCommandEnqueueResult?> TryResolveDuplicateEnqueueAsync(
        Guid projectId,
        string connectorPluginKey,
        string commandKey,
        string idempotencyKey,
        string actor,
        CancellationToken cancellationToken)
    {
        await using var verificationContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ConnectorCommandSchemaInitializer.EnsureAsync(verificationContext, cancellationToken);
        var existing = await verificationContext.Set<ConnectorCommandRecord>()
            .FirstOrDefaultAsync(item =>
                    item.ProjectId == projectId &&
                    item.ConnectorPluginKey == connectorPluginKey &&
                    item.CommandKey == commandKey &&
                    item.IdempotencyKey == idempotencyKey,
                cancellationToken);
        if (existing is null)
        {
            return null;
        }

        verificationContext.Set<ConnectorCommandAuditRecord>().Add(new ConnectorCommandAuditRecord
        {
            ConnectorCommandId = existing.Id,
            ProjectId = existing.ProjectId,
            EventKind = ConnectorCommandAuditEventKind.IdempotencyHit,
            Actor = actor,
            Message = "Duplicate connector command enqueue returned the existing durable command.",
            DetailsJson = "{}",
            CreatedAtUtc = clock.GetUtcNow()
        });
        await verificationContext.SaveChangesAsync(cancellationToken);

        return new ConnectorCommandEnqueueResult(
            existing.Id,
            true,
            existing.Status,
            existing.ApprovalState);
    }

}
