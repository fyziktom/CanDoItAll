using System.Data.Common;
using System.Reflection;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class AgentProjectLifetimePersistenceTests {
    [Fact]
    public async Task Delayed_old_cleanup_keeps_recreated_project_grant_and_second_deletion_owns_a_new_recovery() {
        var gate = new CleanupGate();
        await using var application = await TestApplication.CreateAsync(new TestHarnessOptions {
            ConfigureServices = services => {
                var registration = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IProjectDeletionParticipant) &&
                    descriptor.ImplementationType == typeof(AgentProjectStructureAccessDeletionParticipant));
                services.Remove(registration);
                services.AddScoped<IProjectDeletionParticipant>(provider => {
                    var proxy = DispatchProxy.Create<IAgentFrameworkWorkspaceService, GatedWorkspaceProxy>();
                    var controller = (GatedWorkspaceProxy)(object)proxy;
                    controller.Inner = provider.GetRequiredService<IAgentFrameworkWorkspaceService>();
                    controller.Gate = gate;
                    return ActivatorUtilities.CreateInstance<AgentProjectStructureAccessDeletionParticipant>(provider, proxy);
                });
            }
        });
        await using var oldScope = application.Services.CreateAsyncScope();
        var oldProjects = oldScope.ServiceProvider.GetRequiredService<ProjectsService>();
        var projectId = await CreateProjectAsync(oldProjects, "First lifetime");
        var workspace = oldScope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var agentId = await CreateAgentAsync(workspace, projectId);
        var oldLifetime = await CaptureAsync(oldScope.ServiceProvider, projectId);
        await workspace.GrantAgentProjectStructureLifetimeAsync(agentId, oldLifetime);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var deletion = oldProjects.DeleteAsync(projectId, deadline.Token);
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await using var newScope = application.Services.CreateAsyncScope();
            var newProjects = newScope.ServiceProvider.GetRequiredService<ProjectsService>();
            var recreated = await newProjects.CreateAsync(projectId, new ProjectEditorModel { Name = "Second lifetime" }, deadline.Token);
            Assert.True(recreated.IsSuccess);
            var newLifetime = await CaptureAsync(newScope.ServiceProvider, projectId);
            Assert.NotEqual(oldLifetime.LifetimeId, newLifetime.LifetimeId);
            var newWorkspace = newScope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
            await newWorkspace.GrantAgentProjectStructureLifetimeAsync(agentId, newLifetime);
            gate.Released.TrySetResult();
            await deletion.WaitAsync(TimeSpan.FromSeconds(30));
            var access = await ReadAccessAsync(newWorkspace, agentId);
            Assert.Equal(newLifetime, Assert.Single(access.AllowedProjectLifetimes));
            Assert.Equal(projectId, Assert.Single(access.AllowedProjectIds));
            await newProjects.DeleteAsync(projectId, deadline.Token);
            access = await ReadAccessAsync(newWorkspace, agentId);
            Assert.Empty(access.AllowedProjectIds);
            Assert.Empty(access.AllowedProjectLifetimes);
            await using var context = await newScope.ServiceProvider.GetRequiredService<IDbContextFactory<AgentProjectAccessDbContext>>().CreateDbContextAsync();
            var recoveries = await context.Set<AgentProjectStructureAccessRevocationRecord>().AsNoTracking()
                .Where(record => record.ProjectId == projectId).ToArrayAsync();
            Assert.Equal(2, recoveries.Length);
            Assert.Contains(recoveries, record => record.ProjectLifetimeId == oldLifetime.LifetimeId);
            Assert.Contains(recoveries, record => record.ProjectLifetimeId == newLifetime.LifetimeId);
            Assert.Equal(2, recoveries.Select(record => record.Id).Distinct().Count());
            Assert.All(recoveries, record => {
                Assert.Equal(newLifetime.DatabaseProfileId, record.DatabaseProfileId);
                Assert.Equal(AgentProjectStructureAccessRevocationStatus.Completed, record.Status);
                Assert.Equal(1, record.AttemptCount);
            });
        } finally {
            gate.Released.TrySetResult();
            await deletion.WaitAsync(TimeSpan.FromSeconds(30));
        }
    }

    [Fact]
    public async Task Legacy_recovery_is_retained_across_restart_without_consuming_new_lifetime_and_profiles_remain_bound() {
        await using var environment = CanDoItAllTestEnvironment.Create("agent-project-lifetimes-restart");
        var profile = environment.CreatePostgreSqlProfile("original");
        var otherProfile = environment.CreatePostgreSqlProfile("other");
        var harness = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile };
        var now = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        Guid projectId;
        Guid agentId;
        AgentProjectStructureLifetime lifetime;
        AgentProjectStructureAccessRevocationRecord legacy;
        await using (var original = await TestApplication.CreateAsync(harness)) {
            await using var scope = original.Services.CreateAsyncScope();
            projectId = await CreateProjectAsync(scope.ServiceProvider.GetRequiredService<ProjectsService>(), "Legacy recovery identity");
            lifetime = await CaptureAsync(scope.ServiceProvider, projectId);
            var workspace = scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
            agentId = await CreateAgentAsync(workspace, projectId);
            await workspace.GrantAgentProjectStructureLifetimeAsync(agentId, lifetime);
            legacy = new() {
                Id = Guid.NewGuid(), ProjectId = projectId, Status = AgentProjectStructureAccessRevocationStatus.Completed,
                AttemptCount = 2, CreatedAtUtc = now.AddHours(-2), UpdatedAtUtc = now, LastAttemptAtUtc = now.AddMinutes(-1), CompletedAtUtc = now
            };
            await using var canonical = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
            canonical.Add(legacy);
            await canonical.SaveChangesAsync();
            await using var owner = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<AgentProjectAccessDbContext>>().CreateDbContextAsync();
            var entity = Assert.Single(owner.GetService<IDesignTimeModel>().Model.GetEntityTypes());
            Assert.Equal(canonical.GetService<IDesignTimeModel>().Model.FindEntityType(entity.ClrType)!
                .ToDebugString(MetadataDebugStringOptions.LongDefault), entity.ToDebugString(MetadataDebugStringOptions.LongDefault));
        }
        await using var restarted = await TestApplication.CreateAsync(harness);
        await using var restartedScope = restarted.Services.CreateAsyncScope();
        var services = restartedScope.ServiceProvider;
        await using var context = await services.GetRequiredService<IDbContextFactory<AgentProjectAccessDbContext>>().CreateDbContextAsync();
        var savedLegacy = await context.Set<AgentProjectStructureAccessRevocationRecord>().AsNoTracking().SingleAsync(record => record.Id == legacy.Id);
        Assert.Equal(JsonSerializer.Serialize(legacy), JsonSerializer.Serialize(savedLegacy));
        var restartedWorkspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        Assert.Equal(0, await restartedWorkspace.RevokeProjectStructureLifetimeAccessFromAllAgentsAsync(
            AgentProjectStructureRevocationTarget.UnboundLegacy(lifetime.DatabaseProfileId, projectId)));
        Assert.Equal(lifetime, Assert.Single((await ReadAccessAsync(restartedWorkspace, agentId)).AllowedProjectLifetimes));
        await services.GetRequiredService<ProjectsService>().DeleteAsync(projectId);
        var records = await context.Set<AgentProjectStructureAccessRevocationRecord>().AsNoTracking().Where(record => record.ProjectId == projectId).ToArrayAsync();
        Assert.Equal(2, records.Length);
        Assert.Equal(JsonSerializer.Serialize(legacy), JsonSerializer.Serialize(Assert.Single(records, record => record.Id == legacy.Id)));
        var current = Assert.Single(records, record => record.ProjectLifetimeId == lifetime.LifetimeId);
        Assert.Equal(AgentProjectStructureAccessRevocationStatus.Completed, current.Status);
        Assert.NotEqual(legacy.Id, current.Id);
        await using var other = await TestApplication.CreateAsync(new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = otherProfile });
        await using var otherScope = other.Services.CreateAsyncScope();
        var foreignWorkspace = otherScope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => foreignWorkspace.GrantAgentProjectStructureLifetimeAsync(agentId, lifetime));
        await Assert.ThrowsAsync<InvalidOperationException>(() => foreignWorkspace.RevokeProjectStructureLifetimeAccessFromAllAgentsAsync(
            AgentProjectStructureRevocationTarget.ForLifetime(lifetime)));
        await using var foreignContext = await otherScope.ServiceProvider.GetRequiredService<IDbContextFactory<AgentProjectAccessDbContext>>().CreateDbContextAsync();
        Assert.False(await foreignContext.Set<AgentProjectStructureAccessRevocationRecord>().AnyAsync(record => record.ProjectId == projectId));
    }

    [Fact]
    public async Task Lifetime_capture_and_revocation_insert_share_the_owner_transaction_and_rollback_together() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projectId = await CreateProjectAsync(services.GetRequiredService<ProjectsService>(), "Atomic lifetime capture");
        var probe = new ConnectionProbe();
        var projectOptions = new DbContextOptionsBuilder<ProjectsDbContext>(services.GetRequiredService<DbContextOptions<ProjectsDbContext>>())
            .AddInterceptors(probe).Options;
        var accessOptions = new DbContextOptionsBuilder<AgentProjectAccessDbContext>(services.GetRequiredService<DbContextOptions<AgentProjectAccessDbContext>>())
            .AddInterceptors(probe).Options;
        var coordinator = services.GetRequiredService<CoordinatedDatabaseTransaction>();
        var admission = new ProjectWriteAdmissionService(services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>(),
            projectOptions, coordinator, services.GetRequiredService<ICanonicalRuntimeDatabase>());
        var participant = ActivatorUtilities.CreateInstance<AgentProjectStructureAccessDeletionParticipant>(services, admission, accessOptions);
        await using var owner = new ProjectsDbContext(projectOptions);
        Guid recoveryId;
        await using (var mutation = await SerializableMutationScope.BeginAsync(owner, [ProjectMutationScopeKeys.ForProject(projectId)], CancellationToken.None)) {
            var connection = owner.Database.GetDbConnection();
            var transaction = owner.Database.CurrentTransaction!.GetDbTransaction();
            using (coordinator.Enter(owner)) {
                var prepared = Assert.IsType<ProjectDeletionParticipantPreparation>(await participant.PrepareAsync(projectId));
                recoveryId = prepared.RecoveryId;
                Assert.Contains(probe.Commands, command => command.Table == "Projects_Projects");
                Assert.Contains(probe.Commands, command => command.Table == "AgentFramework_ProjectAccessRevocations");
                Assert.All(probe.Commands, command => {
                    Assert.Same(connection, command.Connection);
                    Assert.Same(transaction, command.Transaction);
                });
                await using var independent = await services.GetRequiredService<IDbContextFactory<AgentProjectAccessDbContext>>().CreateDbContextAsync();
                Assert.False(await independent.Set<AgentProjectStructureAccessRevocationRecord>().AnyAsync(record => record.Id == recoveryId));
            }
        }
        await using var readback = await services.GetRequiredService<IDbContextFactory<AgentProjectAccessDbContext>>().CreateDbContextAsync();
        Assert.False(await readback.Set<AgentProjectStructureAccessRevocationRecord>().AnyAsync(record => record.Id == recoveryId));
    }

    private static async Task<Guid> CreateProjectAsync(ProjectsService projects, string name) {
        var result = await projects.SaveAsync(new ProjectEditorModel { Name = name });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task<AgentProjectStructureLifetime> CaptureAsync(IServiceProvider services, Guid projectId) {
        var admission = Assert.IsType<ProjectWriteAdmission>(await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(projectId));
        return new(admission.DatabaseProfileId, admission.ProjectId, admission.LifetimeId);
    }

    private static async Task<Guid> CreateAgentAsync(IAgentFrameworkWorkspaceService workspace, Guid projectId) {
        var editor = await workspace.GetAgentEditorAsync();
        editor.Name = "Lifetime access agent";
        editor.RoleTitle = "Project access specialist";
        editor.Summary = "Validate exact project lifetime access.";
        editor.Instructions = "Stay within explicitly granted project scope.";
        editor.Status = AgentLifecycleStatus.Active;
        editor.ProjectStructureAccess = new() { CanRead = true, AllowedProjectIds = [projectId] };
        return await workspace.SaveAgentAsync(editor);
    }

    private static async Task<AgentProjectStructureAccessSettings> ReadAccessAsync(IAgentFrameworkWorkspaceService workspace, Guid agentId) {
        var agent = Assert.Single(await workspace.ListAgentsAsync(), candidate => candidate.Id == agentId);
        return AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson);
    }

    public sealed class CleanupGate {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Released { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls;
    }

    public class GatedWorkspaceProxy : DispatchProxy {
        public IAgentFrameworkWorkspaceService Inner { get; set; } = null!;
        public CleanupGate Gate { get; set; } = null!;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            if (targetMethod?.Name != nameof(IAgentFrameworkWorkspaceService.RevokeProjectStructureLifetimeAccessFromAllAgentsAsync)) {
                throw new NotSupportedException(targetMethod?.Name);
            }
            return RevokeAsync((AgentProjectStructureRevocationTarget)args![0]!, (CancellationToken)args[1]!);
        }

        private async Task<int> RevokeAsync(AgentProjectStructureRevocationTarget target, CancellationToken cancellationToken) {
            if (Interlocked.Increment(ref Gate.Calls) == 1) {
                Gate.Entered.TrySetResult();
                await Gate.Released.Task.WaitAsync(cancellationToken);
            }
            return await Inner.RevokeProjectStructureLifetimeAccessFromAllAgentsAsync(target, cancellationToken);
        }
    }

    private sealed class ConnectionProbe : DbCommandInterceptor {
        public List<(string Table, DbConnection? Connection, DbTransaction? Transaction)> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Record(command);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            Record(command);
            return ValueTask.FromResult(result);
        }

        private void Record(DbCommand command) {
            foreach (var table in new[] { "Projects_Projects", "AgentFramework_ProjectAccessRevocations" }) {
                if (command.CommandText.Contains(table, StringComparison.Ordinal)) {
                    Commands.Add((table, command.Connection, command.Transaction));
                }
            }
        }
    }
}
