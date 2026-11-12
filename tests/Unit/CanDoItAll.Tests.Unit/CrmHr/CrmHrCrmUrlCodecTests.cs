using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;

namespace CanDoItAll.Tests.Unit.CrmHr;

// The CRM location: the account, the opportunity open over it, the interaction in context, the account section with
// its context default omitted, and the committed account catalog query.
public sealed class CrmHrCrmUrlCodecTests {
    private const string AccountText = "6f9619ff-8b86-4011-b42d-00c04fc964ff";
    private const string OpportunityText = "c9a646d3-9c61-4cb7-bfcd-ee2522c8f633";
    private const string InteractionText = "1b4e28ba-2fa1-41d2-883f-0016d3cca427";
    private static readonly CrmUrlCodec Codec = CrmUrlCodec.Instance;
    private static readonly Guid Account = Guid.Parse(AccountText);
    private static readonly Guid Opportunity = Guid.Parse(OpportunityText);
    private static readonly Guid Interaction = Guid.Parse(InteractionText);

    [Theory]
    [InlineData("", "")]
    [InlineData("?accountId=" + AccountText, "?accountId=" + AccountText)]
    [InlineData("?accountId=" + AccountText + "&section=overview", "?accountId=" + AccountText)]
    [InlineData("?accountId=" + AccountText + "&section=FINANCIALS", "?accountId=" + AccountText + "&section=financials")]
    [InlineData("?accountId=" + AccountText + "&section=connected-records", "?accountId=" + AccountText + "&section=connected-records")]
    [InlineData("?accountId=" + AccountText + "&section=pipeline", "?accountId=" + AccountText)]
    [InlineData("?accountId=" + AccountText + "&opportunityId=" + OpportunityText, "?accountId=" + AccountText + "&opportunityId=" + OpportunityText)]
    [InlineData("?accountId=" + AccountText + "&opportunityId=" + OpportunityText + "&section=opportunities",
        "?accountId=" + AccountText + "&opportunityId=" + OpportunityText)]
    [InlineData("?accountId=" + AccountText + "&opportunityId=" + OpportunityText + "&section=overview",
        "?accountId=" + AccountText + "&opportunityId=" + OpportunityText + "&section=overview")]
    [InlineData("?opportunityId=" + OpportunityText, "?opportunityId=" + OpportunityText)]
    [InlineData("?interactionId=" + InteractionText + "&section=interactions", "?interactionId=" + InteractionText)]
    [InlineData("?accountId=" + AccountText + "&interactionId=" + InteractionText + "&section=financials",
        "?accountId=" + AccountText + "&interactionId=" + InteractionText + "&section=financials")]
    [InlineData("?section=financials", "")]
    [InlineData("?accountId=not-a-guid&section=financials", "")]
    [InlineData("?accountId=not-a-guid&opportunityId=" + OpportunityText, "?opportunityId=" + OpportunityText)]
    [InlineData("?q=north&tags=enterprise&page=2&scope=people", "?q=north&tags=enterprise&page=2")]
    [InlineData("?page=2&section=financials&q=north&accountId=" + AccountText, "?accountId=" + AccountText + "&section=financials&q=north&page=2")]
    public void Every_location_has_one_stable_canonical_form(string query, string canonical) {
        var state = Read(query);

        Assert.Equal(canonical, Codec.Format(state));
        Assert.Equal(state, Read(canonical));
        Assert.Equal(canonical, Codec.Format(Read(canonical)));
    }

    [Theory]
    [InlineData(CrmSection.Overview, "overview")]
    [InlineData(CrmSection.Financials, "financials")]
    [InlineData(CrmSection.ConnectedRecords, "connected-records")]
    [InlineData(CrmSection.Interactions, "interactions")]
    [InlineData(CrmSection.Opportunities, "opportunities")]
    public void Every_account_section_has_a_token(CrmSection section, string token) {
        Assert.Equal(token, CrmUrlCodec.Sections.Format(section));
        Assert.Equal(section, Read($"?accountId={Account}&section={token}").Section);
    }

    [Fact]
    public void An_omitted_section_is_the_default_of_the_account_context() {
        Assert.Equal(CrmSection.Overview, Read($"?accountId={Account}").Section);
        Assert.Equal(CrmSection.Opportunities, Read($"?accountId={Account}&opportunityId={Opportunity}").Section);
        Assert.Equal(CrmSection.Opportunities, Read($"?opportunityId={Opportunity}&interactionId={Interaction}").Section);
        Assert.Equal(CrmSection.Interactions, Read($"?accountId={Account}&interactionId={Interaction}").Section);
        Assert.Equal(CrmSection.Overview, Read("?section=opportunities").Section);
        Assert.False(Read("?section=opportunities").HasAccountContext);
        Assert.True(Read($"?interactionId={Interaction}").HasAccountContext);
    }

    [Fact]
    public void The_home_opportunity_link_opens_the_opportunity_on_its_pipeline_without_a_redundant_section() {
        var href = Codec.Href(
            "/crm-hr/crm",
            CrmUrlCodec.Default with { AccountId = Account, OpportunityId = Opportunity, Section = CrmSection.Opportunities });

        Assert.Equal($"/crm-hr/crm?accountId={Account}&opportunityId={Opportunity}", href);
        Assert.Equal(CrmSection.Opportunities, Codec.Read(href).Section);
    }

    [Fact]
    public void The_account_catalog_lists_organizations_and_has_no_scope_key() {
        var state = Read("?q=north&scope=people");

        Assert.Equal(PartyRecordScope.Organizations, state.Catalog.Filter);
        Assert.DoesNotContain("scope", Codec.Keys);
    }

    [Fact]
    public void Closing_the_record_keeps_the_catalog_behind_it() {
        var state = Read($"?accountId={Account}&opportunityId={Opportunity}&interactionId={Interaction}&section=financials&q=north&tags=enterprise&page=3");

        Assert.Equal("?q=north&tags=enterprise&page=3", Codec.Format(state.WithoutRecord()));
        Assert.False(state.WithoutRecord().HasAccountContext);
        Assert.Equal(CrmSection.Overview, state.WithoutRecord().Section);
    }

    [Fact]
    public void Closing_the_opportunity_returns_to_the_account_on_the_section_it_shows() {
        var opportunity = Read($"?accountId={Account}&opportunityId={Opportunity}");
        var account = opportunity with { OpportunityId = null };

        Assert.Equal($"?accountId={Account}&section=opportunities", Codec.Format(account));
        Assert.Equal(CrmSection.Opportunities, Codec.Normalize(account).Section);
    }

    [Fact]
    public void Composing_the_location_replaces_owned_keys_and_keeps_foreign_ones() {
        var state = CrmUrlCodec.Default with { AccountId = Account };

        Assert.Equal(
            $"/crm-hr/crm?accountId={Account}&scope=people",
            UrlLocation.Compose($"/crm-hr/crm?opportunityId={Opportunity}&scope=people&section=financials", Codec, state));
    }

    [Fact]
    public void Every_owned_key_has_a_query_trigger_on_the_page() {
        Assert.Equal(new[] { "accountId", "opportunityId", "interactionId", "section", "q", "tags", "page" }, Codec.Keys);
        Assert.Empty(UrlStateTriggers.Missing(typeof(CrmHrCrmPage), Codec.Keys));
    }

    private static CrmUrlState Read(string query) => Codec.Read(UrlQuery.Parse(query));
}
