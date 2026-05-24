using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Collection(PlaywrightCollection.Name)]
public sealed class CrmHrWorkforceFlowTests
{
    private readonly PlaywrightAppFixture fixture;

    public CrmHrWorkforceFlowTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Workforce_workspace_supports_delivery_units_and_worker_profiles()
    {
        var evidenceDirectory = @"C:\repositories\CanDoItAll\evidence\crm-hr\b06";
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
        var managerName = $"B06 Manager {suffix}";
        var workerName = $"B06 Worker {suffix}";
        var unitName = $"B06 Unit {suffix}";
        var seededParties = await SeedWorkforcePartiesAsync(managerName, workerName, unitName);

        await page.GotoAsync($"{fixture.BaseUrl}/crm-hr/workforce");
        await DismissStartupModalIfPresentAsync(page);
        await page.GetByTestId("crmhr-workforce-search").WaitForAsync();

        await page.GetByTestId("crmhr-workforce-search").FillAsync(workerName);
        await page.GetByTestId("crmhr-workforce-item")
            .Filter(new LocatorFilterOptions
            {
                HasText = workerName
            })
            .Locator("button")
            .First
            .ClickAsync();
        await page.GetByTestId("crmhr-workforce-save-button").WaitForAsync();
        await page.GetByTestId("crmhr-workforce-summary-home-unit").WaitForAsync();
        await ExpectTextContainsAsync(page.GetByTestId("crmhr-workforce-summary-home-unit"), unitName);
        await ExpectTextContainsAsync(page.GetByTestId("crmhr-workforce-summary-manager"), managerName);
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-workforce-b06-desktop.png"),
            FullPage = true
        });

        await page.SetViewportSizeAsync(1100, 900);
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-workforce-b06-tablet.png"),
            FullPage = true
        });

        await page.GetByRole(AriaRole.Button, new() { Name = "Open directory record", Exact = true }).First.ClickAsync();
        await WaitForUrlContainsAsync(page, "/crm-hr/directory?partyId=");
        await page.GetByTestId("crmhr-party-display-name").WaitForAsync();
        await ExpectInputValueContainsAsync(page.GetByTestId("crmhr-party-display-name"), workerName);
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private async Task<SeededWorkforceParties> SeedWorkforcePartiesAsync(string managerName, string workerName, string unitName)
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
        var hrService = scope.ServiceProvider.GetRequiredService<HrService>();

        var managerId = await CreatePartyAsync(
            partyDirectoryService,
            managerName,
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Employee,
            $"manager.{managerName[^6..].ToLowerInvariant()}@example.test");
        var workerId = await CreatePartyAsync(
            partyDirectoryService,
            workerName,
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Employee,
            $"worker.{workerName[^6..].ToLowerInvariant()}@example.test");
        var unitId = await CreatePartyAsync(
            partyDirectoryService,
            unitName,
            PartyType.OrganizationUnit,
            PartyLifecycleStatus.Active,
            PartyRoleKind.DeliveryUnit,
            $"unit.{unitName[^6..].ToLowerInvariant()}@example.test");

        var saveProfileResult = await hrService.SaveWorkforceProfileAsync(new WorkforceProfileEditorModel
        {
            PartyId = workerId,
            WorkforceKind = WorkforceKind.Employee,
            Status = "Active",
            HomeUnitPartyId = unitId,
            ManagerPartyId = managerId,
            LastChangedBy = "playwright-tests"
        });
        Assert.True(saveProfileResult.IsSuccess);

        return new SeededWorkforceParties(managerId, workerId, unitId);
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
            TestDatabaseProviderKind.PostgreSql,
            fixture.DatabaseConnectionString,
            workspaceRoot,
            Path.Combine(profileRoot, "manager-artifacts"));
    }

    private static async Task<Guid> CreatePartyAsync(
        PartyDirectoryService partyDirectoryService,
        string displayName,
        PartyType partyType,
        PartyLifecycleStatus lifecycleStatus,
        PartyRoleKind roleKind,
        string email)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = lifecycleStatus,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "playwright-tests",
            Roles =
            [
                new PartyRoleAssignmentEditorModel
                {
                    RoleKind = roleKind,
                    Title = roleKind.ToString(),
                    IsPrimary = true
                }
            ],
            ContactPoints =
            [
                new PartyContactPointEditorModel
                {
                    ContactType = PartyContactType.Email,
                    Label = "Primary email",
                    Value = email,
                    NormalizedValue = email.ToLowerInvariant(),
                    IsPrimary = true,
                    IsPublic = true
                }
            ]
        });

        Assert.True(result.IsSuccess);
        return result.Value;
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

    private static async Task<string> ReadInnerTextIfPresentAsync(ILocator locator)
    {
        return await locator.CountAsync() > 0
            ? (await locator.First.InnerTextAsync()).Trim()
            : string.Empty;
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

    private sealed record SeededWorkforceParties(Guid ManagerId, Guid WorkerId, Guid UnitId);
}
