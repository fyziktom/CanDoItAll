using System.Reflection;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.AgentFramework;

namespace CanDoItAll.Tests.Unit;

/// <summary>
/// Failing-first characterization: source kinds without a canonical authority
/// rule must fail closed. The published observation may deny early, but it can
/// never select the workspace scope, and a UI access hint can never grant
/// mutation. These tests assert the required fail-closed contract and are
/// expected to fail until the source-keyed authority providers land.
/// </summary>
public sealed class UnknownSourceExecutionAuthorityBaselineTests
{
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid ProfileId = Guid.NewGuid();

    [Fact]
    public async Task Unknown_source_ui_hint_cannot_grant_mutation()
    {
        var agent = CreateAgent();
        var resolver = CreateResolver(agent);

        try
        {
            var authority = await resolver.ResolveAsync(CreateUnknownSourceRequest(
                agent.Id,
                observedScope: WorkspaceScopeDescriptor.Project(ProjectId.ToString("D")),
                uiAccessHint: new AgentChatContextAgentAccess(
                    agent.Id,
                    AgentChatContextPermission.Read | AgentChatContextPermission.Mutate,
                    "Published module access")));

            // A bounded read-only result is acceptable; a hint-derived mutation
            // grant is the defect this test characterizes.
            Assert.False(
                authority.MutationAllowed,
                "A UI access hint must never grant mutation for a source kind without a canonical authority rule.");
        }
        catch (AgentExecutionAuthorityMismatchException)
        {
            // Fail-closed denial of the unknown source is also an acceptable outcome.
        }
    }

    [Fact]
    public async Task Unknown_source_cannot_take_workspace_scope_from_published_observation()
    {
        var agent = CreateAgent();
        var resolver = CreateResolver(agent);
        var observedProjectScope = WorkspaceScopeDescriptor.Project(ProjectId.ToString("D"));

        try
        {
            var authority = await resolver.ResolveAsync(CreateUnknownSourceRequest(
                agent.Id,
                observedScope: observedProjectScope,
                uiAccessHint: null));

            Assert.NotEqual(
                observedProjectScope,
                authority.WorkspaceScope);
        }
        catch (AgentExecutionAuthorityMismatchException)
        {
            // Fail-closed denial of the unknown source is also an acceptable outcome.
        }
    }

    private static AgentExecutionAuthorityResolutionRequest CreateUnknownSourceRequest(
        Guid agentId,
        WorkspaceScopeDescriptor? observedScope,
        AgentChatContextAgentAccess? uiAccessHint)
        => new(
            agentId,
            new AgentChatContextSourceKind("workbench"),
            new AgentChatContextSourceId(ProjectId.ToString("D")),
            observedScope,
            new DatabaseProfileGeneration(1),
            uiAccessHint);

    private static CanonicalAgentExecutionAuthorityResolver CreateResolver(
        params AgentDefinition[] agents)
        => new(
            new ListOnlyWorkspaceFactory(agents),
            new FixedDatabaseProfileRuntimeAccessor(ProfileId),
            new FixedAgentExecutionProfileGenerationSource(new DatabaseProfileGeneration(1)),
            TimeProvider.System);

    private static AgentDefinition CreateAgent()
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Id: Guid.NewGuid(),
            Name: "Unknown source authority test agent",
            RoleTitle: "Assistant",
            Summary: "Unknown-source authority baseline test agent.",
            Instructions: "Test instructions.",
            Status: AgentLifecycleStatus.Active,
            ProviderProfileId: Guid.NewGuid(),
            Model: "test-model",
            Workload: AgentWorkloadKind.General,
            ChatHistoryMode: AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
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
                DisplayName = "Unknown source authority test profile",
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
