using CanDoItAll.Processes.Drivers.Abstractions.Gateway;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessVerificationAuditEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset RecordedAtUtc { get; set; }

    public Guid ProcessRunId { get; set; }

    public Guid StepRunId { get; set; }

    public string RequestedBy { get; set; } = string.Empty;

    public ProcessDriverVerificationGatewayLane Lane { get; set; }

    public int ResponseCount { get; set; }

    public int AcceptedCount { get; set; }

    public int DeniedCount { get; set; }

    public bool NoMutationPerformed { get; set; }

    public bool AllowsProcessMutation { get; set; }

    public bool AllowsTransitionMutation { get; set; }

    public bool AllowsFinalizerMutation { get; set; }

    public string ObservationHash { get; set; } = string.Empty;
}
