using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

public interface IMemoryOperationHandler
{
    Task<MemoryOperationHandlerResult<MemoryContextPack>> ExecuteQueryAsync(
        MemoryOperationHandlerRequest<MemoryContextQueryRequest> request,
        CancellationToken cancellationToken = default);

    Task<MemoryOperationHandlerResult<MemorySourceCaptureOperationResult>> CaptureSourceForIngestionAsync(
        MemoryOperationHandlerRequest<MemorySourceCaptureOperationRequest> request,
        CancellationToken cancellationToken = default);

    Task<MemoryOperationHandlerResult<MemoryFeedbackRecord>> SubmitFeedbackAsync(
        MemoryOperationHandlerRequest<MemoryFeedbackOperationRequest> request,
        CancellationToken cancellationToken = default);

    Task<MemoryOperationHandlerResult<MemoryOperationRecord>> GetStatusAsync(
        MemoryOperationHandlerRequest<MemoryOperationStatusRequest> request,
        CancellationToken cancellationToken = default);

    Task<MemoryOperationHandlerResult<MemoryOperationRecord>> CancelAsync(
        MemoryOperationHandlerRequest<MemoryOperationCancellationRequest> request,
        CancellationToken cancellationToken = default);

    Task<MemoryOperationHandlerResult<MemoryEventOutboxRecord>> AcknowledgeEventAsync(
        MemoryOperationHandlerRequest<MemoryEventAcknowledgeRequest> request,
        CancellationToken cancellationToken = default);
}
