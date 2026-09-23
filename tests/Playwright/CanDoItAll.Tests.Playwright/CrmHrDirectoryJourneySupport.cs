using System.Text.RegularExpressions;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

// Shared plumbing of the CRM / HR production journeys: the owner-side service provider bound to the fixture's
// disposable profile, synthetic party seeding, navigation through the browser oracle, the interactive-readiness
// waits, and the constrained-width probe. Nothing here touches component state; every helper either reads the DOM or
// drives a real control.
internal static class CrmHrDirectoryJourneySupport
{
    public const string Actor = "playwright-journeys";

    // The SecondaryTabs buttons of the area navigation carry no data-testid and no aria state; the selected one is
    // told apart by the filled style only.
    public const string ActiveAreaTabClass = "bg-slate-900";

    public static readonly IReadOnlyList<string> AreaTabLabels =
        ["Home", "Directory", "CRM", "Workforce", "Recruiting", "Agents", "Assignments"];

    private const string OverflowProbe = """
        () => ({
            scrollWidth: document.documentElement.scrollWidth,
            clientWidth: document.documentElement.clientWidth
        })
        """;

    public static Task<ServiceProvider> BuildOwnerProviderAsync(PlaywrightAppFixture fixture, string profileName)
        => TestApplicationBootstrap.BuildServiceProviderAsync(
            CreateActiveProfile(fixture, profileName),
            "CanDoItAll.Tests.Playwright.Journeys",
            TestSchemaBootstrapModules.Full,
            new Dictionary<string, string?>
            {
                ["DevelopmentManager:TuningModeEnabled"] = "false"
            });

    public static async Task<Guid> CreatePartyAsync(
        PartyDirectoryService partyDirectoryService,
        string displayName,
        PartyType partyType,
        PartyRoleKind roleKind,
        string email,
        string? summary = null,
        Action<PartyEditorModel>? configure = null)
    {
        var model = new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = summary ?? $"{displayName} summary",
            LastChangedBy = Actor,
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
        };
        configure?.Invoke(model);

