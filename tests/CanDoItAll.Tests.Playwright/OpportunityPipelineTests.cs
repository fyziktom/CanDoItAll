using CanDoItAll.Modules.CrmHr;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Collection(PlaywrightCollection.Name)]
public sealed class OpportunityPipelineTests
{
    private readonly PlaywrightAppFixture fixture;

    public OpportunityPipelineTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Quarantined")]
    public async Task Crm_workspace_supports_pipeline_filters_history_and_project_conversion()
    {
        var evidenceDirectory = @"C:\repositories\CanDoItAll\evidence\crm-hr\b05";
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
        var accountName = $"B05 Account {suffix}";
        var ownerName = $"B05 Owner {suffix}";
        var deliveryUnitName = $"B05 Unit {suffix}";
        var partnerName = $"B05 Partner {suffix}";
        var conversionOpportunityTitle = $"B05 Renewal {suffix}";
        var lostOpportunityTitle = $"B05 Lost {suffix}";
        var homePreviewOpportunityTitle = $"B05 Open {suffix}";

        await page.GotoAsync($"{fixture.BaseUrl}/crm-hr/directory");
        await DismissStartupModalIfPresentAsync(page);
        await page.GetByTestId("crmhr-party-save-button").WaitForAsync();

        await FillPartyAsync(page, PartyType.Organization, PartyLifecycleStatus.Prospect, accountName, $"crm.{suffix}@example.test", "+1 206 555 0300", PartyRoleKind.Customer, "B05-ACC");
        await SavePartyAsync(page);

        await OpenNewPartyAsync(page);
        await FillPartyAsync(page, PartyType.Person, PartyLifecycleStatus.Active, ownerName, $"owner.{suffix}@example.test", "+1 206 555 0301", PartyRoleKind.AccountManager, "B05-OWN");
        await SavePartyAsync(page);

        await OpenNewPartyAsync(page);
        await FillPartyAsync(page, PartyType.OrganizationUnit, PartyLifecycleStatus.Active, deliveryUnitName, $"unit.{suffix}@example.test", "+1 206 555 0302", PartyRoleKind.DeliveryUnit, "B05-UNT");
        await SavePartyAsync(page);

        await OpenNewPartyAsync(page);
        await FillPartyAsync(page, PartyType.Organization, PartyLifecycleStatus.Active, partnerName, $"partner.{suffix}@example.test", "+1 206 555 0303", PartyRoleKind.Partner, "B05-PRT");
        await SavePartyAsync(page);

        await page.GotoAsync($"{fixture.BaseUrl}/crm-hr/crm");
        await page.GetByTestId("crmhr-account-search").WaitForAsync();
        await SelectAccountAsync(page, accountName);
        await page.GetByTestId("crmhr-opportunity-title").WaitForAsync();

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-crm-b05-initial.png"),
            FullPage = true
        });

        await page.GetByTestId("crmhr-opportunity-title").FillAsync(conversionOpportunityTitle);
        await page.GetByTestId("crmhr-opportunity-source").SelectOptionAsync(new[] { OpportunitySource.Partner.ToString() });
        await page.GetByTestId("crmhr-opportunity-owner").SelectOptionAsync(new[]
        {
            new SelectOptionValue
            {
                Label = $"{ownerName} ({PartyType.Person})"
            }
        });
        await page.GetByTestId("crmhr-opportunity-delivery-unit").SelectOptionAsync(new[]
        {
            new SelectOptionValue
            {
                Label = deliveryUnitName
            }
        });
        await page.GetByTestId("crmhr-opportunity-currency").FillAsync("USD");
        await page.GetByTestId("crmhr-opportunity-amount").FillAsync("185000");
        await page.GetByTestId("crmhr-opportunity-probability").FillAsync("45");
        await page.GetByTestId("crmhr-opportunity-close-date").FillAsync("2026-07-01");
        await page.GetByTestId("crmhr-opportunity-summary").FillAsync("Renewal plus managed delivery extension.");
        await page.GetByTestId("crmhr-opportunity-notes").FillAsync("Partner introduced sponsor and procurement lead.");
        await page.GetByTestId("crmhr-opportunity-partner-contribution").FillAsync("Introduced sponsor and handled procurement workshops.");
        await page.GetByTestId("crmhr-opportunity-party-add").ClickAsync();
        await page.GetByTestId("crmhr-opportunity-party-0").SelectOptionAsync(new[]
        {
            new SelectOptionValue
            {
                Label = $"{partnerName} ({PartyType.Organization})"
            }
        });
        await page.GetByTestId("crmhr-opportunity-party-role-0").SelectOptionAsync(new[] { OpportunityPartyRole.Partner.ToString() });
        await page.GetByTestId("crmhr-opportunity-save-button").ClickAsync();
        await page.WaitForSelectorAsync("text=Opportunity saved.");
        var identifiedColumn = page.GetByTestId("crmhr-opportunity-column-identified");
        var identifiedCard = identifiedColumn
            .Locator("[data-testid^='crmhr-opportunity-card-']")
            .Filter(new LocatorFilterOptions
            {
                HasText = conversionOpportunityTitle
            });
        await identifiedCard.WaitForAsync();

        await identifiedCard
            .GetByRole(AriaRole.Button, new() { Name = "Advance to Qualified" })
            .ClickAsync();
        var qualifiedCard = page.GetByTestId("crmhr-opportunity-column-qualified")
            .Locator("[data-testid^='crmhr-opportunity-card-']")
            .Filter(new LocatorFilterOptions
            {
                HasText = conversionOpportunityTitle
            });
        await qualifiedCard.WaitForAsync();

        await page.GetByTestId("crmhr-opportunity-stage").SelectOptionAsync(new[] { OpportunityStage.Won.ToString() });
        await page.GetByTestId("crmhr-opportunity-stage-notes").FillAsync("Closed won after steering committee approval.");
        await page.GetByTestId("crmhr-opportunity-probability").FillAsync("100");
        await page.GetByTestId("crmhr-opportunity-save-button").ClickAsync();
        await page.WaitForSelectorAsync("text=Opportunity saved.");

        await page.GetByTestId("crmhr-opportunity-convert-button").ClickAsync();
        await page.GetByTestId("crmhr-opportunity-conversion-dialog").WaitForAsync();
        await page.GetByTestId("crmhr-opportunity-conversion-name").FillAsync("B05 Project Handoff");
        await page.GetByTestId("crmhr-opportunity-conversion-description").FillAsync("Project context created from the won CRM opportunity.");
        await page.GetByTestId("crmhr-opportunity-conversion-objective").FillAsync("Start structured delivery without retyping account and partner context.");
        await page.GetByTestId("crmhr-opportunity-conversion-phase").FillAsync("Sales handoff");
        await page.GetByTestId("crmhr-opportunity-conversion-save").ClickAsync();
        await page.WaitForSelectorAsync("text=Opportunity converted into a new project.");
        await page.GetByTestId("crmhr-opportunity-open-project").WaitForAsync();

        await page.GetByTestId("crmhr-opportunity-new-button").ClickAsync();
        await page.GetByTestId("crmhr-opportunity-open-project").WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Detached
        });
        await page.WaitForFunctionAsync(
            "() => document.querySelector('[data-testid=\"crmhr-opportunity-title\"]')?.value === ''");
        await page.GetByTestId("crmhr-opportunity-title").FillAsync(lostOpportunityTitle);
        await page.GetByTestId("crmhr-opportunity-stage").SelectOptionAsync(new[] { OpportunityStage.Lost.ToString() });
        await page.GetByTestId("crmhr-opportunity-lost-reason").WaitForAsync();
        await page.GetByTestId("crmhr-opportunity-source").SelectOptionAsync(new[] { OpportunitySource.Direct.ToString() });
        await page.GetByTestId("crmhr-opportunity-owner").SelectOptionAsync(new[]
        {
            new SelectOptionValue
            {
                Label = $"{ownerName} ({PartyType.Person})"
            }
        });
        await page.GetByTestId("crmhr-opportunity-lost-reason").FillAsync("Budget moved to the next fiscal year.");
        await page.GetByTestId("crmhr-opportunity-competitor").FillAsync("Contoso Advisory");
        await page.GetByTestId("crmhr-opportunity-summary").FillAsync("Direct pursuit that lost budget timing.");
        await page.GetByTestId("crmhr-opportunity-stage-notes").FillAsync("Closed lost after budget rebalance.");
        await page.GetByTestId("crmhr-opportunity-save-button").ClickAsync();
        await page.WaitForSelectorAsync("text=Opportunity saved.");
        await page.GetByTestId("crmhr-opportunity-column-lost")
            .Locator("[data-testid^='crmhr-opportunity-card-']")
            .Filter(new LocatorFilterOptions
            {
                HasText = lostOpportunityTitle
            })
            .WaitForAsync();

        await page.GetByTestId("crmhr-opportunity-new-button").ClickAsync();
        await page.WaitForFunctionAsync(
            "() => document.querySelector('[data-testid=\"crmhr-opportunity-title\"]')?.value === ''");
        await page.GetByTestId("crmhr-opportunity-title").FillAsync(homePreviewOpportunityTitle);
        await page.GetByTestId("crmhr-opportunity-stage").SelectOptionAsync(new[] { OpportunityStage.Proposal.ToString() });
        await page.GetByTestId("crmhr-opportunity-source").SelectOptionAsync(new[] { OpportunitySource.Direct.ToString() });
        await page.GetByTestId("crmhr-opportunity-owner").SelectOptionAsync(new[]
        {
            new SelectOptionValue
            {
                Label = $"{ownerName} ({PartyType.Person})"
            }
        });
        await page.GetByTestId("crmhr-opportunity-summary").FillAsync("Open pipeline item for the CRM home preview.");
        await page.GetByTestId("crmhr-opportunity-save-button").ClickAsync();
        await page.WaitForSelectorAsync("text=Opportunity saved.");
        await page.GetByTestId("crmhr-opportunity-column-proposal")
            .Locator("[data-testid^='crmhr-opportunity-card-']")
            .Filter(new LocatorFilterOptions
            {
                HasText = homePreviewOpportunityTitle
            })
            .WaitForAsync();

        await page.GetByTestId("crmhr-opportunity-stage-filter").SelectOptionAsync(new[] { OpportunityStage.Lost.ToString() });
        await page.GetByTestId("crmhr-opportunity-column-lost")
            .Locator("[data-testid^='crmhr-opportunity-card-']")
            .Filter(new LocatorFilterOptions
            {
                HasText = lostOpportunityTitle
            })
            .WaitForAsync();
        await page.GetByTestId("crmhr-opportunity-stage-filter").SelectOptionAsync(new[] { "" });
        await page.GetByTestId("crmhr-opportunity-partner-filter").SelectOptionAsync(new[]
        {
            new SelectOptionValue
            {
                Label = partnerName
            }
        });
        await page.GetByTestId("crmhr-opportunity-column-won")
            .Locator("[data-testid^='crmhr-opportunity-card-']")
            .Filter(new LocatorFilterOptions
            {
                HasText = conversionOpportunityTitle
            })
            .WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Reset", Exact = true }).Last.ClickAsync();
        await page.GetByTestId("crmhr-opportunity-column-won")
            .Locator("[data-testid^='crmhr-opportunity-card-']")
            .Filter(new LocatorFilterOptions
            {
                HasText = conversionOpportunityTitle
            })
            .GetByRole(AriaRole.Button, new() { Name = "Open" })
            .ClickAsync();
        await page.GetByTestId("crmhr-opportunity-open-project").WaitForAsync();

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-crm-b05-desktop.png"),
            FullPage = true
        });

        await page.ReloadAsync();
        await page.GetByTestId("crmhr-opportunity-column-lost")
            .Locator("[data-testid^='crmhr-opportunity-card-']")
            .Filter(new LocatorFilterOptions
            {
                HasText = lostOpportunityTitle
            })
            .WaitForAsync();
        await page.GetByTestId("crmhr-opportunity-column-won")
            .Locator("[data-testid^='crmhr-opportunity-card-']")
            .Filter(new LocatorFilterOptions
            {
                HasText = conversionOpportunityTitle
            })
            .WaitForAsync();
        await page.GetByTestId("crmhr-opportunity-column-won")
            .Locator("[data-testid^='crmhr-opportunity-card-']")
            .Filter(new LocatorFilterOptions
            {
                HasText = conversionOpportunityTitle
            })
            .GetByRole(AriaRole.Button, new() { Name = "Open" })
            .ClickAsync();
        await page.GetByTestId("crmhr-opportunity-open-project").WaitForAsync();
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-crm-b05-reload.png"),
            FullPage = true
        });

        await page.SetViewportSizeAsync(1100, 900);
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-crm-b05-tablet.png"),
            FullPage = true
        });

        await page.SetViewportSizeAsync(1600, 1000);
        await page.GetByTestId("crmhr-opportunity-open-project").ClickAsync();
        await WaitForUrlContainsAsync(page, "/projects?projectId=");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-projects-b05-linked-project.png"),
            FullPage = true
        });

        await page.GotoAsync($"{fixture.BaseUrl}/crm-hr");
        await page.GetByText(homePreviewOpportunityTitle, new PageGetByTextOptions { Exact = true }).WaitForAsync();
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-home-b05-desktop.png"),
            FullPage = true
        });
    }

    private static async Task SelectAccountAsync(IPage page, string accountName)
    {
        await page.GetByTestId("crmhr-account-search").FillAsync(accountName);
        await page.GetByTestId("crmhr-account-item")
            .Filter(new LocatorFilterOptions
            {
                HasText = accountName
            })
            .Locator("button")
            .First
            .ClickAsync();
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
