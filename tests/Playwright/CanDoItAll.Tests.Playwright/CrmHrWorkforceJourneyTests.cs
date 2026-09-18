using System.Globalization;
using System.Text.Json;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

// The Workforce workspace through the real Web host and PostgreSQL: a seeded person opened from the real catalog, the
// staffable profile created and edited through the Profile form, a skill definition and a party skill, a leave block
// added and deleted on the Allocations tab next to the plotted allocation schedule, the independent history lane, and
// the reloaded deep link. Every write is read back through HrService by the seeded party identifier.
[Collection(PlaywrightCollection.Name)]
public sealed class CrmHrWorkforceJourneyTests
{
    // The allocation schedule is a canvas next to an aligned task table (CapacityTimelinePanel: RowHeight 48,
    // HeaderHeight 40, BarHeight 26). The probe reads the table rows with their real geometry and, per row, the plotted
    // bar from the canvas pixels on a scan line inside the bar band, above the bar's caption. The surface colour is
    // the one in the timeline gutter left of the first bar; the bar colour is the dominant other colour of the scan
    // line (grid and "now" lines are one to two pixels wide), and the bar spans its first to its last pixel.
    private const string GanttProbe = """
        () => {
            const host = document.querySelector('[data-testid="crmhr-workforce-allocation-gantt"]');
            if (!host) { return { present: false, rows: [], bars: [] }; }
            const headerRow = host.querySelector('.cda-gantt__table-row--header');
            const canvas = host.querySelector('canvas.cda-gantt__canvas');
            if (!headerRow || !canvas) { return { present: false, rows: [], bars: [] }; }
            const rows = Array.from(host.querySelectorAll('.cda-gantt__table-row:not(.cda-gantt__table-row--header)')).map(row => {
                const box = row.getBoundingClientRect();
                const title = row.querySelector('.cda-gantt__task-button');
                return { title: title ? title.textContent.trim() : '', width: box.width, height: box.height };
            });
            const canvasBox = canvas.getBoundingClientRect();
            const headerHeight = headerRow.getBoundingClientRect().height;
            const scaleX = canvas.width / canvasBox.width;
            const scaleY = canvas.height / canvasBox.height;
            const context = canvas.getContext('2d');
            const barHeight = 26;
            const bars = rows.map((row, index) => {
                const cssY = headerHeight + (index * row.height) + ((row.height - barHeight) / 2) + 4;
                const y = Math.floor(cssY * scaleY);
                if (y < 0 || y >= canvas.height) { return null; }
                const data = context.getImageData(0, y, canvas.width, 1).data;
                const colourAt = x => `${data[4 * x]},${data[(4 * x) + 1]},${data[(4 * x) + 2]},${data[(4 * x) + 3]}`;
                const surface = colourAt(Math.floor(3 * scaleX));
                const totals = new Map();
                for (let x = 0; x < canvas.width; x += 1) {
                    const key = colourAt(x);
                    if (key !== surface) { totals.set(key, (totals.get(key) ?? 0) + 1); }
                }
                if (totals.size === 0) { return null; }
                const [barColour, barPixels] = Array.from(totals.entries()).sort((left, right) => right[1] - left[1])[0];
                let first = -1;
                let last = -1;
                for (let x = 0; x < canvas.width; x += 1) {
                    if (colourAt(x) === barColour) {
                        if (first < 0) { first = x; }
                        last = x;
                    }
                }
                return { start: first / scaleX, length: (last - first + 1) / scaleX, pixels: barPixels / scaleX, colour: barColour };
            });
            return {
                present: true,
                rows,
                bars,
                canvasWidth: canvasBox.width,
                canvasHeight: canvasBox.height,
                backingWidth: canvas.width,
                backingHeight: canvas.height
            };
        }
        """;

    private readonly PlaywrightAppFixture fixture;

    public CrmHrWorkforceJourneyTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Workforce_profile_skills_capacity_and_history_are_saved_through_the_record_dialog_and_survive_a_reload()
    {
        var artifactsDir = CrmHrWorkspaceJourneySupport.ArtifactsDirectory("crm-hr-workforce-journey");
        var suffix = CrmHrWorkspaceJourneySupport.NewSuffix();
        await using var owner = await CrmHrWorkspaceJourneySupport.BuildOwnerProviderAsync(fixture, "playwright-workforce-journey");
        var seed = await SeedAsync(owner, suffix);

        await CrmHrWorkspaceJourneySupport.RunAsync(fixture, artifactsDir, async (page, oracle) =>
        {
            // Open the seeded person from the real catalog: search by the unique name, one click on the one card.
            await CrmHrWorkspaceJourneySupport.NavigateAsync(fixture, page, oracle, "/crm-hr/workforce");
            var card = await CrmHrWorkspaceJourneySupport.FindSingleCardAsync(page, "crmhr-workforce", "crmhr-workforce-item", seed.WorkerName);
            await card.ClickAsync();
            await CrmHrWorkspaceJourneySupport.ExpectUrlAsync(page, $"partyId={seed.WorkerId:D}");
            var dialog = page.GetByTestId("crmhr-workforce-record-dialog");
            await Assertions.Expect(dialog).ToBeVisibleAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(dialog.Locator(".cda-dialog__title")).ToHaveTextAsync(seed.WorkerName);
            await Assertions.Expect(page.GetByTestId("crmhr-workforce-classification")).ToHaveTextAsync("External contact");
            await Assertions.Expect(page.GetByTestId("crmhr-workforce-no-staffable-profile")).ToBeVisibleAsync();
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "01-record-without-profile-1600.png") });

            // Create the staffable profile through the Profile form.
            await page.GetByTestId("crmhr-workforce-create-profile").ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-workforce-job-title")).ToBeVisibleAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await page.GetByTestId("crmhr-workforce-kind").SelectOptionAsync(new[] { WorkforceKind.Contractor.ToString() });
            await page.GetByTestId("crmhr-workforce-status").SelectOptionAsync(new[] { "Active" });
            await page.GetByTestId("crmhr-workforce-job-title").FillAsync(seed.JobTitle);
            await page.GetByTestId("crmhr-workforce-discipline").FillAsync("Delivery Engineering");
            await page.GetByTestId("crmhr-workforce-seniority").SelectOptionAsync(new[] { "Senior" });
            await page.GetByTestId("crmhr-workforce-capacity").FillAsync("32.5");
            await page.GetByTestId("crmhr-workforce-internal-rate").FillAsync("61.25");
            await page.GetByTestId("crmhr-workforce-external-rate").FillAsync("118.75");
            await page.GetByTestId("crmhr-workforce-rate-unit").SelectOptionAsync(new[] { ProjectResourceRateUnit.ManDay.ToString() });
            await page.GetByTestId("crmhr-workforce-rate-currency").FillAsync("EUR");
            await page.GetByTestId("crmhr-workforce-rate-currency").PressAsync("Tab");
            await page.GetByTestId("crmhr-workforce-save-button").ClickAsync();
            await CrmHrWorkspaceJourneySupport.ExpectToastAsync(page, "Workforce profile saved.");
            await Assertions.Expect(page.GetByTestId("crmhr-workforce-classification")).ToHaveTextAsync("Contractor");

