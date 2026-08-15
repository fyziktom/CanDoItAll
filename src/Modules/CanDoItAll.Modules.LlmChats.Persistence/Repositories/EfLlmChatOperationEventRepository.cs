using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Persistence.Entities;
using CanDoItAll.Modules.LlmChats.Ports;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.LlmChats.Persistence.Repositories;

public sealed class EfLlmChatOperationEventRepository(AppDbContext dbContext)
    : ILlmChatOperationEventRepository
{
    public async Task<LlmChatOperationEvent> AppendAsync(
        LlmChatOperationId operationId,
        Func<long, LlmChatOperationEvent> createEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(createEvent);
        var operation = await dbContext.Set<LlmChatOperationRow>()
            .FromSqlInterpolated($"""
                SELECT *
                FROM "LlmChats_Operations"
                WHERE "Id" = {operationId.Value}
                FOR UPDATE
                """)
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException("The LLM Chat operation event target does not exist.");
        var lastSequence = await dbContext.Set<LlmChatOperationEventRow>()
            .Where(row => row.OperationId == operation.Id)
            .MaxAsync(row => (long?)row.Sequence, cancellationToken)
            .ConfigureAwait(false) ?? 0;
        var appended = createEvent(checked(lastSequence + 1));
        if (appended.OperationId != operationId || appended.Sequence != lastSequence + 1)
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
        var operationRow = await dbContext.Set<LlmChatOperationRow>()
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == operationId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (operationRow is null)
        {
            return null;
        }

        var rows = await dbContext.Set<LlmChatOperationEventRow>()
            .AsNoTracking()
            .Where(row => row.OperationId == operationId.Value && row.Sequence > afterSequence)
            .OrderBy(row => row.Sequence)
            .Take(take)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        var earliest = await dbContext.Set<LlmChatOperationEventRow>()
            .Where(row => row.OperationId == operationId.Value)
            .MinAsync(row => (long?)row.Sequence, cancellationToken)
            .ConfigureAwait(false);
        return new(
            LlmChatPersistenceMapper.ToDomain(operationRow),
            [.. rows.Select(ToDomain)],
            earliest);
    }

    public async Task<int> DeleteExpiredTerminalEventsAsync(
        DateTimeOffset completedBeforeUtc,
        int take,
        CancellationToken cancellationToken = default)
    {
        var operationIds = await dbContext.Set<LlmChatOperationRow>()
            .AsNoTracking()
            .Where(row =>
                (row.Status == LlmChatOperationStatus.Succeeded ||
                 row.Status == LlmChatOperationStatus.Failed ||
                 row.Status == LlmChatOperationStatus.Cancelled) &&
                row.CompletedAtUtc < completedBeforeUtc)
            .OrderBy(row => row.CompletedAtUtc)
            .Select(row => row.Id)
            .Take(take)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        if (operationIds.Length == 0)
        {
            return 0;
        }

        return await dbContext.Set<LlmChatOperationEventRow>()
            .Where(row => operationIds.Contains(row.OperationId))
            .ExecuteDeleteAsync(cancellationToken)
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
