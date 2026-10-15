using CanDoItAll.AppComponents.Navigation;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Unit.Navigation;

public sealed class UrlStateTriggersTests {
    [Fact]
    public void Missing_lists_codec_keys_without_a_query_trigger_and_matches_names_ignoring_case() {
        var missing = UrlStateTriggers.Missing(typeof(SamplePage), SampleUrlCodec.Instance.Keys);

        Assert.Equal([SampleUrlCodec.SearchKey, SampleUrlCodec.PageKey], missing);
    }

    private sealed class SamplePage : ComponentBase {
        [SupplyParameterFromQuery(Name = "TAB")] public string? Tab { get; set; }
        [SupplyParameterFromQuery(Name = SampleUrlCodec.ItemKey)] public string? Item { get; set; }
        [SupplyParameterFromQuery] private string? Section { get; set; }
    }
}
