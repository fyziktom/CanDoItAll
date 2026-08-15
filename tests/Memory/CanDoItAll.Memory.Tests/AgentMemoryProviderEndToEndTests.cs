using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Memory.Context;
using CanDoItAll.AgentFramework.Memory.DependencyInjection;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Mock;
using CanDoItAll.Memory.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Memory.Tests.Providers;

public sealed class AgentMemoryProviderEndToEndTests
{
    private static readonly MemoryProviderInstanceId BusinessProviderId =
        MemoryProviderInstanceId.Parse("provider.e2e.business");
    private static readonly MemoryProviderInstanceId ProgrammingProviderId =
        MemoryProviderInstanceId.Parse("provider.e2e.programming");

    [Fact]
    public async Task Agent_context_routes_modes_through_real_handler_driver_and_ledger()
    {
        using var rootProvider = CreateServiceProvider();
        using var scope = rootProvider.CreateScope();
        var services = scope.ServiceProvider;
        await SeedProvidersAsync(services);

        var contributor = services.GetServices<IAgentContextContributor>()
            .OfType<MemoryAgentContextContributor>()
            .Single();
        var driver = services.GetRequiredService<DeterministicMockMemoryProviderDriver>();
        var ledger = services.GetRequiredService<IMemoryOperationLedgerStore>();
        var automaticSettings = CreateSettings(AgentMemoryInvocationMode.Automatic);

        var automatic = await contributor.ContributeAsync(CreateRequest(
            CreateAgent(automaticSettings),
            "recall architecture"));

        Assert.Equal(AgentContextContributionStatus.Provided, automatic.Status);
        Assert.Equal(2, driver.DispatchCount);
        var automaticMessage = Assert.Single(automatic.Messages).Text;
        Assert.True(
            automaticMessage.IndexOf("Memory provider 'business-memory'", StringComparison.Ordinal) <
            automaticMessage.IndexOf("Memory provider 'programming-memory'", StringComparison.Ordinal));

        var explicitSettings = CreateSettings(AgentMemoryInvocationMode.ExplicitDirective);
        var explicitResult = await contributor.ContributeAsync(CreateRequest(
            CreateAgent(explicitSettings),
            "/mem:programming-memory /mem:business-memory recall customer decision"));

        Assert.Equal(AgentContextContributionStatus.Provided, explicitResult.Status);
        Assert.Equal(4, driver.DispatchCount);
        var explicitMessage = Assert.Single(explicitResult.Messages).Text;
        Assert.True(
            explicitMessage.IndexOf("Memory provider 'business-memory'", StringComparison.Ordinal) <
            explicitMessage.IndexOf("Memory provider 'programming-memory'", StringComparison.Ordinal));
        Assert.Equal(
            "recall customer decision",
            Assert.Single(explicitResult.RequestMessageTransformation?.TextReplacements ?? []).Text);

        var businessOperations = await ledger.ListByProviderAsync(BusinessProviderId);
        var programmingOperations = await ledger.ListByProviderAsync(ProgrammingProviderId);
        Assert.Equal(2, businessOperations.Count);
        Assert.Equal(2, programmingOperations.Count);
        Assert.All(
            businessOperations.Concat(programmingOperations),
            operation => Assert.Equal(MemoryLedgerStatus.Completed, operation.Status));

        var unknownAlias = await contributor.ContributeAsync(CreateRequest(
            CreateAgent(explicitSettings),
            "/mem:missing recall customer decision"));

        Assert.Equal(AgentContextContributionStatus.Failed, unknownAlias.Status);
        Assert.Equal(4, driver.DispatchCount);
        Assert.Equal(2, (await ledger.ListByProviderAsync(BusinessProviderId)).Count);
        Assert.Equal(2, (await ledger.ListByProviderAsync(ProgrammingProviderId)).Count);
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseInMemoryDatabase($"agent-memory-e2e-{Guid.NewGuid():N}"));
        services.AddDeterministicMockMemoryProviderDriver();
        services.AddGenericMemoryModule();
        services.AddAgentFrameworkMemory();
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static async Task SeedProvidersAsync(IServiceProvider services)
    {
        var store = services.GetRequiredService<IMemoryProviderProfileStore>();
        var now = DateTimeOffset.UtcNow;
        await store.UpsertAsync(CreateProfile(BusinessProviderId, "Business memory"), now);
        await store.UpsertAsync(CreateProfile(ProgrammingProviderId, "Programming memory"), now);
    }

