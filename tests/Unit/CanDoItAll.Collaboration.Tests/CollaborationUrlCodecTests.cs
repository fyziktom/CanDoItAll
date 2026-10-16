using CanDoItAll.AppComponents.Navigation;
using CanDoItAll.Collaboration.UI;
using CanDoItAll.Modules.Collaboration.Pages;

namespace CanDoItAll.Tests.Unit.Collaboration;

public sealed class CollaborationUrlCodecTests {
    private static readonly Guid Thread = Guid.Parse("6f1f0a51-6a3d-4a8f-9f0e-0c2d6f7a4b11");
    private static readonly CollaborationUrlCodec Codec = CollaborationUrlCodec.Instance;

    [Theory]
    [InlineData("", "")]
    [InlineData("?view=inbox", "")]
    [InlineData("?view=THREADS&unread=1", "?view=threads&unread=true")]
    [InlineData("?view=unknown&unread=no", "")]
    [InlineData("?threadId=6f1f0a51-6a3d-4a8f-9f0e-0c2d6f7a4b11", "?threadId=6f1f0a51-6a3d-4a8f-9f0e-0c2d6f7a4b11")]
    [InlineData("?view=inbox&threadId=6F1F0A51-6A3D-4A8F-9F0E-0C2D6F7A4B11", "?view=inbox&threadId=6f1f0a51-6a3d-4a8f-9f0e-0c2d6f7a4b11")]
    [InlineData("?view=escalations&threadId=not-a-guid", "?view=escalations")]
    public void Locations_have_one_stable_canonical_form(string query, string canonical) {
        var state = Codec.Read(UrlQuery.Parse(query));

        Assert.Equal(canonical, Codec.Format(state));
        Assert.Equal(state, Codec.Normalize(state));
        Assert.Equal(canonical, Codec.Format(Codec.Read(UrlQuery.Parse(canonical))));
    }

    [Fact]
    public void A_thread_without_a_view_stays_an_unresolved_deep_link() {
        var state = Codec.Read(UrlQuery.Parse($"?threadId={Thread}"));

        Assert.Null(state.View);
        Assert.Equal(CollaborationSection.Inbox, state.Section);
        Assert.Equal(Thread, state.ThreadId);
    }

    [Fact]
    public void Composing_the_resolved_thread_keeps_foreign_parameters() {
        var composed = UrlLocation.Compose(
            $"http://localhost/collaboration?threadId={Thread}&source=mail",
            Codec,
            new CollaborationUrlState(CollaborationSection.Threads, false, Thread));

        Assert.Equal($"http://localhost/collaboration?view=threads&threadId={Thread}&source=mail", composed);
    }
}
