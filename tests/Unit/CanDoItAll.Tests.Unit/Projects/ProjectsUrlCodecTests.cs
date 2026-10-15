using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectsUrlCodecTests {
    private const string Project = "4b1f0a51-6a3d-4a8f-9f0e-0c2d6f7a4b11";
    private const string Parent = "5c2f0a51-6a3d-4a8f-9f0e-0c2d6f7a4b22";
    private const string Other = "6d3f0a51-6a3d-4a8f-9f0e-0c2d6f7a4b33";
    private static readonly ProjectsUrlCodec Codec = ProjectsUrlCodec.Instance;

    [Theory]
    [InlineData("", "")]
    [InlineData("?projectId=4B1F0A51-6A3D-4A8F-9F0E-0C2D6F7A4B11", "?projectId=" + Project)]
    [InlineData("?projectId=" + Project + "&mode=EDITOR&step=Stack", "?projectId=" + Project + "&mode=editor&step=stack")]
    [InlineData("?projectId=" + Project + "&mode=overview&step=stack", "?projectId=" + Project)]
    [InlineData("?projectId=" + Project + "&mode=editor&step=identity", "?projectId=" + Project + "&mode=editor")]
    [InlineData("?mode=editor&step=review", "")]
    [InlineData("?view=files&subprojects=false&scope=children&parentId=" + Parent + "&party=Acme&partyCategory=delivery-unit&status=ON-HOLD&q=%20%20alpha%20",
        "?q=alpha&status=on-hold&partyCategory=delivery-unit&party=Acme&parentId=" + Parent + "&scope=children&subprojects=false&view=files")]
    [InlineData("?scope=descendants&subprojects=true&view=cards", "")]
    [InlineData("?status=paused&partyCategory=vendor&scope=sideways&view=grid&subprojects=no&mode=wizard", "")]
    [InlineData("?projectId=not-a-guid&parentId=00000000-0000-0000-0000-000000000000&hierarchyId=bad", "")]
    [InlineData("?projectId=" + Project + "&hierarchyId=" + Parent + "&filesId=" + Other, "?projectId=" + Project)]
    [InlineData("?hierarchyId=" + Parent + "&filesId=" + Other, "?hierarchyId=" + Parent)]
    [InlineData("?filesId=" + Other, "?filesId=" + Other)]
    public void Locations_have_one_stable_canonical_form(string query, string canonical) {
        var state = Codec.Read(UrlQuery.Parse(query));

        Assert.Equal(canonical, Codec.Format(state));
        Assert.Equal(state, Codec.Normalize(state));
        Assert.Equal(canonical, Codec.Format(Codec.Read(UrlQuery.Parse(canonical))));
    }

    [Fact]
    public void Text_longer_than_the_filter_contract_is_dropped_instead_of_throwing() {
        var tooLong = new string('a', ProjectFileFilter.MaximumTextLength + 1);

        var state = Codec.Read(UrlQuery.Parse($"?q={tooLong}&party={tooLong}"));

        Assert.Null(state.Search);
        Assert.Null(state.Party);
        Assert.Equal(new ProjectFileFilter(), state.ToFilter());
    }

    [Fact]
    public void Filter_state_maps_to_the_portfolio_filter_contract() {
        var parent = Guid.Parse(Parent);
        var state = new ProjectsUrlState(Search: "alpha", Status: ProjectStatus.Active, PartyCategory: ProjectPartyPortfolioCategory.Owner,
            Party: "Acme", ParentId: parent, Scope: ProjectHierarchyFilterMode.Related, IncludeSubprojects: false);

        var filter = state.ToFilter();

        Assert.Equal("alpha", filter.Search);
        Assert.Equal(ProjectStatus.Active, filter.Status);
        Assert.Equal(ProjectPartyPortfolioCategory.Owner, filter.RelatedPartyCategory);
        Assert.Equal("Acme", filter.RelatedPartyValue);
        Assert.Equal(parent, filter.HierarchyProjectId);
        Assert.Equal(ProjectHierarchyFilterMode.Related, filter.HierarchyMode);
        Assert.False(filter.IncludeSubprojects);
    }

    [Fact]
    public void Removing_an_overlay_keeps_filters_and_view() {
        var state = Codec.Read(UrlQuery.Parse($"?projectId={Project}&mode=editor&step=links&q=alpha&view=files"));

        var parent = state.WithoutOverlay();

        Assert.True(state.HasOverlay);
        Assert.False(parent.HasOverlay);
        Assert.Equal("?q=alpha&view=files", Codec.Format(parent));
    }

    [Fact]
    public void Dashboard_link_addresses_only_the_project_overview() {
        Assert.Equal($"/projects?projectId={Project}", ProjectsUrlCodec.Dashboard(Guid.Parse(Project)));
        Assert.Throws<ArgumentException>(() => ProjectsUrlCodec.Dashboard(Guid.Empty));
    }

    [Fact]
    public void Composing_the_state_keeps_foreign_parameters_and_the_fragment() {
        var composed = UrlLocation.Compose(
            $"http://localhost/projects?utm=mail&projectId={Project}&status=draft#cards",
            Codec,
            new ProjectsUrlState(Status: ProjectStatus.Completed));

        Assert.Equal("http://localhost/projects?status=completed&utm=mail#cards", composed);
    }

    [Fact]
    public void Every_codec_key_has_a_query_trigger_on_the_page() {
        Assert.Empty(UrlStateTriggers.Missing(typeof(ProjectsPage), Codec.Keys));
    }
}
