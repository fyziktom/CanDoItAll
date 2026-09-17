using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

// What the CRM / HR and Project Structure journeys share: the seed provider of the fixture's profile, synthetic
// parties and projects, the interactive-circuit probe for pages without a readiness attribute, the shared paged
// picker, and the read-back of a Gantt chart whose bars are painted on a canvas.
internal static class CrmHrAssignmentsJourneySupport
{
    private static readonly Regex PrerenderedTabId = new("\\sid=\"(?<id>cad-tabs-[0-9a-f]{32}-tab-[0-9]+)\"", RegexOptions.CultureInvariant);

    // Tabs carry a per-instance element id. The prerendered instance and the circuit's instance never share it, so
    // tabs whose ids are all absent from the prerendered document were rendered by the interactive circuit.
    private const string InteractiveTabsProbe = """
        args => {
            const tabs = Array.from(document.querySelectorAll(args.selector));
            return tabs.length > 0 && tabs.every(tab => tab.id.length > 0 && !args.prerendered.includes(tab.id));
        }
        """;

    public const string OverflowProbe = """
        () => document.documentElement.scrollWidth > document.documentElement.clientWidth
        """;

    // The chart rows are DOM; the bars are painted on the chart canvas. One horizontal line through the upper part of
    // every bar (above its centered label) is read back from the canvas: a bar is the longest run of pixels that
    // differ from the row background. Grid ticks are one pixel wide and never win against a bar.
    private const string GanttProbe = """
        rootTestId => {
            const root = document.querySelector(`[data-testid="${rootTestId}"]`);
            const canvas = root?.querySelector('canvas.cda-gantt__canvas');
            if (!root || !canvas) { return { present: false, rows: [] }; }
            const rows = Array.from(root.querySelectorAll('.cda-gantt__table-row:not(.cda-gantt__table-row--header)'));
            const cssWidth = parseFloat(canvas.style.width);
            const cssHeight = parseFloat(canvas.style.height);
            const scaleX = canvas.width / cssWidth;
            const scaleY = canvas.height / cssHeight;
            const context = canvas.getContext('2d');
            const headerHeight = 40, rowHeight = 48, barHeight = 26;
            const differs = (data, offset, reference) =>
                Math.abs(data[offset] - reference[0]) > 12 ||
                Math.abs(data[offset + 1] - reference[1]) > 12 ||
                Math.abs(data[offset + 2] - reference[2]) > 12;
            const box = canvas.getBoundingClientRect();
            return {
                present: true,
                canvasBackingWidth: canvas.width,
                canvasBackingHeight: canvas.height,
                canvasBoxWidth: box.width,
                canvasBoxHeight: box.height,
                rows: rows.map((row, index) => {
                    const rowBox = row.getBoundingClientRect();
                    const y = Math.round((headerHeight + index * rowHeight + (rowHeight - barHeight) / 2 + 3) * scaleY);
                    const line = context.getImageData(0, y, canvas.width, 1).data;
                    // The row background is the most frequent colour of the leading gutter, where no bar can start.
                    const votes = new Map();
                    for (let x = 0; x < Math.min(48, canvas.width); x += 1) {
                        const key = `${line[x * 4]},${line[x * 4 + 1]},${line[x * 4 + 2]}`;
                        votes.set(key, (votes.get(key) ?? 0) + 1);
                    }
                    const reference = Array.from(votes.entries()).sort((a, b) => b[1] - a[1])[0][0].split(',').map(Number);
                    let best = { start: -1, length: 0 }, start = -1;
                    for (let x = 0; x <= canvas.width; x += 1) {
                        const painted = x < canvas.width && differs(line, x * 4, reference);
                        if (painted && start < 0) { start = x; }
                        if (!painted && start >= 0) {
                            if (x - start > best.length) { best = { start, length: x - start }; }
                            start = -1;
                        }
                    }
                    return {
                        title: (row.querySelector('.cda-gantt__task-button')?.textContent ?? '').trim(),
                        assignments: Array.from(row.querySelectorAll('.cda-gantt__assignment-tooltip')).map(item => item.textContent.replace(/\s+/g, ' ').trim()),
                        width: rowBox.width,
                        height: rowBox.height,
                        barStart: best.start / scaleX,
                        barWidth: best.length / scaleX
                    };
                })
            };
        }
        """;

    // "1 relationships" is also a substring of "11 relationships": a count is matched as a whole number.
    public static Regex Count(int count, string noun)
        => new($"(?<![0-9]){count.ToString(CultureInfo.InvariantCulture)} {Regex.Escape(noun)}");

    public static string IsoDate(DateOnly value)
        => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Join(IEnumerable<CanDoItAll.SharedKernel.Error> errors)
        => string.Join(" ", errors.Select(error => error.Message));

    // A full-page load issued by the journey. The startup prompt of a fresh browser context needs the circuit to appear
    // and to close; later loads of the same tab prove the circuit through the per-instance ids of the page's tabs.
    public static async Task OpenAsync(IPage page, CrmHrBrowserOracle oracle, string url, string tabSelector)
    {
        var response = await oracle.NavigateAsync(url);
        Assert.NotNull(response);
        Assert.True(response!.Ok, $"Expected {url} to return 2xx, got {response.Status}.");
        await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
        var prerendered = PrerenderedTabId.Matches(await response.TextAsync()).Select(match => match.Groups["id"].Value).ToArray();
        await page.WaitForFunctionAsync(InteractiveTabsProbe, new { selector = tabSelector, prerendered }, new() { Timeout = 60_000 });
    }

