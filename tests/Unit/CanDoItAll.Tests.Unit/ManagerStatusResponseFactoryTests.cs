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
        var workspaceRoot = GetRepositoryRoot();
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
            new TailwindWatchStatusSnapshot(
                TailwindWatchState.Ready,
                "Tailwind watch is running.",
                7,
                DateTimeOffset.UtcNow,
                OutputExists: true,
                OutputLastWriteUtc: DateTimeOffset.UtcNow),
            options,
            "http://127.0.0.1:6407");

        var webService = Assert.Single(response.Services, service => service.Key == "web");

        Assert.Equal("Degraded", webService.Health);
        Assert.False(webService.IsOk);
        Assert.Contains("https://localhost:7271", webService.ConfiguredTargets);
        Assert.Contains("https://127.0.0.1:61770", webService.ActiveTargets);
    }

    [Fact]
    public void Create_filters_default_port_aliases_when_launch_profile_urls_are_more_specific()
    {
        var response = ManagerStatusResponseFactory.Create(
            "Development",
            "token-123",
            @"C:\repos\CanDoItAll",
            @"C:\repos\CanDoItAll\src\App\CanDoItAll.Web\CanDoItAll.Web.csproj",
            new WatchStatusSnapshot(
                WatchState.Ready,
                "Ready",
                4,
                45,
                1,
                1,
                DateTimeOffset.UtcNow,
                ["https://localhost:7271", "https://localhost", "http://localhost:5032"]),
            new TailwindWatchStatusSnapshot(
                TailwindWatchState.Ready,
                "Tailwind output propagated.",
                8,
                DateTimeOffset.UtcNow,
                OutputExists: true,
                OutputLastWriteUtc: DateTimeOffset.UtcNow),
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

    [Fact]
    public void Create_includes_tailwind_service_and_paths()
    {
        var workspaceRoot = @"C:\repos\CanDoItAll";
        var response = ManagerStatusResponseFactory.Create(
            "Development",
            "token-123",
            workspaceRoot,
            @"C:\repos\CanDoItAll\src\App\CanDoItAll.Web\CanDoItAll.Web.csproj",
            new WatchStatusSnapshot(
                WatchState.Ready,
                "Ready",
                4,
                45,
                1,
                1,
                DateTimeOffset.UtcNow,
                ["https://localhost:7271", "http://localhost:5032"]),
            new TailwindWatchStatusSnapshot(
                TailwindWatchState.Ready,
                "Tailwind output propagated to output.css.",
                9,
                DateTimeOffset.UtcNow,
                OutputExists: true,
                OutputLastWriteUtc: DateTimeOffset.UtcNow),
            new ManagerOptions(),
            "http://127.0.0.1:6407");

        var tailwindService = Assert.Single(response.Services, service => service.Key == "tailwind");

        Assert.Equal(TailwindWatchState.Ready.ToString(), response.Tailwind.StateName);
        Assert.Equal(@"C:\repos\CanDoItAll\Tailwind", response.Tailwind.WorkspacePath);
        Assert.Equal(@"C:\repos\CanDoItAll\Tailwind\input.css", response.Tailwind.InputFilePath);
        Assert.Equal(@"C:\repos\CanDoItAll\src\App\CanDoItAll.Web\wwwroot\css\output.css", response.Tailwind.OutputFilePath);
        Assert.Equal("Inputs", tailwindService.ConfiguredLabel);
        Assert.Equal("Outputs", tailwindService.ActiveLabel);
        Assert.Contains(response.Tailwind.OutputFilePath, tailwindService.ActiveTargets);
    }

    private static string GetWebProjectPath()
        => Path.Combine(GetRepositoryRoot(), "src", "App", "CanDoItAll.Web", "CanDoItAll.Web.csproj");

    private static string GetRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidatePath = Path.Combine(current.FullName, "src", "App", "CanDoItAll.Web", "CanDoItAll.Web.csproj");
            if (File.Exists(candidatePath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root from the test output directory.");
    }
}
