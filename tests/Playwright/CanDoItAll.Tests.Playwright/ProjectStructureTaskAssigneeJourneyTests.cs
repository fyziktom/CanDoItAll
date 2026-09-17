using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using static CanDoItAll.Tests.Playwright.Smoke.CrmHrAssignmentsJourneySupport;

namespace CanDoItAll.Tests.Playwright.Smoke;

// Task assignees across Project Structure and CRM / HR through the real Web host and PostgreSQL. Work owns the direct
// assignee of a task (Workbench_WorkAssignments); CRM / HR owns parties, affiliations, rates and participation. The
// journey chooses a person and a synchronized AI agent in the real task dialog, replaces and clears an assignee, and
// then reads the same facts in the Gantt view, the CRM / HR Assignments workspace and the person's workforce record.
[Collection(PlaywrightCollection.Name)]
public sealed class ProjectStructureTaskAssigneeJourneyTests
{
    private const string StructureTabs = "[role='tablist'][aria-label='Project structure views'] [role='tab']";
    private const string AssignmentTabs = "[data-testid='crmhr-assignments-workspace-tabs'] [role='tab']";
    private const string AssignmentSource = "project-structure-task-dialog";
    private const string PersonQuote = "CRM workforce rate: Calculated from 8 hour(s) at the CRM workforce rate per hour.";
    private const string AgentQuote = "Agent run history: No completed or attempted agent run is available for a historical price estimate.";

    private readonly PlaywrightAppFixture fixture;

