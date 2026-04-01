using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Web.Infrastructure;

public static class RuntimeDatabaseSwitchingServiceCollectionExtensions
{
    public static IServiceCollection AddCanDoItAllRuntimeDatabaseSwitching(this IServiceCollection services)
    {
        services.AddSingleton<IAppDatabaseBootstrapper, AppDatabaseBootstrapper>();
        services.AddSingleton<IDatabaseSwitchCoordinator, DatabaseSwitchCoordinator>();
        return services;
    }
}

public sealed class AppDatabaseBootstrapper(
    IDatabaseProfileRuntimeAccessor profileAccessor,
    ISwitchableAppDbContextFactory dbContextFactory,
    ILogger<AppDatabaseBootstrapper> logger) : IAppDatabaseBootstrapper
{
    public Task EnsureCurrentProfileReadyAsync(CancellationToken cancellationToken = default)
    {
        return EnsureProfileReadyAsync(profileAccessor.ResolveCurrentProfile(), cancellationToken);
    }

    public async Task EnsureProfileReadyAsync(ResolvedDatabaseProfile profile, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextForProfileAsync(profile, cancellationToken);
        if (!dbContext.Database.IsRelational())
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            return;
        }

        await LegacySqliteMigrationBootstrap.PrepareAsync(dbContext, logger, cancellationToken);
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}

public sealed class DatabaseSwitchCoordinator(
    IDatabaseProfileRuntimeAccessor profileAccessor,
    IDatabaseProfileService profileService,
    IDatabaseDriverRegistry driverRegistry,
    IDatabaseRuntimeState runtimeState,
    IAppDatabaseBootstrapper bootstrapper,
    ILogger<DatabaseSwitchCoordinator> logger) : IDatabaseSwitchCoordinator
{
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(15);

    public async Task<Result<DatabaseSwitchResult>> SwitchAsync(Guid targetProfileId, CancellationToken cancellationToken = default)
    {
        var currentProfile = profileAccessor.ResolveCurrentProfile();
        if (currentProfile.Profile.Runtime.LockedByRuntimeOverride)
        {
            return Result<DatabaseSwitchResult>.Failure(
                Error.Failure("Runtime override is active. Database switching is disabled."));
        }

        if (currentProfile.Profile.Id == targetProfileId)
        {
            var snapshot = runtimeState.GetSnapshot();
            return Result<DatabaseSwitchResult>.Success(new DatabaseSwitchResult(
                currentProfile.Profile.Id,
                currentProfile.Profile.Id,
                snapshot.Generation,
                Environment.ProcessId));
        }

        ResolvedDatabaseProfile targetProfile;
        try
        {
            targetProfile = profileAccessor.ResolveProfile(targetProfileId);
        }
        catch (Exception ex)
        {
            return Result<DatabaseSwitchResult>.Failure(Error.Failure(ex.Message));
        }

        await using var switchSession = await runtimeState.BeginSwitchAsync(cancellationToken);

        try
        {
            await switchSession.WaitForDrainAsync(DrainTimeout, cancellationToken);
            await driverRegistry.Resolve(targetProfile.Profile.ProviderKind)
                .EnsureDatabaseAsync(targetProfile, cancellationToken);
            await bootstrapper.EnsureProfileReadyAsync(targetProfile, cancellationToken);

            var activationResult = await profileService.ActivateAsync(targetProfileId, cancellationToken);
            if (activationResult.IsFailure)
            {
                return Result<DatabaseSwitchResult>.Failure(activationResult.Errors);
            }

            var notification = switchSession.Complete(targetProfile);
            logger.LogInformation(
                "Switched active database from {PreviousProfileId} to {CurrentProfileId} at generation {Generation}.",
                currentProfile.Profile.Id,
                targetProfile.Profile.Id,
                notification.Generation);

            return Result<DatabaseSwitchResult>.Success(new DatabaseSwitchResult(
                currentProfile.Profile.Id,
                targetProfile.Profile.Id,
                notification.Generation,
                Environment.ProcessId));
        }
        catch (TimeoutException ex)
        {
            logger.LogWarning(
                ex,
                "Database switch from {PreviousProfileId} to {TargetProfileId} timed out while waiting for active contexts to drain.",
                currentProfile.Profile.Id,
                targetProfileId);

            return Result<DatabaseSwitchResult>.Failure(
                Error.Failure("Database switch timed out while waiting for active operations to finish."));
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Database switch from {PreviousProfileId} to {TargetProfileId} failed.",
                currentProfile.Profile.Id,
                targetProfileId);

            return Result<DatabaseSwitchResult>.Failure(
                Error.Failure($"Database switch failed: {ex.Message}"));
        }
    }
}
