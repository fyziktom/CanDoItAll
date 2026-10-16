using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Support;

public static class DatabaseTransferTestSupport {
    public static DatabaseTransferOperationRunner Create(DatabaseTransferOwnerSessionRunner? owners = null,
        IProfileAppDbContextFactory? contexts = null, ProjectTransferTargetInspectionRunner? inspections = null,
        DatasetActivationService? activation = null)
        => new(contexts ?? new ProfileAppDbContextFactory(), owners ?? new(), inspections ?? new(), activation ?? UnconfiguredActivation());

    public static DatabaseTransferOperationRunner For(DatabaseTransferOperation transfer, AppDbContext source,
        AppDbContext target, DatabaseTransferOwnerSessionRunner? owners = null, ProjectTransferTargetInspectionRunner? inspections = null,
        DatasetActivationService? activation = null)
        => Create(owners, new FixtureContexts([(transfer.SourceProfile, source), (transfer.TargetProfile, target)]), inspections, activation);

    public static DatabaseTransferOperationRunner ForProfile(ResolvedDatabaseProfile profile, AppDbContext context,
        ProjectTransferTargetInspectionRunner? inspections = null, DatasetActivationService? activation = null)
        => Create(contexts: new FixtureContexts([(profile, context)]), inspections: inspections, activation: activation);

    public static DatasetActivationService CreateActivationService(CanDoItAllTestEnvironment environment) {
        var policies = new PhysicalFileSystemPathPolicyFactory();
        var writer = new DurableFileWriter(policies);
        var paths = new ControlPlanePathResolver(Options.Create(new ControlPlaneOptions { RootPath = environment.ControlPlaneRootPath }),
            environment.CreateHostEnvironment(nameof(DatabaseTransferTestSupport)), writer);
        return new(paths, writer, policies);
    }

    private static DatasetActivationService UnconfiguredActivation() {
        var policies = new PhysicalFileSystemPathPolicyFactory();
        return new(new MissingActivationPaths(), new DurableFileWriter(policies), policies);
    }

    private sealed class MissingActivationPaths : IControlPlanePathResolver {
        public string ResolveRootPath() => throw new InvalidOperationException("A relational target transfer test requires explicit dataset activation.");
        public string ResolveDatabaseProfilesRootPath() => ResolveRootPath();
        public string ResolveCatalogFilePath() => ResolveRootPath();
        public string ResolveActiveProfileStateFilePath() => ResolveRootPath();
        public string ResolveFileApplicationPreferencesFilePath() => ResolveRootPath();
        public string ResolveDataProtectionKeysPath() => ResolveRootPath();
        public string ResolveStateRootPath() => ResolveRootPath();
        public string ResolveLogsRootPath() => ResolveRootPath();
        public string ResolveRuntimeTemporaryRootPath() => ResolveRootPath();
    }

    private sealed class FixtureContexts((ResolvedDatabaseProfile Profile, AppDbContext Context)[] databases) : IProfileAppDbContextFactory {
        public Task<AppDbContext> CreateDbContextForProfileAsync(ResolvedDatabaseProfile profile, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            var fixture = databases.Single(database => database.Profile.Profile.Id == profile.Profile.Id);
            if (fixture.Profile.ConnectionString != profile.ConnectionString || fixture.Profile.Profile.ProviderKind != profile.Profile.ProviderKind) {
                throw new InvalidOperationException("The transfer fixture profile no longer matches its explicit database binding.");
            }
            var options = new DbContextOptionsBuilder<AppDbContext>((DbContextOptions<AppDbContext>)fixture.Context.GetService<IDbContextOptions>());
            if (fixture.Context.Database.IsNpgsql()) {
                options.UseNpgsql(fixture.Context.Database.GetDbConnection(), contextOwnsConnection: false);
            }
            return Task.FromResult(new AppDbContext(options.Options));
        }
    }
}
