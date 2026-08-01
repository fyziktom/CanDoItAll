using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Collection(PlaywrightCollection.Name)]
[Trait("Category", "Quarantined")]
public sealed class RecruitmentFlowTests
{
    private readonly PlaywrightAppFixture fixture;

    public RecruitmentFlowTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Recruiting_pipeline_interviews_support_tasks_and_conversion_flow_on_recruiting_route()
    {
        var evidenceDirectory = @"C:\repositories\CanDoItAll\evidence\crm-hr\b08";
        Directory.CreateDirectory(evidenceDirectory);

        var suffix = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
        var seed = await SeedScenarioAsync(suffix);

        await using var context = await fixture.Browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize
            {
                Width = 1800,
                Height = 1100
            }
        });
        var page = await context.NewPageAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/crm-hr/recruiting");
        await DismissStartupModalIfPresentAsync(page);
        await page.GetByTestId("crmhr-recruiting-new-button").ClickAsync();
        await page.GetByTestId("crmhr-recruiting-record-dialog").WaitForAsync();
        await page.GetByTestId("crmhr-recruiting-candidate-name").WaitForAsync();

        await page.GetByTestId("crmhr-recruiting-candidate-name").FillAsync(seed.CandidateName);
        await page.GetByTestId("crmhr-recruiting-candidate-email").FillAsync($"{seed.CandidateKey}@example.test");
        await page.GetByTestId("crmhr-recruiting-candidate-phone").FillAsync("+1 555 0202");
        await page.GetByTestId("crmhr-recruiting-candidate-summary").FillAsync("Playwright recruiting candidate");
        await page.GetByTestId("crmhr-recruiting-role").FillAsync("Senior Platform Engineer");
        await page.GetByTestId("crmhr-recruiting-application-tab-stage").ClickAsync();
        await page.GetByTestId("crmhr-recruiting-source").FillAsync("Referral");
        await page.GetByTestId("crmhr-recruiting-application-tab-ownership").ClickAsync();
        await ChoosePartyAsync(page, "crmhr-recruiting-recruiter", seed.RecruiterId);
        await ChoosePartyAsync(page, "crmhr-recruiting-hiring-manager", seed.HiringManagerId);
        await ChoosePartyAsync(page, "crmhr-recruiting-target-unit", seed.TargetUnitId);
        await page.GetByTestId("crmhr-recruiting-save-button").ClickAsync();
        await page.GetByTestId("crmhr-recruiting-record-dialog").WaitForAsync();
        await page.GetByTestId("crmhr-recruiting-stage-history-item").WaitForAsync();

        await page.GetByTestId("crmhr-recruiting-application-tab-stage").ClickAsync();
        await page.GetByTestId("crmhr-recruiting-stage").SelectOptionAsync(RecruitmentStage.Interviewing.ToString());
        await page.GetByTestId("crmhr-recruiting-application-tab-notes").ClickAsync();
        await page.GetByTestId("crmhr-recruiting-stage-notes").FillAsync("Move candidate into active interview loop.");
        await page.GetByTestId("crmhr-recruiting-save-button").ClickAsync();

        await page.GetByTestId("crmhr-recruiting-tab-interviews").ClickAsync();
        await page.GetByTestId("crmhr-recruiting-interview-scheduled").FillAsync("2026-04-15T10:30");
        await ChoosePartyAsync(page, "crmhr-recruiting-interviewer", seed.HiringManagerId);
        await page.GetByTestId("crmhr-recruiting-interview-outcome").SelectOptionAsync(RecruitmentInterviewOutcome.Yes.ToString());
        await page.GetByTestId("crmhr-recruiting-interview-recommendation").FillAsync("Proceed to offer");
        await page.GetByTestId("crmhr-recruiting-interview-feedback").FillAsync("Strong system design and delivery fit.");
        await page.GetByTestId("crmhr-recruiting-interview-save-button").ClickAsync();
        await page.GetByTestId("crmhr-recruiting-interview-item").WaitForAsync();

        await page.GetByTestId("crmhr-recruiting-tab-development").ClickAsync();
        await ChoosePartyAsync(page, "crmhr-recruiting-support-manager", seed.HiringManagerId);
        await ChoosePartyAsync(page, "crmhr-recruiting-support-buddy", seed.BuddyId);
        await ChoosePartyAsync(page, "crmhr-recruiting-support-mentor", seed.MentorId);
        await page.GetByTestId("crmhr-recruiting-support-save-button").ClickAsync();

        await page.GetByTestId("crmhr-recruiting-task-kind").SelectOptionAsync(LifecycleTaskKind.Onboarding.ToString());
        await page.GetByTestId("crmhr-recruiting-task-title").FillAsync("Prepare equipment and access");
        await ChoosePartyAsync(page, "crmhr-recruiting-task-owner", seed.HiringManagerId);
        await page.GetByTestId("crmhr-recruiting-task-due-date").FillAsync("2026-04-20");
        await ChooseProjectAsync(page, "crmhr-recruiting-task-project", seed.ProjectId);
        await page.GetByTestId("crmhr-recruiting-task-notes").FillAsync("Provision laptop, VPN, and starter access.");
        await page.GetByTestId("crmhr-recruiting-task-save-button").ClickAsync();
        await page.GetByTestId("crmhr-recruiting-task-item").WaitForAsync();

        await page.GetByTestId("crmhr-recruiting-tab-conversion").ClickAsync();
        await page.GetByTestId("crmhr-recruiting-convert-job-title").FillAsync("Senior Platform Engineer");
        await page.GetByTestId("crmhr-recruiting-convert-discipline").FillAsync("Platform");
        await page.GetByTestId("crmhr-recruiting-convert-seniority").FillAsync("Senior");
        await ChoosePartyAsync(page, "crmhr-recruiting-convert-home-unit", seed.TargetUnitId);
        await ChoosePartyAsync(page, "crmhr-recruiting-convert-manager", seed.HiringManagerId);
        await page.GetByTestId("crmhr-recruiting-convert-start-date").FillAsync("2026-05-01");
        await page.GetByTestId("crmhr-recruiting-convert-location").FillAsync("Remote");
        await page.GetByTestId("crmhr-recruiting-convert-timezone").FillAsync("Europe/Prague");
        await page.GetByTestId("crmhr-recruiting-convert-capacity").FillAsync("40");
        await page.GetByTestId("crmhr-recruiting-convert-save-button").ClickAsync();
        await page.GetByTestId("crmhr-recruiting-convert-existing-callout").WaitForAsync();

        await page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine(evidenceDirectory, "crm-hr-recruiting-b08-desktop.png"),
            FullPage = true
        });

        await VerifyPersistedStateAsync(seed);
        Assert.False(await page.Locator("#blazor-error-ui").IsVisibleAsync());
    }

    private static async Task ChoosePartyAsync(
        IPage page,
        string testIdPrefix,
        Guid partyId)
    {
        await page.GetByTestId($"{testIdPrefix}-select").ClickAsync();
        await page.GetByTestId($"crmhr-party-option-{partyId:N}").ClickAsync();
        await page.GetByTestId($"{testIdPrefix}-dialog-confirm").ClickAsync();
    }

    private static async Task ChooseProjectAsync(
        IPage page,
        string testIdPrefix,
        Guid projectId)
    {
        await page.GetByTestId($"{testIdPrefix}-select").ClickAsync();
        await page.GetByTestId($"crmhr-project-option-{projectId:N}").ClickAsync();
        await page.GetByTestId($"{testIdPrefix}-dialog-confirm").ClickAsync();
    }

    private async Task VerifyPersistedStateAsync(SeededScenario seed)
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
        var recruitingService = scope.ServiceProvider.GetRequiredService<RecruitingService>();
        var hrService = scope.ServiceProvider.GetRequiredService<HrService>();

        var candidate = Assert.Single(await partyDirectoryService.ListDirectoryAsync(), item => item.DisplayName == seed.CandidateName);

        var applications = await recruitingService.SearchRecruitmentApplicationsAsync(
            new RecruitmentApplicationQuery(
                seed.CandidateName,
                PageSize: RecruitmentApplicationQueryLimits.MaximumPageSize));
        var application = Assert.Single(applications.Items, item => item.CandidateName == seed.CandidateName);
        var workspace = await recruitingService.GetRecruitmentWorkspaceAsync(application.Id);

        Assert.True(workspace.HasWorkforceProfile);
        Assert.Equal(RecruitmentStage.Hired, workspace.Application.Stage);
        Assert.Single(workspace.Interviews);
        Assert.Single(workspace.LifecycleTasks);
        Assert.Equal(seed.HiringManagerId, workspace.SupportAssignments.ManagerPartyId);
        Assert.Equal(seed.BuddyId, workspace.SupportAssignments.BuddyPartyId);
        Assert.Equal(seed.MentorId, workspace.SupportAssignments.MentorPartyId);

        var workforce = await hrService.GetWorkforceWorkspaceAsync(candidate.Id);
        Assert.NotNull(workforce);
        Assert.Equal("Senior Platform Engineer", workforce.Profile.JobTitle);
        Assert.Equal(seed.TargetUnitId, workforce.Profile.HomeUnitPartyId);
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
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var projectsService = scope.ServiceProvider.GetRequiredService<ProjectsService>();

        var candidateKey = $"b08-candidate-{suffix}";
        var candidateName = $"B08 Candidate {suffix}";
        var projectId = await CreateProjectAsync(projectsService, $"B08 Project {suffix}");
        var recruiterId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, $"B08 Recruiter {suffix}");
        var hiringManagerId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, $"B08 Hiring {suffix}");
        var buddyId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, $"B08 Buddy {suffix}");
        var mentorId = await CreatePartyAsync(partyDirectoryService, PartyType.Person, $"B08 Mentor {suffix}");
        var targetUnitId = await CreatePartyAsync(partyDirectoryService, PartyType.OrganizationUnit, $"B08 Unit {suffix}");

        return new SeededScenario(candidateKey, candidateName, recruiterId, hiringManagerId, buddyId, mentorId, targetUnitId, projectId);
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
        string CandidateKey,
        string CandidateName,
        Guid RecruiterId,
        Guid HiringManagerId,
        Guid BuddyId,
        Guid MentorId,
        Guid TargetUnitId,
        Guid ProjectId);
}
