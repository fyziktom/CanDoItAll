using CanDoItAll.SharedKernel;

namespace CanDoItAll.Infrastructure.ControlPlane;

public sealed record DatabaseSwitchResult(
    Guid PreviousProfileId,
    Guid CurrentProfileId,
    long Generation,
    int ProcessId);

public interface IAppDatabaseBootstrapper
{
    Task EnsureCurrentProfileReadyAsync(CancellationToken cancellationToken = default);

    Task EnsureProfileReadyAsync(ResolvedDatabaseProfile profile, CancellationToken cancellationToken = default);
}

public interface IDatabaseSwitchCoordinator
{
    Task<Result<DatabaseSwitchResult>> SwitchAsync(Guid targetProfileId, CancellationToken cancellationToken = default);
}
