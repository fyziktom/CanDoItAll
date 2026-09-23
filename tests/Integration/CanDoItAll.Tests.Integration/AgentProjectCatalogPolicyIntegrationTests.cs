using System.Data.Common;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class AgentProjectCatalogPolicyIntegrationTests {
    [Fact]
    public async Task Legacy_bootstrap_binds_only_upgrade_eligible_rows_and_never_rebinds_missing_new_or_recreated_ids() {
        await using var application = await TestApplication.CreateAsync();
        await using var environment = CanDoItAllTestEnvironment.Create("catalog-legacy-binding");
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projects = services.GetRequiredService<ProjectsService>();
        var eligibleId = await CreateProjectAsync(projects, "Upgrade eligible");
        var newId = await CreateProjectAsync(projects, "Created after upgrade");
        var missingId = Guid.NewGuid();
        await using (var context = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync()) {
            var eligible = await context.Set<Project>().SingleAsync(project => project.Id == eligibleId);
            context.Entry(eligible).Property(project => project.LegacyAgentAccessBindingEligible).CurrentValue = true;
            await context.SaveChangesAsync();
        }
        var plain = new FileSandboxWorkspaceStore(environment.RootPath);
        var initial = await plain.LoadCatalogAsync();
        var agent = CreateAgent(initial, [eligibleId, newId, missingId]);
        await plain.UpdateCatalogAsync(catalog => catalog with { Agents = catalog.Agents.Append(agent).ToArray() });
        var guarded = CreateGuardedStore(services, environment.RootPath);
        var bound = ReadAccess(await guarded.LoadCatalogAsync(), agent.Id);
        var oldLifetime = Assert.Single(bound.AllowedProjectLifetimes);
        Assert.Equal(eligibleId, oldLifetime.ProjectId);
        Assert.Equal(3, bound.AllowedProjectIds.Count);
        Assert.True((await projects.CreateAsync(missingId, new() { Name = "Previously missing" })).IsSuccess);
        await projects.DeleteAsync(eligibleId);
        Assert.True((await projects.CreateAsync(eligibleId, new() { Name = "New lifetime with old id" })).IsSuccess);
        var after = ReadAccess(await CreateGuardedStore(services, environment.RootPath).LoadCatalogAsync(), agent.Id);
        Assert.Equal(oldLifetime, Assert.Single(after.AllowedProjectLifetimes));
        Assert.Equal(3, after.AllowedProjectIds.Count);
        Assert.NotEqual(oldLifetime.LifetimeId, (await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(eligibleId))!.LifetimeId);
        var intentional = await CreateProjectAsync(projects, "Explicit new selection");
        await guarded.UpdateCatalogAsync(catalog => catalog with { Agents = catalog.Agents.Select(current => current.Id != agent.Id
            ? current : current with { ConfigurationJson = AgentProjectStructureAccessMetadata.Write(current.ConfigurationJson,
                new() { CanRead = true, AllowedProjectIds = [eligibleId, newId, missingId, intentional] }) }).ToArray() });
        var final = ReadAccess(await guarded.LoadCatalogAsync(), agent.Id);
        Assert.Equal(2, final.AllowedProjectLifetimes.Count);
        Assert.Contains(final.AllowedProjectLifetimes, lifetime => lifetime.ProjectId == intentional);
        Assert.Contains(oldLifetime, final.AllowedProjectLifetimes);
    }

    [Fact]
    public async Task Product_catalog_accepts_only_the_requesting_agent_reservation_and_rejects_stale_new_binding() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var workspace = services.GetRequiredService<IAgentFrameworkWorkspaceService>();
        var firstId = await CreateWorkspaceAgentAsync(workspace, "Reservation owner");
        var otherId = await CreateWorkspaceAgentAsync(workspace, "Other agent");
        var admissions = services.GetRequiredService<ProjectWriteAdmissionService>();
        var reservation = await admissions.ReserveCreationAsync(Guid.NewGuid(), firstId, Guid.NewGuid());
        var lifetime = new AgentProjectStructureLifetime(reservation.DatabaseProfileId, reservation.ProjectId, reservation.LifetimeId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => workspace.GrantAgentProjectStructureLifetimeAsync(otherId, lifetime));
        await workspace.GrantAgentProjectStructureLifetimeAsync(firstId, lifetime);
        await admissions.CancelCreationAsync(reservation);
        Assert.Equal(lifetime, Assert.Single(AgentProjectStructureAccessMetadata.Read(
            Assert.Single(await workspace.ListAgentsAsync(), agent => agent.Id == firstId).ConfigurationJson).AllowedProjectLifetimes));
        await Assert.ThrowsAsync<InvalidOperationException>(() => workspace.GrantAgentProjectStructureLifetimeAsync(otherId, lifetime));
        Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(reservation.ProjectId, new() { Name = "Different lifetime" })).IsSuccess);
        await Assert.ThrowsAsync<InvalidOperationException>(() => workspace.GrantAgentProjectStructureLifetimeAsync(otherId, lifetime));
        await workspace.RevokeAgentProjectStructureLifetimeAsync(firstId, lifetime);
        Assert.Empty(AgentProjectStructureAccessMetadata.Read(
            Assert.Single(await workspace.ListAgentsAsync(), agent => agent.Id == firstId).ConfigurationJson).AllowedProjectIds);
    }

    [Fact]
    public async Task Delayed_catalog_snapshot_cannot_resurrect_a_retired_grant_after_removal_and_same_id_recreation() {
        await using var application = await TestApplication.CreateAsync();
        await using var environment = CanDoItAllTestEnvironment.Create("catalog-stale-snapshot");
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projects = services.GetRequiredService<ProjectsService>();
        var projectId = await CreateProjectAsync(projects, "Original catalog binding");
        var store = CreateGuardedStore(services, environment.RootPath);
        var initial = await store.LoadCatalogAsync();
        var agent = CreateAgent(initial, [projectId]);
        var saved = await store.UpdateCatalogAsync(catalog => catalog with { Agents = catalog.Agents.Append(agent).ToArray() });
        var old = ReadAccess(saved, agent.Id);
        await store.UpdateCatalogAsync(catalog => catalog with { Agents = catalog.Agents.Select(current => current.Id != agent.Id
            ? current : current with { ConfigurationJson = AgentProjectStructureAccessMetadata.RevokeProject(current.ConfigurationJson, projectId).ConfigurationJson }).ToArray() });
        await projects.DeleteAsync(projectId);
        Assert.True((await projects.CreateAsync(projectId, new() { Name = "Replacement catalog binding" })).IsSuccess);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveCatalogAsync(saved));
        var currentCatalog = await store.LoadCatalogAsync();
        Assert.Empty(ReadAccess(currentCatalog, agent.Id).AllowedProjectIds);
        await store.UpdateCatalogAsync(catalog => catalog with { Agents = catalog.Agents.Select(current => current.Id != agent.Id
            ? current : current with { ConfigurationJson = AgentProjectStructureAccessMetadata.Write(current.ConfigurationJson,
                new() { AllowedProjectIds = [projectId] }) }).ToArray() });
        Assert.NotEqual(Assert.Single(old.AllowedProjectLifetimes).LifetimeId,
            Assert.Single(ReadAccess(await store.LoadCatalogAsync(), agent.Id).AllowedProjectLifetimes).LifetimeId);
    }

    [Fact]
    public async Task Many_catalog_agents_use_two_bulk_owner_reads_without_a_carried_database_transaction_and_profile_mismatch_is_denied() {
        await using var application = await TestApplication.CreateAsync();
        await using var environment = CanDoItAllTestEnvironment.Create("catalog-bulk-facts");
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projectId = await CreateProjectAsync(services.GetRequiredService<ProjectsService>(), "Shared catalog project");
        var plain = new FileSandboxWorkspaceStore(environment.RootPath);
        var initial = await plain.LoadCatalogAsync();
        var agents = Enumerable.Range(0, 20).Select(_ => CreateAgent(initial, [])).ToArray();
        await plain.UpdateCatalogAsync(catalog => catalog with { Agents = catalog.Agents.Concat(agents).ToArray() });
        var probe = new QueryProbe();
        var options = new DbContextOptionsBuilder<ProjectsDbContext>(services.GetRequiredService<DbContextOptions<ProjectsDbContext>>())
            .AddInterceptors(probe).Options;
        var canonical = services.GetRequiredService<ICanonicalRuntimeDatabase>();
        var admissions = new ProjectWriteAdmissionService(new ProjectFactory(options), options,
            services.GetRequiredService<CoordinatedDatabaseTransaction>(), canonical);
        var guarded = new FileSandboxWorkspaceStore(environment.RootPath, catalogMutationPolicy:
            new AgentProjectAccessCatalogPolicy(admissions, canonical.Profile.Profile.Id));
        var agentIds = agents.Select(agent => agent.Id).ToHashSet();
        var saved = await guarded.UpdateCatalogAsync(catalog => catalog with { Agents = catalog.Agents.Select(agent => !agentIds.Contains(agent.Id)
            ? agent : agent with { ConfigurationJson = AgentProjectStructureAccessMetadata.Write(agent.ConfigurationJson,
                new() { AllowedProjectIds = [projectId] }) }).ToArray() });
        Assert.Equal(2, probe.Transactions.Count);
        Assert.All(probe.Transactions, transaction => Assert.Null(transaction));
        Assert.All(saved.Agents.Where(agent => agentIds.Contains(agent.Id)), agent =>
            Assert.Equal(projectId, Assert.Single(AgentProjectStructureAccessMetadata.Read(agent.ConfigurationJson).AllowedProjectLifetimes).ProjectId));
        var mismatched = new AgentProjectAccessCatalogPolicy(admissions, Guid.NewGuid());
        await Assert.ThrowsAsync<InvalidOperationException>(() => mismatched.ApplyAsync(saved, saved));
        Assert.Equal(2, probe.Transactions.Count);
    }

    private static FileSandboxWorkspaceStore CreateGuardedStore(IServiceProvider services, string root) => new(root,
        catalogMutationPolicy: new AgentProjectAccessCatalogPolicy(services.GetRequiredService<ProjectWriteAdmissionService>(),
            services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile.Id));

    private static AgentDefinition CreateAgent(SandboxWorkspaceCatalog catalog, IReadOnlyList<Guid> projectIds) => catalog.Agents[0] with {
        Id = Guid.NewGuid(), Name = $"Catalog binding {Guid.NewGuid():N}", IsTemplate = false, TemplateKey = "",
        ConfigurationJson = AgentProjectStructureAccessMetadata.Write("{\"customSavedField\":\"retain\"}", new() { AllowedProjectIds = projectIds.ToList() })
    };

    private static AgentProjectStructureAccessSettings ReadAccess(SandboxWorkspaceCatalog catalog, Guid agentId) =>
        AgentProjectStructureAccessMetadata.Read(Assert.Single(catalog.Agents, agent => agent.Id == agentId).ConfigurationJson);

    private static async Task<Guid> CreateProjectAsync(ProjectsService projects, string name) {
        var result = await projects.SaveAsync(new() { Name = name });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task<Guid> CreateWorkspaceAgentAsync(IAgentFrameworkWorkspaceService workspace, string name) {
        var editor = await workspace.GetAgentEditorAsync();
        editor.Name = name;
        editor.RoleTitle = "Catalog binding test";
        editor.Summary = "Validate project creation grant ownership.";
        editor.Instructions = "Use only admitted project lifetimes.";
        editor.Status = AgentLifecycleStatus.Active;
        return await workspace.SaveAgentAsync(editor);
    }

    private sealed class ProjectFactory(DbContextOptions<ProjectsDbContext> options) : IDbContextFactory<ProjectsDbContext> {
        public ProjectsDbContext CreateDbContext() => new(options);
    }

    private sealed class QueryProbe : DbCommandInterceptor {
        public List<DbTransaction?> Transactions { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            Transactions.Add(command.Transaction);
            return ValueTask.FromResult(result);
        }
    }
}
