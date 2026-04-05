using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Collection(PlaywrightCollection.Name)]
public sealed class CrmHrSensitiveDataFlowTests
{
    private readonly PlaywrightAppFixture fixture;

    public CrmHrSensitiveDataFlowTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Sensitive_directory_and_workforce_flows_preserve_privacy_markers_and_audit_history()
    {
        var evidenceDirectory = @"C:\repositories\CanDoItAll\evidence\crm-hr\b12";
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
        var partyName = $"B12 Sensitive {suffix}";
        var confidentialNote = $"Private salary note {suffix}";
        var partyId = await SeedSensitivePartyAsync(partyName, confidentialNote);

        await page.GotoAsync($"{fixture.BaseUrl}/crm-hr/directory?partyId={partyId}");
        await DismissStartupModalIfPresentAsync(page);
        await page.GetByTestId("crmhr-party-save-button").WaitForAsync();

        await ExpectTextContainsAsync(page.GetByTestId("crmhr-directory-sensitive-callout"), "Hidden from global search");
        var confidentialNoteItem = page.GetByTestId("crmhr-confidential-note-item").First;
        await confidentialNoteItem.WaitForAsync();
        Assert.Equal(confidentialNote, await confidentialNoteItem.Locator("textarea").InputValueAsync());

        await page.GetByTestId("crmhr-party-status").SelectOptionAsync(new[] { PartyLifecycleStatus.Archived.ToString() });
        await SavePartyAsync(page);
        await ExpectTextContainsAsync(page.Locator("body"), $"Archived party '{partyName}'.");

        await page.GetByTestId("crmhr-party-status").SelectOptionAsync(new[] { PartyLifecycleStatus.Active.ToString() });
        await SavePartyAsync(page);
        await ExpectTextContainsAsync(page.Locator("body"), $"Reactivated party '{partyName}'.");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-directory-b12-desktop.png"),
            FullPage = true
        });

        await page.GotoAsync($"{fixture.BaseUrl}/crm-hr");
        await page.GetByTestId("crmhr-home-sensitive-card").WaitForAsync();
        await ExpectTextContainsAsync(page.GetByTestId("crmhr-home-sensitive-card"), partyName);
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-home-b12-desktop.png"),
            FullPage = true
        });

        await page.GotoAsync($"{fixture.BaseUrl}/crm-hr/workforce?partyId={partyId}");
        await page.GetByTestId("crmhr-workforce-job-title").WaitForAsync();
        await page.GetByTestId("crmhr-workforce-kind").SelectOptionAsync(new[] { WorkforceKind.Employee.ToString() });
        await page.GetByTestId("crmhr-workforce-status").SelectOptionAsync(new[] { "Active" });
        await page.GetByTestId("crmhr-workforce-job-title").FillAsync("Privacy Steward");
        await page.GetByTestId("crmhr-workforce-discipline").FillAsync("People Operations");
        await page.GetByTestId("crmhr-workforce-location").FillAsync("Remote");
        await page.GetByTestId("crmhr-workforce-timezone").FillAsync("UTC");
        await page.GetByTestId("crmhr-workforce-notes").FillAsync("Sensitive workforce proof.");
        await page.GetByTestId("crmhr-workforce-save-button").ClickAsync();
        await page.WaitForSelectorAsync("text=Workforce profile saved.");
        await ExpectTextContainsAsync(page.GetByTestId("crmhr-workforce-sensitive-callout"), "Hidden from global search");
        await ExpectTextContainsAsync(page.GetByTestId("crmhr-workforce-timeline"), $"Saved workforce profile for '{partyName}'.");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-workforce-b12-desktop.png"),
            FullPage = true
        });

        await page.SetViewportSizeAsync(1024, 900);
        await page.GetByTestId("crmhr-workforce-sensitive-callout").WaitForAsync();
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-workforce-b12-tablet.png"),
            FullPage = true
        });

        await VerifyPersistedStateAsync(partyId, partyName, confidentialNote);
    }

    private async Task<Guid> SeedSensitivePartyAsync(string partyName, string confidentialNote)
    {
        var activeProfile = CreateActiveProfile();
        await using var serviceProvider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            activeProfile,
            "CanDoItAll.Tests.Playwright.Seed",
            TestSchemaBootstrapModules.Full,
            new Dictionary<string, string?>
            {
                ["DevelopmentManager:TuningModeEnabled"] = "false"
            });
        await using var scope = serviceProvider.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();

        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = PartyType.Person,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = partyName,
            Summary = "Operational summary for B12 proof.",
            Notes = "Operational note for staffing.",
            IsSensitive = true,
            LastChangedBy = "playwright-tests",
            Roles =
            [
                new PartyRoleAssignmentEditorModel
                {
                    RoleKind = PartyRoleKind.Employee,
                    Title = "Employee",
                    IsPrimary = true
                }
            ],
            ContactPoints =
            [
                new PartyContactPointEditorModel
                {
                    ContactType = PartyContactType.Email,
                    Label = "Primary email",
                    Value = $"{partyName.Replace(" ", ".", StringComparison.Ordinal).ToLowerInvariant()}@example.test",
                    NormalizedValue = $"{partyName.Replace(" ", ".", StringComparison.Ordinal).ToLowerInvariant()}@example.test",
                    IsPrimary = true,
                    IsPublic = true
                }
            ],
            ConfidentialNotes =
            [
                new PartyConfidentialNoteEditorModel
                {
                    Category = PartyConfidentialNoteCategories.Compensation,
                    NoteText = confidentialNote,
                    CreatedBy = "playwright-tests"
                }
            ]
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private async Task VerifyPersistedStateAsync(Guid partyId, string partyName, string confidentialNote)
    {
        var activeProfile = CreateActiveProfile();
        await using var serviceProvider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            activeProfile,
            "CanDoItAll.Tests.Playwright.Verify",
            TestSchemaBootstrapModules.Full,
            new Dictionary<string, string?>
            {
                ["DevelopmentManager:TuningModeEnabled"] = "false"
            });
        await using var scope = serviceProvider.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var hrService = scope.ServiceProvider.GetRequiredService<HrService>();
        var searchIndexService = scope.ServiceProvider.GetRequiredService<CanDoItAll.Infrastructure.Search.ISearchIndexService>();

        var party = await partyDirectoryService.GetPartyAsync(partyId);
        Assert.NotNull(party);
        Assert.True(party.IsSensitive);
        Assert.Equal(confidentialNote, Assert.Single(party.ConfidentialNotes).NoteText);

        var workspace = await hrService.GetWorkforceWorkspaceAsync(partyId);
        Assert.NotNull(workspace);
        Assert.True(workspace.IsSensitive);
        Assert.Equal("playwright-tests", workspace.LastChangedBy);

        Assert.Empty(await searchIndexService.SearchAsync(partyName));
        Assert.Empty(await searchIndexService.SearchAsync(confidentialNote));
    }

    private TestDatabaseProfile CreateActiveProfile()
    {
        if (string.IsNullOrWhiteSpace(fixture.DatabaseConnectionString))
        {
            throw new InvalidOperationException("Playwright fixture did not expose a database connection string.");
        }

        if (string.IsNullOrWhiteSpace(fixture.StorageWorkspaceRoot))
        {
            throw new InvalidOperationException("Playwright fixture did not expose the storage workspace root.");
        }

        var workspaceRoot = fixture.StorageWorkspaceRoot;
        var profileRoot = Directory.GetParent(workspaceRoot)?.FullName
            ?? throw new InvalidOperationException($"Could not resolve profile root from '{workspaceRoot}'.");
        var environmentRoot = Path.GetFullPath(Path.Combine(profileRoot, "..", ".."));

        return new TestDatabaseProfile(
            "playwright-seed",
            environmentRoot,
            profileRoot,
            TestDatabaseProviderKind.Sqlite,
            fixture.DatabaseConnectionString,
            workspaceRoot,
            Path.Combine(profileRoot, "manager-artifacts"));
    }

    private static async Task SavePartyAsync(IPage page)
    {
        await page.GetByTestId("crmhr-party-save-button").ClickAsync();
        await WaitForUrlContainsAsync(page, "/crm-hr/directory?partyId=");
        await page.WaitForSelectorAsync("text=Party saved.");
        await page.WaitForTimeoutAsync(500);
    }

    private static async Task ExpectTextContainsAsync(ILocator locator, string expectedValue, int timeoutMs = 10_000)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if ((await locator.InnerTextAsync()).Contains(expectedValue, StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Timed out waiting for text '{expectedValue}'.");
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
