using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.Prompts.Pages;
using CanDoItAll.Prompts.UI.Gallery;

namespace CanDoItAll.Tests.Unit.Prompts;

public sealed class PromptGalleryUrlCodecTests {
    private const string Prompt = "4b1f0a51-6a3d-4a8f-9f0e-0c2d6f7a4b11";
    private static readonly PromptGalleryUrlCodec Codec = PromptGalleryUrlCodec.Instance;

    [Theory]
    [InlineData("", "")]
    [InlineData("?promptId=4B1F0A51-6A3D-4A8F-9F0E-0C2D6F7A4B11", "?promptId=" + Prompt)]
    [InlineData("?page=3&chatModel=false&archived=1&favorites=TRUE&model=gpt-5&provider=OpenAI&status=final&kind=PART&tags=b,a,B,,a&q=%20hello%20&promptId=" + Prompt,
        "?promptId=" + Prompt + "&q=hello&tags=b%2Ca&kind=part&status=final&provider=OpenAI&model=gpt-5&favorites=true&archived=true&chatModel=false&page=3")]
    [InlineData("?kind=prompt&status=published&page=0&favorites=yes&archived=no&chatModel=true", "")]
    [InlineData("?kind=full-prompt&page=1", "?kind=full-prompt")]
    [InlineData("?promptId=not-a-guid&page=-2", "")]
    public void Locations_have_one_stable_canonical_form(string query, string canonical) {
        var state = Codec.Read(UrlQuery.Parse(query));

        Assert.Equal(canonical, Codec.Format(state));
        Assert.True(Codec.SameState(state, Codec.Normalize(state)));
        Assert.Equal(canonical, Codec.Format(Codec.Read(UrlQuery.Parse(canonical))));
    }

    [Fact]
    public void Tags_beyond_the_query_contract_are_dropped() {
        var tags = string.Join(',', Enumerable.Range(1, 25).Select(index => $"tag-{index}").Append(new string('x', 121)));

        var state = Codec.Read(UrlQuery.Parse($"?tags={tags}"));

        Assert.Equal(PromptGalleryUrlCodec.MaximumTagCount, state.Tags!.Count);
        Assert.Equal("tag-20", state.Tags[^1]);
    }

    [Fact]
    public void Search_text_beyond_the_query_contract_is_dropped() {
        var state = Codec.Read(UrlQuery.Parse($"?q={new string('q', PromptGalleryUrlCodec.MaximumSearchLength + 1)}&provider={new string('p', 121)}&model={new string('m', 201)}"));

        Assert.Null(state.Search);
        Assert.Null(state.Provider);
        Assert.Null(state.Model);
    }

    [Fact]
    public void Search_requests_and_commits_map_between_one_based_pages_and_list_filters() {
        var state = Codec.Read(UrlQuery.Parse("?q=hello&tags=alpha&kind=part&status=draft&provider=OpenAI&model=gpt-5&favorites=true&archived=true&chatModel=false&page=4"));

        var request = state.ToSearchRequest();

        Assert.Equal(3, request.PageIndex);
        Assert.Equal("hello", request.Filters.Text);
        Assert.Equal(["alpha"], request.Filters.Tags);
        Assert.Equal(PromptGalleryItemKind.Part, request.Filters.Kind);
        Assert.Equal(PromptArtifactStatus.Draft, request.Filters.Status);
        Assert.Equal("OpenAI", request.Filters.ProviderFilter);
        Assert.Equal("gpt-5", request.Filters.ModelFilter);
        Assert.True(request.Filters.FavoritesOnly);
        Assert.True(request.Filters.IncludeArchived);
        Assert.False(request.Filters.ActualChatModelOnly);
        Assert.True(Codec.SameState(state, new PromptGalleryUrlState().WithSearch(request.Filters, request.PageIndex)));
        Assert.Equal("", Codec.Format(new PromptGalleryUrlState().WithSearch(PromptGalleryFilterValues.Initial, 0)));
    }

    [Fact]
    public void Composing_the_editor_keeps_foreign_parameters_and_the_fragment() {
        var composed = UrlLocation.Compose(
            $"http://localhost/prompt-gallery?from=chat&kind=part#list",
            Codec,
            new PromptGalleryUrlState(PromptId: Guid.Parse(Prompt), Kind: PromptGalleryItemKind.Part));

        Assert.Equal($"http://localhost/prompt-gallery?promptId={Prompt}&kind=part&from=chat#list", composed);
    }

    [Fact]
    public void Every_codec_key_has_a_query_trigger_on_the_page() {
        Assert.Empty(UrlStateTriggers.Missing(typeof(PromptGalleryPage), Codec.Keys));
    }
}
