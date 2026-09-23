using CanDoItAll.Modules.CrmHr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

// Contacts, addresses, roles, tags, organization affiliations and party relationships of a seeded person through the
// real directory editors and pickers, and the privacy boundary of a confidential note: visible in the trusted record
// dialog after a reload, absent from the redacted directory catalog, from its search, and from the Home overview.
[Collection(PlaywrightCollection.Name)]
public sealed class CrmHrDirectoryRelationshipsPrivacyJourneyTests
{
    private readonly PlaywrightAppFixture fixture;

    public CrmHrDirectoryRelationshipsPrivacyJourneyTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Person_gains_contact_address_role_tag_and_affiliation_and_a_confidential_note_stays_inside_the_trusted_dialog()
    {
        var evidence = CrmHrJourneyEvidence.Create("crm-hr-journey-directory-relationships-privacy");
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var secondaryEmail = $"billing.{suffix}@example.test";
        var contactLabel = $"Billing {suffix}";
        var addressLine = $"12 Journey Street {suffix}";
        var roleTitle = $"Hiring panel {suffix}";
        var tag = $"steward-{suffix}";
        var jobTitle = $"Liaison {suffix}";
        var relationshipNotes = $"Represents the organization {suffix}";
        var confidentialMarker = $"Confidential marker {suffix} salary review";

        await using var owner = await CrmHrDirectoryJourneySupport.BuildOwnerProviderAsync(fixture, "playwright-journey-directory-relationships");
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
            var savedToast = CrmHrDirectoryJourneySupport.Toast(page, "Party saved.");
            var personUrl = $"{fixture.BaseUrl}/crm-hr/directory?partyId={seed.PersonId:D}";

            await CrmHrDirectoryJourneySupport.OpenAsync(oracle, page, personUrl);
            await Assertions.Expect(dialog).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(page.GetByTestId("crmhr-party-display-name")).ToHaveValueAsync(seed.PersonName);
            await Assertions.Expect(page.GetByTestId("crmhr-directory-primary-affiliation")).ToHaveTextAsync("No current organization affiliation");

            // Profile: an additional role and a tag.
            await page.GetByTestId("crmhr-party-role-add").ClickAsync();
            await Assertions.Expect(RoleField(page, "kind")).ToHaveCountAsync(1, new() { Timeout = 30_000 });
            await RoleField(page, "kind").SelectOptionAsync(new[] { PartyRoleKind.Recruiter.ToString() });
            await RoleField(page, "title").FillAsync(roleTitle);
            var tagInput = page.GetByTestId("crmhr-party-tags");
            await tagInput.FillAsync(tag);
            await tagInput.PressAsync("Enter");
            await Assertions.Expect(page.GetByTestId("crmhr-party-tags-editor").GetByText(tag, new() { Exact = true })).ToBeVisibleAsync();

            // Contacts: the contact-method wizard (type, details with its own validation, finish) and an address row.
            await page.GetByTestId("crmhr-directory-tab-contacts").ClickAsync();
            await page.GetByTestId("crmhr-contact-add").ClickAsync();
            var wizard = page.GetByTestId("crmhr-contact-wizard");
            await Assertions.Expect(wizard).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(page.GetByTestId("crmhr-contact-wizard-next")).ToBeDisabledAsync();
            await page.GetByTestId("crmhr-contact-wizard-type-email").ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-contact-wizard-type-email")).ToHaveAttributeAsync("aria-pressed", "true");
            await page.GetByTestId("crmhr-contact-wizard-next").ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-contact-wizard-value")).ToBeVisibleAsync();
            await page.GetByTestId("crmhr-contact-wizard-finish").ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-contact-wizard-validation")).ToHaveTextAsync("Enter a contact value.");
            await page.GetByTestId("crmhr-contact-wizard-value").FillAsync(secondaryEmail);
            await page.GetByTestId("crmhr-contact-wizard-label").FillAsync(contactLabel);
            await evidence.ScreenshotAsync(page, "01-contact-wizard-1600.png");
            await page.GetByTestId("crmhr-contact-wizard-finish").ClickAsync();
            await Assertions.Expect(wizard).ToHaveCountAsync(0, new() { Timeout = 30_000 });
            await Assertions.Expect(page.GetByTestId("crmhr-contact-value-0")).ToHaveValueAsync(secondaryEmail);
            await Assertions.Expect(page.GetByTestId("crmhr-contact-type-0")).ToHaveValueAsync(PartyContactType.Email.ToString());

            await page.GetByTestId("crmhr-address-add").ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-address-type-0")).ToHaveValueAsync("Work");
            await page.GetByTestId("crmhr-address-line1-0").FillAsync(addressLine);
            await page.GetByTestId("crmhr-address-city-0").FillAsync("Brno");
            await page.GetByTestId("crmhr-address-region-0").FillAsync("JM");
            await page.GetByTestId("crmhr-address-postal-0").FillAsync("60200");
            await page.GetByTestId("crmhr-address-country-0").FillAsync("CZ");
            await page.GetByTestId("crmhr-address-primary-0").CheckAsync();

            // Relations: an affiliation to the seeded organization through the affiliation editor and its picker.
            await page.GetByTestId("crmhr-directory-tab-relationships").ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-affiliations-editor")).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(page.GetByTestId("crmhr-affiliations-empty")).ToBeVisibleAsync();
            await page.GetByTestId("crmhr-affiliation-add").ClickAsync();
            var picker = page.GetByTestId("crmhr-affiliation-picker");
            await Assertions.Expect(picker).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await CrmHrDirectoryJourneySupport.WaitForCatalogAsync(page, "crmhr-affiliation-picker-browser");
            await page.GetByTestId("crmhr-affiliation-picker-browser-search").FillAsync(seed.OrganizationName);
            var organizationOption = picker.GetByTestId($"crmhr-party-option-{seed.OrganizationId:N}");
            await Assertions.Expect(organizationOption).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(organizationOption).ToHaveAttributeAsync("aria-label", $"Select {seed.OrganizationName}");
            await organizationOption.ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-affiliation-picker-confirm")).ToBeEnabledAsync();
            await evidence.ScreenshotAsync(page, "02-affiliation-picker-1600.png");
            await page.GetByTestId("crmhr-affiliation-picker-confirm").ClickAsync();
            await Assertions.Expect(picker).ToHaveCountAsync(0, new() { Timeout = 30_000 });
            await Assertions.Expect(page.GetByTestId("crmhr-affiliation-company-name-0")).ToHaveTextAsync(seed.OrganizationName);
            await page.GetByTestId("crmhr-affiliation-title-0").FillAsync(jobTitle);

            // Relations: a party relationship to the same organization through the relationship editor and its picker.
            await page.GetByTestId("crmhr-relationship-add").ClickAsync();
            var relationshipPicker = page.GetByTestId("crmhr-relationship-picker");
            await Assertions.Expect(relationshipPicker).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await CrmHrDirectoryJourneySupport.WaitForCatalogAsync(page, "crmhr-relationship-picker-browser");
            await page.GetByTestId("crmhr-relationship-picker-browser-search").FillAsync(seed.OrganizationName);
            var relatedOption = relationshipPicker.GetByTestId($"crmhr-party-option-{seed.OrganizationId:N}");
            await Assertions.Expect(relatedOption).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await relatedOption.ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-relationship-picker-confirm")).ToBeEnabledAsync();
            await page.GetByTestId("crmhr-relationship-picker-confirm").ClickAsync();
            await Assertions.Expect(relationshipPicker).ToHaveCountAsync(0, new() { Timeout = 30_000 });
            await Assertions.Expect(page.GetByTestId("crmhr-relationship-party-name-0")).ToHaveTextAsync($"{seed.OrganizationName} ({PartyType.Organization})");
            await page.GetByTestId("crmhr-relationship-kind-0").SelectOptionAsync(new[] { PartyRelationshipKind.Represents.ToString() });
            await page.GetByTestId("crmhr-relationship-notes-0").FillAsync(relationshipNotes);

            // One click saves the party, its affiliations and its relationships; the owner holds every typed value.
            await saveButton.ClickAsync();
            await Assertions.Expect(savedToast).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(page.GetByTestId("crmhr-directory-primary-affiliation")).ToHaveTextAsync($"{seed.OrganizationName} / {jobTitle}");

            var saved = await CrmHrDirectoryJourneySupport.ReadPartyAsync(owner, seed.PersonId);
            Assert.Equal(seed.PersonName, saved.DisplayName);
            Assert.Equal(seed.PersonSummary, saved.Summary);
            Assert.Equal(seed.OrdinaryNotes, saved.Notes);
            Assert.False(saved.IsSensitive);
            Assert.Equal(new[] { tag }, saved.Tags);
            Assert.Equal(2, saved.Roles.Count);
            var primaryRole = Assert.Single(saved.Roles, role => role.IsPrimary);
            Assert.Equal(PartyRoleKind.Employee, primaryRole.RoleKind);
            var addedRole = Assert.Single(saved.Roles, role => !role.IsPrimary);
            Assert.Equal(PartyRoleKind.Recruiter, addedRole.RoleKind);
            Assert.Equal(roleTitle, addedRole.Title);
            Assert.Equal(2, saved.ContactPoints.Count);
            var primaryContact = Assert.Single(saved.ContactPoints, contact => contact.IsPrimary);
            Assert.Equal(seed.PersonEmail, primaryContact.Value);
            var addedContact = Assert.Single(saved.ContactPoints, contact => !contact.IsPrimary);
            Assert.Equal(PartyContactType.Email, addedContact.ContactType);
            Assert.Equal(secondaryEmail, addedContact.Value);
            Assert.Equal(contactLabel, addedContact.Label);
            Assert.True(addedContact.IsPublic);
            var address = Assert.Single(saved.Addresses);
            Assert.Equal("Work", address.AddressType);
            Assert.Equal(addressLine, address.Line1);
            Assert.Equal("Brno", address.City);
            Assert.Equal("JM", address.Region);
            Assert.Equal("60200", address.PostalCode);
            Assert.Equal("CZ", address.CountryCode);
            Assert.True(address.IsPrimary);
            var affiliation = Assert.Single(await ListAffiliationsAsync(owner, seed.PersonId));
            Assert.Equal(seed.OrganizationId, affiliation.OrganizationPartyId);
            Assert.Equal(seed.OrganizationName, affiliation.OrganizationDisplayName);
            Assert.Equal(jobTitle, affiliation.JobTitle);
            Assert.Equal(PartyOrganizationAffiliationKind.ExternalContact, affiliation.AffiliationKind);
            Assert.True(affiliation.IsPrimary);
            var relationship = Assert.Single(await ListRelationshipsAsync(owner, seed.PersonId));
            Assert.Equal(seed.OrganizationId, relationship.RelatedPartyId);
            Assert.Equal(PartyRelationshipKind.Represents, relationship.RelationshipKind);
            Assert.True(relationship.IsOutgoing);
            Assert.Equal(relationshipNotes, relationship.Notes);
            await evidence.ScreenshotAsync(page, "03-relations-saved-1600.png");

            // Privacy: the record becomes sensitive and gains a real confidential note, distinct from the summary and
            // the operational notes. The previous receipt toast is gone before the second save is awaited.
            await Assertions.Expect(savedToast).ToHaveCountAsync(0, new() { Timeout = 30_000 });
            await page.GetByTestId("crmhr-directory-tab-handling").ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-confidential-note-add")).ToBeDisabledAsync();
            await page.GetByTestId("crmhr-party-sensitive").CheckAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-confidential-note-add")).ToBeEnabledAsync();
            await page.GetByTestId("crmhr-confidential-note-add").ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-confidential-note-item")).ToHaveCountAsync(1, new() { Timeout = 30_000 });
            await ConfidentialNoteCategory(page).SelectOptionAsync(new[] { PartyConfidentialNoteCategories.Compensation });
            await page.GetByTestId("crmhr-confidential-note-text-0").FillAsync(confidentialMarker);
            await saveButton.ClickAsync();
            await Assertions.Expect(savedToast).ToBeVisibleAsync(new() { Timeout = 30_000 });

            var sensitive = await CrmHrDirectoryJourneySupport.ReadPartyAsync(owner, seed.PersonId);
            Assert.True(sensitive.IsSensitive);
            var confidentialNote = Assert.Single(sensitive.ConfidentialNotes);
            Assert.Equal(confidentialMarker, confidentialNote.NoteText);
            Assert.Equal(PartyConfidentialNoteCategories.Compensation, confidentialNote.Category);
            Assert.Equal(seed.PersonSummary, sensitive.Summary);
            Assert.Equal(seed.OrdinaryNotes, sensitive.Notes);
            Assert.Equal(2, sensitive.ContactPoints.Count);
            Assert.Single(sensitive.Addresses);
            Assert.Equal(seed.OrganizationId, Assert.Single(await ListAffiliationsAsync(owner, seed.PersonId)).OrganizationPartyId);
            Assert.Equal(relationshipNotes, Assert.Single(await ListRelationshipsAsync(owner, seed.PersonId)).Notes);

            // Reload: the trusted record dialog shows the confidential note and the saved relations.
            await CrmHrDirectoryJourneySupport.OpenAsync(oracle, page, personUrl);
            await Assertions.Expect(dialog).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(page.GetByTestId("crmhr-directory-primary-affiliation")).ToHaveTextAsync($"{seed.OrganizationName} / {jobTitle}");
            await Assertions.Expect(RoleField(page, "title")).ToHaveCountAsync(1);
            await Assertions.Expect(RoleField(page, "title")).ToHaveValueAsync(roleTitle);
            await Assertions.Expect(RoleField(page, "kind")).ToHaveValueAsync(PartyRoleKind.Recruiter.ToString());
            await page.GetByTestId("crmhr-directory-tab-handling").ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-directory-sensitive-callout")).ToContainTextAsync("Hidden from global search", new() { Timeout = 30_000 });
            await Assertions.Expect(page.GetByTestId("crmhr-party-sensitive")).ToBeCheckedAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-confidential-note-item")).ToHaveCountAsync(1);
            await Assertions.Expect(page.GetByTestId("crmhr-confidential-note-text-0")).ToHaveValueAsync(confidentialMarker);
            await Assertions.Expect(ConfidentialNoteCategory(page)).ToHaveValueAsync(PartyConfidentialNoteCategories.Compensation);
            await Assertions.Expect(page.GetByTestId("crmhr-party-notes")).ToHaveValueAsync(seed.OrdinaryNotes);
            await page.GetByTestId("crmhr-confidential-note-text-0").ScrollIntoViewIfNeededAsync();
            await evidence.ScreenshotAsync(page, "04-trusted-dialog-confidential-note-1600.png");
            await CrmHrDirectoryJourneySupport.AssertConstrainedLayoutAsync(
                page,
                evidence,
                "05-trusted-dialog-1100.png",
                dialog,
                saveButton,
                page.GetByTestId("crmhr-directory-record-close"));
            await page.GetByTestId("crmhr-directory-tab-contacts").ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-contact-value-0")).ToHaveValueAsync(secondaryEmail, new() { Timeout = 30_000 });
            await Assertions.Expect(page.GetByTestId("crmhr-address-line1-0")).ToHaveValueAsync(addressLine);
            await page.GetByTestId("crmhr-directory-tab-relationships").ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-affiliation-company-name-0")).ToHaveTextAsync(seed.OrganizationName, new() { Timeout = 30_000 });
            await Assertions.Expect(page.GetByTestId("crmhr-affiliation-title-0")).ToHaveValueAsync(jobTitle);
            await Assertions.Expect(page.GetByTestId("crmhr-relationship-party-name-0")).ToHaveTextAsync($"{seed.OrganizationName} ({PartyType.Organization})");
            await Assertions.Expect(page.GetByTestId("crmhr-relationship-kind-0")).ToHaveValueAsync(PartyRelationshipKind.Represents.ToString());
            await Assertions.Expect(page.GetByTestId("crmhr-relationship-notes-0")).ToHaveValueAsync(relationshipNotes);

            // The redacted catalog: the sensitive card carries the name only, and neither the confidential marker, the
            // summary nor the tag finds it. The non-sensitive organization proves the same search does match summaries.
            await page.GetByTestId("crmhr-directory-record-close").ClickAsync();
            await Assertions.Expect(dialog).ToHaveCountAsync(0, new() { Timeout = 30_000 });
            await CrmHrDirectoryJourneySupport.WaitForCatalogAsync(page, "crmhr-directory");
            await CrmHrDirectoryJourneySupport.SearchCatalogForAsync(page, "crmhr-directory", "crmhr-directory-item", seed.PersonName);
            var sensitiveCard = page.GetByTestId("crmhr-directory-item-shell");
            await Assertions.Expect(sensitiveCard).ToHaveCountAsync(1);
            await Assertions.Expect(sensitiveCard).ToContainTextAsync(seed.PersonName);
            await Assertions.Expect(sensitiveCard).Not.ToContainTextAsync(seed.PersonSummary);
            await Assertions.Expect(sensitiveCard).Not.ToContainTextAsync(tag);
            await Assertions.Expect(sensitiveCard).Not.ToContainTextAsync(confidentialMarker);
            Assert.DoesNotContain(confidentialMarker, await page.ContentAsync(), StringComparison.Ordinal);
            await evidence.ScreenshotAsync(page, "06-redacted-catalog-card-1600.png");

            await CrmHrDirectoryJourneySupport.SearchCatalogExpectingNoneAsync(page, "crmhr-directory", "crmhr-directory-item", confidentialMarker);
            await CrmHrDirectoryJourneySupport.SearchCatalogExpectingNoneAsync(page, "crmhr-directory", "crmhr-directory-item", seed.PersonSummary);
            await CrmHrDirectoryJourneySupport.SearchCatalogForAsync(page, "crmhr-directory", "crmhr-directory-item", seed.OrganizationName);
            await page.GetByTestId("crmhr-directory-search").FillAsync(seed.OrganizationSummary);
            await Assertions.Expect(CrmHrDirectoryJourneySupport.CatalogCard(page, "crmhr-directory", "crmhr-directory-item", seed.OrganizationName)).ToBeVisibleAsync(new() { Timeout = 30_000 });
            await Assertions.Expect(page.GetByTestId("crmhr-directory").GetByTestId("crmhr-directory-item")).ToHaveCountAsync(1);

            // Home: the sensitive party is listed by name, and neither the confidential note, the operational notes nor
            // (inside the sensitive card) the summary reaches the document. The prerendered document is already ready and
            // the interactive host reads again, so the overview is asserted once it is interactive and ready.
            await CrmHrDirectoryJourneySupport.OpenAsync(oracle, page, $"{fixture.BaseUrl}/crm-hr");
            var home = page.GetByTestId("crmhr-home");
            await Assertions.Expect(home).ToHaveAttributeAsync("data-interactive", "true", new() { Timeout = 30_000 });
            await Assertions.Expect(home).ToHaveAttributeAsync("data-phase", "ready", new() { Timeout = 30_000 });
            var sensitiveHomeCard = page.GetByTestId("crmhr-home-sensitive-card");
            await Assertions.Expect(sensitiveHomeCard).ToContainTextAsync(seed.PersonName);
            await Assertions.Expect(sensitiveHomeCard).Not.ToContainTextAsync(seed.PersonSummary);
            var homeDocument = await page.ContentAsync();
            Assert.DoesNotContain(confidentialMarker, homeDocument, StringComparison.Ordinal);
            Assert.DoesNotContain(seed.OrdinaryNotes, homeDocument, StringComparison.Ordinal);
            await evidence.ScreenshotAsync(page, "07-home-sensitive-card-1600.png");

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

    // The journey has one additional role and one confidential note: row 0 of each editor.
    private static ILocator RoleField(IPage page, string field)
        => page.GetByTestId($"crmhr-party-extra-role-{field}-0");

    private static ILocator ConfidentialNoteCategory(IPage page)
        => page.GetByTestId("crmhr-confidential-note-item").GetByTestId("crmhr-confidential-note-category-0");

    private static async Task<IReadOnlyList<PartyOrganizationAffiliationListItemModel>> ListAffiliationsAsync(ServiceProvider owner, Guid personId)
    {
        await using var scope = owner.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IPartyOrganizationAffiliationService>().ListAsync(personId);
    }

    private static async Task<IReadOnlyList<PartyRelationshipListItemModel>> ListRelationshipsAsync(ServiceProvider owner, Guid partyId)
    {
        await using var scope = owner.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<PartyDirectoryManagementService>().ListRelationshipsAsync(partyId);
    }

    private static async Task<SeededRelations> SeedAsync(ServiceProvider owner, string suffix)
    {
        await using var scope = owner.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();

        var personName = $"Relations Person {suffix}";
        var personEmail = $"relations.person.{suffix}@example.test";
        var personSummary = $"Operational summary {suffix} of the relations person";
        var ordinaryNotes = $"Ordinary notes {suffix} stay in the directory record";
        var organizationName = $"Relations Org {suffix}";
        var organizationSummary = $"Organization summary {suffix} stays searchable";

        var personId = await CrmHrDirectoryJourneySupport.CreatePartyAsync(
            partyDirectoryService,
            personName,
            PartyType.Person,
            PartyRoleKind.Employee,
            personEmail,
            personSummary,
            model => model.Notes = ordinaryNotes);
        var organizationId = await CrmHrDirectoryJourneySupport.CreatePartyAsync(
            partyDirectoryService,
            organizationName,
            PartyType.Organization,
            PartyRoleKind.Customer,
            $"relations.org.{suffix}@example.test",
            organizationSummary);

        return new SeededRelations(personId, personName, personEmail, personSummary, ordinaryNotes, organizationId, organizationName, organizationSummary);
    }

    private sealed record SeededRelations(
        Guid PersonId,
        string PersonName,
        string PersonEmail,
        string PersonSummary,
        string OrdinaryNotes,
        Guid OrganizationId,
        string OrganizationName,
        string OrganizationSummary);
}
