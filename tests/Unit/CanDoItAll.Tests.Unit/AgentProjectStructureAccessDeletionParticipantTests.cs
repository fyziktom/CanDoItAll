using CanDoItAll.Tests.Support;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
        var participant = new AgentFrameworkProjectTransferTargetStateParticipant(new ProjectTransferTargetInspectionRunner());

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
        var storeRoot = new InMemoryDatabaseRoot();
        options.UseInMemoryDatabase(databaseName, storeRoot);
        var projectOptions = new DbContextOptionsBuilder<ProjectsDbContext>();
        AppDbContextOptionsConfigurator.Configure(projectOptions, profile);
        projectOptions.UseInMemoryDatabase(databaseName, storeRoot);
        await using var dbContext = new AgentProjectAccessDbContext(options.Options);
        var coordinator = CoordinatedDatabaseTransaction.ForProfile(profile);
        var projectId = Guid.NewGuid();
        var lifetimeId = Guid.NewGuid();
        var original = new ProjectDeletionPreparationContext(new(profile.Profile.Id, new(Guid.NewGuid()), new(Guid.NewGuid())),
            new(profile.Profile.Id, projectId, lifetimeId));
        await using (var projectSeed = new ProjectsDbContext(projectOptions.Options)) {
            var project = new Project { Id = projectId, Name = "Exact original preparation" };
            project.BindReservedLifetime(lifetimeId);
            projectSeed.Add(project);
            await projectSeed.SaveChangesAsync();
        }
        var participant = new AgentProjectStructureAccessDeletionParticipant(
            workspaceService: null!, dbContextFactory: null!, timeProvider: TimeProvider.System,
            logger: Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentProjectStructureAccessDeletionParticipant>.Instance,
            contextOptions: options.Options, coordinatedTransaction: coordinator, claimOptions: AgentProjectAccessClaimOptions.Default,
            writeAdmissionService: new ProjectWriteAdmissionService(null!, projectOptions.Options, coordinator, new CanonicalDatabase(profile), DatabaseTransferTestSupport.UnconfiguredActivation()),
            deletionCompletions: new PreparationOnlyQuery());
        using (coordinator.Enter(dbContext)) {
            var preparation = await participant.PrepareAsync(original);
            Assert.NotNull(preparation);
            Assert.Equal(preparation, await participant.PrepareAsync(original));
            Assert.Empty(dbContext.ChangeTracker.Entries());
            await using var readback = new AgentProjectAccessDbContext(options.Options);
            var saved = Assert.Single(await readback.Set<AgentProjectStructureAccessRevocationRecord>().ToArrayAsync());
            Assert.Equal(projectId, saved.ProjectId);
            Assert.Equal(preparation.RecoveryId, saved.Id);
            Assert.Equal(original.Completion.Namespace.Value, saved.CompletionNamespaceId);
            Assert.Equal(original.Completion.Id.Value, saved.CompletionId);
            Assert.Equal(original.Project.LifetimeId, saved.ProjectLifetimeId);
            var other = new ProjectDeletionPreparationContext(original.Completion,
                new(original.Project.DatabaseProfileId, original.Project.ProjectId, Guid.NewGuid()));
            await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() => participant.PrepareAsync(other));
            await Assert.ThrowsAsync<NotSupportedException>(() => participant.PrepareAsync(projectId));
            Assert.Equal(AgentProjectStructureAccessRevocationStatus.Pending, saved.Status);
            Assert.Equal(0, saved.AttemptCount);
        }
        await Assert.ThrowsAsync<InvalidOperationException>(() => participant.PrepareAsync(original));
    }
    private sealed class PreparationOnlyQuery : IProjectDeletionCompletionQuery {
        public Task<ProjectDeletionCompletionObservation> ObserveAsync(ProjectDeletionPreparationContext original,
            ProjectDeletionObligation obligation, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Preparation must not query completion release.");
    }

    private sealed class CanonicalDatabase(ResolvedDatabaseProfile profile) : ICanonicalRuntimeDatabase {
        public ResolvedDatabaseProfile Profile { get; } = profile;
        public long Generation => 1;
    }

}