    // The shared paged picker: search the unique name, wait for the filtered page, select the card, confirm once.
    public static async Task PickRecordAsync(IPage page, string dialogTestId, string searchText, string optionTestId)
    {
        var dialog = page.GetByTestId(dialogTestId);
        await Assertions.Expect(dialog).ToBeVisibleAsync();
        var search = page.GetByTestId($"{dialogTestId}-browser-search");
        await Assertions.Expect(search).ToBeEnabledAsync();
        await search.FillAsync(searchText);
        await Assertions.Expect(page.GetByTestId($"{dialogTestId}-browser")).ToContainTextAsync(Count(1, "matching record(s)"));
        var option = dialog.GetByTestId(optionTestId);
        await option.ClickAsync();
        await Assertions.Expect(option).ToHaveAttributeAsync("aria-pressed", "true");
        await Assertions.Expect(page.GetByTestId($"{dialogTestId}-selection-summary")).ToContainTextAsync("1 record selected");
        await page.GetByTestId($"{dialogTestId}-confirm").ClickAsync();
        await Assertions.Expect(dialog).ToHaveCountAsync(0);
    }

    // Resolves once the chart has a row with a painted bar for every expected title, so the read-back never sees a
    // canvas that is still blank.
    public static async Task<JsonElement> ReadGanttAsync(IPage page, string rootTestId, IReadOnlyList<string> expectedTitles)
    {
        await Assertions.Expect(page.GetByTestId(rootTestId)).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await page.WaitForFunctionAsync(
            $"args => {{ const probe = ({GanttProbe})(args.root); return probe.present && args.titles.every(title => probe.rows.some(row => row.title === title && row.barWidth > 8)); }}",
            new { root = rootTestId, titles = expectedTitles },
            new() { Timeout = 30_000 });
        return await page.EvaluateAsync<JsonElement>(GanttProbe, rootTestId);
    }

    public static JsonElement GanttRow(JsonElement probe, string title)
        => Assert.Single(probe.GetProperty("rows").EnumerateArray(), row => row.GetProperty("title").GetString() == title);

    public static string[] GanttAssignments(JsonElement row)
        => row.GetProperty("assignments").EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray();

    // The state a journey failed in stays next to its trace; a passing run removes what an earlier failure left.
    public static async Task CaptureFailureAsync(PlaywrightAppFixture fixture, IPage page, string artifactsDir, Exception exception)
    {
        var errorUiVisible = await page.Locator("#blazor-error-ui").IsVisibleAsync();
        await File.WriteAllTextAsync(
            Path.Combine(artifactsDir, "failure.txt"),
            string.Join(
                Environment.NewLine,
                page.Url,
                $"Blazor error UI visible: {errorUiVisible}",
                exception.ToString(),
                "---- host log tail ----",
                fixture.GetLogSnapshot(300)));
        await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "failure.png"), FullPage = true });
    }

    public static void ClearFailure(string artifactsDir)
    {
        File.Delete(Path.Combine(artifactsDir, "failure.txt"));
        File.Delete(Path.Combine(artifactsDir, "failure.png"));
    }

    public static Task<ServiceProvider> BuildProviderAsync(PlaywrightAppFixture fixture, string profileName)
        => TestApplicationBootstrap.BuildServiceProviderAsync(
            CreateActiveProfile(fixture, profileName),
            "CanDoItAll.Tests.Playwright.Seed",
            TestSchemaBootstrapModules.Full,
            new Dictionary<string, string?>
            {
                ["DevelopmentManager:TuningModeEnabled"] = "false"
            });

    public static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = $"{name} description",
            Objective = $"{name} objective",
            CurrentPhase = "Discovery"
        });
        Assert.True(result.IsSuccess, Join(result.Errors));
        return result.Value;
    }

    public static async Task<Guid> CreatePartyAsync(
        PartyDirectoryService partyDirectoryService,
        string displayName,
        PartyType partyType,
        PartyRoleKind roleKind,
        string email)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
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

        Assert.True(result.IsSuccess, Join(result.Errors));
        return result.Value;
    }

    public static async Task<Guid> CreateAffiliationAsync(
        IPartyOrganizationAffiliationService affiliationService,
        Guid personId,
        Guid organizationId,
        string jobTitle)
    {
        var result = await affiliationService.UpsertAsync(
            new PartyOrganizationAffiliationEditorModel
            {
                PersonPartyId = personId,
                OrganizationPartyId = organizationId,
                AffiliationKind = PartyOrganizationAffiliationKind.Employee,
                IsPrimary = true,
                JobTitle = jobTitle
            },
            "playwright-tests");
        Assert.True(result.IsSuccess, Join(result.Errors));
        return result.Value!.Id;
    }

    public static async Task CreateWorkforceProfileAsync(HrService hrService, Guid partyId, string jobTitle, decimal hourlyCostRate)
    {
        var result = await hrService.SaveWorkforceProfileAsync(new WorkforceProfileEditorModel
        {
            PartyId = partyId,
            WorkforceKind = WorkforceKind.Employee,
            Status = "Active",
            JobTitle = jobTitle,
            Discipline = "Platform",
            Seniority = "Senior",
            Location = "Remote",
            CapacityHoursPerWeek = 40m,
            InternalCostRate = hourlyCostRate,
            RateUnit = ProjectResourceRateUnit.Hour,
            RateCurrencyCode = "USD",
            LastChangedBy = "playwright-tests"
        });
        Assert.True(result.IsSuccess, Join(result.Errors));
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
