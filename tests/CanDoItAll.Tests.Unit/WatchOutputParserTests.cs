using CanDoItAll.Manager;

namespace CanDoItAll.Tests.Unit;

public sealed class WatchOutputParserTests
{
    [Theory]
    [InlineData("Building...", WatchState.Building)]
    [InlineData("watch : Started", WatchState.Starting)]
    [InlineData("Hot reload of changes succeeded.", WatchState.HotReloadApplied)]
    [InlineData("Restarting because file changed.", WatchState.Restarting)]
    [InlineData("Unhandled exception. Boom", WatchState.RuntimeFaulted)]
    [InlineData("error CS1002: ; expected", WatchState.BuildFailed)]
    public void Parse_recognizes_expected_state_transitions(string line, WatchState expectedState)
    {
        var transition = WatchOutputParser.Parse(line);

        Assert.NotNull(transition);
        Assert.Equal(expectedState, transition!.State);
    }

    [Fact]
    public void Parse_treats_hot_reload_enabled_banner_as_starting_not_runtime_ready()
    {
        var transition = WatchOutputParser.Parse("dotnet watch : Hot reload enabled. For a list of supported edits, see https://aka.ms/dotnet/hot-reload.");

        Assert.NotNull(transition);
        Assert.Equal(WatchState.Starting, transition!.State);
        Assert.False(transition.RequiresReadinessProbe);
    }

    [Fact]
    public void Parse_ignores_non_transition_dotnet_watch_progress_lines()
    {
        var transition = WatchOutputParser.Parse("dotnet watch : Loading projects ...");

        Assert.Null(transition);
    }

    [Fact]
    public void Parse_ignores_zero_error_build_summary_lines()
    {
        var transition = WatchOutputParser.Parse("    0 Error(s)");

        Assert.Null(transition);
    }

    [Fact]
    public void TryParseUrl_extracts_runtime_url()
    {
        var url = WatchOutputParser.TryParseUrl("Now listening on: http://127.0.0.1:5188");

        Assert.Equal("http://127.0.0.1:5188", url);
    }
}
