using CanDoItAll.Modules.LlmChats.Application;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Ports;
using CanDoItAll.Modules.Workspace.ApiAccess;

namespace CanDoItAll.Web.Api.Streaming;

internal sealed class LlmChatOperationEventReplayReader(
    LlmChatOperationEventStreamSession session,
    ApiServerSentEventsOptions options) : IBoundedReplayEventReader<LlmChatOperationEventApiResponse>
{
    public TimeSpan HeartbeatInterval => options.HeartbeatInterval;

    public async ValueTask<BoundedReplayReadResult<LlmChatOperationEventApiResponse>> ReadAsync(
        long afterExclusive,
        CancellationToken cancellationToken)
    {
        LlmChatOperationEventPage page;
        try
        {
            page = await session.ReadAsync(
                afterExclusive,
                Math.Min(options.MaxBatchSize, session.MaximumPageSize),
                options.HeartbeatInterval,
                cancellationToken).ConfigureAwait(false);
        }
        catch (LlmChatRuntimeProfileChangedException exception)
            when (session.ProfileLifetime.IsCancellationRequested)
        {
            throw new OperationCanceledException(
                "The LLM Chat event stream's runtime profile changed.",
                exception,
                session.ProfileLifetime);
        }

        var aggregateCharacterCount = page.TextCharactersThroughCursor;
        var events = new List<SequencedServerEvent<LlmChatOperationEventApiResponse>>(page.Events.Count);
        foreach (var operationEvent in page.Events)
        {
            if (operationEvent is LlmChatOperationTextDeltaEvent delta)
            {
                aggregateCharacterCount = checked(aggregateCharacterCount + delta.Text.Length);
            }

            events.Add(new(
                operationEvent.Sequence,
                LlmChatOperationEventApiMapper.ToResponse(
                    page.Operation,
                    operationEvent,
                    aggregateCharacterCount)));
        }

        var gap = CreateGap(page, afterExclusive);
        var deliveredThrough = events.Count > 0
            ? events[^1].Sequence
            : gap?.ResumeAfterSequence ?? afterExclusive;
        var isTerminal = IsStreamTerminal(page.Operation.Status) && deliveredThrough >= page.LatestSequence;
        return new(events, gap, isTerminal);
    }

    private static ReplayGap? CreateGap(LlmChatOperationEventPage page, long afterExclusive)
    {
        var statusUrl = LlmChatOperationApiRoutes.Status(page.Operation.Id.Value);
        if (afterExclusive > page.LatestSequence)
        {
            return new ReplayGap(
                ReplayGapReason.CursorAheadOfStream,
                afterExclusive,
                page.EarliestRetainedSequence ?? page.LatestSequence,
                page.LatestSequence,
                page.LatestSequence,
                statusUrl);
        }

        if (page.EarliestRetainedSequence is { } earliest && afterExclusive < earliest - 1)
        {
            return new ReplayGap(
                ReplayGapReason.CursorBeforeRetention,
                afterExclusive,
                earliest,
                page.LatestSequence,
                earliest - 1,
                statusUrl);
        }

        if (page.EarliestRetainedSequence is null && IsStreamTerminal(page.Operation.Status))
        {
            return new ReplayGap(
                ReplayGapReason.CursorBeforeRetention,
                afterExclusive,
                page.LatestSequence + 1,
                page.LatestSequence,
                page.LatestSequence,
                statusUrl);
        }

        return null;
    }

    private static bool IsStreamTerminal(LlmChatOperationStatus status)
        => status is
            LlmChatOperationStatus.Succeeded or
            LlmChatOperationStatus.Failed or
            LlmChatOperationStatus.Cancelled or
            LlmChatOperationStatus.RecoveryRequired;
}
