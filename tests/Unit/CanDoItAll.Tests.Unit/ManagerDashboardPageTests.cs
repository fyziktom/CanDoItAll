using CanDoItAll.Manager;

namespace CanDoItAll.Tests.Unit.Infrastructure;

public sealed class ManagerDashboardPageTests
{
    [Fact]
    public void Render_includes_services_section_and_configured_urls()
    {
        var html = ManagerDashboardPage.Render(
            new ManagerStatusResponse(
                "CanDoItAll.Manager",
                "Development",
                "token-123",
                @"C:\repos\CanDoItAll",
                @"C:\repos\CanDoItAll\src\App\CanDoItAll.Web\CanDoItAll.Web.csproj",
                ["https://localhost:7271", "http://localhost:5032"],
                new WatchStatusViewModel(
                    (int)WatchState.Ready,
                    WatchState.Ready.ToString(),
                    "Ready",
                    4,
                    45,
                    1,
                    1,
                    DateTimeOffset.UtcNow,
                    ["https://localhost:7271", "http://localhost:5032"]),
                new TailwindStatusViewModel(
                    (int)TailwindWatchState.Ready,
                    TailwindWatchState.Ready.ToString(),
                    "Tailwind output propagated.",
                    8,
                    DateTimeOffset.UtcNow,
                    @"C:\repos\CanDoItAll\Tailwind",
                    @"C:\repos\CanDoItAll\Tailwind\input.css",
                    @"C:\repos\CanDoItAll\src\App\CanDoItAll.Web\wwwroot\css\output.css",
                    true,
                    DateTimeOffset.UtcNow),
                [
                    new ManagedServiceSnapshot(
                        "manager",
                        "CanDoItAll.Manager",
                        "Ok",
                        true,
                        "Manager is responding.",
                        ["http://127.0.0.1:6407"],
                        "Configured URLs",
                        ["http://127.0.0.1:6407"],
                        "Active URLs",
                        ["http://127.0.0.1:6407"]),
                    new ManagedServiceSnapshot(
                        "web",
                        "CanDoItAll.Web",
                        "Ok",
                        true,
                        "Application is ready on the configured launch profile URLs.",
                        ["https://localhost:7271", "http://localhost:5032"],
                        "Configured URLs",
                        ["https://localhost:7271", "http://localhost:5032"],
                        "Active URLs",
                        ["https://localhost:7271", "http://localhost:5032"]),
                    new ManagedServiceSnapshot(
                        "tailwind",
                        "Tailwind watch",
                        "Ok",
                        true,
                        "Tailwind output propagated.",
                        [],
                        "Inputs",
                        [@"C:\repos\CanDoItAll\Tailwind\input.css"],
                        "Outputs",
                        [@"C:\repos\CanDoItAll\src\App\CanDoItAll.Web\wwwroot\css\output.css"])
                ],
                DateTimeOffset.UtcNow),
            new CapsuleCoverageSummary(161, 15, 1, 79, 0, [], [], DateTimeOffset.UtcNow),
            openApiAvailable: true);

        Assert.Contains("CanDoItAll Manager", html);
        Assert.Contains("Services", html);
        Assert.Contains("CanDoItAll.Web", html);
        Assert.Contains("Tailwind", html);
        Assert.Contains("https://localhost:7271", html);
        Assert.Contains("/api/tailwind/logs", html);
        Assert.Contains("/openapi/v1.json", html);
    }
}