    private static MemoryProviderProfile CreateProfile(
        MemoryProviderInstanceId providerId,
        string displayName)
    {
        return new MemoryProviderProfile(
            providerId,
            displayName,
            MemoryProviderDriverKind.Mock,
            IsEnabled: true,
            MemoryProviderHealthState.Healthy,
            MemoryProviderWorkspaceScope.AllWorkspaces,
            SelectionTags: [],
            MemoryProviderProfilePolicy.Default,
            new MemoryProviderManifest(
                MemoryProviderKind.Parse("memory.mock.e2e"),
                MemoryProtocolVersion.Current,
                [new MemoryCapabilityDescriptor(MemoryCapabilityIds.ContextQuerySync, "1", Supported: true)],
                MemoryProviderInteractionSupport.SyncQueryOnly,
                UiSurfaces: [],
                MemoryProviderLimits.Default,
                MemoryExtensionData.Empty));
    }

    private static AgentMemoryAccessSettings CreateSettings(AgentMemoryInvocationMode mode)
    {
        var bindings = new[]
        {
            new AgentMemoryProviderBindingSetting(
                AgentMemoryProviderAlias.Parse("business-memory"),
                BusinessProviderId,
                IncludeInAutomaticContext: true,
                AgentMemoryProviderRequirement.Optional),
            new AgentMemoryProviderBindingSetting(
                AgentMemoryProviderAlias.Parse("programming-memory"),
                ProgrammingProviderId,
                IncludeInAutomaticContext: true,
                AgentMemoryProviderRequirement.Required)
        };
        return new AgentMemoryAccessSettings
        {
            InvocationMode = mode,
            ProviderBindings = bindings,
            AllowedProviderInstanceIds = bindings.Select(binding => binding.ProviderInstanceId).ToArray(),
            AllowedCapabilityIds = [MemoryCapabilityIds.ContextQuerySync]
        };
    }

    private static AgentDefinition CreateAgent(AgentMemoryAccessSettings settings)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Guid.NewGuid(),
            "Memory E2E agent",
            "Memory tester",
            "Exercises the production memory seam.",
            "Use configured memory deliberately.",
            AgentLifecycleStatus.Active,
            Guid.NewGuid(),
            "gpt-5-mini",
            AgentWorkloadKind.Programming,
            AgentChatHistoryMode.ProviderDefault,
            0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            AgentMemoryAccessMetadata.Write("{}", settings),
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: [],
            now,
            now);
    }

    private static AgentContextContributionRequest CreateRequest(
        AgentDefinition agent,
        string prompt)
    {
        return new AgentContextContributionRequest(
            agent,
            CreateChatProvider(),
            [new AgentContextRequestMessage(AgentContextMessageRole.User, prompt)],
            new AgentContextContributionPolicy(
                AgentContextExecutionMode.InteractiveChat,
                SuppressApprovalRequirements: false,
                WorkspaceScopeDescriptor.Project("project-e2e")))
        {
            ContextIntent = AgentRuntimeContextIntent.Empty with
            {
                SourceKind = "chat-session",
                SourceId = "session-e2e",
                WorkspaceScope = WorkspaceScopeDescriptor.Project("project-e2e")
            }
        };
    }

    private static ProviderProfile CreateChatProvider()
    {
        return new ProviderProfile(
            Guid.NewGuid(),
            "Chat provider",
            ProviderKind.OpenAi,
            "https://api.openai.com",
            "OPENAI_API_KEY",
            "gpt-5-mini",
            ProviderTransportKind.ChatCompletions,
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
    }
}
