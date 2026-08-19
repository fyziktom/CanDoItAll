using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Modules.Plugins;

internal sealed class DockerProcessHostCapabilitySource(
    IDockerHostCapabilitySnapshotProvider snapshotProvider) : IProcessHostCapabilitySource
{
    public ProcessHostCapabilitySourceId SourceId { get; } = new("docker-host");

    public IReadOnlySet<ProcessHostCapabilityId> DeclaredCapabilities { get; } =
        new HashSet<ProcessHostCapabilityId> { ProcessHostCapabilityIds.Docker };

    public async ValueTask<IReadOnlyList<ProcessHostCapabilityFact>> ProbeAsync(
        CancellationToken cancellationToken = default)
    {
        DockerHostCapabilitySnapshot snapshot;
        try
        {
            snapshot = await snapshotProvider.GetAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return
            [
                new ProcessHostCapabilityFact(
                    ProcessHostCapabilityIds.Docker,
                    ProcessHostCapabilityAvailability.Unavailable,
                    ProcessHostCapabilityReason.TimedOut,
                    ProcessHostExecutionPort.None)
            ];
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return
            [
                new ProcessHostCapabilityFact(
                    ProcessHostCapabilityIds.Docker,
                    ProcessHostCapabilityAvailability.Unavailable,
                    ProcessHostCapabilityReason.Unavailable,
                    ProcessHostExecutionPort.None)
            ];
        }

        var limitingState = new[] { snapshot.Executable, snapshot.Context, snapshot.Daemon }
            .FirstOrDefault(state => state != DockerHostDependencyState.Available);
        return
        [
            new ProcessHostCapabilityFact(
                ProcessHostCapabilityIds.Docker,
                snapshot.IsReady
                    ? ProcessHostCapabilityAvailability.Available
                    : ProcessHostCapabilityAvailability.Unavailable,
                snapshot.IsReady
                    ? ProcessHostCapabilityReason.Ready
                    : MapReason(limitingState),
                snapshot.IsReady
                    ? ProcessHostExecutionPort.DockerHostTool
                    : ProcessHostExecutionPort.None)
        ];
    }

    private static ProcessHostCapabilityReason MapReason(DockerHostDependencyState state)
        => state switch
        {
            DockerHostDependencyState.InvalidConfiguration => ProcessHostCapabilityReason.InvalidConfiguration,
            DockerHostDependencyState.PermissionDenied => ProcessHostCapabilityReason.PermissionDenied,
            DockerHostDependencyState.Missing => ProcessHostCapabilityReason.DependencyMissing,
            DockerHostDependencyState.TimedOut => ProcessHostCapabilityReason.TimedOut,
            DockerHostDependencyState.Unavailable => ProcessHostCapabilityReason.Unavailable,
            _ => ProcessHostCapabilityReason.Unavailable
        };
}
