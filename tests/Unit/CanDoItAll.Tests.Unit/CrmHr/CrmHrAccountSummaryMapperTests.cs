using CanDoItAll.CrmHr.UI.Accounts;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;

namespace CanDoItAll.Tests.Unit.CrmHr;

// The projection of the CRM account workspace model into the account summary presentation.
public sealed class CrmHrAccountSummaryMapperTests
{
    private static readonly Guid AccountId = Guid.Parse("81000000-0000-0000-0000-000000000001");

    [Fact]
    public void Maps_labels_tones_contacts_roles_and_counts()
    {
        var account = Account(
            CrmAccountRelationshipStage.Prospect,
            PartyLifecycleStatus.Active,
            summary: "Regional logistics account.",
            email: "accounts@aurora.example",
            phone: "+1 555 0100",
            roles: [PartyRoleKind.Customer, PartyRoleKind.Partner],
            connections: 2,
            opportunities: 3,
            tags: ["logistics", "renewal", "north", "priority"]);

        var summary = CrmHrAccountSummaryMapper.ToSummary(account);

        Assert.Equal(AccountId, summary.AccountPartyId);
        Assert.Equal("Aurora Logistics", summary.DisplayName);
        Assert.Equal("Regional logistics account.", summary.Summary);
        Assert.Equal("Prospect", summary.RelationshipStageLabel);
        Assert.Equal(CrmHrAccountTone.Info, summary.RelationshipStageTone);
        Assert.Equal("Active", summary.LifecycleLabel);
        Assert.Equal(CrmHrAccountTone.Success, summary.LifecycleTone);
        Assert.Equal("accounts@aurora.example", summary.PrimaryEmail);
        Assert.Equal("+1 555 0100", summary.PrimaryPhone);
        Assert.Equal(new[] { "Customer", "Partner" }, summary.Roles);
        Assert.Equal(2, summary.ConnectionCount);
        Assert.Equal(3, summary.OpportunityCount);
        Assert.Equal(4, summary.TagCount);
        Assert.True(summary.CanConvertToActiveCustomer);
    }

    [Fact]
    public void Blank_summary_and_contacts_become_absent_and_an_active_customer_is_not_offered_the_conversion()
    {
        var account = Account(
            CrmAccountRelationshipStage.ActiveCustomer,
            PartyLifecycleStatus.Draft,
            summary: "   ",
            email: string.Empty,
            phone: "  ",
            roles: [],
            connections: 0,
            opportunities: 0,
            tags: []);

        var summary = CrmHrAccountSummaryMapper.ToSummary(account);

        Assert.Null(summary.Summary);
        Assert.Null(summary.PrimaryEmail);
        Assert.Null(summary.PrimaryPhone);
        Assert.Empty(summary.Roles);
        Assert.Equal("ActiveCustomer", summary.RelationshipStageLabel);
        Assert.Equal(CrmHrAccountTone.Success, summary.RelationshipStageTone);
        Assert.Equal(CrmHrAccountTone.Neutral, summary.LifecycleTone);
        Assert.False(summary.CanConvertToActiveCustomer);
    }

    [Theory]
    [InlineData(PartyLifecycleStatus.Active, CrmHrAccountTone.Success)]
    [InlineData(PartyLifecycleStatus.Prospect, CrmHrAccountTone.Info)]
    [InlineData(PartyLifecycleStatus.Inactive, CrmHrAccountTone.Warning)]
    [InlineData(PartyLifecycleStatus.Former, CrmHrAccountTone.Warning)]
    [InlineData(PartyLifecycleStatus.Draft, CrmHrAccountTone.Neutral)]
    [InlineData(PartyLifecycleStatus.Archived, CrmHrAccountTone.Neutral)]
    [InlineData(PartyLifecycleStatus.Candidate, CrmHrAccountTone.Neutral)]
    public void Lifecycle_tones_follow_the_module_mapping(PartyLifecycleStatus status, CrmHrAccountTone expected)
        => Assert.Equal(expected, CrmHrAccountSummaryMapper.ToTone(status));

    [Theory]
    [InlineData(CrmAccountRelationshipStage.ActiveCustomer, CrmHrAccountTone.Success)]
    [InlineData(CrmAccountRelationshipStage.Prospect, CrmHrAccountTone.Info)]
    [InlineData(CrmAccountRelationshipStage.DormantCustomer, CrmHrAccountTone.Warning)]
    [InlineData(CrmAccountRelationshipStage.LostCustomer, CrmHrAccountTone.Danger)]
    public void Relationship_stage_tones_follow_the_module_mapping(CrmAccountRelationshipStage stage, CrmHrAccountTone expected)
        => Assert.Equal(expected, CrmHrAccountSummaryMapper.ToTone(stage));

    private static CrmAccountWorkspaceModel Account(
        CrmAccountRelationshipStage stage,
        PartyLifecycleStatus lifecycle,
        string summary,
        string email,
        string phone,
        IReadOnlyList<PartyRoleKind> roles,
        int connections,
        int opportunities,
        IReadOnlyList<string> tags)
        => new(
            AccountId,
            "Aurora Logistics",
            summary,
            lifecycle,
            roles,
            tags,
            email,
            phone,
            new CrmAccountProfileEditorModel { AccountPartyId = AccountId, RelationshipStage = stage },
            Enumerable.Range(1, connections)
                .Select(index => new CrmAccountConnectedRecordItemModel(Guid.NewGuid(), Guid.NewGuid(), $"Contact {index}", PartyType.Person, CrmAccountConnectionRole.Stakeholder, index == 1, string.Empty, []))
                .ToArray(),
            [],
            opportunities);
}
