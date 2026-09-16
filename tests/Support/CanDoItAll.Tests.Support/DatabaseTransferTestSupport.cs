using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace CanDoItAll.Tests.Support;

public static class DatabaseTransferTestSupport {
    public static DatabaseTransferOperationRunner Create(DatabaseTransferOwnerSessionRunner? owners = null,
        IProfileAppDbContextFactory? contexts = null, ProjectTransferTargetInspectionRunner? inspections = null)
        => new(contexts ?? new ProfileAppDbContextFactory(), owners ?? new(), inspections ?? new());

    public static DatabaseTransferOperationRunner For(DatabaseTransferOperation transfer, AppDbContext source,
        AppDbContext target, DatabaseTransferOwnerSessionRunner? owners = null, ProjectTransferTargetInspectionRunner? inspections = null)
        => Create(owners, new FixtureContexts([(transfer.SourceProfile, source), (transfer.TargetProfile, target)]), inspections);

    public static DatabaseTransferOperationRunner ForProfile(ResolvedDatabaseProfile profile, AppDbContext context,
        ProjectTransferTargetInspectionRunner? inspections = null)
        => Create(contexts: new FixtureContexts([(profile, context)]), inspections: inspections);

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
