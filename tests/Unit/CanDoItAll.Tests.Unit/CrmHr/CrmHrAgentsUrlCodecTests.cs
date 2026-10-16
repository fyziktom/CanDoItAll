using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.CrmHr.UI.Agents;
using CanDoItAll.Modules.CrmHr.Pages;

namespace CanDoItAll.Tests.Unit.CrmHr;

// The CRM/HR Agents location: the open agent record, its section and the committed agent catalog query.
public sealed class CrmHrAgentsUrlCodecTests {
    private const string PartyText = "7a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d";
    private static readonly CrmHrAgentsUrlCodec Codec = CrmHrAgentsUrlCodec.Instance;
    private static readonly Guid Party = Guid.Parse(PartyText);

    [Theory]
    [InlineData("", "")]
    [InlineData("?partyId=" + PartyText, "?partyId=" + PartyText)]
    [InlineData("?partyId=" + PartyText + "&section=overview", "?partyId=" + PartyText)]
    [InlineData("?partyId=" + PartyText + "&section=Technical-Projection", "?partyId=" + PartyText + "&section=technical-projection")]
    [InlineData("?partyId=" + PartyText + "&section=runtime", "?partyId=" + PartyText)]
    [InlineData("?section=evidence", "")]
    [InlineData("?partyId=not-a-guid&section=evidence", "")]
    [InlineData("?q=atlas&validation=review-required&page=2", "?q=atlas&validation=review-required&page=2")]
    [InlineData("?validation=all&page=1", "")]
    [InlineData("?validation=retired", "")]
    [InlineData("?tab=agents&partyId=" + PartyText, "?partyId=" + PartyText)]
    [InlineData("?page=2&validation=suspended&q=atlas&section=crm-profile&partyId=" + PartyText,
        "?partyId=" + PartyText + "&section=crm-profile&q=atlas&validation=suspended&page=2")]
    public void Every_location_has_one_stable_canonical_form(string query, string canonical) {
        var state = Read(query);

        Assert.Equal(canonical, Codec.Format(state));
        Assert.Equal(state, Read(canonical));
        Assert.Equal(canonical, Codec.Format(Read(canonical)));
    }

    [Theory]
    [InlineData(CrmHrAgentRecordSection.Overview, "overview")]
    [InlineData(CrmHrAgentRecordSection.CrmProfile, "crm-profile")]
    [InlineData(CrmHrAgentRecordSection.TechnicalProjection, "technical-projection")]
    [InlineData(CrmHrAgentRecordSection.Evidence, "evidence")]
    public void Every_record_section_has_a_token(CrmHrAgentRecordSection section, string token) {
        Assert.Equal(token, CrmHrAgentsUrlCodec.Sections.Format(section));
        Assert.Equal(section, Read($"?partyId={Party}&section={token}").Section);
    }

    [Theory]
    [InlineData(AiAgentValidationFilter.All, "all")]
    [InlineData(AiAgentValidationFilter.Draft, "draft")]
    [InlineData(AiAgentValidationFilter.ReviewRequired, "review-required")]
    [InlineData(AiAgentValidationFilter.Approved, "approved")]
    [InlineData(AiAgentValidationFilter.Suspended, "suspended")]
    public void Every_validation_filter_has_a_token(AiAgentValidationFilter validation, string token) {
        Assert.Equal(token, CrmHrAgentsUrlCodec.Validations.Format(validation));
        Assert.Equal(validation, Read($"?validation={token}").Catalog.Filter);
    }

    [Fact]
    public void Closing_the_record_keeps_the_catalog_behind_it() {
        var state = Read($"?partyId={Party}&section=evidence&q=atlas&validation=approved&page=3");

        Assert.Equal("?q=atlas&validation=approved&page=3", Codec.Format(state.WithoutRecord()));
        Assert.Equal(CrmHrAgentRecordSection.Overview, state.WithoutRecord().Section);
        Assert.Equal(CrmHrAgentsUrlCodec.Default, Codec.Normalize(CrmHrAgentsUrlCodec.Default with { Section = CrmHrAgentRecordSection.Evidence }));
    }

    [Fact]
    public void The_agent_catalog_has_no_tag_filter() {
        Assert.Empty(Read("?tags=vip").Catalog.Tags);
        Assert.DoesNotContain("tags", Codec.Keys);
    }

    [Fact]
    public void Every_owned_key_has_a_query_trigger_on_the_page() {
        Assert.Equal(new[] { "partyId", "section", "q", "validation", "page" }, Codec.Keys);
        Assert.Empty(UrlStateTriggers.Missing(typeof(CrmHrAgentsPage), Codec.Keys));
    }

    private static CrmHrAgentsUrlState Read(string query) => Codec.Read(UrlQuery.Parse(query));
}
