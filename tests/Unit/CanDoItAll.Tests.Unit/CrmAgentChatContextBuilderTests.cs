using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.Tests.Unit.Infrastructure;

public sealed class CrmAgentChatContextBuilderTests
{
    [Fact]
    public void Workspace_context_identifies_the_crm_surface_without_inventing_an_account_selection()
    {
        var scope = CrmAgentChatContextBuilder.BuildWorkspaceScope(AgentChatContextScopeId.Create());
        var fragment = CrmAgentChatContextBuilder.BuildWorkspaceFragment(12);

        Assert.Equal(CrmAgentChatContextBuilder.WorkspaceSourceKind, scope.Source.Kind.Value);
        Assert.Equal(CrmAgentChatContextBuilder.WorkspaceSourceId, scope.Source.Id.Value);
        Assert.Equal("CRM workspace", scope.DisplayName);
        Assert.Equal(AgentChatContextScopeAccessMode.Unrestricted, scope.AccessMode);
        Assert.Null(scope.WorkspaceScope);
        Assert.Empty(scope.AgentAccess);
        Assert.Equal(AgentChatContextCompletionRefreshMode.None, scope.CompletionRefreshMode);
        var position = Assert.IsType<AgentChatSurfacePosition>(scope.SurfacePosition);
        Assert.Equal(CrmHrAgentChatSurfaceBuilder.Module, position.Module);
        Assert.Equal("crm", position.Surface);
        Assert.Equal("accounts", position.View);
        Assert.Equal(CrmHrAgentChatSurfaceBuilder.CrmRoute, position.Route);
        Assert.Null(position.PrimarySelection);
        Assert.Empty(position.SelectedEntities);
        Assert.Empty(position.Facts);
        Assert.Equal(CrmAgentChatContextBuilder.WorkspaceContributorId, fragment.ContributorId.Value);
        Assert.Contains("AccountCount: 12", fragment.Content, StringComparison.Ordinal);
        Assert.Contains("SelectedAccount: None", fragment.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildScope_exposes_the_sanitized_account_and_opportunity_position()
    {
        var account = CreateAccount();
        var opportunity = new CrmAgentChatOpportunityContext(
            account.AccountId,
            Guid.NewGuid(),
            "Renewal",
            OpportunityStage.Negotiation,
            OpportunitySource.Direct,
            [OpportunityPartyRole.Customer]);

        var scope = CrmAgentChatContextBuilder.BuildScope(
            AgentChatContextScopeId.Create(),
            account,
            opportunity);

        var position = Assert.IsType<AgentChatSurfacePosition>(scope.SurfacePosition);
        Assert.Equal(account.AccountId.ToString("D"), position.PrimarySelection?.Id);
        Assert.Equal(account.DisplayLabel, position.PrimarySelection?.DisplayName);
        var selectedOpportunity = Assert.Single(position.SelectedEntities);
        Assert.Equal(opportunity.OpportunityId.ToString("D"), selectedOpportunity.Id);
        Assert.Equal(opportunity.DisplayLabel, selectedOpportunity.DisplayName);
        Assert.Collection(
            position.Facts,
            fact => Assert.Equal(("lifecycle-status", "Active"), (fact.Name, fact.Value)),
            fact => Assert.Equal(("relationship-stage", "Prospect"), (fact.Name, fact.Value)),
            fact => Assert.Equal(("opportunity-stage", "Negotiation"), (fact.Name, fact.Value)),
            fact => Assert.Equal(("opportunity-source", "Direct"), (fact.Name, fact.Value)));
    }

    [Fact]
    public void BuildScope_explicitly_exposes_only_context_and_keeps_tool_scope_unset()
    {
        var account = CreateAccount();

        var scope = CrmAgentChatContextBuilder.BuildScope(
            AgentChatContextScopeId.Create(),
            account,
            opportunity: null);

        Assert.Equal(AgentChatContextScopeAccessMode.Unrestricted, scope.AccessMode);
        Assert.Null(scope.WorkspaceScope);
        Assert.Empty(scope.AgentAccess);
        Assert.Equal(
            AgentChatContextCompletionRefreshMode.OnSuccessfulRun,
            scope.CompletionRefreshMode);
    }

    [Fact]
    public void Scope_builders_preserve_explicit_transition_states()
    {
        var workspaceScope = CrmAgentChatContextBuilder.BuildWorkspaceScope(
            AgentChatContextScopeId.Create(),
            AgentChatContextAccessState.Loading);
        var selectionScope = CrmAgentChatContextBuilder.BuildScope(
            AgentChatContextScopeId.Create(),
            CreateAccount(),
            opportunity: null,
            accessState: AgentChatContextAccessState.Failed);

        Assert.Equal(AgentChatContextAccessState.Loading, workspaceScope.AccessState);
        Assert.Equal(AgentChatContextAccessState.Failed, selectionScope.AccessState);
    }

    [Fact]
    public void Account_fragment_contains_only_bounded_typed_selection_fields()
    {
        var account = new CrmAgentChatAccountContext(
            Guid.NewGuid(),
            "  Acme\r\nHoldings\t  ",
            PartyLifecycleStatus.Active,
            CrmAccountRelationshipStage.ActiveCustomer,
            [PartyRoleKind.Customer, PartyRoleKind.Customer, PartyRoleKind.Vendor]);

        var fragment = CrmAgentChatContextBuilder.BuildAccountFragment(account);

        Assert.Equal(CrmAgentChatContextBuilder.AccountContributorId, fragment.ContributorId.Value);
        Assert.Contains($"AccountId: {account.AccountId:D}", fragment.Content, StringComparison.Ordinal);
        Assert.Contains("DisplayLabel: Acme Holdings", fragment.Content, StringComparison.Ordinal);
        Assert.Contains("LifecycleStatus: Active", fragment.Content, StringComparison.Ordinal);
        Assert.Contains("RelationshipStage: ActiveCustomer", fragment.Content, StringComparison.Ordinal);
        Assert.Contains("Roles: Customer, Vendor", fragment.Content, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', fragment.Content);
        Assert.DoesNotContain("PrimaryEmail", fragment.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("PrimaryPhone", fragment.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Notes", fragment.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Opportunity_fragment_contains_only_id_label_and_typed_classifications()
    {
        var opportunity = new CrmAgentChatOpportunityContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Renewal\nFY27",
            OpportunityStage.Negotiation,
            OpportunitySource.Direct,
            [OpportunityPartyRole.Customer, OpportunityPartyRole.Sponsor]);

        var fragment = CrmAgentChatContextBuilder.BuildOpportunityFragment(opportunity);

        Assert.Equal(CrmAgentChatContextBuilder.OpportunityContributorId, fragment.ContributorId.Value);
        Assert.Contains($"OpportunityId: {opportunity.OpportunityId:D}", fragment.Content, StringComparison.Ordinal);
        Assert.Contains($"AccountId: {opportunity.AccountId:D}", fragment.Content, StringComparison.Ordinal);
        Assert.Contains("DisplayLabel: Renewal FY27", fragment.Content, StringComparison.Ordinal);
        Assert.Contains("Stage: Negotiation", fragment.Content, StringComparison.Ordinal);
        Assert.Contains("Source: Direct", fragment.Content, StringComparison.Ordinal);
        Assert.Contains("PartyRoles: Customer, Sponsor", fragment.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Amount", fragment.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Competitor", fragment.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Summary", fragment.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Interaction_fragment_and_position_expose_only_bounded_typed_selection_fields()
    {
        var account = CreateAccount();
        var interaction = new CrmAgentChatInteractionContext(
            account.AccountId,
            Guid.NewGuid(),
            "Pricing\nreview",
            InteractionType.Call,
            Guid.NewGuid());

        var scope = CrmAgentChatContextBuilder.BuildScope(
            AgentChatContextScopeId.Create(),
            account,
            opportunity: null,
            interaction: interaction);
        var fragment = CrmAgentChatContextBuilder.BuildInteractionFragment(interaction);

        var selectedInteraction = Assert.Single(scope.SurfacePosition!.SelectedEntities);
        Assert.Equal("crm-interaction", selectedInteraction.Kind);
        Assert.Equal(interaction.InteractionId.ToString("D"), selectedInteraction.Id);
        Assert.Contains(
            scope.SurfacePosition.Facts,
            fact => fact.Name == "interaction-type" && fact.Value == "Call");
        Assert.Equal(CrmAgentChatContextBuilder.InteractionContributorId, fragment.ContributorId.Value);
        Assert.Contains($"AccountId: {account.AccountId:D}", fragment.Content, StringComparison.Ordinal);
        Assert.Contains($"InteractionId: {interaction.InteractionId:D}", fragment.Content, StringComparison.Ordinal);
        Assert.Contains("DisplayLabel: Pricing review", fragment.Content, StringComparison.Ordinal);
        Assert.Contains("InteractionType: Call", fragment.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Summary", fragment.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Notes", fragment.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Context_dtos_do_not_expose_sensitive_or_free_form_fields()
    {
        var accountProperties = typeof(CrmAgentChatAccountContext)
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name)
            .ToArray();
        var opportunityProperties = typeof(CrmAgentChatOpportunityContext)
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name)
            .ToArray();
        var interactionProperties = typeof(CrmAgentChatInteractionContext)
            .GetProperties()
            .Select(property => property.Name)
            .OrderBy(name => name)
            .ToArray();

        Assert.Equal(
            ["AccountId", "DisplayLabel", "LifecycleStatus", "RelationshipStage", "Roles"],
            accountProperties);
        Assert.Equal(
            ["AccountId", "DisplayLabel", "OpportunityId", "PartyRoles", "Source", "Stage"],
            opportunityProperties);
        Assert.Equal(
            ["AccountId", "DisplayLabel", "InteractionId", "InteractionType", "RelatedOpportunityId"],
            interactionProperties);
    }

    [Fact]
    public void ValidateSelection_rejects_an_opportunity_from_another_account()
    {
        var account = CreateAccount();
        var opportunity = new CrmAgentChatOpportunityContext(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Unrelated opportunity",
            OpportunityStage.Identified,
            OpportunitySource.Direct,
            []);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            CrmAgentChatContextBuilder.ValidateSelection(account, opportunity));

        Assert.Contains(account.AccountId.ToString("D"), exception.Message, StringComparison.Ordinal);
        Assert.Contains(opportunity.OpportunityId.ToString("D"), exception.Message, StringComparison.Ordinal);
    }

    private static CrmAgentChatAccountContext CreateAccount()
    {
        return new CrmAgentChatAccountContext(
            Guid.NewGuid(),
            "Acme",
            PartyLifecycleStatus.Active,
            CrmAccountRelationshipStage.Prospect,
            [PartyRoleKind.Customer]);
    }
}
