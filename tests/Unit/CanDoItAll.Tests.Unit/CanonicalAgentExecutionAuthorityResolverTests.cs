using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.AgentFramework;

namespace CanDoItAll.Tests.Unit;

/// <summary>
/// Canonical authority resolution: durable agent configuration decides
/// project-structure rights; UI hints and payloads cannot grant or widen them.
/// </summary>
public sealed class CanonicalAgentExecutionAuthorityResolverTests
{
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid ProfileId = Guid.NewGuid();

    [Fact]
    public async Task Project_structure_mutation_comes_from_agent_configuration_not_ui_hint()
    {
        var agent = CreateAgent(new AgentProjectStructureAccessSettings
        {
            CanRead = true,
            CanWrite = false,
            AllowAllProjects = true
        });
        var resolver = CreateResolver(agent);

        var authority = await resolver.ResolveAsync(CreateRequest(
            agent.Id,
            uiAccessHint: new AgentChatContextAgentAccess(
                agent.Id,
                AgentChatContextPermission.Read | AgentChatContextPermission.Mutate,
                "This project")));

        Assert.True(authority.ReadAllowed);
        Assert.False(authority.MutationAllowed);
        Assert.Equal(
            CanonicalAgentExecutionAuthorityResolver.CanonicalPolicyVersion,
            authority.PolicyVersion);
        Assert.Equal(
            WorkspaceScopeDescriptor.Project(ProjectId.ToString("D")),
            authority.WorkspaceScope);
    }

    [Fact]
    public async Task Project_structure_write_configuration_grants_mutation()
    {
        var agent = CreateAgent(new AgentProjectStructureAccessSettings
        {
            CanRead = true,
            CanWrite = true,
            AllowAllProjects = true
        });
        var resolver = CreateResolver(agent);

        var authority = await resolver.ResolveAsync(CreateRequest(agent.Id));

        Assert.True(authority.MutationAllowed);
    }

    [Fact]
    public async Task Agent_without_project_grant_is_denied()
    {
        var agent = CreateAgent(new AgentProjectStructureAccessSettings
        {
            CanRead = true,
            AllowAllProjects = false,
            AllowedProjectIds = [Guid.NewGuid()]
        });
        var resolver = CreateResolver(agent);

        await Assert.ThrowsAsync<AgentChatContextAccessDeniedException>(
            () => resolver.ResolveAsync(CreateRequest(agent.Id)).AsTask());
    }

