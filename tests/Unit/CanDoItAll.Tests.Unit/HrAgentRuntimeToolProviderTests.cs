using System.Reflection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed partial class HrAgentRuntimeToolProviderTests
{
    [Fact]
    public async Task CreateToolsAsync_rejects_id_and_template_spoofs()
    {
        var provider = CreateProvider();
        var capabilityKeys = HrAgentCapabilityKeys.ToolNameToCapabilityKey.Values.ToArray();
        var wrongId = CreateContext(
            capabilityKeys,
            agentId: Guid.NewGuid(),
            templateKey: HrAgentIdentity.TemplateKey,
            allowCrmScope: true);
        var wrongTemplate = CreateContext(
            capabilityKeys,
            agentId: HrAgentIdentity.AgentId,
            templateKey: "hr-agent-spoof",
            allowCrmScope: true);

        var wrongIdTools = await provider.CreateToolsAsync(wrongId, CancellationToken.None);
        var wrongTemplateTools = await provider.CreateToolsAsync(wrongTemplate, CancellationToken.None);

        Assert.Empty(wrongIdTools);
        Assert.Empty(wrongTemplateTools);
        Assert.Empty(provider.GetToolMetadata(wrongId));
        Assert.Empty(provider.GetToolMetadata(wrongTemplate));
    }

    [Fact]
    public async Task CreateToolsAsync_exposes_only_exact_assigned_capability_keys()
    {
        var provider = CreateProvider();
        var context = CreateContext([HrAgentCapabilityKeys.AgentsSearch]);

        var tools = await provider.CreateToolsAsync(context, CancellationToken.None);

        var tool = Assert.Single(tools);
        Assert.Equal(HrAgentToolPolicy.HrAgentsSearch, tool.Name);
        Assert.DoesNotContain(
            tools,
            item => string.Equals(
                item.Name,
                HrAgentToolPolicy.HrAgentSettingsGet,
                StringComparison.Ordinal));

        var wrongCaseContext = CreateContext([HrAgentCapabilityKeys.AgentsSearch.ToUpperInvariant()]);
        Assert.Empty(await provider.CreateToolsAsync(wrongCaseContext, CancellationToken.None));
    }

    [Fact]
    public async Task CreateToolsAsync_denies_crm_tools_without_crm_memory_scope()
    {
        var provider = CreateProvider();
        var context = CreateContext(
            [HrAgentCapabilityKeys.CrmSearch, HrAgentCapabilityKeys.CrmItemSummaryGet],
            allowCrmScope: false);

        var tools = await provider.CreateToolsAsync(context, CancellationToken.None);

        Assert.Empty(tools);
        Assert.Empty(provider.GetToolMetadata(context));
    }

    [Fact]
    public async Task CreateToolsAsync_exposes_all_assigned_tools_for_valid_hr_agent()
    {
        var provider = CreateProvider();
        var expectedNames = HrAgentCapabilityKeys.ToolNameToCapabilityKey.Keys
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        var context = CreateContext(
            HrAgentCapabilityKeys.ToolNameToCapabilityKey.Values,
            allowCrmScope: true);

        var tools = await provider.CreateToolsAsync(context, CancellationToken.None);
        var metadata = provider.GetToolMetadata(context);

        Assert.Equal(expectedNames, tools.Select(tool => tool.Name).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal(expectedNames, metadata.Select(item => item.ToolName).OrderBy(name => name, StringComparer.Ordinal));
        Assert.Equal(
            [AgentRuntimeToolProviderPurpose.InteractiveChat],
            provider.Descriptor.SupportedPurposes);
        Assert.All(
            metadata.Where(item => item.OperationKind == AgentRuntimeToolOperationKind.Mutation),
            item => Assert.True(item.RequiresApprovalByDefault));
    }

    [Theory]
    [InlineData(AgentRuntimeToolProviderPurpose.GovernedProcessAutomation)]
    [InlineData(AgentRuntimeToolProviderPurpose.AutoApprovedNonInteractive)]
    [InlineData(AgentRuntimeToolProviderPurpose.A2AEndpoint)]
    public async Task CreateToolsAsync_never_exposes_hr_tools_outside_interactive_chat(
        AgentRuntimeToolProviderPurpose purpose)
    {
        var provider = CreateProvider();
        var context = CreateContext(
            HrAgentCapabilityKeys.ToolNameToCapabilityKey.Values,
            allowCrmScope: true,
            purpose: purpose);

        var tools = await provider.CreateToolsAsync(context, CancellationToken.None);

        Assert.Empty(tools);
        Assert.Empty(provider.GetToolMetadata(context));
    }

    [Fact]
    public async Task CreateToolsAsync_requires_agent_tool_permission()
    {
        var provider = CreateProvider();
        var context = CreateContext(
            HrAgentCapabilityKeys.ToolNameToCapabilityKey.Values,
            allowCrmScope: true,
            canUseTools: false);

        var tools = await provider.CreateToolsAsync(context, CancellationToken.None);

        Assert.Empty(tools);
        Assert.Empty(provider.GetToolMetadata(context));
    }

    [Theory]
    [InlineData(AgentLifecycleStatus.Draft)]
    [InlineData(AgentLifecycleStatus.Suspended)]
    [InlineData(AgentLifecycleStatus.Archived)]
    public async Task CreateToolsAsync_requires_active_lifecycle(
        AgentLifecycleStatus status)
    {
        var provider = CreateProvider();
        var context = CreateContext(
            HrAgentCapabilityKeys.ToolNameToCapabilityKey.Values,
            allowCrmScope: true,
            status: status);

        var tools = await provider.CreateToolsAsync(context, CancellationToken.None);

        Assert.Empty(tools);
        Assert.Empty(provider.GetToolMetadata(context));
    }

    [Fact]
    public async Task CreateToolsAsync_rejects_template_identity()
    {
        var provider = CreateProvider();
        var context = CreateContext(
            HrAgentCapabilityKeys.ToolNameToCapabilityKey.Values,
            allowCrmScope: true,
            isTemplate: true);

        var tools = await provider.CreateToolsAsync(context, CancellationToken.None);

        Assert.Empty(tools);
        Assert.Empty(provider.GetToolMetadata(context));
    }

    [Fact]
    public void AddAgentFrameworkModule_registers_and_resolves_hr_provider_as_scoped()
    {
        var services = new ServiceCollection();
        services.AddAgentFrameworkModule(new ConfigurationBuilder().Build());

        AssertScopedRegistration<HrAgentAdministrationService>(services);
        AssertScopedRegistration<HrAgentAvatarGenerationService>(services);
        AssertScopedRegistration<HrAgentUsageAnalyticsService>(services);
        AssertScopedRegistration<HrAgentProcessReviewService>(services);
        AssertScopedRegistration<HrAgentRuntimeAuthorizationService>(services);
        var providerDescriptor = Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IAgentRuntimeToolProvider) &&
                descriptor.ImplementationType == typeof(HrAgentRuntimeToolProvider));
        Assert.Equal(ServiceLifetime.Scoped, providerDescriptor.Lifetime);

        foreach (var descriptor in services
                     .Where(descriptor =>
                         descriptor.ServiceType == typeof(IAgentRuntimeToolProvider) &&
                         descriptor != providerDescriptor)
                     .ToArray())
        {
            services.Remove(descriptor);
        }

        services.Replace(ServiceDescriptor.Scoped(_ => CreateUninitialized<HrAgentAdministrationService>()));
        services.Replace(ServiceDescriptor.Scoped(_ => CreateUninitialized<HrAgentAvatarGenerationService>()));
        services.Replace(ServiceDescriptor.Scoped(_ => CreateUninitialized<HrAgentUsageAnalyticsService>()));
        services.Replace(ServiceDescriptor.Scoped(_ => CreateUninitialized<HrAgentProcessReviewService>()));
        services.Replace(ServiceDescriptor.Scoped(_ => CreateUninitialized<HrAgentRuntimeAuthorizationService>()));
        services.Replace(ServiceDescriptor.Scoped<ICrmHrAgentQueryService>(_ => new ThrowingCrmHrAgentQueryService()));
        services.Replace(ServiceDescriptor.Scoped<ICrmPartyCommandService>(_ => new ThrowingCrmPartyCommandService()));
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var provider = Assert.Single(scope.ServiceProvider.GetServices<IAgentRuntimeToolProvider>());

        Assert.IsType<HrAgentRuntimeToolProvider>(provider);
    }

    [Theory]
    [InlineData(HrAgentToolPolicy.HrAgentCreate, HrAgentToolPolicy.HrAgentsSearch)]
    [InlineData(HrAgentToolPolicy.HrAgentSettingsUpdate, HrAgentToolPolicy.HrAgentsSearch)]
    [InlineData(HrAgentToolPolicy.HrAgentAvatarGenerate, HrAgentToolPolicy.HrAgentSettingsGet)]
    [InlineData(HrAgentToolPolicy.HrAgentProcessManagerReviewRequest, HrAgentToolPolicy.HrAgentProcessManagerReviewRequest)]
    public async Task Saved_HR_acknowledgements_require_the_current_matching_disclosure_capability(string toolName, string readTool) {
        var context = CreateContext(HrAgentCapabilityKeys.ToolNameToCapabilityKey.Values, allowCrmScope: true);
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, DisclosureWorkspace>();
        var state = (DisclosureWorkspace)(object)workspace;
        state.Capabilities = context.Capabilities;
        var provider = CreateDisclosureProvider(workspace, new ThrowingCrmHrAgentQueryService(), new ThrowingCrmPartyCommandService());
        var metadata = provider.GetToolMetadata(context).Single(item => item.ToolName == toolName);
        var authorize = metadata.AuthorizeResultDisclosureAsync!;
        Assert.NotNull(authorize);
        var saved = ManagedToolDisclosureTestData.Create(metadata);
        var allowed = context.Agent with { Capabilities = context.Agent.Capabilities.Where(item =>
            item.CapabilityKey == HrAgentCapabilityKeys.ToolNameToCapabilityKey[readTool]).ToArray() };
        state.Agents = [allowed];
        await using (var lease = await authorize(saved, default)) {
            Assert.Null(lease);
        }
        state.Agents = [allowed with { Capabilities = [] }];
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => authorize(saved, default).AsTask());
        state.Agents = [allowed];
        await using (var lease = await authorize(saved, default)) {
            Assert.Null(lease);
        }
        Assert.Equal(AgentToolEffectState.Unknown, saved.EffectState);
    }

    [Theory]
    [InlineData(HrAgentToolPolicy.HrCrmSearch)]
    [InlineData(HrAgentToolPolicy.HrCrmItemSummaryGet)]
    [InlineData(HrAgentToolPolicy.HrCrmPartyCreate)]
    public async Task Saved_CRM_data_is_denied_after_privacy_or_scope_revocation_and_remains_available_after_restore(string toolName) {
        var context = CreateContext(HrAgentCapabilityKeys.ToolNameToCapabilityKey.Values, allowCrmScope: true);
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, DisclosureWorkspace>();
        var state = (DisclosureWorkspace)(object)workspace;
        state.Agents = [context.Agent];
        state.Capabilities = context.Capabilities;
        var item = new CrmHrAgentQueryItem(Guid.NewGuid(), CrmHrAgentRecordKind.Party, "Original private name", new(),
            "Original safe summary", [], null, CrmHrAgentRedactionState.None, CrmHrAgentBusinessTextTrust.UntrustedBusinessData);
        var owner = new DisclosureCrmQuery(item);
        var provider = CreateDisclosureProvider(workspace, owner, new ThrowingCrmPartyCommandService());
        var metadata = provider.GetToolMetadata(context).Single(value => value.ToolName == toolName);
        object result = toolName switch {
            HrAgentToolPolicy.HrCrmSearch => new[] { item },
            HrAgentToolPolicy.HrCrmPartyCreate => new CrmPartyCreateResult(item.Id, PartyType.Person,
                PartyLifecycleStatus.Active, item.DisplayLabel, string.Empty, []),
            _ => item
        };
        var saved = ManagedToolDisclosureTestData.Create(metadata, result: result);
        var authorize = metadata.AuthorizeResultDisclosureAsync!;
        Assert.NotNull(authorize);
        await using (var lease = await authorize(saved, default)) {
            Assert.Null(lease);
        }
        owner.Item = item with { RedactionState = CrmHrAgentRedactionState.SensitiveRecordRedacted };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => authorize(saved, default).AsTask());
        owner.Item = item;
        state.Agents = [context.Agent with { ConfigurationJson = "{}" }];
        var readsBeforeDeniedScope = owner.Reads;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => authorize(saved, default).AsTask());
        Assert.Equal(readsBeforeDeniedScope, owner.Reads);
        state.Agents = [context.Agent];
        await using (var lease = await authorize(saved, default)) {
            Assert.Null(lease);
        }
        Assert.Contains(item.DisplayLabel, saved.Result.GetRawText(), StringComparison.Ordinal);
        Assert.Equal(AgentToolEffectState.Unknown, saved.EffectState);
    }

    [Theory]
    [InlineData(HrAgentToolPolicy.HrCrmPartyAffiliationsList)]
    [InlineData(HrAgentToolPolicy.HrCrmAffiliationUpsert)]
    public async Task Saved_affiliations_recheck_all_current_visible_endpoints_without_upserting(string toolName) {
        var context = CreateContext(HrAgentCapabilityKeys.ToolNameToCapabilityKey.Values, allowCrmScope: true);
        var workspace = DispatchProxy.Create<IAgentFrameworkWorkspaceService, DisclosureWorkspace>();
        var state = (DisclosureWorkspace)(object)workspace;
        state.Agents = [context.Agent];
        state.Capabilities = context.Capabilities;
        var item = new CrmPartyAffiliationResult(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Organization",
            PartyOrganizationAffiliationKind.Employee, false, "Role", null, null, null, null, true, DateTimeOffset.UtcNow);
        var owner = new DisclosureAffiliations { Items = [item] };
        var provider = CreateDisclosureProvider(workspace, new ThrowingCrmHrAgentQueryService(), owner);
        var metadata = provider.GetToolMetadata(context).Single(value => value.ToolName == toolName);
        var saved = ManagedToolDisclosureTestData.Create(metadata, new HrCrmPersonPartyInput(item.PersonPartyId),
            toolName == HrAgentToolPolicy.HrCrmAffiliationUpsert ? item : new[] { item });
        var authorize = metadata.AuthorizeResultDisclosureAsync!;
        Assert.NotNull(authorize);
        await using (var lease = await authorize(saved, default)) {
            Assert.Null(lease);
        }
        owner.Items = [];
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => authorize(saved, default).AsTask());
        owner.Items = [item with { OrganizationPartyId = Guid.NewGuid() }];
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => authorize(saved, default).AsTask());
        owner.Items = [item];
        await using (var lease = await authorize(saved, default)) {
            Assert.Null(lease);
        }
    }

    private static HrAgentRuntimeToolProvider CreateDisclosureProvider(IAgentFrameworkWorkspaceService workspace,
        ICrmHrAgentQueryService query, ICrmPartyCommandService commands) => new(
            CreateUninitialized<HrAgentAdministrationService>(), CreateUninitialized<HrAgentAvatarGenerationService>(),
            CreateUninitialized<HrAgentUsageAnalyticsService>(), CreateUninitialized<HrAgentProcessReviewService>(),
            query, commands, new HrAgentRuntimeAuthorizationService(workspace));

    private class DisclosureWorkspace : DispatchProxy {
        public IReadOnlyList<AgentDefinition> Agents { get; set; } = [];
        public IReadOnlyList<CapabilityCatalogItem> Capabilities { get; set; } = [];
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch {
            nameof(IAgentFrameworkWorkspaceService.ListAgentsAsync) => Task.FromResult(Agents),
            nameof(IAgentFrameworkWorkspaceService.ListCapabilitiesAsync) => Task.FromResult(Capabilities),
            _ => throw new InvalidOperationException("Disclosure must not call an agent writer or runtime.")
        };
    }

    private sealed class DisclosureCrmQuery(CrmHrAgentQueryItem item) : ICrmHrAgentQueryService {
        public CrmHrAgentQueryItem Item { get; set; } = item;
        public int Reads { get; private set; }
        public Task<Result<IReadOnlyList<CrmHrAgentQueryItem>>> SearchAsync(CrmHrAgentSearchQuery query,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("Disclosure must use exact saved resource identities.");
        public Task<Result<CrmHrAgentQueryItem>> GetSummaryAsync(CrmHrAgentItemReference reference,
            CancellationToken cancellationToken = default) {
            Assert.Equal(Item.Id, reference.Id);
            Assert.Equal(Item.RecordKind, reference.RecordKind);
            Reads++;
            return Task.FromResult(Result<CrmHrAgentQueryItem>.Success(Item));
        }
    }

    private sealed class DisclosureAffiliations : ICrmPartyCommandService {
        public IReadOnlyList<CrmPartyAffiliationResult> Items { get; set; } = [];
        public Task<Result<CrmPartyCreateResult>> CreatePartyAsync(CrmPartyCreateCommand command, string actor,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("Disclosure cannot create a party.");
        public Task<Result<IReadOnlyList<CrmPartyAffiliationResult>>> ListAffiliationsAsync(Guid personPartyId,
            CancellationToken cancellationToken = default) => Task.FromResult(Result<IReadOnlyList<CrmPartyAffiliationResult>>.Success(Items));
        public Task<Result<CrmPartyAffiliationResult>> UpsertAffiliationAsync(CrmPartyAffiliationUpsertCommand command,
            string actor, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Disclosure cannot change an affiliation.");
    }

    private static HrAgentRuntimeToolProvider CreateProvider()
    {
        return new HrAgentRuntimeToolProvider(
            CreateUninitialized<HrAgentAdministrationService>(),
            CreateUninitialized<HrAgentAvatarGenerationService>(),
            CreateUninitialized<HrAgentUsageAnalyticsService>(),
            CreateUninitialized<HrAgentProcessReviewService>(),
            new ThrowingCrmHrAgentQueryService(),
            new ThrowingCrmPartyCommandService(),
            CreateUninitialized<HrAgentRuntimeAuthorizationService>());
    }

    private static AgentRuntimeToolProviderContext CreateContext(
        IEnumerable<string> capabilityKeys,
        Guid? agentId = null,
        string? templateKey = null,
        bool allowCrmScope = false,
        AgentRuntimeToolProviderPurpose purpose = AgentRuntimeToolProviderPurpose.InteractiveChat,
        bool canUseTools = true,
        AgentLifecycleStatus status = AgentLifecycleStatus.Active,
        bool isTemplate = false)
    {
        var now = DateTimeOffset.UtcNow;
        var capabilities = capabilityKeys
            .Select(key => new CapabilityCatalogItem(
                Guid.NewGuid(),
                CapabilityKind.Tool,
                key,
                key,
                string.Empty,
                string.Empty,
                string.Empty,
                CapabilityProofStatus.Verified,
                string.Empty,
                now,
                IsBuiltIn: true))
            .ToArray();
        var assignments = capabilities
            .Select(capability => new AgentCapabilityAssignment(
                capability.Id,
                capability.Key,
                capability.Kind,
                capability.ProofStatus,
                capability.LastVerifiedAtUtc,
                capability.ProofNotes))
            .ToArray();
        var configurationJson = allowCrmScope
            ? AgentMemoryAccessMetadata.Write(
                "{}",
                new AgentMemoryAccessSettings
                {
                    AllowedSourceScopes = [MemorySourceScope.Crm]
                })
            : "{}";
        var agent = new AgentDefinition(
            agentId ?? HrAgentIdentity.AgentId,
            "HR Agent",
            "Agent governance",
            "Manages and reviews agents.",
            "Use governed HR tools.",
            status,
            Guid.NewGuid(),
            "gpt-5-mini",
            AgentWorkloadKind.Hr,
            AgentChatHistoryMode.FrameworkManaged,
            0.2d,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            configurationJson,
            IsTemplate: isTemplate,
            templateKey ?? HrAgentIdentity.TemplateKey,
            AgentPermissionsPolicy.Default with { CanUseTools = canUseTools },
            assignments,
            [],
            now,
            now);
        var provider = new ProviderProfile(
            agent.ProviderProfileId!.Value,
            "Chat provider",
            ProviderKind.OpenAi,
            "https://api.openai.com",
            "OPENAI_API_KEY",
            agent.Model,
            ProviderTransportKind.Responses,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: string.Empty,
            Notes: string.Empty,
            HealthStatus: string.Empty,
            LastCheckedAtUtc: null,
            SuggestedModels: [],
            ProviderProfilePurpose.Chat);

        return new AgentRuntimeToolProviderContext(
            agent,
            provider,
            capabilities,
            SuppressApprovalRequirements: false,
            purpose,
            RuntimeSessionKey: "hr-provider-test",
            AgentRuntimeContextIntent.Empty,
            Tags: new Dictionary<string, string>());
    }

    private static T CreateUninitialized<T>() where T : class
    {
        return (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    }

    private static void AssertScopedRegistration<T>(IEnumerable<ServiceDescriptor> services)
    {
        var descriptor = Assert.Single(
            services,
            item =>
                item.ServiceType == typeof(T) &&
                item.ImplementationType == typeof(T));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    private sealed class ThrowingCrmHrAgentQueryService : ICrmHrAgentQueryService
    {
        public Task<Result<IReadOnlyList<CrmHrAgentQueryItem>>> SearchAsync(
            CrmHrAgentSearchQuery query,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("This gating test does not invoke CRM/HR services.");
        }

        public Task<Result<CrmHrAgentQueryItem>> GetSummaryAsync(
            CrmHrAgentItemReference reference,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("This gating test does not invoke CRM/HR services.");
        }
    }

    private sealed class ThrowingCrmPartyCommandService : ICrmPartyCommandService
    {
        public Task<Result<CrmPartyCreateResult>> CreatePartyAsync(
            CrmPartyCreateCommand command,
            string actor,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "This gating test does not invoke CRM commands.");
        }

        public Task<Result<IReadOnlyList<CrmPartyAffiliationResult>>>
            ListAffiliationsAsync(
                Guid personPartyId,
                CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "This gating test does not invoke CRM commands.");
        }

        public Task<Result<CrmPartyAffiliationResult>> UpsertAffiliationAsync(
            CrmPartyAffiliationUpsertCommand command,
            string actor,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException(
                "This gating test does not invoke CRM commands.");
        }
    }
}
