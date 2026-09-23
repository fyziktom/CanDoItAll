using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectCreationReservationIntegrationTests {
    [Fact]
    public async Task Agent_coordinator_persists_exact_grant_before_creation_and_consumes_reservation_with_project() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var agent = await CreateAgentAsync(workspace);
        var projects = services.GetRequiredService<ProjectsService>();
        var factory = services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>();
        var coordinator = services.GetRequiredService<ProjectStructureAgentProjectCreationCoordinator>();
        ProjectCreationReservation? captured = null;
        var id = await coordinator.CreateAsync(agent, async (reservation, token) => {
            captured = reservation;
            await using var before = await factory.CreateDbContextAsync(token);
            Assert.False(await before.Set<Project>().AnyAsync(project => project.Id == reservation.ProjectId, token));
            var reserved = await before.Set<ProjectCreationReservationRecord>().SingleAsync(record => record.Id == reservation.Id, token);
            Assert.Equal(ProjectCreationReservationState.Reserved, reserved.State);
            Assert.Equal(new AgentProjectStructureLifetime(reservation.DatabaseProfileId, reservation.ProjectId, reservation.LifetimeId),
                Assert.Single((await ReadAccessAsync(workspace, agent.Id)).AllowedProjectLifetimes));
            var result = await projects.CreateAsync(reservation, new() { Name = "Reserved agent project" }, token);
            Assert.True(result.IsSuccess);
            return result.Value;
        }, id => id, CancellationToken.None);
        Assert.NotNull(captured);
        Assert.Equal(captured.ProjectId, id);
        await using var context = await factory.CreateDbContextAsync();
        var project = await context.Set<Project>().SingleAsync(project => project.Id == id);
        var saved = await context.Set<ProjectCreationReservationRecord>().SingleAsync(record => record.Id == captured.Id);
        Assert.Equal(captured.LifetimeId, project.LifetimeId);
        Assert.False(project.LegacyAgentAccessBindingEligible);
        Assert.Equal(ProjectCreationReservationState.Consumed, saved.State);
        Assert.NotNull(saved.ConsumedAtUtc);
        Assert.Null(saved.CancelledAtUtc);
        await Assert.ThrowsAsync<InvalidOperationException>(() => services.GetRequiredService<ProjectWriteAdmissionService>().CancelCreationAsync(captured));
    }

    [Fact]
    public async Task Reservation_survives_restart_matches_canonical_mapping_and_rejects_foreign_profile() {
        await using var environment = CanDoItAllTestEnvironment.Create("creation-reservation-restart");
        var profile = environment.CreatePostgreSqlProfile("original");
        var otherProfile = environment.CreatePostgreSqlProfile("other");
        var harness = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile };
        ProjectCreationReservation reservation;
        await using (var original = await TestApplication.CreateAsync(harness)) {
            await using var scope = original.Services.CreateAsyncScope();
            reservation = await scope.ServiceProvider.GetRequiredService<ProjectWriteAdmissionService>()
                .ReserveCreationAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            await using var owner = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
            await using var canonical = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            var entity = owner.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(ProjectCreationReservationRecord))!;
            Assert.Equal(canonical.GetService<IDesignTimeModel>().Model.FindEntityType(entity.ClrType)!
                .ToDebugString(MetadataDebugStringOptions.LongDefault), entity.ToDebugString(MetadataDebugStringOptions.LongDefault));
            Assert.Empty(entity.GetForeignKeys());
        }
        await using var restarted = await TestApplication.CreateAsync(harness);
        await using var restartedScope = restarted.Services.CreateAsyncScope();
        var admissions = restartedScope.ServiceProvider.GetRequiredService<ProjectWriteAdmissionService>();
        Assert.Equal(reservation, await admissions.ReserveCreationAsync(reservation.ProjectId, reservation.RequesterId, reservation.Id));
        await admissions.RequireCreationGrantAsync(reservation);
        await using var other = await TestApplication.CreateAsync(new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = otherProfile });
        await using var otherScope = other.Services.CreateAsyncScope();
        await Assert.ThrowsAsync<InvalidOperationException>(() => otherScope.ServiceProvider.GetRequiredService<ProjectWriteAdmissionService>()
            .RequireCreationGrantAsync(reservation));
        Assert.True((await restartedScope.ServiceProvider.GetRequiredService<ProjectsService>()
            .CreateAsync(reservation, new() { Name = "Created after restart" })).IsSuccess);
    }

    [Fact]
    public async Task Deleting_pending_id_cancels_reservation_and_exact_grant_while_new_public_id_lifetime_remains_supported() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var agent = await CreateAgentAsync(workspace);
        var admissions = services.GetRequiredService<ProjectWriteAdmissionService>();
        var reservation = await admissions.ReserveCreationAsync(Guid.NewGuid(), agent.Id, Guid.NewGuid());
        await workspace.GrantAgentProjectStructureLifetimeAsync(agent.Id, new(reservation.DatabaseProfileId, reservation.ProjectId, reservation.LifetimeId));
        var projects = services.GetRequiredService<ProjectsService>();
        Assert.True((await projects.CreateAsync(reservation.ProjectId, new() { Name = "Unreserved conflict" })).IsFailure);
        await projects.DeleteAsync(reservation.ProjectId);
        Assert.Empty((await ReadAccessAsync(workspace, agent.Id)).AllowedProjectIds);
        await Assert.ThrowsAsync<InvalidOperationException>(() => admissions.RequireCreationGrantAsync(reservation));
        var denied = await projects.CreateAsync(reservation, new() { Name = "Stale reserved create" });
        Assert.Equal(ProjectErrorCodes.ReservationClosed, Assert.Single(denied.Errors).Code);
        Assert.True((await projects.CreateAsync(reservation.ProjectId, new() { Name = "Legitimate new creation" })).IsSuccess);
        Assert.NotEqual(reservation.LifetimeId, (await admissions.CaptureAsync(reservation.ProjectId))!.LifetimeId);
        await using var context = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        var saved = await context.Set<ProjectCreationReservationRecord>().SingleAsync(record => record.Id == reservation.Id);
        Assert.Equal(ProjectCreationReservationState.Cancelled, saved.State);
        Assert.NotNull(saved.CancelledAtUtc);
    }

    [Fact]
    public async Task Parent_deletion_cancels_reserved_children_and_recreation_cannot_consume_the_old_parent_binding() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projects = services.GetRequiredService<ProjectsService>();
        var parentId = Guid.NewGuid();
        Assert.True((await projects.CreateAsync(parentId, new() { Name = "First parent" })).IsSuccess);
        var admissions = services.GetRequiredService<ProjectWriteAdmissionService>();
        var reservation = await admissions.ReserveCreationAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), parentId);
        await projects.DeleteAsync(parentId);
        Assert.True((await projects.CreateAsync(parentId, new() { Name = "Replacement parent" })).IsSuccess);
        Assert.NotEqual(reservation.ParentLifetimeId, (await admissions.CaptureAsync(parentId))!.LifetimeId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => admissions.RequireCreationGrantAsync(reservation));
        var denied = await projects.CreateSubprojectAsync(parentId, reservation, new() { Name = "Late child" });
        Assert.Equal(ProjectErrorCodes.ReservationClosed, Assert.Single(denied.Errors).Code);
        Assert.Null(await admissions.CaptureAsync(reservation.ProjectId));
        Assert.Empty((await admissions.ListAccessBindingFactsAsync([reservation.ProjectId])).Reservations);
    }

    [Fact]
    public async Task Project_insert_failure_rolls_back_reservation_consumption_and_retry_uses_the_same_exact_lifetime() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var admissions = services.GetRequiredService<ProjectWriteAdmissionService>();
        var reservation = await admissions.ReserveCreationAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var options = new DbContextOptionsBuilder<ProjectsDbContext>(services.GetRequiredService<DbContextOptions<ProjectsDbContext>>())
            .AddInterceptors(new RejectProjectInsert()).Options;
        var failingProjects = ActivatorUtilities.CreateInstance<ProjectsService>(services, new ProjectFactory(options));
        await Assert.ThrowsAsync<InjectedProjectSaveException>(() => failingProjects.CreateAsync(reservation, new() { Name = "Rejected insert" }));
        await using (var read = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync()) {
            Assert.False(await read.Set<Project>().AnyAsync(project => project.Id == reservation.ProjectId));
            var saved = await read.Set<ProjectCreationReservationRecord>().SingleAsync(record => record.Id == reservation.Id);
            Assert.Equal(ProjectCreationReservationState.Reserved, saved.State);
            Assert.Null(saved.ConsumedAtUtc);
        }
        Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(reservation, new() { Name = "Successful retry" })).IsSuccess);
        Assert.Equal(reservation.LifetimeId, (await admissions.CaptureAsync(reservation.ProjectId))!.LifetimeId);
    }

    [Fact]
    public async Task One_public_id_has_one_active_operation_and_cancellation_allows_a_distinct_reservation() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var admissions = scope.ServiceProvider.GetRequiredService<ProjectWriteAdmissionService>();
        var projectId = Guid.NewGuid();
        var requester = Guid.NewGuid();
        var first = await admissions.ReserveCreationAsync(projectId, requester, Guid.NewGuid());
        await Assert.ThrowsAsync<InvalidOperationException>(() => admissions.ReserveCreationAsync(projectId, requester, Guid.NewGuid()));
        await admissions.CancelCreationAsync(first);
        var second = await admissions.ReserveCreationAsync(projectId, requester, Guid.NewGuid());
        Assert.NotEqual(first.Id, second.Id);
        Assert.NotEqual(first.LifetimeId, second.LifetimeId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => admissions.RequireCreationGrantAsync(first));
        await admissions.RequireCreationGrantAsync(second);
    }

    private static async Task<AgentDefinition> CreateAgentAsync(IAgentFrameworkWorkspaceService workspace) {
        var editor = await workspace.GetAgentEditorAsync();
        editor.Name = "Reservation test agent";
        editor.RoleTitle = "Project creator";
        editor.Summary = "Reserve a project lifetime before creation.";
        editor.Instructions = "Create only explicitly requested projects.";
        editor.Status = AgentLifecycleStatus.Active;
        editor.ProjectStructureAccess = new() { CanCreateProjects = true, CanCreateSubprojects = true };
        var id = await workspace.SaveAgentAsync(editor);
        return Assert.Single(await workspace.ListAgentsAsync(), agent => agent.Id == id);
    }

    private static async Task<AgentProjectStructureAccessSettings> ReadAccessAsync(IAgentFrameworkWorkspaceService workspace, Guid id) =>
        AgentProjectStructureAccessMetadata.Read(Assert.Single(await workspace.ListAgentsAsync(), agent => agent.Id == id).ConfigurationJson);

    private sealed class ProjectFactory(DbContextOptions<ProjectsDbContext> options) : IDbContextFactory<ProjectsDbContext> {
        public ProjectsDbContext CreateDbContext() => new(options);
    }

    private sealed class InjectedProjectSaveException : Exception { }

    private sealed class RejectProjectInsert : SaveChangesInterceptor {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (eventData.Context!.ChangeTracker.Entries<Project>().Any(entry => entry.State == EntityState.Added)) {
                Assert.Contains(eventData.Context.ChangeTracker.Entries<ProjectCreationReservationRecord>(), entry =>
                    entry.Entity.State == ProjectCreationReservationState.Consumed);
                throw new InjectedProjectSaveException();
            }
            return ValueTask.FromResult(result);
        }
    }
}
