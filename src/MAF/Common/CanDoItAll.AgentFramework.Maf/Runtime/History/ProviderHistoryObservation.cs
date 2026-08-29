using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class ProviderHistoryObservation(
    IProviderHistoryRecorder recorder, TimeProvider clock, HistoryAttemptStart start, ProviderExecutionTariff tariff,
    HistoryAttemptCollection attempts) {
    public HistoryAttemptStart Start { get; } = start;

    public static async Task<ProviderHistoryObservation> BeginAsync(IProviderHistoryRecorder recorder, TimeProvider clock,
        ProviderProfile provider, string? model, HistoryOperation operation, HistoryInvocationContext context,
        CancellationToken cancellationToken) {
        var tariff = ProviderExecutionPricing.Freeze(provider.Id, model ?? "", provider.ModelPrices, provider.PricingSourceRevision ?? "unversioned-runtime-profile");
        var identity = string.IsNullOrWhiteSpace(model) ? (ProviderModelIdentity?)null : new(model);
        var start = await recorder.BeginAsync(new(new(new(provider.Id), provider.Name, provider.Kind.ToString(),
            identity, identity), operation, context), cancellationToken);
        return new(recorder, clock, start, tariff, context.Attempts);
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> invoke, Func<T, ProviderHistoryResult> observe,
        CancellationToken cancellationToken) {
        ProviderHistoryResult? evidence = null;
        var outcome = HistoryOutcome.Failed;
        try {
            var result = await invoke();
            evidence = observe(result);
            outcome = evidence.Outcome ?? HistoryOutcome.Succeeded;
            return result;
        } catch (OperationCanceledException) {
            outcome = HistoryOutcome.Cancelled;
            throw;
        } finally {
            await CompleteAsync(outcome, evidence);
        }
    }

    public async Task CompleteAsync(HistoryOutcome outcome, ProviderHistoryResult? evidence) {
        var usage = evidence?.Usage ?? new(HistoryUsageState.Unavailable);
        var price = ProviderExecutionPricing.Evaluate(tariff, usage.InputTokens, usage.CachedInputTokens,
            usage.CacheWriteTokens, usage.OutputTokens,
            Start.Operation is HistoryOperation.CompleteChat or HistoryOperation.AnalyzeImage,
            evidence?.ReportedAmount, evidence?.ReportedCurrency);
        var completion = new HistoryAttemptCompletion(outcome, clock.GetUtcNow(), usage,
            ProviderHistoryPriceMapping.From(price), evidence?.RemoteRequest) {
            ResponseOriginalBytes = evidence?.ResponseOriginalBytes
        };
        attempts.Complete(Start, completion);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try {
            await recorder.CompleteAsync(Start, completion, evidence?.Response, timeout.Token);
        } catch (Exception exception) when (exception is not ProviderHistoryException) {
            throw new ProviderHistoryException(HistoryFailure.Unavailable,
                "The provider completed, but its terminal history write failed. Inference must not be repeated automatically.", exception);
        }
    }

    public static HistoryUsage Tokens(long? input, long? output, long? cached = null, long? reasoning = null) =>
        new(input.HasValue && output.HasValue ? HistoryUsageState.Complete :
            input.HasValue || output.HasValue ? HistoryUsageState.Partial : HistoryUsageState.Unavailable,
            input, output, cached, ReasoningTokens: reasoning);
}

internal sealed record ProviderHistoryResult(
    HistoryUsage Usage, string? Response = null, decimal? ReportedAmount = null, string? ReportedCurrency = null,
    RemoteRequestReference? RemoteRequest = null, long? ResponseOriginalBytes = null) {
    public HistoryOutcome? Outcome { get; init; }
}
