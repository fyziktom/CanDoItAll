using static CanDoItAll.Tests.Support.ProductToolPolicyTestRegistration;
using System.Reflection;
using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Capabilities.Access;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.SchedulerPlanner;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ModelCapabilityKind = CanDoItAll.AgentFramework.Models.CapabilityKind;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed partial class SchedulerAgentRuntimeToolProviderTests {
    [Fact]
    public void Scheduler_registers_exactly_one_scoped_source_owner() {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddSchedulerPlannerModule(configuration);
        services.AddSchedulerPlannerModule(configuration);

        var registration = Assert.Single(services, item =>
            item.ServiceType == typeof(IAgentExecutionSourceAuthorityProvider));
        Assert.Equal(typeof(SchedulerExecutionAuthorityProvider), registration.ImplementationType);
        Assert.Equal(ServiceLifetime.Scoped, registration.Lifetime);
        var dependencies = typeof(SchedulerExecutionAuthorityProvider).GetConstructors().SelectMany(constructor => constructor.GetParameters()).ToArray();
        Assert.Contains(dependencies, parameter => parameter.ParameterType == typeof(IAgentCatalogReadLeaseStore));
        Assert.DoesNotContain(dependencies, parameter => parameter.ParameterType == typeof(IAgentFrameworkWorkspaceService));
    }

    [Fact]
    public async Task Real_scheduler_surface_keeps_create_in_MAF_with_approval_and_original_readonly_stays_readonly() {
        var runtime = CreateHarness();
        var source = new SchedulerSourceHarness(runtime);
        var request = source.CreateRequest();
        var authority = await source.Resolver.ResolveAsync(request);
        Assert.Equal(WorkspaceScopeDescriptor.Sandbox, authority.WorkspaceScope);
        Assert.Equal(source.Profile.Id, authority.DatabaseProfileId);
        Assert.Equal(AgentExecutionAuthorityPolicyVersions.Canonical, authority.PolicyVersion);
        Assert.True(authority.ReadAllowed);
        Assert.True(authority.MutationAllowed);

        var state = await ComposeSchedulerToolsAsync(runtime);
        await MafRuntimeAgentFactory.FilterToolsOutsideExecutionGovernanceAsync(
            state, AgentExecutionGovernanceSnapshot.FromAuthority(authority), (_, _, _) => Task.CompletedTask);
        Assert.Equal(3, state.Tools.Count);
        Assert.IsType<ApprovalRequiredAIFunction>(state.Tools.Single(tool =>
            tool.Name == SchedulerToolPolicy.SchedulerWorkflowScheduleCreate));
        Assert.True(state.HasApprovalTools);

        var oldResolver = source.CreateResolver([]);
        var original = await oldResolver.ResolveAsync(request);
        Assert.Equal(AgentExecutionAuthorityPolicyVersions.FailClosedSandbox, original.PolicyVersion);
        Assert.False(original.MutationAllowed);
        Assert.NotEqual(original.PolicyFingerprint, authority.PolicyFingerprint);
        var oldState = await ComposeSchedulerToolsAsync(runtime);
        await MafRuntimeAgentFactory.FilterToolsOutsideExecutionGovernanceAsync(
            oldState, AgentExecutionGovernanceSnapshot.FromAuthority(original), (_, _, _) => Task.CompletedTask);
        Assert.Equal(2, oldState.Tools.Count);
        Assert.DoesNotContain(oldState.Tools, tool => tool.Name == SchedulerToolPolicy.SchedulerWorkflowScheduleCreate);
        Assert.False(oldState.HasApprovalTools);
        Assert.False(original.MutationAllowed);
    }

    public enum InvalidSchedulerActor { Id, Template, Inactive, IsTemplate, Tools, Scheduling }

    [Theory]
    [InlineData(InvalidSchedulerActor.Id)]
    [InlineData(InvalidSchedulerActor.Template)]
    [InlineData(InvalidSchedulerActor.Inactive)]
    [InlineData(InvalidSchedulerActor.IsTemplate)]
    [InlineData(InvalidSchedulerActor.Tools)]
    [InlineData(InvalidSchedulerActor.Scheduling)]
    public async Task Scheduler_source_preserves_plain_reads_without_granting_managed_mutations(InvalidSchedulerActor change) {
        var runtime = CreateHarness();
        var actor = runtime.Context.Agent;
        runtime.Workspace.Agents = [change switch {
            InvalidSchedulerActor.Id => actor with { Id = Guid.NewGuid() },
            InvalidSchedulerActor.Template => actor with { TemplateKey = "other-managed-template" },
            InvalidSchedulerActor.Inactive => actor with { Status = AgentLifecycleStatus.Suspended },
            InvalidSchedulerActor.IsTemplate => actor with { IsTemplate = true },
            InvalidSchedulerActor.Tools => actor with { Permissions = actor.Permissions with { CanUseTools = false } },
            InvalidSchedulerActor.Scheduling => actor with { Permissions = actor.Permissions with { CanScheduleWork = false } },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        }];
        var source = new SchedulerSourceHarness(runtime);
        var request = source.CreateRequest() with { AgentId = runtime.Workspace.Agents.Single().Id, UiAccessHint = null };
        if (change is InvalidSchedulerActor.Inactive or InvalidSchedulerActor.IsTemplate) {
            await Assert.ThrowsAsync<AgentExecutionAuthorityMismatchException>(() => source.Resolver.ResolveAsync(request).AsTask());
        } else {
            var authority = await source.Resolver.ResolveAsync(request);
            Assert.True(authority.ReadAllowed);
            Assert.False(authority.MutationAllowed);
            var context = runtime.Context with { Agent = runtime.Workspace.Agents.Single() };
            Assert.Empty(await runtime.Provider.CreateToolsAsync(context, default));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                new SchedulerAgentRuntimeAuthorizationService((IAgentFrameworkWorkspaceService)(object)runtime.Workspace)
                    .EnsureToolInvocationAuthorizedAsync(context.Agent.Id, SchedulerToolPolicy.SchedulerWorkflowScheduleCreate, default));
        }
        Assert.Equal(0, source.Probe.CatalogReads);
    }

    [Theory]
    [InlineData(SchedulerToolPolicy.SchedulerWorkflowTargetsSearch, false)]
    [InlineData(SchedulerToolPolicy.SchedulerWorkflowSchedulesSearch, false)]
    [InlineData(SchedulerToolPolicy.SchedulerWorkflowScheduleCreate, true)]
    public async Task Partial_current_capabilities_keep_only_their_existing_tool_authority(string toolName, bool canMutate) {
        var runtime = CreateHarness();
        var key = SchedulerAgentCapabilityKeys.ToolNameToCapabilityKey[toolName];
        var actor = runtime.Context.Agent with {
            Capabilities = runtime.Context.Agent.Capabilities.Where(item => item.CapabilityKey == key).ToArray()
        };
        runtime.Workspace.Agents = [actor];
        var source = new SchedulerSourceHarness(runtime);
        var authority = await source.Resolver.ResolveAsync(source.CreateRequest());
        Assert.True(authority.ReadAllowed);
        Assert.Equal(canMutate, authority.MutationAllowed);
        var tools = await runtime.Provider.CreateToolsAsync(runtime.Context with { Agent = actor }, default);
        Assert.Equal(toolName, Assert.Single(tools).Name);
    }

    public enum InvalidSchedulerCatalog { Missing, OtherId, Duplicate, OtherKind }

    [Theory]
    [InlineData(InvalidSchedulerCatalog.Missing)]
    [InlineData(InvalidSchedulerCatalog.OtherId)]
    [InlineData(InvalidSchedulerCatalog.Duplicate)]
    [InlineData(InvalidSchedulerCatalog.OtherKind)]
    public async Task Scheduler_identity_and_UI_hint_cannot_replace_the_exact_current_create_catalog_entry(InvalidSchedulerCatalog change) {
        var runtime = CreateHarness();
        var key = SchedulerAgentCapabilityKeys.ToolNameToCapabilityKey[SchedulerToolPolicy.SchedulerWorkflowScheduleCreate];
        var create = runtime.Workspace.Capabilities.Single(item => item.Key == key);
        var others = runtime.Workspace.Capabilities.Where(item => item.Key != key).ToArray();
        runtime.Workspace.Capabilities = change switch {
            InvalidSchedulerCatalog.Missing => others,
            InvalidSchedulerCatalog.OtherId => [.. others, create with { Id = Guid.NewGuid() }],
            InvalidSchedulerCatalog.Duplicate => [.. others, create, create],
            InvalidSchedulerCatalog.OtherKind => [.. others, create with { Kind = ModelCapabilityKind.Skill }],
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        var source = new SchedulerSourceHarness(runtime);
        var authority = await source.Resolver.ResolveAsync(source.CreateRequest());
        Assert.True(authority.ReadAllowed);
        Assert.False(authority.MutationAllowed);
        var state = await ComposeSchedulerToolsAsync(runtime);
        await MafRuntimeAgentFactory.FilterToolsOutsideExecutionGovernanceAsync(
            state, AgentExecutionGovernanceSnapshot.FromAuthority(authority), (_, _, _) => Task.CompletedTask);
        Assert.Equal(2, state.Tools.Count);
        Assert.DoesNotContain(state.Tools, item => item.Name == SchedulerToolPolicy.SchedulerWorkflowScheduleCreate);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            new SchedulerAgentRuntimeAuthorizationService((IAgentFrameworkWorkspaceService)(object)runtime.Workspace)
                .EnsureToolInvocationAuthorizedAsync(runtime.Context.Agent.Id, SchedulerToolPolicy.SchedulerWorkflowScheduleCreate, default));
    }

    [Fact]
    public async Task Removing_all_current_capabilities_keeps_plain_read_but_denies_tools_and_mutation() {
        var runtime = CreateHarness();
        runtime.Workspace.Capabilities = [];
        var source = new SchedulerSourceHarness(runtime);
        var authority = await source.Resolver.ResolveAsync(source.CreateRequest());
        Assert.True(authority.ReadAllowed);
        Assert.False(authority.MutationAllowed);
        Assert.Empty(await runtime.Provider.CreateToolsAsync(runtime.Context with { Capabilities = [] }, default));
        Assert.Equal(3, runtime.Context.Agent.Capabilities.Count);
    }

    public enum UnadmittedSchedulerScope { Sandbox, Organization, Project }

    [Theory]
    [InlineData(UnadmittedSchedulerScope.Sandbox)]
    [InlineData(UnadmittedSchedulerScope.Organization)]
    [InlineData(UnadmittedSchedulerScope.Project)]
    public async Task Initial_scheduler_scope_claims_never_become_authority(UnadmittedSchedulerScope scope) {
        var source = new SchedulerSourceHarness(CreateHarness());
        var observed = scope switch {
            UnadmittedSchedulerScope.Sandbox => WorkspaceScopeDescriptor.Sandbox,
            UnadmittedSchedulerScope.Organization => WorkspaceScopeDescriptor.Organization(source.Profile.Id.ToString("N")),
            UnadmittedSchedulerScope.Project => WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D")),
            _ => throw new ArgumentOutOfRangeException(nameof(scope))
        };
        await Assert.ThrowsAsync<AgentExecutionAuthorityMismatchException>(() => source.Resolver.ResolveAsync(
            source.CreateRequest() with { ObservedWorkspaceScope = observed }).AsTask());
        Assert.Equal(0, source.Probe.CatalogReads);
    }

    [Fact]
    public async Task Restored_scheduler_scope_revalidates_current_capabilities_without_replacing_original_authority() {
        var runtime = CreateHarness();
        var source = new SchedulerSourceHarness(runtime);
        var request = source.CreateRequest();
        var admitted = AgentExecutionGovernanceSnapshot.FromAuthority(await source.Resolver.ResolveAsync(request));
        var reference = new AgentTurnContextReference(AgentTurnContextId.Create(), AgentContextEpochId.Create(),
            request.SourceKind, request.SourceId, "planner", "calendar", 1, "saved-source-digest", DateTimeOffset.UtcNow);
        var restored = JsonSerializer.Deserialize<AgentExecutionGovernanceSnapshot>(JsonSerializer.Serialize(admitted, JsonOptions), JsonOptions)!;
        var restoredReference = JsonSerializer.Deserialize<AgentTurnContextReference>(JsonSerializer.Serialize(reference, JsonOptions), JsonOptions)!;
        var revalidation = AgentExecutionAuthorityResolutionRequest.FromCaptured(restoredReference, restored);
        var current = await source.Resolver.ResolveAsync(revalidation);
        Assert.Equal(admitted.PolicyFingerprint, current.PolicyFingerprint);
        Assert.True(current.MutationAllowed);
        Assert.Equal(admitted.DatabaseProfileId, current.DatabaseProfileId);
        var createKey = SchedulerAgentCapabilityKeys.ToolNameToCapabilityKey[SchedulerToolPolicy.SchedulerWorkflowScheduleCreate];
        runtime.Workspace.Agents = [runtime.Context.Agent with {
            Capabilities = runtime.Context.Agent.Capabilities.Where(item => item.CapabilityKey != createKey).ToArray()
        }];
        var revoked = await source.Resolver.ResolveAsync(revalidation);
        Assert.True(revoked.ReadAllowed);
        Assert.False(revoked.MutationAllowed);
        Assert.NotEqual(admitted.PolicyFingerprint, revoked.PolicyFingerprint);
        Assert.True(restored.MutationAllowed);
        Assert.Equal(admitted.PolicyFingerprint, restored.PolicyFingerprint);
        await Assert.ThrowsAsync<AgentExecutionAuthorityMismatchException>(() => source.Resolver.ResolveAsync(
            revalidation with { SourceId = new("other-scheduler-source") }).AsTask());
        source.Profile.Id = Guid.NewGuid();
        await Assert.ThrowsAsync<AgentExecutionAuthorityMismatchException>(() => source.Resolver.ResolveAsync(revalidation).AsTask());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Scheduler_checks_original_profile_before_and_after_current_catalog_read(bool switchDuringRead) {
        var runtime = CreateHarness();
        var source = new SchedulerSourceHarness(runtime);
        var request = new AgentExecutionSourceAuthorityRequest(runtime.Context.Agent,
            new(SchedulerAgentChatContextBuilder.SourceKind), new(SchedulerAgentChatContextBuilder.SourceId), null, source.Profile.Id);
        if (switchDuringRead) {
            source.Probe.BeforeCatalogRead = () => source.Profile.Id = Guid.NewGuid();
        } else {
            source.Profile.Id = Guid.NewGuid();
        }
        await Assert.ThrowsAsync<AgentExecutionAuthorityMismatchException>(() => source.Provider.ResolveAsync(request).AsTask());
        Assert.Equal(switchDuringRead ? 1 : 0, source.Probe.CatalogReads);
        Assert.Equal(switchDuringRead ? 1 : 0, source.Probe.LeaseDisposals);
    }

    [Fact]
    public async Task Unknown_source_stays_readonly_and_wrong_scheduler_identity_is_rejected() {
        var source = new SchedulerSourceHarness(CreateHarness());
        var request = source.CreateRequest();
        var unknown = await source.Resolver.ResolveAsync(request with { SourceKind = new("unregistered-scheduler-test") });
        Assert.True(unknown.ReadAllowed);
        Assert.False(unknown.MutationAllowed);
        Assert.Equal(AgentExecutionAuthorityPolicyVersions.FailClosedSandbox, unknown.PolicyVersion);
        await Assert.ThrowsAsync<AgentExecutionAuthorityMismatchException>(() => source.Resolver.ResolveAsync(
            request with { SourceId = new("other-scheduler") }).AsTask());
        Assert.Equal(0, source.Probe.CatalogReads);
    }

    private static async Task<RuntimeCapabilityState> ComposeSchedulerToolsAsync(RuntimeHarness runtime) {
        var composer = new RuntimeToolProviderComposer(new RuntimeToolProviderAccessFilter(ProductToolPolicies), ProductToolPolicies);
        var access = new RuntimeCapabilityAccessPlan(new EffectiveCapabilitySet([], []), AllowedCatalogCapabilities: [],
            [], new CapabilityAccessPolicyEvaluator(), InitialAllowedCapabilities: [], InitialDiagnostics: [],
            CatalogCapabilitiesByIdentity: new Dictionary<CapabilityIdentity, CapabilityCatalogItem>(),
            DescriptorsByKey: new Dictionary<string, CapabilityExposureDescriptor>(StringComparer.OrdinalIgnoreCase),
            CorrelationId: "scheduler-source-test");
        var state = new RuntimeCapabilityState();
        await composer.AttachAsync(new RuntimeToolProviderAttachmentRequest(state, access,
            composer.ComposeRegistrations([runtime.Provider]), runtime.Context, SuppressApprovalRequirements: false), default);
        return state;
    }

    [Fact]
    public async Task Scheduler_source_decision_uses_the_current_agent_from_its_released_catalog_lease() {
        var runtime = CreateHarness();
        var source = new SchedulerSourceHarness(runtime);
        var original = runtime.Context.Agent;
        var request = source.CreateRequest();
        source.Probe.BeforeCatalogRead = () => runtime.Workspace.Agents = [original with {
            Permissions = original.Permissions with { CanUseTools = false }
        }];
        var revoked = await source.Resolver.ResolveAsync(request);
        Assert.True(revoked.ReadAllowed);
        Assert.False(revoked.MutationAllowed);
        Assert.Equal(1, source.Probe.CatalogReads);
        Assert.Equal(1, source.Probe.LeaseDisposals);

        source.Probe.BeforeCatalogRead = null;
        runtime.Workspace.Agents = [original];
        var restored = await source.Resolver.ResolveAsync(request);
        Assert.True(restored.MutationAllowed);
        Assert.Equal(2, source.Probe.CatalogReads);
        Assert.Equal(2, source.Probe.LeaseDisposals);
    }

    private sealed class SchedulerSourceHarness {
        private readonly IAgentFrameworkWorkspaceService workspace;
        private readonly RuntimeHarness runtime;
        public SchedulerProfileAccessor Profile { get; } = new();
        public SchedulerSourceWorkspaceProxy Probe { get; }
        public SchedulerExecutionAuthorityProvider Provider { get; }
        public CanonicalAgentExecutionAuthorityResolver Resolver { get; }

        public SchedulerSourceHarness(RuntimeHarness runtime) {
            this.runtime = runtime;
            workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, SchedulerSourceWorkspaceProxy>();
            Probe = (SchedulerSourceWorkspaceProxy)(object)workspace;
            Probe.ReadAgents = () => runtime.Workspace.Agents;
            Probe.ReadCatalog = () => runtime.Workspace.Capabilities;
            Probe.CatalogScope = WorkspaceScopeDescriptor.Organization(Profile.Id.ToString("N"));
            Provider = new SchedulerExecutionAuthorityProvider(Probe, Profile);
            Resolver = CreateResolver([Provider]);
        }

        public CanonicalAgentExecutionAuthorityResolver CreateResolver(IEnumerable<IAgentExecutionSourceAuthorityProvider> providers)
            => new(new SchedulerSourceWorkspaceFactory(workspace), Profile,
                new FixedAgentExecutionProfileGenerationSource(new(1)), TimeProvider.System, providers);

        public AgentExecutionAuthorityResolutionRequest CreateRequest() {
            var surface = SchedulerAgentChatContextBuilder.Build(SchedulerAgentChatView.Calendar, null, null, null);
            return new(runtime.Context.Agent.Id, surface.Source.Kind, surface.Source.Id,
                surface.WorkspaceScope, new(1), surface.AgentAccess.Single());
        }
    }

    private sealed class SchedulerSourceWorkspaceFactory(IAgentFrameworkWorkspaceService workspace) : ICanDoItAllAgentWorkspaceFactory {
        public IAgentFrameworkWorkspaceService GetOrganizationWorkspaceService() => workspace;
        public IAgentFrameworkWorkspaceService GetWorkspaceService(WorkspaceScopeDescriptor scope) => throw new NotSupportedException();
        public WorkspaceScopeDescriptor GetOrganizationScope() => throw new NotSupportedException();
        public string GetWorkspaceRoot() => throw new NotSupportedException();
    }

    private class SchedulerSourceWorkspaceProxy : DispatchProxy, IAgentCatalogReadLeaseStore {
        public Func<IReadOnlyList<AgentDefinition>> ReadAgents { get; set; } = () => [];
        public Func<IReadOnlyList<CapabilityCatalogItem>> ReadCatalog { get; set; } = () => [];
        public Action? BeforeCatalogRead { get; set; }
        public int CatalogReads { get; private set; }
        public int LeaseDisposals { get; private set; }
        public WorkspaceScopeDescriptor CatalogScope { get; set; } = WorkspaceScopeDescriptor.Sandbox;
        public Task<IAgentCatalogReadLease> AcquireAgentReadLeaseAsync(Guid agentId, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            CatalogReads++;
            BeforeCatalogRead?.Invoke();
            return Task.FromResult<IAgentCatalogReadLease>(new SourceAuthorityTestCatalogLease(
                CatalogScope, ReadAgents().SingleOrDefault(agent => agent.Id == agentId), ReadCatalog(), () => LeaseDisposals++));
        }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) {
            if (targetMethod?.Name == nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync)) {
                return Task.FromResult(ReadAgents());
            }
            if (targetMethod?.Name == nameof(IAgentFrameworkWorkspaceService.ListCapabilitiesAsync)) {
                CatalogReads++;
                BeforeCatalogRead?.Invoke();
                return Task.FromResult(ReadCatalog());
            }
            throw new InvalidOperationException($"Unexpected Scheduler source access: {targetMethod?.Name}.");
        }
    }

    private sealed class SchedulerProfileAccessor : IDatabaseProfileRuntimeAccessor {
        public Guid Id { get; set; } = Guid.NewGuid();
        public ResolvedDatabaseProfile ResolveCurrentProfile() => new(new DatabaseProfileRecord {
            Id = Id, DisplayName = "Scheduler source test profile", ProviderKind = DatabaseProviderKind.InMemory,
            SourceKind = DatabaseProfileSourceKind.InMemory
        }, DatabaseProfileResolutionSource.ExplicitOverride, "test");
        public ResolvedDatabaseProfile ResolveProfile(Guid profileId) => throw new NotSupportedException();
    }
}
