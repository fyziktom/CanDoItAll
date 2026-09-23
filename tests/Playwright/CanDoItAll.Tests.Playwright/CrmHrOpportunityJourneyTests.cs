using System.Globalization;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

// Opportunities of a seeded CRM account through the real Web host and PostgreSQL: the three-step create dialog with
// the owner chosen in the real party picker, the detail and edit dialogs, the board's advance action with its stage
// history, a rejected edit, the conversion of the won opportunity into a new project and a second conversion that
// relinks the preselected project. After a reload the detail opens above the account record from a board card and
// from the opportunity deep link. Every write is read back through CrmService and the Projects query owner by exact
// identifiers.
[Collection(PlaywrightCollection.Name)]
public sealed class CrmHrOpportunityJourneyTests
{
    private readonly PlaywrightAppFixture fixture;

    public CrmHrOpportunityJourneyTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Opportunity_is_created_edited_advanced_and_converted_into_exactly_one_project()
    {
        var artifactsDir = CrmHrWorkspaceJourneySupport.ArtifactsDirectory("crm-hr-opportunity-journey");
        var suffix = CrmHrWorkspaceJourneySupport.NewSuffix();
        await using var owner = await CrmHrWorkspaceJourneySupport.BuildOwnerProviderAsync(fixture, "playwright-opportunity-journey");
        var seed = await SeedAsync(owner, suffix);

        await CrmHrWorkspaceJourneySupport.RunAsync(fixture, artifactsDir, async (page, oracle) =>
        {
            await CrmHrWorkspaceJourneySupport.NavigateAsync(fixture, page, oracle, $"/crm-hr/crm?accountId={seed.AccountId:D}");
            var accountDialog = page.GetByTestId("crmhr-crm-record-dialog");
            await Assertions.Expect(accountDialog).ToBeVisibleAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            var summary = page.GetByTestId("crmhr-account-summary");
            await Assertions.Expect(summary).ToHaveAttributeAsync("data-account-id", seed.AccountId.ToString("D"));
            await Assertions.Expect(summary).ToHaveAttributeAsync("data-interactive", "true", new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await page.GetByTestId("crmhr-crm-tab-opportunities").ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-opportunity-empty")).ToBeVisibleAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });

            // Create: basics, owner through the real picker, commercial context, one click on the final button.
            await page.GetByTestId("crmhr-opportunity-add-button").ClickAsync();
            var createDialog = page.GetByTestId("crmhr-opportunity-create-dialog");
            await Assertions.Expect(createDialog).ToBeVisibleAsync();
            await createDialog.GetByTestId("crmhr-opportunity-title").FillAsync(seed.Title);
            await createDialog.GetByTestId("crmhr-opportunity-stage").SelectOptionAsync(new[] { OpportunityStage.Qualified.ToString() });
            await createDialog.GetByTestId("crmhr-opportunity-currency").FillAsync("EUR");
            await createDialog.GetByTestId("crmhr-opportunity-amount").FillAsync("48500.5");
            await createDialog.GetByTestId("crmhr-opportunity-close-date").FillAsync(Iso(seed.ExpectedCloseOn));
            await createDialog.GetByTestId("crmhr-opportunity-close-date").PressAsync("Tab");
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "01-create-basics-1600.png") });
            await createDialog.GetByTestId("crmhr-opportunity-create-next").ClickAsync();
            await Assertions.Expect(createDialog.GetByTestId("crmhr-opportunity-owner")).ToBeVisibleAsync();
            await createDialog.GetByTestId("crmhr-opportunity-owner").ClickAsync();
            await CrmHrWorkspaceJourneySupport.PickPartyAsync(page, "crmhr-opportunity-owner-picker", seed.OwnerName, seed.OwnerId);
            await Assertions.Expect(createDialog).ToContainTextAsync("Owner selected");
            await createDialog.GetByTestId("crmhr-opportunity-create-next").ClickAsync();
            await Assertions.Expect(createDialog.GetByTestId("crmhr-opportunity-summary")).ToBeVisibleAsync();
            await createDialog.GetByTestId("crmhr-opportunity-summary").FillAsync(seed.Summary);
            await createDialog.GetByTestId("crmhr-opportunity-summary").PressAsync("Tab");
            await createDialog.GetByTestId("crmhr-opportunity-save-button").ClickAsync();

            var detailDialog = page.GetByTestId("crmhr-opportunity-detail-dialog");
            await Assertions.Expect(detailDialog).ToBeVisibleAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(createDialog).ToHaveCountAsync(0);
            await Assertions.Expect(detailDialog.Locator(".cda-dialog__title")).ToHaveTextAsync(seed.Title);

            // The owner knows exactly one opportunity with this title on this account.
            var created = await ReadSingleByTitleAsync(owner, seed.AccountId, seed.Title);
            Assert.Equal(seed.AccountId, created.AccountPartyId);
            Assert.Equal(OpportunityStage.Qualified, created.Stage);
            Assert.Equal(48500.5m, created.Amount);
            Assert.Equal("EUR", created.CurrencyCode);
            Assert.Equal(seed.OwnerId, created.OwnerPartyId);
            Assert.Equal(seed.ExpectedCloseOn, created.ExpectedCloseOn);
            Assert.Equal(seed.Summary, created.Summary);
            Assert.Null(created.LinkedProjectId);
            Assert.Equal(new[] { OpportunityStage.Qualified }, created.StageHistory.Select(item => item.Stage).ToArray());
            var opportunityId = created.Id;
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "02-created-detail-1600.png") });

            // The detail of the created opportunity is open above the account record: edit amount and stage through
            // the edit dialog.
            await Assertions.Expect(detailDialog).ToContainTextAsync(seed.OwnerName);
            await detailDialog.GetByTestId("crmhr-opportunity-edit-button").ClickAsync();
            var editDialog = page.GetByTestId("crmhr-opportunity-edit-dialog");
            await Assertions.Expect(editDialog).ToBeVisibleAsync();
            await Assertions.Expect(editDialog.GetByTestId("crmhr-opportunity-title")).ToHaveValueAsync(seed.Title);
            await editDialog.GetByTestId("crmhr-opportunity-amount").FillAsync("52000.75");
            await editDialog.GetByTestId("crmhr-opportunity-stage").SelectOptionAsync(new[] { OpportunityStage.Proposal.ToString() });
            await editDialog.GetByTestId("crmhr-opportunity-amount").PressAsync("Tab");
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "03-edit-dialog-1600.png") });
            await editDialog.GetByTestId("crmhr-opportunity-save-button").ClickAsync();
            await Assertions.Expect(editDialog).ToHaveCountAsync(0, new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(detailDialog).ToBeVisibleAsync();
            await Assertions.Expect(detailDialog).ToContainTextAsync(OpportunityStage.Proposal.ToString());

            var edited = await ReadAsync(owner, opportunityId);
            Assert.Equal(52000.75m, edited.Amount);
            Assert.Equal(OpportunityStage.Proposal, edited.Stage);
            Assert.Equal("EUR", edited.CurrencyCode);
            Assert.Equal(seed.Title, edited.Title);
            Assert.Equal(seed.OwnerId, edited.OwnerPartyId);
            Assert.Equal(
                new[] { OpportunityStage.Proposal, OpportunityStage.Qualified },
                edited.StageHistory.OrderByDescending(item => item.ChangedAtUtc).Select(item => item.Stage).ToArray());

            // Advance one stage with the board's action.
            await detailDialog.GetByTestId("crmhr-opportunity-detail-close").ClickAsync();
            await Assertions.Expect(detailDialog).ToHaveCountAsync(0);
            var advance = page.GetByTestId($"crmhr-opportunity-advance-{opportunityId:N}");
            await Assertions.Expect(advance).ToContainTextAsync("Advance to Negotiation", new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await advance.ClickAsync();
            await Assertions.Expect(detailDialog).ToBeVisibleAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(detailDialog).ToContainTextAsync("Advanced from Proposal to Negotiation.");
            var advanced = await ReadAsync(owner, opportunityId);
            Assert.Equal(OpportunityStage.Negotiation, advanced.Stage);
            var negotiationEntry = Assert.Single(advanced.StageHistory.Where(item => item.Stage == OpportunityStage.Negotiation));
            Assert.Equal("Advanced from Proposal to Negotiation.", negotiationEntry.Notes);
            Assert.Equal(3, advanced.StageHistory.Count);
            Assert.Equal(52000.75m, advanced.Amount);
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "04-advanced-1600.png") });

            // Invalid input: an edit without a title is rejected and changes nothing at the owner.
            await detailDialog.GetByTestId("crmhr-opportunity-edit-button").ClickAsync();
            await Assertions.Expect(editDialog).ToBeVisibleAsync();
            await editDialog.GetByTestId("crmhr-opportunity-title").FillAsync(string.Empty);
            await editDialog.GetByTestId("crmhr-opportunity-amount").FillAsync("1");
            await editDialog.GetByTestId("crmhr-opportunity-amount").PressAsync("Tab");
            await editDialog.GetByTestId("crmhr-opportunity-save-button").ClickAsync();
            await Assertions.Expect(editDialog.GetByTestId("crmhr-opportunity-edit-message")).ToHaveTextAsync("Opportunity title is required.", new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(editDialog).ToBeVisibleAsync();
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "05-rejected-edit-1600.png") });
            var afterRejection = await ReadAsync(owner, opportunityId);
            Assert.Equal(seed.Title, afterRejection.Title);
            Assert.Equal(52000.75m, afterRejection.Amount);
            Assert.Equal(advanced.UpdatedAtUtc, afterRejection.UpdatedAtUtc);
            Assert.Equal(3, afterRejection.StageHistory.Count);
            await editDialog.GetByTestId("crmhr-opportunity-edit-cancel").ClickAsync();
            await Assertions.Expect(editDialog).ToHaveCountAsync(0);
            await Assertions.Expect(detailDialog).ToBeVisibleAsync();
            await Assertions.Expect(detailDialog.Locator(".cda-dialog__title")).ToHaveTextAsync(seed.Title);

            // Win it with the board's action, then convert it into a new project: one click on the final button.
            await detailDialog.GetByTestId("crmhr-opportunity-detail-close").ClickAsync();
            await Assertions.Expect(detailDialog).ToHaveCountAsync(0);
            await Assertions.Expect(advance).ToContainTextAsync("Advance to Won", new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await advance.ClickAsync();
            await Assertions.Expect(detailDialog).ToBeVisibleAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            var convert = detailDialog.GetByTestId("crmhr-opportunity-convert-button");
            await Assertions.Expect(convert).ToBeVisibleAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            Assert.Equal(OpportunityStage.Won, (await ReadAsync(owner, opportunityId)).Stage);

            await convert.ClickAsync();
            var conversionDialog = page.GetByTestId("crmhr-opportunity-conversion-dialog");
            await Assertions.Expect(conversionDialog).ToBeVisibleAsync();
            await Assertions.Expect(conversionDialog.GetByTestId("crmhr-opportunity-conversion-link-existing")).Not.ToBeCheckedAsync();
            await conversionDialog.GetByTestId("crmhr-opportunity-conversion-name").FillAsync(seed.ProjectName);
            await conversionDialog.GetByTestId("crmhr-opportunity-conversion-phase").FillAsync("Kickoff");
            await conversionDialog.GetByTestId("crmhr-opportunity-conversion-phase").PressAsync("Tab");
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "06-conversion-dialog-1600.png") });
            await conversionDialog.GetByTestId("crmhr-opportunity-conversion-save").ClickAsync();
            await Assertions.Expect(conversionDialog).ToHaveCountAsync(0, new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(detailDialog).ToBeVisibleAsync();
            await Assertions.Expect(detailDialog).ToContainTextAsync(seed.ProjectName);
            await Assertions.Expect(detailDialog.GetByTestId("crmhr-opportunity-open-project")).ToBeVisibleAsync();

            var convertedOpportunity = await ReadAsync(owner, opportunityId);
            var project = Assert.Single(await SearchProjectsAsync(owner, seed.ProjectName));
            Assert.Equal(seed.ProjectName, project.Name);
            Assert.Equal("Kickoff", project.CurrentPhase);
            Assert.Equal(project.Id, convertedOpportunity.LinkedProjectId);
            Assert.Equal(seed.ProjectName, convertedOpportunity.LinkedProjectName);
            Assert.Equal(OpportunityStage.Won, convertedOpportunity.Stage);

            // Constrained width with the converted opportunity's detail open.
            await CrmHrWorkspaceJourneySupport.AssertDialogFitsConstrainedWidthAsync(
                page,
                "crmhr-opportunity-detail-dialog",
                ["crmhr-opportunity-edit-button", "crmhr-opportunity-open-project", "crmhr-opportunity-detail-close"],
                Path.Combine(artifactsDir, "07-converted-detail-1100.png"));

            // A second conversion attempt creates nothing new. The dialog preselects the linked project with the
            // admission captured by the load that showed it; saving the untouched preselection relinks that project.
            // (At 4a5724977 this save ended in an unhandled InvalidOperationException and broke the circuit.)
            await convert.ClickAsync();
            await Assertions.Expect(conversionDialog).ToBeVisibleAsync();
            await Assertions.Expect(conversionDialog.GetByTestId("crmhr-opportunity-conversion-link-existing")).ToBeCheckedAsync();
            await Assertions.Expect(conversionDialog).ToContainTextAsync(seed.ProjectName);
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "08-second-conversion-dialog-1600.png") });
            await conversionDialog.GetByTestId("crmhr-opportunity-conversion-save").ClickAsync();
            await Assertions.Expect(conversionDialog).ToHaveCountAsync(0, new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await CrmHrWorkspaceJourneySupport.ExpectToastAsync(page, "Opportunity linked to the selected project.");
            await Assertions.Expect(detailDialog).ToContainTextAsync(seed.ProjectName);
            var projectsAfterSecondAttempt = await SearchProjectsAsync(owner, seed.ProjectName);
            Assert.Equal(project.Id, Assert.Single(projectsAfterSecondAttempt).Id);
            Assert.Equal(project.Id, (await ReadAsync(owner, opportunityId)).LinkedProjectId);

            // Reload the account's deep link: a fresh document and circuit show the persisted opportunity on the board.
            await CrmHrWorkspaceJourneySupport.NavigateAsync(fixture, page, oracle, $"/crm-hr/crm?accountId={seed.AccountId:D}");
            await Assertions.Expect(accountDialog).ToBeVisibleAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(summary).ToHaveAttributeAsync("data-interactive", "true", new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await page.GetByTestId("crmhr-crm-tab-opportunities").ClickAsync();
            var boardCard = page.GetByTestId($"crmhr-opportunity-card-{opportunityId:N}");
            await Assertions.Expect(boardCard).ToHaveAttributeAsync("aria-label", $"Open {seed.Title}", new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(boardCard).ToContainTextAsync(OpportunityStage.Won.ToString());
            await Assertions.Expect(boardCard).ToContainTextAsync(seed.OwnerName);
            await Assertions.Expect(boardCard).ToContainTextAsync($"Close {Iso(seed.ExpectedCloseOn)}");
            Assert.Contains("5200075", new string((await boardCard.InnerTextAsync()).Where(char.IsAsciiDigit).ToArray()), StringComparison.Ordinal);
            await Assertions.Expect(page.GetByTestId("crmhr-opportunity-column-won").GetByTestId($"crmhr-opportunity-card-{opportunityId:N}")).ToHaveCountAsync(1);
            await Assertions.Expect(page.GetByTestId($"crmhr-opportunity-advance-{opportunityId:N}")).ToHaveCountAsync(0);
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "09-reloaded-board-1600.png") });

            // A board card opens the detail above the account record, where its own controls take the click.
            await boardCard.ClickAsync();
            await Assertions.Expect(detailDialog).ToBeVisibleAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(detailDialog.Locator(".cda-dialog__title")).ToHaveTextAsync(seed.Title);
            await Assertions.Expect(accountDialog).ToBeVisibleAsync();
            Assert.Equal("crmhr-opportunity-detail-dialog", await TopDialogAtCenterAsync(detailDialog));
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "10-card-detail-above-account-1600.png") });
            await detailDialog.GetByTestId("crmhr-opportunity-detail-close").ClickAsync();
            await Assertions.Expect(detailDialog).ToHaveCountAsync(0);
            await Assertions.Expect(accountDialog).ToBeVisibleAsync();

            // The opportunity deep link opens the account record and the detail in one render; the detail is on top.
            await CrmHrWorkspaceJourneySupport.NavigateAsync(fixture, page, oracle, $"/crm-hr/crm?accountId={seed.AccountId:D}&opportunityId={opportunityId:D}");
            await Assertions.Expect(detailDialog).ToBeVisibleAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(detailDialog.Locator(".cda-dialog__title")).ToHaveTextAsync(seed.Title);
            await Assertions.Expect(accountDialog).ToBeVisibleAsync();
            await Assertions.Expect(summary).ToHaveAttributeAsync("data-interactive", "true", new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            Assert.Equal("crmhr-opportunity-detail-dialog", await TopDialogAtCenterAsync(detailDialog));
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "11-deep-link-detail-above-account-1600.png") });
            await detailDialog.GetByTestId("crmhr-opportunity-detail-close").ClickAsync();
            await Assertions.Expect(detailDialog).ToHaveCountAsync(0);
            await Assertions.Expect(accountDialog).ToBeVisibleAsync();
        });
    }

    // The test id of the dialog that receives a pointer at the center of the given dialog's surface.
    private static Task<string?> TopDialogAtCenterAsync(ILocator dialog)
        => dialog.EvaluateAsync<string?>(
            """
            root => {
                const box = root.querySelector('.cda-dialog').getBoundingClientRect();
                const hit = document.elementFromPoint(box.left + box.width / 2, box.top + box.height / 2);
                return hit?.closest('dialog')?.getAttribute('data-testid') ?? null;
            }
            """);

    private static string Iso(DateOnly value)
        => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static async Task<CrmOpportunityDetailModel> ReadAsync(ServiceProvider owner, Guid opportunityId)
    {
        await using var scope = owner.CreateAsyncScope();
        var opportunity = await scope.ServiceProvider.GetRequiredService<CrmService>().GetOpportunityAsync(opportunityId);
        Assert.NotNull(opportunity);
        Assert.Equal(opportunityId, opportunity!.Id);
        return opportunity;
    }

    private static async Task<CrmOpportunityDetailModel> ReadSingleByTitleAsync(ServiceProvider owner, Guid accountId, string title)
    {
        await using var scope = owner.CreateAsyncScope();
        var page = await scope.ServiceProvider.GetRequiredService<IOpportunityPipelineQueryService>()
            .SearchAsync(new OpportunityPipelineQuery(accountId, title));
        var item = Assert.Single(page.Items.Where(candidate => string.Equals(candidate.Title, title, StringComparison.Ordinal)));
        Assert.Equal(1, page.TotalCount);
        return await ReadAsync(owner, item.Id);
    }

    private static async Task<IReadOnlyList<ProjectRecordQueryItem>> SearchProjectsAsync(ServiceProvider owner, string marker)
    {
        await using var scope = owner.CreateAsyncScope();
        var page = await scope.ServiceProvider.GetRequiredService<IProjectRecordQueryService>()
            .SearchAsync(new ProjectRecordQuery(marker, ProjectRecordScope.All));
        return page.Items.Where(item => item.Name.Contains(marker, StringComparison.Ordinal)).ToArray();
    }

    private static async Task<SeededOpportunity> SeedAsync(ServiceProvider owner, string suffix)
    {
        await using var scope = owner.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var crmService = scope.ServiceProvider.GetRequiredService<CrmService>();

        var accountName = $"Pipeline Account {suffix}";
        var ownerName = $"Pipeline Owner {suffix}";
        var accountId = await CrmHrWorkspaceJourneySupport.CreatePartyAsync(partyDirectoryService, accountName, PartyType.Organization, PartyRoleKind.Customer, $"pipeline.account.{suffix}@example.test");
        var ownerId = await CrmHrWorkspaceJourneySupport.CreatePartyAsync(partyDirectoryService, ownerName, PartyType.Person, PartyRoleKind.AccountManager, $"pipeline.owner.{suffix}@example.test");
        var profile = await crmService.SaveAccountProfileAsync(new CrmAccountProfileEditorModel
        {
            AccountPartyId = accountId,
            RelationshipStage = CrmAccountRelationshipStage.ActiveCustomer,
            CommercialNotes = "Synthetic account of the opportunity journey.",
            LastChangedBy = "playwright-tests"
        });
        Assert.True(profile.IsSuccess, string.Join(" ", profile.Errors.Select(error => error.Message)));

        return new SeededOpportunity(
            accountId,
            accountName,
            ownerId,
            ownerName,
            $"Platform renewal {suffix}",
            $"Renewal of the platform subscription {suffix}",
            $"Renewal Delivery {suffix}",
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(45));
    }

    private sealed record SeededOpportunity(
        Guid AccountId,
        string AccountName,
        Guid OwnerId,
        string OwnerName,
        string Title,
        string Summary,
        string ProjectName,
        DateOnly ExpectedCloseOn);
}
