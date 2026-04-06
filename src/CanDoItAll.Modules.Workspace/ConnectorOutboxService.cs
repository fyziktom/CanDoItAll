using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workspace;

public sealed class ConnectorCommandProcessor(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    IEnumerable<IConnectorCommandHandler> handlers)
{
    private const int MaxAttempts = 3;

    public async Task<ConnectorCommandStatus?> ProcessAsync(
        Guid commandId,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ConnectorCommandSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var command = await dbContext.Set<ConnectorCommandRecord>()
            .FirstOrDefaultAsync(item => item.Id == commandId, cancellationToken);
        if (command is null)
        {
            return null;
        }

        if (command.Status is ConnectorCommandStatus.Completed or ConnectorCommandStatus.DeadLettered or ConnectorCommandStatus.Rejected)
        {
            return command.Status;
        }

        if (command.ApprovalState == ConnectorCommandApprovalState.Pending)
        {
            return command.Status;
        }

        if (command.NextAttemptAtUtc.HasValue && command.NextAttemptAtUtc.Value > clock.GetUtcNow())
        {
            return command.Status;
        }

        var now = clock.GetUtcNow();
        command.AttemptCount++;
        command.LastAttemptAtUtc = now;
        command.UpdatedAtUtc = now;
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
        if (handler is null)
        {
            MarkDeadLettered(
                dbContext,
                command,
                "No connector command handler is registered for the queued operation.",
                now);
            await dbContext.SaveChangesAsync(cancellationToken);
            return command.Status;
        }

        ConnectorCommandExecutionResult executionResult;
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
        catch (Exception ex)
        {
            executionResult = ConnectorCommandExecutionResult.RetryableFailure(ex.Message);
        }

        now = clock.GetUtcNow();
        switch (executionResult.Outcome)
        {
            case ConnectorCommandExecutionOutcome.Completed:
                command.Status = ConnectorCommandStatus.Completed;
                command.CompletedAtUtc = now;
                command.NextAttemptAtUtc = null;
                command.LastError = string.Empty;
                command.ResultJson = NormalizeJson(executionResult.ResultJson);
                command.UpdatedAtUtc = now;
                dbContext.Set<ConnectorCommandAuditRecord>().Add(new ConnectorCommandAuditRecord
                {
                    ConnectorCommandId = command.Id,
                    ProjectId = command.ProjectId,
                    EventKind = ConnectorCommandAuditEventKind.Completed,
                    Actor = "system",
                    Message = "Connector command completed successfully.",
                    DetailsJson = command.ResultJson,
                    CreatedAtUtc = now
                });
                break;
            case ConnectorCommandExecutionOutcome.RetryableFailure:
                if (command.AttemptCount >= MaxAttempts)
                {
                    MarkDeadLettered(
                        dbContext,
                        command,
                        NormalizeError(executionResult.ErrorMessage, "Connector command exhausted all retry attempts."),
                        now);
                }
                else
                {
                    command.Status = ConnectorCommandStatus.Pending;
                    command.CompletedAtUtc = null;
                    command.LastError = NormalizeError(executionResult.ErrorMessage, "Connector command failed and will be retried.");
                    command.NextAttemptAtUtc = now.Add(ComputeBackoff(command.AttemptCount));
                    command.UpdatedAtUtc = now;
                    dbContext.Set<ConnectorCommandAuditRecord>().Add(new ConnectorCommandAuditRecord
                    {
                        ConnectorCommandId = command.Id,
                        ProjectId = command.ProjectId,
                        EventKind = ConnectorCommandAuditEventKind.AttemptFailed,
                        Actor = "system",
                        Message = "Connector command failed and was scheduled for retry.",
                        DetailsJson = BuildFailureDetailsJson(command.LastError, command.NextAttemptAtUtc),
                        CreatedAtUtc = now
                    });
                }
                break;
            case ConnectorCommandExecutionOutcome.PermanentFailure:
                MarkDeadLettered(
                    dbContext,
                    command,
                    NormalizeError(executionResult.ErrorMessage, "Connector command failed permanently."),
                    now);
                break;
            default:
                throw new InvalidOperationException($"Unsupported execution outcome '{executionResult.Outcome}'.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return command.Status;
    }

    private static void MarkDeadLettered(
        AppDbContext dbContext,
        ConnectorCommandRecord command,
        string errorMessage,
        DateTimeOffset occurredAtUtc)
    {
        command.Status = ConnectorCommandStatus.DeadLettered;
        command.CompletedAtUtc = null;
        command.LastError = errorMessage;
        command.NextAttemptAtUtc = null;
        command.UpdatedAtUtc = occurredAtUtc;
        dbContext.Set<ConnectorCommandAuditRecord>().Add(new ConnectorCommandAuditRecord
        {
            ConnectorCommandId = command.Id,
            ProjectId = command.ProjectId,
            EventKind = ConnectorCommandAuditEventKind.DeadLettered,
            Actor = "system",
            Message = "Connector command was moved to dead-letter state.",
            DetailsJson = BuildFailureDetailsJson(errorMessage, null),
            CreatedAtUtc = occurredAtUtc
        });
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
}

public sealed class ConnectorOutboxService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    ConnectorPluginRegistry connectorPluginRegistry,
    ConnectorCommandProcessor commandProcessor)
{
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

        await dbContext.SaveChangesAsync(cancellationToken);
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
        return commandProcessor.ProcessAsync(commandId, cancellationToken);
    }

    public async Task<int> ProcessPendingAsync(
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            return 0;
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ConnectorCommandSchemaInitializer.EnsureAsync(dbContext, cancellationToken);
        var now = clock.GetUtcNow();
        var pendingCommands = await dbContext.Set<ConnectorCommandRecord>()
            .Where(item =>
                item.Status == ConnectorCommandStatus.Pending &&
                item.ApprovalState != ConnectorCommandApprovalState.Pending)
            .ToListAsync(cancellationToken);
        var commandIds = pendingCommands
            .Where(item =>
                !item.NextAttemptAtUtc.HasValue ||
                item.NextAttemptAtUtc.Value <= now)
            .OrderBy(item => item.CreatedAtUtc)
            .Take(take)
            .Select(item => item.Id)
            .ToList();

        foreach (var commandId in commandIds)
        {
            await commandProcessor.ProcessAsync(commandId, cancellationToken);
        }

        return commandIds.Count;
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
}
