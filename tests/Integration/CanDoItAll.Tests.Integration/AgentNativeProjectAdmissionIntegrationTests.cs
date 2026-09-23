using System.Data;
using System.Data.Common;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class AgentNativeProjectAdmissionIntegrationTests {
    public enum NativeMutation { Create, Edit, Reclassify, Metadata, Status, Progress, Marker, Priority }

    [Theory]
    [InlineData(NativeMutation.Create)]
    [InlineData(NativeMutation.Edit)]
    [InlineData(NativeMutation.Reclassify)]
    [InlineData(NativeMutation.Metadata)]
    [InlineData(NativeMutation.Status)]
    [InlineData(NativeMutation.Progress)]
    [InlineData(NativeMutation.Marker)]
    [InlineData(NativeMutation.Priority)]
    public async Task Delayed_native_mutation_cannot_modify_a_recreated_project_even_when_its_saved_node_key_is_restored(NativeMutation mutation) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projects = services.GetRequiredService<ProjectsService>();
        var admissions = services.GetRequiredService<ProjectWriteAdmissionService>();
        var projectId = Guid.NewGuid();
        Assert.True((await projects.CreateAsync(projectId, new() { Name = "First native lifetime" })).IsSuccess);
        var old = Assert.IsType<ProjectWriteAdmission>(await admissions.CaptureAsync(projectId));
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var node = await workbench.CreateObjectAsync(projectId, new(ProjectObjectType.ProjectBlock, "Original", "", "", $"project:{projectId}"));
        await projects.DeleteAsync(projectId);
        Assert.True((await projects.CreateAsync(projectId, new() { Name = "Restored public id" })).IsSuccess);
        await using (var restored = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync()) {
            restored.Add(new ProjectObjectRecord {
                ProjectId = projectId, NodeKey = node.Id, ObjectType = ProjectObjectType.ProjectBlock, Title = "Current saved node",
                ParentNodeKey = $"project:{projectId}", MetadataJson = "{}", MarkersJson = "[]", Status = "Draft"
            });
            await restored.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(async () => {
            switch (mutation) {
                case NativeMutation.Create:
                    await workbench.CreateObjectAsync(projectId, new(ProjectObjectType.ProjectBlock, "Late create", "", "", $"project:{projectId}") {
                        ExpectedProjectAdmission = old
                    });
                    break;
                case NativeMutation.Edit:
                    await workbench.UpdateObjectAsync(projectId, node.Id, new("Late edit", "", "", null, null, "{}") {
                        ExpectedProjectAdmission = old
                    });
                    break;
                case NativeMutation.Reclassify:
                    await workbench.ReclassifyObjectAsync(projectId, node.Id, new(ProjectObjectType.ProjectBlock, "feature", "Late type", "", "") {
                        ExpectedProjectAdmission = old
                    });
                    break;
                case NativeMutation.Metadata:
                    await workbench.UpdateObjectMetadataAsync(projectId, node.Id, "{\"late\":true}", expectedProjectAdmission: old);
                    break;
                case NativeMutation.Status:
                    await workbench.UpdateObjectStatusesAsync(projectId, [node.Id], "Published", expectedProjectAdmission: old);
                    break;
                case NativeMutation.Progress:
                    await workbench.UpdateObjectProgressAsync(projectId, [node.Id], "manual", 71, expectedProjectAdmission: old);
                    break;
                case NativeMutation.Marker:
                    await workbench.UpdateObjectMarkerAsync(projectId, [node.Id], "flag", "danger", "Late", expectedProjectAdmission: old);
                    break;
                case NativeMutation.Priority:
                    await workbench.UpdateObjectPriorityAsync(projectId, [node.Id], 5, expectedProjectAdmission: old);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation));
            }
        });
        await using (var read = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync()) {
            var current = Assert.Single(await read.Set<ProjectObjectRecord>().Where(record => record.ProjectId == projectId).ToArrayAsync());
            Assert.Equal("Current saved node", current.Title);
            Assert.Equal("Draft", current.Status);
            Assert.Equal("{}", current.MetadataJson);
            Assert.Equal("[]", current.MarkersJson);
        }
        var active = Assert.IsType<ProjectWriteAdmission>(await admissions.CaptureAsync(projectId));
        Assert.NotEqual(old.LifetimeId, active.LifetimeId);
        Assert.Equal(1, await workbench.UpdateObjectStatusesAsync(projectId, [node.Id], "Published", expectedProjectAdmission: active));
    }

    [Fact]
    public async Task Scope_checks_complete_admission_set_with_one_read_on_actual_owner_connection_transaction_and_rolls_back() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projects = services.GetRequiredService<ProjectsService>();
        var first = (await projects.SaveAsync(new() { Name = "First scoped project" })).Value;
        var second = (await projects.SaveAsync(new() { Name = "Second scoped project" })).Value;
        var ownerAdmissions = services.GetRequiredService<ProjectWriteAdmissionService>();
        var captured = new[] { (await ownerAdmissions.CaptureAsync(first))!, (await ownerAdmissions.CaptureAsync(second))! };
        var probe = new ProjectQueryProbe();
        var options = new DbContextOptionsBuilder<ProjectsDbContext>(services.GetRequiredService<DbContextOptions<ProjectsDbContext>>())
            .AddInterceptors(probe).Options;
        var coordinator = services.GetRequiredService<CoordinatedDatabaseTransaction>();
        var admissionReader = new ProjectWriteAdmissionService(new ProjectFactory(options), options, coordinator,
            services.GetRequiredService<ICanonicalRuntimeDatabase>());
        var scopes = new ProjectStructureMutationScopeFactory(services.GetRequiredService<ProjectRecordQueryService>(), admissionReader, coordinator);
        var id = Guid.NewGuid();
        await using (var owner = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync()) {
            await using var mutation = await scopes.BeginBindingWriteAsync(owner,
                ProjectStructureSerializableMutationScope.ForProjects(first, second), CancellationToken.None, captured);
            var command = Assert.Single(probe.Commands);
            Assert.Same(owner.Database.GetDbConnection(), command.Connection);
            Assert.Same(owner.Database.CurrentTransaction!.GetDbTransaction(), command.Transaction);
            Assert.Equal(IsolationLevel.Serializable, command.Transaction!.IsolationLevel);
            owner.Add(new ProjectObjectRecord { Id = id, ProjectId = first, NodeKey = $"node:{id:N}", ObjectType = ProjectObjectType.ProjectBlock, Title = "Rollback" });
            await owner.SaveChangesAsync();
            await using var independent = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
            Assert.False(await independent.Set<ProjectObjectRecord>().AnyAsync(record => record.Id == id));
        }
        await using var read = await services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        Assert.False(await read.Set<ProjectObjectRecord>().AnyAsync(record => record.Id == id));
        await Assert.ThrowsAsync<ArgumentException>(() => scopes.BeginBindingWriteAsync(read,
            ProjectStructureSerializableMutationScope.ForProjects(first, second), CancellationToken.None, [captured[0]]));
        Assert.Single(probe.Commands);
        await Assert.ThrowsAsync<InvalidOperationException>(() => admissionReader.RequireManyForMutationAsync(captured));
    }

    [Fact]
    public async Task Agent_capture_intersects_original_and_current_exact_grants_and_never_binds_bare_ids_to_new_lifetimes() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projects = services.GetRequiredService<ProjectsService>();
        var projectId = (await projects.SaveAsync(new() { Name = "Original grant project" })).Value;
        var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var editor = await workspace.GetAgentEditorAsync();
        editor.Name = "Native admission agent";
        editor.RoleTitle = "Structure editor";
        editor.Summary = "Carry exact native admission.";
        editor.Instructions = "Use only captured project grants.";
        editor.Status = AgentLifecycleStatus.Active;
        editor.ProjectStructureAccess = new() { CanWrite = true, AllowedProjectIds = [projectId] };
        var agentId = await workspace.SaveAgentAsync(editor);
        var originalAccess = AgentProjectStructureAccessMetadata.Read(Assert.Single(await workspace.ListAgentsAsync(), agent => agent.Id == agentId).ConfigurationJson);
        var capture = services.GetRequiredService<ProjectStructureAgentAdmissionService>();
        var original = await capture.CaptureNonTaskWriteAsync(agentId, originalAccess, projectId);
        await workspace.RevokeAgentProjectStructureLifetimeAsync(agentId, new(original.DatabaseProfileId, projectId, original.LifetimeId));
        await Assert.ThrowsAsync<ProjectStructureAgentException>(() => capture.CaptureNonTaskWriteAsync(agentId, originalAccess, projectId));
        await projects.DeleteAsync(projectId);
        Assert.True((await projects.CreateAsync(projectId, new() { Name = "New project grant" })).IsSuccess);
        var current = (await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(projectId))!;
        await workspace.GrantAgentProjectStructureLifetimeAsync(agentId, new(current.DatabaseProfileId, projectId, current.LifetimeId));
        await Assert.ThrowsAsync<ProjectStructureAgentException>(() => capture.CaptureNonTaskWriteAsync(agentId, originalAccess, projectId));
        await Assert.ThrowsAsync<ProjectStructureAgentException>(() => capture.CaptureNonTaskWriteAsync(agentId,
            new() { CanWrite = true, AllowedProjectIds = [projectId] }, projectId));
        var currentAccess = AgentProjectStructureAccessMetadata.Read(Assert.Single(await workspace.ListAgentsAsync(), agent => agent.Id == agentId).ConfigurationJson);
        Assert.Equal(current, await capture.CaptureNonTaskWriteAsync(agentId, currentAccess, projectId));
    }

    private sealed class ProjectFactory(DbContextOptions<ProjectsDbContext> options) : IDbContextFactory<ProjectsDbContext> {
        public ProjectsDbContext CreateDbContext() => new(options);
    }

    private sealed class ProjectQueryProbe : DbCommandInterceptor {
        public List<(DbConnection? Connection, DbTransaction? Transaction)> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Commands.Add((command.Connection, command.Transaction));
            return ValueTask.FromResult(result);
        }
    }
}