        var result = await partyDirectoryService.SavePartyAsync(model);
        Assert.True(result.IsSuccess, string.Join(" ", result.Errors.Select(error => error.Message)));
        return result.Value;
    }

    // Owner readback by exact identity: a fresh scope per read, so nothing cached by an earlier read answers.
    public static async Task<PartyEditorModel> ReadPartyAsync(ServiceProvider owner, Guid partyId)
    {
        await using var scope = owner.CreateAsyncScope();
        var party = await scope.ServiceProvider.GetRequiredService<PartyDirectoryService>().GetPartyAsync(partyId);
        Assert.True(party is not null, $"The owner holds no party '{partyId:D}'.");
        return party!;
    }

    public static async Task<PartyEditorModel?> FindPartyAsync(ServiceProvider owner, Guid partyId)
    {
        await using var scope = owner.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PartyDirectoryService>().GetPartyAsync(partyId);
    }

    public static async Task<IReadOnlyList<PartyDirectoryListItemModel>> ListDirectoryAsync(ServiceProvider owner)
    {
        await using var scope = owner.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PartyDirectoryService>().ListDirectoryAsync();
    }

    // A full-page navigation of the journey: through the oracle, with a 2xx document and the database startup prompt
    // of the tab settled.
    public static async Task OpenAsync(CrmHrBrowserOracle oracle, IPage page, string url)
    {
        var response = await oracle.NavigateAsync(url);
        Assert.True(response?.Ok, $"Expected {url} to return 2xx, got {(int?)response?.Status}.");
        try
        {
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        }
        catch (TimeoutException)
        {
            // A circuit that ended while the page loaded never shows the prompt; name that cause instead of the wait.
            if (await page.Locator("#blazor-error-ui").IsVisibleAsync())
            {
                Assert.Fail($"The circuit of {url} ended with the Blazor error UI before the shell became interactive.");
            }

            throw;
        }
    }

    // The paged record browser keeps its search box disabled until its first interactive render, so an enabled search
    // box means the circuit handles events; the catalog is then awaited until its first page is accepted.
    public static async Task WaitForCatalogAsync(IPage page, string browserTestId)
    {
        await Assertions.Expect(page.GetByTestId($"{browserTestId}-search")).ToBeEnabledAsync(new() { Timeout = 30_000 });
        await Assertions.Expect(page.GetByTestId($"{browserTestId}-loading")).ToHaveCountAsync(0, new() { Timeout = 30_000 });
    }

    // Types into the catalog search and waits for the debounced server page to hold exactly the one expected card.
    public static async Task<ILocator> SearchCatalogForAsync(
        IPage page,
        string browserTestId,
        string optionTestId,
        string title)
    {
        await page.GetByTestId($"{browserTestId}-search").FillAsync(title);
        var card = CatalogCard(page, browserTestId, optionTestId, title);
        await Assertions.Expect(card).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Assertions.Expect(page.GetByTestId(browserTestId).GetByTestId(optionTestId))
            .ToHaveCountAsync(1, new() { Timeout = 30_000 });
        return card;
    }

    // Types a search that must match nothing and waits for the catalog's accepted empty page.
    public static async Task SearchCatalogExpectingNoneAsync(IPage page, string browserTestId, string optionTestId, string searchText)
    {
        await page.GetByTestId($"{browserTestId}-search").FillAsync(searchText);
        await Assertions.Expect(page.GetByTestId($"{browserTestId}-empty")).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Assertions.Expect(page.GetByTestId(browserTestId).GetByTestId(optionTestId)).ToHaveCountAsync(0);
    }

    // A catalog card is the select button of the paged record browser; its accessible name quotes the record title.
    public static ILocator CatalogCard(IPage page, string browserTestId, string optionTestId, string title)
        => page.GetByTestId(browserTestId).Locator($"[data-testid='{optionTestId}'][aria-label='Select {title}']");

    public static ILocator AreaTab(IPage page, string label)
        => page.GetByRole(AriaRole.Button, new() { Name = label, Exact = true })
            .And(page.Locator("button.rounded-full"));

    public static async Task AssertActiveAreaAsync(IPage page, string activeLabel)
    {
        foreach (var label in AreaTabLabels)
        {
            var tab = AreaTab(page, label);
            await Assertions.Expect(tab).ToBeVisibleAsync(new() { Timeout = 30_000 });
            if (string.Equals(label, activeLabel, StringComparison.Ordinal))
            {
                await Assertions.Expect(tab).ToHaveClassAsync(new Regex($@"(^|\s){ActiveAreaTabClass}(\s|$)"));
            }
            else
            {
                await Assertions.Expect(tab).Not.ToHaveClassAsync(new Regex($@"(^|\s){ActiveAreaTabClass}(\s|$)"));
            }
        }
    }

    public static ILocator PageHeading(IPage page, string title)
        => page.GetByRole(AriaRole.Heading, new() { Name = title, Exact = true, Level = 1 });

    public static ILocator Toast(IPage page, string text)
        => page.Locator(".rz-notification").GetByText(text, new() { Exact = true });

    // Whether the middle of the element is painted by the element itself, i.e. nothing else covers it for the user.
    // A read-only probe: Playwright's visibility does not account for a modal dialog's top layer.
    public static Task<bool> IsPaintedOnTopAsync(ILocator locator)
        => locator.EvaluateAsync<bool>(
            """
            element => {
                const box = element.getBoundingClientRect();
                const top = document.elementFromPoint(box.left + box.width / 2, box.top + box.height / 2);
                return top !== null && (element === top || element.contains(top));
            }
            """);

    // Playwright translates the expression for the browser and accepts no .NET-only options such as CultureInvariant.
    public static Regex RouteUrl(string route, string? query = null)
        => new(
            "^https?://[^/]+" + Regex.Escape(route) + (query is null ? "$" : Regex.Escape("?" + query) + "$"),
            RegexOptions.IgnoreCase);

    // The record dialog and its primary actions at a constrained width, and no horizontal overflow of the document.
    public static async Task AssertConstrainedLayoutAsync(
        IPage page,
        CrmHrJourneyEvidence evidence,
        string screenshotName,
        params ILocator[] mustStayInViewport)
    {
        await page.SetViewportSizeAsync(1100, 900);
        await page.WaitForTimeoutAsync(400);
        foreach (var locator in mustStayInViewport)
        {
            await Assertions.Expect(locator).ToBeVisibleAsync();
            await Assertions.Expect(locator).ToBeInViewportAsync();
        }

        var probe = await page.EvaluateAsync<System.Text.Json.JsonElement>(OverflowProbe);
        var scrollWidth = probe.GetProperty("scrollWidth").GetInt32();
        var clientWidth = probe.GetProperty("clientWidth").GetInt32();
        Assert.True(
            scrollWidth <= clientWidth,
            $"The document overflows horizontally at 1100 px: scrollWidth {scrollWidth} > clientWidth {clientWidth}.");
        await evidence.ScreenshotAsync(page, screenshotName);
        await page.SetViewportSizeAsync(1600, 1000);
        await page.WaitForTimeoutAsync(400);
    }

    public static Guid ReadGuidQuery(string url, string name)
    {
        var match = Regex.Match(
            url,
            $@"[?&]{Regex.Escape(name)}=([0-9a-fA-F-]{{32,36}})",
            RegexOptions.CultureInvariant);
        Assert.True(match.Success, $"The URL '{url}' carries no '{name}' identifier.");
        return Guid.Parse(match.Groups[1].Value);
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

// The evidence of one journey under output/playwright (git-ignored): named screenshots, the Playwright trace, the
// circuit teardown the oracle kept apart, and on a failure the exception with the tail of the host log.
internal sealed class CrmHrJourneyEvidence
{
    private readonly string directory;

    private CrmHrJourneyEvidence(string directory)
    {
        this.directory = directory;
    }

    public static CrmHrJourneyEvidence Create(string journeyFolder)
    {
        var directory = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", journeyFolder);
        Directory.CreateDirectory(directory);

        // A failure record of an earlier run must not sit next to the evidence of this one.
        foreach (var staleFailure in Directory.EnumerateFiles(directory, "failure*"))
        {
            File.Delete(staleFailure);
        }

        return new CrmHrJourneyEvidence(directory);
    }

    public Task StartTraceAsync(IBrowserContext context)
        => context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true });

    public Task StopTraceAsync(IBrowserContext context)
        => context.Tracing.StopAsync(new() { Path = PathOf("trace.zip") });

    public Task ScreenshotAsync(IPage page, string fileName)
        => page.ScreenshotAsync(new PageScreenshotOptions { Path = PathOf(fileName) });

    public Task WriteExpectedTeardownAsync(IEnumerable<string> teardown)
        => WriteLinesAsync("expected-teardown.txt", teardown);

    public Task WriteLinesAsync(string fileName, IEnumerable<string> lines)
        => File.WriteAllLinesAsync(PathOf(fileName), lines);

    public async Task WriteFailureAsync(Exception failure, PlaywrightAppFixture fixture, params IPage[] pages)
    {
        await File.WriteAllTextAsync(
            PathOf("failure.txt"),
            failure + Environment.NewLine + Environment.NewLine + "Host log tail:" + Environment.NewLine + fixture.GetLogSnapshot(400));
        for (var index = 0; index < pages.Length; index++)
        {
            if (!pages[index].IsClosed)
            {
                await ScreenshotAsync(pages[index], $"failure-{index + 1}.png");
            }
        }
    }

    private string PathOf(string fileName) => Path.Combine(directory, fileName);
}
