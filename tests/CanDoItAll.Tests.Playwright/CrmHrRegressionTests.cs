using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Modules.Validation;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Collection(PlaywrightCollection.Name)]
public sealed class CrmHrRegressionTests
{
    private readonly PlaywrightAppFixture fixture;

    public CrmHrRegressionTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Final_crm_hr_regression_gate_keeps_core_routes_readable_and_persistent()
    {
        var evidenceDirectory = @"C:\repositories\CanDoItAll\evidence\crm-hr\b13";
        Directory.CreateDirectory(evidenceDirectory);

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var seed = await SeedScenarioAsync(suffix);
        var resourceName = $"B13 Resource {suffix}";
        var validationTitle = $"B13 Validation {suffix}";
        var testPlanTitle = $"B13 Test Plan {suffix}";

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1600,
                Height = 1000
            }
        });
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/crm-hr");
        await DismissStartupModalIfPresentAsync(page);
        await page.GetByTestId("crmhr-home-sensitive-card").WaitForAsync();
        await ExpectTextContainsAsync(page.GetByTestId("crmhr-home-sensitive-card"), seed.SensitivePartyName);
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-home-b13-desktop.png"),
            FullPage = true
        });

        await page.SetViewportSizeAsync(1100, 900);
        await page.GetByTestId("crmhr-home-sensitive-card").WaitForAsync();
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-home-b13-tablet.png"),
            FullPage = true
        });
        await page.SetViewportSizeAsync(1600, 1000);

        await page.GotoAsync($"{fixture.BaseUrl}/projects");
        await page.GetByTestId("projects-board").WaitForAsync();
        await page.Locator("input[placeholder='Search by name or current phase']").FillAsync(seed.ProjectName);
        var projectCard = page.GetByTestId("project-card")
            .Filter(new LocatorFilterOptions
            {
                HasText = seed.ProjectName
            });
        await projectCard.WaitForAsync();
        await ExpectTextContainsAsync(projectCard, seed.ProjectName);
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-projects-b13-desktop.png"),
            FullPage = true
        });

        await page.GotoAsync($"{fixture.BaseUrl}/activity");
        await page.GetByTestId("activity-search-input").WaitForAsync();
        await page.GetByTestId("activity-search-input").FillAsync(seed.InteractionSubject);
        await page.GetByRole(AriaRole.Button, new PageGetByRoleOptions
        {
            Name = "Search",
            Exact = true
        }).ClickAsync();
        await page.WaitForSelectorAsync($"text={seed.InteractionSubject}");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-activity-b13-desktop.png"),
            FullPage = true
        });

        await page.GotoAsync($"{fixture.BaseUrl}/resources?projectId={seed.ProjectId:D}");
        await page.GetByTestId("resource-project-select").WaitForAsync();
        await WaitForSelectOptionAsync(page.GetByTestId("resource-owner-select"), seed.OwnerId.ToString());
        await WaitForSelectOptionAsync(page.GetByTestId("resource-maintainer-select"), seed.MaintainerId.ToString());
        await page.GetByTestId("resource-name-input").FillAsync(resourceName);
        await page.GetByTestId("resource-primary-input").FillAsync($"https://example.test/b13/{suffix}.git");
        await page.GetByTestId("resource-owner-select").SelectOptionAsync(seed.OwnerId.ToString());
        await page.GetByTestId("resource-maintainer-select").SelectOptionAsync(seed.MaintainerId.ToString());
        await page.GetByTestId("resource-save-button").ClickAsync();
        await page.WaitForSelectorAsync("text=Resource saved.");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-resources-b13-desktop.png"),
            FullPage = true
        });

        await page.GotoAsync($"{fixture.BaseUrl}/validation?projectId={seed.ProjectId:D}");
        await page.GetByTestId("validation-project-select").WaitForAsync();
        await WaitForSelectOptionAsync(page.GetByTestId("validation-responsible-party-select"), seed.OwnerId.ToString());
        await page.GetByTestId("validation-responsible-party-select").SelectOptionAsync(seed.OwnerId.ToString());
        await page.GetByTestId("validation-artifact-title-input").FillAsync(validationTitle);
        await page.GetByTestId("validation-source-content-input").FillAsync("B13 regression validation source content.");
        await page.GetByTestId("validation-run-button").ClickAsync();
        await page.WaitForSelectorAsync("text=Validation completed.");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-validation-b13-desktop.png"),
            FullPage = true
        });

        await page.GotoAsync($"{fixture.BaseUrl}/test-lab?projectId={seed.ProjectId:D}");
        await page.GetByTestId("testlab-project-select").WaitForAsync();
        await WaitForSelectOptionAsync(page.GetByTestId("testlab-responsible-party-select"), seed.OwnerId.ToString());
        await page.GetByTestId("testlab-responsible-party-select").SelectOptionAsync(seed.OwnerId.ToString());
        await page.GetByTestId("testlab-title-input").FillAsync(testPlanTitle);
        await page.GetByTestId("testlab-save-button").ClickAsync();
        await page.WaitForSelectorAsync("text=Test plan saved.");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-testlab-b13-desktop.png"),
            FullPage = true
        });

        await VerifyPersistedStateAsync(seed, resourceName, validationTitle, testPlanTitle);
    }

    private async Task<SeededScenario> SeedScenarioAsync(string suffix)
    {
        var activeProfile = CreateActiveProfile();
        await using var serviceProvider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            activeProfile,
            "CanDoItAll.Tests.Playwright.B13.Seed",
            TestSchemaBootstrapModules.Full,
            new Dictionary<string, string?>
            {
                ["DevelopmentManager:TuningModeEnabled"] = "false"
            });
        await using var scope = serviceProvider.CreateAsyncScope();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();
        var projectPartyBridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var crmService = scope.ServiceProvider.GetRequiredService<CrmService>();

        var projectName = $"B13 Project {suffix}";
        var customerName = $"B13 Customer {suffix}";
        var deliveryUnitName = $"B13 Delivery {suffix}";
        var ownerName = $"B13 Owner {suffix}";
        var maintainerName = $"B13 Maintainer {suffix}";
        var accountName = $"B13 Account {suffix}";
        var sensitivePartyName = $"B13 Sensitive {suffix}";
        var interactionSubject = $"B13 Activity Search {suffix}";

        var projectId = await CreateProjectAsync(projectsService, projectName);
        var customerId = await CreatePartyAsync(
            partyDirectoryService,
            customerName,
            PartyType.Organization,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Customer,
            $"customer.{suffix}@example.test",
            isSensitive: false,
            confidentialNote: null);
        var deliveryUnitId = await CreatePartyAsync(
            partyDirectoryService,
            deliveryUnitName,
            PartyType.OrganizationUnit,
            PartyLifecycleStatus.Active,
            PartyRoleKind.DeliveryUnit,
            $"delivery.{suffix}@example.test",
            isSensitive: false,
            confidentialNote: null);
        var ownerId = await CreatePartyAsync(
            partyDirectoryService,
            ownerName,
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Stakeholder,
            $"owner.{suffix}@example.test",
            isSensitive: false,
            confidentialNote: null);
        var maintainerId = await CreatePartyAsync(
            partyDirectoryService,
            maintainerName,
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Employee,
            $"maintainer.{suffix}@example.test",
            isSensitive: false,
            confidentialNote: null);
        var accountId = await CreatePartyAsync(
            partyDirectoryService,
            accountName,
            PartyType.Organization,
            PartyLifecycleStatus.Prospect,
            PartyRoleKind.Customer,
            $"account.{suffix}@example.test",
            isSensitive: false,
            confidentialNote: null);
        var sensitivePartyId = await CreatePartyAsync(
            partyDirectoryService,
            sensitivePartyName,
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Employee,
            $"sensitive.{suffix}@example.test",
            isSensitive: true,
            confidentialNote: $"Confidential regression note {suffix}");

        await SaveAssignmentAsync(projectPartyBridge, projectId, customerId, ProjectPartyAssignmentRole.Customer, "b13-customer", 100m, true);
        await SaveAssignmentAsync(projectPartyBridge, projectId, deliveryUnitId, ProjectPartyAssignmentRole.DeliveryUnit, "b13-delivery", 70m, true);
        await SaveAssignmentAsync(projectPartyBridge, projectId, ownerId, ProjectPartyAssignmentRole.Manager, "b13-owner", 40m, true);

        var accountProfileResult = await crmService.SaveAccountProfileAsync(new CrmAccountProfileEditorModel
        {
            AccountPartyId = accountId,
            RelationshipStage = CrmAccountRelationshipStage.ActiveCustomer,
            CommercialNotes = "B13 regression account proof.",
            ConstraintNotes = "No blockers.",
            TimingRiskNotes = "No timing risk.",
            LastChangedBy = "playwright-tests"
        });
        Assert.True(accountProfileResult.IsSuccess);

        var interactionResult = await crmService.AddInteractionAsync(
            accountId,
            new CrmInteractionEditorModel
            {
                InteractionType = InteractionType.Meeting,
                Subject = interactionSubject,
                Summary = "B13 regression activity proof.",
                Notes = "Final gate activity note.",
                ParticipantPartyIds = [ownerId]
            },
            "playwright-tests");
        Assert.True(interactionResult.IsSuccess);

        return new SeededScenario(
            ProjectId: projectId,
            ProjectName: projectName,
            OwnerId: ownerId,
            MaintainerId: maintainerId,
            SensitivePartyId: sensitivePartyId,
            SensitivePartyName: sensitivePartyName,
            InteractionSubject: interactionSubject);
    }

    private async Task VerifyPersistedStateAsync(SeededScenario seed, string resourceName, string validationTitle, string testPlanTitle)
    {
        var activeProfile = CreateActiveProfile();
        await using var serviceProvider = await TestApplicationBootstrap.BuildServiceProviderAsync(
            activeProfile,
            "CanDoItAll.Tests.Playwright.B13.Verify",
            TestSchemaBootstrapModules.Full,
            new Dictionary<string, string?>
            {
                ["DevelopmentManager:TuningModeEnabled"] = "false"
            });
        await using var scope = serviceProvider.CreateAsyncScope();
        var resourcesService = scope.ServiceProvider.GetRequiredService<ResourcesService>();
        var validationService = scope.ServiceProvider.GetRequiredService<ValidationService>();
        var testLabService = scope.ServiceProvider.GetRequiredService<TestLabService>();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var searchIndexService = scope.ServiceProvider.GetRequiredService<ISearchIndexService>();

        var savedResourceSummary = Assert.Single(await resourcesService.ListAsync(), item => item.Name == resourceName);
        var savedResource = await resourcesService.GetAsync(savedResourceSummary.Id);
        Assert.Equal(seed.OwnerId, savedResource.OwnerPartyId);
        Assert.Equal(seed.MaintainerId, savedResource.MaintainerPartyId);

        var savedValidationSummary = Assert.Single(await validationService.ListRunsAsync(), item => item.ArtifactTitle == validationTitle);
        var savedValidation = await validationService.GetRunAsync(savedValidationSummary.Id);
        Assert.Equal(validationTitle, savedValidation.ArtifactTitle);

        var savedPlanSummary = Assert.Single(await testLabService.ListAsync(), item => item.Title == testPlanTitle);
        var savedPlan = await testLabService.GetAsync(savedPlanSummary.Id);
        Assert.Equal(testPlanTitle, savedPlan.Title);

        var sensitiveParty = await partyDirectoryService.GetPartyAsync(seed.SensitivePartyId);
        Assert.NotNull(sensitiveParty);
        Assert.True(sensitiveParty.IsSensitive);
        Assert.NotEmpty(sensitiveParty.ConfidentialNotes);

        Assert.Empty(await searchIndexService.SearchAsync(seed.SensitivePartyName));
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

    private static async Task<Guid> CreateProjectAsync(ProjectsService projectsService, string name)
    {
        var result = await projectsService.SaveAsync(new ProjectEditorModel
        {
            Name = name,
            Description = $"{name} description",
            Objective = $"{name} objective",
            CurrentPhase = "Regression gate"
        });

        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private static async Task<Guid> CreatePartyAsync(
        PartyDirectoryService partyDirectoryService,
        string displayName,
        PartyType partyType,
        PartyLifecycleStatus lifecycleStatus,
        PartyRoleKind roleKind,
        string email,
        bool isSensitive,
        string? confidentialNote)
    {
        var result = await partyDirectoryService.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = lifecycleStatus,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            Notes = $"{displayName} operational note",
            IsSensitive = isSensitive,
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
            ],
            ConfidentialNotes = string.IsNullOrWhiteSpace(confidentialNote)
                ? []
                :
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

    private static async Task SaveAssignmentAsync(
        IProjectPartyIntegrationBridge projectPartyBridge,
        Guid projectId,
        Guid partyId,
        ProjectPartyAssignmentRole role,
        string nodeKey,
        decimal allocationPercent,
        bool isPrimary)
    {
        var result = await projectPartyBridge.SaveAssignmentAsync(new ProjectPartyAssignmentUpsertRequest
        {
            ProjectId = projectId,
            PartyId = partyId,
            Role = role,
            NodeKey = nodeKey,
            AllocationPercent = allocationPercent,
            StartsOn = DateOnly.FromDateTime(DateTime.UtcNow.Date),
            EndsOn = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(14)),
            Notes = $"B13 assignment for {role}",
            IsPrimary = isPrimary,
            Source = "playwright-tests"
        });

        Assert.True(result.IsSuccess);
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

    private static async Task WaitForSelectOptionAsync(ILocator select, string optionValue, int timeoutMs = 10_000)
    {
        var timeoutAt = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            var hasOption = await select.EvaluateAsync<bool>(
                @"(element, value) => Array.from(element.options).some(option => option.value === value)",
                optionValue);
            if (hasOption)
            {
                return;
            }

            await Task.Delay(200);
        }

        throw new TimeoutException($"Timed out waiting for select option '{optionValue}'.");
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
        Guid OwnerId,
        Guid MaintainerId,
        Guid SensitivePartyId,
        string SensitivePartyName,
        string InteractionSubject);
}
