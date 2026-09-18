using System.Data;
using System.Data.Common;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectCurrentLifetimeBatchIntegrationTests {
    [Fact]
    public async Task Current_lifetime_batch_queries_once_and_stays_independent_of_an_entered_mutation() {
        await using var application = await TestApplication.CreateAsync();
        var canonical = application.Services.GetRequiredService<ICanonicalRuntimeDatabase>();
        var coordinator = CoordinatedDatabaseTransaction.ForProfile(canonical.Profile);
        var probe = new CommandProbe();
        var (admissions, factory) = CreateAdmissions(canonical, coordinator, probe);
        await using var owner = await application.Services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        Project[] projects = [new() { Name = "First" }, new() { Name = "Second" }, new() { Name = "Third" }];
        owner.AddRange(projects);
        await owner.SaveChangesAsync();
        var targets = projects.Select(project => new ProjectWriteAdmission(admissions.DatabaseProfileId, project.Id, project.LifetimeId)).ToArray();

        await admissions.RequireManyCurrentAsync([]);
        await Assert.ThrowsAsync<ArgumentNullException>(() => admissions.RequireManyCurrentAsync(null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => admissions.RequireManyCurrentAsync([targets[0], null!]));
        var foreign = new ProjectWriteAdmission(Guid.NewGuid(), targets[0].ProjectId, targets[0].LifetimeId);
        var foreignFailure = await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() =>
            admissions.RequireManyCurrentAsync([targets[0], foreign]));
        Assert.Same(foreign, foreignFailure.Admission);
        Assert.Equal(0, factory.CreatedContexts);
        Assert.Empty(probe.Commands);

        await admissions.RequireManyCurrentAsync([targets[0], targets[1], targets[0], targets[2]]);
        var command = Assert.Single(probe.Commands);
        Assert.Equal(targets.Select(target => target.ProjectId).Order(), command.ProjectIds.Order());
        Assert.Null(command.Transaction);
        Assert.Equal(1, factory.CreatedContexts);

        probe.Commands.Clear();
        var conflicting = new ProjectWriteAdmission(admissions.DatabaseProfileId, targets[0].ProjectId, Guid.NewGuid());
        var duplicateFailure = await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() =>
            admissions.RequireManyCurrentAsync([targets[0], conflicting, targets[1]]));
        Assert.Same(conflicting, duplicateFailure.Admission);
        Assert.Equal(2, Assert.Single(probe.Commands).ProjectIds.Length);
        await Assert.ThrowsAsync<ArgumentException>(() => admissions.RequireManyForMutationAsync([targets[0], targets[0]]));

        probe.Commands.Clear();
        await using var transaction = await owner.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var pending = new Project { Name = "Uncommitted project" };
        owner.Add(pending);
        await owner.SaveChangesAsync();
        var pendingAdmission = new ProjectWriteAdmission(admissions.DatabaseProfileId, pending.Id, pending.LifetimeId);
        using (coordinator.Enter(owner)) {
            await admissions.RequireManyCurrentAsync(targets);
            var independent = Assert.Single(probe.Commands);
            Assert.NotSame(owner.Database.GetDbConnection(), independent.Connection);
            Assert.Null(independent.Transaction);

            probe.Commands.Clear();
            var uncommittedFailure = await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() =>
                admissions.RequireManyCurrentAsync([targets[0], pendingAdmission]));
            Assert.Same(pendingAdmission, uncommittedFailure.Admission);
            Assert.Null(Assert.Single(probe.Commands).Transaction);
        }
        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task Current_lifetime_batch_rejects_missing_retired_and_recreated_evidence_after_restart() {
        await using var environment = CanDoItAllTestEnvironment.Create("project-current-lifetime-batch");
        var profile = environment.CreatePostgreSqlProfile("restart");
        var harness = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile };
        ProjectWriteAdmission[] targets;
        ProjectWriteAdmission replacementAdmission;
        await using (var initial = await TestApplication.CreateAsync(harness)) {
            var canonical = initial.Services.GetRequiredService<ICanonicalRuntimeDatabase>();
            await using var owner = await initial.Services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
            Project[] projects = [new() { Name = "Retained live" }, new() { Name = "Deleted" },
                new() { Name = "Retired retained row" }, new() { Name = "Original incarnation" }];
            owner.AddRange(projects);
            await owner.SaveChangesAsync();
            targets = projects.Select(project => new ProjectWriteAdmission(canonical.Profile.Profile.Id, project.Id, project.LifetimeId)).ToArray();

            owner.AddRange(projects.Skip(1).Select(project => new ProjectRetirementRecord {
                ProjectId = project.Id, LifetimeId = project.LifetimeId, RetiredAtUtc = DateTimeOffset.UtcNow
            }));
            owner.Remove(projects[1]);
            owner.Remove(projects[3]);
            await owner.SaveChangesAsync();
            var replacement = new Project { Id = projects[3].Id, Name = "New incarnation" };
            owner.Add(replacement);
            await owner.SaveChangesAsync();
            replacementAdmission = new(canonical.Profile.Profile.Id, replacement.Id, replacement.LifetimeId);
        }

        await using var restarted = await TestApplication.CreateAsync(harness);
        var restartedCanonical = restarted.Services.GetRequiredService<ICanonicalRuntimeDatabase>();
        var probe = new CommandProbe();
        var (admissions, _) = CreateAdmissions(restartedCanonical,
            CoordinatedDatabaseTransaction.ForProfile(restartedCanonical.Profile), probe);
        await admissions.RequireManyCurrentAsync([targets[0], replacementAdmission]);
        Assert.Single(probe.Commands);
        Assert.NotEqual(targets[3].LifetimeId, replacementAdmission.LifetimeId);
        foreach (var rejected in targets.Skip(1)) {
            probe.Commands.Clear();
            var failure = await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() =>
                admissions.RequireManyCurrentAsync([targets[0], rejected, replacementAdmission]));
            Assert.Same(rejected, failure.Admission);
            Assert.Null(Assert.Single(probe.Commands).Transaction);
        }
        await using var readback = await restarted.Services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        Assert.Equal(3, await readback.Set<ProjectRetirementRecord>().CountAsync());
        var retained = await readback.Set<Project>().AsNoTracking().SingleAsync(project => project.Id == targets[2].ProjectId);
        Assert.Equal(targets[2].LifetimeId, retained.LifetimeId);
        Assert.Equal("Retired retained row", retained.Name);
        Assert.Equal(replacementAdmission.LifetimeId, (await readback.Set<Project>().AsNoTracking()
            .SingleAsync(project => project.Id == replacementAdmission.ProjectId)).LifetimeId);
    }

    private static (ProjectWriteAdmissionService Admissions, Factory Factory) CreateAdmissions(
        ICanonicalRuntimeDatabase canonical, CoordinatedDatabaseTransaction coordinator, CommandProbe probe) {
        var options = new DbContextOptionsBuilder<ProjectsDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, canonical.Profile);
        options.AddInterceptors(probe);
        var factory = new Factory(options.Options);
        return (new(factory, options.Options, coordinator, canonical), factory);
    }

    private sealed class Factory(DbContextOptions<ProjectsDbContext> options) : IDbContextFactory<ProjectsDbContext> {
        public int CreatedContexts { get; private set; }

        public ProjectsDbContext CreateDbContext() {
            CreatedContexts++;
            return new(options);
        }

        public Task<ProjectsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }

    private sealed record CommandObservation(DbConnection Connection, DbTransaction? Transaction, Guid[] ProjectIds);

    private sealed class CommandProbe : DbCommandInterceptor {
        public List<CommandObservation> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            var projectIds = command.Parameters.Cast<DbParameter>().Select(parameter => parameter.Value).OfType<Guid[]>().Single();
            Commands.Add(new(command.Connection!, command.Transaction, projectIds.ToArray()));
            return ValueTask.FromResult(result);
        }
    }
}
