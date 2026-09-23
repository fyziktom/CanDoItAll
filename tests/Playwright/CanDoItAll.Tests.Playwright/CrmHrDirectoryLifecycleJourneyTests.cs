using System.Text.RegularExpressions;
using CanDoItAll.Modules.CrmHr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

// Directory lifecycle on synthetic records through the real Web host and PostgreSQL: archive and reactivate with
// identity and references preserved, a duplicate merged into the retained party with its contact and relationship
// moved, and the CSV stewardship tools (export, preview, one applied row, a blocked duplicate row, nothing deleted).
[Collection(PlaywrightCollection.Name)]
public sealed class CrmHrDirectoryLifecycleJourneyTests
{
    private const string ExportHeader =
        "DisplayName,PartyType,LifecycleStatus,ExternalCode,LegalName,PreferredName,Summary,Tags,Region,CountryCode,TimeZone,IsSensitive,Roles,ContactPoints,Addresses";

    private readonly PlaywrightAppFixture fixture;

    public CrmHrDirectoryLifecycleJourneyTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Party_is_archived_and_reactivated_a_duplicate_is_merged_and_a_csv_row_is_imported_without_losing_records()
    {
        var evidence = CrmHrJourneyEvidence.Create("crm-hr-journey-directory-lifecycle");
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var mergeReason = $"Duplicate cleanup {suffix}";
        var importedName = $"Imported Candidate {suffix}";
        var importedEmail = $"imported.candidate.{suffix}@example.test";
        var importedAddress = $"200 Import Lane {suffix}";

        await using var owner = await CrmHrDirectoryJourneySupport.BuildOwnerProviderAsync(fixture, "playwright-journey-directory-lifecycle");
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
            var dialog = page.GetByTestId("crmhr-directory-record-dialog");
            var saveButton = page.GetByTestId("crmhr-party-save-button");
            var statusSelect = page.GetByTestId("crmhr-party-status");
            var savedToast = CrmHrDirectoryJourneySupport.Toast(page, "Party saved.");
            var personUrl = $"{fixture.BaseUrl}/crm-hr/directory?partyId={seed.PersonId:D}";
            var retainedUrl = $"{fixture.BaseUrl}/crm-hr/directory?partyId={seed.RetainedId:D}";

            // Archive: the lifecycle status changes through the editor; identity, relationship and affiliation stay.
            await CrmHrDirectoryJourneySupport.OpenAsync(oracle, page, personUrl);
            await Assertions.Expect(dialog).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(page.GetByTestId("crmhr-party-display-name")).ToHaveValueAsync(seed.PersonName);
            await Assertions.Expect(statusSelect).ToHaveValueAsync(PartyLifecycleStatus.Active.ToString());
            await statusSelect.SelectOptionAsync(new[] { PartyLifecycleStatus.Archived.ToString() });
            await saveButton.ClickAsync();
            await Assertions.Expect(savedToast).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await AssertPersonAsync(owner, seed, PartyLifecycleStatus.Archived);

            await CrmHrDirectoryJourneySupport.OpenAsync(oracle, page, personUrl);
            await Assertions.Expect(dialog).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(statusSelect).ToHaveValueAsync(PartyLifecycleStatus.Archived.ToString());
            await Assertions.Expect(page.GetByTestId("crmhr-directory-primary-affiliation")).ToHaveTextAsync($"{seed.ParentName} / {seed.PersonJobTitle}");
            await page.GetByTestId("crmhr-directory-tab-activity").ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-directory-activity-item")
                .GetByText($"Archived party '{seed.PersonName}'.", new() { Exact = true })).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await evidence.ScreenshotAsync(page, "01-archived-activity-1600.png");

            // Reactivate: the same identity returns to Active with the same references.
            await page.GetByTestId("crmhr-directory-tab-profile").ClickAsync();
            await Assertions.Expect(statusSelect).ToHaveValueAsync(PartyLifecycleStatus.Archived.ToString(), new() { Timeout = 30_000 });
            await statusSelect.SelectOptionAsync(new[] { PartyLifecycleStatus.Active.ToString() });
            await saveButton.ClickAsync();
            await Assertions.Expect(savedToast).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await AssertPersonAsync(owner, seed, PartyLifecycleStatus.Active);