    [Fact]
    public async Task Forged_project_scope_is_rejected()
    {
        var agent = CreateAgent(new AgentProjectStructureAccessSettings
        {
            CanRead = true,
            AllowAllProjects = true
        });
        var resolver = CreateResolver(agent);

        await Assert.ThrowsAsync<AgentExecutionAuthorityMismatchException>(
            () => resolver.ResolveAsync(CreateRequest(
                agent.Id,
                observedScope: WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D")))).AsTask());
    }

    [Fact]
    public async Task Inactive_agent_cannot_receive_authority()
    {
        var agent = CreateAgent(new AgentProjectStructureAccessSettings
        {
            CanRead = true,
            AllowAllProjects = true
        }) with
        {
            Status = AgentLifecycleStatus.Draft
        };
        var resolver = CreateResolver(agent);

        await Assert.ThrowsAsync<AgentExecutionAuthorityMismatchException>(
            () => resolver.ResolveAsync(CreateRequest(agent.Id)).AsTask());
    }

    [Fact]
    public async Task Organization_scope_must_belong_to_the_current_profile()
    {
        var agent = CreateAgent(new AgentProjectStructureAccessSettings());
        var resolver = CreateResolver(agent);

        await Assert.ThrowsAsync<AgentExecutionAuthorityMismatchException>(
            () => resolver.ResolveAsync(new AgentExecutionAuthorityResolutionRequest(
                agent.Id,
                new AgentChatContextSourceKind("workbench"),
                new AgentChatContextSourceId("workspace"),
                WorkspaceScopeDescriptor.Organization(Guid.NewGuid().ToString("N")),
                new DatabaseProfileGeneration(1),
                UiAccessHint: null)).AsTask());
    }

    private static AgentExecutionAuthorityResolutionRequest CreateRequest(
        Guid agentId,
        WorkspaceScopeDescriptor? observedScope = null,
        AgentChatContextAgentAccess? uiAccessHint = null)
        => new(
            agentId,
            new AgentChatContextSourceKind(AgentChatTrustedSourceKinds.ProjectStructure),
            new AgentChatContextSourceId(ProjectId.ToString("D")),
            observedScope ?? WorkspaceScopeDescriptor.Project(ProjectId.ToString("D")),
            new DatabaseProfileGeneration(1),
            uiAccessHint);

    private static CanonicalAgentExecutionAuthorityResolver CreateResolver(
        params AgentDefinition[] agents)
        => new(
            new ListOnlyWorkspaceFactory(agents),
            new FixedDatabaseProfileRuntimeAccessor(ProfileId),
            new FixedAgentExecutionProfileGenerationSource(new DatabaseProfileGeneration(1)),
            TimeProvider.System);

    private static AgentDefinition CreateAgent(AgentProjectStructureAccessSettings settings)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Id: Guid.NewGuid(),
            Name: "Authority test agent",
            RoleTitle: "Assistant",
            Summary: "Authority resolution test agent.",
            Instructions: "Test instructions.",
            Status: AgentLifecycleStatus.Active,
            ProviderProfileId: Guid.NewGuid(),
            Model: "test-model",
            Workload: AgentWorkloadKind.General,
            ChatHistoryMode: AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: AgentProjectStructureAccessMetadata.Write(null, settings),
            IsTemplate: false,
            TemplateKey: string.Empty,
            Permissions: AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: now,
            UpdatedAtUtc: now);
    }

    private sealed class ListOnlyWorkspaceFactory(IReadOnlyList<AgentDefinition> agents)
        : ICanDoItAllAgentWorkspaceFactory
    {
        public IAgentFrameworkWorkspaceService GetOrganizationWorkspaceService()
        {
            var proxy = DispatchProxy.Create<IAgentFrameworkWorkspaceService, ListAgentsProxy>();
            ((ListAgentsProxy)(object)proxy).Agents = agents;
            return proxy;
        }

        public IAgentFrameworkWorkspaceService GetWorkspaceService(WorkspaceScopeDescriptor scope)
            => throw new NotSupportedException();

        public WorkspaceScopeDescriptor GetOrganizationScope()
            => WorkspaceScopeDescriptor.Organization(ProfileId.ToString("N"));

        public string GetWorkspaceRoot()
            => throw new NotSupportedException();
    }

    private class ListAgentsProxy : DispatchProxy
    {
        public IReadOnlyList<AgentDefinition> Agents { get; set; } = [];

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name == nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync))
            {
                return Task.FromResult(Agents);
            }

            throw new NotSupportedException(
                $"The canonical authority resolver must not call '{targetMethod.Name}'.");
        }
    }

    private sealed class FixedDatabaseProfileRuntimeAccessor(Guid profileId)
        : IDatabaseProfileRuntimeAccessor
    {
        private readonly ResolvedDatabaseProfile resolvedProfile = new(
            new DatabaseProfileRecord
            {
                Id = profileId,
                DisplayName = "Authority test profile",
                ProviderKind = DatabaseProviderKind.InMemory,
                SourceKind = DatabaseProfileSourceKind.InMemory
            },
            DatabaseProfileResolutionSource.ExplicitOverride,
            "test");

        public ResolvedDatabaseProfile ResolveCurrentProfile()
            => resolvedProfile;

        public ResolvedDatabaseProfile ResolveProfile(Guid requestedProfileId)
            => resolvedProfile;
    }
}
