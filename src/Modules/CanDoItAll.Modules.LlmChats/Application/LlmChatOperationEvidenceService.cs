using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Ports;

namespace CanDoItAll.Modules.LlmChats.Application;

public sealed class LlmChatOperationEvidenceService(
    ILlmChatOperationRepository operationRepository,
    ILlmChatInvocationRecordRepository invocationRepository,
    ILlmChatUnitOfWork unitOfWork) : ILlmChatOperationEvidenceSink
{
    private const int MaximumConcurrencyAttempts = 4;

    public Task<LlmChatOperation> MarkTurnAdmittedAsync(
        LlmChatOperationId operationId,
        DateTimeOffset admittedAtUtc,
        CancellationToken cancellationToken = default)
        => UpdateAsync(
            operationId,
            operation => LlmChatOperationTransitions.MarkTurnAdmitted(operation, admittedAtUtc),
            cancellationToken);

    public Task<LlmChatOperation> MarkProviderDispatchStartedAsync(
        LlmChatOperationId operationId,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken = default)
        => UpdateAsync(
            operationId,
            operation => LlmChatOperationTransitions.MarkProviderDispatchStarted(operation, startedAtUtc),
            cancellationToken);

    public async Task<LlmChatOperation> RecordInvocationAsync(
        LlmChatInvocationRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        for (var attempt = 1; attempt <= MaximumConcurrencyAttempts; attempt++)
        {
            var updated = await unitOfWork.ExecuteAsync(async transactionCancellationToken =>
            {
                var current = await RequireAsync(record.OperationId, transactionCancellationToken)
                    .ConfigureAwait(false);
                var transitioned = LlmChatOperationTransitions.MarkProviderDispatchReturned(
                    current,
                    record.CompletedAtUtc);
                if (!await operationRepository.TryReplaceAsync(
                        transitioned,
                        current.ConcurrencyToken,
                        transactionCancellationToken).ConfigureAwait(false))
                {
                    return null;
                }

                await invocationRepository.AppendAsync(record, transactionCancellationToken).ConfigureAwait(false);
                return await RequireAsync(record.OperationId, transactionCancellationToken).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
            if (updated is not null)
            {
                return updated;
            }
        }

        throw new InvalidOperationException("The LLM Chat invocation evidence could not win its concurrency update.");
    }

    public Task<LlmChatOperation> CompleteTranscriptAsync(
        LlmChatOperationId operationId,
        DateTimeOffset completedAtUtc,
        long resultingTranscriptRevision,
        Guid assistantEntryId,
        CancellationToken cancellationToken = default)
        => UpdateAsync(
            operationId,
            operation => LlmChatOperationTransitions.CompleteTranscript(
                operation,
                completedAtUtc,
                resultingTranscriptRevision,
                assistantEntryId),
            cancellationToken);

    public Task<LlmChatOperation> RequestCancellationAsync(
        LlmChatOperationId operationId,
        DateTimeOffset requestedAtUtc,
        CancellationToken cancellationToken = default)
        => UpdateAsync(
            operationId,
            operation => LlmChatOperationTransitions.RequestCancellation(operation, requestedAtUtc),
            cancellationToken);

    public Task<LlmChatOperation> CompleteCancellationAsync(
        LlmChatOperationId operationId,
        DateTimeOffset completedAtUtc,
        CancellationToken cancellationToken = default)
        => UpdateAsync(
            operationId,
            operation => LlmChatOperationTransitions.CompleteCancellation(operation, completedAtUtc),
            cancellationToken);

    public Task<LlmChatOperation> CompleteFailureAsync(
        LlmChatOperationId operationId,
        DateTimeOffset completedAtUtc,
        string failureCode,
        CancellationToken cancellationToken = default)
        => UpdateAsync(
            operationId,
            operation => LlmChatOperationTransitions.CompleteFailure(operation, completedAtUtc, failureCode),
            cancellationToken);

    public Task<LlmChatOperation> RequireRecoveryAsync(
        LlmChatOperationId operationId,
        string failureCode,
        CancellationToken cancellationToken = default)
        => UpdateAsync(
            operationId,
            operation => LlmChatOperationTransitions.RequireRecovery(operation, failureCode),
            cancellationToken);

    private async Task<LlmChatOperation> UpdateAsync(
        LlmChatOperationId operationId,
        Func<LlmChatOperation, LlmChatOperation> transition,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaximumConcurrencyAttempts; attempt++)
        {
            var updated = await unitOfWork.ExecuteAsync(async transactionCancellationToken =>
            {
                var current = await RequireAsync(operationId, transactionCancellationToken).ConfigureAwait(false);
                var transitioned = transition(current);
                if (ReferenceEquals(transitioned, current) || transitioned == current)
                {
                    return current;
                }

                if (!await operationRepository.TryReplaceAsync(
                        transitioned,
                        current.ConcurrencyToken,
                        transactionCancellationToken).ConfigureAwait(false))
                {
                    return null;
                }

                return await RequireAsync(operationId, transactionCancellationToken).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
            if (updated is not null)
            {
                return updated;
            }
        }

        throw new InvalidOperationException("The LLM Chat operation could not win its concurrency update.");
    }

    private async Task<LlmChatOperation> RequireAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken)
        => await operationRepository.TryGetAsync(operationId, cancellationToken).ConfigureAwait(false)
           ?? throw new InvalidOperationException("The LLM Chat operation evidence target does not exist.");
}