    public ProjectStructureTaskAssigneeJourneyTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Task_assignees_are_chosen_replaced_and_cleared_in_project_structure_and_stay_work_owned_facts_for_crm_hr()
    {
        var artifactsDir = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "project-structure-task-assignee-journey");
        Directory.CreateDirectory(artifactsDir);
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var services = await BuildProviderAsync(fixture, "playwright-task-assignee-journey-seed");
        var seed = await SeedAsync(services, suffix);
        var keptTitle = $"Kept assignee task {suffix}";
        var agentTitle = $"Agent assignee task {suffix}";
        var lifecycleTitle = $"Assignee lifecycle task {suffix}";

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1600, Height = 1000 }
        });
        await context.Tracing.StartAsync(new() { Screenshots = true, Snapshots = true });
        var page = await context.NewPageAsync();
        try
        {
            var oracle = CrmHrBrowserOracle.Attach(page);
            var structureUrl = $"{fixture.BaseUrl}/projects/{seed.ProjectId:D}/structure";
            await OpenAsync(page, oracle, structureUrl, StructureTabs);
            await Assertions.Expect(page.GetByTestId("project-structure-canvas-loaded")).ToBeVisibleAsync(new() { Timeout = 60_000 });

            // Task 1: the seeded person, chosen in the real assignee picker. The organization is never offered.
            var createDialog = await OpenTaskCreateDialogAsync(page, seed.ProjectId);
            var pickerSearch = page.GetByTestId("project-structure-task-create-assignee-picker-search");
            var picker = page.GetByTestId("project-structure-task-create-assignee-picker");
            await Assertions.Expect(page.GetByTestId("project-structure-task-create-assignee-picker-filter-person")).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByTestId("project-structure-task-create-assignee-picker-filter-agent")).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByTestId("project-structure-task-create-assignee-picker-filter-workflow")).ToHaveCountAsync(0);
            await Assertions.Expect(picker.Locator($"[data-testid*='{seed.OrganizationId:N}']")).ToHaveCountAsync(0);
            await pickerSearch.FillAsync(seed.OrganizationName);
            // The organization's name only finds the person affiliated to it; no card carries the organization's identity.
            await Assertions.Expect(PersonOption(page, seed.PersonOneId)).ToBeVisibleAsync();
            await Assertions.Expect(picker.Locator(".resource-card-picker__select")).ToHaveCountAsync(1);
            await Assertions.Expect(picker.Locator($"[data-testid*='{seed.OrganizationId:N}']")).ToHaveCountAsync(0);
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "01-organization-not-offered-1600.png") });

            await page.GetByTestId("project-structure-task-create-title").FillAsync(keptTitle);
            await page.GetByTestId("project-structure-task-create-notes").FillAsync($"Stays assigned to the first person {suffix}");
            await Assertions.Expect(page.GetByTestId("project-structure-task-create-estimate-effort")).ToHaveValueAsync("8");
            await pickerSearch.FillAsync(seed.PersonOneName);
            await PersonOption(page, seed.PersonOneId).ClickAsync();
            await Assertions.Expect(PersonOption(page, seed.PersonOneId)).ToHaveAttributeAsync("aria-pressed", "true");
            // The quote is the seeded rate times the default effort: 40 USD per hour for 8 hours.
            await Assertions.Expect(page.GetByTestId("project-structure-task-create-resource-cost-status")).ToContainTextAsync(PersonQuote);
            await Assertions.Expect(page.GetByTestId("project-structure-task-create-estimate-cost")).ToHaveValueAsync("320");
            await Assertions.Expect(page.GetByTestId("project-structure-task-create-estimate-currency")).ToHaveValueAsync("USD");
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "02-create-task-person-1600.png") });
            await SubmitTaskDialogAsync(page, createDialog, $"{keptTitle} was added with its selected assignee.");
            await CloseToolboxAsync(page);

            var kept = await ReadTaskAsync(services, seed.ProjectId, keptTitle);
            var keptFact = Assert.Single(kept.WorkFacts);
            Assert.Equal(seed.PersonOneId, keptFact.PartyId);
            Assert.Equal(seed.PersonOneAffiliationId, keptFact.PartyOrganizationAffiliationId);
            Assert.Equal(AssignmentSource, keptFact.Source);
            Assert.True(keptFact.IsPrimary);
            Assert.Null(keptFact.AllocationPercent);
            var keptAssignment = Assert.Single(kept.DirectAssignments);
            Assert.Equal(keptFact.Id, keptAssignment.Id);
            Assert.Equal(ProjectPartyType.Person, keptAssignment.PartyType);
            Assert.Equal(ProjectPartyAssignmentRole.WorkItemAssignee, keptAssignment.Role);
            Assert.Equal(320m, kept.WorkItem.ExpectedCostAmount);
            Assert.Equal("USD", kept.WorkItem.ExpectedCostCurrencyCode);
            Assert.Equal(8m, kept.WorkItem.ExpectedEffortHours);
            Assert.Equal(0, await CountCrmParticipationRowsAsync(services, seed.ProjectId));

            // Task 2: a synchronized AI agent. The owner stores the agent's CRM party, never the technical agent id.
            createDialog = await OpenTaskCreateDialogAsync(page, seed.ProjectId);
            await page.GetByTestId("project-structure-task-create-title").FillAsync(agentTitle);
            await pickerSearch.FillAsync(seed.AgentName);
            await AgentOption(page, seed.AgentPartyId).ClickAsync();
            await Assertions.Expect(AgentOption(page, seed.AgentPartyId)).ToHaveAttributeAsync("aria-pressed", "true");
            await Assertions.Expect(picker.Locator($"[data-testid*='{seed.TechnicalAgentId:N}']")).ToHaveCountAsync(0);
            await Assertions.Expect(page.GetByTestId("project-structure-task-create-resource-cost-status")).ToContainTextAsync(AgentQuote);
            await Assertions.Expect(page.GetByTestId("project-structure-task-create-estimate-cost")).ToHaveValueAsync(string.Empty);
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "03-create-task-agent-1600.png") });
            await SubmitTaskDialogAsync(page, createDialog, $"{agentTitle} was added with its selected assignee.");
            await CloseToolboxAsync(page);

            var agentTask = await ReadTaskAsync(services, seed.ProjectId, agentTitle);
            var agentFact = Assert.Single(agentTask.WorkFacts);
            Assert.Equal(seed.AgentPartyId, agentFact.PartyId);
            Assert.NotEqual(seed.TechnicalAgentId, agentFact.PartyId);
            var agentAssignment = Assert.Single(agentTask.DirectAssignments);
            Assert.Equal(ProjectPartyType.AiAgent, agentAssignment.PartyType);
            Assert.Equal(seed.AgentName, agentAssignment.PartyDisplayName);
            Assert.Null(agentTask.WorkItem.ExpectedCostAmount);
            Assert.Equal(seed.TechnicalAgentId, await ReadBoundTechnicalAgentAsync(services, seed.AgentPartyId));

            // Task 3: created for the first person, then replaced and cleared through the edit dialog.
            createDialog = await OpenTaskCreateDialogAsync(page, seed.ProjectId);
            await page.GetByTestId("project-structure-task-create-title").FillAsync(lifecycleTitle);
            await pickerSearch.FillAsync(seed.PersonOneName);
            await PersonOption(page, seed.PersonOneId).ClickAsync();
            await Assertions.Expect(page.GetByTestId("project-structure-task-create-estimate-cost")).ToHaveValueAsync("320");
            await SubmitTaskDialogAsync(page, createDialog, $"{lifecycleTitle} was added with its selected assignee.");
            await CloseToolboxAsync(page);
            var lifecycle = await ReadTaskAsync(services, seed.ProjectId, lifecycleTitle);
            var firstLifecycleFact = Assert.Single(lifecycle.WorkFacts);
            Assert.Equal(seed.PersonOneId, firstLifecycleFact.PartyId);

            // Replace: the edit dialog opens on the saved assignee; the second person re-prices the task at 65 USD per hour.
            var editDialog = await OpenTaskEditDialogAsync(page, lifecycle.NodeId);
            await Assertions.Expect(page.GetByTestId("project-structure-task-create-title")).ToHaveValueAsync(lifecycleTitle);
            await Assertions.Expect(PersonOption(page, seed.PersonOneId)).ToHaveAttributeAsync("aria-pressed", "true");
            await Assertions.Expect(page.GetByTestId("project-structure-task-create-estimate-cost")).ToHaveValueAsync("320");
            await pickerSearch.FillAsync(seed.PersonTwoName);
            await PersonOption(page, seed.PersonTwoId).ClickAsync();
            await Assertions.Expect(PersonOption(page, seed.PersonTwoId)).ToHaveAttributeAsync("aria-pressed", "true");
            await Assertions.Expect(page.GetByTestId("project-structure-task-create-resource-cost-status")).ToContainTextAsync(PersonQuote);
            await Assertions.Expect(page.GetByTestId("project-structure-task-create-estimate-cost")).ToHaveValueAsync("520");

            // Constrained width with the task dialog open: the dialog and its actions stay reachable, nothing overflows.
            await page.SetViewportSizeAsync(1100, 900);
            await page.WaitForTimeoutAsync(300);
            await Assertions.Expect(editDialog).ToBeVisibleAsync();
            await page.GetByTestId("project-structure-task-create-submit").ScrollIntoViewIfNeededAsync();
            await Assertions.Expect(page.GetByTestId("project-structure-task-create-submit")).ToBeInViewportAsync();
            await Assertions.Expect(page.GetByTestId("project-structure-task-create-cancel")).ToBeInViewportAsync();
            Assert.False(await page.EvaluateAsync<bool>(OverflowProbe), "The document overflows horizontally at 1100 px with the task dialog open.");
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "04-edit-task-second-person-1100.png") });
            await page.SetViewportSizeAsync(1600, 1000);
            await page.WaitForTimeoutAsync(300);

            await SubmitTaskDialogAsync(page, editDialog, $"{lifecycleTitle} was updated.");
            await CloseObjectIndexAsync(page);
            lifecycle = await ReadTaskAsync(services, seed.ProjectId, lifecycleTitle);
            var replacedFact = Assert.Single(lifecycle.WorkFacts);
            Assert.Equal(seed.PersonTwoId, replacedFact.PartyId);
            Assert.DoesNotContain(lifecycle.WorkFacts, fact => fact.PartyId == seed.PersonOneId);
            Assert.Equal(ProjectPartyType.Person, Assert.Single(lifecycle.DirectAssignments).PartyType);
            Assert.Equal(520m, lifecycle.WorkItem.ExpectedCostAmount);
            Assert.Equal("USD", lifecycle.WorkItem.ExpectedCostCurrencyCode);

            // Clear: an explicit empty replacement. The task stays, its direct assignments and its resource price go.
            editDialog = await OpenTaskEditDialogAsync(page, lifecycle.NodeId);
            await Assertions.Expect(PersonOption(page, seed.PersonTwoId)).ToHaveAttributeAsync("aria-pressed", "true");
            await page.GetByTestId("project-structure-task-create-assignee-clear").ClickAsync();
            await Assertions.Expect(page.GetByTestId("project-structure-task-create-assignee-clear")).ToHaveCountAsync(0);
            await Assertions.Expect(PersonOption(page, seed.PersonTwoId)).ToHaveAttributeAsync("aria-pressed", "false");
            await Assertions.Expect(page.GetByTestId("project-structure-task-create-resource-cost")).ToHaveCountAsync(0);
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "05-edit-task-cleared-1600.png") });
            await SubmitTaskDialogAsync(page, editDialog, $"{lifecycleTitle} was updated.");
            await CloseObjectIndexAsync(page);
            lifecycle = await ReadTaskAsync(services, seed.ProjectId, lifecycleTitle);
            Assert.Empty(lifecycle.WorkFacts);
            Assert.Empty(lifecycle.DirectAssignments);
            Assert.Null(lifecycle.WorkItem.ExpectedCostAmount);
            // The other tasks keep exactly their own assignee.
            Assert.Equal(keptFact.Id, Assert.Single((await ReadTaskAsync(services, seed.ProjectId, keptTitle)).WorkFacts).Id);
            Assert.Equal(agentFact.Id, Assert.Single((await ReadTaskAsync(services, seed.ProjectId, agentTitle)).WorkFacts).Id);

            // The owner does not accept an organization as a task assignee either.
            Assert.Equal(
                "crmhr.project-assignment.work-item-assignee-party-type-invalid",
                await TryAssignOrganizationAsync(services, seed.ProjectId, seed.OrganizationId, kept.NodeId));
            Assert.Equal(keptFact.Id, Assert.Single((await ReadTaskAsync(services, seed.ProjectId, keptTitle)).WorkFacts).Id);

            // A fresh load of the deep link: the edit dialogs open on the saved assignee state.
            await OpenAsync(page, oracle, structureUrl, StructureTabs);
            await Assertions.Expect(page.GetByTestId("project-structure-canvas-loaded")).ToBeVisibleAsync(new() { Timeout = 60_000 });
            editDialog = await OpenTaskEditDialogAsync(page, lifecycle.NodeId, indexIsOpen: false);
            await Assertions.Expect(page.GetByTestId("project-structure-task-create-title")).ToHaveValueAsync(lifecycleTitle);
            await Assertions.Expect(page.GetByTestId("project-structure-task-create-assignee-clear")).ToHaveCountAsync(0);
            await Assertions.Expect(picker.Locator(".resource-card-picker__select[aria-pressed='true']")).ToHaveCountAsync(0);
            await Assertions.Expect(page.GetByTestId("project-structure-task-create-estimate-cost")).ToHaveValueAsync(string.Empty);
            await page.GetByTestId("project-structure-task-create-cancel").ClickAsync();
            await Assertions.Expect(editDialog).ToHaveCountAsync(0);
            editDialog = await OpenTaskEditDialogAsync(page, agentTask.NodeId, indexIsOpen: true);
            await Assertions.Expect(page.GetByTestId("project-structure-task-create-title")).ToHaveValueAsync(agentTitle);
            await Assertions.Expect(AgentOption(page, seed.AgentPartyId)).ToHaveAttributeAsync("aria-pressed", "true");
            await Assertions.Expect(picker.Locator(".resource-card-picker__select[aria-pressed='true']")).ToHaveCountAsync(1);
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "06-reload-agent-task-1600.png") });
            await page.GetByTestId("project-structure-task-create-cancel").ClickAsync();
            await Assertions.Expect(editDialog).ToHaveCountAsync(0);

            // The Gantt view: one row per task with real geometry, a painted bar, and the canonical assignee projection.
            await OpenAsync(page, oracle, $"{structureUrl}?tab=gantt", StructureTabs);
            var ganttPanel = page.GetByTestId("project-structure-gantt-panel");
            await Assertions.Expect(ganttPanel).ToContainTextAsync(Count(3, "tasks"), new() { Timeout = 60_000 });
            await Assertions.Expect(ganttPanel).ToContainTextAsync(Count(24, "h pure effort"));
            var gantt = await ReadGanttAsync(page, "project-structure-gantt-chart", [keptTitle, agentTitle, lifecycleTitle]);
            Assert.Equal(new[] { $"Person: {seed.PersonOneName}" }, GanttAssignments(GanttRow(gantt, keptTitle)));
            Assert.Equal(new[] { $"Agent: {seed.AgentName}" }, GanttAssignments(GanttRow(gantt, agentTitle)));
            Assert.Empty(GanttAssignments(GanttRow(gantt, lifecycleTitle)));
            Assert.True(gantt.GetProperty("canvasBackingWidth").GetInt32() > 100, "The Gantt canvas has no backing store.");
            Assert.True(gantt.GetProperty("canvasBoxHeight").GetDouble() >= 40 + 3 * 48, "The Gantt canvas is shorter than its three rows.");
            Assert.All(new[] { keptTitle, agentTitle, lifecycleTitle }, title =>
            {
                var row = GanttRow(gantt, title);
                Assert.True(row.GetProperty("width").GetDouble() > 100, $"The Gantt row of '{title}' has no width.");
                Assert.Equal(48d, row.GetProperty("height").GetDouble(), 1);
                Assert.True(row.GetProperty("barWidth").GetDouble() > 8, $"The Gantt bar of '{title}' is not painted.");
            });
            // The only priced task left is the kept one: 320 in the host's currency format.
            var costPill = ganttPanel.Locator(".project-structure-gantt-panel__utility-row").GetByText(new Regex(" expected$"));
            await Assertions.Expect(costPill).ToHaveCountAsync(1);
            Assert.Contains("320", Digits(await costPill.InnerTextAsync()), StringComparison.Ordinal);
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "07-gantt-1600.png") });

            // CRM / HR Assignments: the Work-owned assignees are reported under their explicit role with their task,
            // and under no participation role.
            await OpenAsync(page, oracle, $"{fixture.BaseUrl}/crm-hr/assignments?projectId={seed.ProjectId:D}", AssignmentTabs);
            var contextBar = page.GetByTestId("crmhr-assignments-project-context");
            await Assertions.Expect(contextBar).ToContainTextAsync("Active project");
            await Assertions.Expect(contextBar).ToContainTextAsync(seed.ProjectName);
            await Assertions.Expect(contextBar).ToContainTextAsync(Count(2, "relationships"));
            await page.GetByTestId("crmhr-assignments-tab-relationships").ClickAsync();
            var relationships = page.GetByTestId("crmhr-assignment-page");
            await Assertions.Expect(relationships).ToContainTextAsync($"2 assignment(s) for {seed.ProjectName}");
            var personCard = page.GetByTestId("crmhr-assignment-item").Filter(new() { Has = page.Locator("h3", new() { HasTextString = seed.PersonOneName }) });
            await Assertions.Expect(personCard).ToContainTextAsync("Work-item assignee");
            await Assertions.Expect(personCard).ToContainTextAsync(kept.NodeId);
            await Assertions.Expect(personCard.GetByTestId("crmhr-assignment-affiliation")).ToContainTextAsync(seed.OrganizationName);
            var agentCard = page.GetByTestId("crmhr-assignment-item").Filter(new() { Has = page.Locator("h3", new() { HasTextString = seed.AgentName }) });
            await Assertions.Expect(agentCard).ToContainTextAsync("Work-item assignee");
            await Assertions.Expect(agentCard).ToContainTextAsync(agentTask.NodeId);
            await Assertions.Expect(page.GetByTestId("crmhr-assignment-item").Filter(new() { HasText = seed.PersonTwoName })).ToHaveCountAsync(0);
            await Assertions.Expect(page.GetByTestId("crmhr-assignment-item").Filter(new() { HasText = lifecycle.NodeId })).ToHaveCountAsync(0);
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "08-crm-assignments-1600.png") });
            var roleFilter = page.GetByTestId("crmhr-assignment-role-filter");
            await roleFilter.SelectOptionAsync(nameof(ProjectPartyAssignmentRole.TeamMember));
            await Assertions.Expect(relationships).ToContainTextAsync(Count(0, "matching assignment(s)"));
            await Assertions.Expect(page.GetByTestId("crmhr-assignment-item")).ToHaveCountAsync(0);
            await roleFilter.SelectOptionAsync(nameof(ProjectPartyAssignmentRole.WorkItemAssignee));
            await Assertions.Expect(relationships).ToContainTextAsync(Count(2, "matching assignment(s)"));
            await Assertions.Expect(page.GetByTestId("crmhr-assignment-item")).ToHaveCountAsync(2);
            Assert.Equal(0, await CountCrmParticipationRowsAsync(services, seed.ProjectId));

            // Workforce: a task assignment is not an allocation. The person's capacity view stays free of commitments.
            var response = await oracle.NavigateAsync($"{fixture.BaseUrl}/crm-hr/workforce?partyId={seed.PersonOneId:D}");
            Assert.True(response?.Ok, $"Expected the Workforce route to return 2xx, got {(int?)response?.Status}.");
            await PlaywrightAppFixture.CompleteDatabaseStartupAsync(page);
            await Assertions.Expect(page.GetByTestId("crmhr-workforce-search")).ToBeEnabledAsync(new() { Timeout = 60_000 });
            await Assertions.Expect(page.GetByTestId("crmhr-workforce-record-dialog")).ToContainTextAsync(seed.PersonOneName);
            await page.GetByTestId("crmhr-workforce-tab-allocations").ClickAsync();
            var allocationsPanel = page.GetByTestId("crmhr-workforce-allocations-panel");
            await Assertions.Expect(allocationsPanel).ToContainTextAsync("0 project-linked commitment(s)", new() { Timeout = 30_000 });
            await Assertions.Expect(allocationsPanel).ToContainTextAsync("No project allocations are currently pushing capacity from the project module.");
            await Assertions.Expect(page.GetByTestId("crmhr-workforce-allocation-item")).ToHaveCountAsync(0);
            await Assertions.Expect(page.GetByTestId("crmhr-workforce-allocation-gantt")).ToHaveCountAsync(0);
            await Assertions.Expect(page.GetByTestId("crmhr-capacity-conflict-callout")).ToHaveCountAsync(0);
            await page.ScreenshotAsync(new() { Path = Path.Combine(artifactsDir, "09-workforce-allocations-1600.png") });
            Assert.Empty(await ReadWorkforceAllocationsAsync(services, seed.PersonOneId));

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

    private static ILocator PersonOption(IPage page, Guid partyId)
        => page.GetByTestId($"project-structure-task-create-assignee-person-{partyId:N}");

    private static ILocator AgentOption(IPage page, Guid partyId)
        => page.GetByTestId($"project-structure-task-create-assignee-agent-{partyId:N}");

    private static string Digits(string text)
        => new(text.Where(char.IsAsciiDigit).ToArray());

    // The object index outline names a row after its node: every character that is not a letter or digit becomes '-'.
    private static string OutlineNodeTestId(string nodeId)
        => "project-structure-outline-node-" + new string(nodeId.Select(character => char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : '-').ToArray());

    // Every task of the journey is placed from the project root: the root is selected in the object index, the index
    // is closed again because floating windows may overlap, and the task entry of the toolbox opens the dialog.
    private static async Task<ILocator> OpenTaskCreateDialogAsync(IPage page, Guid projectId)
    {
        var index = page.GetByTestId("project-structure-object-index-window");
        await page.GetByTestId("project-structure-object-index-toggle").ClickAsync();
        await Assertions.Expect(index).ToBeVisibleAsync();
        var root = page.GetByTestId(OutlineNodeTestId($"project:{projectId:D}"));
        await root.ClickAsync();
        await Assertions.Expect(root).ToHaveAttributeAsync("aria-selected", "true");
        await CloseObjectIndexAsync(page);

        await page.GetByTestId("project-structure-toolbox-toggle").ClickAsync();
        await Assertions.Expect(page.GetByTestId("project-structure-toolbox-window")).ToBeVisibleAsync();
        await page.GetByTestId("project-structure-toolbox-group-work").ClickAsync();
        await Assertions.Expect(page.GetByTestId("project-structure-toolbox-group-body-work")).ToBeVisibleAsync();
        await page.GetByTestId("project-structure-toolbox-add-work-task").ClickAsync();
        var dialog = page.GetByTestId("project-structure-task-create-dialog");
        await Assertions.Expect(dialog).ToBeVisibleAsync();
        return dialog;
    }

    // The supported DOM path to a task's editor: the object index row's context menu.
    private static async Task<ILocator> OpenTaskEditDialogAsync(IPage page, string nodeId, bool indexIsOpen = false)
    {
        var index = page.GetByTestId("project-structure-object-index-window");
        if (!indexIsOpen)
        {
            await page.GetByTestId("project-structure-object-index-toggle").ClickAsync();
        }

        await Assertions.Expect(index).ToBeVisibleAsync();
        await page.GetByTestId(OutlineNodeTestId(nodeId)).ClickAsync(new() { Button = MouseButton.Right });
        await Assertions.Expect(page.GetByTestId("project-structure-outline-context-menu")).ToBeVisibleAsync();
        await page.GetByTestId("project-structure-outline-context-action-edit").ClickAsync();
        var dialog = page.GetByTestId("project-structure-task-edit-dialog");
        await Assertions.Expect(dialog).ToBeVisibleAsync();
        return dialog;
    }

    // The dialog closes before the page commits the task and its assignee. The completion notice names the task and
    // is raised after the authoritative reload, so the owner is read only once the whole save has finished. A notice
    // of an earlier save of the same task must be gone before the next save starts.
    private static async Task SubmitTaskDialogAsync(IPage page, ILocator dialog, string completion)
    {
        var notices = page.Locator(".rz-notification");
        await Assertions.Expect(notices).Not.ToContainTextAsync(completion, new() { Timeout = 15_000 });
        await page.GetByTestId("project-structure-task-create-submit").ClickAsync();
        await Assertions.Expect(dialog).ToHaveCountAsync(0, new() { Timeout = 60_000 });
        await Assertions.Expect(notices).ToContainTextAsync(completion, new() { Timeout = 60_000 });
    }

    private static async Task CloseToolboxAsync(IPage page)
    {
        await page.GetByTestId("project-structure-toolbox-toggle").ClickAsync();
        await Assertions.Expect(page.GetByTestId("project-structure-toolbox-window")).ToBeHiddenAsync();
    }

    private static async Task CloseObjectIndexAsync(IPage page)
    {
        await page.GetByTestId("project-structure-object-index-toggle").ClickAsync();
        await Assertions.Expect(page.GetByTestId("project-structure-object-index-window")).ToBeHiddenAsync();
    }

    // The Work owner's facts of one task: the node, its pricing, the rows of Workbench_WorkAssignments for its node
    // key, and the owner's typed direct assignments (party type and role).
    private static async Task<TaskFacts> ReadTaskAsync(ServiceProvider services, Guid projectId, string title)
    {
        await using var scope = services.CreateAsyncScope();
        var surface = await scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>().GetStructureAsync(projectId);
        var node = Assert.Single(surface.Nodes, candidate => candidate.Title == title);
        Assert.Equal(ProjectObjectType.WorkItem, node.ObjectType);
        Assert.Equal("task", node.ObjectSubtype);
        var workItem = ProjectObjectMetadataSerializer.Parse(node.MetadataJson).WorkItem;
        Assert.NotNull(workItem);
        var workFacts = (await scope.ServiceProvider.GetRequiredService<IProjectWorkAssignmentQueries>().ListForProjectsAsync([projectId]))
            .Where(fact => fact.NodeKey == node.Id)
            .ToArray();
        var snapshot = await scope.ServiceProvider.GetRequiredService<ProjectStructureWorkItemAssigneeService>().ReadAsync(projectId, node.Id);
        return new TaskFacts(node.Id, workItem!, workFacts, snapshot.DirectAssignments);
    }

    private static async Task<int> CountCrmParticipationRowsAsync(ServiceProvider services, Guid projectId)
    {
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CrmHrDbContext>>();
        await using var dbContext = await factory.CreateDbContextAsync();
        return await dbContext.Set<ProjectPartyAssignment>().CountAsync(row => row.ProjectId == projectId);
    }

    private static async Task<Guid?> ReadBoundTechnicalAgentAsync(ServiceProvider services, Guid agentPartyId)
    {
        await using var scope = services.CreateAsyncScope();
        var directory = await scope.ServiceProvider.GetRequiredService<AiAgentService>().ListAgentDirectoryAsync();
        var agent = Assert.Single(directory, item => item.PartyId == agentPartyId);
        Assert.Equal(AiResourceBindingStatus.Bound, agent.BindingStatus);
        return agent.TechnicalAgentId;
    }

    private static async Task<IReadOnlyList<ProjectAllocationItemModel>> ReadWorkforceAllocationsAsync(ServiceProvider services, Guid partyId)
    {
        await using var scope = services.CreateAsyncScope();
        var workspace = await scope.ServiceProvider.GetRequiredService<HrService>().GetWorkforceCapacityWorkspaceAsync(partyId);
        Assert.NotNull(workspace);
        return workspace!.ProjectAllocations;
    }

    private static async Task<string> TryAssignOrganizationAsync(ServiceProvider services, Guid projectId, Guid organizationId, string nodeId)
    {
        await using var scope = services.CreateAsyncScope();
        var admission = await scope.ServiceProvider.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(projectId);
        Assert.NotNull(admission);
        var result = await scope.ServiceProvider.GetRequiredService<ProjectPartyIntegrationService>().SaveAssignmentAsync(
            new ProjectPartyAssignmentUpsertRequest
            {
                ProjectId = projectId,
                ExpectedProjectAdmission = admission,
                PartyId = organizationId,
                Role = ProjectPartyAssignmentRole.WorkItemAssignee,
                NodeKey = nodeId,
                IsPrimary = true,
                Source = "playwright-tests"
            });
        Assert.True(result.IsFailure, "The owner accepted an organization as a task assignee.");
        return Assert.Single(result.Errors).Code;
    }

    private static async Task<SeededAssignees> SeedAsync(ServiceProvider services, string suffix)
    {
        await using var scope = services.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var affiliationService = scope.ServiceProvider.GetRequiredService<IPartyOrganizationAffiliationService>();
        var hrService = scope.ServiceProvider.GetRequiredService<HrService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();

        var organizationName = $"Assignee Employer {suffix}";
        var personOneName = $"Assignee One {suffix}";
        var personTwoName = $"Assignee Two {suffix}";
        var agentName = $"Assignee Agent {suffix}";
        var projectName = $"Assignee Journey Project {suffix}";

        var organizationId = await CreatePartyAsync(partyDirectoryService, organizationName, PartyType.Organization, PartyRoleKind.Customer, $"employer.{suffix}@example.test");
        var personOneId = await CreatePartyAsync(partyDirectoryService, personOneName, PartyType.Person, PartyRoleKind.Employee, $"assignee-one.{suffix}@example.test");
        var personTwoId = await CreatePartyAsync(partyDirectoryService, personTwoName, PartyType.Person, PartyRoleKind.Employee, $"assignee-two.{suffix}@example.test");
        var personOneAffiliationId = await CreateAffiliationAsync(affiliationService, personOneId, organizationId, "Delivery engineer");
        await CreateWorkforceProfileAsync(hrService, personOneId, "Delivery engineer", hourlyCostRate: 40m);
        await CreateWorkforceProfileAsync(hrService, personTwoId, "Platform engineer", hourlyCostRate: 65m);

        // A technical agent saved through the AgentFramework workspace is projected into CRM / HR as an AI-agent party
        // with a durable binding; the party, not the technical id, is what a task can be assigned to.
        var technicalAgentId = await scope.ServiceProvider.GetRequiredService<IAgentFrameworkWorkspaceService>().SaveAgentAsync(new AgentEditorModel
        {
            Name = agentName,
            RoleTitle = "Delivery analyst",
            Summary = "Synthetic agent of the task assignee journey.",
            Instructions = "Review the brief and report the delivery evidence."
        });
        var directory = await scope.ServiceProvider.GetRequiredService<AiAgentService>().ListAgentDirectoryAsync();
        var agent = Assert.Single(directory, item => item.TechnicalAgentId == technicalAgentId);
        Assert.Equal(agentName, agent.DisplayName);
        Assert.NotEqual(technicalAgentId, agent.PartyId);

        var projectId = await CreateProjectAsync(projectsService, projectName);
        return new SeededAssignees(
            projectId,
            projectName,
            organizationId,
            organizationName,
            personOneId,
            personOneName,
            personOneAffiliationId,
            personTwoId,
            personTwoName,
            agent.PartyId,
            agentName,
            technicalAgentId);
    }

    private sealed record TaskFacts(
        string NodeId,
        ProjectWorkItemMetadata WorkItem,
        IReadOnlyList<ProjectWorkAssignmentFact> WorkFacts,
        IReadOnlyList<ProjectPartyAssignmentDetail> DirectAssignments);

    private sealed record SeededAssignees(
        Guid ProjectId,
        string ProjectName,
        Guid OrganizationId,
        string OrganizationName,
        Guid PersonOneId,
        string PersonOneName,
        Guid PersonOneAffiliationId,
        Guid PersonTwoId,
        string PersonTwoName,
        Guid AgentPartyId,
        string AgentName,
        Guid TechnicalAgentId);
}
