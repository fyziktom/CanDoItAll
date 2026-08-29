using System.Runtime.CompilerServices;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.Providers;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class HistoryStreamingChatDriver(IProviderStreamingChatCompletionDriver inner,
    IProviderHistoryRecorder recorder, TimeProvider clock) : IProviderStreamingChatCompletionDriver {
    public ProviderKind ProviderKind => inner.ProviderKind;
    public IReadOnlySet<AgentProviderCapabilityKind> Capabilities => inner.Capabilities;
    public ProviderDispatchLimits GetDispatchLimits(ProviderDispatchQuery query) => inner.GetDispatchLimits(query);
    public ProviderChatStreamingMode ResolveStreamingMode(ProviderChatCompletionRequest request) => inner.ResolveStreamingMode(request);

    public async IAsyncEnumerable<ProviderChatStreamingUpdate> StreamChatAsync(ProviderChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) {
        var observation = await ProviderHistoryObservation.BeginAsync(recorder, clock, request.Provider, request.Model,
            request.Attachments is { Count: > 0 } ? HistoryOperation.AnalyzeImage : HistoryOperation.CompleteChat,
            request.History, cancellationToken);
        var response = new HistoryResponseBuffer(observation.Start);
        var usage = new HistoryUsage(HistoryUsageState.Unavailable);
        var outcome = HistoryOutcome.Cancelled;
        var terminalObserved = false;
        RemoteRequestReference? remote = null;
        try {
            await using var enumerator = inner.StreamChatAsync(request, cancellationToken).GetAsyncEnumerator(cancellationToken);
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
                    outcome = terminalObserved ? HistoryOutcome.Succeeded : HistoryOutcome.Failed;
                    break;
                }
                switch (enumerator.Current) {
                    case ProviderChatCorrelationObserved correlation:
                        remote = correlation.Reference;
                        continue;
                    case ProviderChatUsageObserved observed:
                        usage = observed.Usage;
                        continue;
                    case ProviderChatTextDelta delta when !terminalObserved:
                        response.Append(delta.Text);
                        break;
                    case ProviderChatCompleted completed:
                        usage = completed.ObservedUsage ?? usage;
                        terminalObserved = true;
                        outcome = HistoryOutcome.Succeeded;
                        break;
                }
                yield return enumerator.Current;
            }
        } finally {
            await observation.CompleteAsync(outcome, new(usage, response.GetText(), RemoteRequest: remote, ResponseOriginalBytes: response.OriginalBytes));
        }
    }
}
