using System.Data;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Entities;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Repositories;

public sealed class EfLlmChatOperationEventRepository(AppDbContext dbContext)
    : ILlmChatOperationEventRepository
{
    public async Task<LlmChatOperationEvent> AppendAsync(
        LlmChatOperationId operationId,
        Func<long, LlmChatOperationEvent> createEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(createEvent);
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Appending an LLM Chat operation event requires an active transaction.");
        }

        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.Transaction = dbContext.Database.CurrentTransaction.GetDbTransaction();
        command.CommandText =
            """
            UPDATE "LlmChats_Operations"
            SET "LastEventSequence" = "LastEventSequence" + 1
            WHERE "Id" = @operationId
            RETURNING "LastEventSequence"
            """;
        var operationParameter = command.CreateParameter();
        operationParameter.ParameterName = "operationId";
        operationParameter.Value = operationId.Value;
        command.Parameters.Add(operationParameter);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        var nextSequence = result is long sequence ? sequence : 0;
        if (nextSequence < 1)
        {
            throw new InvalidOperationException("The LLM Chat operation event target does not exist.");
        }

        var appended = createEvent(nextSequence);
        if (appended.OperationId != operationId || appended.Sequence != nextSequence)
        {
            throw new InvalidOperationException("The LLM Chat operation event factory changed journal identity.");
        }

        dbContext.Add(ToRow(appended));
        return appended;
    }

    public async Task<LlmChatOperationEventPage?> ListAfterAsync(
        LlmChatOperationId operationId,
        long afterSequence,
        int take,
        CancellationToken cancellationToken = default)
    {
        await using var snapshot = dbContext.Database.CurrentTransaction is null
            ? await dbContext.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken)
                .ConfigureAwait(false)
            : null;
        var operationRow = await dbContext.Set<LlmChatOperationRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == operationId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (operationRow is null)
        {
            if (snapshot is not null)
            {
                await snapshot.CommitAsync(cancellationToken).ConfigureAwait(false);
            }

            return null;
        }

        var rows = await dbContext.Set<LlmChatOperationEventRow>()
            .AsNoTracking()
            .Where(row => row.OperationId == operationId.Value && row.Sequence > afterSequence)
            .OrderBy(row => row.Sequence)
            .Take(take)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var earliestRetainedSequence = await dbContext.Set<LlmChatOperationEventRow>()
            .Where(row => row.OperationId == operationId.Value)
            .MinAsync(row => (long?)row.Sequence, cancellationToken)
            .ConfigureAwait(false);
        var textCharactersThroughCursor = await dbContext.Set<LlmChatOperationEventRow>()
            .Where(row =>
                row.OperationId == operationId.Value &&
                row.Sequence <= afterSequence &&
                row.Kind == LlmChatOperationEventKind.TextDelta)
            .SumAsync(row => row.Text.Length, cancellationToken)
            .ConfigureAwait(false);
        var page = new LlmChatOperationEventPage(
            LlmChatPersistenceMapper.ToDomain(operationRow),
            [.. rows.Select(ToDomain)],
            earliestRetainedSequence,
            operationRow.LastEventSequence,
            textCharactersThroughCursor);
        if (snapshot is not null)
        {
            await snapshot.CommitAsync(cancellationToken).ConfigureAwait(false);
        }

        return page;
    }

    public Task<long?> TryGetLatestSequenceAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
        => dbContext.Set<LlmChatOperationRow>()
            .Where(row => row.Id == operationId.Value)
            .Select(row => (long?)row.LastEventSequence)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<int> DeleteExpiredTerminalEventsAsync(
        DateTimeOffset completedBeforeUtc,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (!dbContext.Database.IsRelational())
        {
            var candidates = await (
                    from operation in dbContext.Set<LlmChatOperationRow>()
                    join operationEvent in dbContext.Set<LlmChatOperationEventRow>()
                        on operation.Id equals operationEvent.OperationId
                    where (operation.Status == LlmChatOperationStatus.Succeeded ||
                           operation.Status == LlmChatOperationStatus.Failed ||
                           operation.Status == LlmChatOperationStatus.Cancelled) &&
                          operation.CompletedAtUtc < completedBeforeUtc
                    orderby operation.CompletedAtUtc, operationEvent.OperationId, operationEvent.Sequence
                    select operationEvent)
                .Take(take)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
            dbContext.RemoveRange(candidates);
            return candidates.Length;
        }

        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Deleting LLM Chat operation events requires an active transaction.");
        }

        return await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            WITH candidates AS
            (
                SELECT event."OperationId", event."Sequence"
                FROM "LlmChats_Operations" AS operation
                INNER JOIN "LlmChats_OperationEvents" AS event
                    ON event."OperationId" = operation."Id"
                WHERE operation."Status" IN (
                    {(int)LlmChatOperationStatus.Succeeded},
                    {(int)LlmChatOperationStatus.Failed},
                    {(int)LlmChatOperationStatus.Cancelled})
                  AND operation."CompletedAtUtc" < {completedBeforeUtc}
                ORDER BY operation."CompletedAtUtc", event."OperationId", event."Sequence"
                LIMIT {take}
                FOR UPDATE OF event SKIP LOCKED
            )
            DELETE FROM "LlmChats_OperationEvents" AS event
            USING candidates
            WHERE event."OperationId" = candidates."OperationId"
              AND event."Sequence" = candidates."Sequence"
            """,
            cancellationToken)
            .ConfigureAwait(false);
    }

    private static LlmChatOperationEventRow ToRow(LlmChatOperationEvent operationEvent)
    {
        var row = new LlmChatOperationEventRow
        {
            OperationId = operationEvent.OperationId.Value,
            Sequence = operationEvent.Sequence,
            Kind = operationEvent.Kind,
            OccurredAtUtc = operationEvent.OccurredAtUtc
        };
        switch (operationEvent)
        {
            case LlmChatOperationStateChangedEvent state:
                row.Status = state.Status;
                row.FailureCode = state.FailureCode;
                row.Model = state.Model;
                SetUsage(row, state.Usage);
                break;
            case LlmChatOperationAttemptStartedEvent started:
                row.AttemptOrdinal = started.AttemptOrdinal;
                row.Model = started.Model;
                row.DeliveryMode = started.DeliveryMode;
                break;
            case LlmChatOperationAttemptFinishedEvent finished:
                row.AttemptOrdinal = finished.AttemptOrdinal;
                row.InvocationOutcome = finished.Outcome;
                row.Model = finished.Model;
                row.FinishReason = finished.FinishReason;
                row.DeliveryMode = finished.DeliveryMode;
                row.FailureCode = finished.FailureCode;
                SetUsage(row, finished.Usage);
                break;
            case LlmChatOperationTextDeltaEvent delta:
                row.AttemptOrdinal = delta.AttemptOrdinal;
                row.Text = delta.Text;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operationEvent), operationEvent.Kind, "Unknown operation event.");
        }

        return row;
    }

    private static LlmChatOperationEvent ToDomain(LlmChatOperationEventRow row)
        => row.Kind switch
        {
            LlmChatOperationEventKind.StateChanged => new LlmChatOperationStateChangedEvent(
                new(row.OperationId),
                row.Sequence,
                row.Status ?? throw InvalidRow(row),
                row.OccurredAtUtc,
                row.FailureCode,
                row.Model,
                GetUsage(row)),
            LlmChatOperationEventKind.AttemptStarted => new LlmChatOperationAttemptStartedEvent(
                new(row.OperationId),
                row.Sequence,
                row.AttemptOrdinal ?? throw InvalidRow(row),
                row.Model,
                row.DeliveryMode ?? throw InvalidRow(row),
                row.OccurredAtUtc),
            LlmChatOperationEventKind.AttemptFinished => new LlmChatOperationAttemptFinishedEvent(
                new(row.OperationId),
                row.Sequence,
                row.AttemptOrdinal ?? throw InvalidRow(row),
                row.Model,
                row.FinishReason,
                row.DeliveryMode ?? throw InvalidRow(row),
                row.InvocationOutcome ?? throw InvalidRow(row),
                GetUsage(row) ?? throw InvalidRow(row),
                row.OccurredAtUtc,
                row.FailureCode),
            LlmChatOperationEventKind.TextDelta => new LlmChatOperationTextDeltaEvent(
                new(row.OperationId),
                row.Sequence,
                row.AttemptOrdinal ?? throw InvalidRow(row),
                row.Text,
                row.OccurredAtUtc),
            _ => throw InvalidRow(row)
        };

    private static void SetUsage(LlmChatOperationEventRow row, LlmUsage? usage)
    {
        row.InputTokens = usage?.InputTokens;
        row.OutputTokens = usage?.OutputTokens;
        row.CachedInputTokens = usage?.CachedInputTokens;
    }

    private static LlmUsage? GetUsage(LlmChatOperationEventRow row)
        => row.InputTokens is null && row.OutputTokens is null && row.CachedInputTokens is null
            ? null
            : new LlmUsage(
                row.InputTokens ?? throw InvalidRow(row),
                row.OutputTokens ?? throw InvalidRow(row),
                row.CachedInputTokens ?? 0);

    private static InvalidDataException InvalidRow(LlmChatOperationEventRow row)
        => new($"LLM Chat operation event {row.OperationId:N}/{row.Sequence} is invalid.");
}
