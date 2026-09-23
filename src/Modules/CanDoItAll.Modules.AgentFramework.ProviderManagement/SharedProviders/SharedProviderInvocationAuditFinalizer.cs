using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

internal sealed class SharedProviderInvocationAuditFinalizer(
    string requestId,
    SharedProviderRelayOperation operation,
    ProviderExecutionTariff pricing,
    SharedProviderInvocationAuditService invocationAuditService,
    IClock clock,
    ILogger logger) {
    private static readonly TimeSpan FinalizationTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(50),
        TimeSpan.FromMilliseconds(200)
    ];
    private const int MaximumAttempts = 3;
    private readonly object gate = new();
    private Task? finalization;
    private TerminalObservation? lastObservation;

    public Task SucceededAsync(SharedProviderRelayUsage usage)
        => FinalizeOnceAsync(SharedProviderInvocationOutcome.Succeeded, null, usage);

    public Task FailedAsync(
        SharedProviderFailure failure,
        SharedProviderRelayUsage usage) {
        ArgumentNullException.ThrowIfNull(failure);
        return failure.Category == SharedProviderFailureCategory.Cancelled
            ? CancelledAsync(usage)
            : FinalizeOnceAsync(
                SharedProviderInvocationOutcome.Failed,
                failure.Category,
                usage);
    }

    public Task CancelledAsync(SharedProviderRelayUsage usage)
        => FinalizeOnceAsync(
            SharedProviderInvocationOutcome.Cancelled,
            SharedProviderFailureCategory.Cancelled,
            usage);

    private Task FinalizeOnceAsync(
        SharedProviderInvocationOutcome outcome,
        SharedProviderFailureCategory? failureCategory,
        SharedProviderRelayUsage usage) {
        ArgumentNullException.ThrowIfNull(usage);
        lock (gate) {
            var incoming = new TerminalObservation(outcome, failureCategory, usage);
            if (lastObservation is { } previous) {
                if (previous == incoming || previous.Outcome != SharedProviderInvocationOutcome.Cancelled &&
                    outcome == SharedProviderInvocationOutcome.Cancelled && usage.Completeness <= previous.Usage.Completeness) {
                    return finalization!;
                }
                if (previous.Outcome != SharedProviderInvocationOutcome.Cancelled ||
                    usage.Completeness < previous.Usage.Completeness ||
                    outcome == SharedProviderInvocationOutcome.Cancelled && usage.Completeness == previous.Usage.Completeness) {
                    throw new InvalidOperationException("Conflicting relay terminal observations.");
                }
            }
            lastObservation = incoming;
            return finalization = PersistAfterAsync(finalization, incoming);
        }
    }

    private async Task PersistAfterAsync(Task? previous, TerminalObservation observation) {
        if (previous is not null) {
            try {
                await previous;
            } catch (Exception exception) {
                logger.LogWarning("Earlier audit write for request {RequestId} failed with {FailureType}; reconciling richer terminal evidence.",
                    requestId, exception.GetType().Name);
            }
        }
        await PersistAsync(observation.Outcome, observation.FailureCategory, observation.Usage);
    }

    private sealed record TerminalObservation(SharedProviderInvocationOutcome Outcome,
        SharedProviderFailureCategory? FailureCategory, SharedProviderRelayUsage Usage);

    private async Task PersistAsync(
        SharedProviderInvocationOutcome outcome,
        SharedProviderFailureCategory? failureCategory,
        SharedProviderRelayUsage usage) {
        var mappedUsage = MapUsage(operation, usage);
        var price = SharedProviderExecutionPricingResolver.Evaluate(pricing, operation, usage);
        var completion = new SharedProviderInvocationCompletion(
            outcome,
            clock.GetUtcNow(),
            failureCategory,
            mappedUsage.InputTokens,
            mappedUsage.OutputTokens,
            mappedUsage.Completeness,
            price.Amount,
            price.Kind == ProviderPriceEvidenceKind.PartialEstimate
                ? SharedProviderMetadataCompleteness.Partial
                : price.Amount.HasValue ? SharedProviderMetadataCompleteness.Complete : SharedProviderMetadataCompleteness.Unavailable) {
            ImageCount = mappedUsage.ImageCount,
            PriceEvidence = price,
            CachedInputTokenCount = usage.CachedInputTokens,
            CacheWriteTokenCount = usage.CacheWriteTokens,
            ReasoningTokenCount = usage.ReasoningTokens
        };
        using var finalizationCancellation = new CancellationTokenSource(FinalizationTimeout);
        Exception? terminalFailure = null;
        var attempts = 0;
        while (attempts < MaximumAttempts) {
            attempts++;
            try {
                await invocationAuditService.FinalizeAsync(
                    requestId,
                    completion,
                    finalizationCancellation.Token);
                return;
            }
            catch (Exception exception) {
                terminalFailure = exception;
            }

            if (attempts >= MaximumAttempts || finalizationCancellation.IsCancellationRequested) {
                break;
            }

            try {
                await Task.Delay(
                    RetryDelays[attempts - 1],
                    finalizationCancellation.Token);
            }
            catch (OperationCanceledException exception) {
                terminalFailure = exception;
                break;
            }
        }

        logger.LogWarning(
            "Shared-provider invocation audit finalization did not complete for request {RequestId} after {AttemptCount} attempt(s); durable recovery remains scheduled.",
            requestId,
            attempts);
        throw new SharedProviderInvocationTerminalizationException(terminalFailure!);
    }

    private static PersistedUsage MapUsage(
        SharedProviderRelayOperation operation,
        SharedProviderRelayUsage usage) {
        if (usage.Completeness == SharedProviderRelayUsageCompleteness.Unavailable) {
            return PersistedUsage.Unavailable;
        }

        return operation switch {
            SharedProviderRelayOperation.ChatCompletions or SharedProviderRelayOperation.Responses
                when !usage.ImageCount.HasValue => new PersistedUsage(
                    usage.InputTokens,
                    usage.OutputTokens,
                    MapCompleteness(usage.Completeness),
                    ImageCount: null),
            SharedProviderRelayOperation.ImageGenerations
                when usage.Completeness == SharedProviderRelayUsageCompleteness.Complete &&
                    usage.ImageCount.HasValue => new PersistedUsage(
                        InputTokens: null,
                        OutputTokens: null,
                        SharedProviderMetadataCompleteness.Complete,
                        usage.ImageCount),
            _ => throw new InvalidOperationException(
                $"Relay usage is incompatible with operation '{operation}'.")
        };
    }

    private static SharedProviderMetadataCompleteness MapCompleteness(
        SharedProviderRelayUsageCompleteness completeness)
        => completeness switch {
            SharedProviderRelayUsageCompleteness.Partial =>
                SharedProviderMetadataCompleteness.Partial,
            SharedProviderRelayUsageCompleteness.Complete =>
                SharedProviderMetadataCompleteness.Complete,
            _ => throw new ArgumentOutOfRangeException(nameof(completeness), completeness, null)
        };

    private sealed record PersistedUsage(
        long? InputTokens,
        long? OutputTokens,
        SharedProviderMetadataCompleteness Completeness,
        int? ImageCount) {
        public static PersistedUsage Unavailable { get; } = new(
            null,
            null,
            SharedProviderMetadataCompleteness.Unavailable,
            null);
    }
}

internal sealed class SharedProviderInvocationTerminalizationException(Exception innerException) :
    Exception("Shared-provider invocation audit finalization could not be persisted.", innerException);

