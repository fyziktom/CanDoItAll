using CanDoItAll.Modules.CrmHr;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Collection(PlaywrightCollection.Name)]
public sealed class CrmHrDirectoryFlowTests
{
    private readonly PlaywrightAppFixture fixture;

    public CrmHrDirectoryFlowTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Quarantined")]
    public async Task Directory_supports_relationships_import_export_and_duplicate_merge()
    {
        var evidenceDirectory = @"C:\repositories\CanDoItAll\evidence\crm-hr\b03";
        Directory.CreateDirectory(evidenceDirectory);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1600,
                Height = 1000
            }
        });
        var page = await context.NewPageAsync();
        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var parentName = $"B03 Parent {suffix}";
        var retainedName = $"B03 Retained {suffix}";
        var duplicateName = $"B03 Duplicate {suffix}";
        var importedName = $"B03 Imported {suffix}";
        var sharedEmail = $"shared.{suffix}@example.test";

        await page.GotoAsync($"{fixture.BaseUrl}/crm-hr/directory");
        await DismissStartupModalIfPresentAsync(page);
        await page.GetByTestId("crmhr-party-save-button").WaitForAsync();

        await FillBasicPartyAsync(page, parentName, $"parent.{suffix}@example.test", "+1 206 555 0100", PartyRoleKind.Partner, "PARENT");
        await SavePartyAsync(page);

        await OpenNewPartyAsync(page);
        await FillBasicPartyAsync(page, retainedName, sharedEmail, "+1 206 555 0101", PartyRoleKind.Customer, "RETAINED");
        await SavePartyAsync(page);

        await page.GetByTestId("crmhr-directory-tab-contacts").ClickAsync();
        await page.GetByTestId("crmhr-contact-add").ClickAsync();
        var contactType = page.Locator("[data-testid^='crmhr-contact-type-']").First;
        await RequireLocatorAsync(page, contactType, Path.Combine(evidenceDirectory, "crm-hr-directory-b03-contact-missing.html"));
        await contactType.SelectOptionAsync(new[] { PartyContactType.Website.ToString() });
        await page.Locator("[data-testid^='crmhr-contact-label-']").First.FillAsync("Website");
        await page.Locator("[data-testid^='crmhr-contact-value-']").First.FillAsync($"https://{suffix}.example.test");
        await page.GetByTestId("crmhr-address-add").ClickAsync();
        await page.Locator("[data-testid^='crmhr-address-type-']").First.FillAsync("HQ");
        await page.Locator("[data-testid^='crmhr-address-line1-']").First.FillAsync("100 Market Street");
        await page.Locator("[data-testid^='crmhr-address-city-']").First.FillAsync("Seattle");
        await page.Locator("[data-testid^='crmhr-address-region-']").First.FillAsync("WA");
        await page.Locator("[data-testid^='crmhr-address-postal-']").First.FillAsync("98101");
        await page.Locator("[data-testid^='crmhr-address-country-']").First.FillAsync("US");
        await page.Locator("[data-testid^='crmhr-address-primary-']").First.CheckAsync();
        await SavePartyAsync(page);

        await page.GetByTestId("crmhr-directory-tab-relationships").ClickAsync();
        await page.GetByTestId("crmhr-relationship-add").ClickAsync();
        await page.Locator("[data-testid^='crmhr-relationship-kind-']").First.SelectOptionAsync(new[] { PartyRelationshipKind.ManagedBy.ToString() });
        await page.Locator("[data-testid^='crmhr-relationship-party-']").First.SelectOptionAsync(
            new[]
            {
                new SelectOptionValue
                {
                    Label = $"{parentName} ({PartyType.Organization})"
                }
            });
        await page.Locator("[data-testid^='crmhr-relationship-notes-']").First.FillAsync("Primary parent");
        await page.GetByTestId("crmhr-party-save-button").ClickAsync();

        var importExportDialog = page.GetByTestId("crmhr-directory-import-export-dialog");
        await page.GetByTestId("crmhr-directory-import-export-button").ClickAsync();
        await importExportDialog.WaitForAsync();
        await page.GetByTestId("crmhr-export-refresh").ClickAsync();
        await ExpectTextAreaValueContainsAsync(page.GetByTestId("crmhr-export-textarea"), retainedName);
        await ExpectTextAreaValueContainsAsync(page.GetByTestId("crmhr-export-textarea"), "100 Market Street");
        await importExportDialog.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await importExportDialog.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Detached
        });

        await OpenNewPartyAsync(page);
        await FillBasicPartyAsync(page, duplicateName, sharedEmail, "+1 206 555 0102", PartyRoleKind.Customer, "DUPLICATE");
        await SavePartyAsync(page);

        await SelectPartyAsync(page, retainedName);
        await page.GetByTestId("crmhr-directory-tab-relationships").ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Merge into current party", Exact = true }).ClickAsync();
        await page.GetByTestId("crmhr-merge-dialog").WaitForAsync();
        await page.GetByTestId("crmhr-merge-reason").FillAsync("Playwright merge validation");
        await page.GetByTestId("crmhr-merge-confirm").ClickAsync();
        await page.WaitForSelectorAsync($"text=Merged '{duplicateName}' into '{retainedName}'.");

        await page.GetByTestId("crmhr-directory-search").FillAsync(duplicateName);
        await WaitForListItemCountAsync(page, 0);
        await page.GetByTestId("crmhr-directory-search").FillAsync(string.Empty);

        var csvContent = $"""
            DisplayName,PartyType,LifecycleStatus,ExternalCode,LegalName,PreferredName,Summary,Tags,Region,CountryCode,TimeZone,IsSensitive,Roles,ContactPoints,Addresses
            {importedName},Person,Active,IMP-{suffix},{importedName} LLC,{importedName},Imported through Playwright,imported,NA,US,America/Chicago,False,Candidate|Candidate|True,Email|Primary|imported.{suffix}@example.test|True|True,Work|200 Lake Street||Chicago|IL|60601|US|True
            """;

        await page.GetByTestId("crmhr-directory-import-export-button").ClickAsync();
        await importExportDialog.WaitForAsync();
        await page.GetByTestId("crmhr-import-textarea").FillAsync(csvContent);
        await page.GetByTestId("crmhr-import-preview-button").ClickAsync();
        await page.GetByTestId("crmhr-import-row").WaitForAsync();
        await page.WaitForSelectorAsync($"text={importedName}");
        await page.GetByTestId("crmhr-import-apply-button").ClickAsync();
        await importExportDialog.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Detached
        });

        await page.GetByTestId("crmhr-directory-search").FillAsync(importedName);
        await WaitForListItemCountAsync(page, 1);
        await SelectPartyAsync(page, importedName);
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-directory-b03-desktop.png"),
            FullPage = true
        });

        await page.SetViewportSizeAsync(1024, 900);
        await page.GetByTestId("crmhr-directory-search").WaitForAsync();
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-directory-b03-tablet.png"),
            FullPage = true
        });
    }

    private static async Task FillBasicPartyAsync(
        IPage page,
        string partyName,
        string email,
        string phone,
        PartyRoleKind roleKind,
        string externalCodePrefix)
    {
        await page.GetByTestId("crmhr-party-type").SelectOptionAsync(new[] { PartyType.Organization.ToString() });
        await page.GetByTestId("crmhr-party-status").SelectOptionAsync(new[] { PartyLifecycleStatus.Active.ToString() });
        await page.GetByTestId("crmhr-party-display-name").FillAsync(partyName);
        await page.GetByTestId("crmhr-party-role").SelectOptionAsync(new[] { roleKind.ToString() });
        await page.GetByTestId("crmhr-party-email").FillAsync(email);
        await page.GetByTestId("crmhr-party-phone").FillAsync(phone);
        await page.GetByTestId("crmhr-party-tags").FillAsync("crm-hr, playwright");
        await page.GetByTestId("crmhr-party-external-code").FillAsync($"{externalCodePrefix}-{partyName[^6..]}");
        await page.GetByTestId("crmhr-party-summary").FillAsync($"Summary for {partyName}");
    }

    private static async Task OpenNewPartyAsync(IPage page)
    {
        await page.GetByTestId("crmhr-directory-new-button").ClickAsync();
        await page.GetByTestId("crmhr-party-display-name").WaitForAsync();
        await page.GetByTestId("crmhr-party-display-name").FillAsync(string.Empty);
    }

    private static async Task SavePartyAsync(IPage page)
    {
        await page.GetByTestId("crmhr-party-save-button").ClickAsync();
        await WaitForUrlContainsAsync(page, "/crm-hr/directory?partyId=");
        await page.WaitForSelectorAsync("text=Party saved.");
        await page.WaitForTimeoutAsync(500);
    }

    private static async Task SelectPartyAsync(IPage page, string partyName)
    {
        await page.GetByTestId("crmhr-directory-search").FillAsync(partyName);
        var listItem = page.Locator($"div[data-testid='crmhr-directory-item']:has-text(\"{EscapeSelectorText(partyName)}\")").First;
        await listItem.WaitForAsync();
        await listItem.GetByText(partyName, new LocatorGetByTextOptions { Exact = true }).First.ClickAsync();
        await page.GetByTestId("crmhr-party-display-name").WaitForAsync();
        await ExpectInputValueContainsAsync(page.GetByTestId("crmhr-party-display-name"), partyName);
    }

    private static async Task ExpectInputValueContainsAsync(ILocator locator, string expectedValue, int timeoutMs = 10_000)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if (string.Equals(await locator.InputValueAsync(), expectedValue, StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Timed out waiting for input value '{expectedValue}'.");
    }

    private static async Task ExpectTextAreaValueContainsAsync(ILocator locator, string expectedValue, int timeoutMs = 10_000)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if ((await locator.InputValueAsync()).Contains(expectedValue, StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Timed out waiting for textarea value to contain '{expectedValue}'.");
    }

    private static async Task WaitForListItemCountAsync(IPage page, int expectedCount, int timeoutMs = 10_000)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if (await page.Locator("[data-testid='crmhr-directory-item']").CountAsync() == expectedCount)
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Timed out waiting for directory item count {expectedCount}.");
    }

    private static string EscapeSelectorText(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static async Task DismissStartupModalIfPresentAsync(IPage page, float timeoutMs = 1_500)
    {
        var startupDialog = page.GetByTestId("database-startup-modal");
        try
        {
            await startupDialog.WaitForAsync(new LocatorWaitForOptions
            {
                Timeout = timeoutMs
            });
        }
        catch (TimeoutException)
        {
            return;
        }

        await page.GetByTestId("database-startup-continue").ClickAsync();
        await startupDialog.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Detached
        });
    }

    private static async Task WaitForUrlContainsAsync(IPage page, string fragment, int timeoutMs = 10_000)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if (page.Url.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Timed out waiting for URL to contain '{fragment}'. Current URL: {page.Url}");
    }

    private static async Task RequireLocatorAsync(IPage page, ILocator locator, string htmlDumpPath, int timeoutMs = 5_000)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if (await locator.CountAsync() > 0)
            {
                return;
            }

            await Task.Delay(200);
        }

        await File.WriteAllTextAsync(htmlDumpPath, await page.ContentAsync());
        throw new InvalidOperationException($"Expected locator to exist. Dumped HTML to '{htmlDumpPath}'.");
    }
}
