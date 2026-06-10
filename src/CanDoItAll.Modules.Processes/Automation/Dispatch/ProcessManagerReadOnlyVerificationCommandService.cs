using CanDoItAll.Processes.Drivers.Abstractions.Gateway;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessManagerReadOnlyVerificationCommandService
{
    private readonly IProcessVerificationRuntimeHost host;

    public ProcessManagerReadOnlyVerificationCommandService(IProcessVerificationRuntimeHost host)
    {
        this.host = host ?? throw new ArgumentNullException(nameof(host));
    }

    public ProcessManagerReadOnlyVerificationCommandResult Run(ProcessManagerReadOnlyVerificationCommandRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var hostResponse = host.Verify(new ProcessVerificationHostRequest(
            request.Lane,
            request.Payload,
            request.RequestedBy,
            request.RequestedAt));
        var projection = ProcessManagerReadOnlyVerificationProjectionMapper.Project(
            new ProcessManagerReadOnlyVerificationProjectionRequest(
                hostResponse.Observation,
                request.ProjectionMode,
                request.RequestedBy,
                request.RequestedAt,
                hostResponse.AuditRecord.Id));

        return new ProcessManagerReadOnlyVerificationCommandResult(
            request.Lane,
            hostResponse.Observation,
            projection,
            hostResponse.AuditRecord,
            hostResponse.NoMutationPerformed,
            hostResponse.AllowsProcessMutation,
            hostResponse.AllowsTransitionMutation,
            hostResponse.AllowsFinalizerMutation);
    }
}

internal sealed record ProcessManagerReadOnlyVerificationCommandRequest(
    ProcessDriverVerificationGatewayLane Lane,
    ProcessReadOnlyVerificationBatchPayload Payload,
    ProcessManagerReadOnlyVerificationProjectionMode ProjectionMode,
    string RequestedBy,
    DateTimeOffset RequestedAt);

internal sealed record ProcessManagerReadOnlyVerificationCommandResult(
    ProcessDriverVerificationGatewayLane Lane,
    ProcessReadOnlyVerificationBatchObservation Observation,
    ProcessManagerReadOnlyVerificationProjection Projection,
    ProcessVerificationAuditRecord AuditRecord,
    bool NoMutationPerformed,
    bool AllowsProcessMutation,
    bool AllowsTransitionMutation,
    bool AllowsFinalizerMutation);
