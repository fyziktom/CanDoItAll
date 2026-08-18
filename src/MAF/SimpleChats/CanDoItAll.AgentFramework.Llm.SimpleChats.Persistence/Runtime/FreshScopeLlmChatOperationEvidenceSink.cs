using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence;

internal sealed class FreshScopeLlmChatOperationEvidenceSink(
    IServiceScopeFactory scopeFactory) : ILlmChatOperationEvidenceSink
{
    public Task<LlmChatOperation> MarkTurnAdmittedAsync(
        LlmChatOperationId operationId,
        DateTimeOffset admittedAtUtc,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(
            sink => sink.MarkTurnAdmittedAsync(operationId, admittedAtUtc, cancellationToken));

    public Task<LlmChatOperation> MarkProviderDispatchStartedAsync(
        LlmChatOperationId operationId,
        LlmStreamingAttemptStarted attempt,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(
            sink => sink.MarkProviderDispatchStartedAsync(operationId, attempt, cancellationToken));

    public Task<LlmChatOperation> RecordInvocationAsync(
        LlmChatInvocationRecord record,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(sink => sink.RecordInvocationAsync(record, cancellationToken));

    public Task<LlmChatOperation> CompleteTranscriptAsync(
        LlmChatOperationId operationId,
        DateTimeOffset completedAtUtc,
        long resultingTranscriptRevision,
        Guid assistantEntryId,
        string model,
        LlmUsage usage,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(sink => sink.CompleteTranscriptAsync(
            operationId,
            completedAtUtc,
            resultingTranscriptRevision,
            assistantEntryId,
            model,
            usage,
            cancellationToken));

    public Task<LlmChatOperation> RequestCancellationAsync(
        LlmChatOperationId operationId,
        DateTimeOffset requestedAtUtc,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(sink => sink.RequestCancellationAsync(
            operationId,
            requestedAtUtc,
            cancellationToken));

    public Task<LlmChatOperation> CompleteCancellationAsync(
        LlmChatOperationId operationId,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(sink => sink.CompleteCancellationAsync(
            operationId,
            completedAtUtc,
            cancellationToken));

    public Task<LlmChatOperation> CompleteFailureAsync(
        LlmChatOperationId operationId,
        DateTimeOffset completedAtUtc,
        string failureCode,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(sink => sink.CompleteFailureAsync(
            operationId,
            completedAtUtc,
            failureCode,
            cancellationToken));

    public Task<LlmChatOperation> RequireRecoveryAsync(
        LlmChatOperationId operationId,
        string failureCode,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(sink => sink.RequireRecoveryAsync(
            operationId,
            failureCode,
            cancellationToken));

    private async Task<LlmChatOperation> ExecuteAsync(
        Func<ILlmChatOperationEvidenceSink, Task<LlmChatOperation>> operation)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var sink = scope.ServiceProvider.GetRequiredService<ILlmChatOperationEvidenceSink>();
        return await operation(sink).ConfigureAwait(false);
    }
}
