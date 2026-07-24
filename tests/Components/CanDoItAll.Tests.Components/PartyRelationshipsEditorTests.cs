using Bunit;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components;

public sealed class PartyRelationshipsEditorTests {
    [Fact]
    public void Shows_save_prompt_until_the_party_exists() {
        using var context = CreateContext(new StubPartyRecordQueryService([]));
        var relationships = new List<PartyRelationshipEditorModel>();
        var cut = context.RenderComponent<PartyRelationshipsEditor>(parameters => parameters
            .Add(component => component.CurrentPartyId, (Guid?)null)
            .Add(component => component.Relationships, relationships));

        Assert.Contains("Save the party first", cut.Markup, StringComparison.Ordinal);
        Assert.True(cut.Find("[data-testid='crmhr-relationship-add']").HasAttribute("disabled"));
    }

    [Fact]
    public void Add_relationship_uses_the_paged_party_picker_and_commits_the_selected_id() {
        var currentPartyId = Guid.NewGuid();
        var relatedPartyId = Guid.NewGuid();
        var queryService = new StubPartyRecordQueryService(
        [
            new PartyRecordQueryItem(
                relatedPartyId,
                "Northwind Holding",
                PartyType.Organization,
                PartyLifecycleStatus.Active,
                "NW-HOLDING",
                "Parent organization",
                ["partner"],
                false)
        ]);
        using var context = CreateContext(queryService);
        var relationships = new List<PartyRelationshipEditorModel>();
        var cut = context.RenderComponent<PartyRelationshipsEditor>(parameters => parameters
            .Add(component => component.CurrentPartyId, currentPartyId)
            .Add(component => component.Relationships, relationships));

        cut.Find("[data-testid='crmhr-relationship-add']").Click();

        var relationship = Assert.Single(relationships);
        Assert.True(relationship.IsOutgoing);
        Assert.Equal(PartyRelationshipKind.MemberOf, relationship.RelationshipKind);
        Assert.Equal(Guid.Empty, relationship.RelatedPartyId);
        Assert.Equal(currentPartyId, queryService.LastQuery?.ExcludedPartyId);

        cut.WaitForElement($"[data-testid='crmhr-party-option-{relatedPartyId:N}']")
            .Click();
        cut.Find("[data-testid='crmhr-relationship-picker-confirm']")
            .Click();

        Assert.Equal(relatedPartyId, relationship.RelatedPartyId);
        Assert.Contains("Northwind Holding", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(cut.FindAll("[data-testid='crmhr-relationship-picker']"));
    }

    [Fact]
    public void Direction_and_remove_callbacks_target_the_rendered_relationship_after_reorder() {
        using var context = CreateContext(new StubPartyRecordQueryService([]));
        var first = new PartyRelationshipEditorModel {
            RelatedPartyId = Guid.NewGuid(),
            RelationshipKind = PartyRelationshipKind.MemberOf,
            IsOutgoing = true
        };
        var second = new PartyRelationshipEditorModel {
            RelatedPartyId = Guid.NewGuid(),
            RelationshipKind = PartyRelationshipKind.ReportsTo,
            IsOutgoing = true
        };
        var relationships = new List<PartyRelationshipEditorModel> { first, second };
        var cut = context.RenderComponent<PartyRelationshipsEditor>(parameters => parameters
            .Add(component => component.CurrentPartyId, Guid.NewGuid())
            .Add(component => component.Relationships, relationships));

        relationships.Reverse();
        cut.SetParametersAndRender(parameters => parameters
            .Add(component => component.CurrentPartyId, Guid.NewGuid())
            .Add(component => component.Relationships, relationships));
        cut.Find("[data-testid='crmhr-relationship-direction-0']")
            .Change("false");

        Assert.False(second.IsOutgoing);
        Assert.True(first.IsOutgoing);

        cut.Find("[data-testid='crmhr-relationship-remove-0']")
            .Click();

        Assert.Single(relationships);
        Assert.Same(first, relationships[0]);
    }

    [Fact]
    public void Cancelling_the_picker_discards_a_new_unsaved_relationship() {
        using var context = CreateContext(new StubPartyRecordQueryService([]));
        var relationships = new List<PartyRelationshipEditorModel>();
        var cut = context.RenderComponent<PartyRelationshipsEditor>(parameters => parameters
            .Add(component => component.CurrentPartyId, Guid.NewGuid())
            .Add(component => component.Relationships, relationships));

        cut.Find("[data-testid='crmhr-relationship-add']").Click();
        Assert.Single(relationships);

        cut.Find("[data-testid='crmhr-relationship-picker-cancel']").Click();

        Assert.Empty(relationships);
        Assert.Empty(cut.FindAll("[data-testid='crmhr-relationship-picker']"));
    }

    private static TestContext CreateContext(IPartyRecordQueryService queryService) {
        var context = new TestContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddSingleton(queryService);
        return context;
    }

    private sealed class StubPartyRecordQueryService(
        IReadOnlyList<PartyRecordQueryItem> items) : IPartyRecordQueryService {
        public PartyRecordQuery? LastQuery { get; private set; }

        public Task<PartyRecordQueryItem?> GetAsync(
            Guid partyId,
            bool includeArchived = false,
            CancellationToken cancellationToken = default) {
            return Task.FromResult(items.SingleOrDefault(item => item.Id == partyId));
        }

        public Task<PartyRecordPage> SearchAsync(
            PartyRecordQuery query,
            CancellationToken cancellationToken = default) {
            LastQuery = query;
            var filteredItems = items
                .Where(item => item.Id != query.ExcludedPartyId)
                .ToList();
            var pageItems = filteredItems
                .Skip(query.PageIndex * query.PageSize)
                .Take(query.PageSize)
                .ToList();

            return Task.FromResult(new PartyRecordPage(
                pageItems,
                query.PageIndex,
                query.PageSize,
                filteredItems.Count));
        }
    }
}
