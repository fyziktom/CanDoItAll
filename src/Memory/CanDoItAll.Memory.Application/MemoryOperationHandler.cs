using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

internal sealed class MemoryOperationHandler : IMemoryOperationHandler
{
    private readonly MemoryQueryOperationService queryService;
    private readonly MemorySourceCaptureOperationService sourceCaptureService;
    private readonly MemoryFeedbackOperationService feedbackService;
    private readonly MemoryStatusOperationService statusService;
    private readonly MemoryEventOperationService eventService;

    internal MemoryOperationHandler(
        MemoryQueryOperationService queryService,
        MemorySourceCaptureOperationService sourceCaptureService,
        MemoryFeedbackOperationService feedbackService,
        MemoryStatusOperationService statusService,
        MemoryEventOperationService eventService)
    {
        this.queryService = queryService;
        this.sourceCaptureService = sourceCaptureService;
        this.feedbackService = feedbackService;
        this.statusService = statusService;
        this.eventService = eventService;
    }

    public Task<MemoryOperationHandlerResult<MemoryContextPack>> ExecuteQueryAsync(
        MemoryOperationHandlerRequest<MemoryContextQueryRequest> request,
        CancellationToken cancellationToken = default) =>
        queryService.ExecuteAsync(request, cancellationToken);

    public Task<MemoryOperationHandlerResult<MemorySourceCaptureOperationResult>> CaptureSourceForIngestionAsync(
        MemoryOperationHandlerRequest<MemorySourceCaptureOperationRequest> request,
        CancellationToken cancellationToken = default) =>
        sourceCaptureService.CaptureAsync(request, cancellationToken);

    public Task<MemoryOperationHandlerResult<MemoryFeedbackRecord>> SubmitFeedbackAsync(
        MemoryOperationHandlerRequest<MemoryFeedbackOperationRequest> request,
        CancellationToken cancellationToken = default) =>
        feedbackService.SubmitAsync(request, cancellationToken);

    public Task<MemoryOperationHandlerResult<MemoryOperationRecord>> GetStatusAsync(
        MemoryOperationHandlerRequest<MemoryOperationStatusRequest> request,
        CancellationToken cancellationToken = default) =>
        statusService.GetStatusAsync(request, cancellationToken);

    public Task<MemoryOperationHandlerResult<MemoryOperationRecord>> CancelAsync(
        MemoryOperationHandlerRequest<MemoryOperationCancellationRequest> request,
        CancellationToken cancellationToken = default) =>
        statusService.CancelAsync(request, cancellationToken);

    public Task<MemoryOperationHandlerResult<MemoryEventOutboxRecord>> AcknowledgeEventAsync(
        MemoryOperationHandlerRequest<MemoryEventAcknowledgeRequest> request,
        CancellationToken cancellationToken = default) =>
        eventService.AcknowledgeAsync(request, cancellationToken);
}
