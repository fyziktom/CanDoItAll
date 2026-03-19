using CanDoItAll.Manager;

namespace CanDoItAll.Tests.Unit;

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
                @"C:\repos\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj",
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
                [
                    new ManagedServiceSnapshot(
                        "manager",
                        "CanDoItAll.Manager",
                        "Ok",
                        true,
                        "Manager is responding.",
                        ["http://127.0.0.1:6407"],
                        ["http://127.0.0.1:6407"],
                        ["http://127.0.0.1:6407"]),
                    new ManagedServiceSnapshot(
                        "web",
                        "CanDoItAll.Web",
                        "Ok",
                        true,
                        "Application is ready on the configured launch profile URLs.",
                        ["https://localhost:7271", "http://localhost:5032"],
                        ["https://localhost:7271", "http://localhost:5032"],
                        ["https://localhost:7271", "http://localhost:5032"])
                ],
                DateTimeOffset.UtcNow),
            new CapsuleCoverageSummary(161, 15, 1, 79, 0, [], [], DateTimeOffset.UtcNow),
            openApiAvailable: true);

        Assert.Contains("CanDoItAll Manager", html);
        Assert.Contains("Services", html);
        Assert.Contains("CanDoItAll.Web", html);
        Assert.Contains("https://localhost:7271", html);
        Assert.Contains("/openapi/v1.json", html);
    }
}
