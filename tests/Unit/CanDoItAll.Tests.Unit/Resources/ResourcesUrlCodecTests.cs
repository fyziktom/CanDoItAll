using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.Resources.Pages;

namespace CanDoItAll.Tests.Unit.Resources;

public sealed class ResourcesUrlCodecTests {
    private const string Resource = "4b1f0a51-6a3d-4a8f-9f0e-0c2d6f7a4b11";
    private const string Project = "5c2f0a51-6a3d-4a8f-9f0e-0c2d6f7a4b22";
    private const string Filter = "6d3f0a51-6a3d-4a8f-9f0e-0c2d6f7a4b33";
    private const string SourceDigits = "7e4f0a516a3d4a8f9f0e0c2d6f7a4b44";
    private static readonly ResourcesUrlCodec Codec = ResourcesUrlCodec.Instance;

    [Theory]
    [InlineData("", "")]
    [InlineData("?view=registry", "")]
    [InlineData("?view=BROWSE", "?view=browse")]
    [InlineData("?source=project:7E4F0A516A3D4A8F9F0E0C2D6F7A4B44&validation=WARNING&connector=resource.repository&project=" + Filter +
        "&q=%20repo%20&projectId=" + Project + "&resourceId=" + Resource + "&view=browse",
        "?view=browse&resourceId=" + Resource + "&projectId=" + Project + "&q=repo&project=" + Filter +
        "&connector=resource.repository&validation=warning&source=project%3A" + SourceDigits)]
    [InlineData("?view=list&validation=maybe&source=folder:abc&resourceId=1&project=x", "")]
    [InlineData("?source=storage:" + SourceDigits, "?source=storage%3A" + SourceDigits)]
    public void Locations_have_one_stable_canonical_form(string query, string canonical) {
        var state = Codec.Read(UrlQuery.Parse(query));

        Assert.Equal(canonical, Codec.Format(state));
        Assert.Equal(state, Codec.Normalize(state));
        Assert.Equal(canonical, Codec.Format(Codec.Read(UrlQuery.Parse(canonical))));
    }

    [Fact]
    public void Browse_source_is_the_typed_source_identity() {
        var state = Codec.Read(UrlQuery.Parse("?source=project%3A" + SourceDigits));

        Assert.Equal(ResourceFileSourceKey.ForProject(Guid.Parse(SourceDigits)), state.Source);
    }

    [Fact]
    public void Filter_comparison_ignores_selection_view_and_source() {
        var state = new ResourcesUrlState(Search: "repo", Validation: ResourceValidationStatus.Valid);

        Assert.True(state.HasSameFilters(state with { View = ResourcesView.Browse, ResourceId = Guid.Parse(Resource) }));
        Assert.False(state.HasSameFilters(state with { Search = "repository" }));
        Assert.False(state.HasSameFilters(state with { ProjectFilter = Guid.Parse(Filter) }));
    }

    [Fact]
    public void Composing_a_selection_keeps_foreign_parameters_and_the_fragment() {
        var composed = UrlLocation.Compose(
            $"http://localhost/resources?from=project&resourceId={Resource}#editor",
            Codec,
            new ResourcesUrlState(ProjectId: Guid.Parse(Project)));

        Assert.Equal($"http://localhost/resources?projectId={Project}&from=project#editor", composed);
    }

    [Fact]
    public void Every_codec_key_has_a_query_trigger_on_the_page() {
        Assert.Empty(UrlStateTriggers.Missing(typeof(ResourcesPage), Codec.Keys));
    }
}
