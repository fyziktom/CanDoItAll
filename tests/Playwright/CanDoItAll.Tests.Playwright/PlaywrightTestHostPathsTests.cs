namespace CanDoItAll.Tests.Playwright;

public sealed class PlaywrightTestHostPathsTests
{
    [Fact]
    public void BuildDotnetRunArguments_uses_active_configuration_and_no_build()
    {
        var arguments = PlaywrightTestHostPaths.BuildDotnetRunArguments(
            "src/App/CanDoItAll.Web",
            "http://127.0.0.1:5010");

        Assert.Contains($"--configuration {PlaywrightTestHostPaths.BuildConfiguration}", arguments, StringComparison.Ordinal);
        Assert.Contains("--no-build", arguments, StringComparison.Ordinal);
        Assert.Contains("--no-launch-profile", arguments, StringComparison.Ordinal);
        Assert.Contains("--project src/App/CanDoItAll.Web", arguments, StringComparison.Ordinal);
        Assert.Contains("--urls http://127.0.0.1:5010", arguments, StringComparison.Ordinal);
    }

    [Fact]
    public void RepositoryRoot_points_to_solution_root()
    {
        Assert.True(File.Exists(Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "CanDoItAll.slnx")));
    }
}
