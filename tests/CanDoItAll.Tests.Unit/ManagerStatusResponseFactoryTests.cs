using CanDoItAll.Manager;

namespace CanDoItAll.Tests.Unit;

public sealed class ManagerStatusResponseFactoryTests
{
    [Fact]
    public void ResolveConfiguredApplicationUrls_uses_launch_settings_when_no_explicit_override_exists()
    {
        var options = new ManagerOptions();
        var projectPath = GetWebProjectPath();

        var urls = ManagerStatusResponseFactory.ResolveConfiguredApplicationUrls(projectPath, options);

        Assert.Equal(["https://localhost:7271", "http://localhost:5032"], urls);
    }

    [Fact]
    public void Create_marks_web_service_degraded_when_ready_urls_do_not_match_launch_profile()
    {
        var options = new ManagerOptions();
        var projectPath = GetWebProjectPath();
        var workspaceRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var response = ManagerStatusResponseFactory.Create(
            "Development",
            "token-123",
            workspaceRoot,
            projectPath,
            new WatchStatusSnapshot(
                WatchState.Ready,
                "Ready",
                4,
                45,
                1,
                1,
                DateTimeOffset.UtcNow,
                ["https://127.0.0.1:61770", "http://127.0.0.1:61771"]),
            options,
            "http://127.0.0.1:6407");

        var webService = Assert.Single(response.Services, service => service.Key == "web");

        Assert.Equal("Degraded", webService.Health);
        Assert.False(webService.IsOk);
        Assert.Contains("https://localhost:7271", webService.ExpectedUrls);
        Assert.Contains("https://127.0.0.1:61770", webService.ActiveUrls);
    }

    [Fact]
    public void Create_filters_default_port_aliases_when_launch_profile_urls_are_more_specific()
    {
        var response = ManagerStatusResponseFactory.Create(
            "Development",
            "token-123",
            @"C:\repos\CanDoItAll",
            @"C:\repos\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj",
            new WatchStatusSnapshot(
                WatchState.Ready,
                "Ready",
                4,
                45,
                1,
                1,
                DateTimeOffset.UtcNow,
                ["https://localhost:7271", "https://localhost", "http://localhost:5032"]),
            new ManagerOptions
            {
                WatchUrls = ["https://localhost:7271", "http://localhost:5032"],
                WatchLaunchProfile = string.Empty
            },
            "http://127.0.0.1:6407");

        var webService = Assert.Single(response.Services, service => service.Key == "web");

        Assert.DoesNotContain("https://localhost", response.Watch.ActiveUrls);
        Assert.DoesNotContain("https://localhost", webService.Links);
        Assert.Contains("https://localhost:7271", webService.Links);
    }

    private static string GetWebProjectPath()
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "CanDoItAll.Web", "CanDoItAll.Web.csproj"));
}
