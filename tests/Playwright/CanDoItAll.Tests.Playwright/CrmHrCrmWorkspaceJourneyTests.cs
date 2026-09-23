using System.Globalization;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

// The CRM workspace through the real Web host and PostgreSQL: a seeded organization account opened from the real
// catalog, the relationship profile edited through the Overview form, the summary's conversion action, connected
// records chosen in the real party picker, an interaction with a participant and an overdue follow-up, and a second
// account that stays untouched by the first account's edits and unsaved draft. Every write is read back through
// CrmService (and the persisted interaction row) by the seeded identifiers.
[Collection(PlaywrightCollection.Name)]
public sealed class CrmHrCrmWorkspaceJourneyTests
{
    private readonly PlaywrightAppFixture fixture;

    public CrmHrCrmWorkspaceJourneyTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Crm_account_profile_connections_and_an_overdue_follow_up_are_saved_for_the_selected_account_only()
    {
        var artifactsDir = CrmHrWorkspaceJourneySupport.ArtifactsDirectory("crm-hr-crm-workspace-journey");
        var suffix = CrmHrWorkspaceJourneySupport.NewSuffix();
        await using var owner = await CrmHrWorkspaceJourneySupport.BuildOwnerProviderAsync(fixture, "playwright-crm-workspace-journey");
        var seed = await SeedAsync(owner, suffix);

        await CrmHrWorkspaceJourneySupport.RunAsync(fixture, artifactsDir, async (page, oracle) =>
        {
            // Select the account from the real catalog: search by the unique name, one click on the one card.
            await CrmHrWorkspaceJourneySupport.NavigateAsync(fixture, page, oracle, "/crm-hr/crm");
            var card = await CrmHrWorkspaceJourneySupport.FindSingleCardAsync(page, "crmhr-account", "crmhr-account-item", seed.AccountName);
            await card.ClickAsync();
            await CrmHrWorkspaceJourneySupport.ExpectUrlAsync(page, $"accountId={seed.AccountId:D}");
            var dialog = page.GetByTestId("crmhr-crm-record-dialog");
            await Assertions.Expect(dialog).ToBeVisibleAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(dialog.Locator(".cda-dialog__title")).ToHaveTextAsync(seed.AccountName);
            var summary = page.GetByTestId("crmhr-account-summary");
            await Assertions.Expect(summary).ToHaveAttributeAsync("data-account-id", seed.AccountId.ToString("D"));
            await Assertions.Expect(summary).ToHaveAttributeAsync("data-interactive", "true", new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(page.GetByTestId("crmhr-account-summary-name")).ToHaveTextAsync(seed.AccountName);
            var stageBadge = page.GetByTestId("crmhr-account-summary-stage");
            await Assertions.Expect(stageBadge).ToHaveTextAsync(CrmAccountRelationshipStage.Prospect.ToString());
            await Assertions.Expect(page.GetByTestId("crmhr-account-commercial-notes")).ToHaveValueAsync(seed.SeededAccountNotes);
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "01-account-selected-1600.png") });

            // Relationship profile through the Overview form: stage and commercial note.
            await page.GetByTestId("crmhr-account-stage").SelectOptionAsync(new[] { CrmAccountRelationshipStage.DormantCustomer.ToString() });
            await page.GetByTestId("crmhr-account-commercial-notes").FillAsync(seed.CommercialNote);
            await page.GetByTestId("crmhr-account-constraints").FillAsync(seed.ConstraintNote);
            await page.GetByTestId("crmhr-account-timing-risks").FillAsync(seed.TimingRiskNote);
            await page.GetByTestId("crmhr-account-timing-risks").PressAsync("Tab");
            await page.GetByTestId("crmhr-account-save-button").ClickAsync();
            await Assertions.Expect(stageBadge).ToHaveTextAsync(CrmAccountRelationshipStage.DormantCustomer.ToString(), new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await CrmHrWorkspaceJourneySupport.ExpectToastAsync(page, "CRM account profile saved.");
            var dormant = await ReadAccountAsync(owner, seed.AccountId);
            Assert.Equal(CrmAccountRelationshipStage.DormantCustomer, dormant.Profile.RelationshipStage);
            Assert.Equal(seed.CommercialNote, dormant.Profile.CommercialNotes);
            Assert.Equal(seed.ConstraintNote, dormant.Profile.ConstraintNotes);
            Assert.Equal(seed.TimingRiskNote, dormant.Profile.TimingRiskNotes);

            // Back to a prospect through the form, then the summary's conversion action: one click each.
            await page.GetByTestId("crmhr-account-stage").SelectOptionAsync(new[] { CrmAccountRelationshipStage.Prospect.ToString() });
            await page.GetByTestId("crmhr-account-save-button").ClickAsync();
            await Assertions.Expect(stageBadge).ToHaveTextAsync(CrmAccountRelationshipStage.Prospect.ToString(), new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            Assert.Equal(CrmAccountRelationshipStage.Prospect, (await ReadAccountAsync(owner, seed.AccountId)).Profile.RelationshipStage);

            await page.GetByTestId("crmhr-account-convert-active-button").ClickAsync();
            await Assertions.Expect(stageBadge).ToHaveTextAsync(CrmAccountRelationshipStage.ActiveCustomer.ToString(), new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(page.GetByTestId("crmhr-account-convert-active-button")).ToHaveCountAsync(0);
            await Assertions.Expect(page.GetByTestId("crmhr-account-stage")).ToHaveValueAsync(CrmAccountRelationshipStage.ActiveCustomer.ToString());
            var converted = await ReadAccountAsync(owner, seed.AccountId);
            Assert.Equal(CrmAccountRelationshipStage.ActiveCustomer, converted.Profile.RelationshipStage);
            Assert.Equal(seed.CommercialNote, converted.Profile.CommercialNotes);
            Assert.Equal(seed.ConstraintNote, converted.Profile.ConstraintNotes);
            Assert.Equal(seed.TimingRiskNote, converted.Profile.TimingRiskNotes);
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "02-profile-converted-1600.png") });

            // Constrained width with the Overview form open.
            await CrmHrWorkspaceJourneySupport.AssertDialogFitsConstrainedWidthAsync(
                page,
                "crmhr-crm-record-dialog",
                ["crmhr-account-save-button", "crmhr-crm-record-close"],
                Path.Combine(artifactsDir, "03-overview-1100.png"));

            // Connected records: the contact and the account manager, each chosen in the real party picker.
            await page.GetByTestId("crmhr-crm-tab-connections").ClickAsync();
            await page.GetByTestId("crmhr-connection-add").ClickAsync();
            await page.GetByTestId("crmhr-connection-party-0").ClickAsync();
            await CrmHrWorkspaceJourneySupport.PickPartyAsync(page, "crmhr-connection-party-picker", seed.ContactName, seed.ContactId);
            await Assertions.Expect(dialog).ToContainTextAsync($"{seed.ContactName} (Person)");
            await page.GetByTestId("crmhr-connection-role-0").SelectOptionAsync(new[] { CrmAccountConnectionRole.PrimaryContact.ToString() });
            await page.GetByTestId("crmhr-connection-notes-0").FillAsync(seed.ConnectionNote);
            await page.GetByTestId("crmhr-connection-notes-0").PressAsync("Tab");
            await page.GetByTestId("crmhr-connection-primary-0").CheckAsync();

            await page.GetByTestId("crmhr-connection-add").ClickAsync();
            await page.GetByTestId("crmhr-connection-party-1").ClickAsync();
            await CrmHrWorkspaceJourneySupport.PickPartyAsync(page, "crmhr-connection-party-picker", seed.ManagerName, seed.ManagerId);
            await Assertions.Expect(dialog).ToContainTextAsync($"{seed.ManagerName} (Person)");
            await page.GetByTestId("crmhr-connection-role-1").SelectOptionAsync(new[] { CrmAccountConnectionRole.AccountManager.ToString() });
            await page.GetByTestId("crmhr-connection-save-button").ClickAsync();
            await CrmHrWorkspaceJourneySupport.ExpectToastAsync(page, "Company connections and related projects saved.");

            var connected = (await ReadAccountAsync(owner, seed.AccountId)).ConnectedRecords;
            Assert.Equal(2, connected.Count);
            var contactConnection = Assert.Single(connected.Where(item => item.RelatedPartyId == seed.ContactId));
            Assert.Equal(CrmAccountConnectionRole.PrimaryContact, contactConnection.Role);
            Assert.Equal(seed.ConnectionNote, contactConnection.Notes);
            Assert.Equal(seed.ContactName, contactConnection.DisplayName);
            Assert.True(contactConnection.IsPrimary);
            var managerConnection = Assert.Single(connected.Where(item => item.RelatedPartyId == seed.ManagerId));
            Assert.Equal(CrmAccountConnectionRole.AccountManager, managerConnection.Role);
            Assert.False(managerConnection.IsPrimary);
            await AssertConnectionRowsAsync(page, seed);
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "04-connections-saved-1600.png") });

            // Interactions: no follow-up is accepted yet; then a call with a participant and an overdue follow-up.
            await page.GetByTestId("crmhr-crm-tab-interactions").ClickAsync();
            var activity = page.GetByTestId("crmhr-account-activity");
            await Assertions.Expect(activity).ToHaveAttributeAsync("data-accepted", "true", new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(activity).ToHaveAttributeAsync("data-loading", "false");
            await Assertions.Expect(page.GetByTestId("crmhr-account-action-count")).ToHaveTextAsync("0 open follow-ups");
            await Assertions.Expect(page.GetByTestId("crmhr-account-overdue-count")).ToHaveTextAsync("0 overdue");

            await page.GetByTestId("crmhr-interaction-type").SelectOptionAsync(new[] { InteractionType.Call.ToString() });
            await page.GetByTestId("crmhr-interaction-occurred-on").FillAsync(Iso(seed.OccurredOn));
            await page.GetByTestId("crmhr-interaction-subject").FillAsync(seed.InteractionSubject);
            await page.GetByTestId("crmhr-interaction-summary").FillAsync(seed.InteractionSummary);
            await page.GetByTestId($"crmhr-interaction-participant-{seed.ContactId:N}").CheckAsync();
            await page.GetByTestId("crmhr-next-action-text").FillAsync(seed.NextActionText);
            await page.GetByTestId("crmhr-next-action-owner").SelectOptionAsync(new[] { seed.ManagerId.ToString("D") });
            await page.GetByTestId("crmhr-next-action-due-on").FillAsync(Iso(seed.FollowUpDueOn));
            await page.GetByTestId("crmhr-next-action-due-on").PressAsync("Tab");
            await page.GetByTestId("crmhr-interaction-save-button").ClickAsync();
            await CrmHrWorkspaceJourneySupport.ExpectToastAsync(page, "CRM interaction saved.");

            var interactionRow = page.Locator("[data-testid='crmhr-account-activity-item'][data-kind='Interaction']").Filter(new() { HasText = seed.InteractionSubject });
            await Assertions.Expect(interactionRow).ToHaveCountAsync(1, new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(interactionRow).ToHaveAttributeAsync("data-overdue", "true");
            await Assertions.Expect(interactionRow).ToContainTextAsync("Overdue");
            await Assertions.Expect(interactionRow).ToContainTextAsync(seed.ContactName);
            await Assertions.Expect(interactionRow).ToContainTextAsync(seed.InteractionSummary);
            await Assertions.Expect(page.GetByTestId("crmhr-account-activity-overdue-total")).ToHaveTextAsync("1 overdue");
            await Assertions.Expect(page.GetByTestId("crmhr-account-activity-totals")).ToContainTextAsync("1 next actions");
            await Assertions.Expect(page.GetByTestId("crmhr-account-action-count")).ToHaveTextAsync("1 open follow-ups");
            await Assertions.Expect(page.GetByTestId("crmhr-account-overdue-count")).ToHaveTextAsync("1 overdue");
            await Assertions.Expect(page.GetByTestId("crmhr-account-activity-counts-unavailable")).ToHaveCountAsync(0);

            var ownerActivity = await ReadActivityAsync(owner, seed.AccountId);
            Assert.Equal(1, ownerActivity.ActionCount);
            Assert.Equal(1, ownerActivity.OverdueActionCount);
            await Assertions.Expect(page.GetByTestId("crmhr-account-activity-totals")).ToContainTextAsync($"{ownerActivity.TotalCount} activities");
            var ownerInteraction = Assert.Single(ownerActivity.Items.Where(item =>
                string.Equals(item.Kind, "Interaction", StringComparison.Ordinal) &&
                string.Equals(item.Title, seed.InteractionSubject, StringComparison.Ordinal)));
            Assert.True(ownerInteraction.IsOverdue);
            Assert.Contains(seed.ContactName, ownerInteraction.Meta, StringComparison.Ordinal);
            Assert.Contains($"Next action due {Iso(seed.FollowUpDueOn)}", ownerInteraction.Meta, StringComparison.Ordinal);
            var persisted = await ReadInteractionAsync(owner, seed.AccountId, ownerInteraction.Id);
            Assert.Equal(InteractionType.Call, persisted.Detail.InteractionType);
            Assert.Equal(seed.InteractionSubject, persisted.Detail.Subject);
            Assert.Equal(seed.InteractionSummary, persisted.Record.Summary);
            Assert.Equal(seed.NextActionText, persisted.Record.NextActionText);
            Assert.Equal(seed.ManagerId, persisted.Record.NextActionOwnerPartyId);
            Assert.Equal(seed.FollowUpDueOn, DateOnly.FromDateTime(persisted.Record.NextActionDueUtc!.Value.UtcDateTime));
            Assert.Equal(seed.OccurredOn, DateOnly.FromDateTime(persisted.Record.OccurredAtUtc.UtcDateTime));
            Assert.Contains(seed.ContactId, persisted.ParticipantIds);
            Assert.DoesNotContain(seed.ManagerId, persisted.ParticipantIds);
            await interactionRow.ScrollIntoViewIfNeededAsync();
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "05-overdue-follow-up-1600.png") });

            // An unsaved draft on this account, then a deep link to the other account: nothing of this account is
            // shown there, and neither the saved edits nor the draft reached the other account or were persisted.
            await page.GetByTestId("crmhr-crm-tab-overview").ClickAsync();
            await page.GetByTestId("crmhr-account-commercial-notes").FillAsync(seed.UnsavedDraft);
            await page.GetByTestId("crmhr-account-commercial-notes").PressAsync("Tab");
            await CrmHrWorkspaceJourneySupport.NavigateAsync(fixture, page, oracle, $"/crm-hr/crm?accountId={seed.OtherAccountId:D}");
            await Assertions.Expect(dialog).ToBeVisibleAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(summary).ToHaveAttributeAsync("data-account-id", seed.OtherAccountId.ToString("D"));
            await Assertions.Expect(summary).ToHaveAttributeAsync("data-interactive", "true", new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(dialog.Locator(".cda-dialog__title")).ToHaveTextAsync(seed.OtherAccountName);
            await Assertions.Expect(page.GetByTestId("crmhr-account-summary-name")).ToHaveTextAsync(seed.OtherAccountName);
            await Assertions.Expect(stageBadge).ToHaveTextAsync(CrmAccountRelationshipStage.Prospect.ToString());
            await Assertions.Expect(page.GetByTestId("crmhr-account-stage")).ToHaveValueAsync(CrmAccountRelationshipStage.Prospect.ToString());
            await Assertions.Expect(page.GetByTestId("crmhr-account-commercial-notes")).ToHaveValueAsync(seed.OtherAccountNotes);
            await Assertions.Expect(page.GetByTestId("crmhr-account-constraints")).ToHaveValueAsync(string.Empty);
            await Assertions.Expect(page.GetByTestId("crmhr-account-timing-risks")).ToHaveValueAsync(string.Empty);
            await page.GetByTestId("crmhr-crm-tab-connections").ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-connection-save-button")).ToBeVisibleAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(page.GetByTestId("crmhr-connection-role-0")).ToHaveCountAsync(0);
            await page.GetByTestId("crmhr-crm-tab-interactions").ClickAsync();
            await Assertions.Expect(activity).ToHaveAttributeAsync("data-accepted", "true", new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(page.GetByTestId("crmhr-account-action-count")).ToHaveTextAsync("0 open follow-ups");
            await Assertions.Expect(page.GetByTestId("crmhr-account-overdue-count")).ToHaveTextAsync("0 overdue");
            var otherDialogText = await dialog.InnerTextAsync();
            foreach (var marker in new[] { seed.AccountName, seed.CommercialNote, seed.UnsavedDraft, seed.InteractionSubject, seed.NextActionText })
            {
                Assert.DoesNotContain(marker, otherDialogText, StringComparison.Ordinal);
            }

            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "06-other-account-untouched-1600.png") });

            var other = await ReadAccountAsync(owner, seed.OtherAccountId);
            Assert.Equal(CrmAccountRelationshipStage.Prospect, other.Profile.RelationshipStage);
            Assert.Equal(seed.OtherAccountNotes, other.Profile.CommercialNotes);
            Assert.Empty(other.ConnectedRecords);
            var otherActivity = await ReadActivityAsync(owner, seed.OtherAccountId);
            Assert.Equal(0, otherActivity.ActionCount);
            Assert.DoesNotContain(otherActivity.Items, item => string.Equals(item.Kind, "Interaction", StringComparison.Ordinal));
            var afterDraft = await ReadAccountAsync(owner, seed.AccountId);
            Assert.Equal(seed.CommercialNote, afterDraft.Profile.CommercialNotes);
            Assert.Equal(CrmAccountRelationshipStage.ActiveCustomer, afterDraft.Profile.RelationshipStage);

            // Reload the first account's deep link: a fresh document and circuit show the persisted record.
            await CrmHrWorkspaceJourneySupport.NavigateAsync(fixture, page, oracle, $"/crm-hr/crm?accountId={seed.AccountId:D}");
            await Assertions.Expect(dialog).ToBeVisibleAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(summary).ToHaveAttributeAsync("data-account-id", seed.AccountId.ToString("D"));
            await Assertions.Expect(summary).ToHaveAttributeAsync("data-interactive", "true", new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(stageBadge).ToHaveTextAsync(CrmAccountRelationshipStage.ActiveCustomer.ToString());
            await Assertions.Expect(page.GetByTestId("crmhr-account-convert-active-button")).ToHaveCountAsync(0);
            await Assertions.Expect(page.GetByTestId("crmhr-account-commercial-notes")).ToHaveValueAsync(seed.CommercialNote);
            await page.GetByTestId("crmhr-crm-tab-connections").ClickAsync();
            await AssertConnectionRowsAsync(page, seed);
            await page.GetByTestId("crmhr-crm-tab-interactions").ClickAsync();
            await Assertions.Expect(interactionRow).ToHaveAttributeAsync("data-overdue", "true", new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(page.GetByTestId("crmhr-account-action-count")).ToHaveTextAsync("1 open follow-ups");
            await Assertions.Expect(page.GetByTestId("crmhr-account-overdue-count")).ToHaveTextAsync("1 overdue");
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "07-reloaded-account-1600.png") });

            // The summary's directory action leaves the CRM workspace for exactly this account's directory record.
            await page.GetByTestId("crmhr-account-summary-open-directory").ClickAsync();
            await CrmHrWorkspaceJourneySupport.ExpectUrlAsync(page, $"/crm-hr/directory?partyId={seed.AccountId:D}");
            await Assertions.Expect(page.GetByTestId("crmhr-directory-record-dialog")).ToBeVisibleAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(page.GetByTestId("crmhr-party-display-name")).ToHaveValueAsync(seed.AccountName);
            await Assertions.Expect(page.GetByTestId("crmhr-party-type")).ToHaveValueAsync(PartyType.Organization.ToString());
        });
    }

    private static string Iso(DateOnly value)
        => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // The saved connections as the form shows them: exactly two rows, each with the label of its directory record and
    // the role saved for that record. The owner decides the row order, so a row is found by its record label. The
    // label carries no test identifier; the row is the nearest container of the row's picker button and role select.
    private static async Task AssertConnectionRowsAsync(IPage page, SeededCrm seed)
    {
        await Assertions.Expect(page.GetByTestId("crmhr-connection-role-1")).ToBeVisibleAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
        await Assertions.Expect(page.GetByTestId("crmhr-connection-role-2")).ToHaveCountAsync(0);
        var expected = new Dictionary<string, CrmAccountConnectionRole>(StringComparer.Ordinal)
        {
            [$"{seed.ContactName} (Person)"] = CrmAccountConnectionRole.PrimaryContact,
            [$"{seed.ManagerName} (Person)"] = CrmAccountConnectionRole.AccountManager
        };
        foreach (var (label, role) in expected)
        {
            var rows = Enumerable.Range(0, 2)
                .Select(index => (
                    Row: page.GetByTestId($"crmhr-connection-role-{index}").Locator($"xpath=ancestor::*[.//*[@data-testid='crmhr-connection-party-{index}']][1]"),
                    Role: page.GetByTestId($"crmhr-connection-role-{index}")))
                .ToArray();
            var matching = new List<ILocator>();
            foreach (var (row, roleSelect) in rows)
            {
                if ((await row.InnerTextAsync()).Contains(label, StringComparison.Ordinal))
                {
                    matching.Add(roleSelect);
                }
            }

            var select = Assert.Single(matching);
            await Assertions.Expect(select).ToHaveValueAsync(role.ToString());
        }
    }

    private static async Task<CrmAccountWorkspaceModel> ReadAccountAsync(ServiceProvider owner, Guid accountId)
    {
        await using var scope = owner.CreateAsyncScope();
        var workspace = await scope.ServiceProvider.GetRequiredService<CrmService>().GetAccountWorkspaceAsync(accountId);
        Assert.NotNull(workspace);
        Assert.Equal(accountId, workspace!.AccountPartyId);
        Assert.Equal(accountId, workspace.Profile.AccountPartyId);
        return workspace;
    }

    private static async Task<CrmActivityHistoryPage> ReadActivityAsync(ServiceProvider owner, Guid accountId)
    {
        await using var scope = owner.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CrmService>()
            .SearchAccountActivityAsync(new CrmActivityHistoryQuery(accountId, 0, CrmActivityHistoryQueryLimits.MaximumPageSize));
    }

    // The owner's interaction by its exact identifier, with the persisted follow-up and participant rows the owner's
    // read model does not expose.
    private static async Task<PersistedInteraction> ReadInteractionAsync(ServiceProvider owner, Guid accountId, Guid interactionId)
    {
        await using var scope = owner.CreateAsyncScope();
        var detail = await scope.ServiceProvider.GetRequiredService<CrmService>().GetAccountInteractionAsync(accountId, interactionId);
        Assert.NotNull(detail);
        Assert.Equal(interactionId, detail!.Id);
        await using var dbContext = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        var record = await dbContext.Set<InteractionRecord>().AsNoTracking().SingleAsync(item => item.Id == interactionId);
        var links = await dbContext.Set<InteractionPartyLink>().AsNoTracking()
            .Where(item => item.InteractionId == interactionId)
            .ToListAsync();
        Assert.Contains(links, link => link.PartyId == accountId && link.Role == InteractionPartyRole.Account);
        return new PersistedInteraction(
            detail,
            record,
            links.Where(link => link.Role != InteractionPartyRole.Account).Select(link => link.PartyId).ToArray());
    }

    private static async Task<SeededCrm> SeedAsync(ServiceProvider owner, string suffix)
    {
        await using var scope = owner.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var crmService = scope.ServiceProvider.GetRequiredService<CrmService>();

        var accountName = $"Journey Account {suffix}";
        var otherAccountName = $"Journey Bystander {suffix}";
        var contactName = $"Journey Contact {suffix}";
        var managerName = $"Journey Manager {suffix}";
        var seededAccountNotes = $"Seeded prospect notes {suffix}";
        var otherAccountNotes = $"Bystander notes {suffix}";

        var accountId = await CrmHrWorkspaceJourneySupport.CreatePartyAsync(partyDirectoryService, accountName, PartyType.Organization, PartyRoleKind.Customer, $"journey.account.{suffix}@example.test");
        var otherAccountId = await CrmHrWorkspaceJourneySupport.CreatePartyAsync(partyDirectoryService, otherAccountName, PartyType.Organization, PartyRoleKind.Customer, $"journey.bystander.{suffix}@example.test");
        var contactId = await CrmHrWorkspaceJourneySupport.CreatePartyAsync(partyDirectoryService, contactName, PartyType.Person, PartyRoleKind.CustomerContact, $"journey.contact.{suffix}@example.test");
        var managerId = await CrmHrWorkspaceJourneySupport.CreatePartyAsync(partyDirectoryService, managerName, PartyType.Person, PartyRoleKind.AccountManager, $"journey.manager.{suffix}@example.test");

        foreach (var (id, notes) in new[] { (accountId, seededAccountNotes), (otherAccountId, otherAccountNotes) })
        {
            var profile = await crmService.SaveAccountProfileAsync(new CrmAccountProfileEditorModel
            {
                AccountPartyId = id,
                RelationshipStage = CrmAccountRelationshipStage.Prospect,
                CommercialNotes = notes,
                LastChangedBy = "playwright-tests"
            });
            Assert.True(profile.IsSuccess, string.Join(" ", profile.Errors.Select(error => error.Message)));
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return new SeededCrm(
            accountId,
            accountName,
            seededAccountNotes,
            otherAccountId,
            otherAccountName,
            otherAccountNotes,
            contactId,
            contactName,
            managerId,
            managerName,
            $"Renewal priorities agreed {suffix}",
            $"Legal review stays open {suffix}",
            $"Executive sign-off may slip {suffix}",
            $"Unsaved draft {suffix}",
            $"Signs the renewal {suffix}",
            $"Renewal scoping call {suffix}",
            $"Scope and budget confirmed {suffix}",
            $"Send the renewal proposal {suffix}",
            today.AddDays(-10),
            today.AddDays(-3));
    }

    private sealed record PersistedInteraction(CrmInteractionDetailModel Detail, InteractionRecord Record, IReadOnlyList<Guid> ParticipantIds);

    private sealed record SeededCrm(
        Guid AccountId,
        string AccountName,
        string SeededAccountNotes,
        Guid OtherAccountId,
        string OtherAccountName,
        string OtherAccountNotes,
        Guid ContactId,
        string ContactName,
        Guid ManagerId,
        string ManagerName,
        string CommercialNote,
        string ConstraintNote,
        string TimingRiskNote,
        string UnsavedDraft,
        string ConnectionNote,
        string InteractionSubject,
        string InteractionSummary,
        string NextActionText,
        DateOnly OccurredOn,
        DateOnly FollowUpDueOn);
}
