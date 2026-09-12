using static CanDoItAll.Tests.Support.ProductToolPolicyTestRegistration;
using System.Reflection;
using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Capabilities.Access;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ModelCapabilityKind = CanDoItAll.AgentFramework.Models.CapabilityKind;

namespace CanDoItAll.Tests.Unit.AgentFramework;

[Collection(AppDbContextModelRegistryTestCollectionNames.Name)]
public sealed partial class CuratorExecutionSourceAuthorityTests {
    public enum Curator { Prompts, Workflows }
    public enum InvalidActor { Id, Template, Inactive, IsTemplate, Tools }

    [Fact]
    public void Module_registers_both_scoped_catalog_source_owners_once() {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddAgentFrameworkModule(configuration);
        services.AddAgentFrameworkModule(configuration);
        Assert.Single(services, item => item.ServiceType == typeof(IAgentRuntimeToolProvider) &&
            item.ImplementationType == typeof(PromptGalleryAgentRuntimeToolProvider));
        foreach (var type in new[] { typeof(PromptGalleryExecutionAuthorityProvider), typeof(WorkflowsExecutionAuthorityProvider) }) {
            var descriptor = Assert.Single(services, item => item.ServiceType == typeof(IAgentExecutionSourceAuthorityProvider) &&
                item.ImplementationType == type);
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
            var dependencies = type.GetConstructors().SelectMany(constructor => constructor.GetParameters()).ToArray();
            Assert.Contains(dependencies, parameter => parameter.ParameterType == typeof(IAgentCatalogReadLeaseStore));
            Assert.DoesNotContain(dependencies, parameter => parameter.ParameterType == typeof(IAgentFrameworkWorkspaceService));
        }
    }

    [Theory]
    [InlineData(Curator.Prompts, 5, 3)]
    [InlineData(Curator.Workflows, 7, 4)]
    public async Task Real_surface_and_canonical_owner_retain_MAF_mutations_with_approval(Curator curator, int tools, int mutations) {
        var fixture = new Fixture(curator);
        var request = fixture.Request();
        var authority = await fixture.Resolver.ResolveAsync(request);
        Assert.Equal(WorkspaceScopeDescriptor.Sandbox, authority.WorkspaceScope);
        Assert.Equal(fixture.Profile.Id, authority.DatabaseProfileId);
        Assert.Equal(AgentExecutionAuthorityPolicyVersions.Canonical, authority.PolicyVersion);
        Assert.True(authority.ReadAllowed);
        Assert.True(authority.MutationAllowed);
        var state = await fixture.ComposeAsync(authority);
        Assert.Equal(tools, state.Tools.Count);
        Assert.Equal(mutations, state.Tools.OfType<ApprovalRequiredAIFunction>().Count());
        Assert.True(state.HasApprovalTools);
        Assert.All(state.RuntimeToolMetadata.Where(item => item.OperationKind == AgentRuntimeToolOperationKind.Mutation),
            item => Assert.True(item.RequiresApprovalByDefault));

        var previous = await fixture.CreateResolver([]).ResolveAsync(request);
        Assert.Equal(AgentExecutionAuthorityPolicyVersions.FailClosedSandbox, previous.PolicyVersion);
        Assert.False(previous.MutationAllowed);
        Assert.NotEqual(previous.PolicyFingerprint, authority.PolicyFingerprint);
        var oldState = await fixture.ComposeAsync(previous);
        Assert.Equal(tools - mutations, oldState.Tools.Count);
        Assert.Empty(oldState.Tools.OfType<ApprovalRequiredAIFunction>());
        Assert.False(previous.MutationAllowed);
    }

