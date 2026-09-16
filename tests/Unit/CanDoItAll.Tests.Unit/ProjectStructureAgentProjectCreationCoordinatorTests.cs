using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectStructureAgentProjectCreationCoordinatorTests
{
    [Fact]
    public async Task Grant_failure_prevents_project_creation()
    {
        var (coordinator, proxy, agent, reservedProjectId) = CreateCoordinator();
        proxy.ThrowOnGrant = true;
        var createCalled = false;

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.CreateAsync(
            agent,
            (projectId, _) =>
            {
                createCalled = true;
                return Task.FromResult(projectId.ProjectId);
            },
            projectId => projectId,
            CancellationToken.None));

        Assert.False(createCalled);
        Assert.Equal(["grant"], proxy.Events);
    }

    [Fact]
    public async Task Grant_precedes_creation_and_creation_uses_the_reserved_id()
    {
        var (coordinator, proxy, agent, reservedProjectId) = CreateCoordinator();
        Guid? receivedProjectId = null;
        ProjectCreationReservation? sessionReservation = null;

        var result = await coordinator.CreateAsync(
            agent,
            (projectId, _) =>
            {
                proxy.Events.Add("create");
                receivedProjectId = projectId.ProjectId;
                return Task.FromResult(projectId.ProjectId);
            },
            projectId => projectId,
            CancellationToken.None,
            retainLifetimeAccessForSession: reservation => sessionReservation = reservation);

        Assert.Equal(reservedProjectId, result);
        Assert.Equal(reservedProjectId, receivedProjectId);
        Assert.NotNull(sessionReservation);
        Assert.Equal(reservedProjectId, sessionReservation.ProjectId);
        Assert.Equal(Assert.Single(await proxy.ReadReservationsAsync()).LifetimeId, sessionReservation.LifetimeId);
        Assert.Equal(["grant", "create"], proxy.Events);
    }

    [Fact]
    public async Task Deterministic_creation_rejection_revokes_the_reserved_grant()
    {
        var (coordinator, proxy, agent, reservedProjectId) = CreateCoordinator();

        await Assert.ThrowsAsync<ProjectStructureProjectCreationRejectedException>(() => coordinator.CreateAsync<Guid>(
            agent,
            (_, _) => throw new ProjectStructureProjectCreationRejectedException("Expected rejection."),
            projectId => projectId,
            CancellationToken.None));

        Assert.Equal(["grant", "revoke"], proxy.Events);
        Assert.Equal(ProjectCreationReservationState.Cancelled, Assert.Single(await proxy.ReadReservationsAsync()).State);
        Assert.DoesNotContain(
            reservedProjectId,
            AgentProjectStructureAccessMetadata.Read(proxy.Agent.ConfigurationJson).AllowedProjectIds);
    }

    [Fact]
    public async Task Agent_mapped_creation_rejection_still_revokes_the_reserved_grant()
    {
        var (coordinator, proxy, agent, reservedProjectId) = CreateCoordinator();
        var applicationFailure = new ProjectStructureProjectCreationRejectedException("Expected rejection.");
        Assert.True(ProjectStructureAgentTransferFailureMapper.TryMap(applicationFailure, out var agentFailure));

        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(() => coordinator.CreateAsync<Guid>(
            agent,
            (_, _) => throw agentFailure,
            projectId => projectId,
            CancellationToken.None));

        Assert.Same(agentFailure, exception);
        Assert.Equal(["grant", "revoke"], proxy.Events);
        Assert.DoesNotContain(
            reservedProjectId,
            AgentProjectStructureAccessMetadata.Read(proxy.Agent.ConfigurationJson).AllowedProjectIds);
    }

    [Fact]
    public async Task Ambiguous_creation_exception_retains_the_reserved_grant()
    {
        var (coordinator, proxy, agent, reservedProjectId) = CreateCoordinator();

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.CreateAsync<Guid>(
            agent,
            (_, _) => throw new InvalidOperationException("Commit outcome is unknown."),
            projectId => projectId,
            CancellationToken.None));

        Assert.Equal(["grant"], proxy.Events);
        var retained = Assert.Single(await proxy.ReadReservationsAsync());
        Assert.Equal(ProjectCreationReservationState.Reserved, retained.State);
        Assert.Equal(retained.LifetimeId, Assert.Single(AgentProjectStructureAccessMetadata.Read(proxy.Agent.ConfigurationJson).AllowedProjectLifetimes).LifetimeId);
        Assert.Contains(
            reservedProjectId,
            AgentProjectStructureAccessMetadata.Read(proxy.Agent.ConfigurationJson).AllowedProjectIds);
    }

    [Fact]
    public async Task Transfer_failure_after_child_creation_retains_access_to_the_child()
    {
        var (coordinator, proxy, agent, reservedProjectId) = CreateCoordinator();

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.CreateAsync<Guid>(
            agent,
            (_, _) =>
            {
                proxy.Events.Add("child-created");
                throw new InvalidOperationException("Node transfer failed after child commit.");
            },
            projectId => projectId,
            CancellationToken.None));

        Assert.Equal(["grant", "child-created"], proxy.Events);
        Assert.Contains(
            reservedProjectId,
            AgentProjectStructureAccessMetadata.Read(proxy.Agent.ConfigurationJson).AllowedProjectIds);
    }

    [Fact]
    public async Task Compensated_transfer_failure_revokes_access_and_rethrows_the_original_failure()
    {
        var (coordinator, proxy, agent, reservedProjectId) = CreateCoordinator();
        var transferFailure = new ProjectStructureTransferRejectedException(
            ProjectStructureTransferRejectionReason.SelectedNodesUnavailable,
            "The selected nodes could not be moved.",
            Guid.NewGuid(),
            reservedProjectId);

        var exception = await Assert.ThrowsAsync<ProjectStructureTransferRejectedException>(() => coordinator.CreateAsync<Guid>(
            agent,
            (projectId, _) => throw new ProjectStructureCompensatedSubprojectTransferException(
                projectId.ProjectId,
                transferFailure),
            projectId => projectId,
            CancellationToken.None));

        Assert.Same(transferFailure, exception);
        Assert.Equal(["grant", "revoke"], proxy.Events);
        Assert.DoesNotContain(
            reservedProjectId,
            AgentProjectStructureAccessMetadata.Read(proxy.Agent.ConfigurationJson).AllowedProjectIds);
    }

    [Fact]
    public async Task Compensated_transfer_with_lease_cleanup_failure_revokes_access_and_surfaces_every_failure()
    {
        var (coordinator, proxy, agent, reservedProjectId) = CreateCoordinator();
        var transferFailure = new ProjectStructureTransferRejectedException(
            ProjectStructureTransferRejectionReason.SelectedNodesUnavailable,
            "The selected nodes could not be moved.",
            Guid.NewGuid(),
            reservedProjectId);
        var compensatedTransfer = new ProjectStructureCompensatedSubprojectTransferException(
            reservedProjectId,
            transferFailure);
        var leaseReleaseFailure = new InvalidOperationException("The target project lease could not be released.");
        var combinedFailure = new AggregateException(
            "The transfer and lease cleanup failed.",
            compensatedTransfer,
            leaseReleaseFailure);

        var exception = await Assert.ThrowsAsync<AggregateException>(() => coordinator.CreateAsync<Guid>(
            agent,
            (_, _) => throw combinedFailure,
            projectId => projectId,
            CancellationToken.None));

        Assert.Same(combinedFailure, exception);
        Assert.Contains(compensatedTransfer, exception.InnerExceptions);
        Assert.Contains(leaseReleaseFailure, exception.InnerExceptions);
        Assert.Equal(["grant", "revoke"], proxy.Events);
        Assert.DoesNotContain(
            reservedProjectId,
            AgentProjectStructureAccessMetadata.Read(proxy.Agent.ConfigurationJson).AllowedProjectIds);
    }

    [Fact]
    public async Task Partial_commit_keeps_persisted_access_grants_session_access_and_rethrows_recovery_evidence()
    {
        var (coordinator, proxy, agent, reservedProjectId) = CreateCoordinator();
        var partialCommit = new ProjectStructureTransferPartialCommitException(
            new ProjectStructureTransferRecovery(
                reservedProjectId,
                Guid.NewGuid(),
                ProjectStructureTransferReconciliationStatus.Failed,
                ProjectStructureTransferCommitState.WorkbenchCommitted,
                "Retry durable reconciliation."),
            "The transfer committed, but durable reconciliation failed.");
        var leaseReleaseFailure = new InvalidOperationException("The target lease release failed.");
        var combinedFailure = new AggregateException(partialCommit, leaseReleaseFailure);
        Guid? sessionProjectId = null;
        ProjectCreationReservation? sessionReservation = null;

        var exception = await Assert.ThrowsAsync<AggregateException>(() => coordinator.CreateAsync<Guid>(
            agent,
            (_, _) => throw combinedFailure,
            projectId => projectId,
            CancellationToken.None,
            projectId => sessionProjectId = projectId,
            retainLifetimeAccessForSession: reservation => sessionReservation = reservation));

        Assert.Same(combinedFailure, exception);
        Assert.Equal(reservedProjectId, sessionProjectId);
        Assert.NotNull(sessionReservation);
        Assert.Equal(reservedProjectId, sessionReservation.ProjectId);
        Assert.Equal(Assert.Single(await proxy.ReadReservationsAsync()).LifetimeId, sessionReservation.LifetimeId);
        Assert.Equal(["grant"], proxy.Events);
        Assert.Contains(
            reservedProjectId,
            AgentProjectStructureAccessMetadata.Read(proxy.Agent.ConfigurationJson).AllowedProjectIds);
    }

    [Fact]
    public void Pure_validation_rejects_requests_before_access_is_reserved()
    {
        Assert.Equal(
            "ProjectNameRequired",
            Assert.Throws<ProjectStructureAgentException>(() =>
                ProjectStructureAgentCreationValidation.EnsureProjectRequest(
                    new ProjectStructureProjectSaveRequest(string.Empty, string.Empty, string.Empty, string.Empty))).ErrorCode);
        Assert.Equal(
            "ParentProjectRequired",
            Assert.Throws<ProjectStructureAgentException>(() =>
                ProjectStructureAgentCreationValidation.EnsureSubprojectRequest(
                    Guid.Empty,
                    new ProjectStructureProjectSaveRequest("Child", string.Empty, string.Empty, string.Empty))).ErrorCode);
        Assert.Equal(
            "SubprojectNameRequired",
            Assert.Throws<ProjectStructureAgentException>(() =>
                ProjectStructureAgentCreationValidation.EnsureNodesToSubprojectRequest(
                    Guid.NewGuid(),
                    new ProjectStructureNodesToSubprojectInput(string.Empty, ["node-1"]))).ErrorCode);
        Assert.Equal(
            "SelectedNodesRequired",
            Assert.Throws<ProjectStructureAgentException>(() =>
                ProjectStructureAgentCreationValidation.EnsureNodesToSubprojectRequest(
                    Guid.NewGuid(),
                    new ProjectStructureNodesToSubprojectInput("Child", []))).ErrorCode);
    }

    private static (
        ProjectStructureAgentProjectCreationCoordinator Coordinator,
        WorkspaceServiceProxy Proxy,
        AgentDefinition Agent,
        Guid ReservedProjectId) CreateCoordinator()
    {
        var agent = CreateAgent();
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, WorkspaceServiceProxy>();
        var proxy = (WorkspaceServiceProxy)(object)workspace;
        proxy.Agent = agent;
        var authorizationService = new ProjectStructureAgentAuthorizationService(workspace);
        var reservedProjectId = Guid.NewGuid();
        var databaseName = $"project-creation-reservation-{Guid.NewGuid():N}";
        var profile = new ResolvedDatabaseProfile(new() { Id = Guid.NewGuid(), ProviderKind = DatabaseProviderKind.InMemory },
            DatabaseProfileResolutionSource.ExplicitOverride, databaseName);
        var options = new DbContextOptionsBuilder<ProjectsDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, profile);
        var factory = new ProjectFactory(options.Options);
        proxy.ReservationFactory = factory;
        var admission = new ProjectWriteAdmissionService(factory, options.Options, CoordinatedDatabaseTransaction.ForProfile(profile), new CanonicalDatabase(profile));
        return (
            new ProjectStructureAgentProjectCreationCoordinator(
                authorizationService,
                admission,
                () => reservedProjectId),
            proxy,
            agent,
            reservedProjectId);
    }

    private static AgentDefinition CreateAgent()
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Guid.NewGuid(),
            "Restricted project creator",
            "Project creator",
            "Creates governed projects.",
            "Use project tools.",
            AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            Model: string.Empty,
            AgentWorkloadKind.Management,
            AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: AgentProjectStructureAccessMetadata.Write(
                "{}",
                new AgentProjectStructureAccessSettings { CanCreateProjects = true }),
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default with { CanUseTools = true },
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }

    private class WorkspaceServiceProxy : DispatchProxy
    {
        public AgentDefinition Agent { get; set; } = null!;

        public List<string> Events { get; } = [];

        public bool ThrowOnGrant { get; set; }

        public IDbContextFactory<ProjectsDbContext> ReservationFactory { get; set; } = null!;

        public async Task<ProjectCreationReservationRecord[]> ReadReservationsAsync() {
            await using var context = await ReservationFactory.CreateDbContextAsync();
            return await context.Set<ProjectCreationReservationRecord>().AsNoTracking().ToArrayAsync();
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            args ??= [];

            if (targetMethod.Name == nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync))
            {
                return Task.FromResult<IReadOnlyList<AgentDefinition>>([Agent]);
            }

            if (targetMethod.Name == nameof(IAgentFrameworkWorkspaceService.GrantAgentProjectStructureLifetimeAsync))
            {
                Events.Add("grant");
                if (ThrowOnGrant)
                {
                    throw new InvalidOperationException("Expected catalog grant failure.");
                }

                UpdateProjectAccess((AgentProjectStructureLifetime)args[1]!, add: true);
                return Task.CompletedTask;
            }

            if (targetMethod.Name == nameof(IAgentFrameworkWorkspaceService.RevokeAgentProjectStructureLifetimeAsync))
            {
                Events.Add("revoke");
                UpdateProjectAccess((AgentProjectStructureLifetime)args[1]!, add: false);
                return Task.CompletedTask;
            }

            throw new NotSupportedException($"Unexpected workspace call '{targetMethod.Name}'.");
        }

        private void UpdateProjectAccess(AgentProjectStructureLifetime lifetime, bool add) {
            Agent = Agent with {
                ConfigurationJson = add
                    ? AgentProjectStructureAccessMetadata.GrantProjectLifetime(Agent.ConfigurationJson, lifetime)
                    : AgentProjectStructureAccessMetadata.RevokeProjectLifetime(Agent.ConfigurationJson,
                        AgentProjectStructureRevocationTarget.ForLifetime(lifetime)).ConfigurationJson
            };
        }

    }
    private sealed class ProjectFactory(DbContextOptions<ProjectsDbContext> options) : IDbContextFactory<ProjectsDbContext> {
        public ProjectsDbContext CreateDbContext() => new(options);
    }

    private sealed class CanonicalDatabase(ResolvedDatabaseProfile profile) : ICanonicalRuntimeDatabase {
        public ResolvedDatabaseProfile Profile { get; } = profile;
        public long Generation => 1;
    }

}
