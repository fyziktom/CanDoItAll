using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;

namespace CanDoItAll.Tests.Unit.CrmHr;

// The Directory location: the open party record or the new-party editor, its section and the committed catalog query.
public sealed class CrmHrDirectoryUrlCodecTests {
    private const string PartyText = "3f2504e0-4f89-41d3-9a0c-0305e82c3301";
    private static readonly DirectoryUrlCodec Codec = DirectoryUrlCodec.Instance;
    private static readonly Guid Party = Guid.Parse(PartyText);

    [Theory]
    [InlineData("", "")]
    [InlineData("?partyId=" + PartyText, "?partyId=" + PartyText)]
    [InlineData("?partyId=3F2504E0-4F89-41D3-9A0C-0305E82C3301&section=CONTACTS", "?partyId=" + PartyText + "&section=contacts")]
    [InlineData("?partyId=" + PartyText + "&section=profile", "?partyId=" + PartyText)]
    [InlineData("?partyId=" + PartyText + "&section=relationships", "?partyId=" + PartyText + "&section=relationships")]
    [InlineData("?partyId=" + PartyText + "&section=unknown", "?partyId=" + PartyText)]
    [InlineData("?partyId=" + PartyText + "&dialog=new-party", "?partyId=" + PartyText)]
    [InlineData("?dialog=new-party", "?dialog=new-party")]
    [InlineData("?dialog=new-party&section=handling", "?dialog=new-party&section=handling")]
    [InlineData("?dialog=new-party&section=activity", "?dialog=new-party")]
    [InlineData("?dialog=new-party&section=relationships", "?dialog=new-party")]
    [InlineData("?dialog=edit-party", "")]
    [InlineData("?section=contacts", "")]
    [InlineData("?partyId=not-a-guid&section=contacts", "")]
    [InlineData("?partyId=00000000-0000-0000-0000-000000000000", "")]
    [InlineData("?q=acme&tags=vip,partner&scope=organizations&page=3", "?q=acme&tags=vip%2Cpartner&scope=organizations&page=3")]
    [InlineData("?scope=all&page=1", "")]
    [InlineData("?scope=robots&page=0", "")]
    [InlineData("?page=3&scope=units&tags=vip&q=acme&section=handling&partyId=" + PartyText,
        "?partyId=" + PartyText + "&section=handling&q=acme&tags=vip&scope=units&page=3")]
    public void Every_location_has_one_stable_canonical_form(string query, string canonical) {
        var state = Read(query);

        Assert.Equal(canonical, Codec.Format(state));
        Assert.Equal(state, Read(canonical));
        Assert.Equal(canonical, Codec.Format(Read(canonical)));
    }

    [Theory]
    [InlineData(DirectorySection.Profile, "profile")]
    [InlineData(DirectorySection.Contacts, "contacts")]
    [InlineData(DirectorySection.Handling, "handling")]
    [InlineData(DirectorySection.Activity, "activity")]
    [InlineData(DirectorySection.Relationships, "relationships")]
    public void Every_record_section_has_a_token(DirectorySection section, string token) {
        Assert.Equal(token, DirectoryUrlCodec.Sections.Format(section));
        Assert.Equal(section, Read($"?partyId={Party}&section={token}").Section);
    }

    [Theory]
    [InlineData(PartyRecordScope.All, "all")]
    [InlineData(PartyRecordScope.People, "people")]
    [InlineData(PartyRecordScope.Organizations, "organizations")]
    [InlineData(PartyRecordScope.OrganizationUnits, "units")]
    [InlineData(PartyRecordScope.AiAgents, "agents")]
    public void Every_catalog_scope_has_a_token(PartyRecordScope scope, string token) {
        Assert.Equal(token, DirectoryUrlCodec.Scopes.Format(scope));
        Assert.Equal(scope, Read($"?scope={token}").Catalog.Filter);
    }

    [Fact]
    public void A_full_location_reads_into_the_record_and_the_catalog_behind_it() {
        var state = Read($"?partyId={Party}&section=activity&q=acme&tags=vip,partner&scope=people&page=2");

        Assert.Equal(Party, state.PartyId);
        Assert.Null(state.Dialog);
        Assert.Equal(DirectorySection.Activity, state.Section);
        Assert.True(state.IsRecordOpen);
        Assert.Equal(new CrmHrCatalogUrlState<PartyRecordScope>("acme", ["vip", "partner"], PartyRecordScope.People, 2), state.Catalog);
    }

    [Fact]
    public void Closing_the_record_keeps_the_catalog_behind_it() {
        var state = Read($"?partyId={Party}&section=contacts&q=acme&scope=people&page=2");

        Assert.Equal("?q=acme&scope=people&page=2", Codec.Format(state.WithoutRecord()));
        Assert.False(state.WithoutRecord().IsRecordOpen);
        Assert.Equal("?dialog=new-party&q=acme&scope=people&page=2",
            Codec.Format(state.WithoutRecord() with { Dialog = DirectoryDialog.NewParty }));
    }

    [Fact]
    public void Normalizing_applies_the_dependency_rules_to_a_written_state() {
        var created = DirectoryUrlCodec.Default with { PartyId = Party, Dialog = DirectoryDialog.NewParty, Section = DirectorySection.Contacts };
        var draft = DirectoryUrlCodec.Default with { Dialog = DirectoryDialog.NewParty, Section = DirectorySection.Relationships };
        var orphan = DirectoryUrlCodec.Default with { Section = DirectorySection.Handling };

        Assert.Equal(DirectoryUrlCodec.Default with { PartyId = Party, Section = DirectorySection.Contacts }, Codec.Normalize(created));
        Assert.Equal(DirectoryUrlCodec.Default with { Dialog = DirectoryDialog.NewParty }, Codec.Normalize(draft));
        Assert.Equal(DirectoryUrlCodec.Default, Codec.Normalize(orphan));
    }

    [Fact]
    public void Composing_the_location_replaces_owned_keys_and_keeps_foreign_ones() {
        var state = DirectoryUrlCodec.Default with { PartyId = Party, Catalog = DirectoryUrlCodec.Catalog.Default with { Search = "acme" } };

        Assert.Equal(
            $"https://localhost/crm-hr/directory?partyId={Party}&q=acme&tab=x&utm=1#top",
            UrlLocation.Compose("https://localhost/crm-hr/directory?partyId=old&tab=x&q=old&SECTION=contacts&utm=1#top", Codec, state));
    }

    [Fact]
    public void Every_owned_key_has_a_query_trigger_on_the_page() {
        Assert.Equal(new[] { "partyId", "dialog", "section", "q", "tags", "scope", "page" }, Codec.Keys);
        Assert.Empty(UrlStateTriggers.Missing(typeof(CrmHrDirectoryPage), Codec.Keys));
    }

    private static DirectoryUrlState Read(string query) => Codec.Read(UrlQuery.Parse(query));
}
