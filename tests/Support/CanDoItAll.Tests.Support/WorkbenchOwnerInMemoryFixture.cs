using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace CanDoItAll.Tests.Support;

public sealed class WorkbenchOwnerInMemoryFixture {
    public WorkbenchOwnerInMemoryFixture(string prefix, bool ignoreTransactionWarning = false) {
        var name = $"{prefix}-{Guid.NewGuid():N}";
        var root = new InMemoryDatabaseRoot();
        var complete = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name, root);
        AppDbContextOptionsConfigurator.ConfigureModelCacheKey(complete);
        var workbench = new DbContextOptionsBuilder<WorkbenchDbContext>().UseInMemoryDatabase(name, root);
        var projects = new DbContextOptionsBuilder<ProjectsDbContext>().UseInMemoryDatabase(name, root);
        if (ignoreTransactionWarning) {
            complete.ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            workbench.ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));
        }
        CompleteOptions = complete.Options;
        WorkbenchOptions = workbench.Options;
        WorkbenchFactory = new PooledDbContextFactory<WorkbenchDbContext>(workbench.Options);
        ProjectsFactory = new PooledDbContextFactory<ProjectsDbContext>(projects.Options);
        var profile = new ResolvedDatabaseProfile(new DatabaseProfileRecord {
            Id = Guid.NewGuid(),
            ProviderKind = DatabaseProviderKind.InMemory,
            SourceKind = DatabaseProfileSourceKind.InMemory
        }, DatabaseProfileResolutionSource.ExplicitOverride, name);
        Transactions = CoordinatedDatabaseTransaction.ForProfile(profile);
        Projects = new(ProjectsFactory, projects.Options, Transactions);
        Hierarchy = new(ProjectsFactory, projects.Options, Transactions);
        Admissions = new ProjectWriteAdmissionService(ProjectsFactory, projects.Options, Transactions, new CanonicalDatabase(profile));
        MutationScopes = new(Projects, Admissions, Transactions);
    }

    private sealed class CanonicalDatabase(ResolvedDatabaseProfile profile) : ICanonicalRuntimeDatabase {
        public ResolvedDatabaseProfile Profile { get; } = profile;
        public long Generation => 0;
    }

    public DbContextOptions<AppDbContext> CompleteOptions { get; }
    public DbContextOptions<WorkbenchDbContext> WorkbenchOptions { get; }
    public ProjectWriteAdmissionService Admissions { get; }
    public IDbContextFactory<WorkbenchDbContext> WorkbenchFactory { get; }
    public IDbContextFactory<ProjectsDbContext> ProjectsFactory { get; }
    public ProjectRecordQueryService Projects { get; }
    public ProjectStructureProjectionQueryService Hierarchy { get; }
    public ProjectStructureMutationScopeFactory MutationScopes { get; }
    public CoordinatedDatabaseTransaction Transactions { get; }
}
