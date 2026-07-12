using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.SharedKernel;
using MafMemorySourceKind = CanDoItAll.Memory.SourceGateway.MemorySourceKind;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectMemoryIngestionService(
    IMemoryOperationHandler operationHandler,
    IClock clock)
{
    public async Task<MemorySourceIngestionJobRecord> EnqueueProjectStructureIngestionAsync(
        MemoryProviderInstanceId providerInstanceId,
        Guid projectId,
        string requestedBy,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException("Project id is required.", nameof(projectId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(requestedBy);

        var requester = new MemoryLedgerRequester(
            requestedBy.Trim(),
            AgentId: null,
            AgentRole: null,
            SessionId: null,
            WorkflowId: null,
            WorkflowNodeId: null,
            ProcessId: null,
            ProcessStepId: null);
        var sourceRequest = new MemorySourceGatewayRequest(
            MafMemorySourceKind.WorkbenchProjectStructure,
            projectId,
            MemorySourceScope.Project,
            Cursor: null,
            Take: null,
            MemorySourceGatewayPolicy.AllowScopes(
                [MafMemorySourceKind.WorkbenchProjectStructure],
                [MemorySourceScope.Project]),
            requestedBy.Trim());
        var now = clock.GetUtcNow();
        var handlerRequest = MemoryOperationRequestBuilder.SourceCapture(
            MemoryOperationCaller.SourceIngestion("workbench.project-structure.memory-ingestion", requester),
            CreateExplicitProviderPolicy(
                providerInstanceId,
                MemoryCapabilityIds.IngestionSnapshot),
            new MemorySourceCaptureOperationRequest(
                providerInstanceId,
                sourceRequest,
                "Project structure source snapshot captured for provider ingestion."),
            MemoryLedgerRetentionPolicy.Expiring(now.AddDays(30), now.AddDays(90)));
        var result = await operationHandler.CaptureSourceForIngestionAsync(handlerRequest, cancellationToken);
        if (result.Status != MemoryOperationHandlerStatus.Accepted || result.Output is null)
        {
            throw new InvalidOperationException(
                $"Project source ingestion snapshot capture failed for project '{projectId:D}' and provider '{providerInstanceId.Value}'. Status: {result.Status}. Diagnostic: {result.Diagnostic}");
        }

        return result.Output.JobRecord;
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
