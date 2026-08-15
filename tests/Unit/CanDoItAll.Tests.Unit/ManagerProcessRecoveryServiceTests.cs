using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Manager;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.Infrastructure;

public sealed class ManagerProcessRecoveryServiceTests
{
    [Fact]
    public async Task Startup_reconciles_every_manager_process_purpose_before_supervisors_launch()
    {
        var coordinator = new RecordingCoordinator();
        var service = new ManagerProcessRecoveryService(
            coordinator,
            NullLogger<ManagerProcessRecoveryService>.Instance);

        await service.StartAsync(CancellationToken.None);

        Assert.Equal(Enum.GetValues<ManagerProcessPurpose>(), coordinator.ReclaimedPurposes);
    }

    private sealed class RecordingCoordinator : IManagerProcessCoordinator
    {
        public List<ManagerProcessPurpose> ReclaimedPurposes { get; } = [];

        public Task<IManagerProcessLease> StartAsync(
            ManagerProcessLaunchRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<WorkspaceProcessTerminationResult>> ReclaimRegisteredAsync(
            ManagerProcessPurpose purpose,
            string diagnosticCode,
            CancellationToken cancellationToken = default)
        {
            ReclaimedPurposes.Add(purpose);
            return Task.FromResult<IReadOnlyList<WorkspaceProcessTerminationResult>>([]);
        }
    }
}
