using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using static CanDoItAll.Tests.Playwright.Smoke.CrmHrAssignmentsJourneySupport;

namespace CanDoItAll.Tests.Playwright.Smoke;

// The CRM / HR Assignments workspace through the real Web host and PostgreSQL: the project picker of the context bar,
// project participation, a staffing request, an allocation that is created and deleted again, the read-only resource
// schedule, the four search boxes, a project switch A -> B -> A without stale rows or drafts, the deep link, and an
// unknown project. Every write is one click and is read back through its owner by exact identity.
[Collection(PlaywrightCollection.Name)]
public sealed class CrmHrAssignmentsJourneyTests
{
    // The backing fields the four search boxes once rendered as literal text.
    private static readonly string[] SearchBackingFieldNames =
    [
        "relationshipAssignmentSearchText",
        "staffingRequestSearchText",
        "allocationAssignmentSearchText",
        "candidateSearchText"
    ];

    private const string WorkspaceTabs = "[data-testid='crmhr-assignments-workspace-tabs'] [role='tab']";
    private const string ScheduleChart = "crmhr-assignment-resource-gantt";

    private readonly PlaywrightAppFixture fixture;

    public CrmHrAssignmentsJourneyTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Assignments_workspace_records_participation_staffing_and_allocations_per_project_without_stale_data()
    {
        var artifactsDir = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "crm-hr-assignments-journey");
        Directory.CreateDirectory(artifactsDir);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var services = await BuildProviderAsync(fixture, "playwright-assignments-journey-seed");
        var seed = await SeedAsync(services, suffix);

