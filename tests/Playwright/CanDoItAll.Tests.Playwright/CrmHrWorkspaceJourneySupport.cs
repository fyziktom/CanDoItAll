using System.Text.RegularExpressions;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

// Shared plumbing of the CRM / HR production journeys: one owner-side service provider per journey (seeding and owner
// readback against the fixture's PostgreSQL profile), the browser context with its trace and failure evidence, the
// interactive-readiness waits of the paged catalogs, and the constrained-width check of an open record dialog.
internal static class CrmHrWorkspaceJourneySupport
{
    public const int DefaultTimeoutMs = 30_000;

    private const string OverflowProbe = """
        () => ({
            scrollWidth: document.documentElement.scrollWidth,
            clientWidth: document.documentElement.clientWidth
        })
        """;

    public static string ArtifactsDirectory(string journeyFolder)
        => Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", journeyFolder);

    public static string NewSuffix()
        => Guid.NewGuid().ToString("N")[..8];

    // The provider the journey seeds with and reads the owner back through. Every service call opens its own
    // database context, so a readback after a UI action observes what the Web host committed.
    public static Task<ServiceProvider> BuildOwnerProviderAsync(PlaywrightAppFixture fixture, string profileName)
        => TestApplicationBootstrap.BuildServiceProviderAsync(
            CreateActiveProfile(fixture, profileName),
            "CanDoItAll.Tests.Playwright.Seed",
            TestSchemaBootstrapModules.Full,
            new Dictionary<string, string?>
            {
                ["DevelopmentManager:TuningModeEnabled"] = "false"
            });

