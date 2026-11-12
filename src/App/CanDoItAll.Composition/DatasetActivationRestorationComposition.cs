using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Composition;

public sealed class DatasetActivationRestorationSession : IAsyncDisposable {
    private readonly ExistingDatabaseProfileSession profileSession;

    internal DatasetActivationRestorationSession(ExistingDatabaseProfileSession profileSession,
        DatasetActivationRestorationCoordinator coordinator) {
        this.profileSession = profileSession;
        Coordinator = coordinator;
    }

    public DatasetActivationRestorationCoordinator Coordinator { get; }
    public ValueTask DisposeAsync() => profileSession.DisposeAsync();
}

public static class DatasetActivationRestorationComposition {
    public static async Task<DatasetActivationRestorationSession> OpenAsync(IConfiguration configuration, IHostEnvironment host,
        Guid profileId, ILogger<DatasetActivationRestorationCoordinator> logger, CancellationToken cancellationToken = default) {
        var session = await ExistingDatabaseProfileSession.OpenAsync(configuration, host, profileId, cancellationToken);
        try {
            var projects = configuration.GetSection(ProjectDefinitionOperationOptions.SectionName).Get<ProjectDefinitionOperationOptions>() ?? new();
            var workbench = configuration.GetSection(ProjectTaskPlanOperationOptions.SectionName).Get<ProjectTaskPlanOperationOptions>() ?? new();
            var transactions = CoordinatedDatabaseTransaction.ForProfile(session.Profile);
            var lifecycle = new DbContextOptionsBuilder<DatasetLifecycleDbContext>().UseNpgsql(session.ConnectionString).Options;
            var epochs = new DbContextOptionsBuilder<FiniteOperationDbContext>().UseNpgsql(session.ConnectionString).Options;
            var coordinator = new DatasetActivationRestorationCoordinator(profileId,
                new(session.Paths, session.Files, new PhysicalFileSystemPathPolicyFactory()), lifecycle,
                new(epochs, transactions), transactions, [ProjectFiniteOperationPool.Create(projects), WorkbenchTaskPlanFiniteOperations.Create(workbench)],
                session.RequireOriginalAsync, logger);
            return new(session, coordinator);
        } catch (Exception primary) {
            try {
                await session.DisposeAsync();
            } catch (Exception release) {
                throw new AggregateException("Restoration composition and explicit-profile release both failed.", primary, release);
            }
            throw;
        }
    }
}
