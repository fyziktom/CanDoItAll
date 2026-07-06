using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using GenericMemorySourceScope = CanDoItAll.Memory.Abstractions.MemorySourceScope;
using MemoryProviderInstanceId = CanDoItAll.Memory.Abstractions.MemoryProviderInstanceId;
using MafMemorySourceKind = CanDoItAll.AgentFramework.Core.MemorySourceKind;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectMemoryIngestionServiceTests
{
    private static readonly DateTimeOffset NowUtc = DateTimeOffset.Parse("2026-07-05T12:34:56Z");
    private static readonly Guid ProjectId = Guid.Parse("7b86c1c8-0a57-4ef6-b3f4-9f9446f03d3e");

    [Fact]
    public async Task EnqueueProjectStructureIngestionAsync_captures_snapshot_through_shared_handler()
    {
        var providerInstanceId = MemoryProviderInstanceId.Parse("provider.programming");
        var handler = new CapturingMemoryOperationHandler(acceptSourceCapture: true);
        var service = new ProjectMemoryIngestionService(handler, new FixedClock(NowUtc));

        var record = await service.EnqueueProjectStructureIngestionAsync(
            providerInstanceId,
            ProjectId,
            requestedBy: "user-42");

        var request = Assert.IsType<MemoryOperationHandlerRequest<MemorySourceCaptureOperationRequest>>(
            handler.LastSourceCaptureRequest);
        Assert.Equal(MemoryOperationCallerKind.SourceIngestion, request.Caller.Kind);
        Assert.Equal(MemoryOperationKind.Ingestion, request.OperationKind);
        Assert.Equal(providerInstanceId, request.Payload.ProviderInstanceId);
        Assert.Equal(MemoryCapabilityIds.IngestionSnapshot, request.SelectionPolicy.RequiredCapability);
        Assert.Equal(providerInstanceId, request.SelectionPolicy.ExplicitProviderId);
        Assert.Equal(ProjectId, request.Payload.SourceGatewayRequest.ScopeId);
        Assert.Equal(MafMemorySourceKind.WorkbenchProjectStructure, request.Payload.SourceGatewayRequest.SourceKind);
        Assert.Equal(GenericMemorySourceScope.Project, request.Payload.SourceGatewayRequest.RequestedScope);
        Assert.Equal("user-42", request.Payload.SourceGatewayRequest.RequesterId);
        Assert.Equal(providerInstanceId, record.ProviderInstanceId);
        Assert.Equal(MemorySourceIngestionJobStatus.SnapshotCaptured, record.Status);
        Assert.Equal(NowUtc, record.CreatedAtUtc);
        Assert.Equal(NowUtc, record.UpdatedAtUtc);
    }

    [Fact]
    public async Task EnqueueProjectStructureIngestionAsync_fails_explicitly_when_handler_denies_snapshot()
    {
        var handler = new CapturingMemoryOperationHandler(acceptSourceCapture: false);
        var service = new ProjectMemoryIngestionService(handler, new FixedClock(NowUtc));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.EnqueueProjectStructureIngestionAsync(
                MemoryProviderInstanceId.Parse("provider.programming"),
                ProjectId,
                requestedBy: "user-42"));

        Assert.Contains("SourceCaptureFailed", exception.Message, StringComparison.Ordinal);
    }

    private sealed class CapturingMemoryOperationHandler(bool acceptSourceCapture) : IMemoryOperationHandler
    {
        public object? LastSourceCaptureRequest { get; private set; }

        public Task<MemoryOperationHandlerResult<MemorySourceCaptureOperationResult>> CaptureSourceForIngestionAsync(
            MemoryOperationHandlerRequest<MemorySourceCaptureOperationRequest> request,
            CancellationToken cancellationToken = default)
        {
            LastSourceCaptureRequest = request;
            var selection = MemoryProviderSelectionResult.Selected(
                CreateProviderProfile(request.Payload.ProviderInstanceId),
                MemoryProviderSelectionReason.ExplicitProvider,
                MemoryCapabilityIds.IngestionSnapshot);
            if (!acceptSourceCapture)
            {
                return Task.FromResult(new MemoryOperationHandlerResult<MemorySourceCaptureOperationResult>(
                    MemoryOperationHandlerStatus.SourceCaptureFailed,
                    selection,
                    OperationRecord: null,
                    Output: null,
                    AcceptedOperation: null,
                    FeedbackHandle: null,
                    DriverDispatchAttempted: false,
                    "Source capture failed."));
            }

            var operation = MemoryOperationRecord.Create(
                MemoryOperationRecordId.New(),
                MemoryOperationId.New(),
                request.Payload.ProviderInstanceId,
                MemoryCapabilityIds.IngestionSnapshot,
                MemoryOperationKind.Ingestion,
                request.Caller.Requester,
                request.CorrelationId,
                request.CausationId,
                [MemorySourceSnapshotId.Parse("snapshot.project.1")],
                request.Retention,
                NowUtc);
            var jobRecord = new MemorySourceIngestionJobRecord(
                Guid.NewGuid(),
                request.Payload.ProviderInstanceId,
                request.Payload.SourceGatewayRequest,
                MemorySourceIngestionJobStatus.SnapshotCaptured,
                NowUtc,
                NowUtc,
                request.Payload.StatusReason,
                CapturedSnapshotId: new CanDoItAll.AgentFramework.Core.MemorySourceSnapshotId("maf.snapshot.project.1"),
                operation.OperationId);
            return Task.FromResult(new MemoryOperationHandlerResult<MemorySourceCaptureOperationResult>(
                MemoryOperationHandlerStatus.Accepted,
                selection,
                operation,
                new MemorySourceCaptureOperationResult(jobRecord, []),
                AcceptedOperation: null,
                FeedbackHandle: null,
                DriverDispatchAttempted: false,
                "Source snapshot captured."));
        }

        public Task<MemoryOperationHandlerResult<MemoryEventOutboxRecord>> AcknowledgeEventAsync(
            MemoryOperationHandlerRequest<MemoryEventAcknowledgeRequest> request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MemoryOperationHandlerResult<MemoryOperationRecord>> CancelAsync(
            MemoryOperationHandlerRequest<MemoryOperationCancellationRequest> request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MemoryOperationHandlerResult<MemoryContextPack>> ExecuteQueryAsync(
            MemoryOperationHandlerRequest<MemoryContextQueryRequest> request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MemoryOperationHandlerResult<MemoryOperationRecord>> GetStatusAsync(
            MemoryOperationHandlerRequest<MemoryOperationStatusRequest> request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<MemoryOperationHandlerResult<MemoryFeedbackRecord>> SubmitFeedbackAsync(
            MemoryOperationHandlerRequest<MemoryFeedbackOperationRequest> request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        private static MemoryProviderProfile CreateProviderProfile(MemoryProviderInstanceId providerInstanceId)
        {
            return new MemoryProviderProfile(
                providerInstanceId,
                DisplayName: "Programming provider",
                MemoryProviderDriverKind.Mock,
                IsEnabled: true,
                MemoryProviderHealthState.Healthy,
                MemoryProviderWorkspaceScope.AllWorkspaces,
                SelectionTags: ["test"],
                MemoryProviderProfilePolicy.Default,
                new MemoryProviderManifest(
                    MemoryProviderKind.Parse("provider.programming"),
                    MemoryProtocolVersion.Current,
                    [new MemoryCapabilityDescriptor(MemoryCapabilityIds.IngestionSnapshot, Version: "1", Supported: true)],
                    MemoryProviderInteractionSupport.SyncQueryOnly,
                    UiSurfaces: [],
                    MemoryProviderLimits.Default,
                    MemoryExtensionData.Empty));
        }
    }

    private sealed class FixedClock(DateTimeOffset nowUtc) : IClock
    {
        public DateTimeOffset GetUtcNow() => nowUtc;
    }
}
