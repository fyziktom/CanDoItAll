using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectStructureAgentProjectCreationCoordinatorIntegrationTests
{
    [Fact]
    public Task Grant_failure_prevents_project_creation() => WithCoordinatorAsync(async (coordinator, proxy, agent, reservedProjectId) => {
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
        Assert.Equal(["grant", "revoke"], proxy.Events);
        Assert.Equal(ProjectCreationReservationState.Cancelled, Assert.Single(await proxy.ReadReservationsAsync()).State);
    });

    [Fact]
    public Task Grant_precedes_creation_and_creation_uses_the_reserved_id() => WithCoordinatorAsync(async (coordinator, proxy, agent, reservedProjectId) => {
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
    });

    [Fact]
    public Task Deterministic_creation_rejection_revokes_the_reserved_grant() => WithCoordinatorAsync(async (coordinator, proxy, agent, reservedProjectId) => {

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
    });

    [Fact]
    public Task Agent_mapped_creation_rejection_still_revokes_the_reserved_grant() => WithCoordinatorAsync(async (coordinator, proxy, agent, reservedProjectId) => {
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
    });

    [Fact]
    public Task Ambiguous_creation_exception_retains_the_reserved_grant() => WithCoordinatorAsync(async (coordinator, proxy, agent, reservedProjectId) => {

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
    });

    [Fact]
    public Task Transfer_failure_after_child_creation_retains_access_to_the_child() => WithCoordinatorAsync(async (coordinator, proxy, agent, reservedProjectId) => {

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
    });

    [Fact]
    public Task Compensated_transfer_without_original_receipt_keeps_the_grant_and_refuses_unbound_cleanup() => WithCoordinatorAsync(async (coordinator, proxy, agent, reservedProjectId) => {
        var transferFailure = new InvalidOperationException("Original transfer failure.");
        var reported = new ProjectStructureCompensatedSubprojectTransferException(reservedProjectId, transferFailure);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.CreateAsync<Guid>(
            agent, (_, _) => throw reported, projectId => projectId, CancellationToken.None));
        Assert.Same(reported, exception.InnerException);
        Assert.Equal(["grant"], proxy.Events);
        Assert.Contains(reservedProjectId, AgentProjectStructureAccessMetadata.Read(proxy.Agent.ConfigurationJson).AllowedProjectIds);
    });

    [Fact]
    public Task Unbound_compensation_with_a_lease_failure_preserves_both_failures_without_revoking() => WithCoordinatorAsync(async (coordinator, proxy, agent, reservedProjectId) => {
        var transferFailure = new InvalidOperationException("Original transfer failure.");
        var compensated = new ProjectStructureCompensatedSubprojectTransferException(reservedProjectId, transferFailure);
        var leaseFailure = new InvalidOperationException("Original lease release failure.");
        var combined = new AggregateException(compensated, leaseFailure);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.CreateAsync<Guid>(
            agent, (_, _) => throw combined, projectId => projectId, CancellationToken.None));
        Assert.Same(combined, exception.InnerException);
        Assert.Equal(["grant"], proxy.Events);
        Assert.Contains(reservedProjectId, AgentProjectStructureAccessMetadata.Read(proxy.Agent.ConfigurationJson).AllowedProjectIds);
    });

    [Fact]
    public Task Exact_compensated_creation_observes_the_real_participant_completion_without_second_revocation() => WithCoordinatorAsync(async (coordinator, proxy, agent, reservedProjectId) => {
        var transferFailure = new InvalidOperationException("Original transfer failure.");
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.CreateAsync<Guid>(agent,
            async (reservation, token) => {
                var created = await proxy.Projects.CreateWithReceiptAsync(reservation, new() { Name = "Compensated original creation" }, token);
                Assert.True(created.IsSuccess);
                var receipt = created.Value!;
                var outcome = Assert.IsType<ProjectCreationCompensationOutcome.Committed>(await proxy.Projects.CompensateCreationAsync(receipt, token));
                Assert.Null(outcome.CompletionIssue);
                throw new ProjectStructureCompensatedSubprojectTransferException(reservedProjectId, transferFailure) { CreationReceipt = receipt };
            }, projectId => projectId, CancellationToken.None));
        Assert.Same(transferFailure, exception);
        Assert.Equal(["grant", "revoke"], proxy.Events);
        Assert.DoesNotContain(reservedProjectId, AgentProjectStructureAccessMetadata.Read(proxy.Agent.ConfigurationJson).AllowedProjectIds);
        var reservation = Assert.Single(await proxy.ReadReservationsAsync());
        Assert.Equal(ProjectCreationReservationState.Cancelled, reservation.State);
        Assert.NotNull(reservation.ConsumedAtUtc);
    });

    [Fact]
    public Task Partial_commit_keeps_persisted_access_grants_session_access_and_rethrows_recovery_evidence() => WithCoordinatorAsync(async (coordinator, proxy, agent, reservedProjectId) => {
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
    });

    private static async Task WithCoordinatorAsync(Func<ProjectStructureAgentProjectCreationCoordinator,
        WorkspaceServiceProxy, AgentDefinition, Guid, Task> test) {
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, WorkspaceServiceProxy>();
        var proxy = (WorkspaceServiceProxy)(object)workspace;
        var agent = CreateAgent();
        proxy.Agent = agent;
        var application = await TestApplication.CreateAsync(new TestHarnessOptions {
            ConfigureServices = services => {
                foreach (var descriptor in services.Where(value => value.ServiceType == typeof(IProjectDeletionParticipant) &&
                        value.ImplementationType == typeof(AgentProjectStructureAccessDeletionParticipant)).ToArray()) {
                    services.Remove(descriptor);
                }
                services.AddScoped<IProjectDeletionParticipant>(provider =>
                    ActivatorUtilities.CreateInstance<AgentProjectStructureAccessDeletionParticipant>(provider, workspace));
            }
        });
        AsyncServiceScope? scope = null;
        Exception? primary = null;
        try {
            scope = application.Services.CreateAsyncScope();
            var services = scope.Value.ServiceProvider;
            proxy.ReservationFactory = services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>();
            proxy.Projects = services.GetRequiredService<ProjectsService>();
            var reservedProjectId = Guid.NewGuid();
            var coordinator = new ProjectStructureAgentProjectCreationCoordinator(
                new ProjectStructureAgentAuthorizationService(workspace),
                services.GetRequiredService<ProjectWriteAdmissionService>(),
                services.GetRequiredService<ProjectReservationCancellationOwner>(),
                services.GetRequiredService<ProjectCreationCompensationReader>(),
                services.GetRequiredService<ProjectDeletionCompletionProjection>(), () => reservedProjectId);
            await test(coordinator, proxy, agent, reservedProjectId);
        } catch (Exception failure) {
            primary = failure;
            throw;
        } finally {
            if (primary is null) {
                await TestFixtureCleanup.DisposeAsync(scope, application);
            } else {
                await TestFixtureCleanup.DisposeAfterFailureAsync(primary, scope, application);
            }
        }
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

        public ProjectsService Projects { get; set; } = null!;

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

            if (targetMethod.Name == nameof(IAgentFrameworkWorkspaceService.RevokeProjectStructureLifetimeAccessFromAllAgentsAsync))
            {
                Events.Add("revoke");
                Agent = Agent with { ConfigurationJson = AgentProjectStructureAccessMetadata.RevokeProjectLifetime(
                    Agent.ConfigurationJson, (AgentProjectStructureRevocationTarget)args[0]!).ConfigurationJson };
                return Task.FromResult(1);
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
}
