using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Modules.TestLab;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Collection(PlaywrightCollection.Name)]
public sealed class CrmHrCrossModuleFlowTests
{
    private readonly PlaywrightAppFixture fixture;

    public CrmHrCrossModuleFlowTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Quarantined")]
    public async Task Cross_module_routes_surface_search_activity_ownership_and_automation_signals()
    {
        var evidenceDirectory = @"C:\repositories\CanDoItAll\evidence\crm-hr\b11";
        Directory.CreateDirectory(evidenceDirectory);

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var seed = await SeedScenarioAsync(suffix);
        var resourceName = $"B11 Resource {suffix}";
        var testPlanTitle = $"B11 Test Plan {suffix}";

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1600,
                Height = 1000
            }
        });
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/crm-hr/crm?accountId={seed.AccountId:D}");
        await DismissStartupModalIfPresentAsync(page);
        await page.WaitForSelectorAsync($"text={seed.InteractionSubject}");
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-account-b11-desktop.png"),
            FullPage = true
        });
        await page.GetByTestId("crmhr-overdue-action-item").WaitForAsync();
        await page.WaitForSelectorAsync($"text={seed.NextActionText}");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());

        await page.GotoAsync($"{fixture.BaseUrl}/crm-hr/directory?partyId={seed.CandidateId:D}");
        await page.GetByTestId("crmhr-party-display-name").WaitForAsync();
        await ExpectInputValueContainsAsync(page.GetByTestId("crmhr-party-display-name"), seed.CandidateName);
        await page.GetByTestId("crmhr-directory-tab-activity").ClickAsync();
        await page.GetByTestId("crmhr-party-assignment-item").WaitForAsync();
        await page.WaitForSelectorAsync($"text={seed.ProjectName}");
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-directory-b11-desktop.png"),
            FullPage = true
        });
        await page.SetViewportSizeAsync(1100, 900);
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-directory-b11-tablet.png"),
            FullPage = true
        });
        await page.SetViewportSizeAsync(1600, 1000);
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());

        await page.GotoAsync($"{fixture.BaseUrl}/resources?projectId={seed.ProjectId:D}");
        await page.GetByTestId("resource-project-select").WaitForAsync();
        await WaitForSelectOptionAsync(page.GetByTestId("resource-owner-select"), seed.OwnerId.ToString());
        await WaitForSelectOptionAsync(page.GetByTestId("resource-maintainer-select"), seed.MaintainerId.ToString());
        await page.GetByTestId("resource-name-input").FillAsync(resourceName);
        await page.GetByTestId("resource-primary-input").FillAsync($"https://example.test/b11/{suffix}.git");
        await page.GetByTestId("resource-owner-select").SelectOptionAsync(seed.OwnerId.ToString());
        await page.GetByTestId("resource-maintainer-select").SelectOptionAsync(seed.MaintainerId.ToString());
        await page.GetByTestId("resource-save-button").ClickAsync();
        await page.WaitForSelectorAsync("text=Resource saved.");
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-resources-b11-desktop.png"),
            FullPage = true
        });
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());

        await page.GotoAsync($"{fixture.BaseUrl}/test-lab?projectId={seed.ProjectId:D}");
        await page.GetByTestId("testlab-project-select").WaitForAsync();
        await WaitForSelectOptionAsync(page.GetByTestId("testlab-responsible-party-select"), seed.OwnerId.ToString());
        await page.GetByTestId("testlab-responsible-party-select").SelectOptionAsync(seed.OwnerId.ToString());
        await page.GetByTestId("testlab-title-input").FillAsync(testPlanTitle);
        await page.GetByTestId("testlab-save-button").ClickAsync();
        await page.WaitForSelectorAsync("text=Test plan saved.");
        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-testlab-b11-desktop.png"),
            FullPage = true
        });
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());

        await page.GotoAsync($"{fixture.BaseUrl}/crm-hr/recruiting");
        await page.GetByTestId("crmhr-recruiting-applications-search").WaitForAsync();
        await page.GetByTestId("crmhr-recruiting-applications-search").FillAsync(seed.CandidateName);
        await page.GetByRole(
                AriaRole.Button,
                new PageGetByRoleOptions
                {
                    Name = $"Select {seed.CandidateName}",
                    Exact = true
                })
            .ClickAsync();
        await page.GetByTestId("crmhr-recruiting-record-dialog").WaitForAsync();
        await page.GetByTestId("crmhr-recruiting-tab-conversion").ClickAsync();
        await page.GetByTestId("crmhr-recruiting-convert-existing-callout").WaitForAsync();
        await page.GetByTestId("crmhr-recruiting-tab-lifecycle").ClickAsync();
        await page.WaitForSelectorAsync($"text={seed.LifecycleTaskTitle}");
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());

        await VerifyPersistedStateAsync(seed, resourceName, testPlanTitle);
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
        var projectPartyBridge = scope.ServiceProvider.GetRequiredService<IProjectPartyIntegrationBridge>();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var crmService = scope.ServiceProvider.GetRequiredService<CrmService>();
        var recruitingService = scope.ServiceProvider.GetRequiredService<RecruitingService>();

        var projectName = $"B11 Project {suffix}";
        var ownerName = $"B11 Owner {suffix}";
        var maintainerName = $"B11 Maintainer {suffix}";
        var recruiterName = $"B11 Recruiter {suffix}";
        var managerName = $"B11 Manager {suffix}";
        var candidateName = $"B11 Candidate {suffix}";
        var unitName = $"B11 Delivery {suffix}";
        var accountName = $"B11 Account {suffix}";
        var interactionSubject = $"B11 Quarterly expansion review {suffix}";
        var nextActionText = $"Send pricing update {suffix}";
        var lifecycleTaskTitle = $"Prepare laptop and VPN {suffix}";

        var projectId = await CreateProjectAsync(projectsService, projectName);
        var ownerId = await CreatePartyAsync(
            partyDirectoryService,
            ownerName,
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Stakeholder,
            $"owner.{suffix}@example.test");
        var maintainerId = await CreatePartyAsync(
            partyDirectoryService,
            maintainerName,
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Employee,
            $"maintainer.{suffix}@example.test");
        var recruiterId = await CreatePartyAsync(
            partyDirectoryService,
            recruiterName,
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Recruiter,
            $"recruiter.{suffix}@example.test");
        var managerId = await CreatePartyAsync(
            partyDirectoryService,
            managerName,
            PartyType.Person,
            PartyLifecycleStatus.Active,
            PartyRoleKind.Stakeholder,
            $"manager.{suffix}@example.test");
        var candidateId = await CreatePartyAsync(
            partyDirectoryService,
            candidateName,
            PartyType.Person,
            PartyLifecycleStatus.Candidate,
            PartyRoleKind.Candidate,
            $"candidate.{suffix}@example.test");
        var unitId = await CreatePartyAsync(
            partyDirectoryService,
            unitName,
            PartyType.OrganizationUnit,
            PartyLifecycleStatus.Active,
            PartyRoleKind.DeliveryUnit,
            $"unit.{suffix}@example.test");
        var accountId = await CreatePartyAsync(
            partyDirectoryService,
            accountName,
            PartyType.Organization,
            PartyLifecycleStatus.Prospect,
            PartyRoleKind.Customer,
            $"account.{suffix}@example.test");

        await SaveAssignmentAsync(projectPartyBridge, projectId, ownerId, ProjectPartyAssignmentRole.TeamMember, "b11-owner", 100m, true);
        await SaveAssignmentAsync(projectPartyBridge, projectId, maintainerId, ProjectPartyAssignmentRole.Manager, "b11-maintainer", 60m, false);
        await SaveAssignmentAsync(projectPartyBridge, projectId, candidateId, ProjectPartyAssignmentRole.TeamMember, "b11-delivery", 80m, true);

        var accountProfileResult = await crmService.SaveAccountProfileAsync(new CrmAccountProfileEditorModel
        {
            AccountPartyId = accountId,
            RelationshipStage = CrmAccountRelationshipStage.ActiveCustomer,
            CommercialNotes = "Cross-module B11 account proof.",
            ConstraintNotes = "No blockers.",
            TimingRiskNotes = "Follow-up depends on response timing.",
            LastChangedBy = "playwright-tests"
        });
        Assert.True(accountProfileResult.IsSuccess);

        var interactionResult = await crmService.AddInteractionAsync(
            accountId,
            new CrmInteractionEditorModel
            {
                InteractionType = InteractionType.Meeting,
                Subject = interactionSubject,
                Summary = "Confirmed follow-up ownership for B11 proof.",
                Notes = "Pricing and staffing input are both required.",
                NextActionText = nextActionText,
                NextActionOwnerPartyId = recruiterId,
                NextActionDueOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
                ParticipantPartyIds = [recruiterId]
            },
            "playwright-tests");
        Assert.True(interactionResult.IsSuccess);

        var applicationResult = await recruitingService.SaveRecruitmentApplicationAsync(new RecruitmentApplicationEditorModel
        {
            PartyId = candidateId,
            RecruiterPartyId = recruiterId,
            HiringManagerPartyId = managerId,
            TargetUnitPartyId = unitId,
            DesiredRole = "Senior Platform Engineer",
            Source = "Referral",
            Stage = RecruitmentStage.Interviewing,
            Notes = "Cross-module B11 recruiting proof.",
            LastChangedBy = "playwright-tests"
        });
        Assert.True(applicationResult.IsSuccess);

        var taskResult = await recruitingService.SaveLifecycleTaskAsync(new LifecycleTaskEditorModel
        {
            PartyId = candidateId,
            TaskKind = LifecycleTaskKind.Onboarding,
            Title = lifecycleTaskTitle,
            OwnerPartyId = managerId,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            Status = LifecycleTaskStatus.NotStarted,
            Notes = "Provision baseline delivery access."
        });
        Assert.True(taskResult.IsSuccess);

        var convertResult = await recruitingService.ConvertCandidateAsync(new RecruitmentConversionEditorModel
        {
            ApplicationId = applicationResult.Value,
            WorkforceKind = WorkforceKind.Employee,
            JobTitle = "Senior Platform Engineer",
            Discipline = "Platform",
            Seniority = "Senior",
            HomeUnitPartyId = unitId,
            ManagerPartyId = managerId,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            Location = "Remote",
            TimeZone = "UTC",
            CapacityHoursPerWeek = 40m,
            Status = "Active",
            Notes = "Converted during B11 Playwright proof.",
            LastChangedBy = "playwright-tests"
        });
        Assert.True(convertResult.IsSuccess);

        return new SeededScenario(
            projectId,
            projectName,
            ownerId,
            ownerName,
            maintainerId,
            maintainerName,
            candidateId,
            candidateName,
            accountId,
            accountName,
            interactionSubject,
            nextActionText,
            lifecycleTaskTitle);
    }

    private async Task VerifyPersistedStateAsync(SeededScenario seed, string resourceName, string testPlanTitle)
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
        var resourcesService = scope.ServiceProvider.GetRequiredService<ResourcesService>();
        var testLabService = scope.ServiceProvider.GetRequiredService<TestLabService>();

        var savedResourceSummary = Assert.Single(await resourcesService.ListAsync(), item => item.Name == resourceName);
        var savedResource = await resourcesService.GetAsync(savedResourceSummary.Id);
        Assert.Equal(seed.OwnerId, savedResource.OwnerPartyId);
        Assert.Equal(seed.MaintainerId, savedResource.MaintainerPartyId);

        var savedPlanSummary = Assert.Single(await testLabService.ListAsync(), item => item.Title == testPlanTitle);
        var savedPlan = await testLabService.GetAsync(savedPlanSummary.Id);
        Assert.Equal(seed.OwnerId, savedPlan.ResponsiblePartyId);
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
            Notes = $"B11 assignment for {role}",
            IsPrimary = isPrimary,
            Source = "playwright-tests"
        });

        Assert.True(result.IsSuccess);
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

    private sealed record SeededScenario(
        Guid ProjectId,
        string ProjectName,
        Guid OwnerId,
        string OwnerName,
        Guid MaintainerId,
        string MaintainerName,
        Guid CandidateId,
        string CandidateName,
        Guid AccountId,
        string AccountName,
        string InteractionSubject,
        string NextActionText,
        string LifecycleTaskTitle);
}