            await CrmHrDirectoryJourneySupport.OpenAsync(oracle, page, personUrl);
            await Assertions.Expect(dialog).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(statusSelect).ToHaveValueAsync(PartyLifecycleStatus.Active.ToString());
            await page.GetByTestId("crmhr-directory-tab-activity").ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-directory-activity-item")
                .GetByText($"Reactivated party '{seed.PersonName}'.", new() { Exact = true })).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await page.GetByTestId("crmhr-directory-tab-relationships").ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-affiliation-company-name-0")).ToHaveTextAsync(seed.ParentName, new() { Timeout = 30_000 });
            await Assertions.Expect(page.GetByTestId("crmhr-relationship-party-name-0")).ToHaveTextAsync($"{seed.ParentName} ({PartyType.Organization})");
            await evidence.ScreenshotAsync(page, "02-reactivated-references-1600.png");

            // Merge: the retained party lists the seeded duplicate by its shared contact value; the merge dialog takes a
            // reason and one confirmation.
            await CrmHrDirectoryJourneySupport.OpenAsync(oracle, page, retainedUrl);
            await Assertions.Expect(dialog).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(page.GetByTestId("crmhr-party-display-name")).ToHaveValueAsync(seed.RetainedName);
            await page.GetByTestId("crmhr-directory-tab-relationships").ClickAsync();
            var mergeOpen = page.GetByTestId($"crmhr-merge-open-{seed.DuplicateId:N}");
            await Assertions.Expect(mergeOpen).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(dialog).ToContainTextAsync("Reasons: matching contact value");
            await Assertions.Expect(page.GetByTestId("crmhr-relationship-party-name-0")).ToHaveCountAsync(0);
            await mergeOpen.ClickAsync();
            var mergeDialog = page.GetByTestId("crmhr-merge-dialog");
            await Assertions.Expect(mergeDialog).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(mergeDialog).ToContainTextAsync(seed.RetainedName);
            await Assertions.Expect(mergeDialog).ToContainTextAsync(seed.DuplicateName);
            await page.GetByTestId("crmhr-merge-reason").FillAsync(mergeReason);
            await evidence.ScreenshotAsync(page, "03-merge-dialog-1600.png");
            await page.GetByTestId("crmhr-merge-confirm").ClickAsync();
            await Assertions.Expect(mergeDialog).ToHaveCountAsync(0, new() { Timeout = 30_000 });
            await Assertions.Expect(CrmHrDirectoryJourneySupport.Toast(page, $"Merged '{seed.DuplicateName}' into '{seed.RetainedName}'.")).ToBeVisibleAsync(new() { Timeout = 30_000 });

            // The owner's merge semantics: the duplicate is gone, the retained identity stays, the shared email is kept
            // once, the duplicate's own contact and its relationship now belong to the retained party.
            Assert.Null(await CrmHrDirectoryJourneySupport.FindPartyAsync(owner, seed.DuplicateId));
            var retained = await CrmHrDirectoryJourneySupport.ReadPartyAsync(owner, seed.RetainedId);
            Assert.Equal(seed.RetainedName, retained.DisplayName);
            Assert.Equal(PartyLifecycleStatus.Active, retained.LifecycleStatus);
            Assert.Equal(2, retained.ContactPoints.Count);
            var sharedEmail = Assert.Single(retained.ContactPoints, contact => contact.ContactType == PartyContactType.Email);
            Assert.Equal(seed.SharedEmail, sharedEmail.Value);
            Assert.True(sharedEmail.IsPrimary);
            var movedWebsite = Assert.Single(retained.ContactPoints, contact => contact.ContactType == PartyContactType.Website);
            Assert.Equal(seed.DuplicateWebsite, movedWebsite.Value);
            Assert.Contains($"Merged party '{seed.DuplicateName}'", retained.Notes, StringComparison.Ordinal);
            Assert.Contains(mergeReason, retained.Notes, StringComparison.Ordinal);
            var movedRelationship = Assert.Single(await ListRelationshipsAsync(owner, seed.RetainedId));
            Assert.Equal(seed.ParentId, movedRelationship.RelatedPartyId);
            Assert.Equal(PartyRelationshipKind.PartnerOf, movedRelationship.RelationshipKind);
            Assert.True(movedRelationship.IsOutgoing);
            Assert.Equal(seed.DuplicateRelationshipNotes, movedRelationship.Notes);
            var directoryAfterMerge = await CrmHrDirectoryJourneySupport.ListDirectoryAsync(owner);
            Assert.DoesNotContain(directoryAfterMerge, item => item.Id == seed.DuplicateId);
            Assert.DoesNotContain(directoryAfterMerge, item => string.Equals(item.DisplayName, seed.DuplicateName, StringComparison.Ordinal));
            Assert.Equal(seed.RetainedId, Assert.Single(directoryAfterMerge, item => string.Equals(item.DisplayName, seed.RetainedName, StringComparison.Ordinal)).Id);

            // The same circuit shows the merged state, and so does a reload of the retained record.
            await Assertions.Expect(mergeOpen).ToHaveCountAsync(0, new() { Timeout = 30_000 });
            await Assertions.Expect(page.GetByTestId("crmhr-relationship-party-name-0")).ToHaveTextAsync($"{seed.ParentName} ({PartyType.Organization})", new() { Timeout = 30_000 });
            await CrmHrDirectoryJourneySupport.OpenAsync(oracle, page, retainedUrl);
            await Assertions.Expect(dialog).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await page.GetByTestId("crmhr-directory-tab-relationships").ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-relationship-party-name-0")).ToHaveTextAsync($"{seed.ParentName} ({PartyType.Organization})", new() { Timeout = 30_000 });
            await Assertions.Expect(page.GetByTestId("crmhr-relationship-notes-0")).ToHaveValueAsync(seed.DuplicateRelationshipNotes);
            await Assertions.Expect(dialog).ToContainTextAsync("No duplicate candidates are currently detected for this party.");
            await page.GetByTestId("crmhr-directory-tab-contacts").ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-contact-value-0")).ToHaveValueAsync(seed.DuplicateWebsite, new() { Timeout = 30_000 });
            await evidence.ScreenshotAsync(page, "04-retained-after-merge-1600.png");
            await CrmHrDirectoryJourneySupport.AssertConstrainedLayoutAsync(
                page,
                evidence,
                "05-retained-record-1100.png",
                dialog,
                saveButton,
                page.GetByTestId("crmhr-directory-record-close"));

            // The merged identity no longer opens as a record of its own, and the catalog no longer lists it.
            await CrmHrDirectoryJourneySupport.OpenAsync(oracle, page, $"{fixture.BaseUrl}/crm-hr/directory?partyId={seed.DuplicateId:D}");
            await CrmHrDirectoryJourneySupport.WaitForCatalogAsync(page, "crmhr-directory");
            await Assertions.Expect(dialog).ToHaveCountAsync(0);
            await CrmHrDirectoryJourneySupport.SearchCatalogExpectingNoneAsync(page, "crmhr-directory", "crmhr-directory-item", seed.DuplicateName);

            // CSV export: the refreshed export carries the documented header and the seeded parties.
            var directoryBeforeImport = await CrmHrDirectoryJourneySupport.ListDirectoryAsync(owner);
            await page.GetByTestId("crmhr-directory-import-export-button").ClickAsync();
            var csvDialog = page.GetByTestId("crmhr-directory-import-export-dialog");
            await Assertions.Expect(csvDialog).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(page.GetByTestId("crmhr-import-apply-button")).ToBeDisabledAsync();
            await page.GetByTestId("crmhr-export-refresh").ClickAsync();
            await Assertions.Expect(csvDialog).ToContainTextAsync("Directory export refreshed.", new() { Timeout = 30_000 });
            var exportArea = page.GetByTestId("crmhr-export-textarea");
            await Assertions.Expect(exportArea).ToHaveValueAsync(new Regex("^" + Regex.Escape(ExportHeader) + @"\r?\n"));
            await Assertions.Expect(exportArea).ToHaveValueAsync(new Regex(Regex.Escape(seed.RetainedName)));
            await Assertions.Expect(exportArea).ToHaveValueAsync(new Regex(Regex.Escape(seed.PersonName)));
            await Assertions.Expect(exportArea).ToHaveValueAsync(new Regex(Regex.Escape(seed.PersonAddress)));
            await Assertions.Expect(exportArea).ToHaveValueAsync(new Regex(Regex.Escape(seed.DuplicateWebsite)));
            await Assertions.Expect(exportArea).Not.ToHaveValueAsync(new Regex(Regex.Escape(seed.DuplicateName)));

            // CSV import: one new valid row and one row the duplicate analysis blocks; the preview says which is which
            // and one click applies only the ready row.
            var csv = string.Join(
                "\n",
                ExportHeader,
                $"{importedName},Person,Candidate,IMP-{suffix},,,Imported through the stewardship tools {suffix},imported-{suffix},EU,CZ,Europe/Prague,False,Candidate|Candidate|True,Email|Primary email|{importedEmail}|True|True,Work|{importedAddress}||Brno|JM|60200|CZ|True",
                $"{seed.RetainedName},Organization,Active,,,,Blocked duplicate row {suffix},,,,,False,Customer|Customer|True,,");
            await page.GetByTestId("crmhr-import-textarea").FillAsync(csv);
            await page.GetByTestId("crmhr-import-preview-button").ClickAsync();
            await Assertions.Expect(csvDialog).ToContainTextAsync("Preview prepared for 2 row(s).", new() { Timeout = 30_000 });
            await Assertions.Expect(csvDialog).ToContainTextAsync("1 ready row(s), 1 blocking row(s).");
            var previewRows = page.GetByTestId("crmhr-import-row");
            await Assertions.Expect(previewRows).ToHaveCountAsync(2);
            await Assertions.Expect(previewRows.Nth(0)).ToContainTextAsync($"Row 2: {importedName}");
            await Assertions.Expect(previewRows.Nth(0)).ToContainTextAsync("Ready");
            await Assertions.Expect(previewRows.Nth(1)).ToContainTextAsync($"Row 3: {seed.RetainedName}");
            await Assertions.Expect(previewRows.Nth(1)).ToContainTextAsync("Blocked");
            await Assertions.Expect(previewRows.Nth(1)).ToContainTextAsync($"Duplicates: {seed.RetainedName}");
            await evidence.ScreenshotAsync(page, "06-csv-preview-1600.png");
            await Assertions.Expect(page.GetByTestId("crmhr-import-apply-button")).ToBeEnabledAsync();
            await page.GetByTestId("crmhr-import-apply-button").ClickAsync();
            await Assertions.Expect(csvDialog).ToHaveCountAsync(0, new() { Timeout = 30_000 });

            var directoryAfterImport = await CrmHrDirectoryJourneySupport.ListDirectoryAsync(owner);
            var importedItem = Assert.Single(directoryAfterImport, item => string.Equals(item.DisplayName, importedName, StringComparison.Ordinal));
            Assert.Single(directoryAfterImport, item => string.Equals(item.DisplayName, seed.RetainedName, StringComparison.Ordinal));
            Assert.DoesNotContain(directoryAfterImport, item => string.Equals(item.Summary, $"Blocked duplicate row {suffix}", StringComparison.Ordinal));
            var missingAfterImport = directoryBeforeImport.Select(item => item.Id).Except(directoryAfterImport.Select(item => item.Id)).ToArray();
            Assert.True(missingAfterImport.Length == 0, "The import removed parties: " + string.Join(", ", missingAfterImport));
            foreach (var seededId in new[] { seed.ParentId, seed.PersonId, seed.RetainedId })
            {
                Assert.Contains(directoryAfterImport, item => item.Id == seededId);
            }

            var imported = await CrmHrDirectoryJourneySupport.ReadPartyAsync(owner, importedItem.Id);
            Assert.Equal(PartyType.Person, imported.PartyType);
            Assert.Equal(PartyLifecycleStatus.Candidate, imported.LifecycleStatus);
            Assert.Equal($"IMP-{suffix}", imported.ExternalCode);
            Assert.Equal($"Imported through the stewardship tools {suffix}", imported.Summary);
            Assert.Equal(new[] { $"imported-{suffix}" }, imported.Tags);
            Assert.Equal("CZ", imported.CountryCode);
            Assert.False(imported.IsSensitive);
            Assert.Equal(PartyRoleKind.Candidate, Assert.Single(imported.Roles).RoleKind);
            var importedContact = Assert.Single(imported.ContactPoints);
            Assert.Equal(importedEmail, importedContact.Value);
            Assert.True(importedContact.IsPrimary);
            Assert.Equal(importedAddress, Assert.Single(imported.Addresses).Line1);

            // The catalog of the same circuit lists the imported party, and its record opens by the owner's identity.
            await CrmHrDirectoryJourneySupport.WaitForCatalogAsync(page, "crmhr-directory");
            await CrmHrDirectoryJourneySupport.SearchCatalogForAsync(page, "crmhr-directory", "crmhr-directory-item", importedName);
            await CrmHrDirectoryJourneySupport.OpenAsync(oracle, page, $"{fixture.BaseUrl}/crm-hr/directory?partyId={importedItem.Id:D}");
            await Assertions.Expect(dialog).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(page.GetByTestId("crmhr-party-display-name")).ToHaveValueAsync(importedName);
            await Assertions.Expect(page.GetByTestId("crmhr-party-email")).ToHaveValueAsync(importedEmail);
            await Assertions.Expect(statusSelect).ToHaveValueAsync(PartyLifecycleStatus.Candidate.ToString());
            await evidence.ScreenshotAsync(page, "07-imported-record-1600.png");

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

    // The person after a lifecycle save: the same identity in the expected status, still related to and affiliated with
    // the seeded parent organization.
    private static async Task AssertPersonAsync(ServiceProvider owner, SeededLifecycle seed, PartyLifecycleStatus expectedStatus)
    {
        var person = await CrmHrDirectoryJourneySupport.ReadPartyAsync(owner, seed.PersonId);
        Assert.Equal(seed.PersonId, person.Id);
        Assert.Equal(seed.PersonName, person.DisplayName);
        Assert.Equal(expectedStatus, person.LifecycleStatus);
        Assert.Equal(seed.PersonEmail, Assert.Single(person.ContactPoints).Value);
        Assert.Equal(PartyRoleKind.Employee, Assert.Single(person.Roles).RoleKind);
        Assert.Equal(seed.PersonAddress, Assert.Single(person.Addresses).Line1);

        var relationship = Assert.Single(await ListRelationshipsAsync(owner, seed.PersonId));
        Assert.Equal(seed.ParentId, relationship.RelatedPartyId);
        Assert.Equal(PartyRelationshipKind.MemberOf, relationship.RelationshipKind);
        Assert.Equal(seed.PersonRelationshipNotes, relationship.Notes);

        await using var scope = owner.CreateAsyncScope();
        var affiliation = Assert.Single(await scope.ServiceProvider.GetRequiredService<IPartyOrganizationAffiliationService>().ListAsync(seed.PersonId));
        Assert.Equal(seed.ParentId, affiliation.OrganizationPartyId);
        Assert.Equal(seed.PersonJobTitle, affiliation.JobTitle);
        Assert.True(affiliation.IsPrimary);
    }

    private static async Task<IReadOnlyList<PartyRelationshipListItemModel>> ListRelationshipsAsync(ServiceProvider owner, Guid partyId)
    {
        await using var scope = owner.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>().ListRelationshipsAsync(partyId);
    }

    private static async Task<SeededLifecycle> SeedAsync(ServiceProvider owner, string suffix)
    {
        await using var scope = owner.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var managementService = scope.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>();
        var affiliationService = scope.ServiceProvider.GetRequiredService<IPartyOrganizationAffiliationService>();

        var parentName = $"Lifecycle Parent {suffix}";
        var personName = $"Lifecycle Person {suffix}";
        var personEmail = $"lifecycle.person.{suffix}@example.test";
        var personJobTitle = $"Coordinator {suffix}";
        var personAddress = $"1 Lifecycle Way {suffix}";
        var personRelationshipNotes = $"Member since the journey {suffix}";
        var retainedName = $"Merge Retained {suffix}";
        var duplicateName = $"Merge Duplicate {suffix}";
        var sharedEmail = $"shared.account.{suffix}@example.test";
        var duplicateWebsite = $"https://duplicate-{suffix}.example.test";
        var duplicateRelationshipNotes = $"Partner link of the duplicate {suffix}";

        var parentId = await CrmHrDirectoryJourneySupport.CreatePartyAsync(partyDirectoryService, parentName, PartyType.Organization, PartyRoleKind.Partner, $"lifecycle.parent.{suffix}@example.test");
        var personId = await CrmHrDirectoryJourneySupport.CreatePartyAsync(
            partyDirectoryService,
            personName,
            PartyType.Person,
            PartyRoleKind.Employee,
            personEmail,
            configure: model => model.Addresses.Add(new PartyAddressEditorModel
            {
                AddressType = "Work",
                Line1 = personAddress,
                City = "Brno",
                PostalCode = "60200",
                CountryCode = "CZ",
                IsPrimary = true
            }));
        var retainedId = await CrmHrDirectoryJourneySupport.CreatePartyAsync(partyDirectoryService, retainedName, PartyType.Organization, PartyRoleKind.Customer, sharedEmail);
        var duplicateId = await CrmHrDirectoryJourneySupport.CreatePartyAsync(
            partyDirectoryService,
            duplicateName,
            PartyType.Organization,
            PartyRoleKind.Customer,
            sharedEmail,
            configure: model => model.ContactPoints.Add(new PartyContactPointEditorModel
            {
                ContactType = PartyContactType.Website,
                Label = "Website",
                Value = duplicateWebsite,
                NormalizedValue = duplicateWebsite.ToLowerInvariant(),
                IsPrimary = false,
                IsPublic = true
            }));

        var personRelationship = await managementService.SaveRelationshipsAsync(
            personId,
            [
                new PartyRelationshipEditorModel
                {
                    RelatedPartyId = parentId,
                    RelationshipKind = PartyRelationshipKind.MemberOf,
                    IsOutgoing = true,
                    Notes = personRelationshipNotes
                }
            ],
            CrmHrDirectoryJourneySupport.Actor);
        Assert.True(personRelationship.IsSuccess, string.Join(" ", personRelationship.Errors.Select(error => error.Message)));

        var duplicateRelationship = await managementService.SaveRelationshipsAsync(
            duplicateId,
            [
                new PartyRelationshipEditorModel
                {
                    RelatedPartyId = parentId,
                    RelationshipKind = PartyRelationshipKind.PartnerOf,
                    IsOutgoing = true,
                    Notes = duplicateRelationshipNotes
                }
            ],
            CrmHrDirectoryJourneySupport.Actor);
        Assert.True(duplicateRelationship.IsSuccess, string.Join(" ", duplicateRelationship.Errors.Select(error => error.Message)));

        var affiliation = await affiliationService.UpsertAsync(
            new PartyOrganizationAffiliationEditorModel
            {
                PersonPartyId = personId,
                OrganizationPartyId = parentId,
                AffiliationKind = PartyOrganizationAffiliationKind.ExternalContact,
                IsPrimary = true,
                JobTitle = personJobTitle
            },
            CrmHrDirectoryJourneySupport.Actor);
        Assert.True(affiliation.IsSuccess, string.Join(" ", affiliation.Errors.Select(error => error.Message)));

        // The product's duplicate rule must see the seeded pair before the journey relies on it.
        var candidates = await managementService.FindPotentialDuplicatesAsync(retainedId);
        var candidate = Assert.Single(candidates, item => item.Id == duplicateId);
        Assert.Contains("matching contact value", candidate.MatchReasons);

        return new SeededLifecycle(
            parentId,
            parentName,
            personId,
            personName,
            personEmail,
            personJobTitle,
            personAddress,
            personRelationshipNotes,
            retainedId,
            retainedName,
            duplicateId,
            duplicateName,
            sharedEmail,
            duplicateWebsite,
            duplicateRelationshipNotes);
    }

    private sealed record SeededLifecycle(
        Guid ParentId,
        string ParentName,
        Guid PersonId,
        string PersonName,
        string PersonEmail,
        string PersonJobTitle,
        string PersonAddress,
        string PersonRelationshipNotes,
        Guid RetainedId,
        string RetainedName,
        Guid DuplicateId,
        string DuplicateName,
        string SharedEmail,
        string DuplicateWebsite,
        string DuplicateRelationshipNotes);
}