    [Theory]
    [InlineData(Curator.Prompts)]
    [InlineData(Curator.Workflows)]
    public async Task Current_capabilities_preserve_partial_read_and_require_exact_catalog_identity(Curator curator) {
        var fixture = new Fixture(curator);
        var original = fixture.Agent;
        foreach (var mutation in fixture.Policies.Where(item => item.IsStateChanging)) {
            var key = fixture.Keys[mutation.Name];
            fixture.Agent = original with { Capabilities = original.Capabilities.Where(item => item.CapabilityKey == key).ToArray() };
            var isolated = await fixture.Resolver.ResolveAsync(fixture.Request());
            Assert.True(isolated.ReadAllowed);
            Assert.True(isolated.MutationAllowed);
        }
        var read = fixture.Policies.First(item => !item.IsStateChanging);
        fixture.Agent = original with { Capabilities = original.Capabilities.Where(item => item.CapabilityKey == fixture.Keys[read.Name]).ToArray() };
        var readonlyAuthority = await fixture.Resolver.ResolveAsync(fixture.Request());
        Assert.True(readonlyAuthority.ReadAllowed);
        Assert.False(readonlyAuthority.MutationAllowed);
        Assert.Equal(read.Name, Assert.Single((await fixture.ComposeAsync(readonlyAuthority)).Tools).Name);

        fixture.Agent = original;
        var catalog = fixture.Catalog;
        var readEntries = catalog.Where(item => fixture.Policies.Any(policy => !policy.IsStateChanging && fixture.Keys[policy.Name] == item.Key)).ToArray();
        var mutationEntries = catalog.Except(readEntries).ToArray();
        IReadOnlyList<CapabilityCatalogItem>[] invalidCatalogs = [
            readEntries,
            [.. readEntries, .. mutationEntries.Select(item => item with { Id = Guid.NewGuid() })],
            [.. readEntries, .. mutationEntries, .. mutationEntries],
            [.. readEntries, .. mutationEntries.Select(item => item with { Kind = ModelCapabilityKind.Skill })]
        ];
        foreach (var invalid in invalidCatalogs) {
            fixture.Catalog = invalid;
            var denied = await fixture.Resolver.ResolveAsync(fixture.Request());
            Assert.True(denied.ReadAllowed);
            Assert.False(denied.MutationAllowed);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.RequireToolAsync(fixture.Policies.First(item => item.IsStateChanging).Name));
        }
        fixture.Catalog = [];
        var empty = await fixture.Resolver.ResolveAsync(fixture.Request());
        Assert.True(empty.ReadAllowed);
        Assert.Empty((await fixture.ComposeAsync(empty)).Tools);
        Assert.False(empty.MutationAllowed);
        Assert.Same(original, fixture.Agent);
    }

    [Theory]
    [InlineData(Curator.Prompts)]
    [InlineData(Curator.Workflows)]
    public async Task Restored_source_uses_original_scope_and_current_actor_without_upgrading_old_evidence(Curator curator) {
        var fixture = new Fixture(curator);
        var request = fixture.Request();
        var authority = AgentExecutionGovernanceSnapshot.FromAuthority(await fixture.Resolver.ResolveAsync(request));
        var reference = new AgentTurnContextReference(AgentTurnContextId.Create(), AgentContextEpochId.Create(),
            request.SourceKind, request.SourceId, "catalog", "editor", 1, "saved-curator-source", DateTimeOffset.UtcNow);
        var restoredAuthority = JsonSerializer.Deserialize<AgentExecutionGovernanceSnapshot>(JsonSerializer.Serialize(authority))!;
        var restoredReference = JsonSerializer.Deserialize<AgentTurnContextReference>(JsonSerializer.Serialize(reference))!;
        var revalidation = AgentExecutionAuthorityResolutionRequest.FromCaptured(restoredReference, restoredAuthority);
        var current = await fixture.Resolver.ResolveAsync(revalidation);
        Assert.Equal(authority.PolicyFingerprint, current.PolicyFingerprint);
        Assert.Equal(authority.WorkspaceScope, current.WorkspaceScope);
        fixture.Agent = fixture.Agent with { Capabilities = [] };
        var revoked = await fixture.Resolver.ResolveAsync(revalidation);
        Assert.True(revoked.ReadAllowed);
        Assert.False(revoked.MutationAllowed);
        Assert.True(restoredAuthority.ReadAllowed);
        Assert.True(restoredAuthority.MutationAllowed);
        Assert.Equal(authority.PolicyFingerprint, restoredAuthority.PolicyFingerprint);
        await Assert.ThrowsAsync<AgentExecutionAuthorityMismatchException>(() => fixture.Resolver.ResolveAsync(
            revalidation with { SourceId = new("different-source") }).AsTask());
        fixture.Profile.Id = Guid.NewGuid();
        await Assert.ThrowsAsync<AgentExecutionAuthorityMismatchException>(() => fixture.Resolver.ResolveAsync(revalidation).AsTask());
    }

    [Theory]
    [InlineData(Curator.Prompts, InvalidActor.Id)]
    [InlineData(Curator.Prompts, InvalidActor.Template)]
    [InlineData(Curator.Prompts, InvalidActor.Inactive)]
    [InlineData(Curator.Prompts, InvalidActor.IsTemplate)]
    [InlineData(Curator.Prompts, InvalidActor.Tools)]
    [InlineData(Curator.Workflows, InvalidActor.Id)]
    [InlineData(Curator.Workflows, InvalidActor.Template)]
    [InlineData(Curator.Workflows, InvalidActor.Inactive)]
    [InlineData(Curator.Workflows, InvalidActor.IsTemplate)]
    [InlineData(Curator.Workflows, InvalidActor.Tools)]
    public async Task Source_reads_preserve_active_actors_while_managed_mutation_requirements_remain_required(Curator curator, InvalidActor change) {
        var fixture = new Fixture(curator);
        var actor = fixture.Agent;
        fixture.Agent = change switch {
            InvalidActor.Id => actor with { Id = Guid.NewGuid() },
            InvalidActor.Template => actor with { TemplateKey = "other-template" },
            InvalidActor.Inactive => actor with { Status = AgentLifecycleStatus.Suspended },
            InvalidActor.IsTemplate => actor with { IsTemplate = true },
            InvalidActor.Tools => actor with { Permissions = actor.Permissions with { CanUseTools = false } },
            _ => throw new ArgumentOutOfRangeException(nameof(change))
        };
        var request = fixture.Request() with { UiAccessHint = null };
        if (change is InvalidActor.Inactive or InvalidActor.IsTemplate) {
            await Assert.ThrowsAsync<AgentExecutionAuthorityMismatchException>(() => fixture.Resolver.ResolveAsync(request).AsTask());
        } else {
            var authority = await fixture.Resolver.ResolveAsync(request);
            Assert.True(authority.ReadAllowed);
            Assert.False(authority.MutationAllowed);
            Assert.Empty((await fixture.ComposeAsync(authority)).Tools);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.RequireToolAsync(
                fixture.Policies.First(item => item.IsStateChanging).Name));
        }
        Assert.Equal(0, fixture.Probe.CatalogReads);
    }

    [Theory]
    [InlineData(Curator.Prompts)]
    [InlineData(Curator.Workflows)]
    public async Task Initial_workspace_claims_are_rejected_even_when_position_has_a_project(Curator curator) {
        var fixture = new Fixture(curator);
        var request = fixture.Request(curator == Curator.Workflows ? WorkflowSurface(AgentFrameworkWorkflowsChatView.Editor, true, true) : null);
        WorkspaceScopeDescriptor[] scopes = [WorkspaceScopeDescriptor.Sandbox,
            WorkspaceScopeDescriptor.Organization(fixture.Profile.Id.ToString("N")), WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D"))];
        foreach (var scope in scopes) {
            await Assert.ThrowsAsync<AgentExecutionAuthorityMismatchException>(() => fixture.Resolver.ResolveAsync(
                request with { ObservedWorkspaceScope = scope }).AsTask());
        }
        Assert.Equal(0, fixture.Probe.CatalogReads);
    }

    [Theory]
    [InlineData(Curator.Prompts, false)]
    [InlineData(Curator.Prompts, true)]
    [InlineData(Curator.Workflows, false)]
    [InlineData(Curator.Workflows, true)]
    public async Task Original_profile_is_checked_before_and_after_catalog_observation(Curator curator, bool duringRead) {
        var fixture = new Fixture(curator);
        var request = fixture.Request();
        var ownerRequest = new AgentExecutionSourceAuthorityRequest(fixture.Agent, request.SourceKind, request.SourceId, null, fixture.Profile.Id);
        if (duringRead) {
            fixture.Probe.BeforeCatalogRead = () => fixture.Profile.Id = Guid.NewGuid();
        } else {
            fixture.Profile.Id = Guid.NewGuid();
        }
        await Assert.ThrowsAsync<AgentExecutionAuthorityMismatchException>(() => fixture.SourceOwner.ResolveAsync(ownerRequest).AsTask());
        Assert.Equal(duringRead ? 1 : 0, fixture.Probe.CatalogReads);
        Assert.Equal(duringRead ? 1 : 0, fixture.Probe.LeaseDisposals);
    }

    [Theory]
    [InlineData(Curator.Prompts)]
    [InlineData(Curator.Workflows)]
    public async Task Unpublished_positions_are_denied_and_unknown_source_is_still_readonly(Curator curator) {
        var fixture = new Fixture(curator);
        var request = fixture.Request();
        string[] invalid = ["other-position", "WORKFLOWS", "workflow:not-a-guid", "workflow:00000000-0000-0000-0000-000000000000",
            $"project:{Guid.NewGuid():D}:workflow:not-a-guid", $"workflow:{Guid.NewGuid():D}:extra"];
        foreach (var id in invalid) {
            await Assert.ThrowsAsync<AgentExecutionAuthorityMismatchException>(() => fixture.Resolver.ResolveAsync(
                request with { SourceId = new(id) }).AsTask());
        }
        var unknown = await fixture.Resolver.ResolveAsync(request with { SourceKind = new("unregistered-curator-source") });
        Assert.True(unknown.ReadAllowed);
        Assert.False(unknown.MutationAllowed);
        Assert.Equal(AgentExecutionAuthorityPolicyVersions.FailClosedSandbox, unknown.PolicyVersion);
        Assert.Equal(0, fixture.Probe.CatalogReads);
    }

    [Theory]
    [InlineData(AgentFrameworkWorkflowsChatView.Dashboard, false, false)]
    [InlineData(AgentFrameworkWorkflowsChatView.Workflows, false, false)]
    [InlineData(AgentFrameworkWorkflowsChatView.Editor, false, false)]
    [InlineData(AgentFrameworkWorkflowsChatView.History, false, false)]
    [InlineData(AgentFrameworkWorkflowsChatView.Analytics, false, false)]
    [InlineData(AgentFrameworkWorkflowsChatView.Editor, true, false)]
    [InlineData(AgentFrameworkWorkflowsChatView.Editor, true, true)]
    public async Task Every_shipped_workflow_position_remains_global_catalog_authority(AgentFrameworkWorkflowsChatView view, bool definition, bool project) {
        var fixture = new Fixture(Curator.Workflows);
        Assert.Equal("{}", fixture.Agent.ConfigurationJson);
        var surface = WorkflowSurface(view, definition, project);
        Assert.Null(surface.WorkspaceScope);
        var authority = await fixture.Resolver.ResolveAsync(fixture.Request(surface));
        Assert.True(authority.MutationAllowed);
        Assert.Equal(WorkspaceScopeDescriptor.Sandbox, authority.WorkspaceScope);
        Assert.Equal(fixture.Profile.Id, authority.DatabaseProfileId);
    }

    private static AgentChatContextSurface WorkflowSurface(AgentFrameworkWorkflowsChatView view, bool definition = false, bool project = false)
        => AgentFrameworkWorkflowsChatContextBuilder.Build(view, 0, definition ? new WorkflowId(Guid.NewGuid()) : null,
            null, null, null, false, 0, 0, 0, 0, selectedProject: project ? new(Guid.NewGuid(), "Position only") : null);

    [Theory]
    [InlineData(Curator.Prompts)]
    [InlineData(Curator.Workflows)]
    public async Task Source_decision_uses_current_agent_and_capabilities_from_one_released_catalog_lease(Curator curator) {
        var fixture = new Fixture(curator);
        var original = fixture.Agent;
        var request = fixture.Request();
        fixture.Probe.BeforeCatalogRead = () => fixture.Agent = original with {
            Permissions = original.Permissions with { CanUseTools = false }
        };
        var revoked = await fixture.Resolver.ResolveAsync(request);
        Assert.True(revoked.ReadAllowed);
        Assert.False(revoked.MutationAllowed);
        Assert.Equal(1, fixture.Probe.CatalogReads);
        Assert.Equal(1, fixture.Probe.LeaseDisposals);

        fixture.Probe.BeforeCatalogRead = null;
        fixture.Agent = original;
        var restored = await fixture.Resolver.ResolveAsync(request);
        Assert.True(restored.MutationAllowed);
        Assert.Equal(2, fixture.Probe.CatalogReads);
        Assert.Equal(2, fixture.Probe.LeaseDisposals);
    }

    private sealed class Fixture {
        private readonly IAgentFrameworkWorkspaceService workspace;
        private readonly AgentRuntimeToolProviderContext context;
        private readonly IAgentRuntimeToolProvider tools;
        private readonly Curator curator;
        public IAgentFrameworkWorkspaceService Workspace => workspace;
        public IAgentCatalogReadLeaseStore CatalogLeases => Probe;
        public AgentRuntimeToolProviderContext Context => context with { Agent = Agent, Capabilities = Catalog };
        public AgentDefinition Agent { get; set; }
        public IReadOnlyList<CapabilityCatalogItem> Catalog { get; set; }
        public IReadOnlyDictionary<string, string> Keys { get; }
        public IReadOnlyList<ToolCapabilityMetadata> Policies { get; }
        public ProfileAccessor Profile { get; } = new();
        public SourceWorkspaceProxy Probe { get; }
        public IAgentExecutionSourceAuthorityProvider SourceOwner { get; }
        public CanonicalAgentExecutionAuthorityResolver Resolver { get; }

        public Fixture(Curator curator) {
            this.curator = curator;
            Keys = curator == Curator.Prompts ? PromptsCuratorAgentCapabilityKeys.ToolNameToCapabilityKey : WorkflowCuratorAgentCapabilityKeys.ToolNameToCapabilityKey;
            Policies = (curator == Curator.Prompts ? PromptGalleryToolPolicy.Capabilities : WorkflowCuratorToolPolicy.Capabilities)
                .Where(item => Keys.ContainsKey(item.Name)).ToArray();
            var now = DateTimeOffset.UtcNow;
            Catalog = Keys.Values.Select(key => new CapabilityCatalogItem(Guid.NewGuid(), ModelCapabilityKind.Tool, key, key,
                string.Empty, string.Empty, string.Empty, CapabilityProofStatus.Verified, string.Empty, now, IsBuiltIn: true)).ToArray();
            var assignments = Catalog.Select(item => new AgentCapabilityAssignment(item.Id, item.Key, item.Kind,
                item.ProofStatus, item.LastVerifiedAtUtc, item.ProofNotes)).ToArray();
            var providerId = Guid.NewGuid();
            Agent = new AgentDefinition(curator == Curator.Prompts ? PromptsCuratorAgentIdentity.AgentId : WorkflowCuratorAgentIdentity.AgentId,
                "Managed curator", "Curator", "Source authority test", "Use assigned curator tools", AgentLifecycleStatus.Active,
                providerId, "gpt-5.4-mini", AgentWorkloadKind.Management, AgentChatHistoryMode.FrameworkManaged, 0.2,
                RequirePerServiceCallChatHistoryPersistence: false, EnableBackgroundResponses: false, "{}", IsTemplate: false,
                curator == Curator.Prompts ? PromptsCuratorAgentIdentity.TemplateKey : WorkflowCuratorAgentIdentity.TemplateKey,
                AgentPermissionsPolicy.Default with { CanUseTools = true }, assignments, [], now, now);
            var provider = new ProviderProfile(providerId, "Fixture", ProviderKind.OpenAi, "https://api.openai.com/v1", "OPENAI_API_KEY",
                Agent.Model, ProviderTransportKind.Responses, IsEnabled: true, SupportsStreaming: true, SupportsTools: true,
                PreferFrameworkManagedChatHistory: true, SupportsBackgroundResponses: false, ConfigurationJson: string.Empty,
                Notes: string.Empty, HealthStatus: string.Empty, LastCheckedAtUtc: null, SuggestedModels: [], ProviderProfilePurpose.Chat);
            workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, SourceWorkspaceProxy>();
            Probe = (SourceWorkspaceProxy)(object)workspace;
            Probe.ReadAgents = () => [Agent];
            Probe.ReadCatalog = () => Catalog;
            Probe.CatalogScope = WorkspaceScopeDescriptor.Organization(Profile.Id.ToString("N"));
            SourceOwner = curator == Curator.Prompts ? new PromptGalleryExecutionAuthorityProvider(Probe, Profile)
                : new WorkflowsExecutionAuthorityProvider(Probe, Profile);
            Resolver = CreateResolver([SourceOwner]);
            if (curator == Curator.Prompts) {
                var gallery = PromptGalleryTestSupport.CreateService(PromptGalleryTestSupport.CreateFactory(
                    $"{nameof(CuratorExecutionSourceAuthorityTests)}_{Guid.NewGuid():N}"));
                tools = new PromptsCuratorAgentRuntimeToolProvider(gallery, new PromptsCuratorAgentRuntimeAuthorizationService(workspace));
            } else {
                var catalog = new InMemoryWorkflowCatalogService(new InMemoryWorkflowCatalogStore(), new WorkflowDefinitionValidator());
                tools = new WorkflowCuratorAgentRuntimeToolProvider(catalog, catalog, catalog, WorkflowExecutorCatalog.FromDescriptors([]),
                    new WorkflowRuntimeBackendCatalog([WorkflowRuntimeBackendKind.InProcess]), new WorkflowCuratorAgentRuntimeAuthorizationService(workspace));
            }
            context = new(Agent, provider, Catalog, SuppressApprovalRequirements: false, AgentRuntimeToolProviderPurpose.InteractiveChat,
                "curator-source-test", AgentRuntimeContextIntent.Empty, new Dictionary<string, string>());
        }

        public CanonicalAgentExecutionAuthorityResolver CreateResolver(IEnumerable<IAgentExecutionSourceAuthorityProvider> owners)
            => new(new WorkspaceFactory(workspace), Profile, new FixedAgentExecutionProfileGenerationSource(new(1)), TimeProvider.System, owners);

        public AgentExecutionAuthorityResolutionRequest Request(AgentChatContextSurface? surface = null) {
            surface ??= curator == Curator.Prompts ? PromptGalleryAgentChatContextBuilder.Build() : WorkflowSurface(AgentFrameworkWorkflowsChatView.Workflows);
            return new(Agent.Id, surface.Source.Kind, surface.Source.Id, surface.WorkspaceScope, new(1), surface.AgentAccess.Single());
        }

        public Task RequireToolAsync(string toolName) => curator == Curator.Prompts
            ? new PromptsCuratorAgentRuntimeAuthorizationService(workspace).EnsureToolInvocationAuthorizedAsync(Agent.Id, toolName, default)
            : new WorkflowCuratorAgentRuntimeAuthorizationService(workspace).EnsureToolInvocationAuthorizedAsync(Agent.Id, toolName, default);

        public async Task<RuntimeCapabilityState> ComposeAsync(AgentExecutionAuthorityRecord authority, IAgentRuntimeToolProvider? provider = null) {
            var composer = new RuntimeToolProviderComposer(new RuntimeToolProviderAccessFilter(ProductToolPolicies), ProductToolPolicies);
            var access = new RuntimeCapabilityAccessPlan(new EffectiveCapabilitySet([], []), AllowedCatalogCapabilities: [], [],
                new CapabilityAccessPolicyEvaluator(), InitialAllowedCapabilities: [], InitialDiagnostics: [],
                CatalogCapabilitiesByIdentity: new Dictionary<CapabilityIdentity, CapabilityCatalogItem>(),
                DescriptorsByKey: new Dictionary<string, CapabilityExposureDescriptor>(StringComparer.OrdinalIgnoreCase), CorrelationId: "curator-source-test");
            var state = new RuntimeCapabilityState();
            await composer.AttachAsync(new RuntimeToolProviderAttachmentRequest(state, access, composer.ComposeRegistrations([provider ?? tools]),
                context with { Agent = Agent, Capabilities = Catalog }, SuppressApprovalRequirements: false), default);
            await MafRuntimeAgentFactory.FilterToolsOutsideExecutionGovernanceAsync(state,
                AgentExecutionGovernanceSnapshot.FromAuthority(authority), (_, _, _) => Task.CompletedTask);
            return state;
        }
    }

    private class SourceWorkspaceProxy : DispatchProxy, IAgentCatalogReadLeaseStore {
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
            if (targetMethod?.Name is "add_ExecutionUpdated" or "remove_ExecutionUpdated") {
                return null;
            }
            if (targetMethod?.Name == nameof(IAgentFrameworkWorkspaceService.GetOrCreateChatSessionAsync)) {
                var agentId = Assert.IsType<Guid>(args![0]);
                Assert.Equal(ReadAgents().Single().Id, agentId);
                return Task.FromResult(new ChatSessionRecord(Guid.NewGuid(), agentId, "Ordinary surface read",
                    DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, Messages: []));
            }
            if (targetMethod?.Name == nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync)) {
                return Task.FromResult(ReadAgents());
            }
            if (targetMethod?.Name == nameof(IAgentFrameworkWorkspaceService.ListCapabilitiesAsync)) {
                CatalogReads++;
                BeforeCatalogRead?.Invoke();
                return Task.FromResult(ReadCatalog());
            }
            throw new InvalidOperationException($"Unexpected curator source access: {targetMethod?.Name}.");
        }
    }

    private sealed class WorkspaceFactory(IAgentFrameworkWorkspaceService workspace) : ICanDoItAllAgentWorkspaceFactory {
        public IAgentFrameworkWorkspaceService GetOrganizationWorkspaceService() => workspace;
        public IAgentFrameworkWorkspaceService GetWorkspaceService(WorkspaceScopeDescriptor scope) => throw new NotSupportedException();
        public WorkspaceScopeDescriptor GetOrganizationScope() => throw new NotSupportedException();
        public string GetWorkspaceRoot() => throw new NotSupportedException();
    }

    private sealed class ProfileAccessor : IDatabaseProfileRuntimeAccessor {
        public Guid Id { get; set; } = Guid.NewGuid();
        public ResolvedDatabaseProfile ResolveCurrentProfile() => new(new DatabaseProfileRecord {
            Id = Id, DisplayName = "Curator source test profile", ProviderKind = DatabaseProviderKind.InMemory, SourceKind = DatabaseProfileSourceKind.InMemory
        }, DatabaseProfileResolutionSource.ExplicitOverride, "test");
        public ResolvedDatabaseProfile ResolveProfile(Guid profileId) => throw new NotSupportedException();
    }
}
