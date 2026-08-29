using System.Runtime.CompilerServices;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class ProviderHistoryChatClient(IChatClient inner, ProviderProfile provider, string model,
    IProviderHistoryRecorder recorder, TimeProvider clock) : DelegatingChatClient(inner) {
    public override async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        CancellationToken cancellationToken = default) {
        var observation = await BeginAsync(options, cancellationToken);
        return await observation.ExecuteAsync(() => base.GetResponseAsync(messages, ProviderHistoryChatContext.ForTransport(options), cancellationToken),
            response => new(Usage(response.Usage),
                observation.Start.ContentOwner is null && observation.Start.Policy.Policy.CaptureMode == HistoryCaptureMode.Detailed
                    ? response.Text : null), cancellationToken);
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
        ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        var observation = await BeginAsync(options, cancellationToken);
        var response = new HistoryResponseBuffer(observation.Start);
        UsageDetails? usage = null;
        var outcome = HistoryOutcome.Cancelled;
        var terminalObserved = false;
        try {
            await using var enumerator = base.GetStreamingResponseAsync(messages, ProviderHistoryChatContext.ForTransport(options),
                cancellationToken).GetAsyncEnumerator(cancellationToken);
            while (true) {
                bool moved;
                try {
                    moved = await enumerator.MoveNextAsync();
                } catch (OperationCanceledException) {
                    outcome = terminalObserved ? HistoryOutcome.Succeeded : HistoryOutcome.Cancelled;
                    throw;
                } catch {
                    outcome = terminalObserved ? HistoryOutcome.Succeeded : HistoryOutcome.Failed;
                    throw;
                }
                if (!moved) {
                    outcome = HistoryOutcome.Succeeded;
                    break;
                }
                var update = enumerator.Current;
                foreach (var content in update.Contents) {
                    if (content is UsageContent observed) {
                        usage ??= new();
                        usage.Add(observed.Details);
                    } else if (content is TextContent text) {
                        response.Append(text.Text);
                    }
                }
                if (update.FinishReason is not null) {
                    terminalObserved = true;
                    outcome = HistoryOutcome.Succeeded;
                }
                yield return update;
            }
        } finally {
            await observation.CompleteAsync(outcome, new(Usage(usage), response.GetText(), ResponseOriginalBytes: response.OriginalBytes));
        }
    }

    private Task<ProviderHistoryObservation> BeginAsync(ChatOptions? options, CancellationToken cancellationToken) =>
        ProviderHistoryObservation.BeginAsync(recorder, clock, provider, options?.ModelId ?? model, HistoryOperation.CompleteChat,
            ProviderHistoryChatContext.Read(options) ?? HistoryInvocationContext.Create(HistoryWorkload.Agent), cancellationToken);

    private static HistoryUsage Usage(UsageDetails? usage) =>
        ProviderHistoryObservation.Tokens(usage?.InputTokenCount, usage?.OutputTokenCount, usage?.CachedInputTokenCount);
}
