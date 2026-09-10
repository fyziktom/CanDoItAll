using System.Data;
using System.Data.Common;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectsOwnerPersistenceTests {
    [Fact]
    public async Task Owner_models_match_canonical_mappings_and_keep_the_cross_owner_crm_fk_in_the_complete_model() {
        await using var application = await TestApplication.CreateAsync();
        await using var canonical = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        await using var projects = await application.Services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        await using var workbench = await application.Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        AssertModel(canonical, projects, 5);
        AssertModel(canonical, workbench, 10);
        Assert.Throws<InvalidOperationException>(() => projects.Set<ProjectObjectRecord>().ToQueryString());
        Assert.Throws<InvalidOperationException>(() => workbench.Set<Project>().ToQueryString());
        var crmLink = Assert.IsAssignableFrom<IEntityType>(canonical.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(CrmAccountConnectionProjectLink)));
        Assert.Equal("CrmHr_AccountConnectionProjects", crmLink.GetTableName());
        var relationship = Assert.Single(crmLink.GetForeignKeys(), key => key.PrincipalEntityType.ClrType == typeof(Project));
        Assert.Equal(nameof(CrmAccountConnectionProjectLink.ProjectId), Assert.Single(relationship.Properties).Name);
        Assert.Equal("Projects_Projects", relationship.PrincipalEntityType.GetTableName());
        Assert.Equal(DeleteBehavior.Cascade, relationship.DeleteBehavior);
        Assert.Null(projects.Model.FindEntityType(typeof(CrmAccountConnectionProjectLink)));
        Assert.Null(workbench.Model.FindEntityType(typeof(CrmAccountConnectionProjectLink)));
    }

    [Fact]
    public async Task Legacy_projects_phases_options_and_hierarchy_survive_restart_owner_edits_and_separate_profiles() {
        await using var environment = CanDoItAllTestEnvironment.Create("projects-owner-restart");
        var profile = environment.CreatePostgreSqlProfile("original");
        var otherProfile = environment.CreatePostgreSqlProfile("other");
        var harness = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile };
        var now = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var project = new Project {
            Id = Guid.NewGuid(), Name = "Retained project", Slug = "retained-project", Description = "Legacy description",
            Objective = "Retained objective", Status = ProjectStatus.OnHold, CurrentPhase = "Review",
            TargetDateUtc = now.UtcDateTime.AddDays(7), CreatedAtUtc = now, UpdatedAtUtc = now
        };
        var parent = new Project { Id = Guid.NewGuid(), Name = "Retained parent", Slug = "retained-parent", CreatedAtUtc = now, UpdatedAtUtc = now };
        var phase = new ProjectPhase {
            Id = Guid.NewGuid(), ProjectId = project.Id, Name = "Review", Goal = "Retained phase goal", Status = ProjectPhaseStatus.Blocked,
            OrderIndex = 0, StartDateUtc = now.UtcDateTime, EndDateUtc = now.UtcDateTime.AddDays(2)
        };
        var option = new ProjectOptionSelection {
            Id = Guid.NewGuid(), ProjectId = project.Id, Category = ProjectOptionCategory.Language,
            OptionName = "C#", Notes = "Retained option notes"
        };
        var hierarchy = new ProjectHierarchyLink { Id = Guid.NewGuid(), ParentProjectId = parent.Id, ChildProjectId = project.Id, CreatedAtUtc = now };
        await using (var original = await TestApplication.CreateAsync(harness)) {
            await using var schema = await original.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            schema.AddRange(project, parent, phase, option, hierarchy);
            await schema.SaveChangesAsync();
        }

        await using var restarted = await TestApplication.CreateAsync(harness);
        await using var scope = restarted.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var queries = scope.ServiceProvider.GetRequiredService<ProjectRecordQueryService>();
        await using (var readback = await restarted.Services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync()) {
            Assert.Equal(JsonSerializer.Serialize(project), JsonSerializer.Serialize(await readback.Set<Project>().SingleAsync(item => item.Id == project.Id)));
            Assert.Equal(JsonSerializer.Serialize(phase), JsonSerializer.Serialize(await readback.Set<ProjectPhase>().SingleAsync(item => item.Id == phase.Id)));
            Assert.Equal(JsonSerializer.Serialize(option), JsonSerializer.Serialize(await readback.Set<ProjectOptionSelection>().SingleAsync(item => item.Id == option.Id)));
            Assert.Equal(JsonSerializer.Serialize(hierarchy), JsonSerializer.Serialize(await readback.Set<ProjectHierarchyLink>().SingleAsync(item => item.Id == hierarchy.Id)));
        }
        var editor = await service.GetAsync(project.Id);
        editor.Name = "Updated through Projects owner";
        editor.Phases.Single(item => item.Id == phase.Id).Goal = "Updated phase goal";
        editor.Options.Single(item => item.Id == option.Id).Notes = "Updated option notes";
        var result = await service.SaveAsync(editor);
        Assert.True(result.IsSuccess);
        Assert.Equal(project.Id, result.Value);
        Assert.Equal(parent.Id, Assert.Single((await service.GetHierarchyAsync(project.Id)).ParentProjects).Id);
        await using (var canonical = await restarted.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync()) {
            var saved = await canonical.Set<Project>().SingleAsync(item => item.Id == project.Id);
            Assert.Equal(editor.Name, saved.Name);
            Assert.Equal(project.CreatedAtUtc, saved.CreatedAtUtc);
            Assert.Equal(project.TargetDateUtc, saved.TargetDateUtc);
            Assert.Equal("Updated phase goal", (await canonical.Set<ProjectPhase>().SingleAsync(item => item.Id == phase.Id)).Goal);
            Assert.Equal("Updated option notes", (await canonical.Set<ProjectOptionSelection>().SingleAsync(item => item.Id == option.Id)).Notes);
            Assert.Equal(hierarchy.Id, (await canonical.Set<ProjectHierarchyLink>().SingleAsync(item => item.ChildProjectId == project.Id)).Id);
        }
        await using var other = await TestApplication.CreateAsync(new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = otherProfile });
        await using var otherScope = other.Services.CreateAsyncScope();
        Assert.Null(await otherScope.ServiceProvider.GetRequiredService<ProjectRecordQueryService>().GetAsync(project.Id));
        Assert.Equal(editor.Name, (await queries.GetAsync(project.Id))!.Name);
    }

    [Fact]
    public async Task Bulk_project_queries_remain_uncapped_and_explicit_mutation_reads_share_the_owner_transaction() {
        await using var application = await TestApplication.CreateAsync();
        var profile = application.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        var coordinator = CoordinatedDatabaseTransaction.ForProfile(profile);
        var probe = new CommandProbe();
        var options = new DbContextOptionsBuilder<ProjectsDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, profile);
        options.AddInterceptors(probe);
        var queries = new ProjectRecordQueryService(new Factory(options.Options), options.Options, coordinator);
        var prefix = $"owner-{Guid.NewGuid():N}";
        var records = Enumerable.Range(0, ProjectRecordQueryLimits.MaximumReferenceCount + 1)
            .Select(index => new Project { Id = Guid.NewGuid(), Name = $"{prefix}-{index:D4}", Slug = $"{prefix}-{index:D4}" }).ToArray();
        var ids = records.Select(item => item.Id).ToArray();
        await using var owner = await application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        owner.AddRange(records);
        await owner.SaveChangesAsync();
        Assert.Equal(records.Length, (await queries.GetManyAsync(ids)).Count);
        Assert.Equal(ProjectRecordQueryLimits.MaximumReferenceCount, (await queries.ListReferencesAsync(ProjectRecordQueryLimits.MaximumReferenceCount)).Count);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => queries.ListReferencesAsync(ProjectRecordQueryLimits.MaximumReferenceCount + 1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => queries.GetForMutationAsync(records[0].Id));
        var oldName = records[0].Name;
        await using var transaction = await owner.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        records[0].Name = prefix + "-uncommitted";
        await owner.SaveChangesAsync();
        using (coordinator.Enter(owner)) {
            probe.Commands.Clear();
            Assert.Equal(records[0].Name, (await queries.GetForMutationAsync(records[0].Id))!.Name);
            Assert.Equal(records.Length, (await queries.GetManyForMutationAsync(ids)).Count);
            Assert.NotEmpty(probe.Commands);
            Assert.All(probe.Commands, command => {
                Assert.Same(owner.Database.GetDbConnection(), command.Connection);
                Assert.Same(transaction.GetDbTransaction(), command.Transaction);
            });
            probe.Commands.Clear();
            Assert.Equal(oldName, (await queries.GetAsync(records[0].Id))!.Name);
            Assert.NotEmpty(probe.Commands);
            Assert.All(probe.Commands, command => {
                Assert.NotSame(owner.Database.GetDbConnection(), command.Connection);
                Assert.Null(command.Transaction);
            });
        }
        await transaction.RollbackAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => queries.GetManyForMutationAsync(ids));
        Assert.Equal(oldName, (await queries.GetAsync(records[0].Id))!.Name);
    }

    private static void AssertModel(AppDbContext canonical, DbContext owner, int count) {
        var entities = owner.GetService<IDesignTimeModel>().Model.GetEntityTypes().ToArray();
        Assert.Equal(count, entities.Length);
        foreach (var entity in entities) {
            var full = Assert.IsAssignableFrom<IEntityType>(canonical.GetService<IDesignTimeModel>().Model.FindEntityType(entity.ClrType));
            Assert.Equal(full.ToDebugString(MetadataDebugStringOptions.LongDefault), entity.ToDebugString(MetadataDebugStringOptions.LongDefault));
        }
    }

    private sealed class Factory(DbContextOptions<ProjectsDbContext> options) : IDbContextFactory<ProjectsDbContext> {
        public ProjectsDbContext CreateDbContext() => new(options);
    }

    private sealed class CommandProbe : DbCommandInterceptor {
        public List<(DbConnection? Connection, DbTransaction? Transaction)> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Commands.Add((command.Connection, command.Transaction));
            return ValueTask.FromResult(result);
        }
    }
}
