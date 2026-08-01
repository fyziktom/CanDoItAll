using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit;

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
