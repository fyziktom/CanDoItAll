using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

// Route and chrome of the CRM / HR module through the real Web host and PostgreSQL: every area route with its
// document title, header and active area tab; area changes through the real secondary tabs; browser Back; supported
// deep links of seeded records with reload and close; and unknown identities that must never open another record.
[Collection(PlaywrightCollection.Name)]
public sealed class CrmHrRouteChromeJourneyTests
{
    private static readonly AreaRoute HomeArea = new("/crm-hr", "CRM / HR", "Unified relationship and workforce workspace", "Home", null);
    private static readonly AreaRoute DirectoryArea = new("/crm-hr/directory", "CRM / HR Directory", "Party directory", "Directory", "crmhr-directory");
    private static readonly AreaRoute CrmArea = new("/crm-hr/crm", "CRM / HR CRM", "CRM workspace", "CRM", "crmhr-account");
    private static readonly AreaRoute WorkforceArea = new("/crm-hr/workforce", "CRM / HR Workforce", "Workforce workspace", "Workforce", "crmhr-workforce");
    private static readonly AreaRoute RecruitingArea = new("/crm-hr/recruiting", "CRM / HR Recruiting", "Recruiting workspace", "Recruiting", "crmhr-recruiting-applications");
    private static readonly AreaRoute AgentsArea = new("/crm-hr/agents", "CRM Agents", "Agent directory projection", "Agents", "crmhr-agent");
    private static readonly AreaRoute AssignmentsArea = new("/crm-hr/assignments", "CRM / HR Assignments", "Assignments workspace", "Assignments", null);

    private readonly PlaywrightAppFixture fixture;

    public CrmHrRouteChromeJourneyTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Areas_load_with_their_chrome_tabs_and_back_navigate_and_deep_links_open_only_the_requested_record()
    {
        var evidence = CrmHrJourneyEvidence.Create("crm-hr-journey-route-chrome");
        var suffix = Guid.NewGuid().ToString("N")[..8];

        await using var owner = await CrmHrDirectoryJourneySupport.BuildOwnerProviderAsync(fixture, "playwright-journey-route-chrome");
        var seed = await SeedAsync(owner, suffix);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1600, Height = 1000 }
        });
        await evidence.StartTraceAsync(context);
        var page = await context.NewPageAsync();
        try
        {
            var oracle = CrmHrBrowserOracle.Attach(page);

            // Every area route as a full-page load: a 2xx document, the document title, the page header and the area
            // tabs with exactly the routed area active. Home is loaded last so the tab journey starts from it.
            foreach (var area in new[] { DirectoryArea, CrmArea, WorkforceArea, RecruitingArea, AgentsArea, AssignmentsArea, HomeArea })
            {
                await CrmHrDirectoryJourneySupport.OpenAsync(oracle, page, $"{fixture.BaseUrl}{area.Route}");
                await AssertAreaAsync(page, area);
                await evidence.ScreenshotAsync(page, $"01-route-{area.TabLabel.ToLowerInvariant()}-1600.png");
            }

            // Area changes through the real secondary tabs, one click each: the URL, the header and the active tab
            // follow, and the circuit survives (no document reload between areas).
            foreach (var area in new[] { DirectoryArea, CrmArea, WorkforceArea, RecruitingArea, AgentsArea, AssignmentsArea, HomeArea })
            {
                await CrmHrDirectoryJourneySupport.AreaTab(page, area.TabLabel).ClickAsync();
                await Assertions.Expect(page).ToHaveURLAsync(CrmHrDirectoryJourneySupport.RouteUrl(area.Route), new() { Timeout = 30_000 });
                await AssertAreaAsync(page, area);
            }

            // Browser Back walks the areas in reverse.
            await page.GoBackAsync();
            await Assertions.Expect(page).ToHaveURLAsync(CrmHrDirectoryJourneySupport.RouteUrl(AssignmentsArea.Route), new() { Timeout = 30_000 });
            await AssertAreaAsync(page, AssignmentsArea);
            await page.GoBackAsync();
            await Assertions.Expect(page).ToHaveURLAsync(CrmHrDirectoryJourneySupport.RouteUrl(AgentsArea.Route), new() { Timeout = 30_000 });
            await AssertAreaAsync(page, AgentsArea);

            // A record opened from the catalog and closed again leaves the area on its base route, and Back then
            // returns to the previous area instead of reopening the record.
            await CrmHrDirectoryJourneySupport.AreaTab(page, DirectoryArea.TabLabel).ClickAsync();
            await Assertions.Expect(page).ToHaveURLAsync(CrmHrDirectoryJourneySupport.RouteUrl(DirectoryArea.Route), new() { Timeout = 30_000 });
            await AssertAreaAsync(page, DirectoryArea);
            var personCard = await CrmHrDirectoryJourneySupport.SearchCatalogForAsync(page, "crmhr-directory", "crmhr-directory-item", seed.PersonName);
            await personCard.ClickAsync();
            var directoryDialog = page.GetByTestId("crmhr-directory-record-dialog");
            await Assertions.Expect(directoryDialog).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(directoryDialog.Locator(".cda-dialog__title")).ToHaveTextAsync(seed.PersonName);
            await Assertions.Expect(page.GetByTestId("crmhr-party-display-name")).ToHaveValueAsync(seed.PersonName);
            await Assertions.Expect(page).ToHaveURLAsync(CrmHrDirectoryJourneySupport.RouteUrl(DirectoryArea.Route, $"partyId={seed.PersonId:D}"));
            await evidence.ScreenshotAsync(page, "02-directory-record-from-catalog-1600.png");
            await CrmHrDirectoryJourneySupport.AssertConstrainedLayoutAsync(
                page,
                evidence,
                "03-directory-record-1100.png",
                directoryDialog,
                page.GetByTestId("crmhr-party-save-button"),
                page.GetByTestId("crmhr-directory-record-close"));
            await page.GetByTestId("crmhr-directory-record-close").ClickAsync();
            await Assertions.Expect(directoryDialog).ToHaveCountAsync(0, new() { Timeout = 30_000 });
            await Assertions.Expect(page).ToHaveURLAsync(CrmHrDirectoryJourneySupport.RouteUrl(DirectoryArea.Route));
            await page.GoBackAsync();
            await Assertions.Expect(page).ToHaveURLAsync(CrmHrDirectoryJourneySupport.RouteUrl(AgentsArea.Route), new() { Timeout = 30_000 });
            await AssertAreaAsync(page, AgentsArea);

            // Supported deep links: the seeded record opens with its exact name, a reload restores the same record, and
            // closing the dialog returns to the base route of the area.
            await AssertDeepLinkAsync(
                oracle,
                page,
                DirectoryArea,
                $"partyId={seed.PersonId:D}",
                "crmhr-directory-record-dialog",
                "crmhr-directory-record-close",
                seed.PersonName,
                () => Assertions.Expect(page.GetByTestId("crmhr-party-display-name")).ToHaveValueAsync(seed.PersonName),
                evidence,
                "04-deep-link-directory-1600.png");
            await AssertDeepLinkAsync(
                oracle,
                page,
                WorkforceArea,
                $"partyId={seed.WorkerId:D}",
                "crmhr-workforce-record-dialog",
                "crmhr-workforce-record-close",
                seed.WorkerName,
                () => Assertions.Expect(page.GetByTestId("crmhr-workforce-lifecycle")).ToHaveTextAsync(PartyLifecycleStatus.Active.ToString()),
                evidence,
                "05-deep-link-workforce-1600.png");
            await AssertDeepLinkAsync(
                oracle,
                page,
                CrmArea,
                $"accountId={seed.AccountId:D}",
                "crmhr-crm-record-dialog",
                "crmhr-crm-record-close",
                seed.AccountName,
                async () =>
                {
                    await Assertions.Expect(page.GetByTestId("crmhr-account-summary")).ToHaveAttributeAsync("data-account-id", seed.AccountId.ToString("D"));
                    await Assertions.Expect(page.GetByTestId("crmhr-account-summary-name")).ToHaveTextAsync(seed.AccountName);
                    await Assertions.Expect(page.GetByTestId("crmhr-account-summary")).ToHaveAttributeAsync("data-interactive", "true", new() { Timeout = 30_000 });
                },
                evidence,
                "06-deep-link-crm-1600.png");

            // Unknown identities never open a record: no record dialog appears, and the catalog still finds and opens
            // the seeded record by its exact name.
            var unknownId = Guid.NewGuid();
            await CrmHrDirectoryJourneySupport.OpenAsync(oracle, page, $"{fixture.BaseUrl}{DirectoryArea.Route}?partyId={unknownId:D}");
            await AssertAreaAsync(page, DirectoryArea);
            await Assertions.Expect(directoryDialog).ToHaveCountAsync(0);
            personCard = await CrmHrDirectoryJourneySupport.SearchCatalogForAsync(page, "crmhr-directory", "crmhr-directory-item", seed.PersonName);
            await personCard.ClickAsync();
            await Assertions.Expect(directoryDialog).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(page.GetByTestId("crmhr-party-display-name")).ToHaveValueAsync(seed.PersonName);
            await evidence.ScreenshotAsync(page, "07-unknown-party-directory-recovers-1600.png");

            await CrmHrDirectoryJourneySupport.OpenAsync(oracle, page, $"{fixture.BaseUrl}{WorkforceArea.Route}?partyId={unknownId:D}");
            await AssertAreaAsync(page, WorkforceArea);
            await Assertions.Expect(page.GetByTestId("crmhr-workforce-record-dialog")).ToHaveCountAsync(0);
            await CrmHrDirectoryJourneySupport.SearchCatalogForAsync(page, "crmhr-workforce", "crmhr-workforce-item", seed.WorkerName);

            await CrmHrDirectoryJourneySupport.OpenAsync(oracle, page, $"{fixture.BaseUrl}{CrmArea.Route}?accountId={unknownId:D}");
            await AssertAreaAsync(page, CrmArea);
            await Assertions.Expect(page.GetByTestId("crmhr-crm-record-dialog")).ToHaveCountAsync(0);
            await CrmHrDirectoryJourneySupport.SearchCatalogForAsync(page, "crmhr-account", "crmhr-account-item", seed.AccountName);

            // Assignments exposes neither a catalog search nor an interactive marker. A fresh tab has its own session
            // storage, so the database startup prompt answers there only once the circuit handles events.
            var assignmentsPage = await context.NewPageAsync();
            var assignmentsOracle = CrmHrBrowserOracle.Attach(assignmentsPage);
            await CrmHrDirectoryJourneySupport.OpenAsync(assignmentsOracle, assignmentsPage, $"{fixture.BaseUrl}{AssignmentsArea.Route}?projectId={unknownId:D}");
            await AssertAreaAsync(assignmentsPage, AssignmentsArea);
            var projectContext = assignmentsPage.GetByTestId("crmhr-assignments-project-context");
            await Assertions.Expect(projectContext).ToContainTextAsync("The selected project record is unavailable.", new() { Timeout = 30_000 });
            await Assertions.Expect(projectContext).Not.ToContainTextAsync("Active project");
            await Assertions.Expect(projectContext).Not.ToContainTextAsync(seed.ProjectName);
            await evidence.ScreenshotAsync(assignmentsPage, "08-unknown-project-assignments-1600.png");
            await assignmentsPage.GetByTestId("crmhr-assignment-project").ClickAsync();
            await Assertions.Expect(assignmentsPage.GetByTestId("crmhr-assignment-project-picker")).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await CrmHrDirectoryJourneySupport.WaitForCatalogAsync(assignmentsPage, "crmhr-assignment-project-picker-browser");
            await assignmentsPage.GetByTestId("crmhr-assignment-project-picker-browser-search").FillAsync(seed.ProjectName);
            var projectOption = assignmentsPage.GetByTestId($"crmhr-project-option-{seed.ProjectId:N}");
            await Assertions.Expect(projectOption).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await projectOption.ClickAsync();
            await Assertions.Expect(assignmentsPage.GetByTestId("crmhr-assignment-project-picker-confirm")).ToBeEnabledAsync();
            await assignmentsPage.GetByTestId("crmhr-assignment-project-picker-confirm").ClickAsync();
            await Assertions.Expect(assignmentsPage.GetByTestId("crmhr-assignment-project-picker")).ToHaveCountAsync(0, new() { Timeout = 30_000 });
            await Assertions.Expect(projectContext).ToContainTextAsync("Active project", new() { Timeout = 30_000 });
            await Assertions.Expect(projectContext).ToContainTextAsync(seed.ProjectName);
            await evidence.ScreenshotAsync(assignmentsPage, "09-assignments-project-chosen-1600.png");

            await evidence.WriteExpectedTeardownAsync(oracle.ExpectedTeardown.Concat(assignmentsOracle.ExpectedTeardown));
            await assignmentsOracle.AssertCleanAsync();
            await oracle.AssertCleanAsync();
        }
        catch (Exception failure)
        {
            await evidence.WriteFailureAsync(failure, fixture, page);
            throw;
        }
        finally
        {
            await evidence.StopTraceAsync(context);
        }
    }

    // An unknown agent identity: the Agents host opens its record dialog in a loading state while it resolves the
    // identity and withdraws it once nothing matches. The settled page has no record dialog, a catalog that still
    // answers a search over the live circuit, and no browser-side failure.
    [Fact]
    public async Task Unknown_agent_identity_opens_no_record_and_leaves_the_agents_catalog_usable()
    {
        var evidence = CrmHrJourneyEvidence.Create("crm-hr-journey-route-chrome-unknown-agent");
        var suffix = Guid.NewGuid().ToString("N")[..8];

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1600, Height = 1000 }
        });
        await evidence.StartTraceAsync(context);
        var page = await context.NewPageAsync();
        try
        {
            var oracle = CrmHrBrowserOracle.Attach(page);
            await CrmHrDirectoryJourneySupport.OpenAsync(oracle, page, $"{fixture.BaseUrl}{AgentsArea.Route}?partyId={Guid.NewGuid():D}");
            await AssertAreaAsync(page, AgentsArea);
            await Assertions.Expect(page.GetByTestId("crmhr-agent-record-dialog")).ToHaveCountAsync(0, new() { Timeout = 30_000 });
            await Assertions.Expect(page.GetByTestId("crmhr-agent-results").Or(page.GetByTestId("crmhr-agent-empty"))).ToBeVisibleAsync();
            await evidence.ScreenshotAsync(page, "01-unknown-party-agents-1600.png");

            // The reset control enables only after the server accepted the typed search, so it proves a live circuit.
            await Assertions.Expect(page.GetByTestId("crmhr-agent-reset")).ToBeDisabledAsync();
            await page.GetByTestId("crmhr-agent-search").FillAsync($"no-such-agent-{suffix}");
            await Assertions.Expect(page.GetByTestId("crmhr-agent-reset")).ToBeEnabledAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(page.GetByTestId("crmhr-agent-empty")).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(page.GetByTestId("crmhr-agent-record-dialog")).ToHaveCountAsync(0);
            await evidence.ScreenshotAsync(page, "02-agents-catalog-answers-1600.png");

            await evidence.WriteExpectedTeardownAsync(oracle.ExpectedTeardown);
            await oracle.AssertCleanAsync();
        }
        catch (Exception failure)
        {
            await evidence.WriteFailureAsync(failure, fixture, page);
            throw;
        }
        finally
        {
            await evidence.StopTraceAsync(context);
        }
    }

    // The chrome of one area: the document title, the page header, exactly the routed area tab active, and the
    // area's own readiness signal (interactive Home, or an enabled catalog search with an accepted first page).
    private static async Task AssertAreaAsync(IPage page, AreaRoute area)
    {
        await Assertions.Expect(page).ToHaveTitleAsync(area.DocumentTitle, new() { Timeout = 30_000 });
        await Assertions.Expect(CrmHrDirectoryJourneySupport.PageHeading(page, area.Heading)).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await CrmHrDirectoryJourneySupport.AssertActiveAreaAsync(page, area.TabLabel);
        if (area.CatalogTestId is not null)
        {
            await CrmHrDirectoryJourneySupport.WaitForCatalogAsync(page, area.CatalogTestId);
        }
        else if (ReferenceEquals(area, HomeArea))
        {
            // The prerendered document is already ready; the interactive host reads again, so interactive comes first.
            var home = page.GetByTestId("crmhr-home");
            await Assertions.Expect(home).ToHaveAttributeAsync("data-interactive", "true", new() { Timeout = 30_000 });
            await Assertions.Expect(home).ToHaveAttributeAsync("data-phase", "ready", new() { Timeout = 30_000 });
        }
        else
        {
            await Assertions.Expect(page.GetByTestId("crmhr-assignments-project-context")).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(page.GetByTestId("crmhr-assignments-workspace-tabs")).ToBeVisibleAsync(new() { Timeout = 30_000 });
        }
    }

    private async Task AssertDeepLinkAsync(
        CrmHrBrowserOracle oracle,
        IPage page,
        AreaRoute area,
        string query,
        string dialogTestId,
        string closeTestId,
        string expectedName,
        Func<Task> assertRecord,
        CrmHrJourneyEvidence evidence,
        string screenshotName)
    {
        var dialog = page.GetByTestId(dialogTestId);
        // The first pass is the deep link, the second one its reload.
        for (var pass = 0; pass < 2; pass++)
        {
            await CrmHrDirectoryJourneySupport.OpenAsync(oracle, page, $"{fixture.BaseUrl}{area.Route}?{query}");
            await Assertions.Expect(dialog).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(dialog.Locator(".cda-dialog__title")).ToHaveTextAsync(expectedName);
            await assertRecord();
            await Assertions.Expect(page).ToHaveURLAsync(CrmHrDirectoryJourneySupport.RouteUrl(area.Route, query));
            await CrmHrDirectoryJourneySupport.AssertActiveAreaAsync(page, area.TabLabel);
        }

        await evidence.ScreenshotAsync(page, screenshotName);
        await page.GetByTestId(closeTestId).ClickAsync();
        await Assertions.Expect(dialog).ToHaveCountAsync(0, new() { Timeout = 30_000 });
        await Assertions.Expect(page).ToHaveURLAsync(CrmHrDirectoryJourneySupport.RouteUrl(area.Route), new() { Timeout = 30_000 });
        await AssertAreaAsync(page, area);
    }

    private static async Task<SeededChrome> SeedAsync(ServiceProvider owner, string suffix)
    {
        await using var scope = owner.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var hrService = scope.ServiceProvider.GetRequiredService<HrService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();

        var personName = $"Chrome Person {suffix}";
        var workerName = $"Chrome Worker {suffix}";
        var accountName = $"Chrome Account {suffix}";
        var projectName = $"Chrome Project {suffix}";

        var personId = await CrmHrDirectoryJourneySupport.CreatePartyAsync(partyDirectoryService, personName, PartyType.Person, PartyRoleKind.CustomerContact, $"chrome.person.{suffix}@example.test");
        var workerId = await CrmHrDirectoryJourneySupport.CreatePartyAsync(partyDirectoryService, workerName, PartyType.Person, PartyRoleKind.Employee, $"chrome.worker.{suffix}@example.test");
        var accountId = await CrmHrDirectoryJourneySupport.CreatePartyAsync(partyDirectoryService, accountName, PartyType.Organization, PartyRoleKind.Customer, $"chrome.account.{suffix}@example.test");

        var profile = await hrService.SaveWorkforceProfileAsync(new WorkforceProfileEditorModel
        {
            PartyId = workerId,
            WorkforceKind = WorkforceKind.Employee,
            Status = "Active",
            JobTitle = "Route Steward",
            Discipline = "Operations",
            CapacityHoursPerWeek = 40m,
            LastChangedBy = CrmHrDirectoryJourneySupport.Actor
        });
        Assert.True(profile.IsSuccess, string.Join(" ", profile.Errors.Select(error => error.Message)));

        var project = await projectsService.CreateWithAdmissionAsync(new ProjectEditorModel
        {
            Name = projectName,
            Description = $"{projectName} description",
            Objective = $"{projectName} objective",
            CurrentPhase = "Journey"
        });
        Assert.True(project.IsSuccess, string.Join(" ", project.Errors.Select(error => error.Message)));
        var admission = Assert.IsType<ProjectWriteAdmission>(project.Value);

        return new SeededChrome(personId, personName, workerId, workerName, accountId, accountName, admission.ProjectId, projectName);
    }

    private sealed record AreaRoute(string Route, string DocumentTitle, string Heading, string TabLabel, string? CatalogTestId);

    private sealed record SeededChrome(
        Guid PersonId,
        string PersonName,
        Guid WorkerId,
        string WorkerName,
        Guid AccountId,
        string AccountName,
        Guid ProjectId,
        string ProjectName);
}
