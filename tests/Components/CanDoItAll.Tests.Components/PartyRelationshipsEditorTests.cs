using Bunit;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;

namespace CanDoItAll.Tests.Components;

public sealed class PartyRelationshipsEditorTests
{
    [Fact]
    public async Task Shows_save_prompt_until_the_party_exists_and_adds_new_rows()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var relationships = new List<PartyRelationshipEditorModel>();
        var cut = harness.Context.RenderComponent<PartyRelationshipsEditor>(parameters => parameters
            .Add(component => component.CurrentPartyId, (Guid?)null)
            .Add(component => component.Relationships, relationships)
            .Add(component => component.AvailableParties, Array.Empty<PartyDirectoryListItemModel>()));

        Assert.Contains("Save the party first", cut.Markup, StringComparison.Ordinal);

        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.CurrentPartyId, Guid.NewGuid())
            .Add(component => component.Relationships, relationships)
            .Add(component => component.AvailableParties,
                new[]
                {
                    new PartyDirectoryListItemModel(
                        Guid.NewGuid(),
                        "Northwind Holding",
                        PartyType.Organization,
                        PartyLifecycleStatus.Active,
                        false,
                        "NW-HOLDING",
                        "Parent organization",
                        [],
                        [PartyRoleKind.Partner],
                        "holding@northwind.example",
                        string.Empty,
                        DateTimeOffset.UtcNow)
                }));

        cut.Find("[data-testid='crmhr-relationship-add']").Click();

        Assert.Single(relationships);
        Assert.True(relationships[0].IsOutgoing);
        Assert.Equal(PartyRelationshipKind.MemberOf, relationships[0].RelationshipKind);
        Assert.Contains("Northwind Holding", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("Add relationship", cut.Markup, StringComparison.Ordinal);
    }
}
