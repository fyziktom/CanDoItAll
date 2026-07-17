using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class CrmAgentChatContextProviderTests
{
    [Fact]
    public void Provider_replaces_stale_module_context_and_transitions_between_workspace_and_selection()
    {
        using var context = new TestContext();
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        context.Services.AddLogging();
        context.Services.AddSingleton<IAgentChatContextRegistry>(registry);
        context.Services.AddSingleton<IAgentChatExecutionNotificationHub>(new RecordingNotificationHub());
        context.Services.AddSingleton<IAgentReferenceDataProvider>(
            new StubAgentReferenceDataProvider(CreateAgent()));
        context.Services.AddSingleton<IAgentReferenceDataCacheInvalidator>(
            new StubReferenceDataCacheInvalidator());
        using var staleScopeLease = registry.ActivateScope(new AgentChatContextScope(
            AgentChatContextScopeId.Create(),
            new AgentChatContextSource(
                new AgentChatContextSourceKind("project-structure"),
                new AgentChatContextSourceId(Guid.NewGuid().ToString("D"))),
            "Stale project structure"));

        var cut = context.RenderComponent<CrmAgentChatContextProvider>(parameters => parameters
            .Add(component => component.AccountCount, 0)
            .Add(component => component.Account, (CrmAgentChatAccountContext?)null));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(CrmAgentChatContextBuilder.WorkspaceSourceKind, snapshot.Scope.Source.Kind.Value);
            Assert.Equal(CrmAgentChatContextBuilder.WorkspaceSourceId, snapshot.Scope.Source.Id.Value);
            var fragment = Assert.Single(snapshot.Fragments);
            Assert.Equal(CrmAgentChatContextBuilder.WorkspaceContributorId, fragment.ContributorId.Value);
            Assert.Contains("AccountCount: 0", fragment.Content, StringComparison.Ordinal);
            Assert.Contains("SelectedAccount: None", fragment.Content, StringComparison.Ordinal);
        });

        var account = CreateAccount();
        var opportunity = CreateOpportunity(account.AccountId, "Expansion", OpportunityStage.Qualified);
        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.AccountCount, 1)
            .Add(component => component.Account, account)
            .Add(component => component.Opportunity, opportunity));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(CrmAgentChatContextBuilder.SourceKind, snapshot.Scope.Source.Kind.Value);
            Assert.Equal(account.AccountId.ToString("D"), snapshot.Scope.Source.Id.Value);
            Assert.Collection(
                snapshot.Fragments,
                fragment => Assert.Equal(CrmAgentChatContextBuilder.AccountContributorId, fragment.ContributorId.Value),
                fragment => Assert.Equal(CrmAgentChatContextBuilder.OpportunityContributorId, fragment.ContributorId.Value));
        });

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.AccountCount, 1)
            .Add(component => component.Account, (CrmAgentChatAccountContext?)null)
            .Add(component => component.Opportunity, (CrmAgentChatOpportunityContext?)null));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(CrmAgentChatContextBuilder.WorkspaceSourceKind, snapshot.Scope.Source.Kind.Value);
            var fragment = Assert.Single(snapshot.Fragments);
            Assert.Contains("AccountCount: 1", fragment.Content, StringComparison.Ordinal);
            Assert.Contains("SelectedAccount: None", fragment.Content, StringComparison.Ordinal);
        });

        cut.Instance.Dispose();
        cut.Dispose();

        Assert.Null(registry.Capture());
    }

    [Fact]
    public async Task Provider_tracks_selection_publishes_refresh_and_releases_its_scope()
    {
        using var context = new TestContext();
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var hub = new RecordingNotificationHub();
        var invalidator = new StubReferenceDataCacheInvalidator();
        var agent = CreateAgent();
        context.Services.AddLogging();
        context.Services.AddSingleton<IAgentChatContextRegistry>(registry);
        context.Services.AddSingleton<IAgentChatExecutionNotificationHub>(hub);
        context.Services.AddSingleton<IAgentReferenceDataProvider>(
            new StubAgentReferenceDataProvider(agent));
        context.Services.AddSingleton<IAgentReferenceDataCacheInvalidator>(invalidator);
        var account = CreateAccount();
        var opportunity = CreateOpportunity(account.AccountId, "Expansion", OpportunityStage.Qualified);
        var refreshCount = 0;

        var cut = context.RenderComponent<CrmAgentChatContextProvider>(parameters => parameters
            .Add(component => component.Account, account)
            .Add(component => component.Opportunity, opportunity)
            .Add(component => component.RefreshRequested, _ => refreshCount++));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(account.AccountId.ToString("D"), snapshot.Scope.Source.Id.Value);
            Assert.Equal(AgentChatContextScopeAccessMode.Unrestricted, snapshot.Scope.AccessMode);
            Assert.Null(snapshot.Scope.WorkspaceScope);
            Assert.True(snapshot.FindAccess(agent.Id)?.CanMutate);
            Assert.Collection(
                snapshot.Fragments,
                fragment => Assert.Equal(CrmAgentChatContextBuilder.AccountContributorId, fragment.ContributorId.Value),
                fragment => Assert.Contains("DisplayLabel: Expansion", fragment.Content, StringComparison.Ordinal));
        });

        var activeSnapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.Account, account)
            .Add(component => component.Opportunity, opportunity)
            .Add(component => component.RefreshRequested, _ => refreshCount++));
        Assert.Equal(
            activeSnapshot.Version,
            Assert.IsType<AgentChatContextSnapshot>(registry.Capture()).Version);

        await hub.PublishAsync(new AgentChatExecutionCompleted(
            activeSnapshot.Scope.Id,
            activeSnapshot.Scope.Source,
            agent.Id,
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateTimeOffset.UtcNow));

        cut.WaitForAssertion(() => Assert.Equal(1, refreshCount));

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.Account, account)
            .Add(component => component.Opportunity, CreateOpportunity(account.AccountId, "Renewal", OpportunityStage.Negotiation))
            .Add(component => component.RefreshRequested, _ => refreshCount++));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Contains(
                snapshot.Fragments,
                fragment => fragment.Content.Contains("DisplayLabel: Renewal", StringComparison.Ordinal));
        });

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.Account, account)
            .Add(component => component.Opportunity, (CrmAgentChatOpportunityContext?)null)
            .Add(component => component.RefreshRequested, _ => refreshCount++));

        cut.WaitForAssertion(() =>
            Assert.Single(Assert.IsType<AgentChatContextSnapshot>(registry.Capture()).Fragments));

        cut.Instance.Dispose();
        cut.Dispose();

        Assert.Null(registry.Capture());
        Assert.Equal(0, hub.SubscriptionCount);
    }

    private static CrmAgentChatAccountContext CreateAccount()
    {
        return new CrmAgentChatAccountContext(
            Guid.NewGuid(),
            "Acme",
            PartyLifecycleStatus.Active,
            CrmAccountRelationshipStage.ActiveCustomer,
            [PartyRoleKind.Customer]);
    }

    private static CrmAgentChatOpportunityContext CreateOpportunity(
        Guid accountId,
        string displayLabel,
        OpportunityStage stage)
    {
        return new CrmAgentChatOpportunityContext(
            accountId,
            Guid.NewGuid(),
            displayLabel,
            stage,
            OpportunitySource.Direct,
            [OpportunityPartyRole.Customer]);
    }

    private static AgentDefinition CreateAgent()
    {
        var timestamp = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Guid.NewGuid(),
            "CRM assistant",
            "Assistant",
            "CRM test agent",
            "Help the user.",
            AgentLifecycleStatus.Active,
            ProviderProfileId: null,
            "test-model",
            AgentWorkloadKind.General,
            AgentChatHistoryMode.FrameworkManaged,
            Temperature: 0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            ConfigurationJson: "{}",
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            Capabilities: [],
            Tags: [],
            CreatedAtUtc: timestamp,
            UpdatedAtUtc: timestamp);
    }

    private sealed class StubAgentReferenceDataProvider(
        AgentDefinition agent) : IAgentReferenceDataProvider
    {
        public Task<AgentReferenceDataSnapshot> GetAsync(
            AgentReferenceDataRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new AgentReferenceDataSnapshot(
                AgentReferenceDataSections.Agents,
                [agent],
                [],
                new Dictionary<Guid, ProviderProfile>(),
                DateTimeOffset.UtcNow,
                TimeSpan.Zero));
        }
    }

    private sealed class StubReferenceDataCacheInvalidator : IAgentReferenceDataCacheInvalidator
    {
        public event EventHandler? Invalidated;

        public void Invalidate()
            => Invalidated?.Invoke(this, EventArgs.Empty);
    }

    private sealed class RecordingNotificationHub : IAgentChatExecutionNotificationHub
    {
        private readonly Dictionary<Guid, SubscriptionEntry> subscriptions = [];

        public int SubscriptionCount => subscriptions.Count;

        public IAgentChatExecutionNotificationSubscription Subscribe(
            AgentChatContextSource source,
            Func<AgentChatExecutionCompleted, Task> handler)
        {
            var id = Guid.NewGuid();
            subscriptions.Add(id, new SubscriptionEntry(source, handler));
            return new Subscription(this, id, source);
        }

        public async Task PublishAsync(
            AgentChatExecutionCompleted notification,
            CancellationToken cancellationToken = default)
        {
            foreach (var subscription in subscriptions.Values
                         .Where(entry => entry.Source == notification.Source)
                         .ToArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                await subscription.Handler(notification);
            }
        }

        private sealed record SubscriptionEntry(
            AgentChatContextSource Source,
            Func<AgentChatExecutionCompleted, Task> Handler);

        private sealed class Subscription(
            RecordingNotificationHub owner,
            Guid id,
            AgentChatContextSource source) : IAgentChatExecutionNotificationSubscription
        {
            private RecordingNotificationHub? owner = owner;

            public AgentChatContextSource Source { get; } = source;

            public void Dispose()
            {
                var currentOwner = Interlocked.Exchange(ref owner, null);
                currentOwner?.subscriptions.Remove(id);
            }
        }
    }
}
