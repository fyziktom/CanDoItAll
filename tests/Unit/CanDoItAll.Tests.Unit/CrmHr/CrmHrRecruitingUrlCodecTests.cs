using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;

namespace CanDoItAll.Tests.Unit.CrmHr;

// The Recruiting location: the application (or the candidate whose latest application opens), the new-application
// editor, the record section and the committed application catalog query.
public sealed class CrmHrRecruitingUrlCodecTests {
    private const string ApplicationText = "2c1f7e5a-8b3d-4c6e-9f0a-1b2c3d4e5f60";
    private const string PartyText = "5e6f7a8b-9c0d-4e1f-a2b3-c4d5e6f7a8b9";
    private static readonly RecruitingUrlCodec Codec = RecruitingUrlCodec.Instance;
    private static readonly Guid Application = Guid.Parse(ApplicationText);
    private static readonly Guid Party = Guid.Parse(PartyText);

    [Theory]
    [InlineData("", "")]
    [InlineData("?applicationId=" + ApplicationText, "?applicationId=" + ApplicationText)]
    [InlineData("?applicationId=" + ApplicationText + "&section=application", "?applicationId=" + ApplicationText)]
    [InlineData("?applicationId=" + ApplicationText + "&section=INTERVIEWS", "?applicationId=" + ApplicationText + "&section=interviews")]
    [InlineData("?partyId=" + PartyText + "&section=conversion", "?partyId=" + PartyText + "&section=conversion")]
    [InlineData("?applicationId=" + ApplicationText + "&partyId=" + PartyText, "?applicationId=" + ApplicationText + "&partyId=" + PartyText)]
    [InlineData("?applicationId=" + ApplicationText + "&dialog=new-application", "?applicationId=" + ApplicationText)]
    [InlineData("?partyId=" + PartyText + "&dialog=new-application", "?partyId=" + PartyText)]
    [InlineData("?dialog=new-application", "?dialog=new-application")]
    [InlineData("?dialog=new-application&section=interviews", "?dialog=new-application")]
    [InlineData("?dialog=offer-letter", "")]
    [InlineData("?section=assessments", "")]
    [InlineData("?applicationId=not-a-guid&partyId=" + PartyText, "?partyId=" + PartyText)]
    [InlineData("?stage=hired&q=engineer&page=2", "?q=engineer&stage=hired&page=2")]
    [InlineData("?stage=all&page=1", "")]
    [InlineData("?stage=archived", "")]
    [InlineData("?page=3&stage=offer&q=engineer&section=development&applicationId=" + ApplicationText,
        "?applicationId=" + ApplicationText + "&section=development&q=engineer&stage=offer&page=3")]
    public void Every_location_has_one_stable_canonical_form(string query, string canonical) {
        var state = Read(query);

        Assert.Equal(canonical, Codec.Format(state));
        Assert.Equal(state, Read(canonical));
        Assert.Equal(canonical, Codec.Format(Read(canonical)));
    }

    [Theory]
    [InlineData(RecruitingSection.Application, "application")]
    [InlineData(RecruitingSection.Interviews, "interviews")]
    [InlineData(RecruitingSection.Assessments, "assessments")]
    [InlineData(RecruitingSection.Development, "development")]
    [InlineData(RecruitingSection.Conversion, "conversion")]
    public void Every_record_section_has_a_token(RecruitingSection section, string token) {
        Assert.Equal(token, RecruitingUrlCodec.Sections.Format(section));
        Assert.Equal(section, Read($"?applicationId={Application}&section={token}").Section);
    }

    [Theory]
    [InlineData(RecruitmentApplicationScope.All, "all")]
    [InlineData(RecruitmentApplicationScope.Applied, "applied")]
    [InlineData(RecruitmentApplicationScope.Screening, "screening")]
    [InlineData(RecruitmentApplicationScope.Interviewing, "interviewing")]
    [InlineData(RecruitmentApplicationScope.Offer, "offer")]
    [InlineData(RecruitmentApplicationScope.Hired, "hired")]
    [InlineData(RecruitmentApplicationScope.Rejected, "rejected")]
    [InlineData(RecruitmentApplicationScope.Withdrawn, "withdrawn")]
    public void Every_application_stage_has_a_token(RecruitmentApplicationScope stage, string token) {
        Assert.Equal(token, RecruitingUrlCodec.Stages.Format(stage));
        Assert.Equal(stage, Read($"?stage={token}").Catalog.Filter);
    }

    [Fact]
    public void A_candidate_names_an_application_and_the_editor_opens_a_record() {
        var candidate = Read($"?partyId={Party}");
        var editor = Read("?dialog=new-application");

        Assert.True(candidate.NamesApplication);
        Assert.True(candidate.IsRecordOpen);
        Assert.False(editor.NamesApplication);
        Assert.True(editor.IsRecordOpen);
        Assert.Equal(RecruitingDialog.NewApplication, editor.Dialog);
        Assert.False(Read("?q=engineer").IsRecordOpen);
    }

    [Fact]
    public void Publishing_the_resolved_application_replaces_the_candidate() {
        var candidate = Read($"?partyId={Party}&section=interviews&q=engineer");
        var resolved = candidate with { ApplicationId = Application, PartyId = null };

        Assert.Equal($"?applicationId={Application}&section=interviews&q=engineer", Codec.Format(resolved));
    }

    [Fact]
    public void Closing_the_record_keeps_the_catalog_behind_it() {
        var state = Read($"?applicationId={Application}&partyId={Party}&section=conversion&q=engineer&stage=offer&page=2");

        Assert.Equal("?q=engineer&stage=offer&page=2", Codec.Format(state.WithoutRecord()));
        Assert.False(state.WithoutRecord().IsRecordOpen);
        Assert.Equal(
            RecruitingUrlCodec.Default with { Dialog = RecruitingDialog.NewApplication },
            Codec.Normalize(RecruitingUrlCodec.Default with { Dialog = RecruitingDialog.NewApplication, Section = RecruitingSection.Conversion }));
    }

    [Fact]
    public void Every_owned_key_has_a_query_trigger_on_the_page() {
        Assert.Equal(new[] { "applicationId", "partyId", "dialog", "section", "q", "stage", "page" }, Codec.Keys);
        Assert.Empty(UrlStateTriggers.Missing(typeof(CrmHrRecruitingPage), Codec.Keys));
    }

    private static RecruitingUrlState Read(string query) => Codec.Read(UrlQuery.Parse(query));
}
