using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Collection(PlaywrightCollection.Name)]
public sealed class StaffingFlowTests
{
    private readonly PlaywrightAppFixture fixture;

    public StaffingFlowTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Staffing_requests_allocations_and_capacity_conflicts_flow_between_assignments_and_workforce()
    {
        var evidenceDirectory = @"C:\repositories\CanDoItAll\evidence\crm-hr\b07";
        Directory.CreateDirectory(evidenceDirectory);

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var seed = await SeedScenarioAsync(suffix);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1600,
                Height = 1000
            }
        });
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/crm-hr/assignments?projectId={seed.ProjectId:D}");
        await DismissStartupModalIfPresentAsync(page);
        await page.GetByTestId("crmhr-staffing-request-title").WaitForAsync();

        await page.GetByTestId("crmhr-staffing-request-title").FillAsync("Need senior platform coverage");
        await page.GetByTestId("crmhr-staffing-request-role").FillAsync("Senior Platform Engineer");
        await page.GetByTestId("crmhr-staffing-request-allocation").FillAsync("70");
        await page.GetByTestId("crmhr-staffing-request-requested-by").SelectOptionAsync(seed.RequesterId.ToString());
        await page.GetByTestId("crmhr-staffing-request-delivery-unit").SelectOptionAsync(seed.DeliveryUnitId.ToString());
        await page.Locator("[data-testid='crmhr-staffing-request-skill']").First.CheckAsync();
        await page.GetByTestId("crmhr-staffing-request-save-button").ClickAsync();
        await ExpectTextContainsAsync(page.GetByTestId("crmhr-assignment-message"), "Staffing request saved.");

        await page.GetByTestId("crmhr-allocation-candidate-skill").SelectOptionAsync(seed.SkillId.ToString());
        await page.GetByTestId("crmhr-allocation-candidate-search-button").ClickAsync();
        await page.GetByTestId("crmhr-staffing-candidate-item")
            .Filter(new LocatorFilterOptions
            {
                HasText = seed.WorkerName
            })
            .WaitForAsync();
        await page.GetByTestId("crmhr-staffing-candidate-item")
            .Filter(new LocatorFilterOptions
            {
                HasText = seed.WorkerName
            })
            .GetByRole(AriaRole.Button, new() { Name = "Use candidate", Exact = true })
            .ClickAsync();
        await page.GetByTestId("crmhr-allocation-percent").FillAsync("70");
        await page.GetByTestId("crmhr-allocation-save-button").ClickAsync();
        await ExpectTextContainsAsync(page.GetByTestId("crmhr-assignment-message"), "Project allocation saved.");

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-assignments-b07-desktop.png"),
            FullPage = true
        });

        await page.SetViewportSizeAsync(1100, 900);
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-assignments-b07-tablet.png"),
            FullPage = true
        });

        await page.GotoAsync($"{fixture.BaseUrl}/crm-hr/workforce?partyId={seed.WorkerId:D}");
        await page.GetByTestId("crmhr-capacity-block-save-button").WaitForAsync();
        await page.GetByTestId("crmhr-capacity-block-percentage").FillAsync("40");
        await page.GetByTestId("crmhr-capacity-block-notes").FillAsync("Planned leave");
        await page.GetByTestId("crmhr-capacity-block-save-button").ClickAsync();
        await page.GetByTestId("crmhr-capacity-conflict-callout").WaitForAsync();
        await ExpectTextContainsAsync(page.GetByTestId("crmhr-capacity-conflict-callout"), "Conflict:");
        await page.GetByTestId("crmhr-workforce-allocation-item")
            .Filter(new LocatorFilterOptions
            {
                HasText = seed.ProjectName
            })
            .WaitForAsync();

        await page.SetViewportSizeAsync(1600, 1000);
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-workforce-b07-desktop.png"),
            FullPage = true
        });

        await page.SetViewportSizeAsync(1100, 900);
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-workforce-b07-tablet.png"),
            FullPage = true
        });

        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private async Task<SeededScenario> SeedScenarioAsync(string suffix)
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
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var hrService = scope.ServiceProvider.GetRequiredService<HrService>();

        var projectName = $"B07 Project {suffix}";
        var requesterName = $"B07 Requester {suffix}";
        var workerName = $"B07 Worker {suffix}";
        var deliveryUnitName = $"B07 Delivery {suffix}";

        var projectId = await CreateProjectAsync(projectsService, projectName);
        var requesterId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, requesterName);
        var workerId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, workerName);
        var deliveryUnitId = await CreatePartyAsync(partyDirectoryService, PartyType.OrganizationUnit, deliveryUnitName);

        Assert.True((await hrService.SaveWorkforceProfileAsync(new WorkforceProfileEditorModel
        {
            PartyId = workerId,
            WorkforceKind = WorkforceKind.Employee,
            Status = "Active",
            JobTitle = "Platform Engineer",
            Discipline = "Platform",
            Seniority = "Senior",
            Location = "Remote",
            CapacityHoursPerWeek = 40m,
            LastChangedBy = "playwright-tests"
        })).IsSuccess);

        var saveSkillResult = await hrService.SaveSkillDefinitionAsync(new SkillDefinitionEditorModel
        {
            Name = $"Platform Delivery {suffix}",
            Category = "Engineering",
            Description = "Playwright staffing skill",
            IsActive = true
        });
        Assert.True(saveSkillResult.IsSuccess);
        var skillId = saveSkillResult.Value;

        Assert.True((await hrService.SavePartySkillAsync(new PartySkillEditorModel
        {
            PartyId = workerId,
            SkillId = skillId,
            Proficiency = SkillProficiencyLevel.Expert,
            YearsExperience = 7,
            CertificationStatus = "AWS SA Pro"
        })).IsSuccess);

        return new SeededScenario(projectId, projectName, requesterId, workerId, workerName, deliveryUnitId, skillId);
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

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = $"{name} description",
            Objective = $"{name} objective",
            CurrentPhase = "Discovery"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task<Guid> CreatePartyAsync(PartyDirectoryService partyDirectoryService, PartyType partyType, string displayName)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "playwright-tests"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
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

    private sealed record SeededScenario(
        Guid ProjectId,
        string ProjectName,
        Guid RequesterId,
        Guid WorkerId,
        string WorkerName,
        Guid DeliveryUnitId,
        Guid SkillId);
}
