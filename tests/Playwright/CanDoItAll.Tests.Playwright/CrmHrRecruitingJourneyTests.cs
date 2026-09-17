using System.Globalization;
using System.Text.RegularExpressions;
using CanDoItAll.Modules.CrmHr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright.Smoke;

// Recruiting through the real Web host and PostgreSQL: a cancelled draft that leaves nothing behind, a new application
// for a seeded candidate chosen in the real party picker, an interview, support roles and a lifecycle task with their
// people chosen in the picker, the approval the conversion policy requires, and the conversion to workforce on the
// same party identity. Every write is read back through RecruitingService and HrService by exact identifiers.
[Collection(PlaywrightCollection.Name)]
public sealed class CrmHrRecruitingJourneyTests
{
    private static readonly Regex ApplicationIdInUrl = new("applicationId=([0-9a-fA-F-]{36})");

    private readonly PlaywrightAppFixture fixture;

    public CrmHrRecruitingJourneyTests(PlaywrightAppFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    public async Task Recruiting_application_interview_support_task_and_conversion_stay_on_the_candidate_party()
    {
        var artifactsDir = CrmHrWorkspaceJourneySupport.ArtifactsDirectory("crm-hr-recruiting-journey");
        var suffix = CrmHrWorkspaceJourneySupport.NewSuffix();
        await using var owner = await CrmHrWorkspaceJourneySupport.BuildOwnerProviderAsync(fixture, "playwright-recruiting-journey");
        var seed = await SeedAsync(owner, suffix);

        await CrmHrWorkspaceJourneySupport.RunAsync(fixture, artifactsDir, async (page, oracle) =>
        {
            await CrmHrWorkspaceJourneySupport.NavigateAsync(fixture, page, oracle, "/crm-hr/recruiting");
            await CrmHrWorkspaceJourneySupport.WaitForCatalogAsync(page, "crmhr-recruiting-applications");
            var dialog = page.GetByTestId("crmhr-recruiting-record-dialog");

            // Cancellation: a typed draft that is closed without saving creates neither an application nor a person.
            await page.GetByTestId("crmhr-recruiting-new-button").ClickAsync();
            await Assertions.Expect(dialog).ToBeVisibleAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(dialog.Locator(".cda-dialog__title")).ToHaveTextAsync("New recruitment application");
            await page.GetByTestId("crmhr-recruiting-role").FillAsync(seed.CancelledRole);
            await page.GetByTestId("crmhr-recruiting-candidate-name").FillAsync(seed.CancelledCandidateName);
            await page.GetByTestId("crmhr-recruiting-candidate-name").PressAsync("Tab");
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "01-cancelled-draft-1600.png") });
            await page.GetByTestId("crmhr-recruiting-record-close").ClickAsync();
            await Assertions.Expect(dialog).ToHaveCountAsync(0);
            Assert.Equal(0, await CountApplicationsAsync(owner, seed.CancelledRole));
            Assert.Equal(0, await CountPartiesAsync(owner, seed.CancelledCandidateName));

            // New application: the draft is empty again, the candidate comes from the real picker, one click saves.
            await page.GetByTestId("crmhr-recruiting-new-button").ClickAsync();
            await Assertions.Expect(dialog).ToBeVisibleAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(page.GetByTestId("crmhr-recruiting-role")).ToHaveValueAsync(string.Empty);
            await Assertions.Expect(page.GetByTestId("crmhr-recruiting-candidate-name")).ToHaveValueAsync(string.Empty);
            await page.GetByTestId("crmhr-recruiting-candidate-party-select").ClickAsync();
            await CrmHrWorkspaceJourneySupport.PickPartyAsync(page, "crmhr-recruiting-candidate-party-dialog", seed.CandidateName, seed.CandidateId);
            await Assertions.Expect(page.GetByTestId("crmhr-recruiting-candidate-party-summary")).ToContainTextAsync(seed.CandidateName);
            await page.GetByTestId("crmhr-recruiting-role").FillAsync(seed.RoleTitle);
            await page.GetByTestId("crmhr-recruiting-role").PressAsync("Tab");
            await page.GetByTestId("crmhr-recruiting-application-tab-stage").ClickAsync();
            await page.GetByTestId("crmhr-recruiting-stage").SelectOptionAsync(new[] { RecruitmentStage.Screening.ToString() });
            await page.GetByTestId("crmhr-recruiting-source").FillAsync("Referral");
            await page.GetByTestId("crmhr-recruiting-source").PressAsync("Tab");
            await page.GetByTestId("crmhr-recruiting-save-button").ClickAsync();
            await Assertions.Expect(page).ToHaveURLAsync(ApplicationIdInUrl, new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            var applicationId = Guid.Parse(ApplicationIdInUrl.Match(page.Url).Groups[1].Value);
            await Assertions.Expect(dialog).ToBeVisibleAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(dialog.Locator(".cda-dialog__title")).ToHaveTextAsync(seed.CandidateName);

            var created = await ReadAsync(owner, applicationId);
            Assert.Equal(applicationId, created.Application.Id);
            Assert.Equal(seed.CandidateId, created.Application.PartyId);
            Assert.Equal(seed.RoleTitle, created.Application.DesiredRole);
            Assert.Equal(RecruitmentStage.Screening, created.Application.Stage);
            Assert.Equal(RecruitmentDecision.Pending, created.Application.Decision);
            Assert.Equal("Referral", created.Application.Source);
            Assert.False(created.HasWorkforceProfile);
            Assert.Equal(1, await CountApplicationsAsync(owner, seed.RoleTitle));
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "02-application-created-1600.png") });

            // Constrained width with the application form open.
            await CrmHrWorkspaceJourneySupport.AssertDialogFitsConstrainedWidthAsync(
                page,
                "crmhr-recruiting-record-dialog",
                ["crmhr-recruiting-save-button", "crmhr-recruiting-record-close"],
                Path.Combine(artifactsDir, "03-application-form-1100.png"));

            // Interview: type, local date and time, interviewer through the picker.
            await page.GetByTestId("crmhr-recruiting-tab-interviews").ClickAsync();
            await page.GetByTestId("crmhr-recruiting-interview-type").SelectOptionAsync(new[] { RecruitmentInterviewType.Technical.ToString() });
            await page.GetByTestId("crmhr-recruiting-interview-scheduled").FillAsync(seed.InterviewLocal.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture));
            await page.GetByTestId("crmhr-recruiting-interview-scheduled").PressAsync("Tab");
            await page.GetByTestId("crmhr-recruiting-interviewer-select").ClickAsync();
            await CrmHrWorkspaceJourneySupport.PickPartyAsync(page, "crmhr-recruiting-interviewer-dialog", seed.InterviewerName, seed.InterviewerId);
            await Assertions.Expect(page.GetByTestId("crmhr-recruiting-interviewer-summary")).ToContainTextAsync(seed.InterviewerName);
            await page.GetByTestId("crmhr-recruiting-interview-save-button").ClickAsync();
            await CrmHrWorkspaceJourneySupport.ExpectToastAsync(page, "Interview saved.");

            var interview = Assert.Single((await ReadAsync(owner, applicationId)).Interviews);
            Assert.Equal(applicationId, interview.ApplicationId);
            Assert.Equal(RecruitmentInterviewType.Technical, interview.InterviewType);
            Assert.Equal(seed.InterviewerId, interview.InterviewerPartyId);
            Assert.Equal(new DateTimeOffset(seed.InterviewLocal, TimeZoneInfo.Local.GetUtcOffset(seed.InterviewLocal)).ToUniversalTime(), interview.ScheduledAtUtc);
            await ReenterTabAsync(page, "crmhr-recruiting-tab-application", "crmhr-recruiting-tab-interviews");
            var interviewItem = page.GetByTestId("crmhr-recruiting-interview-item");
            await Assertions.Expect(interviewItem).ToHaveCountAsync(1, new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(interviewItem).ToContainTextAsync(RecruitmentInterviewType.Technical.ToString());
            await Assertions.Expect(interviewItem).ToContainTextAsync(seed.InterviewerName);
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "04-interview-saved-1600.png") });

            // Support roles: manager, buddy and mentor through their pickers, one click saves all three.
            await page.GetByTestId("crmhr-recruiting-tab-development").ClickAsync();
            await page.GetByTestId("crmhr-recruiting-support-manager-select").ClickAsync();
            await CrmHrWorkspaceJourneySupport.PickPartyAsync(page, "crmhr-recruiting-support-manager-dialog", seed.ManagerName, seed.ManagerId);
            await page.GetByTestId("crmhr-recruiting-support-buddy-select").ClickAsync();
            await CrmHrWorkspaceJourneySupport.PickPartyAsync(page, "crmhr-recruiting-support-buddy-dialog", seed.BuddyName, seed.BuddyId);
            await page.GetByTestId("crmhr-recruiting-support-mentor-select").ClickAsync();
            await CrmHrWorkspaceJourneySupport.PickPartyAsync(page, "crmhr-recruiting-support-mentor-dialog", seed.InterviewerName, seed.InterviewerId);
            await page.GetByTestId("crmhr-recruiting-support-save-button").ClickAsync();
            await CrmHrWorkspaceJourneySupport.ExpectToastAsync(page, "Support assignments saved.");

            var support = (await ReadAsync(owner, applicationId)).SupportAssignments;
            Assert.Equal(seed.CandidateId, support.PartyId);
            Assert.Equal(seed.ManagerId, support.ManagerPartyId);
            Assert.Equal(seed.BuddyId, support.BuddyPartyId);
            Assert.Equal(seed.InterviewerId, support.MentorPartyId);
            await ReenterTabAsync(page, "crmhr-recruiting-tab-interviews", "crmhr-recruiting-tab-development");
            await Assertions.Expect(dialog).ToContainTextAsync($"Manager: {seed.ManagerName}");
            await Assertions.Expect(dialog).ToContainTextAsync($"Buddy: {seed.BuddyName}");
            await Assertions.Expect(dialog).ToContainTextAsync($"Mentor: {seed.InterviewerName}");

            // An onboarding task with an owner and a due date.
            await page.GetByTestId("crmhr-recruiting-task-kind").SelectOptionAsync(new[] { LifecycleTaskKind.Onboarding.ToString() });
            await page.GetByTestId("crmhr-recruiting-task-title").FillAsync(seed.TaskTitle);
            await page.GetByTestId("crmhr-recruiting-task-due-date").FillAsync(Iso(seed.TaskDueOn));
            await page.GetByTestId("crmhr-recruiting-task-due-date").PressAsync("Tab");
            await page.GetByTestId("crmhr-recruiting-task-owner-select").ClickAsync();
            await CrmHrWorkspaceJourneySupport.PickPartyAsync(page, "crmhr-recruiting-task-owner-dialog", seed.BuddyName, seed.BuddyId);
            await Assertions.Expect(page.GetByTestId("crmhr-recruiting-task-owner-summary")).ToContainTextAsync(seed.BuddyName);
            await page.GetByTestId("crmhr-recruiting-task-save-button").ClickAsync();
            await CrmHrWorkspaceJourneySupport.ExpectToastAsync(page, "Lifecycle task saved.");

            var task = Assert.Single((await ReadAsync(owner, applicationId)).LifecycleTasks);
            Assert.Equal(seed.CandidateId, task.PartyId);
            Assert.Equal(LifecycleTaskKind.Onboarding, task.TaskKind);
            Assert.Equal(seed.TaskTitle, task.Title);
            Assert.Equal(seed.BuddyId, task.OwnerPartyId);
            Assert.Equal(seed.TaskDueOn, task.DueDate);
            Assert.Equal(LifecycleTaskStatus.NotStarted, task.Status);
            await ReenterTabAsync(page, "crmhr-recruiting-tab-interviews", "crmhr-recruiting-tab-development");
            var taskItem = page.GetByTestId("crmhr-recruiting-task-item");
            await Assertions.Expect(taskItem).ToHaveCountAsync(1, new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(taskItem).ToContainTextAsync(seed.TaskTitle);
            await Assertions.Expect(taskItem).ToContainTextAsync(seed.BuddyName);
            await Assertions.Expect(taskItem).ToContainTextAsync(Iso(seed.TaskDueOn));
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "05-support-and-task-1600.png") });

            // The conversion policy (RecruitmentConversionPolicy): no rejected or withdrawn stage, and an approved
            // decision. While the decision is pending the conversion is refused in the form itself.
            await page.GetByTestId("crmhr-recruiting-tab-conversion").ClickAsync();
            var convertButton = page.GetByTestId("crmhr-recruiting-convert-save-button");
            await Assertions.Expect(convertButton).ToBeDisabledAsync();
            await Assertions.Expect(dialog).ToContainTextAsync("Approve the recruitment decision before converting the candidate to workforce.");

            await page.GetByTestId("crmhr-recruiting-tab-application").ClickAsync();
            await page.GetByTestId("crmhr-recruiting-application-tab-stage").ClickAsync();
            await page.GetByTestId("crmhr-recruiting-stage").SelectOptionAsync(new[] { RecruitmentStage.Offer.ToString() });
            await page.GetByTestId("crmhr-recruiting-decision").SelectOptionAsync(new[] { RecruitmentDecision.Approved.ToString() });
            await page.GetByTestId("crmhr-recruiting-save-button").ClickAsync();
            await CrmHrWorkspaceJourneySupport.ExpectToastAsync(page, "Recruitment application saved.");
            var approved = await ReadAsync(owner, applicationId);
            Assert.Equal(RecruitmentStage.Offer, approved.Application.Stage);
            Assert.Equal(RecruitmentDecision.Approved, approved.Application.Decision);
            Assert.Equal(seed.CandidateId, approved.Application.PartyId);
            await ReenterTabAsync(page, "crmhr-recruiting-tab-interviews", "crmhr-recruiting-tab-application");
            await Assertions.Expect(page.GetByTestId("crmhr-recruiting-stage-history-item").Filter(new() { HasText = $"Moved {seed.CandidateName} to Offer" })).ToHaveCountAsync(1, new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });

            // Conversion to workforce on the same party: one click.
            await page.GetByTestId("crmhr-recruiting-tab-conversion").ClickAsync();
            await Assertions.Expect(convertButton).ToBeEnabledAsync();
            await page.GetByTestId("crmhr-recruiting-convert-kind").SelectOptionAsync(new[] { WorkforceKind.Employee.ToString() });
            await page.GetByTestId("crmhr-recruiting-convert-job-title").FillAsync(seed.JobTitle);
            await page.GetByTestId("crmhr-recruiting-convert-discipline").FillAsync("Platform Engineering");
            await page.GetByTestId("crmhr-recruiting-convert-capacity").FillAsync("36");
            await page.GetByTestId("crmhr-recruiting-convert-capacity").PressAsync("Tab");
            await Assertions.Expect(page.GetByTestId("crmhr-recruiting-convert-manager-summary")).ToContainTextAsync(seed.ManagerName);
            await convertButton.ClickAsync();
            await CrmHrWorkspaceJourneySupport.ExpectToastAsync(page, "Candidate converted to workforce.");

            var workforce = await ReadWorkforceAsync(owner, seed.CandidateId);
            Assert.NotNull(workforce.Profile.Id);
            Assert.Equal(seed.CandidateId, workforce.Profile.PartyId);
            Assert.Equal(WorkforceKind.Employee, workforce.Profile.WorkforceKind);
            Assert.Equal(seed.JobTitle, workforce.Profile.JobTitle);
            Assert.Equal("Platform Engineering", workforce.Profile.Discipline);
            Assert.Equal(36m, workforce.Profile.CapacityHoursPerWeek);
            Assert.Equal(seed.ManagerId, workforce.Profile.ManagerPartyId);
            Assert.Equal(seed.CandidateName, workforce.DisplayName);
            Assert.Equal(PartyLifecycleStatus.Active, workforce.LifecycleStatus);
            var converted = await ReadAsync(owner, applicationId);
            Assert.Equal(RecruitmentStage.Hired, converted.Application.Stage);
            Assert.True(converted.HasWorkforceProfile);
            // No second person was created: the unique candidate name still resolves to exactly the seeded party.
            Assert.Equal(1, await CountPartiesAsync(owner, seed.CandidateName));
            Assert.Equal(1, await CountApplicationsAsync(owner, seed.RoleTitle));
            await ReenterTabAsync(page, "crmhr-recruiting-tab-interviews", "crmhr-recruiting-tab-conversion");
            await Assertions.Expect(page.GetByTestId("crmhr-recruiting-convert-existing-callout")).ToBeVisibleAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "06-converted-1600.png") });

            // Reload the deep link: a fresh document and circuit show the persisted record.
            await CrmHrWorkspaceJourneySupport.NavigateAsync(fixture, page, oracle, $"/crm-hr/recruiting?applicationId={applicationId:D}");
            await CrmHrWorkspaceJourneySupport.WaitForCatalogAsync(page, "crmhr-recruiting-applications");
            await Assertions.Expect(dialog).ToBeVisibleAsync(new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await Assertions.Expect(dialog.Locator(".cda-dialog__title")).ToHaveTextAsync(seed.CandidateName);
            await Assertions.Expect(page.GetByTestId("crmhr-recruiting-role")).ToHaveValueAsync(seed.RoleTitle);
            await Assertions.Expect(dialog).ToContainTextAsync("Workforce profile exists");
            await page.GetByTestId("crmhr-recruiting-application-tab-stage").ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-recruiting-stage")).ToHaveValueAsync(RecruitmentStage.Hired.ToString());
            await Assertions.Expect(page.GetByTestId("crmhr-recruiting-decision")).ToHaveValueAsync(RecruitmentDecision.Approved.ToString());
            await page.GetByTestId("crmhr-recruiting-tab-interviews").ClickAsync();
            await Assertions.Expect(interviewItem).ToContainTextAsync(seed.InterviewerName, new() { Timeout = CrmHrWorkspaceJourneySupport.DefaultTimeoutMs });
            await page.GetByTestId("crmhr-recruiting-tab-development").ClickAsync();
            await Assertions.Expect(dialog).ToContainTextAsync($"Manager: {seed.ManagerName}");
            await Assertions.Expect(taskItem).ToContainTextAsync(seed.TaskTitle);
            await page.GetByTestId("crmhr-recruiting-tab-conversion").ClickAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-recruiting-convert-existing-callout")).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByTestId("crmhr-recruiting-convert-job-title")).ToHaveValueAsync(seed.JobTitle);
            await page.ScreenshotAsync(new PageScreenshotOptions { Path = Path.Combine(artifactsDir, "07-reloaded-application-1600.png") });
        });
    }

    private static string Iso(DateOnly value)
        => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    // At 4a5724977 the record's tabs render the workspace the previous render captured, so the lists of a tab show a
    // saved change only one save later. The receipt of every save is the owner readback; the operator's way to see the
    // current record is to leave the tab and come back, which is what the journey does before it reads the tab.
    private static async Task ReenterTabAsync(IPage page, string otherTabTestId, string tabTestId)
    {
        await page.GetByTestId(otherTabTestId).ClickAsync();
        await Assertions.Expect(page.GetByTestId(otherTabTestId)).ToHaveAttributeAsync("aria-selected", "true");
        await page.GetByTestId(tabTestId).ClickAsync();
        await Assertions.Expect(page.GetByTestId(tabTestId)).ToHaveAttributeAsync("aria-selected", "true");
    }

    private static async Task<RecruitmentWorkspaceModel> ReadAsync(ServiceProvider owner, Guid applicationId)
    {
        await using var scope = owner.CreateAsyncScope();
        var workspace = await scope.ServiceProvider.GetRequiredService<RecruitingService>().GetRecruitmentWorkspaceAsync(applicationId);
        Assert.True(workspace.HasSelectedApplication, $"The owner does not know application '{applicationId:D}'.");
        Assert.Equal(applicationId, workspace.Application.Id);
        return workspace;
    }

    private static async Task<WorkforceProfileWorkspaceModel> ReadWorkforceAsync(ServiceProvider owner, Guid partyId)
    {
        await using var scope = owner.CreateAsyncScope();
        var workspace = await scope.ServiceProvider.GetRequiredService<HrService>().GetWorkforceProfileWorkspaceAsync(partyId);
        Assert.NotNull(workspace);
        Assert.Equal(partyId, workspace!.PartyId);
        return workspace;
    }

    private static async Task<int> CountApplicationsAsync(ServiceProvider owner, string searchText)
    {
        await using var scope = owner.CreateAsyncScope();
        var page = await scope.ServiceProvider.GetRequiredService<RecruitingService>()
            .SearchRecruitmentApplicationsAsync(new RecruitmentApplicationQuery(searchText));
        return page.TotalCount;
    }

    private static async Task<int> CountPartiesAsync(ServiceProvider owner, string displayName)
    {
        await using var scope = owner.CreateAsyncScope();
        var page = await scope.ServiceProvider.GetRequiredService<IPartyRecordQueryService>()
            .SearchAsync(new PartyRecordQuery(displayName, IncludeArchived: true));
        return page.Items.Count(item => string.Equals(item.DisplayName, displayName, StringComparison.Ordinal)) == page.TotalCount
            ? page.TotalCount
            : throw new InvalidOperationException($"The party search for '{displayName}' returned records with other names.");
    }

    private static async Task<SeededRecruiting> SeedAsync(ServiceProvider owner, string suffix)
    {
        await using var scope = owner.CreateAsyncScope();
        var partyDirectoryService = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();

        var candidateName = $"Journey Candidate {suffix}";
        var interviewerName = $"Journey Interviewer {suffix}";
        var managerName = $"Journey Hiring Manager {suffix}";
        var buddyName = $"Journey Buddy {suffix}";
        var candidateId = await CrmHrWorkspaceJourneySupport.CreatePartyAsync(partyDirectoryService, candidateName, PartyType.Person, PartyRoleKind.Candidate, $"journey.candidate.{suffix}@example.test", PartyLifecycleStatus.Candidate);
        var interviewerId = await CrmHrWorkspaceJourneySupport.CreatePartyAsync(partyDirectoryService, interviewerName, PartyType.Person, PartyRoleKind.Employee, $"journey.interviewer.{suffix}@example.test");
        var managerId = await CrmHrWorkspaceJourneySupport.CreatePartyAsync(partyDirectoryService, managerName, PartyType.Person, PartyRoleKind.Employee, $"journey.hiring.manager.{suffix}@example.test");
        var buddyId = await CrmHrWorkspaceJourneySupport.CreatePartyAsync(partyDirectoryService, buddyName, PartyType.Person, PartyRoleKind.Employee, $"journey.buddy.{suffix}@example.test");

        var today = DateTime.Today;
        return new SeededRecruiting(
            candidateId,
            candidateName,
            interviewerId,
            interviewerName,
            managerId,
            managerName,
            buddyId,
            buddyName,
            $"Platform Engineer {suffix}",
            $"Cancelled Role {suffix}",
            $"Cancelled Candidate {suffix}",
            $"Prepare workstation {suffix}",
            $"Senior Platform Engineer {suffix}",
            today.AddDays(6).AddHours(14).AddMinutes(30),
            DateOnly.FromDateTime(today.AddDays(21)));
    }

    private sealed record SeededRecruiting(
        Guid CandidateId,
        string CandidateName,
        Guid InterviewerId,
        string InterviewerName,
        Guid ManagerId,
        string ManagerName,
        Guid BuddyId,
        string BuddyName,
        string RoleTitle,
        string CancelledRole,
        string CancelledCandidateName,
        string TaskTitle,
        string JobTitle,
        DateTime InterviewLocal,
        DateOnly TaskDueOn);
}
