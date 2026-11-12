using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Modules.Workbench.Pages;

namespace CanDoItAll.Tests.Unit.ProjectStructure;

public sealed class ProjectStructureUrlCodecTests {
    private static readonly Guid Project = Guid.Parse("4b1f0a51-6a3d-4a8f-9f0e-0c2d6f7a4b11");
    private static readonly ProjectStructureUrlCodec Codec = ProjectStructureUrlCodec.Instance;

    [Theory]
    [InlineData("", "")]
    [InlineData("?tab=canvas", "")]
    [InlineData("?tab=GANTT", "?tab=gantt")]
    [InlineData("?Tab=manager-summary", "?tab=manager-summary")]
    [InlineData("?tab=timeline", "")]
    [InlineData("?tab=", "")]
    public void Locations_have_one_stable_canonical_form(string query, string canonical) {
        var state = Codec.Read(UrlQuery.Parse(query));

        Assert.Equal(canonical, Codec.Format(state));
        Assert.Equal(state, Codec.Normalize(state));
        Assert.Equal(canonical, Codec.Format(Codec.Read(UrlQuery.Parse(canonical))));
    }

    [Theory]
    [InlineData(ProjectStructureView.Canvas, "")]
    [InlineData(ProjectStructureView.Gantt, "?tab=gantt")]
    [InlineData(ProjectStructureView.ManagerSummary, "?tab=manager-summary")]
    public void Structure_routes_carry_the_view_token(ProjectStructureView view, string query) {
        Assert.Equal($"/projects/{Project:D}/structure{query}", ProjectStructureUrlCodec.Route(Project, view));
    }

    [Fact]
    public void A_route_without_a_project_is_rejected() {
        Assert.Throws<ArgumentException>(() => ProjectStructureUrlCodec.Route(Guid.Empty));
    }

    [Fact]
    public void Composing_a_view_keeps_foreign_parameters_and_the_fragment() {
        var composed = UrlLocation.Compose(
            $"http://localhost/projects/{Project:D}/structure?source=mail&tab=gantt#node",
            Codec,
            new ProjectStructureUrlState(ProjectStructureView.ManagerSummary));

        Assert.Equal($"http://localhost/projects/{Project:D}/structure?tab=manager-summary&source=mail#node", composed);
    }

    [Fact]
    public void Every_codec_key_has_a_query_trigger_on_the_page() {
        Assert.Empty(UrlStateTriggers.Missing(typeof(ProjectStructurePage), Codec.Keys));
    }
}
