using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed class AgentProjectStructureAccessDeletionParticipantTests
{
    [Fact]
    public void Agent_framework_module_registers_the_project_access_deletion_participant_as_scoped()
    {
        var services = new ServiceCollection();

        services.AddAgentFrameworkModule(new ConfigurationBuilder().Build());

        var descriptor = Assert.Single(
            services,
            candidate =>
                candidate.ServiceType == typeof(IProjectDeletionParticipant) &&
                candidate.ImplementationType ==
                    typeof(AgentProjectStructureAccessDeletionParticipant));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
        Assert.Equal(
            "agent-project-structure-access",
            AgentProjectStructureAccessDeletionParticipant.ParticipantIdValue);
    }

    [Fact]
    public void Agent_framework_target_state_participant_locks_the_durable_revocation_table()
    {
        var participant = new AgentFrameworkProjectTransferTargetStateParticipant();

        Assert.Contains(
            typeof(AgentProjectStructureAccessRevocationRecord),
            participant.EntityTypesToLock);
    }

    [Fact]
    public async Task Prepare_stages_one_pending_record_in_the_callers_context_without_saving()
    {
        using var modelRegistryScope = AppDbContextModelRegistry.UseIsolatedAssembliesForTesting();
        AppDbContextModelRegistry.ConfigureAssemblies([
            typeof(AgentFrameworkModuleAssemblyMarker).Assembly
        ]);
        var databaseName = $"agent-project-access-prepare-{Guid.NewGuid():N}";
        var options = AppDbContextTestOptionsBuilder.Create()
            .UseInMemoryDatabase(databaseName)
            .Options;
        await using var dbContext = new AppDbContext(options);
        var projectId = Guid.NewGuid();
        var participant = new AgentProjectStructureAccessDeletionParticipant(
            workspaceService: null!,
            dbContextFactory: null!,
            timeProvider: TimeProvider.System,
            logger: Microsoft.Extensions.Logging.Abstractions.NullLogger<
                AgentProjectStructureAccessDeletionParticipant>.Instance);

        var preparation = await participant.PrepareAsync(dbContext, projectId);

        Assert.NotNull(preparation);
        var staged = Assert.Single(
            dbContext.ChangeTracker.Entries<AgentProjectStructureAccessRevocationRecord>());
        Assert.Equal(EntityState.Added, staged.State);
        Assert.Equal(projectId, staged.Entity.ProjectId);
        Assert.Equal(preparation.RecoveryId, staged.Entity.Id);
        Assert.Equal(AgentProjectStructureAccessRevocationStatus.Pending, staged.Entity.Status);
        Assert.Equal(0, staged.Entity.AttemptCount);
        await using var independentContext = new AppDbContext(options);
        Assert.False(await independentContext
            .Set<AgentProjectStructureAccessRevocationRecord>()
            .AnyAsync());
    }
}
