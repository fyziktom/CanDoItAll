using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Ports;

namespace CanDoItAll.Modules.LlmChats.Persistence;

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
        cancellationToken.ThrowIfCancellationRequested();
        var startedAtUtc = timeProvider.GetUtcNow();
        await evidenceSink.MarkProviderDispatchStartedAsync(operationId, startedAtUtc, cancellationToken)
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
                exception.Kind == LlmInvocationFailureKind.DeadlineExceeded
                    ? LlmChatInvocationOutcome.Cancelled
                    : LlmChatInvocationOutcome.Failed,
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
        LlmChatInvocationOutcome outcome,
        string failureCode,
        DateTimeOffset startedAtUtc)
        => evidenceSink.RecordInvocationAsync(new LlmChatInvocationRecord(
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
            request.CorrelationId), CancellationToken.None);

    private static string MapFailureCode(LlmInvocationFailureKind failureKind)
        => failureKind switch
        {
            LlmInvocationFailureKind.InvalidRequest => LlmChatErrorCodes.ModelSettingsInvalid,
            LlmInvocationFailureKind.DeadlineExceeded => LlmChatErrorCodes.DeadlineExceeded,
            _ => LlmChatErrorCodes.ProviderUnavailable
        };
}
