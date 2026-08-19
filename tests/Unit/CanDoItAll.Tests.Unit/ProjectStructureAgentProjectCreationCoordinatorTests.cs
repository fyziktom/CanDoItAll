using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;

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
                return Task.FromResult(projectId);
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

        var result = await coordinator.CreateAsync(
            agent,
            (projectId, _) =>
            {
                proxy.Events.Add("create");
                receivedProjectId = projectId;
                return Task.FromResult(projectId);
            },
            projectId => projectId,
            CancellationToken.None);

        Assert.Equal(reservedProjectId, result);
        Assert.Equal(reservedProjectId, receivedProjectId);
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
                projectId,
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

        var exception = await Assert.ThrowsAsync<AggregateException>(() => coordinator.CreateAsync<Guid>(
            agent,
            (_, _) => throw combinedFailure,
            projectId => projectId,
            CancellationToken.None,
            projectId => sessionProjectId = projectId));

        Assert.Same(combinedFailure, exception);
        Assert.Equal(reservedProjectId, sessionProjectId);
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
        return (
            new ProjectStructureAgentProjectCreationCoordinator(
                authorizationService,
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

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            args ??= [];

            if (targetMethod.Name == nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync))
            {
                return Task.FromResult<IReadOnlyList<AgentDefinition>>([Agent]);
            }

            if (targetMethod.Name == nameof(IAgentFrameworkWorkspaceService.GrantAgentProjectStructureAccessAsync))
            {
                Events.Add("grant");
                if (ThrowOnGrant)
                {
                    throw new InvalidOperationException("Expected catalog grant failure.");
                }

                UpdateProjectAccess((Guid)args[1]!, add: true);
                return Task.CompletedTask;
            }

            if (targetMethod.Name == nameof(IAgentFrameworkWorkspaceService.RevokeAgentProjectStructureAccessAsync))
            {
                Events.Add("revoke");
                UpdateProjectAccess((Guid)args[1]!, add: false);
                return Task.CompletedTask;
            }

            throw new NotSupportedException($"Unexpected workspace call '{targetMethod.Name}'.");
        }

        private void UpdateProjectAccess(Guid projectId, bool add)
        {
            var access = AgentProjectStructureAccessMetadata.Read(Agent.ConfigurationJson);
            if (add)
            {
                access.AllowedProjectIds.Add(projectId);
            }
            else
            {
                access.AllowedProjectIds.Remove(projectId);
            }

            Agent = Agent with
            {
                ConfigurationJson = AgentProjectStructureAccessMetadata.Write(Agent.ConfigurationJson, access)
            };
        }
    }
}
