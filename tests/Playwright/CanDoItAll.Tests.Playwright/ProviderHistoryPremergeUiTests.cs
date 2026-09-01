using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using Microsoft.EntityFrameworkCore;
using Microsoft.Playwright;
using Xunit.Abstractions;
using PersistedProvider = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfile;

namespace CanDoItAll.Tests.Playwright;

[Collection(PlaywrightCollection.Name)]
[Trait("Category", "Playwright")]
public sealed class ProviderHistoryPremergeUiTests(PlaywrightAppFixture fixture, ITestOutputHelper output) {
    private const string ProviderName = "Premerge visual fixture";

    [Fact]
    public async Task Isolated_history_paging_details_and_policy_remain_explicit() {
        Assert.False(string.IsNullOrWhiteSpace(fixture.DatabaseConnectionString),
            "This test may seed only the disposable database owned by its fixture.");
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        var factory = new ContextFactory(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.DatabaseConnectionString).Options);
        var providerId = await SeedVisualRowsAsync(factory);
        var evidence = Path.Combine(
            PlaywrightTestHostPaths.RepositoryRoot,
            "artifacts",
            "playwright",
            "provider-history");
        Directory.CreateDirectory(evidence);
        await using var context = await fixture.Browser.NewContextAsync(new() {
            ViewportSize = new() { Width = 1920, Height = 1080 }, DeviceScaleFactor = 1
        });
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(30_000);
        try {
            await page.GotoAsync($"{fixture.BaseUrl}/agents?tab=request-history");
            await page.GetByTestId("database-startup-continue").ClickAsync();
            await Assertions.Expect(page.GetByTestId("database-startup-modal")).ToHaveCountAsync(0);
            await Assertions.Expect(page.GetByTestId("provider-request-history")).ToContainTextAsync("History not requested");
            await Assertions.Expect(page.GetByTestId("history-results")).ToHaveCountAsync(0);
            await page.GetByTestId("history-provider").FillAsync(providerId.ToString());
            await page.GetByTestId("history-more-filters").ClickAsync();
            await page.GetByTestId("history-page-size").FillAsync("10");
            await page.GetByTestId("history-more-filters").ClickAsync();
            await page.GetByTestId("history-search").ClickAsync();
            await Assertions.Expect(page.GetByTestId("history-details")).ToHaveCountAsync(10);
            await CaptureAsync("history-normal");
            await page.GetByTestId("history-next").ClickAsync();
            await Assertions.Expect(page.GetByTestId("history-results")).ToContainTextAsync("Page 2");
            await Assertions.Expect(page.GetByTestId("history-details")).ToHaveCountAsync(5);
            await page.GetByTestId("history-previous").ClickAsync();
            await Assertions.Expect(page.GetByTestId("history-details")).ToHaveCountAsync(10);
            await page.GetByTestId("history-details").First.ClickAsync();
            await Assertions.Expect(page.GetByTestId("history-detail-dialog")).ToContainTextAsync(ProviderName);
            await CaptureAsync("history-details", "history-detail-dialog");
            await page.GetByTestId("history-load-content").ClickAsync();
            await Assertions.Expect(page.GetByTestId("history-content-state")).ToContainTextAsync("NotCaptured");
            await CaptureAsync("history-content-light", "history-content-dialog");
            await page.GetByTestId("history-content-close").ClickAsync();
            await page.GetByTestId("history-detail-close").ClickAsync();

            await page.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^Providers") }).ClickAsync();
            await page.GetByTestId("providers-search").FillAsync(ProviderName);
            await page.GetByTestId("providers-tree-provider").Filter(new() { HasText = ProviderName }).First.ClickAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = ProviderName, Exact = true })).ToBeVisibleAsync();
            await page.GetByTestId("provider-editor-tab-history").ClickAsync();
            await Assertions.Expect(page.GetByTestId("provider-request-history")).ToContainTextAsync("History not requested");
            await page.GetByTestId("history-search").ClickAsync();
            await Assertions.Expect(page.GetByTestId("history-results")).ToContainTextAsync(ProviderName);
            await CaptureAsync("provider-history");

            await page.GetByTestId("shell-settings-action").ClickAsync();
            await page.GetByRole(AriaRole.Button, new() { Name = "Provider history", Exact = true }).ClickAsync();
            await Assertions.Expect(page.GetByTestId("history-policy-panel")).ToContainTextAsync("Policy not requested");
            await page.GetByTestId("history-policy-load").ClickAsync();
            await Assertions.Expect(page.GetByTestId("history-policy-form")).ToBeVisibleAsync();
            await CaptureAsync("policy-light");
            await page.GetByTestId("history-policy-mode").SelectOptionAsync(new SelectOptionValue { Label = "Detailed" });
            await Assertions.Expect(page.GetByTestId("history-policy-panel"))
                .ToContainTextAsync("Detailed capture can contain sensitive user text");
            await page.GetByTestId("history-policy-apply").ClickAsync();
            await Assertions.Expect(page.GetByTestId("history-policy-success")).ToBeVisibleAsync();
            await CaptureAsync("policy-detailed");
            await page.GetByTestId("history-policy-metadata-days").FillAsync("2");
            await page.GetByTestId("history-policy-detail-days").FillAsync("1");
            await page.GetByTestId("history-policy-preview").ClickAsync();
            await Assertions.Expect(page.GetByTestId("history-policy-confirmation")).ToContainTextAsync("Canonical conversations are unchanged");
            await CaptureAsync("policy-retention-preview", "history-policy-confirmation");
            await page.GetByTestId("history-policy-confirmation")
                .GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
            await Assertions.Expect(page.GetByTestId("history-policy-confirmation")).ToHaveCountAsync(0);
            await using var db = factory.CreateDbContext();
            var policy = await db.Set<HistoryPolicyRow>().SingleAsync();
            Assert.Equal(HistoryCaptureMode.Detailed, policy.CaptureMode);
            Assert.Equal(30, policy.MetadataRetentionDays);
            Assert.Equal(7, policy.DetailRetentionDays);
            Assert.Equal(15, await db.Set<HistoryEntryRow>().CountAsync(row => row.ProviderId == providerId));
            output.WriteLine(JsonSerializer.Serialize(new {
                fixture.BaseUrl, Viewport = "1920x1080", Rows = 15,
                Scope = "Disposable visual fixture; does not prove provider production or multi-instance transport."
            }));
        } catch {
            await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, "failure.png"), FullPage = false });
            output.WriteLine(fixture.GetLogSnapshot(50));
            throw;
        }

        async Task CaptureAsync(string name, string? dialog = null) {
            await Assertions.Expect(page.Locator("#blazor-error-ui")).Not.ToBeVisibleAsync();
            if (dialog is not null) {
                var bounds = await page.GetByTestId(dialog).BoundingBoxAsync();
                Assert.NotNull(bounds);
                Assert.InRange(bounds.X, 0, 1920);
                Assert.InRange(bounds.Y, 0, 1080);
                Assert.True(bounds.X + bounds.Width <= 1921 && bounds.Y + bounds.Height <= 1081);
            }
            await page.ScreenshotAsync(new() { Path = Path.Combine(evidence, $"{name}.png"), FullPage = false });
        }
    }

    private static async Task<Guid> SeedVisualRowsAsync(ContextFactory factory) {
        var partition = await new HistoryPartitionStore(factory).GetAsync(default);
        await using var db = factory.CreateDbContext();
        var profile = new PersistedProvider {
            Name = ProviderName, ConnectorPluginKey = ProviderConnectorKeys.OpenAi,
            ConfigSchemaVersion = "1.0", BaseUrl = "https://example.invalid/v1",
            DefaultModel = "visual-model", IsEnabled = true
        };
        db.Add(profile);
        var now = DateTimeOffset.UtcNow.AddMinutes(-1);
        // Deliberate visual fixture: producer and lifecycle behavior is proved by integration tests.
        for (var index = 0; index < 15; index++) {
            var started = now.AddSeconds(-index);
            db.Add(new HistoryEntryRow {
                Id = Guid.NewGuid(), PartitionId = partition.StorageLineageId,
                RequestId = Guid.NewGuid(), AttemptId = Guid.NewGuid(),
                Granularity = HistoryGranularity.ProviderCallAttempt, TimeBasis = HistoryTimeBasis.AttemptStarted,
                SortAtUtc = started, StartedAtUtc = started, FinishedAtUtc = started.AddMilliseconds(500),
                ProviderId = profile.Id, ProviderName = ProviderName, ProviderKind = "OpenAI",
                RequestedModel = "visual-model", ResolvedModel = "visual-model",
                Operation = HistoryOperation.CompleteChat, Workload = HistoryWorkload.Direct,
                Outcome = HistoryOutcome.Succeeded, AuthenticationKind = HistoryAuthenticationKind.TrustedLocalOperator,
                UsageState = HistoryUsageState.Complete, InputTokens = 17, OutputTokens = 23,
                PriceState = HistoryPriceState.CalculatedAtExecution, Amount = 0.001m, Currency = "USD",
                MetadataAuthority = HistoryMetadataAuthority.Standalone, RetentionAuthority = HistoryRetentionAuthority.HistoryPolicy,
                DetailState = HistoryDetailState.NotCaptured, ExpiresAtUtc = started.AddDays(30),
                Version = 1
            });
        }
        await db.SaveChangesAsync();
        return profile.Id;
    }

    private sealed class ContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext> {
        public AppDbContext CreateDbContext() => new(options);
        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateDbContext());
    }
}
