using System.Text.RegularExpressions;
using CanDoItAll.Modules.CrmHr;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

// Directory create and edit through the real Web host and PostgreSQL: a rejected save without a display name, a party
// created from the real identity inputs, the returned identity reopened after a reload, an edited summary, unsaved
// drafts surviving an editor tab change, and a cancelled draft that never reaches the owner.
[Collection(PlaywrightCollection.Name)]
public sealed class CrmHrDirectoryEditJourneyTests
{
    private static readonly Regex SavedPartyUrl = new(@"^https?://[^/]+/crm-hr/directory\?partyId=[0-9a-fA-F-]{36}$");

    private readonly PlaywrightAppFixture fixture;

    public CrmHrDirectoryEditJourneyTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Party_is_validated_created_reopened_edited_and_a_cancelled_draft_is_never_persisted()
    {
        var evidence = CrmHrJourneyEvidence.Create("crm-hr-journey-directory-edit");
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var displayName = $"Journey Org {suffix}";
        var legalName = $"Journey Org {suffix} Ltd";
        var externalCode = $"JRN-{suffix}";
        var email = $"journey.org.{suffix}@example.test";
        var createdSummary = $"Created through the editor {suffix}";
        var editedSummary = $"Edited after the reload {suffix}";
        var firstTag = $"journey-{suffix}";
        var secondTag = $"priority-{suffix}";
        var validationMarker = $"Rejected draft {suffix}";
        var draftName = $"Unsaved Name {suffix}";
        var draftSummary = $"Unsaved summary {suffix}";

        await using var owner = await CrmHrDirectoryJourneySupport.BuildOwnerProviderAsync(fixture, "playwright-journey-directory-edit");

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = 1600, Height = 1000 }
        });
        await evidence.StartTraceAsync(context);
        var page = await context.NewPageAsync();
        try
        {
            var oracle = CrmHrBrowserOracle.Attach(page);
            var dialog = page.GetByTestId("crmhr-directory-record-dialog");
            var saveButton = page.GetByTestId("crmhr-party-save-button");
            var nameInput = page.GetByTestId("crmhr-party-display-name");
            var summaryInput = page.GetByTestId("crmhr-party-summary");

            await CrmHrDirectoryJourneySupport.OpenAsync(oracle, page, $"{fixture.BaseUrl}/crm-hr/directory");
            await CrmHrDirectoryJourneySupport.WaitForCatalogAsync(page, "crmhr-directory");

            // New party: the editor opens as a draft on the base route.
            await page.GetByTestId("crmhr-directory-new-button").ClickAsync();
            await Assertions.Expect(dialog).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(dialog.Locator(".cda-dialog__title")).ToHaveTextAsync("New party");
            await Assertions.Expect(nameInput).ToHaveValueAsync(string.Empty);

            // Validation: a save without a display name is rejected by the owner, nothing is created, and the draft
            // stays open with what was typed.
            await summaryInput.FillAsync(validationMarker);
            await saveButton.ClickAsync();
            var rejectionToast = CrmHrDirectoryJourneySupport.Toast(page, "Display name is required.");
            await Assertions.Expect(rejectionToast).ToBeVisibleAsync(new() { Timeout = 30_000 });
            // Evidence only: whether the rejection message is painted above the modal record dialog for the user.
            await evidence.WriteLinesAsync(
                "rejection-toast-visibility.txt",
                [$"'Display name is required.' painted on top of the record dialog: {await CrmHrDirectoryJourneySupport.IsPaintedOnTopAsync(rejectionToast)}"]);
            await Assertions.Expect(dialog).ToBeVisibleAsync();
            await Assertions.Expect(dialog.Locator(".cda-dialog__title")).ToHaveTextAsync("New party");
            await Assertions.Expect(summaryInput).ToHaveValueAsync(validationMarker);
            await Assertions.Expect(page).ToHaveURLAsync(CrmHrDirectoryJourneySupport.RouteUrl("/crm-hr/directory"));
            var afterRejectedSave = await CrmHrDirectoryJourneySupport.ListDirectoryAsync(owner);
            Assert.DoesNotContain(afterRejectedSave, item => string.Equals(item.Summary, validationMarker, StringComparison.Ordinal));
            Assert.DoesNotContain(afterRejectedSave, item => string.IsNullOrWhiteSpace(item.DisplayName));
            await evidence.ScreenshotAsync(page, "01-rejected-save-1600.png");

            // Create: identity fields through the real inputs, then one click on Save party.
            await page.GetByTestId("crmhr-party-type").SelectOptionAsync(new[] { PartyType.Organization.ToString() });
            await page.GetByTestId("crmhr-party-status").SelectOptionAsync(new[] { PartyLifecycleStatus.Active.ToString() });
            await page.GetByTestId("crmhr-party-external-code").FillAsync(externalCode);
            await nameInput.FillAsync(displayName);
            await page.GetByTestId("crmhr-party-legal-name").FillAsync(legalName);
            await page.GetByTestId("crmhr-party-role").SelectOptionAsync(new[] { PartyRoleKind.Customer.ToString() });
            await AddTagAsync(page, firstTag);
            await AddTagAsync(page, secondTag);
            await page.GetByTestId("crmhr-party-email").FillAsync(email);
            await summaryInput.FillAsync(createdSummary);
            await saveButton.ClickAsync();

            // The returned identity is the one the host routes to; everything else is read back from the owner.
            await Assertions.Expect(page).ToHaveURLAsync(SavedPartyUrl, new() { Timeout = 30_000 });
            await Assertions.Expect(CrmHrDirectoryJourneySupport.Toast(page, "Party saved.")).ToBeVisibleAsync(new() { Timeout = 30_000 });
            var partyId = CrmHrDirectoryJourneySupport.ReadGuidQuery(page.Url, "partyId");
            await Assertions.Expect(dialog.Locator(".cda-dialog__title")).ToHaveTextAsync(displayName);

            var created = await CrmHrDirectoryJourneySupport.ReadPartyAsync(owner, partyId);
            Assert.Equal(partyId, created.Id);
            Assert.Equal(displayName, created.DisplayName);
            Assert.Equal(legalName, created.LegalName);
            Assert.Equal(externalCode, created.ExternalCode);
            Assert.Equal(PartyType.Organization, created.PartyType);
            Assert.Equal(PartyLifecycleStatus.Active, created.LifecycleStatus);
            Assert.Equal(createdSummary, created.Summary);
            Assert.Equal(new[] { firstTag, secondTag }.Order(StringComparer.Ordinal), created.Tags.Order(StringComparer.Ordinal));
            var createdRole = Assert.Single(created.Roles);
            Assert.Equal(PartyRoleKind.Customer, createdRole.RoleKind);
            Assert.True(createdRole.IsPrimary);
            var createdContact = Assert.Single(created.ContactPoints);
            Assert.Equal(PartyContactType.Email, createdContact.ContactType);
            Assert.Equal(email, createdContact.Value);
            Assert.True(createdContact.IsPrimary);
            Assert.False(created.IsSensitive);
            Assert.Equal(partyId, Assert.Single(await CrmHrDirectoryJourneySupport.ListDirectoryAsync(owner), item => string.Equals(item.DisplayName, displayName, StringComparison.Ordinal)).Id);
            await evidence.ScreenshotAsync(page, "02-created-1600.png");

            // Reload: the returned identity reopens the same record with the typed values.
            await CrmHrDirectoryJourneySupport.OpenAsync(oracle, page, $"{fixture.BaseUrl}/crm-hr/directory?partyId={partyId:D}");
            await Assertions.Expect(dialog).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(dialog.Locator(".cda-dialog__title")).ToHaveTextAsync(displayName);
            await Assertions.Expect(nameInput).ToHaveValueAsync(displayName);
            await Assertions.Expect(page.GetByTestId("crmhr-party-type")).ToHaveValueAsync(PartyType.Organization.ToString());
            await Assertions.Expect(page.GetByTestId("crmhr-party-status")).ToHaveValueAsync(PartyLifecycleStatus.Active.ToString());
            await Assertions.Expect(page.GetByTestId("crmhr-party-external-code")).ToHaveValueAsync(externalCode);
            await Assertions.Expect(page.GetByTestId("crmhr-party-legal-name")).ToHaveValueAsync(legalName);
            await Assertions.Expect(page.GetByTestId("crmhr-party-role")).ToHaveValueAsync(PartyRoleKind.Customer.ToString());
            await Assertions.Expect(page.GetByTestId("crmhr-party-email")).ToHaveValueAsync(email);
            await Assertions.Expect(summaryInput).ToHaveValueAsync(createdSummary);
            var tagsEditor = page.GetByTestId("crmhr-party-tags-editor");
            await Assertions.Expect(tagsEditor.GetByText(firstTag, new() { Exact = true })).ToBeVisibleAsync();
            await Assertions.Expect(tagsEditor.GetByText(secondTag, new() { Exact = true })).ToBeVisibleAsync();

            // Edit: the summary changes, one click saves, and the owner holds the change on the same identity.
            await summaryInput.FillAsync(editedSummary);
            await saveButton.ClickAsync();
            await Assertions.Expect(CrmHrDirectoryJourneySupport.Toast(page, "Party saved.")).ToBeVisibleAsync(new() { Timeout = 30_000 });
            var edited = await CrmHrDirectoryJourneySupport.ReadPartyAsync(owner, partyId);
            Assert.Equal(editedSummary, edited.Summary);
            Assert.Equal(displayName, edited.DisplayName);
            Assert.Equal(PartyType.Organization, edited.PartyType);
            Assert.Equal(new[] { firstTag, secondTag }.Order(StringComparer.Ordinal), edited.Tags.Order(StringComparer.Ordinal));
            Assert.Equal(email, Assert.Single(edited.ContactPoints).Value);
            Assert.Single(await CrmHrDirectoryJourneySupport.ListDirectoryAsync(owner), item => string.Equals(item.DisplayName, displayName, StringComparison.Ordinal));

            await CrmHrDirectoryJourneySupport.OpenAsync(oracle, page, $"{fixture.BaseUrl}/crm-hr/directory?partyId={partyId:D}");
            await Assertions.Expect(dialog).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(summaryInput).ToHaveValueAsync(editedSummary);
            await evidence.ScreenshotAsync(page, "03-edited-after-reload-1600.png");

            // Draft preservation: unsaved values survive a change of editor tab and back.
            await nameInput.FillAsync(draftName);
            await summaryInput.FillAsync(draftSummary);
            await page.GetByTestId("crmhr-directory-tab-contacts").ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-contact-add")).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(nameInput).ToHaveCountAsync(0);
            await page.GetByTestId("crmhr-directory-tab-profile").ClickAsync();
            await Assertions.Expect(nameInput).ToHaveValueAsync(draftName, new() { Timeout = 30_000 });
            await Assertions.Expect(summaryInput).ToHaveValueAsync(draftSummary);
            await evidence.ScreenshotAsync(page, "04-draft-preserved-1600.png");
            await CrmHrDirectoryJourneySupport.AssertConstrainedLayoutAsync(
                page,
                evidence,
                "05-editor-1100.png",
                dialog,
                saveButton,
                page.GetByTestId("crmhr-directory-record-close"));

            // Cancel: closing the dialog discards the draft; the owner and the reopened record keep the saved values.
            await page.GetByTestId("crmhr-directory-record-close").ClickAsync();
            await Assertions.Expect(dialog).ToHaveCountAsync(0, new() { Timeout = 30_000 });
            await Assertions.Expect(page).ToHaveURLAsync(CrmHrDirectoryJourneySupport.RouteUrl("/crm-hr/directory"));
            var afterCancel = await CrmHrDirectoryJourneySupport.ReadPartyAsync(owner, partyId);
            Assert.Equal(displayName, afterCancel.DisplayName);
            Assert.Equal(editedSummary, afterCancel.Summary);
            var directoryAfterCancel = await CrmHrDirectoryJourneySupport.ListDirectoryAsync(owner);
            Assert.DoesNotContain(directoryAfterCancel, item => string.Equals(item.DisplayName, draftName, StringComparison.Ordinal));
            Assert.DoesNotContain(directoryAfterCancel, item => string.Equals(item.Summary, draftSummary, StringComparison.Ordinal));

            await CrmHrDirectoryJourneySupport.WaitForCatalogAsync(page, "crmhr-directory");
            await CrmHrDirectoryJourneySupport.SearchCatalogExpectingNoneAsync(page, "crmhr-directory", "crmhr-directory-item", draftName);
            var card = await CrmHrDirectoryJourneySupport.SearchCatalogForAsync(page, "crmhr-directory", "crmhr-directory-item", displayName);
            await card.ClickAsync();
            await Assertions.Expect(dialog).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(nameInput).ToHaveValueAsync(displayName);
            await Assertions.Expect(summaryInput).ToHaveValueAsync(editedSummary);
            await Assertions.Expect(page).ToHaveURLAsync(CrmHrDirectoryJourneySupport.RouteUrl("/crm-hr/directory", $"partyId={partyId:D}"));
            await evidence.ScreenshotAsync(page, "06-reopened-after-cancel-1600.png");

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

    // The tag editor commits the typed text on Enter and clears its input; the chip is the receipt of the commit.
    private static async Task AddTagAsync(IPage page, string tag)
    {
        var input = page.GetByTestId("crmhr-party-tags");
        await input.FillAsync(tag);
        await input.PressAsync("Enter");
        await Assertions.Expect(page.GetByTestId("crmhr-party-tags-editor").GetByText(tag, new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(input).ToHaveValueAsync(string.Empty);
    }
}
