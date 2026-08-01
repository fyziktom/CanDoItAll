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

namespace CanDoItAll.Tests.Unit;

public sealed class HrAgentRuntimeToolProviderTests
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
        Assert.Equal(AgentToolInvocationPolicyMetadata.HrAgentsSearch, tool.Name);
        Assert.DoesNotContain(
            tools,
            item => string.Equals(
                item.Name,
                AgentToolInvocationPolicyMetadata.HrAgentSettingsGet,
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
