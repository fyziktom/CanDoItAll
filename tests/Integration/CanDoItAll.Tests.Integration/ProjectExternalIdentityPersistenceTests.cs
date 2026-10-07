using System.Text.Json;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectExternalIdentityPersistenceTests {
    private const string PreviousMigration = "20260927120448_AddInvocationCompletionUsageIndex";
    private const string IdentityScope = "synthetic-project-external-identity-test";

    [Fact]
    public async Task Migration_preserves_existing_project_and_lifetime_and_refuses_loss_of_assigned_identity() {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        await using var lease = PostgresTestDatabaseLease.Create("project-identity-migration");
        var id = Guid.NewGuid();
        var lifetime = Guid.NewGuid();
        await using (var previous = new AppDbContext(lease.CreateAppDbContextOptions())) {
            await previous.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            await previous.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Projects_Projects" ("Id", "LifetimeId", "Name", "Slug", "Description", "Objective", "Status", "CurrentPhase", "CreatedAtUtc", "UpdatedAtUtc")
                VALUES ({id}, {lifetime}, 'Synthetic legacy project', 'legacy', 'Retained description', 'Retained objective', 0, 'Plan', {DateTimeOffset.UnixEpoch}, {DateTimeOffset.UnixEpoch})
                """);
        }
        for (var restart = 0; restart < 2; restart++) {
            await using var upgraded = new AppDbContext(lease.CreateAppDbContextOptions());
            await upgraded.Database.MigrateAsync();
            Assert.False(upgraded.Database.HasPendingModelChanges());
            var project = await upgraded.Set<Project>().SingleAsync();
            Assert.Equal(id, project.Id);
            Assert.Equal(lifetime, project.LifetimeId);
            Assert.Equal("Retained description", project.Description);
            Assert.Null(project.ExternalNamespace);
            Assert.Null(project.ExternalKey);
        }
        await using var current = new AppDbContext(lease.CreateAppDbContextOptions());
        var bound = await current.Set<Project>().SingleAsync();
        bound.ExternalNamespace = "synthetic";
        bound.ExternalKey = "resort";
        await current.SaveChangesAsync();
        await Assert.ThrowsAsync<PostgresException>(() => current.GetService<IMigrator>().MigrateAsync(PreviousMigration));
        await using var readback = new AppDbContext(lease.CreateAppDbContextOptions());
        Assert.Equal("resort", (await readback.Set<Project>().SingleAsync()).ExternalKey);
        Assert.NotEqual(PreviousMigration, (await readback.Database.GetAppliedMigrationsAsync()).Last());
    }

    [Theory]
    [InlineData(null, "resort")]
    [InlineData("synthetic", null)]
    [InlineData("Synthetic", "resort")]
    [InlineData("synthetic", "")]
    [InlineData("synthetic", "resort-")]
    [InlineData("synthetic", "resort\n")]
    public async Task Database_rejects_malformed_pairs_even_when_service_is_bypassed(string? externalNamespace, string? externalKey) {
        await using var database = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var context = database.Factory.CreateDbContext();
        context.Add(new Project { Name = "Invalid direct insert", ExternalNamespace = externalNamespace, ExternalKey = externalKey });
        var failure = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var provider = Assert.IsType<PostgresException>(failure.InnerException);
        Assert.Equal(PostgresErrorCodes.CheckViolation, provider.SqlState);
        Assert.Equal("CK_Projects_ExternalIdentity", provider.ConstraintName);
    }

    [Fact]
    public async Task Unique_index_arbitrates_independent_concurrent_writers_and_allows_multiple_unbound_projects() {
        await using var database = await HistoryPersistenceTestDatabase.CreateAsync();
        await using (var unbound = database.Factory.CreateDbContext()) {
            unbound.AddRange(new Project { Name = "Unbound one" }, new Project { Name = "Unbound two" });
            await unbound.SaveChangesAsync();
        }
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var writers = 0;
        async Task<bool> InsertAsync() {
            await using var context = database.Factory.CreateDbContext();
            context.Add(new Project { Name = "Concurrent synthetic insert", ExternalNamespace = "synthetic", ExternalKey = "resort" });
            if (Interlocked.Increment(ref writers) == 2) {
                ready.SetResult();
            }
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(30));
            try {
                await context.SaveChangesAsync();
                return true;
            } catch (DbUpdateException exception) {
                var provider = Assert.IsType<PostgresException>(exception.InnerException);
                Assert.Equal(PostgresErrorCodes.UniqueViolation, provider.SqlState);
                Assert.Equal("UX_Projects_ExternalIdentity", provider.ConstraintName);
                return false;
            }
        }
        var results = await Task.WhenAll(InsertAsync(), InsertAsync());
        Assert.Single(results, succeeded => succeeded);
        Assert.Single(results, succeeded => !succeeded);
        await using var readback = database.Factory.CreateDbContext();
        Assert.Equal(3, await readback.Set<Project>().CountAsync());
    }

    [Fact]
    public async Task Transfer_and_package_payload_preserve_identity_and_create_fresh_target_lifetime() {
        await using var source = await HistoryPersistenceTestDatabase.CreateAsync();
        var project = new Project { Name = "Transferred synthetic resort", ExternalNamespace = "synthetic", ExternalKey = "resort" };
        await using (var context = source.Factory.CreateDbContext()) {
            context.Add(project);
            await context.SaveChangesAsync();
        }
        var operations = DatabaseTransferTestSupport.Create();
        var store = new ProjectsProfileTransferStore(operations);
        var exported = await operations.RunIndependentAsync(source.Profile, (session, token) => store.LoadAsync(session, token));
        var package = new ProjectTransferDataSet { Projects = exported.Projects.ToList() };
        package.PrepareForTargetImport(source.Profile.Profile.Id, Guid.NewGuid());
        var imported = JsonSerializer.Deserialize<ProjectsProfileTransferData>(JsonSerializer.Serialize(exported with { Projects = package.Projects }))!;
        Assert.Equal("synthetic", Assert.Single(imported.Projects).ExternalNamespace);
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync();
        await operations.RunSerializableAsync(target.Profile, [IdentityScope], async (session, token) => {
            await store.SaveAsync(session, imported, token);
            return true;
        });
        await using var readback = target.Factory.CreateDbContext();
        var restored = await readback.Set<Project>().SingleAsync();
        Assert.Equal(project.Id, restored.Id);
        Assert.NotEqual(project.LifetimeId, restored.LifetimeId);
        Assert.Equal(project.ExternalNamespace, restored.ExternalNamespace);
        Assert.Equal(project.ExternalKey, restored.ExternalKey);
    }

    [Fact]
    public async Task Later_identity_binding_prevents_creation_compensation_even_without_timestamp_change() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var projects = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var created = await projects.CreateWithReceiptAsync(Guid.NewGuid(), new ProjectEditorModel { Name = "Synthetic compensation evidence" });
        Assert.True(created.IsSuccess);
        var receipt = created.Value!;
        await using (var context = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync()) {
            var project = await context.Set<Project>().SingleAsync(row => row.Id == receipt.Project.ProjectId);
            project.ExternalNamespace = "synthetic";
            project.ExternalKey = "resort";
            await context.SaveChangesAsync();
        }
        Assert.False(await projects.TryCompensateCreationAsync(receipt));
        Assert.True((await scope.ServiceProvider.GetRequiredService<ProjectIdentityQueryService>()
            .ResolveExternalIdentityAsync("synthetic", "resort", receipt.Project.LifetimeId)).IsSuccess);
    }
}
