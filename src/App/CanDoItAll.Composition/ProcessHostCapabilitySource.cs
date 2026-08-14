using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Composition;

internal sealed class ApplicationProcessHostCapabilitySource(
    IHostCapabilitySnapshotProvider snapshotProvider) : IProcessHostCapabilitySource
{
    public ProcessHostCapabilitySourceId SourceId { get; } = new("application-host");

    public IReadOnlySet<ProcessHostCapabilityId> DeclaredCapabilities { get; } =
        new HashSet<ProcessHostCapabilityId>
        {
            ProcessHostCapabilityIds.DesktopOpen,
            ProcessHostCapabilityIds.InteractiveTerminal
        };

    public ValueTask<IReadOnlyList<ProcessHostCapabilityFact>> ProbeAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        HostCapabilitySnapshot snapshot;
        try
        {
            snapshot = snapshotProvider.GetSnapshot();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromResult<IReadOnlyList<ProcessHostCapabilityFact>>(
            [
                Unavailable(ProcessHostCapabilityIds.DesktopOpen, ProcessHostCapabilityReason.TimedOut),
                Unavailable(ProcessHostCapabilityIds.InteractiveTerminal, ProcessHostCapabilityReason.TimedOut)
            ]);
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return ValueTask.FromResult<IReadOnlyList<ProcessHostCapabilityFact>>(
            [
                Unavailable(ProcessHostCapabilityIds.DesktopOpen, ProcessHostCapabilityReason.IoFailure),
                Unavailable(ProcessHostCapabilityIds.InteractiveTerminal, ProcessHostCapabilityReason.IoFailure)
            ]);
        }

        return ValueTask.FromResult<IReadOnlyList<ProcessHostCapabilityFact>>(
        [
            Project(
                snapshot,
                HostCapabilityId.DesktopFileOpen,
                ProcessHostCapabilityIds.DesktopOpen,
                ProcessHostExecutionPort.DesktopLauncher),
            Project(
                snapshot,
                HostCapabilityId.InteractiveTerminal,
                ProcessHostCapabilityIds.InteractiveTerminal,
                ProcessHostExecutionPort.InteractiveTerminal)
        ]);
    }

    private static ProcessHostCapabilityFact Unavailable(
        ProcessHostCapabilityId id,
        ProcessHostCapabilityReason reason)
        => new(
            id,
            ProcessHostCapabilityAvailability.Unavailable,
            reason,
            ProcessHostExecutionPort.None);

    private static ProcessHostCapabilityFact Project(
        HostCapabilitySnapshot snapshot,
        HostCapabilityId sourceId,
        ProcessHostCapabilityId targetId,
        ProcessHostExecutionPort executionPort)
    {
        var source = snapshot.Capabilities.SingleOrDefault(capability => capability.Id == sourceId);
        if (source is null)
        {
            return new ProcessHostCapabilityFact(
                targetId,
                ProcessHostCapabilityAvailability.Unavailable,
                ProcessHostCapabilityReason.NotRegistered,
                ProcessHostExecutionPort.None);
        }

        var availability = source.Availability switch
        {
            HostCapabilityAvailability.Available => ProcessHostCapabilityAvailability.Available,
            HostCapabilityAvailability.Unsupported => ProcessHostCapabilityAvailability.Unsupported,
            HostCapabilityAvailability.Unverified => ProcessHostCapabilityAvailability.Unverified,
            _ => ProcessHostCapabilityAvailability.Unavailable
        };
        return new ProcessHostCapabilityFact(
            targetId,
            availability,
            MapReason(source.ReasonCode),
            availability == ProcessHostCapabilityAvailability.Available
                ? executionPort
                : ProcessHostExecutionPort.None);
    }

    private static ProcessHostCapabilityReason MapReason(HostCapabilityReasonCode reason)
        => reason switch
        {
            HostCapabilityReasonCode.Ready => ProcessHostCapabilityReason.Ready,
            HostCapabilityReasonCode.DisabledByProfile => ProcessHostCapabilityReason.DisabledByProfile,
            HostCapabilityReasonCode.DependencyUnavailable => ProcessHostCapabilityReason.DependencyMissing,
            HostCapabilityReasonCode.UnsupportedByProfile => ProcessHostCapabilityReason.UnsupportedByProfile,
            HostCapabilityReasonCode.InvalidConfiguration or HostCapabilityReasonCode.UnsafePath =>
                ProcessHostCapabilityReason.InvalidConfiguration,
            HostCapabilityReasonCode.PermissionDenied => ProcessHostCapabilityReason.PermissionDenied,
            HostCapabilityReasonCode.ProbePending => ProcessHostCapabilityReason.ProbePending,
            HostCapabilityReasonCode.IoFailure => ProcessHostCapabilityReason.IoFailure,
            HostCapabilityReasonCode.ActualHostValidationDeferred =>
                ProcessHostCapabilityReason.ActualHostValidationDeferred,
            _ => ProcessHostCapabilityReason.NotRegistered
        };
}

internal sealed class ApplicationProcessHostProfileSource(
    IHostCapabilitySnapshotProvider snapshotProvider) : IProcessHostProfileSource
{
    public ValueTask<ProcessHostProfileId> GetProfileIdAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var profileId = snapshotProvider.GetSnapshot().OperatingSystem switch
        {
            RuntimeHostOperatingSystem.Windows => "windows",
            RuntimeHostOperatingSystem.Linux => "linux",
            RuntimeHostOperatingSystem.MacOs => "macos",
            _ => "unknown"
        };
        return ValueTask.FromResult(new ProcessHostProfileId(profileId));
    }
}
