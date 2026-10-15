using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.TestLab.Pages;
using CanDoItAll.TestLab.UI;

namespace CanDoItAll.Tests.Unit.TestLab;

public sealed class TestLabUrlCodecTests {
    private const string Plan = "3d7c1b2a-5e4f-4a6b-9c8d-7e6f5a4b3c21";
    private const string Project = "9a8b7c6d-5e4f-4321-8765-43210fedcba9";
    private static readonly TestLabUrlCodec Codec = TestLabUrlCodec.Instance;

    [Theory]
    [InlineData("", "")]
    [InlineData("?planId=3D7C1B2A-5E4F-4A6B-9C8D-7E6F5A4B3C21", "?planId=" + Plan)]
    [InlineData("?planId=not-a-plan", "")]
    [InlineData("?planId=00000000-0000-0000-0000-000000000000", "")]
    [InlineData("?section=overview", "")]
    [InlineData("?section=Cases", "?section=cases")]
    [InlineData("?section=history", "")]
    [InlineData("?section=runs&planId=" + Plan, "?planId=" + Plan + "&section=runs")]
    [InlineData("?projectId=" + Project, "?projectId=" + Project)]
    [InlineData("?projectId=" + Project + "&planId=" + Plan, "?planId=" + Plan + "&projectId=" + Project)]
    [InlineData("?q=%20%20regression%20%20", "?q=regression")]
    [InlineData("?project=" + Project + "&phase=%20Browser%20phase%20&result=Passed", "?project=" + Project + "&phase=Browser%20phase&result=passed")]
    [InlineData("?result=unknown&project=unknown", "")]
    [InlineData("?result=blocked&q=&phase=", "?result=blocked")]
    public void Locations_have_one_stable_canonical_form(string query, string canonical) {
        var state = Codec.Read(UrlQuery.Parse(query));

        Assert.Equal(canonical, Codec.Format(state));
        Assert.Equal(state, Codec.Normalize(state));
        Assert.Equal(canonical, Codec.Format(Codec.Read(UrlQuery.Parse(canonical))));
    }

    [Fact]
    public void Every_state_value_round_trips_in_a_fixed_key_order() {
        var state = new TestLabUrlState(Guid.Parse(Plan), TestLabSection.Evidence, Guid.Parse(Project), "release",
            Guid.Parse(Project), "Browser phase", TestCaseStatus.Implemented);

        var query = Codec.Format(state);

        Assert.Equal($"?planId={Plan}&section=evidence&projectId={Project}&q=release&project={Project}&phase=Browser%20phase&result=implemented", query);
        Assert.Equal(state, Codec.Read(UrlQuery.Parse(query)));
        Assert.Equal(new TestLabPlanFilter("release", Guid.Parse(Project), "Browser phase", TestCaseStatus.Implemented), state.Filter);
    }

    [Fact]
    public void Text_beyond_the_bound_is_neither_read_nor_written() {
        var tooLong = new string('a', TestLabUrlCodec.MaximumTextLength + 1);

        Assert.Equal(string.Empty, Codec.Format(new TestLabUrlState(Search: tooLong, Phase: tooLong)));
        Assert.Equal(string.Empty, Codec.Read(UrlQuery.Parse("?q=" + tooLong)).Search);
    }

    [Fact]
    public void Composing_keeps_foreign_parameters_and_the_fragment() {
        var composed = UrlLocation.Compose($"http://localhost/test-lab?utm=mail&PLANID={Plan}&q=old#cases", Codec,
            new TestLabUrlState(Section: TestLabSection.Cases, Search: "new"));

        Assert.Equal("http://localhost/test-lab?section=cases&q=new&utm=mail#cases", composed);
    }

    [Fact]
    public void Token_maps_cover_every_typed_value() {
        Assert.All(Enum.GetValues<TestLabSection>(), section => Assert.True(TestLabUrlCodec.Sections.Contains(section)));
        Assert.All(Enum.GetValues<TestCaseStatus>(), result => Assert.True(TestLabUrlCodec.Results.Contains(result)));
    }

    [Fact]
    public void Every_owned_key_has_a_query_trigger_on_the_routed_page() {
        Assert.Empty(UrlStateTriggers.Missing(typeof(TestLabPage), Codec.Keys));
    }
}
