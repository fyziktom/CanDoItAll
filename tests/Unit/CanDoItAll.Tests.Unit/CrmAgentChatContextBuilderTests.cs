using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.Tests.Unit;

public sealed class CrmAgentChatContextBuilderTests
{
    [Fact]
    public void BuildScope_explicitly_exposes_only_context_and_keeps_tool_scope_unset()
    {
        var agentId = Guid.NewGuid();
        var account = CreateAccount();

        var scope = CrmAgentChatContextBuilder.BuildScope(
            AgentChatContextScopeId.Create(),
            account,
            [agentId, agentId, Guid.Empty]);

        Assert.Equal(AgentChatContextScopeAccessMode.Unrestricted, scope.AccessMode);
        Assert.Null(scope.WorkspaceScope);
        var access = Assert.Single(scope.AgentAccess);
        Assert.Equal(agentId, access.AgentId);
        Assert.True(access.CanRead);
        Assert.True(access.CanMutate);
        Assert.Equal("CRM sanitized selection", access.ScopeLabel);
    }

    [Fact]
    public void Account_fragment_contains_only_bounded_typed_selection_fields()
    {
        var account = new CrmAgentChatAccountContext(
            Guid.NewGuid(),
            "  Acme\r\nHoldings\t  ",
            PartyLifecycleStatus.Active,
            CrmAccountRelationshipStage.ActiveCustomer,
            [PartyRoleKind.Partner, PartyRoleKind.Customer, PartyRoleKind.Partner]);

        var fragment = CrmAgentChatContextBuilder.BuildAccountFragment(account);

        Assert.Equal(CrmAgentChatContextBuilder.AccountContributorId, fragment.ContributorId.Value);
        Assert.Contains($"AccountId: {account.AccountId:D}", fragment.Content, StringComparison.Ordinal);
        Assert.Contains("DisplayLabel: Acme Holdings", fragment.Content, StringComparison.Ordinal);
        Assert.Contains("LifecycleStatus: Active", fragment.Content, StringComparison.Ordinal);
        Assert.Contains("RelationshipStage: ActiveCustomer", fragment.Content, StringComparison.Ordinal);
        Assert.Contains("Roles: Customer, Partner", fragment.Content, StringComparison.Ordinal);
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
            OpportunitySource.Renewal,
            [OpportunityPartyRole.Sponsor, OpportunityPartyRole.Partner, OpportunityPartyRole.Sponsor]);

        var fragment = CrmAgentChatContextBuilder.BuildOpportunityFragment(opportunity);

        Assert.Equal(CrmAgentChatContextBuilder.OpportunityContributorId, fragment.ContributorId.Value);
        Assert.Contains($"OpportunityId: {opportunity.OpportunityId:D}", fragment.Content, StringComparison.Ordinal);
        Assert.Contains($"AccountId: {opportunity.AccountId:D}", fragment.Content, StringComparison.Ordinal);
        Assert.Contains("DisplayLabel: Renewal FY27", fragment.Content, StringComparison.Ordinal);
        Assert.Contains("Stage: Negotiation", fragment.Content, StringComparison.Ordinal);
        Assert.Contains("Source: Renewal", fragment.Content, StringComparison.Ordinal);
        Assert.Contains("PartyRoles: Partner, Sponsor", fragment.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Amount", fragment.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Competitor", fragment.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Summary", fragment.Content, StringComparison.Ordinal);
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

        Assert.Equal(
            ["AccountId", "DisplayLabel", "LifecycleStatus", "RelationshipStage", "Roles"],
            accountProperties);
        Assert.Equal(
            ["AccountId", "DisplayLabel", "OpportunityId", "PartyRoles", "Source", "Stage"],
            opportunityProperties);
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
