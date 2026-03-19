using CanDoItAll.Manager;

namespace CanDoItAll.Tests.Unit;

public sealed class WatchOutputParserTests
{
    [Theory]
    [InlineData("Building...", WatchState.Building)]
    [InlineData("dotnet watch 🔥", WatchState.Starting)]
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
    public void TryParseUrl_extracts_runtime_url()
    {
        var url = WatchOutputParser.TryParseUrl("Now listening on: http://127.0.0.1:5188");

        Assert.Equal("http://127.0.0.1:5188", url);
    }
}