        // The allocation window is typed into the form; it lies inside the schedule's default horizon on any run date.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var allocationStart = today.AddDays(7);
        var allocationEnd = today.AddDays(20);
        var requestStart = today.AddDays(10);
        var requestEnd = today.AddDays(40);
        var requestTitle = $"Platform coverage {suffix}";
        var customerNotes = $"Primary customer {suffix}";
        var allocationNotes = $"Delivery sprint {suffix}";

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1600, Height = 1000 }
        });
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true });
        var page = await context.NewPageAsync();
        try
        {
            var oracle = CrmHrBrowserOracle.Attach(page);
            var contextBar = page.GetByTestId("crmhr-assignments-project-context");

            // No project in the route: the workspace opens on its own fallback project. The journey clears it and then
            // chooses its project in the real picker of the context bar.
            await OpenWorkspaceAsync(page, oracle, $"{fixture.BaseUrl}/crm-hr/assignments");
            Assert.Equal("CRM / HR Assignments", await page.TitleAsync());
            await page.GetByTestId("crmhr-assignment-project-clear").ClickAsync();
            await Assertions.Expect(contextBar).ToContainTextAsync("No project selected");
            await Assertions.Expect(contextBar).ToContainTextAsync("Choose a project to load its resource schedule and assignment workflows.");
            await Assertions.Expect(page.GetByTestId("crmhr-assignment-project-clear")).ToHaveCountAsync(0);

            await ChooseProjectAsync(page, seed.ProjectAId, seed.ProjectAName);
            await Assertions.Expect(contextBar).ToContainTextAsync("Active project");
            await Assertions.Expect(contextBar).ToContainTextAsync(seed.ProjectAName);
            await Assertions.Expect(contextBar).ToContainTextAsync(Count(0, "relationships"));
            await Assertions.Expect(contextBar).ToContainTextAsync(Count(0, "allocations"));
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "01-project-a-selected-1600.png") });

            // Relationships: Customer -> the organization.
            var relationships = page.GetByTestId("crmhr-assignment-page");
            var relationshipSearch = page.GetByTestId("crmhr-assignment-search");
            await page.GetByTestId("crmhr-assignments-tab-relationships").ClickAsync();
            await Assertions.Expect(relationships).ToContainTextAsync($"0 assignment(s) for {seed.ProjectAName}");
            await AssertUntouchedSearchBoxAsync(relationshipSearch);

            var createDialog = page.GetByTestId("crmhr-assignment-create-dialog");
            await page.GetByTestId("crmhr-assignment-create-button").ClickAsync();
            await Assertions.Expect(createDialog).ToBeVisibleAsync();
            await Assertions.Expect(createDialog).ToContainTextAsync(seed.ProjectAName);
            await page.GetByTestId("crmhr-assignment-role").SelectOptionAsync(nameof(ProjectPartyAssignmentRole.Customer));
            await page.GetByTestId("crmhr-assignment-party-select").ClickAsync();
            await PickPartyAsync(page, "crmhr-assignment-party-dialog", seed.CustomerName, seed.CustomerId);
            await Assertions.Expect(page.GetByTestId("crmhr-assignment-party-summary")).ToContainTextAsync(seed.CustomerName);
            // An organization has no organization affiliation of its own: the picker discloses the directory fallback.
            await Assertions.Expect(page.GetByTestId("crmhr-assignment-affiliation-fallback")).ToBeVisibleAsync();
            await page.GetByTestId("crmhr-assignment-notes").FillAsync(customerNotes);
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "02-customer-assignment-dialog-1600.png") });
            await page.GetByTestId("crmhr-assignment-save-button").ClickAsync();
            await Assertions.Expect(createDialog).ToHaveCountAsync(0);
            await Assertions.Expect(AssignmentCard(page, seed.CustomerName)).ToContainTextAsync("Customer");

            var customerAssignment = Assert.Single(await ReadAssignmentsAsync(services, seed.ProjectAId, ProjectPartyAssignmentRole.Customer));
            Assert.Equal(seed.CustomerId, customerAssignment.PartyId);
            Assert.Equal(ProjectPartyType.Organization, customerAssignment.PartyType);
            Assert.Equal(seed.ProjectAId, customerAssignment.ProjectId);
            Assert.Equal(customerNotes, customerAssignment.Notes);
            Assert.Equal("crm-hr-ui", customerAssignment.Source);
            Assert.True(customerAssignment.IsPrimary);
            Assert.Null(customerAssignment.AllocationPercent);
            Assert.Equal(string.Empty, customerAssignment.NodeKey);

            // Relationships: Manager -> the person, with the affiliation the picker resolved for that person.
            await page.GetByTestId("crmhr-assignment-create-button").ClickAsync();
            await Assertions.Expect(createDialog).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-assignment-notes")).ToHaveValueAsync(string.Empty);
            await page.GetByTestId("crmhr-assignment-role").SelectOptionAsync(nameof(ProjectPartyAssignmentRole.Manager));
            await page.GetByTestId("crmhr-assignment-party-select").ClickAsync();
            await PickPartyAsync(page, "crmhr-assignment-party-dialog", seed.ManagerName, seed.ManagerId);
            await Assertions.Expect(page.GetByTestId("crmhr-assignment-party-summary")).ToContainTextAsync(seed.ManagerName);
            var affiliation = createDialog.Locator("select[data-testid='crmhr-assignment-affiliation']");
            await Assertions.Expect(affiliation).ToHaveValueAsync(seed.ManagerAffiliationId.ToString("D"));
            await Assertions.Expect(affiliation.Locator("option:checked")).ToContainTextAsync(seed.CustomerName);
            await page.GetByTestId("crmhr-assignment-save-button").ClickAsync();
            await Assertions.Expect(createDialog).ToHaveCountAsync(0);
            await Assertions.Expect(AssignmentCard(page, seed.ManagerName)).ToContainTextAsync("Manager");
            await Assertions.Expect(AssignmentCard(page, seed.ManagerName).GetByTestId("crmhr-assignment-affiliation")).ToContainTextAsync(seed.CustomerName);
            await Assertions.Expect(page.GetByTestId("crmhr-assignment-item")).ToHaveCountAsync(2);

            var managerAssignment = Assert.Single(await ReadAssignmentsAsync(services, seed.ProjectAId, ProjectPartyAssignmentRole.Manager));
            Assert.Equal(seed.ManagerId, managerAssignment.PartyId);
            Assert.Equal(ProjectPartyType.Person, managerAssignment.PartyType);
            Assert.Equal(seed.ManagerAffiliationId, managerAssignment.PartyAffiliationId);
            Assert.Equal(seed.CustomerName, managerAssignment.Affiliation?.OrganizationName);
            Assert.NotEqual(customerAssignment.Id, managerAssignment.Id);
            // A manager is an allocation role of the product: the relationship counts as one open-ended allocation.
            await Assertions.Expect(contextBar).ToContainTextAsync(Count(2, "relationships"));
            await Assertions.Expect(contextBar).ToContainTextAsync(Count(1, "allocations"));

            // Relationship search: the box keeps what was typed and the server filters by it.
            await relationshipSearch.FillAsync(seed.CustomerName);
            await Assertions.Expect(relationships).ToContainTextAsync(Count(1, "matching assignment(s)"));
            await Assertions.Expect(relationshipSearch).ToHaveValueAsync(seed.CustomerName);
            await Assertions.Expect(page.GetByTestId("crmhr-assignment-item")).ToHaveCountAsync(1);
            await Assertions.Expect(AssignmentCard(page, seed.CustomerName)).ToBeVisibleAsync();
            var noMatch = $"zz-no-match-{suffix}";
            await relationshipSearch.FillAsync(noMatch);
            await Assertions.Expect(relationships).ToContainTextAsync(Count(0, "matching assignment(s)"));
            await Assertions.Expect(relationships).ToContainTextAsync("No matching assignments");
            await Assertions.Expect(relationshipSearch).ToHaveValueAsync(noMatch);
            await relationshipSearch.FillAsync(string.Empty);
            await Assertions.Expect(relationships).ToContainTextAsync(Count(2, "matching assignment(s)"));
            await Assertions.Expect(page.GetByTestId("crmhr-assignment-item")).ToHaveCountAsync(2);
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "03-relationships-1600.png") });

            // An abandoned draft: typed, cancelled, and never saved. It must not reappear for another project.
            await page.GetByTestId("crmhr-assignment-create-button").ClickAsync();
            await Assertions.Expect(createDialog).ToBeVisibleAsync();
            await page.GetByTestId("crmhr-assignment-notes").FillAsync($"Abandoned draft {suffix}");
            await createDialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
            await Assertions.Expect(createDialog).ToHaveCountAsync(0);

            // Staffing requests.
            var staffingSearch = page.GetByTestId("crmhr-staffing-request-search");
            await page.GetByTestId("crmhr-assignments-tab-staffing").ClickAsync();
            await Assertions.Expect(staffingSearch).ToBeVisibleAsync();
            await Assertions.Expect(contextBar).ToContainTextAsync(Count(0, "staffing requests"));
            await AssertUntouchedSearchBoxAsync(staffingSearch);

            var staffingDialog = page.GetByTestId("crmhr-staffing-request-dialog");
            await page.GetByTestId("crmhr-staffing-request-create-button").ClickAsync();
            await Assertions.Expect(staffingDialog).ToBeVisibleAsync();
            await page.GetByTestId("crmhr-staffing-request-title").FillAsync(requestTitle);
            await page.GetByTestId("crmhr-staffing-request-role").FillAsync("Senior platform engineer");
            await page.GetByTestId("crmhr-staffing-request-allocation").FillAsync("75");
            await page.GetByTestId("crmhr-staffing-request-start-date").FillAsync(IsoDate(requestStart));
            await page.GetByTestId("crmhr-staffing-request-end-date").FillAsync(IsoDate(requestEnd));
            await page.GetByTestId("crmhr-staffing-request-requested-by-select").ClickAsync();
            await PickPartyAsync(page, "crmhr-staffing-request-requested-by-dialog", seed.RequesterName, seed.RequesterId);
            await Assertions.Expect(page.GetByTestId("crmhr-staffing-request-requested-by-summary")).ToContainTextAsync(seed.RequesterName);
            await page.GetByTestId("crmhr-staffing-request-notes").FillAsync($"Coverage for the release window {suffix}");

            // Constrained width with the record dialog open: the dialog and its primary action stay reachable.
            await page.SetViewportSizeAsync(1100, 900);
            await page.WaitForTimeoutAsync(300);
            await Assertions.Expect(staffingDialog).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-staffing-request-save-button")).ToBeInViewportAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-staffing-request-reset-button")).ToBeInViewportAsync();
            Assert.False(await page.EvaluateAsync<bool>(OverflowProbe), "The document overflows horizontally at 1100 px with the staffing request dialog open.");
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "04-staffing-request-dialog-1100.png") });
            await page.SetViewportSizeAsync(1600, 1000);
            await page.WaitForTimeoutAsync(300);

            await page.GetByTestId("crmhr-staffing-request-save-button").ClickAsync();
            await Assertions.Expect(staffingDialog).ToHaveCountAsync(0);
            var requestCard = page.GetByTestId("crmhr-staffing-request-item").Filter(new() { HasText = requestTitle });
            await Assertions.Expect(requestCard).ToContainTextAsync($"Requested by {seed.RequesterName}");
            await Assertions.Expect(requestCard).ToContainTextAsync("75% allocation");
            await Assertions.Expect(requestCard).ToContainTextAsync($"{IsoDate(requestStart)} → {IsoDate(requestEnd)}");
            await Assertions.Expect(contextBar).ToContainTextAsync(Count(1, "staffing requests"));

            var request = Assert.Single(await ReadStaffingRequestsAsync(services, seed.ProjectAId));
            Assert.Equal(requestTitle, request.Title);
            Assert.Equal("Senior platform engineer", request.NeededRole);
            Assert.Equal(seed.RequesterId, request.RequestedByPartyId);
            Assert.Equal(75m, request.AllocationPercent);
            Assert.Equal(requestStart, request.StartDate);
            Assert.Equal(requestEnd, request.EndDate);
            Assert.Equal(StaffingRequestStatus.Open, request.Status);
            Assert.Equal(seed.ProjectAId, request.ProjectId);

            await staffingSearch.FillAsync("Senior platform");
            await Assertions.Expect(page.GetByTestId("crmhr-staffing-request-item")).ToHaveCountAsync(1);
            await Assertions.Expect(staffingSearch).ToHaveValueAsync("Senior platform");
            await staffingSearch.FillAsync(noMatch);
            await Assertions.Expect(page.GetByTestId("crmhr-staffing-request-item")).ToHaveCountAsync(0);
            await Assertions.Expect(page.GetByText("No matching requests", new() { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(staffingSearch).ToHaveValueAsync(noMatch);
            await staffingSearch.FillAsync(string.Empty);
            await Assertions.Expect(requestCard).ToBeVisibleAsync();

            // Allocations: the manager relationship is already listed; the worker is allocated from the candidate finder.
            var allocations = page.GetByTestId("crmhr-allocations-surface");
            var allocationSearch = page.GetByTestId("crmhr-allocation-filter-search");
            await page.GetByTestId("crmhr-assignments-tab-allocations").ClickAsync();
            await Assertions.Expect(allocations).ToContainTextAsync(Count(1, "saved allocation(s)"));
            await AssertUntouchedSearchBoxAsync(allocationSearch);
            await Assertions.Expect(AllocationCard(page, seed.ManagerName)).ToContainTextAsync("Manager");

            var allocationDialog = page.GetByTestId("crmhr-allocation-dialog");
            var candidateSearch = page.GetByTestId("crmhr-allocation-candidate-search");
            await page.GetByTestId("crmhr-allocation-create-button").ClickAsync();
            await Assertions.Expect(allocationDialog).ToBeVisibleAsync();
            await AssertUntouchedSearchBoxAsync(candidateSearch);
            await candidateSearch.FillAsync(seed.WorkerName);
            await page.GetByTestId("crmhr-allocation-candidate-search-button").ClickAsync();
            var candidates = page.GetByTestId("crmhr-allocation-candidates-surface");
            await Assertions.Expect(candidates).ToContainTextAsync(Count(1, "matching candidate(s)"));
            await Assertions.Expect(candidateSearch).ToHaveValueAsync(seed.WorkerName);
            var candidate = page.GetByTestId("crmhr-staffing-candidate-item").Filter(new() { HasText = seed.WorkerName });
            await Assertions.Expect(candidate).ToContainTextAsync("Platform engineer");
            await candidate.GetByTestId("crmhr-allocation-use-candidate").ClickAsync();
            await Assertions.Expect(allocationDialog).ToContainTextAsync($"Allocate {seed.WorkerName}");
            await Assertions.Expect(page.GetByTestId("crmhr-allocation-party-summary")).ToContainTextAsync(seed.WorkerName);
            await Assertions.Expect(page.GetByTestId("crmhr-allocation-role")).ToHaveValueAsync(nameof(ProjectPartyAssignmentRole.TeamMember));
            await page.GetByTestId("crmhr-allocation-percent").FillAsync("60");
            await page.GetByTestId("crmhr-allocation-start-date").FillAsync(IsoDate(allocationStart));
            await page.GetByTestId("crmhr-allocation-end-date").FillAsync(IsoDate(allocationEnd));
            await page.GetByTestId("crmhr-allocation-notes").FillAsync(allocationNotes);
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "05-allocation-dialog-1600.png") });
            await page.GetByTestId("crmhr-allocation-save-button").ClickAsync();
            await Assertions.Expect(allocationDialog).ToHaveCountAsync(0);
            await Assertions.Expect(AllocationCard(page, seed.WorkerName))
                .ToContainTextAsync($"Team member · 60% · {IsoDate(allocationStart)} → {IsoDate(allocationEnd)}");
            await Assertions.Expect(allocations).ToContainTextAsync(Count(2, "saved allocation(s)"));
            await Assertions.Expect(contextBar).ToContainTextAsync(Count(3, "relationships"));
            await Assertions.Expect(contextBar).ToContainTextAsync(Count(2, "allocations"));

            var allocation = Assert.Single(await ReadAssignmentsAsync(services, seed.ProjectAId, ProjectPartyAssignmentRole.TeamMember));
            Assert.Equal(seed.WorkerId, allocation.PartyId);
            Assert.Equal(60m, allocation.AllocationPercent);
            Assert.Equal(allocationStart, DateOnly.FromDateTime(allocation.StartsAtUtc!.Value.UtcDateTime));
            Assert.Equal(allocationEnd, DateOnly.FromDateTime(allocation.EndsAtUtc!.Value.UtcDateTime));
            Assert.Equal(allocationNotes, allocation.Notes);
            Assert.Equal("crm-hr-ui", allocation.Source);
            Assert.False(allocation.IsPrimary);

            await allocationSearch.FillAsync(seed.WorkerName);
            await Assertions.Expect(allocations).ToContainTextAsync(Count(1, "matching allocation(s)"));
            await Assertions.Expect(allocationSearch).ToHaveValueAsync(seed.WorkerName);
            await Assertions.Expect(page.GetByTestId("crmhr-allocation-item")).ToHaveCountAsync(1);
            await allocationSearch.FillAsync(string.Empty);
            await Assertions.Expect(allocations).ToContainTextAsync(Count(2, "matching allocation(s)"));

            // Resource schedule: two rows with real boxes, and the bars painted on the chart canvas. The dated
            // allocation is 14 days; the open-ended manager relationship is clipped to the 121-day display horizon.
            await page.GetByTestId("crmhr-assignments-tab-schedule").ClickAsync();
            var schedule = page.GetByTestId("crmhr-assignment-resource-schedule");
            await Assertions.Expect(schedule).ToContainTextAsync(Count(2, "scheduled"));
            await Assertions.Expect(schedule).ToContainTextAsync(Count(2, "resources shown"));
            await Assertions.Expect(page.GetByTestId("crmhr-assignment-resource-gantt-open-boundary")).ToBeVisibleAsync();
            var workerRowTitle = $"{seed.WorkerName} · Team member · 60% allocated";
            var managerRowTitle = $"{seed.ManagerName} · Manager · allocation not set";
            var painted = await ReadGanttAsync(page, ScheduleChart, [workerRowTitle, managerRowTitle]);
            Assert.Equal(2, painted.GetProperty("rows").GetArrayLength());
            var workerRow = GanttRow(painted, workerRowTitle);
            var managerRow = GanttRow(painted, managerRowTitle);
            Assert.True(painted.GetProperty("canvasBackingWidth").GetInt32() > 1000, "The schedule canvas has no backing store.");
            Assert.True(painted.GetProperty("canvasBoxHeight").GetDouble() >= 40 + 2 * 48, "The schedule canvas is shorter than its two rows.");
            Assert.All(new[] { workerRow, managerRow }, row =>
            {
                Assert.True(row.GetProperty("width").GetDouble() > 100, "A schedule row has no width.");
                Assert.Equal(48d, row.GetProperty("height").GetDouble(), 1);
            });
            var workerBar = workerRow.GetProperty("barWidth").GetDouble();
            var managerBar = managerRow.GetProperty("barWidth").GetDouble();
            Assert.True(workerBar > 100, $"The dated allocation has no painted bar (run of {workerBar:F1} px).");
            Assert.True(
                Math.Abs(workerBar / managerBar - 14d / 121d) < 0.004,
                $"The 14-day allocation bar is {workerBar:F1} px against {managerBar:F1} px for the 121-day horizon.");
            var startOffsetDays = (workerRow.GetProperty("barStart").GetDouble() - managerRow.GetProperty("barStart").GetDouble()) / (managerBar / 121d);
            Assert.InRange(startOffsetDays, 35.9, 37.1);
            Assert.Equal(0, await page.GetByText("The chart model is invalid", new() { Exact = false }).CountAsync());
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "06-resource-schedule-1600.png") });

            // Delete the allocation again: exactly that row goes, the relationships of the other roles stay.
            await page.GetByTestId("crmhr-assignments-tab-allocations").ClickAsync();
            await Assertions.Expect(AllocationCard(page, seed.WorkerName)).ToBeVisibleAsync();
            await AllocationCard(page, seed.WorkerName).GetByTestId("crmhr-allocation-delete-button").ClickAsync();
            await Assertions.Expect(AllocationCard(page, seed.WorkerName)).ToHaveCountAsync(0);
            await Assertions.Expect(allocations).ToContainTextAsync(Count(1, "saved allocation(s)"));
            await Assertions.Expect(AllocationCard(page, seed.ManagerName)).ToBeVisibleAsync();
            await Assertions.Expect(contextBar).ToContainTextAsync(Count(2, "relationships"));
            await Assertions.Expect(contextBar).ToContainTextAsync(Count(1, "allocations"));
            Assert.Empty(await ReadAssignmentsAsync(services, seed.ProjectAId, ProjectPartyAssignmentRole.TeamMember));
            Assert.Equal(customerAssignment.Id, Assert.Single(await ReadAssignmentsAsync(services, seed.ProjectAId, ProjectPartyAssignmentRole.Customer)).Id);
            Assert.Equal(managerAssignment.Id, Assert.Single(await ReadAssignmentsAsync(services, seed.ProjectAId, ProjectPartyAssignmentRole.Manager)).Id);
            Assert.Equal(request.Id, Assert.Single(await ReadStaffingRequestsAsync(services, seed.ProjectAId)).Id);

            // A search left in project A must not follow the workspace into project B.
            await allocationSearch.FillAsync(seed.ManagerName);
            await Assertions.Expect(allocations).ToContainTextAsync(Count(1, "matching allocation(s)"));
            await Assertions.Expect(allocationSearch).ToHaveValueAsync(seed.ManagerName);

            // A -> B: only project B's seeded facts, no row, search text or draft of project A.
            await ChooseProjectAsync(page, seed.ProjectBId, seed.ProjectBName);
            await Assertions.Expect(contextBar).ToContainTextAsync(seed.ProjectBName);
            await Assertions.Expect(contextBar).Not.ToContainTextAsync(seed.ProjectAName);
            await Assertions.Expect(contextBar).ToContainTextAsync(Count(1, "relationships"));
            await Assertions.Expect(contextBar).ToContainTextAsync(Count(0, "allocations"));
            await Assertions.Expect(allocations).ToContainTextAsync(seed.ProjectBName);
            await Assertions.Expect(allocations).ToContainTextAsync(Count(0, "saved allocation(s)"));
            await Assertions.Expect(page.GetByTestId("crmhr-allocation-item")).ToHaveCountAsync(0);
            await AssertUntouchedSearchBoxAsync(allocationSearch);

            await page.GetByTestId("crmhr-assignments-tab-relationships").ClickAsync();
            await Assertions.Expect(relationships).ToContainTextAsync($"1 assignment(s) for {seed.ProjectBName}");
            await Assertions.Expect(page.GetByTestId("crmhr-assignment-item")).ToHaveCountAsync(1);
            await Assertions.Expect(AssignmentCard(page, seed.VendorName)).ToContainTextAsync("Vendor");
            await Assertions.Expect(AssignmentCard(page, seed.CustomerName)).ToHaveCountAsync(0);
            await Assertions.Expect(AssignmentCard(page, seed.ManagerName)).ToHaveCountAsync(0);
            await AssertUntouchedSearchBoxAsync(relationshipSearch);
            await page.GetByTestId("crmhr-assignment-create-button").ClickAsync();
            await Assertions.Expect(createDialog).ToBeVisibleAsync();
            await Assertions.Expect(createDialog).ToContainTextAsync(seed.ProjectBName);
            await Assertions.Expect(page.GetByTestId("crmhr-assignment-notes")).ToHaveValueAsync(string.Empty);
            await Assertions.Expect(createDialog).ToContainTextAsync("No party selected");
            await createDialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
            await Assertions.Expect(createDialog).ToHaveCountAsync(0);

            await page.GetByTestId("crmhr-assignments-tab-staffing").ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-staffing-request-item")).ToHaveCountAsync(1);
            await Assertions.Expect(page.GetByTestId("crmhr-staffing-request-item")).ToContainTextAsync(seed.ProjectBRequestTitle);
            await Assertions.Expect(page.GetByTestId("crmhr-staffing-request-item").Filter(new() { HasText = requestTitle })).ToHaveCountAsync(0);
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "07-project-b-1600.png") });

            // B -> A: the saved rows of project A are intact.
            await ChooseProjectAsync(page, seed.ProjectAId, seed.ProjectAName);
            await Assertions.Expect(contextBar).ToContainTextAsync(seed.ProjectAName);
            await Assertions.Expect(contextBar).ToContainTextAsync(Count(2, "relationships"));
            await Assertions.Expect(contextBar).ToContainTextAsync(Count(1, "allocations"));
            await Assertions.Expect(requestCard).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-staffing-request-item")).ToHaveCountAsync(1);
            await page.GetByTestId("crmhr-assignments-tab-relationships").ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-assignment-item")).ToHaveCountAsync(2);
            await Assertions.Expect(AssignmentCard(page, seed.CustomerName)).ToContainTextAsync(customerNotes);
            await Assertions.Expect(AssignmentCard(page, seed.ManagerName)).ToBeVisibleAsync();
            await Assertions.Expect(AssignmentCard(page, seed.VendorName)).ToHaveCountAsync(0);
            Assert.Equal(seed.ProjectBVendorAssignmentId, Assert.Single(await ReadAssignmentsAsync(services, seed.ProjectBId, ProjectPartyAssignmentRole.Vendor)).Id);

            // The deep link restores the selected project after a fresh load of the document.
            await OpenWorkspaceAsync(page, oracle, $"{fixture.BaseUrl}/crm-hr/assignments?projectId={seed.ProjectAId:D}");
            await Assertions.Expect(contextBar).ToContainTextAsync(seed.ProjectAName);
            await Assertions.Expect(contextBar).ToContainTextAsync(Count(2, "relationships"));
            await Assertions.Expect(contextBar).ToContainTextAsync(Count(1, "allocations"));
            await page.GetByTestId("crmhr-assignments-tab-relationships").ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-assignment-item")).ToHaveCountAsync(2);
            await Assertions.Expect(AssignmentCard(page, seed.CustomerName)).ToContainTextAsync(customerNotes);
            await Assertions.Expect(AssignmentCard(page, seed.ManagerName).GetByTestId("crmhr-assignment-affiliation")).ToContainTextAsync(seed.CustomerName);
            await AssertUntouchedSearchBoxAsync(relationshipSearch);
            await page.GetByTestId("crmhr-assignments-tab-staffing").ClickAsync();
            await Assertions.Expect(requestCard).ToContainTextAsync($"Requested by {seed.RequesterName}");
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "08-deep-link-reload-1600.png") });

            // An unknown project never borrows another project's data.
            await OpenWorkspaceAsync(page, oracle, $"{fixture.BaseUrl}/crm-hr/assignments?projectId={Guid.NewGuid():D}");
            await Assertions.Expect(contextBar).ToContainTextAsync("The selected project record is unavailable.");
            await Assertions.Expect(contextBar).Not.ToContainTextAsync("Active project");
            await page.GetByTestId("crmhr-assignments-tab-relationships").ClickAsync();
            await Assertions.Expect(relationships).ToContainTextAsync("Choose a project to manage relationships");
            await Assertions.Expect(page.GetByTestId("crmhr-assignment-item")).ToHaveCountAsync(0);
            var unknownProjectText = await page.Locator("body").InnerTextAsync();
            Assert.All(
                new[] { seed.ProjectAName, seed.ProjectBName, seed.CustomerName, seed.ManagerName, seed.VendorName, requestTitle },
                value => Assert.DoesNotContain(value, unknownProjectText, StringComparison.Ordinal));
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "09-unknown-project-1600.png") });

            await File.WriteAllLinesAsync(Path.Combine(artifactsDir, "expected-teardown.txt"), oracle.ExpectedTeardown);
            await oracle.AssertCleanAsync();
            ClearFailure(artifactsDir);
        }
        catch (Exception exception)
        {
            await CaptureFailureAsync(fixture, page, artifactsDir, exception);
            throw;
        }
        finally
        {
            await context.Tracing.StopAsync(new() { Path = Path.Combine(artifactsDir, "trace.zip") });
        }
    }

    // A card is identified by its heading: a person's card also names the organization of the affiliation.
    private static ILocator AssignmentCard(IPage page, string partyName)
        => page.GetByTestId("crmhr-assignment-item").Filter(new() { Has = page.Locator("h3", new() { HasTextString = partyName }) });

    private static ILocator AllocationCard(IPage page, string partyName)
        => page.GetByTestId("crmhr-allocation-item").Filter(new() { Has = page.Locator("h3", new() { HasTextString = partyName }) });

    private static async Task AssertUntouchedSearchBoxAsync(ILocator search)
    {
        await Assertions.Expect(search).ToBeVisibleAsync();
        await Assertions.Expect(search).ToHaveValueAsync(string.Empty);
        var value = await search.InputValueAsync();
        Assert.All(SearchBackingFieldNames, name => Assert.DoesNotContain(name, value, StringComparison.OrdinalIgnoreCase));
    }

    // A full-page load of the workspace; no search box of the fresh document shows a backing field name.
    private static async Task OpenWorkspaceAsync(IPage page, CrmHrBrowserOracle oracle, string url)
    {
        await OpenAsync(page, oracle, url, WorkspaceTabs);
        var searchValues = await page.Locator("input[data-testid$='search']").EvaluateAllAsync<string[]>("inputs => inputs.map(input => input.value)");
        Assert.All(searchValues, value => Assert.All(SearchBackingFieldNames, name => Assert.DoesNotContain(name, value, StringComparison.OrdinalIgnoreCase)));
    }

    private static async Task ChooseProjectAsync(IPage page, Guid projectId, string projectName)
    {
        const string dialogTestId = "crmhr-assignment-project-picker";
        await page.GetByTestId("crmhr-assignment-project").ClickAsync();
        await PickRecordAsync(page, dialogTestId, projectName, $"crmhr-project-option-{projectId:N}");
    }

    private static Task PickPartyAsync(IPage page, string dialogTestId, string partyName, Guid partyId)
        => PickRecordAsync(page, dialogTestId, partyName, $"crmhr-party-option-{partyId:N}");

    private static async Task<IReadOnlyList<ProjectPartyAssignmentDetail>> ReadAssignmentsAsync(
        ServiceProvider services,
        Guid projectId,
        ProjectPartyAssignmentRole role)
    {
        await using var scope = services.CreateAsyncScope();
        var integration = scope.ServiceProvider.GetRequiredService<ProjectPartyIntegrationService>();
        var page = await integration.SearchAssignmentsDetailedAsync(new ProjectPartyAssignmentQuery(projectId, [role], PageSize: 50));
        Assert.Equal(page.TotalCount, page.Items.Count);
        return page.Items;
    }

    private static async Task<IReadOnlyList<StaffingRequestItemModel>> ReadStaffingRequestsAsync(ServiceProvider services, Guid projectId)
    {
        await using var scope = services.CreateAsyncScope();
        var hrService = scope.ServiceProvider.GetRequiredService<HrService>();
        var page = await hrService.SearchStaffingRequestsAsync(new StaffingRequestQuery(projectId, PageSize: 50));
        Assert.Equal(page.TotalCount, page.Items.Count);
        return page.Items;
    }

    private static async Task<SeededAssignments> SeedAsync(ServiceProvider services, string suffix)
    {
        await using var scope = services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var affiliationService = scope.ServiceProvider.GetRequiredService<IPartyOrganizationAffiliationService>();
        var hrService = scope.ServiceProvider.GetRequiredService<HrService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var admissions = scope.ServiceProvider.GetRequiredService<ProjectWriteAdmissionService>();
        var integration = scope.ServiceProvider.GetRequiredService<ProjectPartyIntegrationService>();

        var customerName = $"Journey Customer {suffix}";
        var vendorName = $"Journey Vendor {suffix}";
        var managerName = $"Journey Manager {suffix}";
        var workerName = $"Journey Worker {suffix}";
        var requesterName = $"Journey Requester {suffix}";
        var projectAName = $"Journey Project A {suffix}";
        var projectBName = $"Journey Project B {suffix}";
        var projectBRequestTitle = $"Vendor onboarding {suffix}";

        var customerId = await CreatePartyAsync(partyDirectoryService, customerName, PartyType.Organization, PartyRoleKind.Customer, $"customer.{suffix}@example.test");
        var vendorId = await CreatePartyAsync(partyDirectoryService, vendorName, PartyType.Organization, PartyRoleKind.Vendor, $"vendor.{suffix}@example.test");
        var managerId = await CreatePartyAsync(partyDirectoryService, managerName, PartyType.Person, PartyRoleKind.Employee, $"manager.{suffix}@example.test");
        var workerId = await CreatePartyAsync(partyDirectoryService, workerName, PartyType.Person, PartyRoleKind.Employee, $"worker.{suffix}@example.test");
        var requesterId = await CreatePartyAsync(partyDirectoryService, requesterName, PartyType.Person, PartyRoleKind.Stakeholder, $"requester.{suffix}@example.test");

        var managerAffiliationId = await CreateAffiliationAsync(affiliationService, managerId, customerId, "Delivery lead");
        await CreateWorkforceProfileAsync(hrService, workerId, "Platform engineer", hourlyCostRate: 50m);

        var projectAId = await CreateProjectAsync(projectsService, projectAName);
        var projectBId = await CreateProjectAsync(projectsService, projectBName);

        // Project B owns one relationship and one staffing request of its own, so a switch shows B's facts, not an
        // empty workspace that could hide a stale list.
        var admissionB = await admissions.CaptureAsync(projectBId);
        Assert.NotNull(admissionB);
        var vendorAssignment = await integration.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectBId,
            ExpectedProjectAdmission = admissionB,
            PartyId = vendorId,
            Role = ProjectPartyAssignmentRole.Vendor,
            IsPrimary = true,
            Source = "playwright-tests",
            Notes = $"Seeded vendor {suffix}"
        });
        Assert.True(vendorAssignment.IsSuccess, Join(vendorAssignment.Errors));
        var requestB = await hrService.SaveStaffingRequestAsync(new StaffingRequestEditorModel
        {
            ProjectId = projectBId,
            ExpectedProjectAdmission = admissionB,
            Title = projectBRequestTitle,
            NeededRole = "Vendor coordinator",
            AllocationPercent = 40m,
            Status = StaffingRequestStatus.Open
        });
        Assert.True(requestB.IsSuccess, Join(requestB.Errors));

        return new SeededAssignments(
            projectAId,
            projectAName,
            projectBId,
            projectBName,
            customerId,
            customerName,
            vendorId,
            vendorName,
            managerId,
            managerName,
            managerAffiliationId,
            workerId,
            workerName,
            requesterId,
            requesterName,
            vendorAssignment.Value,
            projectBRequestTitle);
    }

    private sealed record SeededAssignments(
        Guid ProjectAId,
        string ProjectAName,
        Guid ProjectBId,
        string ProjectBName,
        Guid CustomerId,
        string CustomerName,
        Guid VendorId,
        string VendorName,
        Guid ManagerId,
        string ManagerName,
        Guid ManagerAffiliationId,
        Guid WorkerId,
        string WorkerName,
        Guid RequesterId,
        string RequesterName,
        Guid ProjectBVendorAssignmentId,
        string ProjectBRequestTitle);
}