            var created = await ReadProfileAsync(owner, seed.WorkerId);
            Assert.NotNull(created.Id);
            Assert.Equal(seed.WorkerId, created.PartyId);
            Assert.Equal(WorkforceKind.Contractor, created.WorkforceKind);
            Assert.Equal("Active", created.Status);
            Assert.Equal(seed.JobTitle, created.JobTitle);
            Assert.Equal("Delivery Engineering", created.Discipline);
            Assert.Equal("Senior", created.Seniority);
            Assert.Equal(32.5m, created.CapacityHoursPerWeek);
            Assert.Equal(61.25m, created.InternalCostRate);
            Assert.Equal(118.75m, created.ExternalBillingRate);
            Assert.Equal(ProjectResourceRateUnit.ManDay, created.RateUnit);
            Assert.Equal("EUR", created.RateCurrencyCode);
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "02-profile-created-1600.png") });

            // Edit the saved profile: only the edited fields change at the owner, the identity stays the same.
            await CrmHrWorkspaceJourneySupport.ExpectToastGoneAsync(page, "Workforce profile saved.");
            await page.GetByTestId("crmhr-workforce-job-title").FillAsync(seed.EditedJobTitle);
            await page.GetByTestId("crmhr-workforce-external-rate").FillAsync("131.4");
            await page.GetByTestId("crmhr-workforce-rate-unit").SelectOptionAsync(new[] { ProjectResourceRateUnit.Hour.ToString() });
            await page.GetByTestId("crmhr-workforce-external-rate").PressAsync("Tab");
            await page.GetByTestId("crmhr-workforce-save-button").ClickAsync();
            await CrmHrWorkspaceJourneySupport.ExpectToastAsync(page, "Workforce profile saved.");

            var edited = await ReadProfileAsync(owner, seed.WorkerId);
            Assert.Equal(created.Id, edited.Id);
            Assert.Equal(seed.EditedJobTitle, edited.JobTitle);
            Assert.Equal(131.4m, edited.ExternalBillingRate);
            Assert.Equal(ProjectResourceRateUnit.Hour, edited.RateUnit);
            Assert.Equal(61.25m, edited.InternalCostRate);
            Assert.Equal(32.5m, edited.CapacityHoursPerWeek);
            Assert.Equal("EUR", edited.RateCurrencyCode);
            Assert.Equal("Delivery Engineering", edited.Discipline);

            // Constrained width with the Profile form open.
            await CrmHrWorkspaceJourneySupport.AssertDialogFitsConstrainedWidthAsync(
                page,
                "crmhr-workforce-record-dialog",
                ["crmhr-workforce-save-button", "crmhr-workforce-record-close"],
                Path.Combine(artifactsDir, "03-profile-form-1100.png"));

            // Skills: a new definition in the dictionary, then the party skill with proficiency and years.
            await page.GetByTestId("crmhr-workforce-tab-skills").ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-skill-definition-name")).ToBeVisibleAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await page.GetByTestId("crmhr-skill-definition-name").FillAsync(seed.SkillName);
            await page.GetByTestId("crmhr-skill-definition-category").FillAsync("Delivery");
            await page.GetByTestId("crmhr-skill-definition-description").FillAsync("Synthetic skill of the workforce journey.");
            await page.GetByTestId("crmhr-skill-definition-description").PressAsync("Tab");
            await page.GetByTestId("crmhr-skill-definition-save-button").ClickAsync();
            await CrmHrWorkspaceJourneySupport.ExpectToastAsync(page, "Skill definition saved.");

            var definition = Assert.Single((await ListSkillCatalogAsync(owner)).Where(item => string.Equals(item.Name, seed.SkillName, StringComparison.Ordinal)));
            Assert.Equal("Delivery", definition.Category);
            Assert.True(definition.IsActive);

            var skillSelect = page.GetByTestId("crmhr-skill-skill-id");
            await Assertions.Expect(skillSelect.Locator($"option[value='{definition.Id:D}']")).ToHaveTextAsync($"{seed.SkillName} (Delivery)");
            await skillSelect.SelectOptionAsync(new[] { definition.Id.ToString("D") });
            await page.GetByTestId("crmhr-skill-proficiency").SelectOptionAsync(new[] { SkillProficiencyLevel.Strong.ToString() });
            await page.GetByTestId("crmhr-skill-years").FillAsync("7");
            await page.GetByTestId("crmhr-skill-certification").FillAsync(seed.Certification);
            await page.GetByTestId("crmhr-skill-certification").PressAsync("Tab");
            await page.GetByTestId("crmhr-skill-save-button").ClickAsync();
            await CrmHrWorkspaceJourneySupport.ExpectToastAsync(page, "Skill saved.");
            var skillItem = page.GetByTestId("crmhr-skill-item");
            await Assertions.Expect(skillItem).ToHaveCountAsync(1);
            await Assertions.Expect(skillItem).ToContainTextAsync(seed.SkillName);
            await Assertions.Expect(skillItem).ToContainTextAsync("7y experience");
            await Assertions.Expect(skillItem).ToContainTextAsync(seed.Certification);
            await Assertions.Expect(skillItem).ToContainTextAsync("Strong");

            var savedSkill = Assert.Single((await ReadWorkspaceAsync(owner, seed.WorkerId)).Skills);
            Assert.Equal(definition.Id, savedSkill.SkillId);
            Assert.Equal(seed.SkillName, savedSkill.SkillName);
            Assert.Equal(SkillProficiencyLevel.Strong, savedSkill.Proficiency);
            Assert.Equal(7, savedSkill.YearsExperience);
            Assert.Equal(seed.Certification, savedSkill.CertificationStatus);
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "04-skill-saved-1600.png") });

            // Allocations: the seeded project commitments are plotted; the availability copy is the owner's summary.
            await page.GetByTestId("crmhr-workforce-tab-allocations").ClickAsync();
            var availability = page.GetByTestId("crmhr-capacity-availability-message");
            await Assertions.Expect(availability).ToHaveTextAsync("Allocated at 30% with 0% blocked.", new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(page.GetByTestId("crmhr-workforce-allocation-item")).ToHaveCountAsync(2);
            await AssertAllocationScheduleAsync(page, seed);
            await page.GetByTestId("crmhr-workforce-allocation-gantt").ScrollIntoViewIfNeededAsync();
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "05-allocation-schedule-1600.png") });

            // A leave block that covers today: one click, then the list, the availability copy and the owner agree.
            await page.GetByTestId("crmhr-capacity-block-kind").SelectOptionAsync(new[] { CapacityBlockKind.Leave.ToString() });
            await page.GetByTestId("crmhr-capacity-block-start-date").FillAsync(Iso(seed.BlockStart));
            await page.GetByTestId("crmhr-capacity-block-end-date").FillAsync(Iso(seed.BlockEnd));
            await page.GetByTestId("crmhr-capacity-block-percentage").FillAsync("40");
            await page.GetByTestId("crmhr-capacity-block-notes").FillAsync(seed.BlockNotes);
            await page.GetByTestId("crmhr-capacity-block-notes").PressAsync("Tab");
            await page.GetByTestId("crmhr-capacity-block-save-button").ClickAsync();
            await CrmHrWorkspaceJourneySupport.ExpectToastAsync(page, "Capacity block saved.");
            var blockItem = page.GetByTestId("crmhr-capacity-block-item");
            await Assertions.Expect(blockItem).ToHaveCountAsync(1);
            await Assertions.Expect(blockItem).ToContainTextAsync("Leave");
            await Assertions.Expect(blockItem).ToContainTextAsync($"{Iso(seed.BlockStart)} -> {Iso(seed.BlockEnd)}");
            await Assertions.Expect(blockItem).ToContainTextAsync("40%");
            await Assertions.Expect(blockItem).ToContainTextAsync(seed.BlockNotes);
            await Assertions.Expect(blockItem).ToContainTextAsync("Active");
            await Assertions.Expect(availability).ToHaveTextAsync("Allocated at 30% with 40% blocked.");

            var savedBlock = Assert.Single((await ReadCapacityAsync(owner, seed.WorkerId)).CapacityBlocks);
            Assert.Equal(CapacityBlockKind.Leave, savedBlock.BlockKind);
            Assert.Equal(seed.BlockStart, savedBlock.StartDate);
            Assert.Equal(seed.BlockEnd, savedBlock.EndDate);
            Assert.Equal(40m, savedBlock.Percentage);
            Assert.Equal(seed.BlockNotes, savedBlock.Notes);
            Assert.True(savedBlock.IsActive);
            await AssertAllocationScheduleAsync(page, seed);
            await blockItem.ScrollIntoViewIfNeededAsync();
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "06-capacity-block-saved-1600.png") });

            // Delete the block: it disappears from the list, from the availability copy and from the owner.
            await blockItem.GetByTestId("crmhr-capacity-block-delete-button").ClickAsync();
            await CrmHrWorkspaceJourneySupport.ExpectToastAsync(page, "Capacity block removed.");
            await Assertions.Expect(blockItem).ToHaveCountAsync(0);
            await Assertions.Expect(availability).ToHaveTextAsync("Allocated at 30% with 0% blocked.");
            Assert.Empty((await ReadCapacityAsync(owner, seed.WorkerId)).CapacityBlocks);

            // History: the independent lane shows what it accepted for this party: the party creation of the seed and
            // the two profile saves of this journey, without follow-ups.
            await page.GetByTestId("crmhr-workforce-tab-history").ClickAsync();
            await AssertHistoryAsync(page, seed);
            var ownerHistory = await ReadHistoryAsync(owner, seed.WorkerId);
            Assert.Equal(3, ownerHistory.TotalCount);
            Assert.Equal(0, ownerHistory.ActionCount);
            Assert.Equal(2, ownerHistory.Items.Count(item => string.Equals(item.Title, seed.ProfileSavedTitle, StringComparison.Ordinal) || string.Equals(item.Description, seed.ProfileSavedTitle, StringComparison.Ordinal)));
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "07-history-1600.png") });

            // Reload the deep link: a fresh document and circuit show the persisted record.
            await CrmHrWorkspaceJourneySupport.NavigateAsync(fixture, page, oracle, $"/crm-hr/workforce?partyId={seed.WorkerId:D}");
            await CrmHrWorkspaceJourneySupport.WaitForCatalogAsync(page, "crmhr-workforce");
            await Assertions.Expect(dialog).ToBeVisibleAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(dialog.Locator(".cda-dialog__title")).ToHaveTextAsync(seed.WorkerName);
            await Assertions.Expect(page.GetByTestId("crmhr-workforce-classification")).ToHaveTextAsync("Contractor");
            await Assertions.Expect(page.GetByTestId("crmhr-workforce-no-staffable-profile")).ToHaveCountAsync(0);
            await page.GetByTestId("crmhr-workforce-tab-profile").ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-workforce-job-title")).ToHaveValueAsync(seed.EditedJobTitle, new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(page.GetByTestId("crmhr-workforce-discipline")).ToHaveValueAsync("Delivery Engineering");
            await Assertions.Expect(page.GetByTestId("crmhr-workforce-kind")).ToHaveValueAsync(WorkforceKind.Contractor.ToString());
            await Assertions.Expect(page.GetByTestId("crmhr-workforce-seniority")).ToHaveValueAsync("Senior");
            await Assertions.Expect(page.GetByTestId("crmhr-workforce-rate-unit")).ToHaveValueAsync(ProjectResourceRateUnit.Hour.ToString());
            await Assertions.Expect(page.GetByTestId("crmhr-workforce-rate-currency")).ToHaveValueAsync("EUR");
            Assert.Equal(32.5m, await ReadDecimalAsync(page, "crmhr-workforce-capacity"));
            Assert.Equal(61.25m, await ReadDecimalAsync(page, "crmhr-workforce-internal-rate"));
            Assert.Equal(131.4m, await ReadDecimalAsync(page, "crmhr-workforce-external-rate"));
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "08-reloaded-profile-1600.png") });

            await page.GetByTestId("crmhr-workforce-tab-skills").ClickAsync();
            await Assertions.Expect(skillItem).ToHaveCountAsync(1, new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(skillItem).ToContainTextAsync(seed.SkillName);
            await Assertions.Expect(skillItem).ToContainTextAsync("7y experience");

            await page.GetByTestId("crmhr-workforce-tab-allocations").ClickAsync();
            await Assertions.Expect(availability).ToHaveTextAsync("Allocated at 30% with 0% blocked.", new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(blockItem).ToHaveCountAsync(0);
            await AssertAllocationScheduleAsync(page, seed);

            await page.GetByTestId("crmhr-workforce-tab-history").ClickAsync();
            await AssertHistoryAsync(page, seed);
        });
    }

    // The accepted history of the seeded party: three activities (party created, profile saved twice), no follow-ups.
    private static async Task AssertHistoryAsync(IPage page, SeededWorkforce seed)
    {
        var history = page.GetByTestId("crmhr-workforce-history");
        await Assertions.Expect(history).ToHaveAttributeAsync("data-accepted", "true", new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
        await Assertions.Expect(history).ToHaveAttributeAsync("data-loading", "false");
        var totals = page.GetByTestId("crmhr-workforce-history-totals");
        await Assertions.Expect(totals).ToContainTextAsync("3 activities");
        await Assertions.Expect(totals).ToContainTextAsync("0 next actions");
        await Assertions.Expect(page.GetByTestId("crmhr-workforce-history-overdue-total")).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByTestId("crmhr-workforce-history-item")).ToHaveCountAsync(3);
        await Assertions.Expect(page.GetByTestId("crmhr-workforce-history-item").Filter(new() { HasText = seed.ProfileSavedTitle })).ToHaveCountAsync(2);
        await Assertions.Expect(page.GetByTestId("crmhr-workforce-history-error")).ToHaveCountAsync(0);
    }

    // The plotted allocation schedule against the seeded commitments: one aligned table row and one plotted bar per
    // allocation, real geometry, bar lengths in the proportion of the seeded durations, and the future commitment
    // plotted to the right of the active one.
    private static async Task AssertAllocationScheduleAsync(IPage page, SeededWorkforce seed)
    {
        await page.WaitForFunctionAsync(
            $"() => {{ const probe = ({GanttProbe})(); return probe.present && probe.rows.length === 2 && probe.bars.every(bar => bar !== null && bar.length >= 8); }}",
            null,
            new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
        var probe = await page.EvaluateAsync<JsonElement>(GanttProbe);
        var rows = probe.GetProperty("rows").EnumerateArray().ToArray();
        Assert.Equal(2, rows.Length);
        Assert.Equal($"{seed.ProjectName} · TeamMember · 30%", rows[0].GetProperty("title").GetString());
        Assert.Equal($"{seed.ProjectName} · Reviewer · 20%", rows[1].GetProperty("title").GetString());
        Assert.All(rows, row =>
        {
            Assert.True(row.GetProperty("height").GetDouble() >= 40, "A schedule row has no height.");
            Assert.True(row.GetProperty("width").GetDouble() >= 200, "A schedule row has no width.");
        });
        Assert.True(probe.GetProperty("canvasHeight").GetDouble() >= 130, "The schedule canvas is not tall enough for two rows.");
        Assert.True(probe.GetProperty("canvasWidth").GetDouble() >= 400, "The schedule canvas has no width.");
        Assert.True(probe.GetProperty("backingWidth").GetInt32() > 1, "The schedule canvas was never sized for drawing.");

        var bars = probe.GetProperty("bars").EnumerateArray()
            .Select(bar => (
                Start: bar.GetProperty("start").GetDouble(),
                Length: bar.GetProperty("length").GetDouble(),
                Pixels: bar.GetProperty("pixels").GetDouble(),
                Colour: bar.GetProperty("colour").GetString()))
            .ToArray();
        Assert.Equal(bars[0].Colour, bars[1].Colour);
        Assert.All(bars, bar => Assert.True(
            bar.Pixels / bar.Length > 0.9,
            $"Only {bar.Pixels:F0}px of the {bar.Length:F0}px span are plotted in the bar colour; the span is not one filled bar."));
        var expectedRatio = (double)seed.ActiveAllocationDays / seed.FutureAllocationDays;
        var actualRatio = bars[0].Length / bars[1].Length;
        Assert.True(
            Math.Abs(actualRatio - expectedRatio) / expectedRatio < 0.05,
            $"The plotted bars are {bars[0].Length:F1}px and {bars[1].Length:F1}px (ratio {actualRatio:F3}); the seeded durations are {seed.ActiveAllocationDays} and {seed.FutureAllocationDays} days (ratio {expectedRatio:F3}).");
        Assert.True(
            bars[1].Start > bars[0].Start + bars[0].Length,
            $"The future commitment starts at {bars[1].Start:F1}px, not after the active one ending at {bars[0].Start + bars[0].Length:F1}px.");
    }

    private static async Task<decimal> ReadDecimalAsync(IPage page, string testId)
        => decimal.Parse(await page.GetByTestId(testId).InputValueAsync(), NumberStyles.Number, CultureInfo.InvariantCulture);

    private static string Iso(DateOnly value)
        => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static async Task<WorkforceProfileWorkspaceModel> ReadWorkspaceAsync(ServiceProvider owner, Guid partyId)
    {
        await using var scope = owner.CreateAsyncScope();
        var workspace = await scope.ServiceProvider.GetRequiredService<HrService>().GetWorkforceProfileWorkspaceAsync(partyId);
        Assert.NotNull(workspace);
        Assert.Equal(partyId, workspace!.PartyId);
        return workspace;
    }

    private static async Task<WorkforceProfileEditorModel> ReadProfileAsync(ServiceProvider owner, Guid partyId)
        => (await ReadWorkspaceAsync(owner, partyId)).Profile;

    private static async Task<WorkforceCapacityWorkspaceModel> ReadCapacityAsync(ServiceProvider owner, Guid partyId)
    {
        await using var scope = owner.CreateAsyncScope();
        var workspace = await scope.ServiceProvider.GetRequiredService<HrService>().GetWorkforceCapacityWorkspaceAsync(partyId);
        Assert.NotNull(workspace);
        Assert.Equal(partyId, workspace!.PartyId);
        return workspace;
    }

    private static async Task<IReadOnlyList<SkillCatalogItemModel>> ListSkillCatalogAsync(ServiceProvider owner)
    {
        await using var scope = owner.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<HrService>().ListSkillCatalogAsync();
    }

    private static async Task<CrmActivityHistoryPage> ReadHistoryAsync(ServiceProvider owner, Guid partyId)
    {
        await using var scope = owner.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PartyDirectoryService>().SearchPartyActivityAsync(new CrmActivityHistoryQuery(partyId));
    }

    private static async Task<SeededWorkforce> SeedAsync(ServiceProvider owner, string suffix)
    {
        await using var scope = owner.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var projectPartyBridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();

        var workerName = $"Journey Worker {suffix}";
        var projectName = $"Journey Delivery {suffix}";
        var workerId = await CrmHrWorkspaceJourneySupport.CreatePartyAsync(
            partyDirectoryService,
            workerName,
            PartyType.Person,
            PartyRoleKind.Stakeholder,
            $"journey.worker.{suffix}@example.test");

        var project = await projectsService.CreateWithAdmissionAsync(new ProjectEditorModel
        {
            Name = projectName,
            Description = $"{projectName} description",
            Objective = $"{projectName} objective",
            CurrentPhase = "Delivery"
        });
        Assert.True(project.IsSuccess, string.Join(" ", project.Errors.Select(error => error.Message)));
        var admission = Assert.IsType<ProjectWriteAdmission>(project.Value);

        // Two commitments of the same person: an active one and a later one half as long, so the plotted bars have a
        // known proportion and order. Neither ends within 30 days, which keeps the availability copy deterministic.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        const int activeDays = 46;
        const int futureDays = 23;
        await SaveAllocationAsync(projectPartyBridge, admission, workerId, ProjectPartyAssignmentRole.TeamMember, "journey-delivery", 30m, today.AddDays(-5), today.AddDays(-5 + activeDays - 1));
        await SaveAllocationAsync(projectPartyBridge, admission, workerId, ProjectPartyAssignmentRole.Reviewer, "journey-review", 20m, today.AddDays(50), today.AddDays(50 + futureDays - 1));

        return new SeededWorkforce(
            workerId,
            workerName,
            projectName,
            $"Integration Engineer {suffix}",
            $"Lead Integration Engineer {suffix}",
            $"Journey Skill {suffix}",
            $"Certified {suffix}",
            today.AddDays(-2),
            today.AddDays(60),
            $"Parental leave {suffix}",
            $"Saved workforce profile for '{workerName}'.",
            activeDays,
            futureDays);
    }

    private static async Task SaveAllocationAsync(
        IProjectPartyIntegrationBridge projectPartyBridge,
        ProjectWriteAdmission admission,
        Guid partyId,
        ProjectPartyAssignmentRole role,
        string nodeKey,
        decimal allocationPercent,
        DateOnly startsOn,
        DateOnly endsOn)
    {
        var result = await projectPartyBridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = admission.ProjectId,
            ExpectedProjectAdmission = admission,
            PartyId = partyId,
            Role = role,
            NodeKey = nodeKey,
            AllocationPercent = allocationPercent,
            StartsOn = startsOn,
            EndsOn = endsOn,
            Notes = $"Journey allocation for {role}",
            IsPrimary = false,
            Source = "playwright-tests"
        });

        Assert.True(result.IsSuccess, string.Join(" ", result.Errors.Select(error => error.Message)));
    }

    private sealed record SeededWorkforce(
        Guid WorkerId,
        string WorkerName,
        string ProjectName,
        string JobTitle,
        string EditedJobTitle,
        string SkillName,
        string Certification,
        DateOnly BlockStart,
        DateOnly BlockEnd,
        string BlockNotes,
        string ProfileSavedTitle,
        int ActiveAllocationDays,
        int FutureAllocationDays);
}
