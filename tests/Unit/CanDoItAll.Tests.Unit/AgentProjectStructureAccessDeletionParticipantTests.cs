using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit.Projects;

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
    public async Task Prepare_saves_one_pending_record_in_the_explicit_test_store_and_reuses_its_identity() {
        var databaseName = $"agent-project-access-prepare-{Guid.NewGuid():N}";
        var profile = new ResolvedDatabaseProfile(new() { ProviderKind = DatabaseProviderKind.InMemory },
            DatabaseProfileResolutionSource.ExplicitOverride, databaseName);
        var options = new DbContextOptionsBuilder<AgentProjectAccessDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, profile);
        await using var dbContext = new AgentProjectAccessDbContext(options.Options);
        var coordinator = CoordinatedDatabaseTransaction.ForProfile(profile);
        var projectId = Guid.NewGuid();
        var participant = new AgentProjectStructureAccessDeletionParticipant(
            workspaceService: null!, dbContextFactory: null!, timeProvider: TimeProvider.System,
            logger: Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentProjectStructureAccessDeletionParticipant>.Instance,
            contextOptions: options.Options, coordinatedTransaction: coordinator, claimOptions: AgentProjectAccessClaimOptions.Default);
        using (coordinator.Enter(dbContext)) {
            var preparation = await participant.PrepareAsync(projectId);
            Assert.NotNull(preparation);
            Assert.Equal(preparation, await participant.PrepareAsync(projectId));
            Assert.Empty(dbContext.ChangeTracker.Entries());
            await using var readback = new AgentProjectAccessDbContext(options.Options);
            var saved = Assert.Single(await readback.Set<AgentProjectStructureAccessRevocationRecord>().ToArrayAsync());
            Assert.Equal(projectId, saved.ProjectId);
            Assert.Equal(preparation.RecoveryId, saved.Id);
            Assert.Equal(AgentProjectStructureAccessRevocationStatus.Pending, saved.Status);
            Assert.Equal(0, saved.AttemptCount);
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => participant.PrepareAsync(projectId));
    }
}
