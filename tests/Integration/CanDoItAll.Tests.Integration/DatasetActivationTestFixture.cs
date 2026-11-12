using CanDoItAll.Composition;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using System.Text.Json;

namespace CanDoItAll.Tests.Integration;

internal sealed class DatasetActivationTestFixture(PostgresTestDatabaseLease lease, CanDoItAllTestEnvironment environment) : IAsyncDisposable {
    public CanDoItAllTestEnvironment Environment { get; } = environment;
    public ResolvedDatabaseProfile Profile { get; } = new(new DatabaseProfileRecord {
        Id = Guid.NewGuid(),
        ProviderKind = DatabaseProviderKind.PostgreSql
    }, DatabaseProfileResolutionSource.ExplicitOverride, lease.ConnectionString);
    private DbContextOptions<FiniteOperationDbContext> EpochOptions { get; } = new DbContextOptionsBuilder<FiniteOperationDbContext>()
        .UseNpgsql(lease.ConnectionString).Options;
    private DbContextOptions<AppDbContext> CanonicalOptions => lease.CreateAppDbContextOptions();
    public DatasetActivationCoordinator Coordinator => CoordinatorFor(Environment);
    public DatasetLifecycleStore Lifecycle => new(new DbContextOptionsBuilder<DatasetLifecycleDbContext>()
        .UseNpgsql(lease.ConnectionString).Options, CoordinatedDatabaseTransaction.ForProfile(Profile));
    public FiniteOperationPoolRegistration Pool { get; } = ProjectFiniteOperationPool.Create(new());
    public FiniteOperationEpochStore Epochs => EpochsFor(CoordinatedDatabaseTransaction.ForProfile(Profile));

    public FiniteOperationEpochStore EpochsFor(CoordinatedDatabaseTransaction transactions) => new(EpochOptions, transactions);
    public AppDbContext Projects() => new(CanonicalOptions);
    public DatasetActivationService Files(DurableFileWriter? writer = null) => FilesFor(Environment, writer);

    public DatasetActivationCoordinator CoordinatorFor(CanDoItAllTestEnvironment host, DurableFileWriter? writer = null,
        IReadOnlyCollection<FiniteOperationPoolRegistration>? registrations = null)
        => new(FilesFor(host, writer), registrations ?? [Pool]);

    public async Task<DatasetActivationFileRecord> ReadFileAsync(CanDoItAllTestEnvironment? host = null) {
        using var file = FilesFor(host ?? Environment).Acquire(Profile.Profile.Id);
        return (await file.ReadAsync())!;
    }

    private static DatasetActivationService FilesFor(CanDoItAllTestEnvironment host, DurableFileWriter? writer = null) {
        var policies = new PhysicalFileSystemPathPolicyFactory();
        writer ??= new(policies);
        var paths = new ControlPlanePathResolver(Options.Create(new ControlPlaneOptions { RootPath = host.ControlPlaneRootPath }),
            host.CreateHostEnvironment(nameof(DatasetActivationCoordinatorTests)), writer);
        return new(paths, writer, policies);
    }

    public static async Task<DatasetActivationTestFixture> CreateAsync() {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        var fixture = new DatasetActivationTestFixture(PostgresTestDatabaseLease.Create("dataset-activation-coordinator"),
            CanDoItAllTestEnvironment.Create("dataset-activation-coordinator"));
        try {
            await using var context = new AppDbContext(fixture.CanonicalOptions);
            await context.Database.MigrateAsync();
            return fixture;
        } catch {
            await fixture.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync() {
        try {
            await lease.DisposeAsync();
        } finally {
            await Environment.DisposeAsync();
        }
    }
    public async Task<string> InstallationSnapshotAsync() {
        await using var context = Projects();
        return JsonSerializer.Serialize(new {
            Lifecycle = await context.Set<DatasetLifecycleRecord>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(),
            State = await context.Set<FiniteOperationStateRecord>().AsNoTracking().OrderBy(row => row.NamespaceId).ThenBy(row => row.PoolId).ToArrayAsync(),
            Epochs = await context.Set<FiniteOperationEpochRecord>().AsNoTracking().OrderBy(row => row.NamespaceId).ThenBy(row => row.PoolId)
                .ThenBy(row => row.Epoch).ToArrayAsync()
        });
    }

}
