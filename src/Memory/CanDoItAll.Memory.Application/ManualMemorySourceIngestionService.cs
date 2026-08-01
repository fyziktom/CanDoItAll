using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

public sealed class ManualMemorySourceIngestionService(
    IMemoryOperationHandler operationHandler)
{
    public async Task<ManualMemorySourceIngestionResult> EnqueueAsync(
        ManualMemorySourceIngestionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var sourceGatewayRequest = request.Payload.ToGatewayRequest(
            request.ProviderInstanceId,
            request.RequestedBy);
        var handlerRequest = MemoryOperationRequestBuilder.SourceCapture(
            MemoryOperationCaller.ManualIngestion("memory.manual-ingestion.enqueue", request.Requester),
            CreateExplicitProviderPolicy(
                request.ProviderInstanceId,
                MemoryCapabilityIds.IngestionSnapshot),
            new MemorySourceCaptureOperationRequest(
                request.ProviderInstanceId,
                sourceGatewayRequest,
                "Manual source snapshot captured for provider ingestion."),
            request.Retention);
        var result = await operationHandler.CaptureSourceForIngestionAsync(handlerRequest, cancellationToken);
        if (result.Status != MemoryOperationHandlerStatus.Accepted ||
            result.OperationRecord is null ||
            result.Output is null)
        {
            throw new InvalidOperationException(
                $"Manual memory source capture failed with status '{result.Status}': {result.Diagnostic}");
        }

        var jobRecord = result.Output.JobRecord;
        var capturedSnapshotId = jobRecord.CapturedSnapshotId
            ?? throw new InvalidOperationException("Manual memory source capture did not return a snapshot id.");

        return new ManualMemorySourceIngestionResult(
            jobRecord.JobId,
            result.OperationRecord.OperationId,
            capturedSnapshotId,
            result.Output.PayloadForms);
    }

    private static MemoryProviderSelectionPolicy CreateExplicitProviderPolicy(
        MemoryProviderInstanceId providerInstanceId,
        MemoryCapabilityId capability) =>
        new(
            capability,
            providerInstanceId,
            DefaultProviderId: null,
            Assignments: [],
            AllowedCapabilities: [],
            DeniedCapabilities: [],
            MemoryProviderFallbackBehavior.DenyImplicitFallback);
}
