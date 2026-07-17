using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureAgentAuthorizationServiceTests
{
    [Fact]
    public async Task Creation_permissions_are_revalidated_independently_from_canonical_agent_state()
    {
        var parentProjectId = Guid.NewGuid();
        var (workspace, proxy) = CreateWorkspace(CreateAgent(new AgentProjectStructureAccessSettings
        {
            CanCreateProjects = true,
            CanCreateSubprojects = false,
            AllowedProjectIds = [parentProjectId]
        }));
        var service = new ProjectStructureAgentAuthorizationService(workspace);

        await service.EnsureProjectCreationAuthorizedAsync(proxy.Agent.Id, CancellationToken.None);
        var denied = await Assert.ThrowsAsync<ProjectStructureAgentException>(
            () => service.EnsureSubprojectCreationAuthorizedAsync(
                proxy.Agent.Id,
                parentProjectId,
                CancellationToken.None));

        Assert.Equal(403, denied.StatusCode);

        proxy.Agent = proxy.Agent with { Status = AgentLifecycleStatus.Suspended };
        await Assert.ThrowsAsync<ProjectStructureAgentException>(
            () => service.EnsureProjectCreationAuthorizedAsync(proxy.Agent.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Subproject_creation_requires_the_current_parent_scope()
    {
        var allowedParentId = Guid.NewGuid();
        var (workspace, proxy) = CreateWorkspace(CreateAgent(new AgentProjectStructureAccessSettings
        {
            CanCreateSubprojects = true,
            AllowedProjectIds = [allowedParentId]
        }));
        var service = new ProjectStructureAgentAuthorizationService(workspace);

        await service.EnsureSubprojectCreationAuthorizedAsync(
            proxy.Agent.Id,
            allowedParentId,
            CancellationToken.None);

        await Assert.ThrowsAsync<ProjectStructureAgentException>(
            () => service.EnsureSubprojectCreationAuthorizedAsync(
                proxy.Agent.Id,
                Guid.NewGuid(),
                CancellationToken.None));
    }

    [Theory]
    [InlineData(HierarchyProject.Parent)]
    [InlineData(HierarchyProject.Child)]
    [InlineData(HierarchyProject.CurrentParent)]
    public async Task Subproject_link_requires_write_access_to_every_hierarchy_project(
        HierarchyProject unauthorizedProject)
    {
        var parentProjectId = Guid.NewGuid();
        var childProjectId = Guid.NewGuid();
        var currentParentProjectId = Guid.NewGuid();
        var (workspace, proxy) = CreateWorkspace(CreateAgent(new AgentProjectStructureAccessSettings
        {
            CanCreateSubprojects = true,
            CanWriteNonTaskStructure = true,
            AllowedProjectIds = [parentProjectId, childProjectId, currentParentProjectId]
        }));
        var service = new ProjectStructureAgentAuthorizationService(workspace);

        await service.EnsureSubprojectLinkAuthorizedAsync(
            proxy.Agent.Id,
            parentProjectId,
            childProjectId,
            currentParentProjectId,
            CancellationToken.None);

        var allowedProjectIds = new List<Guid>
        {
            parentProjectId,
            childProjectId,
            currentParentProjectId
        };
        allowedProjectIds.Remove(unauthorizedProject switch
        {
            HierarchyProject.Parent => parentProjectId,
            HierarchyProject.Child => childProjectId,
            HierarchyProject.CurrentParent => currentParentProjectId,
            _ => throw new ArgumentOutOfRangeException(nameof(unauthorizedProject))
        });
        proxy.Agent = CreateAgent(new AgentProjectStructureAccessSettings
        {
            CanCreateSubprojects = true,
            CanWriteNonTaskStructure = true,
            AllowedProjectIds = allowedProjectIds
        }) with { Id = proxy.Agent.Id };

        await AssertAccessDeniedAsync(
            () => service.EnsureSubprojectLinkAuthorizedAsync(
                proxy.Agent.Id,
                parentProjectId,
                childProjectId,
                currentParentProjectId,
                CancellationToken.None),
            AgentToolInvocationPolicyMetadata.ProjectStructureSubprojectLink);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Subproject_link_requires_creation_and_structure_write_permissions(
        bool canCreateSubprojects,
        bool canWriteNonTaskStructure)
    {
        var parentProjectId = Guid.NewGuid();
        var childProjectId = Guid.NewGuid();
        var (workspace, proxy) = CreateWorkspace(CreateAgent(new AgentProjectStructureAccessSettings
        {
            CanCreateSubprojects = canCreateSubprojects,
            CanWriteNonTaskStructure = canWriteNonTaskStructure,
            AllowedProjectIds = [parentProjectId, childProjectId]
        }));
        var service = new ProjectStructureAgentAuthorizationService(workspace);

        await AssertAccessDeniedAsync(
            () => service.EnsureSubprojectLinkAuthorizedAsync(
                proxy.Agent.Id,
                parentProjectId,
                childProjectId,
                currentParentProjectId: null,
                CancellationToken.None),
            AgentToolInvocationPolicyMetadata.ProjectStructureSubprojectLink);
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    public async Task Subproject_link_allows_broad_or_non_task_structure_writers(
        bool canWrite,
        bool canWriteNonTaskStructure,
        bool hasCurrentParent)
    {
        var parentProjectId = Guid.NewGuid();
        var childProjectId = Guid.NewGuid();
        var currentParentProjectId = hasCurrentParent ? Guid.NewGuid() : (Guid?)null;
        Guid[] allowedProjectIds = currentParentProjectId.HasValue
            ? new[] { parentProjectId, childProjectId, currentParentProjectId.Value }
            : [parentProjectId, childProjectId];
        var (workspace, proxy) = CreateWorkspace(CreateAgent(new AgentProjectStructureAccessSettings
        {
            CanCreateSubprojects = true,
            CanWrite = canWrite,
            CanWriteNonTaskStructure = canWriteNonTaskStructure,
            AllowedProjectIds = [.. allowedProjectIds]
        }));
        var service = new ProjectStructureAgentAuthorizationService(workspace);

        await service.EnsureSubprojectLinkAuthorizedAsync(
            proxy.Agent.Id,
            parentProjectId,
            childProjectId,
            currentParentProjectId,
            CancellationToken.None);
    }

    [Fact]
    public async Task Nodes_to_new_subproject_uses_the_current_non_task_write_guard_after_downgrade()
    {
        var projectId = Guid.NewGuid();
        var (workspace, proxy) = CreateWorkspace(CreateAgent(new AgentProjectStructureAccessSettings
        {
            CanCreateSubprojects = true,
            CanWrite = true,
            AllowedProjectIds = [projectId]
        }));
        var service = new ProjectStructureAgentAuthorizationService(workspace);

        var initial = await service.EnsureNodesToNewSubprojectAuthorizedAsync(
            proxy.Agent.Id,
            projectId,
            CancellationToken.None);
        Assert.False(initial.RequiresNonTaskWriteGuard);

        proxy.Agent = CreateAgent(new AgentProjectStructureAccessSettings
        {
            CanCreateSubprojects = true,
            CanWriteNonTaskStructure = true,
            AllowedProjectIds = [projectId]
        }) with { Id = proxy.Agent.Id };
        var downgraded = await service.EnsureNodesToNewSubprojectAuthorizedAsync(
            proxy.Agent.Id,
            projectId,
            CancellationToken.None);

        Assert.True(downgraded.RequiresNonTaskWriteGuard);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task Nodes_to_new_subproject_requires_creation_and_structure_write_permissions(
        bool canCreateSubprojects,
        bool canWriteNonTaskStructure)
    {
        var sourceProjectId = Guid.NewGuid();
        var (workspace, proxy) = CreateWorkspace(CreateAgent(new AgentProjectStructureAccessSettings
        {
            CanCreateSubprojects = canCreateSubprojects,
            CanWriteNonTaskStructure = canWriteNonTaskStructure,
            AllowedProjectIds = [sourceProjectId]
        }));
        var service = new ProjectStructureAgentAuthorizationService(workspace);

        await AssertAccessDeniedAsync(
            () => service.EnsureNodesToNewSubprojectAuthorizedAsync(
                proxy.Agent.Id,
                sourceProjectId,
                CancellationToken.None),
            AgentToolInvocationPolicyMetadata.ProjectStructureNodesToNewSubproject);
    }

    [Fact]
    public async Task Nodes_to_new_subproject_requires_access_to_the_source_project()
    {
        var (workspace, proxy) = CreateWorkspace(CreateAgent(new AgentProjectStructureAccessSettings
        {
            CanCreateSubprojects = true,
            CanWriteNonTaskStructure = true,
            AllowedProjectIds = [Guid.NewGuid()]
        }));
        var service = new ProjectStructureAgentAuthorizationService(workspace);

        await AssertAccessDeniedAsync(
            () => service.EnsureNodesToNewSubprojectAuthorizedAsync(
                proxy.Agent.Id,
                Guid.NewGuid(),
                CancellationToken.None),
            AgentToolInvocationPolicyMetadata.ProjectStructureNodesToNewSubproject);
    }

    [Theory]
    [InlineData(ActorDenialReason.Suspended)]
    [InlineData(ActorDenialReason.ToolUseDisabled)]
    public async Task Subproject_mutations_require_an_active_tool_enabled_actor(ActorDenialReason reason)
    {
        var parentProjectId = Guid.NewGuid();
        var childProjectId = Guid.NewGuid();
        var (workspace, proxy) = CreateWorkspace(CreateAgent(new AgentProjectStructureAccessSettings
        {
            CanCreateSubprojects = true,
            CanWriteNonTaskStructure = true,
            AllowedProjectIds = [parentProjectId, childProjectId]
        }));
        proxy.Agent = reason switch
        {
            ActorDenialReason.Suspended => proxy.Agent with { Status = AgentLifecycleStatus.Suspended },
            ActorDenialReason.ToolUseDisabled => proxy.Agent with
            {
                Permissions = proxy.Agent.Permissions with { CanUseTools = false }
            },
            _ => throw new ArgumentOutOfRangeException(nameof(reason))
        };
        var service = new ProjectStructureAgentAuthorizationService(workspace);

        await AssertAccessDeniedAsync(
            () => service.EnsureSubprojectLinkAuthorizedAsync(
                proxy.Agent.Id,
                parentProjectId,
                childProjectId,
                currentParentProjectId: null,
                CancellationToken.None),
            "project-structure");
        await AssertAccessDeniedAsync(
            () => service.EnsureNodesToNewSubprojectAuthorizedAsync(
                proxy.Agent.Id,
                parentProjectId,
                CancellationToken.None),
            "project-structure");
    }

    [Fact]
    public async Task Created_project_access_is_forwarded_to_durable_workspace_storage()
    {
        var (workspace, proxy) = CreateWorkspace(CreateAgent(new AgentProjectStructureAccessSettings
        {
            CanCreateProjects = true
        }));
        var service = new ProjectStructureAgentAuthorizationService(workspace);
        var projectId = Guid.NewGuid();

        await service.GrantCreatedProjectAccessAsync(proxy.Agent.Id, projectId, CancellationToken.None);

        Assert.Equal((proxy.Agent.Id, projectId), Assert.Single(proxy.ProjectAccessGrants));
    }

    private static (IAgentFrameworkWorkspaceService Workspace, WorkspaceServiceProxy Proxy) CreateWorkspace(
        AgentDefinition agent)
    {
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, WorkspaceServiceProxy>();
        var proxy = (WorkspaceServiceProxy)(object)workspace;
        proxy.Agent = agent;
        return (workspace, proxy);
    }

    private static AgentDefinition CreateAgent(AgentProjectStructureAccessSettings access)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Guid.NewGuid(),
            "Project creator",
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
            ConfigurationJson: AgentProjectStructureAccessMetadata.Write("{}", access),
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default with { CanUseTools = true },
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }

    private static async Task AssertAccessDeniedAsync(Func<Task> action, string toolName)
    {
        var exception = await Assert.ThrowsAsync<ProjectStructureAgentException>(action);

        Assert.Equal(403, exception.StatusCode);
        Assert.Equal("AgentToolAccessDenied", exception.ErrorCode);
        Assert.Contains($"'{toolName}'", exception.Message);
    }

    public enum HierarchyProject
    {
        Parent,
        Child,
        CurrentParent
    }

    public enum ActorDenialReason
    {
        Suspended,
        ToolUseDisabled
    }

    private class WorkspaceServiceProxy : DispatchProxy
    {
        public AgentDefinition Agent { get; set; } = null!;

        public List<(Guid AgentId, Guid ProjectId)> ProjectAccessGrants { get; } = [];

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
                ProjectAccessGrants.Add(((Guid)args[0]!, (Guid)args[1]!));
                return Task.CompletedTask;
            }

            throw new NotSupportedException($"Unexpected workspace call '{targetMethod.Name}'.");
        }
    }
}
