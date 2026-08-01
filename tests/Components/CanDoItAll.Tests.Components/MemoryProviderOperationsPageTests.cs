using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Mock;
using CanDoItAll.Memory.Persistence;
using CanDoItAll.Modules.Memory;
using CanDoItAll.Modules.Memory.Pages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class MemoryProviderOperationsPageTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-05T20:30:00Z");

    [Fact]
    public async Task MemoryProvidersPage_RunsSyncQueryWithoutAdvertisingUnsupportedFeedback()
    {
        var setup = await CreateRuntimeContextAsync(
            enableDeterministicMockDriver: true,
            CreateProviderProfile(
                "provider.sync",
                "Synchronous memory",
                MemoryProviderHealthState.Healthy,
                MemoryCapabilityIds.ContextQuerySync,
                MemoryCapabilityIds.FeedbackImmediate));
        using var context = setup.Context;
        var cut = context.Render<MemoryProvidersPage>();

        cut.WaitForElement("[data-testid='memory-ui-provider-list']");
        cut.Find("[data-testid='memory-ui-tab-query']").Click();
        cut.WaitForElement("[data-testid='memory-ui-query-text']").Change("payment integration");
        cut.Find("[data-testid='memory-ui-query-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Mock memory context for payment integration", cut.Markup);
            Assert.Contains("Deterministic mock memory", cut.Markup);
            Assert.Contains("Project 1", cut.Markup);
            Assert.DoesNotContain("memory-feedback:", cut.Markup);
            Assert.Empty(cut.FindAll("[data-testid='memory-ui-feedback-submit']"));
        });
    }

    [Fact]
    public async Task MemoryProvidersPage_RejectsStaleMockAsyncAndCancellationClaims()
    {
        var setup = await CreateRuntimeContextAsync(
            enableDeterministicMockDriver: false,
            CreateProviderProfile(
                "provider.async",
                "Async memory",
                MemoryProviderHealthState.Healthy,
                MemoryCapabilityIds.ContextQueryAsync,
                MemoryCapabilityIds.OperationStatus),
            services => services.AddSingleton<IMemoryProviderDriver>(new AcceptingMemoryProviderDriver(Now)));
        using var context = setup.Context;
        var cut = context.Render<MemoryProvidersPage>();

        cut.WaitForElement("[data-testid='memory-ui-provider-list']");
        cut.Find("[data-testid='memory-ui-tab-query']").Click();
        cut.WaitForElement("[data-testid='memory-ui-query-text']").Change("long recall");
        cut.Find("[data-testid='memory-ui-query-async']").Change(true);

        cut.WaitForAssertion(() =>
        {
            Assert.True(cut.Find("[data-testid='memory-ui-query-submit']").HasAttribute("disabled"));
            Assert.Contains("cannot execute asynchronous context queries", cut.Markup);
        });

        cut.Find("[data-testid='memory-ui-tab-operations']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("No operation rows", cut.Markup);
            Assert.Empty(cut.FindAll("[data-testid='memory-ui-cancel-operation']"));
        });
    }

    [Fact]
    public async Task MemoryProvidersPage_RendersProviderFailureWithoutHiddenFallback()
    {
        var setup = await CreateRuntimeContextAsync(
            enableDeterministicMockDriver: false,
            CreateProviderProfile(
                "provider.failure",
                "Failing memory",
                MemoryProviderHealthState.Healthy,
                MemoryCapabilityIds.ContextQuerySync),
            services => services.AddSingleton<IMemoryProviderDriver>(
                new FailingMemoryProviderDriver(MemoryProviderDriverResultKind.ProviderError, "provider route failed")));
        using var context = setup.Context;
        var cut = context.Render<MemoryProvidersPage>();

        cut.WaitForElement("[data-testid='memory-ui-provider-list']");
        cut.Find("[data-testid='memory-ui-tab-query']").Click();
        cut.WaitForElement("[data-testid='memory-ui-query-text']").Change("failure case");
        cut.Find("[data-testid='memory-ui-query-submit']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Failed", cut.Markup);
            Assert.Contains("provider route failed", cut.Markup);
            Assert.DoesNotContain("Cognitive Memory", cut.Markup);
        });
    }

    [Fact]
    public async Task MemoryProvidersPage_RendersEventAndFeedbackLedgersWithoutUnsupportedActions()
    {
        var setup = await CreateRuntimeContextAsync(
            enableDeterministicMockDriver: false,
            CreateProviderProfile(
                "provider.ledger",
                "Ledger memory",
                MemoryProviderHealthState.Healthy,
                MemoryCapabilityIds.EventsProviderPush,
                MemoryCapabilityIds.FeedbackDelayed,
                MemoryCapabilityIds.OperationStatus));
        var provider = setup.Context.Services.GetRequiredService<IServiceScopeFactory>();
        using (var scope = provider.CreateScope())
        {
            await SeedForgottenFeedbackAndEventAsync(scope.ServiceProvider, MemoryProviderInstanceId.Parse("provider.ledger"));
        }

        using var context = setup.Context;
        var cut = context.Render<MemoryProvidersPage>();

        cut.WaitForElement("[data-testid='memory-ui-provider-list']");
        cut.Find("[data-testid='memory-ui-tab-events']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Provider event", cut.Markup);
            Assert.Contains("Pending", cut.Markup);
            Assert.Empty(cut.FindAll("[data-testid='memory-ui-event-acknowledge']"));
        });

        cut.Find("[data-testid='memory-ui-tab-feedback']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Forgotten", cut.Markup);
            Assert.Contains("LaterCorrection", cut.Markup);
        });
    }

    [Fact]
    public async Task MemoryProvidersPage_RejectsStaleManualIngestionClaim()
    {
        var setup = await CreateRuntimeContextAsync(
            enableDeterministicMockDriver: false,
            CreateProviderProfile(
                "provider.ingestion",
                "Ingestion memory",
                MemoryProviderHealthState.Healthy,
                MemoryCapabilityIds.IngestionSnapshot));
        using var context = setup.Context;
        var cut = context.Render<MemoryProvidersPage>();

        cut.WaitForElement("[data-testid='memory-ui-provider-list']");
        cut.Find("[data-testid='memory-ui-tab-ingestion']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Ingestion unavailable", cut.Markup);
            Assert.Contains("cannot execute snapshot ingestion", cut.Markup);
            Assert.Empty(cut.FindAll("[data-testid='memory-ui-ingestion-submit']"));
        });

        cut.Find("[data-testid='memory-ui-tab-operations']").Click();
        cut.WaitForAssertion(() =>
        {
            Assert.Contains("No operation rows", cut.Markup);
        });
    }

    private static async Task<ComponentSetup> CreateRuntimeContextAsync(
        bool enableDeterministicMockDriver,
        MemoryProviderProfile profile,
        Action<IServiceCollection>? configureServices = null)
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddDbContextFactory<AppDbContext>(options =>
            options.UseInMemoryDatabase($"memory-ui-ops-{Guid.NewGuid():N}"));
        context.Services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        context.Services.AddGenericMemoryModule();
        if (enableDeterministicMockDriver)
        {
            context.Services.AddDeterministicMockMemoryProviderDriver();
        }
        configureServices?.Invoke(context.Services);
        context.Services.AddMemoryUiModule();

        using var scope = context.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
        var profileStore = scope.ServiceProvider.GetRequiredService<IMemoryProviderProfileStore>();
        await profileStore.UpsertAsync(profile, Now);

        return new ComponentSetup(context);
    }

    private static MemoryProviderProfile CreateProviderProfile(
        string instanceId,
        string displayName,
        MemoryProviderHealthState healthState,
        params MemoryCapabilityId[] capabilities)
    {
        return new MemoryProviderProfile(
            MemoryProviderInstanceId.Parse(instanceId),
            displayName,
            MemoryProviderDriverKind.Mock,
            IsEnabled: true,
            healthState,
            MemoryProviderWorkspaceScope.AllWorkspaces,
            SelectionTags: ["component-test"],
            MemoryProviderProfilePolicy.Default,
            new MemoryProviderManifest(
                MemoryProviderKind.Parse("memory.mock"),
                MemoryProtocolVersion.Current,
                capabilities
                    .Select(capability => new MemoryCapabilityDescriptor(capability, Version: "1", Supported: true))
                    .ToArray(),
                new MemoryProviderInteractionSupport(
                    SupportsSynchronousQueries: capabilities.Contains(MemoryCapabilityIds.ContextQuerySync),
                    SupportsAsynchronousOperations: capabilities.Contains(MemoryCapabilityIds.ContextQueryAsync),
                    SupportsSourceRequests: capabilities.Contains(MemoryCapabilityIds.IngestionProviderRequestedSource),
                    SupportsFeedback: capabilities.Contains(MemoryCapabilityIds.FeedbackImmediate) ||
                                      capabilities.Contains(MemoryCapabilityIds.FeedbackDelayed),
                    SupportsProviderEvents: capabilities.Contains(MemoryCapabilityIds.EventsProviderPush)),
                UiSurfaces: [],
                MemoryProviderLimits.Default,
                MemoryExtensionData.Empty));
    }

    private static async Task SeedForgottenFeedbackAndEventAsync(
        IServiceProvider provider,
        MemoryProviderInstanceId providerInstanceId)
    {
        var requester = CreateRequester();
        var feedback = MemoryFeedbackRecord.CreateUnmatched(
            MemoryFeedbackRecordId.New(),
            providerInstanceId,
            MemoryFeedbackStage.LaterCorrection,
            MemoryFeedbackOutcome.NotUseful,
            requester,
            "Delayed feedback arrived after the original context delivery expired.",
            CreateRetentionPolicy(),
            Now);
        var feedbackStore = provider.GetRequiredService<IMemoryFeedbackLedgerStore>();
        await feedbackStore.SubmitAsync(feedback);
        var expired = await feedbackStore.TransitionAsync(
            feedback.FeedbackRecordId,
            MemoryLedgerStatus.Expired,
            Now.AddMinutes(2),
            "retention policy expired feedback");
        await feedbackStore.TransitionAsync(
            expired.FeedbackRecordId,
            MemoryLedgerStatus.Forgotten,
            Now.AddMinutes(3),
            "retention policy forgot feedback");

        var eventRecord = MemoryEventInboxRecord.Create(
            MemoryEventInboxRecordId.New(),
            providerInstanceId,
            MemoryProviderEventId.New(),
            MemoryProviderEventKind.FeedbackRequest,
            MemoryCorrelationId.New(),
            MemoryCausationId.New(),
            MemoryEventPriority.Normal,
            MemoryEventLoopContext.ProviderOrigin(providerInstanceId),
            CreateRetentionPolicy(),
            Now);
        await provider.GetRequiredService<IMemoryEventLedgerStore>().EnqueueInboxAsync(eventRecord);
    }

    private static MemoryLedgerRequester CreateRequester()
    {
        return new MemoryLedgerRequester(
            RequesterId: "component-test-user",
            AgentId: "agent-dev",
            AgentRole: "developer",
            SessionId: "component-session",
            WorkflowId: null,
            WorkflowNodeId: null,
            ProcessId: null,
            ProcessStepId: null);
    }

    private static MemoryLedgerRetentionPolicy CreateRetentionPolicy()
    {
        return MemoryLedgerRetentionPolicy.Expiring(Now.AddDays(7), Now.AddDays(30));
    }

    private sealed record ComponentSetup(BunitContext Context);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class AcceptingMemoryProviderDriver(DateTimeOffset now) : IMemoryProviderDriver
    {
        public MemoryProviderDriverKind DriverKind => MemoryProviderDriverKind.Mock;

        public Task<MemoryProviderDriverResult> ExecuteContextQueryAsync(
            MemoryProviderProfile provider,
            MemoryOperationRecord operation,
            MemoryContextQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            var accepted = new MemoryOperationAccepted(
                operation.OperationId,
                $"/memory/operations/{operation.OperationId}",
                now.AddMinutes(5),
                TimeSpan.FromSeconds(5),
                CallbackAvailable: false);
            return Task.FromResult(MemoryProviderDriverResult.Accepted(accepted, "provider accepted long-running query"));
        }
    }

    private sealed class FailingMemoryProviderDriver(
        MemoryProviderDriverResultKind kind,
        string diagnostic) : IMemoryProviderDriver
    {
        public MemoryProviderDriverKind DriverKind => MemoryProviderDriverKind.Mock;

        public Task<MemoryProviderDriverResult> ExecuteContextQueryAsync(
            MemoryProviderProfile provider,
            MemoryOperationRecord operation,
            MemoryContextQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(MemoryProviderDriverResult.Failed(kind, diagnostic));
        }
    }
}
