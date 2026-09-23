using CanDoItAll.Modules.CrmHr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

// What a save does to the rest of the record workspace, in the shipped UI on the real Web host and PostgreSQL.
//
// The read-back that follows a commit reconciles the committed editor with what the owner accepted and leaves every
// other editor alone: an interaction the operator started and has not saved is still there afterwards, word for word,
// and the profile form shows the owner's accepted values. Saving again updates the same profile record instead of
// creating a second one. The deterministic timing of a value typed while the write is still in flight is proven at the
// component level, where the owner call itself can be held open; nothing here places a fault in the shipped host.
[Collection(PlaywrightCollection.Name)]
public sealed class CrmHrSaveReconciliationJourneyTests
{
    private readonly PlaywrightAppFixture fixture;

    public CrmHrSaveReconciliationJourneyTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task A_saved_profile_takes_the_owners_values_while_an_unsaved_interaction_draft_of_the_same_record_survives()
    {
        var artifactsDir = CrmHrWorkspaceJourneySupport.ArtifactsDirectory("crm-hr-save-reconciliation-journey");
        var suffix = CrmHrWorkspaceJourneySupport.NewSuffix();
        await using var owner = await CrmHrWorkspaceJourneySupport.BuildOwnerProviderAsync(fixture, "playwright-crm-save-reconciliation");
        var seed = await SeedAsync(owner, suffix);
        var firstNote = $"Saved first {suffix}";
        var interactionDraft = $"Interaction the operator is still writing {suffix}";
        var secondNote = $"Saved second {suffix}";

        await CrmHrWorkspaceJourneySupport.RunAsync(fixture, artifactsDir, async (page, oracle) =>
        {
            await CrmHrWorkspaceJourneySupport.NavigateAsync(fixture, page, oracle, $"/crm-hr/crm?accountId={seed.AccountId:D}");
            var summary = page.GetByTestId("crmhr-account-summary");
            await Assertions.Expect(summary).ToHaveAttributeAsync("data-interactive", "true", new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(page.GetByTestId("crmhr-account-commercial-notes")).ToHaveValueAsync(seed.SeededNotes);

            // One committed profile, read back by its identifier through the owner.
            await page.GetByTestId("crmhr-account-commercial-notes").FillAsync(firstNote);
            await page.GetByTestId("crmhr-account-commercial-notes").PressAsync("Tab");
            await page.GetByTestId("crmhr-account-save-button").ClickAsync();
            await CrmHrWorkspaceJourneySupport.ExpectToastAsync(page, "CRM account profile saved.");
            var afterFirstSave = await ReadAccountAsync(owner, seed.AccountId);
            Assert.Equal(firstNote, afterFirstSave.Profile.CommercialNotes);
            var profileId = afterFirstSave.Profile.Id;
            Assert.NotNull(profileId);

            // An interaction the operator starts and does not save: it belongs to them until they save it.
            await page.GetByTestId("crmhr-crm-tab-interactions").ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-interaction-subject")).ToBeVisibleAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await page.GetByTestId("crmhr-interaction-subject").FillAsync(interactionDraft);
            await page.GetByTestId("crmhr-interaction-subject").PressAsync("Tab");
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "01-interaction-draft-1600.png") });

            // The profile is committed again from the Overview form while that draft is open.
            await page.GetByTestId("crmhr-crm-tab-overview").ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-account-commercial-notes")).ToHaveValueAsync(firstNote);
            await page.GetByTestId("crmhr-account-commercial-notes").FillAsync(secondNote);
            await page.GetByTestId("crmhr-account-commercial-notes").PressAsync("Tab");
            await page.GetByTestId("crmhr-account-save-button").ClickAsync();
            await CrmHrWorkspaceJourneySupport.ExpectToastAsync(page, "CRM account profile saved.");

            // The committed editor shows what the owner accepted; the same profile record was updated, not replaced.
            var afterSecondSave = await ReadAccountAsync(owner, seed.AccountId);
            Assert.Equal(secondNote, afterSecondSave.Profile.CommercialNotes);
            Assert.Equal(profileId, afterSecondSave.Profile.Id);
            await Assertions.Expect(page.GetByTestId("crmhr-account-commercial-notes")).ToHaveValueAsync(secondNote);

            // The unsaved interaction draft survived the read-back of an editor it has nothing to do with.
            await page.GetByTestId("crmhr-crm-tab-interactions").ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-interaction-subject")).ToHaveValueAsync(interactionDraft);
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "02-draft-survived-1600.png") });

            // Nothing of that draft reached the owner: its subject is nowhere in the record's history.
            var activity = await ReadActivityAsync(owner, seed.AccountId);
            Assert.DoesNotContain(activity.Items, item => item.Title.Contains(interactionDraft, StringComparison.Ordinal));
        });
    }

    private static async Task<CrmAccountWorkspaceModel> ReadAccountAsync(ServiceProvider owner, Guid accountId)
    {
        await using var scope = owner.CreateAsyncScope();
        var workspace = await scope.ServiceProvider.GetRequiredService<CrmService>().GetAccountWorkspaceAsync(accountId);
        Assert.NotNull(workspace);
        Assert.Equal(accountId, workspace!.AccountPartyId);
        return workspace;
    }

    private static async Task<CrmActivityHistoryPage> ReadActivityAsync(ServiceProvider owner, Guid accountId)
    {
        await using var scope = owner.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CrmService>()
            .SearchAccountActivityAsync(new CrmActivityHistoryQuery(accountId, 0, CrmActivityHistoryQueryLimits.MaximumPageSize));
    }

    private static async Task<SeededAccount> SeedAsync(ServiceProvider owner, string suffix)
    {
        await using var scope = owner.CreateAsyncScope();
        var parties = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var crm = scope.ServiceProvider.GetRequiredService<CrmService>();
        var accountName = $"Reconciliation Account {suffix}";
        var seededNotes = $"Seeded reconciliation notes {suffix}";
        var accountId = await CrmHrWorkspaceJourneySupport.CreatePartyAsync(
            parties,
            accountName,
            PartyType.Organization,
            PartyRoleKind.Customer,
            $"reconciliation.account.{suffix}@example.test");
        var profile = await crm.SaveAccountProfileAsync(new CrmAccountProfileEditorModel
        {
            AccountPartyId = accountId,
            RelationshipStage = CrmAccountRelationshipStage.Prospect,
            CommercialNotes = seededNotes,
            LastChangedBy = "playwright-tests"
        });
        Assert.True(profile.IsSuccess, string.Join(" ", profile.Errors.Select(error => error.Message)));
        return new SeededAccount(accountId, accountName, seededNotes);
    }

    private sealed record SeededAccount(Guid AccountId, string AccountName, string SeededNotes);
}
