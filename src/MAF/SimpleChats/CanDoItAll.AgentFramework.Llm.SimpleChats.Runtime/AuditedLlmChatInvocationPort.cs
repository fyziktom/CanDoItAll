using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Runtime;

public sealed class AuditedLlmChatInvocationPort(
    ILlmInvocationPort inner,
    ILlmChatOperationEvidenceSink evidenceSink,
    IProviderModelCapabilityResolver capabilityResolver,
    ILlmChatOperationScopeAccessor operationScope,
    TimeProvider timeProvider) : ILlmInvocationPort
{
    public async Task<LlmInvocationResult> InvokeAsync(
        LlmInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var operationId = operationScope.Current?.OperationId
            ?? throw new InvalidOperationException("An LLM Chat operation scope is required for invocation audit.");
        request = LlmChatHistoryContext.Attach(request, operationId, operationScope.Current?.HistoryCaller);
        cancellationToken.ThrowIfCancellationRequested();
        var startedAtUtc = timeProvider.GetUtcNow();
        await evidenceSink.MarkProviderDispatchStartedAsync(
                operationId,
                new LlmStreamingAttemptStarted(
                    1,
                    request.Provider.Id,
                    request.Provider.Kind,
                    request.Model,
                    LlmStreamingDeliveryMode.CompletedFallback,
                    startedAtUtc),
                cancellationToken)
            .ConfigureAwait(false);
        var requestedEffort = request.Settings?.ThinkingEffort;
        var effectiveEffort = requestedEffort ??
            capabilityResolver.ResolveProviderDefaultThinkingEffort(request.Provider, request.Model);
        try
        {
            var result = await inner.InvokeAsync(request, cancellationToken).ConfigureAwait(false);
            await RecordAsync(
                request,
                operationId,
                requestedEffort,
                effectiveEffort,
                result.Usage,
                LlmChatInvocationEvidenceCapture.ResolveReturnedUsage(result.Usage),
                LlmChatInvocationOutcome.Succeeded,
                string.Empty,
                startedAtUtc).ConfigureAwait(false);
            return result;
        }
        catch (LlmInvocationException exception)
        {
            await RecordAsync(
                request,
                operationId,
                requestedEffort,
                effectiveEffort,
                exception.Usage ?? LlmUsage.Zero,
                exception.Usage is null
                    ? LlmChatInvocationUsageEvidenceStatus.MissingAfterProviderActivity
                    : LlmChatInvocationUsageEvidenceStatus.Observed,
                LlmChatInvocationOutcome.Failed,
                MapFailureCode(exception.Kind),
                startedAtUtc).ConfigureAwait(false);
            throw;
        }
        catch (OperationCanceledException)
        {
            await RecordAsync(
                request,
                operationId,
                requestedEffort,
                effectiveEffort,
                LlmUsage.Zero,
                LlmChatInvocationUsageEvidenceStatus.MissingAfterProviderActivity,
                LlmChatInvocationOutcome.Cancelled,
                LlmChatErrorCodes.Cancelled,
                startedAtUtc).ConfigureAwait(false);
            throw;
        }
    }

    private Task<LlmChatOperation> RecordAsync(
        LlmInvocationRequest request,
        LlmChatOperationId operationId,
        AgentReasoningEffortLevel? requestedEffort,
        AgentReasoningEffortLevel? effectiveEffort,
        LlmUsage usage,
        LlmChatInvocationUsageEvidenceStatus usageStatus,
        LlmChatInvocationOutcome outcome,
        string failureCode,
        DateTimeOffset startedAtUtc)
    {
        var pricing = LlmChatInvocationEvidenceCapture.CapturePricing(request, request.Model, usage, usageStatus);
        return evidenceSink.RecordInvocationAsync(new LlmChatInvocationRecord(
            operationId,
            request.Provider.Id,
            request.Provider.Kind,
            request.Provider.Name,
            request.Model,
            requestedEffort,
            effectiveEffort,
            1,
            usage,
            outcome,
            failureCode,
            startedAtUtc,
            timeProvider.GetUtcNow(),
            request.CorrelationId,
            usageStatus: usageStatus,
            pricingStatus: pricing.Status,
            providerCostUsd: pricing.ProviderCostUsd,
            calculatedCostUsd: pricing.CalculatedCostUsd,
            pricingProfileHash: pricing.PricingProfileHash,
            pricingVersion: pricing.PricingVersion) {
                HistoryAttempts = request.History.Attempts.EvidenceSnapshot()
            }, CancellationToken.None);
    }

    internal static string MapFailureCode(LlmInvocationFailureKind failureKind)
        => failureKind switch
        {
            LlmInvocationFailureKind.InvalidRequest => LlmChatErrorCodes.ModelSettingsInvalid,
            LlmInvocationFailureKind.DeadlineExceeded => LlmChatErrorCodes.DeadlineExceeded,
            _ => LlmChatErrorCodes.ProviderUnavailable
        };
}
