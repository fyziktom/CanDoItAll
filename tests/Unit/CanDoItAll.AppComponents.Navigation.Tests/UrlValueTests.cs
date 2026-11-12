using CanDoItAll.AppComponents.Navigation;

namespace CanDoItAll.Tests.Unit.Navigation;

public sealed class UrlValueTests {
    private enum Color {
        Red,
        DarkBlue,
        Green
    }

    private static readonly UrlTokenMap<Color> Colors = new((Color.Red, "red"), (Color.DarkBlue, "dark-blue"), (Color.Green, "green"));

    [Theory]
    [InlineData("Red")]
    [InlineData("red_x")]
    [InlineData("-red")]
    [InlineData("red--blue")]
    [InlineData("")]
    public void Token_map_rejects_non_canonical_tokens(string token) {
        Assert.Throws<ArgumentException>(() => new UrlTokenMap<Color>((Color.Red, token)));
    }

    [Fact]
    public void Token_map_rejects_duplicates_and_colliding_aliases() {
        Assert.Throws<ArgumentException>(() => new UrlTokenMap<Color>((Color.Red, "red"), (Color.Green, "red")));
        Assert.Throws<ArgumentException>(() => new UrlTokenMap<Color>((Color.Red, "red"), (Color.Red, "rot")));
        Assert.Throws<ArgumentException>(() => Colors.WithAliases(("RED", Color.Green)));
    }

    [Fact]
    public void Token_map_parses_tokens_and_aliases_ignoring_case_but_formats_canonical_tokens() {
        var legacy = Colors.WithAliases(("Blue", Color.DarkBlue), ("1", Color.DarkBlue));

        Assert.Equal(Color.DarkBlue, legacy.Parse(" DARK-BLUE "));
        Assert.Equal(Color.DarkBlue, legacy.Parse("blue"));
        Assert.Equal(Color.DarkBlue, legacy.Parse("1"));
        Assert.Null(Colors.Parse("blue"));
        Assert.Null(legacy.Parse("purple"));
        Assert.Equal("dark-blue", legacy.Format(Color.DarkBlue));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UrlTokenMap<Color>((Color.Red, "red")).Format(Color.Green));
    }

    [Fact]
    public void Writer_omits_defaults_and_formats_values_invariantly() {
        string[] keys = ["text", "id", "empty", "color", "default", "colors", "page", "first", "number", "same", "date", "instant", "on", "off"];
        var writer = new UrlQueryWriter(keys)
            .Text("text", "  hello world ")
            .Id("id", Guid.Parse("8C7B1F7E-2A43-4BD9-9D2B-5B1C6F0D1E11"))
            .Id("empty", Guid.Empty)
            .Token("color", (Color?)Color.DarkBlue, Colors)
            .Token("default", Color.Red, Colors, Color.Red)
            .Tokens("colors", [Color.Green, Color.Red], Colors)
            .Page("page", 3)
            .Page("first", 1)
            .Number("number", 25, 50)
            .Number("same", 50, 50)
            .Date("date", new DateOnly(2026, 9, 4))
            .Instant("instant", new DateTimeOffset(2026, 9, 4, 10, 30, 15, TimeSpan.FromHours(2)))
            .Flag("on", true)
            .Flag("off", false);

        Assert.Equal(
            "?text=hello%20world&id=8c7b1f7e-2a43-4bd9-9d2b-5b1c6f0d1e11&color=dark-blue&colors=red%2Cgreen&page=3&number=25&date=2026-09-04&instant=2026-09-04T08%3A30%3A15Z&on=true",
            UrlLocation.Format(writer.Pairs));
    }

    [Fact]
    public void Writer_rejects_undeclared_and_repeated_keys_and_invalid_pages() {
        Assert.Throws<InvalidOperationException>(() => new UrlQueryWriter(["tab"]).Text("Tab", "x"));
        Assert.Throws<InvalidOperationException>(() => new UrlQueryWriter(["tab"]).Text("tab", "x").Text("tab", "y"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new UrlQueryWriter(["page"]).Page("page", 0));
    }

    [Fact]
    public void Reader_is_tolerant_and_returns_absent_values_for_invalid_text() {
        var query = UrlQuery.Parse(
            "id=not-a-guid&empty=00000000-0000-0000-0000-000000000000&good=8C7B1F7E-2A43-4BD9-9D2B-5B1C6F0D1E11" +
            "&page=0&page2=12&huge=999999999&neg=-4&date=2026-02-30&date2=2026-09-04&instant=2026-09-04T10:30:15.123%2B02:00" +
            "&flag=TRUE&flag2=yes&text=%20%20&long=abcdefghij&colors=green,purple,RED,green");

        Assert.Null(query.GetId("id"));
        Assert.Null(query.GetId("empty"));
        Assert.Equal(Guid.Parse("8c7b1f7e-2a43-4bd9-9d2b-5b1c6f0d1e11"), query.GetId("good"));
        Assert.Equal(1, query.GetPage("page"));
        Assert.Equal(12, query.GetPage("page2"));
        Assert.Equal(1, query.GetPage("huge"));
        Assert.Equal(-4, query.GetNumber("neg", -10, 10));
        Assert.Null(query.GetNumber("neg", 0, 10));
        Assert.Null(query.GetDate("date"));
        Assert.Equal(new DateOnly(2026, 9, 4), query.GetDate("date2"));
        Assert.Equal(new DateTimeOffset(2026, 9, 4, 8, 30, 15, TimeSpan.Zero), query.GetInstant("instant"));
        Assert.True(query.GetFlag("flag"));
        Assert.False(query.GetFlag("flag2"));
        Assert.Null(query.GetText("text", 10));
        Assert.Null(query.GetText("long", 5));
        Assert.Equal("abcdefghij", query.GetText("long", 10));
        Assert.Equal(new HashSet<Color> { Color.Green, Color.Red }, query.GetTokens("colors", Colors));
    }

    [Fact]
    public void A_flag_that_defaults_to_true_is_written_only_when_false_and_reads_back() {
        var writer = new UrlQueryWriter(["on", "off"]).Flag("on", true, defaultValue: true).Flag("off", false, defaultValue: true);
        var query = UrlQuery.Parse("?a=FALSE&b=0&c=yes&d=1");

        Assert.Equal("?off=false", UrlLocation.Format(writer.Pairs));
        Assert.False(query.GetFlag("a", defaultValue: true));
        Assert.False(query.GetFlag("b", defaultValue: true));
        Assert.True(query.GetFlag("c", defaultValue: true));
        Assert.True(query.GetFlag("d", defaultValue: false));
        Assert.True(query.GetFlag("missing", defaultValue: true));
    }
}
