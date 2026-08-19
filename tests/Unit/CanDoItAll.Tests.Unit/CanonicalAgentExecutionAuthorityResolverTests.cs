using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Processes.AgentChat;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit.AgentFramework;

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

    [Fact]
    public async Task Agents_surface_admits_mutation_for_tool_enabled_agent()
    {
        var agent = CreateAgent(new AgentProjectStructureAccessSettings());
        var resolver = CreateResolverWithProviders(
            [new AgentFrameworkAgentsExecutionAuthorityProvider()],
            agent);

        var authority = await resolver.ResolveAsync(new AgentExecutionAuthorityResolutionRequest(
            agent.Id,
            new AgentChatContextSourceKind(AgentFrameworkAgentsChatContextBuilder.SourceKind),
            new AgentChatContextSourceId("chat"),
            ObservedWorkspaceScope: null,
            new DatabaseProfileGeneration(1),
            UiAccessHint: null));

        Assert.Equal(WorkspaceScopeDescriptor.Sandbox, authority.WorkspaceScope);
        Assert.True(authority.ReadAllowed);
        Assert.True(authority.MutationAllowed);
        Assert.Equal(
            CanonicalAgentExecutionAuthorityResolver.CanonicalPolicyVersion,
            authority.PolicyVersion);
    }

    [Fact]
    public async Task Agents_surface_keeps_tool_disabled_agent_read_only()
    {
        var agent = CreateAgent(new AgentProjectStructureAccessSettings()) with
        {
            Permissions = AgentPermissionsPolicy.Default with { CanUseTools = false }
        };
        var resolver = CreateResolverWithProviders(
            [new AgentFrameworkAgentsExecutionAuthorityProvider()],
            agent);

        var authority = await resolver.ResolveAsync(new AgentExecutionAuthorityResolutionRequest(
            agent.Id,
            new AgentChatContextSourceKind(AgentFrameworkAgentsChatContextBuilder.SourceKind),
            new AgentChatContextSourceId("chat"),
            ObservedWorkspaceScope: null,
            new DatabaseProfileGeneration(1),
            UiAccessHint: null));

        Assert.True(authority.ReadAllowed);
        Assert.False(authority.MutationAllowed);
    }

    [Fact]
    public async Task Agents_surface_rejects_published_workspace_scope()
    {
        var agent = CreateAgent(new AgentProjectStructureAccessSettings());
        var resolver = CreateResolverWithProviders(
            [new AgentFrameworkAgentsExecutionAuthorityProvider()],
            agent);

        await Assert.ThrowsAsync<AgentExecutionAuthorityMismatchException>(
            () => resolver.ResolveAsync(new AgentExecutionAuthorityResolutionRequest(
                agent.Id,
                new AgentChatContextSourceKind(AgentFrameworkAgentsChatContextBuilder.SourceKind),
                new AgentChatContextSourceId("chat"),
                WorkspaceScopeDescriptor.Organization(ProfileId.ToString("N")),
                new DatabaseProfileGeneration(1),
                UiAccessHint: null)).AsTask());
    }

    [Fact]
    public void Authority_provider_implementations_remain_with_source_owning_modules()
    {
        var moduleRoot = Path.Combine(
            FindRepositoryRoot(),
            "src", "Modules", "CanDoItAll.Modules.AgentFramework");
        var source = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(moduleRoot, "*.cs", SearchOption.AllDirectories)
                .Select(File.ReadAllText));

        Assert.DoesNotContain("CreateDefaultProviders", source, StringComparison.Ordinal);
        Assert.Contains("AgentFrameworkAgentsExecutionAuthorityProvider", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectStructureExecutionAuthorityProvider", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectsExecutionAuthorityProvider", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ProcessesExecutionAuthorityProvider", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Source_authority_providers_are_registered_by_their_owning_modules()
    {
        var configuration = new ConfigurationBuilder().Build();
        var agentFrameworkServices = new ServiceCollection();
        agentFrameworkServices.AddAgentFrameworkModule(configuration);
        agentFrameworkServices.AddAgentFrameworkModule(configuration);
        AssertProviderRegistration(agentFrameworkServices, "AgentFrameworkAgentsExecutionAuthorityProvider");

        var projectsServices = new ServiceCollection();
        projectsServices.AddProjectsModule();
        projectsServices.AddProjectsModule();
        AssertProviderRegistration(projectsServices, "ProjectsExecutionAuthorityProvider");

        var workbenchServices = new ServiceCollection();
        workbenchServices.AddWorkbenchModule();
        workbenchServices.AddWorkbenchModule();
        AssertProviderRegistration(workbenchServices, "ProjectStructureExecutionAuthorityProvider");

        var processesServices = new ServiceCollection();
        processesServices.AddProcessesModule(configuration);
        processesServices.AddProcessesModule(configuration);
        AssertProviderRegistration(processesServices, "ProcessesExecutionAuthorityProvider");
        AssertProviderRegistration(processesServices, "LiveProcessesExecutionAuthorityProvider");
    }

    [Fact]
    public void Duplicate_source_authority_provider_keys_fail_deterministically()
    {
        var agent = CreateAgent(new AgentProjectStructureAccessSettings());

        var exception = Assert.Throws<InvalidOperationException>(() => CreateResolverWithProviders(
            [
                new StubSourceAuthorityProvider("duplicate"),
                new StubSourceAuthorityProvider("duplicate")
            ],
            agent));

        Assert.Equal(
            "Duplicate execution authority provider for source kind 'duplicate'.",
            exception.Message);
    }

    [Fact]
    public async Task Projects_portfolio_without_selection_remains_read_only_sandboxed()
    {
        var agent = CreateAgent(new AgentProjectStructureAccessSettings());
        var resolver = CreateResolverWithProviders(
            [new ProjectsExecutionAuthorityProvider()],
            agent);

        var authority = await resolver.ResolveAsync(new AgentExecutionAuthorityResolutionRequest(
            agent.Id,
            new AgentChatContextSourceKind(ProjectsAgentChatContextBuilder.SourceKind),
            new AgentChatContextSourceId(ProjectsAgentChatContextBuilder.WorkspaceSourceId),
            ObservedWorkspaceScope: null,
            new DatabaseProfileGeneration(1),
            UiAccessHint: null));

        Assert.Equal(WorkspaceScopeDescriptor.Sandbox, authority.WorkspaceScope);
        Assert.True(authority.ReadAllowed);
        Assert.False(authority.MutationAllowed);
    }

    [Fact]
    public async Task Process_sources_preserve_durable_project_authority()
    {
        var agent = CreateAgent(new AgentProjectStructureAccessSettings
        {
            CanRead = true,
            CanWrite = true,
            AllowAllProjects = true
        });
        var expectedScope = WorkspaceScopeDescriptor.Project(ProjectId.ToString("D"));
        IAgentExecutionSourceAuthorityProvider[] providers =
        [
            new ProcessesExecutionAuthorityProvider(),
            new LiveProcessesExecutionAuthorityProvider()
        ];

        foreach (var provider in providers)
        {
            var resolver = CreateResolverWithProviders([provider], agent);
            var authority = await resolver.ResolveAsync(new AgentExecutionAuthorityResolutionRequest(
                agent.Id,
                new AgentChatContextSourceKind(provider.SourceKind),
                new AgentChatContextSourceId($"surface:project:{ProjectId:D}"),
                expectedScope,
                new DatabaseProfileGeneration(1),
                UiAccessHint: null));

            Assert.Equal(expectedScope, authority.WorkspaceScope);
            Assert.True(authority.ReadAllowed);
            Assert.True(authority.MutationAllowed);
        }
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
        => CreateResolverWithProviders(
            [new ProjectStructureExecutionAuthorityProvider()],
            agents);

    private static CanonicalAgentExecutionAuthorityResolver CreateResolverWithProviders(
        IEnumerable<IAgentExecutionSourceAuthorityProvider> providers,
        params AgentDefinition[] agents)
        => new(
            new ListOnlyWorkspaceFactory(agents),
            new FixedDatabaseProfileRuntimeAccessor(ProfileId),
            new FixedAgentExecutionProfileGenerationSource(new DatabaseProfileGeneration(1)),
            TimeProvider.System,
            providers);

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

    private static void AssertProviderRegistration(
        IServiceCollection services,
        string implementationTypeName)
    {
        Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IAgentExecutionSourceAuthorityProvider) &&
                descriptor.ImplementationType?.Name == implementationTypeName &&
                descriptor.Lifetime == ServiceLifetime.Singleton);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
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

    private sealed class StubSourceAuthorityProvider(string sourceKind)
        : IAgentExecutionSourceAuthorityProvider
    {
        public string SourceKind { get; } = sourceKind;

        public ValueTask<AgentExecutionSourceAuthorityDecision> ResolveAsync(
            AgentExecutionSourceAuthorityRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
