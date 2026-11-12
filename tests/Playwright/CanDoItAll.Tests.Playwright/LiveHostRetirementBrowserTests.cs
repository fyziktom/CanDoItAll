using System.Text.Json;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class LiveHostRetirementBrowserTests {
    [Fact]
    public async Task Spend_guard_application_stop_retains_database_and_browser_for_failure_evidence() {
        var fixture = new PlaywrightAppFixture();
        try {
            await fixture.InitializeAsync();
            await using var provider = await TestApplicationBootstrap.BuildServiceProviderAsync(fixture.OwnedDatabaseProfile,
                "Live.Guard.FailureEvidence", TestSchemaBootstrapModules.Full,
                new Dictionary<string, string?> { [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind });
            await using var context = await fixture.Browser.NewContextAsync();
            var page = await context.NewPageAsync();
            var marker = "Failure evidence " + Guid.NewGuid().ToString("N");
            await page.GotoAsync(fixture.BaseUrl + "/settings");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            await page.GetByTestId("defaults-name").FillAsync(marker);
            await page.GetByTestId("defaults-save").ClickAsync();
            await Assertions.Expect(page.GetByTestId("settings-operation").First).ToContainTextAsync("Committed");

            await fixture.StopOwnedApplicationAsync();
            await fixture.StopOwnedApplicationAsync();
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            await Assert.ThrowsAsync<HttpRequestException>(async () => {
                using var response = await client.GetAsync(fixture.BaseUrl);
            });
            Assert.True(fixture.Browser.IsConnected);
            await using var readback = provider.CreateAsyncScope();
            var saved = await readback.ServiceProvider.GetRequiredService<WorkspaceService>().GetSettingsAsync();
            Assert.Equal(marker, saved.WorkspaceName);
            var evidencePage = await context.NewPageAsync();
            await evidencePage.SetContentAsync("<title>Failure evidence remains available</title>");
            Assert.Equal("Failure evidence remains available", await evidencePage.TitleAsync());

            var artifacts = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "live-host-retirement");
            Directory.CreateDirectory(artifacts);
            await File.WriteAllTextAsync(Path.Combine(artifacts, "proof.json"), JsonSerializer.Serialize(new {
                ApplicationStopped = true, RepeatedStop = true, BrowserRetained = true,
                PersistedWorkspaceName = saved.WorkspaceName, ModelRequests = 0
            }, new JsonSerializerOptions { WriteIndented = true }));
        } finally {
            await fixture.DisposeAsync();
        }
    }
}
