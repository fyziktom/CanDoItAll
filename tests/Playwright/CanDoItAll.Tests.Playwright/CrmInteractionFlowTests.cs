using CanDoItAll.Modules.CrmHr;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Quarantined;

[Collection(PlaywrightCollection.Name)]
public sealed class CrmInteractionFlowTests
{
    private readonly PlaywrightAppFixture fixture;

    public CrmInteractionFlowTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Quarantined")]
    public async Task Crm_workspace_supports_account_profile_connections_and_overdue_followups()
    {
        var evidenceDirectory = @"C:\repositories\CanDoItAll\evidence\crm-hr\b04";
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
        var accountName = $"B04 Account {suffix}";
        var contactName = $"B04 Contact {suffix}";

        await page.GotoAsync($"{fixture.BaseUrl}/crm-hr/directory");
        await DismissStartupModalIfPresentAsync(page);
        await page.GetByTestId("crmhr-party-save-button").WaitForAsync();

        await FillPartyAsync(page, PartyType.Organization, PartyLifecycleStatus.Prospect, accountName, $"crm.{suffix}@example.test", "+1 206 555 0200", PartyRoleKind.Customer, "B04-ACC");
        await SavePartyAsync(page);

        await OpenNewPartyAsync(page);
        await FillPartyAsync(page, PartyType.Person, PartyLifecycleStatus.Active, contactName, $"contact.{suffix}@example.test", "+1 206 555 0201", PartyRoleKind.CustomerContact, "B04-CON");
        await SavePartyAsync(page);

        await page.GotoAsync($"{fixture.BaseUrl}/crm-hr/crm");
        await page.GetByTestId("crmhr-account-search").WaitForAsync();
        await page.GetByTestId("crmhr-account-search").FillAsync(accountName);
        await page.GetByTestId("crmhr-account-item")
            .Filter(new LocatorFilterOptions
            {
                HasText = accountName
            })
            .Locator("button")
            .First
            .ClickAsync();
        await page.GetByTestId("crmhr-account-stage").WaitForAsync();

        await page.GetByTestId("crmhr-account-stage").SelectOptionAsync(new[] { CrmAccountRelationshipStage.ActiveCustomer.ToString() });
        await page.GetByTestId("crmhr-account-commercial-notes").FillAsync("Pilot renewal depends on procurement timing.");
        await page.GetByTestId("crmhr-account-constraints").FillAsync("Legal review remains open.");
        await page.GetByTestId("crmhr-account-timing-risks").FillAsync("Executive sign-off may slip.");
        await page.GetByTestId("crmhr-account-save-button").ClickAsync();
        await page.WaitForSelectorAsync("text=CRM account profile saved.");

        await page.GetByTestId("crmhr-crm-tab-connections").ClickAsync();
        await page.GetByTestId("crmhr-connection-add").ClickAsync();
        await page.GetByTestId("crmhr-connection-party-0").ClickAsync();
        await page.GetByTestId("crmhr-connection-party-picker-browser-search").FillAsync(contactName);
        await page.GetByTestId("crmhr-connection-party-picker-browser").GetByText(contactName).ClickAsync();
        await page.GetByTestId("crmhr-connection-party-picker-confirm").ClickAsync();
        await page.GetByTestId("crmhr-connection-role-0").SelectOptionAsync(new[] { CrmAccountConnectionRole.BillingContact.ToString() });
        await page.GetByTestId("crmhr-connection-primary-0").CheckAsync();
        await page.GetByTestId("crmhr-connection-notes-0").FillAsync("Primary invoicing contact");
        await page.GetByTestId("crmhr-connection-save-button").ClickAsync();
        await page.WaitForSelectorAsync("text=Company connections and related projects saved.");

        await page.GetByTestId("crmhr-crm-tab-interactions").ClickAsync();
        await page.GetByLabel($"{contactName} ({PartyType.Person})", new() { Exact = true }).CheckAsync();
        await page.GetByTestId("crmhr-interaction-type").SelectOptionAsync(new[] { InteractionType.Meeting.ToString() });
        await page.GetByTestId("crmhr-interaction-subject").FillAsync("Commercial steering call");
        await page.GetByTestId("crmhr-interaction-summary").FillAsync("Confirmed invoicing owner and next follow-up.");
        await page.GetByTestId("crmhr-next-action-text").FillAsync("Send revised statement of work");
        await page.GetByTestId("crmhr-next-action-owner").SelectOptionAsync(new[]
        {
            new SelectOptionValue
            {
                Label = $"{contactName} ({PartyType.Person})"
            }
        });
        await page.GetByTestId("crmhr-next-action-due-on").FillAsync(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)).ToString("yyyy-MM-dd"));
        await page.GetByTestId("crmhr-interaction-save-button").ClickAsync();
        await page.WaitForSelectorAsync("text=CRM interaction saved.");
        await page.WaitForSelectorAsync("text=Send revised statement of work");
        await page.WaitForSelectorAsync($"text=Owner: {contactName}");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-crm-b04-desktop.png"),
            FullPage = true
        });

        await page.GetByRole(AriaRole.Button, new() { Name = "Open directory record", Exact = true }).First.ClickAsync();
        await WaitForUrlContainsAsync(page, "/crm-hr/directory?partyId=");
        await page.GetByTestId("crmhr-party-display-name").WaitForAsync();
        await ExpectInputValueContainsAsync(page.GetByTestId("crmhr-party-display-name"), accountName);
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());

        await page.SetViewportSizeAsync(1024, 900);
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-directory-b04-tablet.png"),
            FullPage = true
        });
    }

    private static async Task FillPartyAsync(
        IPage page,
        PartyType partyType,
        PartyLifecycleStatus lifecycleStatus,
        string partyName,
        string email,
        string phone,
        PartyRoleKind roleKind,
        string externalCodePrefix)
    {
        await page.GetByTestId("crmhr-party-type").SelectOptionAsync(new[] { partyType.ToString() });
        await page.GetByTestId("crmhr-party-status").SelectOptionAsync(new[] { lifecycleStatus.ToString() });
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
}
