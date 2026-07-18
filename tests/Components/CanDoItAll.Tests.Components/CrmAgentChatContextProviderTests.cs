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
        context.Services.AddSingleton<IAgentChatContextRegistry>(registry);
        context.Services.AddSingleton<IAgentChatExecutionNotificationHub>(new RecordingNotificationHub());
        using var staleScopeLease = registry.ActivateScope(new AgentChatContextScope(
            AgentChatContextScopeId.Create(),
            new AgentChatContextSource(
                new AgentChatContextSourceKind("project-structure"),
                new AgentChatContextSourceId(Guid.NewGuid().ToString("D"))),
            "Stale project structure"));

        var cut = context.RenderComponent<CrmAgentChatContextProvider>(parameters => parameters
            .Add(component => component.Account, (CrmAgentChatAccountContext?)null));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(CrmAgentChatContextBuilder.WorkspaceSourceKind, snapshot.Scope.Source.Kind.Value);
            Assert.Equal(CrmAgentChatContextBuilder.WorkspaceSourceId, snapshot.Scope.Source.Id.Value);
            Assert.Equal("crm", snapshot.Scope.SurfacePosition?.Surface);
            Assert.Null(snapshot.Scope.SurfacePosition?.PrimarySelection);
            var fragment = Assert.Single(snapshot.Fragments);
            Assert.Equal(CrmAgentChatContextBuilder.WorkspaceContributorId, fragment.ContributorId.Value);
            Assert.Contains("SelectedAccount: None", fragment.Content, StringComparison.Ordinal);
        });

        var account = CreateAccount();
        var opportunity = CreateOpportunity(account.AccountId, "Expansion", OpportunityStage.Qualified);
        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.Account, account)
            .Add(component => component.Opportunity, opportunity));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(CrmAgentChatContextBuilder.SourceKind, snapshot.Scope.Source.Kind.Value);
            Assert.Equal(account.AccountId.ToString("D"), snapshot.Scope.Source.Id.Value);
            Assert.Equal(account.AccountId.ToString("D"), snapshot.Scope.SurfacePosition?.PrimarySelection?.Id);
            Assert.Equal(
                opportunity.OpportunityId.ToString("D"),
                Assert.Single(snapshot.Scope.SurfacePosition!.SelectedEntities).Id);
            Assert.Collection(
                snapshot.Fragments,
                fragment => Assert.Equal(CrmAgentChatContextBuilder.AccountContributorId, fragment.ContributorId.Value),
                fragment => Assert.Equal(CrmAgentChatContextBuilder.OpportunityContributorId, fragment.ContributorId.Value));
        });

        var interaction = new CrmAgentChatInteractionContext(
            account.AccountId,
            Guid.NewGuid(),
            "Architecture review",
            InteractionType.Meeting,
            opportunity.OpportunityId);
        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.Account, account)
            .Add(component => component.Opportunity, (CrmAgentChatOpportunityContext?)null)
            .Add(component => component.Interaction, interaction));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            var selectedInteraction = Assert.Single(snapshot.Scope.SurfacePosition!.SelectedEntities);
            Assert.Equal("crm-interaction", selectedInteraction.Kind);
            Assert.Equal(interaction.InteractionId.ToString("D"), selectedInteraction.Id);
            Assert.Collection(
                snapshot.Fragments,
                fragment => Assert.Equal(CrmAgentChatContextBuilder.AccountContributorId, fragment.ContributorId.Value),
                fragment => Assert.Equal(CrmAgentChatContextBuilder.InteractionContributorId, fragment.ContributorId.Value));
        });

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.Account, (CrmAgentChatAccountContext?)null)
            .Add(component => component.Opportunity, (CrmAgentChatOpportunityContext?)null)
            .Add(component => component.Interaction, (CrmAgentChatInteractionContext?)null));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(CrmAgentChatContextBuilder.WorkspaceSourceKind, snapshot.Scope.Source.Kind.Value);
            var fragment = Assert.Single(snapshot.Fragments);
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
        var agent = CreateAgent();
        context.Services.AddSingleton<IAgentChatContextRegistry>(registry);
        context.Services.AddSingleton<IAgentChatExecutionNotificationHub>(hub);
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
            Assert.Empty(snapshot.Scope.AgentAccess);
            Assert.Equal(
                AgentChatContextCompletionRefreshMode.OnSuccessfulRun,
                snapshot.Scope.CompletionRefreshMode);
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
            Assert.Contains(
                snapshot.Scope.SurfacePosition!.Facts,
                fact => fact.Name == "opportunity-stage" && fact.Value == "Negotiation");
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

    [Fact]
    public void Provider_blocks_transition_context_and_recovers_without_replacing_the_selection_scope()
    {
        using var context = new TestContext();
        var registry = new AgentChatContextRegistry(TimeProvider.System);
        var agent = CreateAgent();
        context.Services.AddSingleton<IAgentChatContextRegistry>(registry);
        context.Services.AddSingleton<IAgentChatExecutionNotificationHub>(new RecordingNotificationHub());
        var account = CreateAccount();

        var cut = context.RenderComponent<CrmAgentChatContextProvider>(parameters => parameters
            .Add(component => component.Account, account)
            .Add(component => component.ContextAccessState, AgentChatContextAccessState.Loading));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(AgentChatContextAccessState.Loading, snapshot.Scope.AccessState);
            Assert.Throws<AgentChatContextUnavailableException>(() =>
                AgentChatContextContributionComposer.Compose(snapshot, agent.Id));
        });
        var loadingSnapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.Account, account)
            .Add(component => component.ContextAccessState, AgentChatContextAccessState.Ready));

        cut.WaitForAssertion(() =>
        {
            var snapshot = Assert.IsType<AgentChatContextSnapshot>(registry.Capture());
            Assert.Equal(loadingSnapshot.Scope.Id, snapshot.Scope.Id);
            Assert.Equal(AgentChatContextAccessState.Ready, snapshot.Scope.AccessState);
            Assert.NotNull(AgentChatContextContributionComposer.Compose(snapshot, agent.Id));
        });
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
