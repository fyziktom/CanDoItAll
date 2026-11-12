using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.CrmHr.UI.Pickers;
using CanDoItAll.Modules.CrmHr.Pages;

namespace CanDoItAll.Tests.Unit.CrmHr;

// The Workforce location: the open workforce record, its section and the committed catalog query behind the dialog.
public sealed class CrmHrWorkforceUrlCodecTests {
    private const string PartyText = "9d3b8f0c-1e2a-4b5c-8d7e-6f5a4b3c2d1e";
    private static readonly WorkforceUrlCodec Codec = WorkforceUrlCodec.Instance;
    private static readonly Guid Party = Guid.Parse(PartyText);

    [Theory]
    [InlineData("", "")]
    [InlineData("?partyId=" + PartyText, "?partyId=" + PartyText)]
    [InlineData("?partyId=" + PartyText + "&section=overview", "?partyId=" + PartyText)]
    [InlineData("?partyId=" + PartyText + "&section=HISTORY", "?partyId=" + PartyText + "&section=history")]
    [InlineData("?partyId=" + PartyText + "&section=payroll", "?partyId=" + PartyText)]
    [InlineData("?section=skills", "")]
    [InlineData("?partyId=not-a-guid&section=skills", "")]
    [InlineData("?q=designer&kind=contractor&page=4", "?q=designer&kind=contractor&page=4")]
    [InlineData("?kind=all&page=1", "")]
    [InlineData("?kind=intern", "")]
    [InlineData("?tags=vip&q=dev", "?q=dev")]
    [InlineData("?page=2&kind=delivery-unit&q=dev&section=allocations&partyId=" + PartyText,
        "?partyId=" + PartyText + "&section=allocations&q=dev&kind=delivery-unit&page=2")]
    public void Every_location_has_one_stable_canonical_form(string query, string canonical) {
        var state = Read(query);

        Assert.Equal(canonical, Codec.Format(state));
        Assert.Equal(state, Read(canonical));
        Assert.Equal(canonical, Codec.Format(Read(canonical)));
    }

    [Theory]
    [InlineData(WorkforceSection.Overview, "overview")]
    [InlineData(WorkforceSection.Profile, "profile")]
    [InlineData(WorkforceSection.Skills, "skills")]
    [InlineData(WorkforceSection.Allocations, "allocations")]
    [InlineData(WorkforceSection.History, "history")]
    public void Every_record_section_has_a_token(WorkforceSection section, string token) {
        Assert.Equal(token, WorkforceUrlCodec.Sections.Format(section));
        Assert.Equal(section, Read($"?partyId={Party}&section={token}").Section);
    }

    [Theory]
    [InlineData(WorkforceRecordViewFilter.All, "all")]
    [InlineData(WorkforceRecordViewFilter.Employee, "employee")]
    [InlineData(WorkforceRecordViewFilter.Contractor, "contractor")]
    [InlineData(WorkforceRecordViewFilter.Freelancer, "freelancer")]
    [InlineData(WorkforceRecordViewFilter.ExternalContact, "external-contact")]
    [InlineData(WorkforceRecordViewFilter.DeliveryUnit, "delivery-unit")]
    public void Every_workforce_kind_has_a_token(WorkforceRecordViewFilter kind, string token) {
        Assert.Equal(token, WorkforceUrlCodec.Kinds.Format(kind));
        Assert.Equal(kind, Read($"?kind={token}").Catalog.Filter);
    }

    [Fact]
    public void Closing_the_record_keeps_the_catalog_behind_it() {
        var state = Read($"?partyId={Party}&section=skills&q=dev&kind=employee&page=2");

        Assert.Equal(WorkforceSection.Skills, state.Section);
        Assert.Equal("?q=dev&kind=employee&page=2", Codec.Format(state.WithoutRecord()));
        Assert.Equal(WorkforceUrlCodec.Default, Codec.Normalize(WorkforceUrlCodec.Default with { Section = WorkforceSection.History }));
    }

    [Fact]
    public void Composing_the_location_replaces_owned_keys_and_keeps_foreign_ones() {
        var state = WorkforceUrlCodec.Default with { PartyId = Party, Section = WorkforceSection.Profile };

        Assert.Equal(
            $"/crm-hr/workforce?partyId={Party}&section=profile&tab=legacy",
            UrlLocation.Compose("/crm-hr/workforce?kind=employee&tab=legacy", Codec, state));
    }

    [Fact]
    public void Every_owned_key_has_a_query_trigger_on_the_page() {
        Assert.Equal(new[] { "partyId", "section", "q", "kind", "page" }, Codec.Keys);
        Assert.Empty(UrlStateTriggers.Missing(typeof(CrmHrWorkforcePage), Codec.Keys));
    }

    private static WorkforceUrlState Read(string query) => Codec.Read(UrlQuery.Parse(query));
}