    // One journey in one browser context at 1600x1000 with a Playwright trace. Every browser-side failure counts
    // (CrmHrBrowserOracle); the expected circuit teardown of the journey's own navigations is written next to the
    // captures. A failing journey leaves a screenshot, the oracle's findings and the host log tail behind.
    public static async Task RunAsync(
        PlaywrightAppFixture fixture,
        string artifactsDir,
        Func<IPage, CrmHrBrowserOracle, Task> journey)
    {
        Directory.CreateDirectory(artifactsDir);
        foreach (var stale in new[] { "failure.png", "failure-exception.txt", "failure-browser-oracle.txt", "failure-host-log.txt" })
        {
            File.Delete(Path.Combine(artifactsDir, stale));
        }

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1600, Height = 1000 }
        });
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true });
        try
        {
            var page = await context.NewPageAsync();
            var oracle = CrmHrBrowserOracle.Attach(page);
            try
            {
                await journey(page, oracle);
                await File.WriteAllLinesAsync(Path.Combine(artifactsDir, "expected-teardown.txt"), oracle.ExpectedTeardown);
                await oracle.AssertCleanAsync();
            }
            catch (Exception failure)
            {
                await CaptureFailureEvidenceAsync(fixture, page, oracle, artifactsDir, failure);
                throw;
            }
        }
        finally
        {
            await context.Tracing.StopAsync(new() { Path = Path.Combine(artifactsDir, "trace.zip") });
        }
    }

    // A full-page navigation of the journey itself, followed by the database startup prompt of a fresh document.
    public static async Task NavigateAsync(PlaywrightAppFixture fixture, IPage page, CrmHrBrowserOracle oracle, string relativeUrl)
    {
        var response = await oracle.NavigateAsync($"{fixture.BaseUrl}{relativeUrl}");
        Assert.True(response?.Ok, $"Expected {relativeUrl} to return 2xx, got {(int?)response?.Status}.");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
    }

    // The paged record browser keeps its search box disabled until its first interactive render, so an enabled
    // search box proves that the circuit handles events for the page.
    public static async Task<ILocator> WaitForCatalogAsync(IPage page, string browserTestId)
    {
        var search = page.GetByTestId($"{browserTestId}-search");
        await Assertions.Expect(search).ToBeEnabledAsync(new() { Timeout = DefaultTimeoutMs });
        return search;
    }

    // Narrows a server-paged catalog to the one record with the unique name and returns its card. The card carries
    // no record key, so the identity is the unique title: exactly one card, labelled with exactly that name.
    public static async Task<ILocator> FindSingleCardAsync(IPage page, string browserTestId, string optionTestId, string uniqueName)
    {
        var search = await WaitForCatalogAsync(page, browserTestId);
        await search.FillAsync(uniqueName);
        var cards = page.GetByTestId(browserTestId).GetByTestId(optionTestId);
        await Assertions.Expect(cards).ToHaveCountAsync(1, new() { Timeout = DefaultTimeoutMs });
        await Assertions.Expect(cards).ToHaveAttributeAsync("aria-label", $"Select {uniqueName}");
        await Assertions.Expect(cards).ToBeEnabledAsync();
        return cards;
    }

    public static Task PickPartyAsync(IPage page, string pickerTestId, string uniqueName, Guid partyId)
        => PickRecordAsync(page, pickerTestId, uniqueName, $"crmhr-party-option-{partyId:N}");

    public static Task PickProjectAsync(IPage page, string pickerTestId, string uniqueName, Guid projectId)
        => PickRecordAsync(page, pickerTestId, uniqueName, $"crmhr-project-option-{projectId:N}");

    // Chooses one record in an open paged picker dialog: search by the unique name, select the option that carries
    // exactly the expected record identifier, confirm once, and wait for the picker to close.
    private static async Task PickRecordAsync(IPage page, string pickerTestId, string uniqueName, string optionTestId)
    {
        var picker = page.GetByTestId(pickerTestId);
        await Assertions.Expect(picker).ToBeVisibleAsync(new() { Timeout = DefaultTimeoutMs });
        var search = picker.GetByTestId($"{pickerTestId}-browser-search");
        await Assertions.Expect(search).ToBeEnabledAsync(new() { Timeout = DefaultTimeoutMs });
        await search.FillAsync(uniqueName);
        var options = picker.GetByTestId($"{pickerTestId}-browser").Locator("button.paged-record-browser__select");
        await Assertions.Expect(options).ToHaveCountAsync(1, new() { Timeout = DefaultTimeoutMs });
        var option = picker.GetByTestId(optionTestId);
        await Assertions.Expect(option).ToHaveAttributeAsync("aria-label", $"Select {uniqueName}");
        await Assertions.Expect(option).ToBeEnabledAsync();
        await option.ClickAsync();
        await Assertions.Expect(option).ToHaveAttributeAsync("aria-pressed", "true");
        await Assertions.Expect(picker.GetByTestId($"{pickerTestId}-selection-summary")).ToHaveTextAsync("1 record selected");
        await picker.GetByTestId($"{pickerTestId}-confirm").ClickAsync();
        await Assertions.Expect(picker).ToHaveCountAsync(0, new() { Timeout = DefaultTimeoutMs });
    }

    // Blazor navigates client-side, so the URL is asserted with a retrying expectation. Playwright translates the
    // expression for the browser and accepts no .NET-only options.
    public static Task ExpectUrlAsync(IPage page, string fragment)
        => Assertions.Expect(page).ToHaveURLAsync(
            new Regex(Regex.Escape(fragment)),
            new() { Timeout = DefaultTimeoutMs });

    // A toast is only the signal that the host finished the action; the receipt is always the owner readback.
    public static async Task ExpectToastAsync(IPage page, string text)
    {
        var toast = page.Locator(".rz-notification").GetByText(text, new() { Exact = true });
        await Assertions.Expect(toast.First).ToBeVisibleAsync(new() { Timeout = DefaultTimeoutMs });
    }

    // Success toasts dismiss themselves; waiting for that keeps the next identical toast attributable to the next action.
    public static Task ExpectToastGoneAsync(IPage page, string text)
        => Assertions.Expect(page.Locator(".rz-notification").GetByText(text, new() { Exact = true }))
            .ToHaveCountAsync(0, new() { Timeout = DefaultTimeoutMs });

    // Constrained width: the open record dialog and its primary actions stay visible (an action inside the dialog's
    // scrolling body is brought into view the way an operator would reach it) and the document does not scroll
    // horizontally. The viewport returns to 1600x1000 afterwards.
    public static async Task AssertDialogFitsConstrainedWidthAsync(
        IPage page,
        string dialogTestId,
        IReadOnlyList<string> primaryActionTestIds,
        string screenshotPath)
    {
        await page.SetViewportSizeAsync(1100, 900);
        await page.WaitForTimeoutAsync(500);
        var dialog = page.GetByTestId(dialogTestId);
        await Assertions.Expect(dialog).ToBeVisibleAsync();
        foreach (var actionTestId in primaryActionTestIds)
        {
            var action = dialog.GetByTestId(actionTestId);
            await Assertions.Expect(action).ToBeVisibleAsync();
            await action.ScrollIntoViewIfNeededAsync();
            await Assertions.Expect(action).ToBeInViewportAsync(new() { Ratio = 1 });
        }

        var probe = await page.EvaluateAsync<System.Text.Json.JsonElement>(OverflowProbe);
        var scrollWidth = probe.GetProperty("scrollWidth").GetInt32();
        var clientWidth = probe.GetProperty("clientWidth").GetInt32();
        Assert.True(scrollWidth <= clientWidth, $"The document overflows horizontally at 1100 px: scrollWidth {scrollWidth} > clientWidth {clientWidth}.");
        await page.ScreenshotAsync(new PageScreenshotOptions { Path = screenshotPath });
        await page.SetViewportSizeAsync(1600, 1000);
        await page.WaitForTimeoutAsync(500);
    }

    public static async Task<Guid> CreatePartyAsync(
        PartyDirectoryService partyDirectoryService,
        string displayName,
        PartyType partyType,
        PartyRoleKind roleKind,
        string email,
        PartyLifecycleStatus lifecycleStatus = PartyLifecycleStatus.Active,
        string? summary = null)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = lifecycleStatus,
            DisplayName = displayName,
            Summary = summary ?? $"{displayName} summary",
            LastChangedBy = "playwright-tests",
            Roles =
            [
                new PartyRoleAssignmentEditorModel
                {
                    RoleKind = roleKind,
                    Title = roleKind.ToString(),
                    IsPrimary = true
                }
            ],
            ContactPoints =
            [
                new PartyContactPointEditorModel
                {
                    ContactType = PartyContactType.Email,
                    Label = "Primary email",
                    Value = email,
                    NormalizedValue = email.ToLowerInvariant(),
                    IsPrimary = true,
                    IsPublic = true
                }
            ]
        });

        Assert.True(result.IsSuccess, string.Join(" ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    private static async Task CaptureFailureEvidenceAsync(
        PlaywrightAppFixture fixture,
        IPage page,
        CrmHrBrowserOracle oracle,
        string artifactsDir,
        Exception failure)
    {
        await File.WriteAllTextAsync(Path.Combine(artifactsDir, "failure-exception.txt"), $"{page.Url}{Environment.NewLine}{failure}");
        try
        {
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "failure.png") });
        }
        catch (PlaywrightException)
        {
        }

        try
        {
            await oracle.AssertCleanAsync();
        }
        catch (Exception oracleFinding) when (oracleFinding is Xunit.Sdk.XunitException or PlaywrightException)
        {
            await File.WriteAllTextAsync(Path.Combine(artifactsDir, "failure-browser-oracle.txt"), oracleFinding.Message);
        }

        await File.WriteAllTextAsync(Path.Combine(artifactsDir, "failure-host-log.txt"), fixture.GetLogSnapshot(400));
    }

    private static TestDatabaseProfile CreateActiveProfile(PlaywrightAppFixture fixture, string profileName)
    {
        if (string.IsNullOrWhiteSpace(fixture.DatabaseConnectionString))
        {
            throw new InvalidOperationException("Playwright fixture did not expose a database connection string.");
        }

        if (string.IsNullOrWhiteSpace(fixture.StorageWorkspaceRoot))
        {
            throw new InvalidOperationException("Playwright fixture did not expose the storage workspace root.");
        }

        var workspaceRoot = fixture.StorageWorkspaceRoot;
        var profileRoot = Directory.GetParent(workspaceRoot)?.FullName
            ?? throw new InvalidOperationException($"Could not resolve profile root from '{workspaceRoot}'.");
        var environmentRoot = Path.GetFullPath(Path.Combine(profileRoot, "..", ".."));

        return new TestDatabaseProfile(
            profileName,
            environmentRoot,
            profileRoot,
            TestDatabaseProviderKind.PostgreSql,
            fixture.DatabaseConnectionString,
            workspaceRoot,
            Path.Combine(profileRoot, "manager-artifacts"));
    }
}
