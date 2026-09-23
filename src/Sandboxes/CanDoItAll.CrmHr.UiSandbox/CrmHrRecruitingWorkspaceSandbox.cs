using CanDoItAll.AppComponents;
using CanDoItAll.CrmHr.UI.Recruiting;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.CrmHr.UiSandbox;

public enum CrmHrRecruitingWorkspaceSandboxScenario
{
    Catalog,
    SelectedExisting,
    NewDraft,
    NonDefaultSection,
    Failed,
    UnavailableReferences,
    Empty
}

public sealed record CrmHrRecruitingWorkspaceSandboxContext(
    CrmHrRecruitingWorkspaceSandboxScenario Scenario = CrmHrRecruitingWorkspaceSandboxScenario.Catalog,
    CrmHrHomeSandboxLayout Layout = CrmHrHomeSandboxLayout.Matched)
{
    public static CrmHrRecruitingWorkspaceSandboxContext Parse(string? scenario, string? layout)
        => new(ParseScenario(scenario), CrmHrHomeSandboxContext.ParseLayout(layout));

    public static CrmHrRecruitingWorkspaceSandboxScenario ParseScenario(string? token)
        => Enum.GetValues<CrmHrRecruitingWorkspaceSandboxScenario>()
            .FirstOrDefault(scenario => string.Equals(Token(scenario), token?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string Token(CrmHrRecruitingWorkspaceSandboxScenario scenario)
        => string.Concat(scenario.ToString().Select((character, index) =>
            char.IsUpper(character) && index > 0 ? $"-{char.ToLowerInvariant(character)}" : char.ToLowerInvariant(character).ToString()));

    public IReadOnlyDictionary<string, object?> ToQuery() => new Dictionary<string, object?>
    {
        ["scenario"] = Token(Scenario),
        ["layout"] = CrmHrHomeSandboxContext.Token(Layout)
    };
}

// The application record this sandbox owns end to end: the catalog, its interviews, lifecycle tasks and support
// assignments never leave this view, so LoadApplicationPageAsync pages the same local list every mutation updates.
internal sealed class CrmHrSandboxRecruitmentApplication
{
    public Guid Id { get; init; }
    public Guid? PartyId { get; set; }
    public PartyType CandidatePartyType { get; set; } = PartyType.Person;
    public string CandidateName { get; set; } = "";
    public string CandidateEmail { get; set; } = "";
    public string CandidatePhone { get; set; } = "";
    public string CandidateSummary { get; set; } = "";
    public Guid? TargetUnitPartyId { get; set; }
    public Guid? RecruiterPartyId { get; set; }
    public Guid? HiringManagerPartyId { get; set; }
    public string DesiredRole { get; set; } = "";
    public RecruitmentStage Stage { get; set; } = RecruitmentStage.Applied;
    public RecruitmentDecision Decision { get; set; } = RecruitmentDecision.Pending;
    public bool HasWorkforceProfile { get; set; }
    public List<RecruitmentStageHistoryItemModel> StageHistory { get; set; } = [];
    public List<RecruitmentInterviewItemModel> Interviews { get; set; } = [];
    public List<LifecycleTaskItemModel> LifecycleTasks { get; set; } = [];
    public Guid? ManagerPartyId { get; set; }
    public Guid? BuddyPartyId { get; set; }
    public Guid? MentorPartyId { get; set; }
    public string TargetUnitName { get; set; } = "";
    public string RecruiterName { get; set; } = "";
    public string HiringManagerName { get; set; } = "";
}

// Deterministic local state for the real Recruiting workspace surface. Nothing here calls a query service, persists,
// navigates or reaches an agent context; the application catalog, its interviews and its tasks are all owned here.
public sealed class CrmHrRecruitingSandboxView(CrmHrSandboxPartyStore partyStore) : ICrmHrRecruitingWorkspaceView
{
    private readonly List<CrmHrSandboxRecruitmentApplication> applications = SeedApplications();
    private CrmHrSandboxRecruitmentApplication? selected;

    public Action? RenderRequested { get; set; }

    public string IntentLog { get; private set; } = "No intent yet.";

    public RecruitmentWorkspaceModel? CurrentWorkspace { get; private set; }

    public RecruitmentApplicationEditorModel ApplicationEditor { get; private set; } = new();

    public RecruitmentInterviewEditorModel InterviewEditor { get; private set; } = new();

    public LifecycleTaskEditorModel TaskEditor { get; private set; } = new();

    public RecruitmentSupportAssignmentsEditorModel SupportEditor { get; private set; } = new();

    public RecruitmentConversionEditorModel ConversionEditor { get; private set; } = new();

    public RecruitmentApplicationSummary ApplicationSummary => new(
        applications.Count,
        applications.Count(item => item.Stage == RecruitmentStage.Interviewing),
        applications.Count(item => item.Stage is RecruitmentStage.Offer or RecruitmentStage.Hired));

    public int ApplicationBrowserRevision { get; private set; }

    public bool IsRecruitmentDialogOpen { get; private set; }

    public Guid? SelectedPartyId => selected?.PartyId;

    public Error? ConversionEligibilityError => selected is null
        ? null
        : RecruitmentConversionPolicy.Evaluate(selected.Stage, selected.Decision,
            selected.CandidatePartyType == PartyType.AiAgent, assessmentReady: false);

    public PagedRecordSelection<Guid>? ApplicationSelection => selected is null ? null : new PagedRecordSelection<Guid>(selected.Id);

    public int SelectedRecruitingTabIndex { get; private set; }

    public string RecruitmentDialogTitle => selected?.CandidateName ?? "New candidate";

    public void RequestRender() => RenderRequested?.Invoke();

    // Host-only navigation: the secondary tabs slot only logs the destination in this sandbox.
    public void LogNavigation(string route) => Log($"Navigate: {route}");

    public Task<PagedRecordPage<Guid>> LoadApplicationPageAsync(PagedRecordRequest<RecruitmentApplicationScope> request, CancellationToken cancellationToken)
    {
        var matches = applications.Where(item => MatchesScope(item.Stage, request.Filter));
        if (!string.IsNullOrWhiteSpace(request.SearchText))
        {
            var searchText = request.SearchText.Trim();
            matches = matches.Where(item => item.CandidateName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                item.DesiredRole.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = matches.OrderBy(item => item.CandidateName, StringComparer.OrdinalIgnoreCase).ToList();
        var pageSize = request.PageSize <= 0 ? RecruitmentApplicationQueryLimits.DefaultPageSize : request.PageSize;
        var page = ordered.Skip(request.PageIndex * pageSize).Take(pageSize)
            .Select(item => new PagedRecordOption<Guid>(item.Id, item.CandidateName, item.Stage.ToString())
            {
                Subtitle = item.DesiredRole,
                Description = item.CandidateSummary,
                TestId = "crmhr-recruiting-application-item"
            }).ToList();
        return Task.FromResult(new PagedRecordPage<Guid>(page, request.PageIndex, pageSize, ordered.Count));
    }

    public Task SelectApplicationAsync(Guid applicationId)
    {
        var application = applications.FirstOrDefault(item => item.Id == applicationId);
        if (application is null)
        {
            Log($"Select application ignored: {applicationId:D}");
            return Task.CompletedTask;
        }

        OpenApplication(application, tabIndex: 0);
        Log($"Select application: {application.CandidateName}");
        return Task.CompletedTask;
    }

    public Task HandleApplicationPageLoadFailed(Exception exception)
    {
        Log($"Application page load failed: {exception.Message}");
        return Task.CompletedTask;
    }

    public Task CreateNewApplicationAsync()
    {
        selected = null;
        ApplicationEditor = new RecruitmentApplicationEditorModel();
        SelectedRecruitingTabIndex = 0;
        IsRecruitmentDialogOpen = true;
        CurrentWorkspace = new RecruitmentWorkspaceModel(
            ApplicationEditor, false, "", "", "", "", null, null, AiResourceBindingStatus.Unbound, "", "", false,
            [], [], [], new RecruitmentSupportAssignmentsModel(Guid.Empty, null, "", null, "", null, ""), ConversionEditor);
        Log("Create new application");
        return Task.CompletedTask;
    }

    public Task CloseRecruitmentDialogAsync()
    {
        IsRecruitmentDialogOpen = false;
        Log("Close recruitment dialog");
        return Task.CompletedTask;
    }

    public Task HandleRecruitingTabChangedAsync(int selectedIndex)
    {
        SelectedRecruitingTabIndex = selectedIndex;
        Log($"Change recruiting tab: {selectedIndex}");
        return Task.CompletedTask;
    }

    public Task SaveApplicationAsync()
    {
        if (selected is null)
        {
            selected = new CrmHrSandboxRecruitmentApplication { Id = CrmHrWorkspaceSandboxIds.Id($"application-{Guid.NewGuid():N}") };
            applications.Add(selected);
        }

        selected.PartyId = ApplicationEditor.PartyId;
        selected.CandidateName = ApplicationEditor.CandidateName;
        selected.CandidateEmail = ApplicationEditor.CandidateEmail;
        selected.CandidatePhone = ApplicationEditor.CandidatePhone;
        selected.CandidateSummary = ApplicationEditor.CandidateSummary;
        selected.TargetUnitPartyId = ApplicationEditor.TargetUnitPartyId;
        selected.RecruiterPartyId = ApplicationEditor.RecruiterPartyId;
        selected.HiringManagerPartyId = ApplicationEditor.HiringManagerPartyId;
        selected.DesiredRole = ApplicationEditor.DesiredRole;
        selected.Stage = ApplicationEditor.Stage;
        selected.Decision = ApplicationEditor.Decision;
        selected.TargetUnitName = selected.TargetUnitPartyId.HasValue ? partyStore.DisplayNameOf(selected.TargetUnitPartyId.Value) : "";
        selected.RecruiterName = selected.RecruiterPartyId.HasValue ? partyStore.DisplayNameOf(selected.RecruiterPartyId.Value) : "";
        selected.HiringManagerName = selected.HiringManagerPartyId.HasValue ? partyStore.DisplayNameOf(selected.HiringManagerPartyId.Value) : "";
        selected.StageHistory.Add(new RecruitmentStageHistoryItemModel(CrmHrWorkspaceSandboxIds.Id($"stage-{Guid.NewGuid():N}"),
            selected.Stage, "Application saved", ApplicationEditor.StageNotes, DateTimeOffset.UtcNow, "crm-hr-sandbox"));

        ApplicationBrowserRevision++;
        OpenApplication(selected, SelectedRecruitingTabIndex);
        Log($"Save application: {selected.CandidateName}");
        return Task.CompletedTask;
    }

    public Task ResetApplicationAsync()
    {
        ApplicationEditor = selected is null ? new RecruitmentApplicationEditorModel() : ToEditor(selected);
        Log("Reset application draft");
        return Task.CompletedTask;
    }

    public Task SaveInterviewAsync()
    {
        if (selected is null)
        {
            return Task.CompletedTask;
        }

        var interviewerName = InterviewEditor.InterviewerPartyId.HasValue ? partyStore.DisplayNameOf(InterviewEditor.InterviewerPartyId.Value) : "";
        var id = InterviewEditor.Id ?? CrmHrWorkspaceSandboxIds.Id($"interview-{Guid.NewGuid():N}");
        selected.Interviews.RemoveAll(item => item.Id == id);
        selected.Interviews.Add(new RecruitmentInterviewItemModel(id, selected.Id,
            new DateTimeOffset(InterviewEditor.ScheduledAtLocal ?? DateTime.UtcNow, TimeSpan.Zero), InterviewEditor.InterviewType,
            InterviewEditor.InterviewerPartyId, interviewerName, InterviewEditor.Outcome, InterviewEditor.Recommendation, InterviewEditor.Feedback));
        RefreshWorkspace();
        InterviewEditor = new RecruitmentInterviewEditorModel { ApplicationId = selected.Id };
        Log("Save interview");
        return Task.CompletedTask;
    }

    public Task ResetInterviewAsync()
    {
        InterviewEditor = new RecruitmentInterviewEditorModel { ApplicationId = selected?.Id ?? Guid.Empty };
        Log("Reset interview draft");
        return Task.CompletedTask;
    }

    public Task DeleteInterviewAsync(Guid interviewId)
    {
        selected?.Interviews.RemoveAll(item => item.Id == interviewId);
        RefreshWorkspace();
        Log($"Delete interview: {interviewId:D}");
        return Task.CompletedTask;
    }

    public Task SaveTaskAsync()
    {
        if (selected is null)
        {
            return Task.CompletedTask;
        }

        var id = TaskEditor.Id ?? CrmHrWorkspaceSandboxIds.Id($"task-{Guid.NewGuid():N}");
        var ownerName = TaskEditor.OwnerPartyId.HasValue ? partyStore.DisplayNameOf(TaskEditor.OwnerPartyId.Value) : "";
        selected.LifecycleTasks.RemoveAll(item => item.Id == id);
        selected.LifecycleTasks.Add(new LifecycleTaskItemModel(id, selected.PartyId ?? Guid.Empty, TaskEditor.TaskKind, TaskEditor.Title,
            TaskEditor.OwnerPartyId, ownerName, TaskEditor.DueDate, TaskEditor.Status, TaskEditor.RelatedProjectId, "", TaskEditor.Notes,
            TaskEditor.DueDate.HasValue && TaskEditor.DueDate < DateOnly.FromDateTime(DateTime.UtcNow) && TaskEditor.Status != LifecycleTaskStatus.Completed));
        RefreshWorkspace();
        TaskEditor = new LifecycleTaskEditorModel { PartyId = selected.PartyId ?? Guid.Empty };
        Log("Save lifecycle task");
        return Task.CompletedTask;
    }

    public Task ResetTaskAsync()
    {
        TaskEditor = new LifecycleTaskEditorModel { PartyId = selected?.PartyId ?? Guid.Empty };
        Log("Reset task draft");
        return Task.CompletedTask;
    }

    public Task DeleteTaskAsync(Guid taskId)
    {
        selected?.LifecycleTasks.RemoveAll(item => item.Id == taskId);
        RefreshWorkspace();
        Log($"Delete task: {taskId:D}");
        return Task.CompletedTask;
    }

    public Task SaveSupportAssignmentsAsync()
    {
        if (selected is not null)
        {
            selected.ManagerPartyId = SupportEditor.ManagerPartyId;
            selected.BuddyPartyId = SupportEditor.BuddyPartyId;
            selected.MentorPartyId = SupportEditor.MentorPartyId;
            RefreshWorkspace();
        }

        Log("Save support assignments");
        return Task.CompletedTask;
    }

    public Task ConvertCandidateAsync()
    {
        if (selected is null || ConversionEligibilityError is not null)
        {
            Log("Convert candidate ignored: not eligible");
            return Task.CompletedTask;
        }

        selected.HasWorkforceProfile = true;
        if (selected.PartyId is { } partyId)
        {
            var record = partyStore.Find(partyId);
            if (record is not null)
            {
                partyStore.Upsert(record with
                {
                    Classification = WorkforceRecordClassification.Employee,
                    JobTitle = ConversionEditor.JobTitle,
                    Discipline = ConversionEditor.Discipline,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                });
            }
        }

        RefreshWorkspace();
        Log($"Convert candidate: {selected.CandidateName}");
        return Task.CompletedTask;
    }

    public Task SetConversionHomeUnitAsync(Guid? partyId)
    {
        ConversionEditor.HomeUnitPartyId = partyId;
        Log($"Set conversion home unit: {(partyId.HasValue ? partyStore.DisplayNameOf(partyId.Value) : "None")}");
        return Task.CompletedTask;
    }

    public Task SetConversionManagerAsync(Guid? partyId)
    {
        ConversionEditor.ManagerPartyId = partyId;
        Log($"Set conversion manager: {(partyId.HasValue ? partyStore.DisplayNameOf(partyId.Value) : "None")}");
        return Task.CompletedTask;
    }

    public Task OpenWorkforceAsync()
    {
        Log($"Open workforce: {SelectedPartyId:D}");
        return Task.CompletedTask;
    }

    public void Apply(CrmHrRecruitingWorkspaceSandboxScenario next)
    {
        IntentLog = "No intent yet.";
        IsRecruitmentDialogOpen = false;
        selected = null;
        applications.Clear();
        applications.AddRange(SeedApplications());

        switch (next)
        {
            case CrmHrRecruitingWorkspaceSandboxScenario.SelectedExisting:
                OpenApplication(applications[0], tabIndex: 0);
                break;
            case CrmHrRecruitingWorkspaceSandboxScenario.NewDraft:
                _ = CreateNewApplicationAsync();
                break;
            case CrmHrRecruitingWorkspaceSandboxScenario.NonDefaultSection:
                OpenApplication(applications[0], tabIndex: 1);
                break;
            case CrmHrRecruitingWorkspaceSandboxScenario.Failed:
                var ineligible = applications.First(item => item.Stage == RecruitmentStage.Screening);
                OpenApplication(ineligible, tabIndex: 3);
                break;
            case CrmHrRecruitingWorkspaceSandboxScenario.UnavailableReferences:
                var withMissingRefs = applications[0];
                withMissingRefs.RecruiterName = "Unavailable recruiter";
                withMissingRefs.HiringManagerName = "Unavailable hiring manager";
                withMissingRefs.TargetUnitName = "Unavailable unit";
                OpenApplication(withMissingRefs, tabIndex: 0);
                break;
            case CrmHrRecruitingWorkspaceSandboxScenario.Empty:
                var empty = applications.First(item => item.CandidateName == "Ava Thompson");
                empty.Interviews.Clear();
                empty.LifecycleTasks.Clear();
                empty.StageHistory.Clear();
                OpenApplication(empty, tabIndex: 2);
                break;
            default:
                break;
        }
    }

    private void OpenApplication(CrmHrSandboxRecruitmentApplication application, int tabIndex)
    {
        selected = application;
        IsRecruitmentDialogOpen = true;
        SelectedRecruitingTabIndex = tabIndex;
        ApplicationEditor = ToEditor(application);
        InterviewEditor = new RecruitmentInterviewEditorModel { ApplicationId = application.Id };
        TaskEditor = new LifecycleTaskEditorModel { PartyId = application.PartyId ?? Guid.Empty };
        SupportEditor = new RecruitmentSupportAssignmentsEditorModel
        {
            PartyId = application.PartyId ?? Guid.Empty,
            ManagerPartyId = application.ManagerPartyId,
            BuddyPartyId = application.BuddyPartyId,
            MentorPartyId = application.MentorPartyId
        };
        ConversionEditor = new RecruitmentConversionEditorModel
        {
            ApplicationId = application.Id,
            JobTitle = application.DesiredRole,
            HomeUnitPartyId = application.TargetUnitPartyId,
            ManagerPartyId = application.HiringManagerPartyId
        };
        RefreshWorkspace();
    }

    private void RefreshWorkspace()
    {
        if (selected is null)
        {
            return;
        }

        var candidatePartyId = selected.PartyId;
        var technicalAgentId = selected.CandidatePartyType == PartyType.AiAgent ? candidatePartyId : null;
        CurrentWorkspace = new RecruitmentWorkspaceModel(
            ApplicationEditor, true, selected.CandidateName, selected.CandidateSummary, selected.CandidateEmail,
            selected.CandidatePhone, selected.CandidatePartyType, technicalAgentId,
            selected.CandidatePartyType == PartyType.AiAgent ? AiResourceBindingStatus.Bound : AiResourceBindingStatus.Unbound,
            selected.RecruiterName, selected.HiringManagerName, selected.HasWorkforceProfile,
            selected.StageHistory, selected.Interviews, selected.LifecycleTasks,
            new RecruitmentSupportAssignmentsModel(selected.PartyId ?? Guid.Empty, selected.ManagerPartyId,
                selected.ManagerPartyId.HasValue ? partyStore.DisplayNameOf(selected.ManagerPartyId.Value) : "",
                selected.BuddyPartyId, selected.BuddyPartyId.HasValue ? partyStore.DisplayNameOf(selected.BuddyPartyId.Value) : "",
                selected.MentorPartyId, selected.MentorPartyId.HasValue ? partyStore.DisplayNameOf(selected.MentorPartyId.Value) : ""),
            ConversionEditor);
    }

    private static RecruitmentApplicationEditorModel ToEditor(CrmHrSandboxRecruitmentApplication application) => new()
    {
        Id = application.Id,
        PartyId = application.PartyId,
        CandidateName = application.CandidateName,
        CandidateEmail = application.CandidateEmail,
        CandidatePhone = application.CandidatePhone,
        CandidateSummary = application.CandidateSummary,
        TargetUnitPartyId = application.TargetUnitPartyId,
        RecruiterPartyId = application.RecruiterPartyId,
        HiringManagerPartyId = application.HiringManagerPartyId,
        DesiredRole = application.DesiredRole,
        Stage = application.Stage,
        Decision = application.Decision,
        LastChangedBy = "crm-hr-sandbox"
    };

    private static bool MatchesScope(RecruitmentStage stage, RecruitmentApplicationScope scope)
        => scope switch
        {
            RecruitmentApplicationScope.All => true,
            RecruitmentApplicationScope.Applied => stage == RecruitmentStage.Applied,
            RecruitmentApplicationScope.Screening => stage == RecruitmentStage.Screening,
            RecruitmentApplicationScope.Interviewing => stage == RecruitmentStage.Interviewing,
            RecruitmentApplicationScope.Offer => stage == RecruitmentStage.Offer,
            RecruitmentApplicationScope.Hired => stage == RecruitmentStage.Hired,
            RecruitmentApplicationScope.Rejected => stage == RecruitmentStage.Rejected,
            RecruitmentApplicationScope.Withdrawn => stage == RecruitmentStage.Withdrawn,
            _ => true
        };

    private static List<CrmHrSandboxRecruitmentApplication> SeedApplications() =>
    [
        new()
        {
            Id = CrmHrWorkspaceSandboxIds.Id("application-lucas-meyer"),
            PartyId = CrmHrSandboxData.LucasMeyer,
            CandidateName = "Lucas Meyer",
            CandidateEmail = "lucas.meyer@example.test",
            CandidatePhone = "+1 555 0119",
            CandidateSummary = "Backend engineer candidate in the interviewing stage.",
            TargetUnitPartyId = CrmHrSandboxData.CascadeRnDUnit,
            RecruiterPartyId = CrmHrSandboxData.PriyaNatarajan,
            HiringManagerPartyId = CrmHrSandboxData.OliverBennett,
            DesiredRole = "Backend Engineer",
            Stage = RecruitmentStage.Interviewing,
            Decision = RecruitmentDecision.Pending,
            TargetUnitName = "Cascade R&D Unit",
            RecruiterName = "Priya Natarajan",
            HiringManagerName = "Oliver Bennett",
            StageHistory =
            [
                new RecruitmentStageHistoryItemModel(CrmHrWorkspaceSandboxIds.Id("stage-lucas-applied"), RecruitmentStage.Applied,
                    "Application received", "", CrmHrSandboxData.BaseUpdatedAtUtc.AddDays(-14), "crm-hr-ui"),
                new RecruitmentStageHistoryItemModel(CrmHrWorkspaceSandboxIds.Id("stage-lucas-interviewing"), RecruitmentStage.Interviewing,
                    "Moved to interviewing", "", CrmHrSandboxData.BaseUpdatedAtUtc.AddDays(-3), "crm-hr-ui")
            ],
            Interviews =
            [
                new RecruitmentInterviewItemModel(CrmHrWorkspaceSandboxIds.Id("interview-lucas-screening"),
                    CrmHrWorkspaceSandboxIds.Id("application-lucas-meyer"), CrmHrSandboxData.BaseUpdatedAtUtc.AddDays(-5),
                    RecruitmentInterviewType.Screening, CrmHrSandboxData.PriyaNatarajan, "Priya Natarajan",
                    RecruitmentInterviewOutcome.Yes, "Advance", "Strong fundamentals, clear communication.")
            ]
        },
        new()
        {
            Id = CrmHrWorkspaceSandboxIds.Id("application-ava-thompson"),
            PartyId = CrmHrSandboxData.AvaThompson,
            CandidateName = "Ava Thompson",
            CandidateEmail = "ava.thompson@example.test",
            CandidatePhone = "+1 555 0120",
            CandidateSummary = "Delivery-lead candidate freshly applied.",
            TargetUnitPartyId = CrmHrSandboxData.NorthwindFulfillmentUnit,
            RecruiterPartyId = CrmHrSandboxData.PriyaNatarajan,
            DesiredRole = "Delivery Lead",
            Stage = RecruitmentStage.Applied,
            Decision = RecruitmentDecision.Pending,
            TargetUnitName = "Northwind Fulfillment Unit",
            RecruiterName = "Priya Natarajan"
        },
        new()
        {
            Id = CrmHrWorkspaceSandboxIds.Id("application-recruiting-scout"),
            PartyId = CrmHrSandboxData.RecruitingScoutAgent,
            CandidatePartyType = PartyType.AiAgent,
            CandidateName = "Recruiting Scout Agent",
            CandidateSummary = "AI candidate in the recruiting pipeline, pending assessment review.",
            RecruiterPartyId = CrmHrSandboxData.PriyaNatarajan,
            DesiredRole = "Operations Copilot",
            Stage = RecruitmentStage.Screening,
            Decision = RecruitmentDecision.Pending,
            RecruiterName = "Priya Natarajan"
        },
        new()
        {
            Id = CrmHrWorkspaceSandboxIds.Id("application-noah-fischer"),
            PartyId = CrmHrSandboxData.NoahFischer,
            CandidateName = "Noah Fischer",
            CandidateEmail = "noah.fischer@example.test",
            DesiredRole = "Support Engineer",
            Stage = RecruitmentStage.Hired,
            Decision = RecruitmentDecision.Approved,
            HasWorkforceProfile = true
        }
    ];

    private void Log(string message) => IntentLog = message;
}
