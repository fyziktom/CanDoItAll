using CanDoItAll.CrmHr.UI.Assignments;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.CrmHr.UiSandbox;

public enum CrmHrAssignmentsWorkspaceSandboxScenario
{
    Catalog,
    SelectedExisting,
    NewDraft,
    NonDefaultSection,
    Loading,
    UnavailableReferences,
    Empty
}

public sealed record CrmHrAssignmentsWorkspaceSandboxContext(
    CrmHrAssignmentsWorkspaceSandboxScenario Scenario = CrmHrAssignmentsWorkspaceSandboxScenario.Catalog,
    CrmHrHomeSandboxLayout Layout = CrmHrHomeSandboxLayout.Matched)
{
    public static CrmHrAssignmentsWorkspaceSandboxContext Parse(string? scenario, string? layout)
        => new(ParseScenario(scenario), CrmHrHomeSandboxContext.ParseLayout(layout));

    public static CrmHrAssignmentsWorkspaceSandboxScenario ParseScenario(string? token)
        => Enum.GetValues<CrmHrAssignmentsWorkspaceSandboxScenario>()
            .FirstOrDefault(scenario => string.Equals(Token(scenario), token?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string Token(CrmHrAssignmentsWorkspaceSandboxScenario scenario)
        => string.Concat(scenario.ToString().Select((character, index) =>
            char.IsUpper(character) && index > 0 ? $"-{char.ToLowerInvariant(character)}" : char.ToLowerInvariant(character).ToString()));

    public IReadOnlyDictionary<string, object?> ToQuery() => new Dictionary<string, object?>
    {
        ["scenario"] = Token(Scenario),
        ["layout"] = CrmHrHomeSandboxContext.Token(Layout)
    };
}

// One assignment row this sandbox owns, with the allocation/relationship split the real read side keeps separate.
internal sealed class CrmHrSandboxAssignmentRow
{
    public required ProjectPartyAssignmentDetail Detail { get; init; }
    public bool IsAllocation { get; init; }
}

// Deterministic local state for the real Assignments workspace surface. Nothing here calls a query service,
// persists, navigates or reaches an agent context; the assignment, staffing and candidate lists are all owned here
// per project so every mutation is immediately visible.
public sealed class CrmHrAssignmentsSandboxView(CrmHrSandboxPartyStore partyStore) : ICrmHrAssignmentsWorkspaceView
{
    private static readonly IReadOnlyList<SkillCatalogItemModel> SkillCatalogSeed =
    [
        new(CrmHrWorkspaceSandboxIds.Id("assign-skill-csharp"), "C#", "Engineering", "Backend and platform development.", true),
        new(CrmHrWorkspaceSandboxIds.Id("assign-skill-python"), "Python", "Engineering", "Data pipelines and automation.", true)
    ];

    private readonly Dictionary<Guid, List<CrmHrSandboxAssignmentRow>> assignmentsByProject = [];
    private readonly Dictionary<Guid, List<StaffingRequestItemModel>> staffingRequestsByProject = [];

    public Action? RenderRequested { get; set; }

    public string IntentLog { get; private set; } = "No intent yet.";

    public int ProjectCount => CrmHrSandboxData.Projects.Count;

    public IReadOnlyList<ProjectPartyAssignmentDetail> ScheduleAssignments { get; private set; } = [];

    public IReadOnlyList<ProjectPartyAssignmentDetail> RelationshipAssignments { get; private set; } = [];

    public IReadOnlyList<ProjectPartyAssignmentDetail> AllocationAssignments { get; private set; } = [];

    public ProjectPartyAssignmentCounts AssignmentCounts { get; private set; } = ProjectPartyAssignmentCounts.Empty;

    public int ScheduleAssignmentTotalCount { get; private set; }

    public int RelationshipAssignmentPageIndex { get; private set; }

    public int RelationshipAssignmentTotalCount { get; private set; }

    public int AllocationAssignmentPageIndex { get; private set; }

    public int AllocationAssignmentTotalCount { get; private set; }

    public IReadOnlyList<SkillCatalogItemModel> SkillCatalog => SkillCatalogSeed;

    public IReadOnlyList<StaffingRequestItemModel> StaffingRequests { get; private set; } = [];

    public IReadOnlyList<StaffingCandidateItemModel> StaffingCandidates { get; private set; } = [];

    public int StaffingRequestPageIndex { get; private set; }

    public int StaffingRequestTotalCount { get; private set; }

    public int StaffingCandidatePageIndex { get; private set; }

    public int StaffingCandidateTotalCount { get; private set; }

    public StaffingDashboardModel StaffingDashboard { get; private set; } = new(0, 0m, 0, 0);

    public Guid? SelectedProjectId { get; private set; }

    public AssignmentWorkspaceTab SelectedWorkspaceTab { get; private set; }

    public bool IsProjectContextLoading { get; private set; }

    public string CandidateSearchText { get; private set; } = "";

    public string CandidateSkillFilter { get; private set; } = "";

    public string CandidateAvailabilityFilter { get; private set; } = "";

    public string StaffingRequestSearchText { get; private set; } = "";

    public string RelationshipAssignmentSearchText { get; private set; } = "";

    public string AllocationAssignmentSearchText { get; private set; } = "";

    public StaffingRequestStatus? StaffingRequestStatusFilter { get; private set; }

    public ProjectPartyAssignmentRole? RelationshipAssignmentRoleFilter { get; private set; }

    public ProjectPartyAssignmentRole? AllocationAssignmentRoleFilter { get; private set; }

    public ProjectPartyAssignmentUpsertRequest DraftAssignment { get; private set; } = new();

    public ProjectPartyAssignmentUpsertRequest AllocationDraft { get; private set; } = new();

    public StaffingRequestEditorModel StaffingRequestDraft { get; private set; } = new();

    public WorkspaceTabsIdentity WorkspaceTabsKey => new(SelectedProjectId, AssignmentCounts.TotalCount,
        AssignmentCounts.AllocationCount, StaffingRequestCount ?? 0, StaffingRequestCount.HasValue);

    public int? StaffingRequestCount => SelectedProjectId.HasValue ? StaffingRequestTotalCount : null;

    public string StaffingRequestBadgeText => StaffingRequestCount?.ToString() ?? "-";

    public ProjectRecordQueryItem? SelectedProject { get; private set; }

    public void RequestRender() => RenderRequested?.Invoke();

    // Host-only navigation: the secondary tabs slot only logs the destination in this sandbox.
    public void LogNavigation(string route) => Log($"Navigate: {route}");

    public void Apply(CrmHrAssignmentsWorkspaceSandboxScenario next)
    {
        IntentLog = "No intent yet.";
        SelectedProjectId = null;
        SelectedProject = null;
        SelectedWorkspaceTab = AssignmentWorkspaceTab.ResourceSchedule;
        IsProjectContextLoading = false;
        CandidateSearchText = "";
        CandidateSkillFilter = "";
        CandidateAvailabilityFilter = "";
        StaffingRequestSearchText = "";
        RelationshipAssignmentSearchText = "";
        AllocationAssignmentSearchText = "";
        StaffingRequestStatusFilter = null;
        RelationshipAssignmentRoleFilter = null;
        AllocationAssignmentRoleFilter = null;
        DraftAssignment = new ProjectPartyAssignmentUpsertRequest();
        AllocationDraft = new ProjectPartyAssignmentUpsertRequest();
        StaffingRequestDraft = new StaffingRequestEditorModel();
        assignmentsByProject.Clear();
        staffingRequestsByProject.Clear();
        SeedProject(CrmHrSandboxData.ProjectWarehouseAutomation);
        RefreshCandidates();
        RefreshDashboard();

        switch (next)
        {
            case CrmHrAssignmentsWorkspaceSandboxScenario.SelectedExisting:
                SelectProject(CrmHrSandboxData.ProjectWarehouseAutomation);
                SelectedWorkspaceTab = AssignmentWorkspaceTab.Relationships;
                break;
            case CrmHrAssignmentsWorkspaceSandboxScenario.NewDraft:
                SelectProject(CrmHrSandboxData.ProjectWarehouseAutomation);
                SelectedWorkspaceTab = AssignmentWorkspaceTab.Relationships;
                DraftAssignment = new ProjectPartyAssignmentUpsertRequest { ProjectId = SelectedProjectId!.Value, Role = ProjectPartyAssignmentRole.TeamMember };
                break;
            case CrmHrAssignmentsWorkspaceSandboxScenario.NonDefaultSection:
                SelectProject(CrmHrSandboxData.ProjectWarehouseAutomation);
                SelectedWorkspaceTab = AssignmentWorkspaceTab.Allocations;
                break;
            case CrmHrAssignmentsWorkspaceSandboxScenario.Loading:
                SelectProject(CrmHrSandboxData.ProjectWarehouseAutomation);
                IsProjectContextLoading = true;
                break;
            case CrmHrAssignmentsWorkspaceSandboxScenario.UnavailableReferences:
                SelectProject(CrmHrSandboxData.ProjectWarehouseAutomation);
                var rows = assignmentsByProject[SelectedProjectId!.Value];
                rows.Add(new CrmHrSandboxAssignmentRow
                {
                    IsAllocation = false,
                    Detail = new ProjectPartyAssignmentDetail(
                        CrmHrWorkspaceSandboxIds.Id("unavailable-assignment-row"), SelectedProjectId.Value, CrmHrSandboxData.MissingParty,
                        ProjectPartyAssignmentRole.Stakeholder, "Unavailable party", "Person", ProjectPartyType.Person,
                        "unavailable-node", false, null, null, null, "manual", "This party no longer exists in the directory.")
                });
                RefreshProjectAssignments();
                break;
            case CrmHrAssignmentsWorkspaceSandboxScenario.Empty:
                SelectProject(CrmHrSandboxData.ProjectBeaconHillEngagement);
                break;
            default:
                break;
        }
    }

    public Task HandleProjectChanged(Guid? projectId)
    {
        if (projectId.HasValue)
        {
            SelectProject(projectId.Value);
        }
        else
        {
            SelectedProjectId = null;
            SelectedProject = null;
            RefreshProjectAssignments();
            RefreshStaffingRequests();
        }

        Log($"Change project: {SelectedProject?.Name ?? "None"}");
        return Task.CompletedTask;
    }

    public Task HandleWorkspaceTabChangedAsync(int selectedIndex)
    {
        SelectedWorkspaceTab = (AssignmentWorkspaceTab)selectedIndex;
        Log($"Change workspace tab: {SelectedWorkspaceTab}");
        return Task.CompletedTask;
    }

    public Task<bool> PrepareRelationshipCreateAsync()
    {
        if (!SelectedProjectId.HasValue)
        {
            return Task.FromResult(false);
        }

        DraftAssignment = new ProjectPartyAssignmentUpsertRequest { ProjectId = SelectedProjectId.Value, Role = ProjectPartyAssignmentRole.TeamMember };
        Log("Prepare relationship create");
        return Task.FromResult(true);
    }

    public Task<bool> PrepareStaffingCreateAsync()
    {
        if (!SelectedProjectId.HasValue)
        {
            return Task.FromResult(false);
        }

        StaffingRequestDraft = new StaffingRequestEditorModel { ProjectId = SelectedProjectId.Value };
        Log("Prepare staffing create");
        return Task.FromResult(true);
    }

    public Task<bool> PrepareAllocationCreateAsync()
    {
        if (!SelectedProjectId.HasValue)
        {
            return Task.FromResult(false);
        }

        AllocationDraft = new ProjectPartyAssignmentUpsertRequest { ProjectId = SelectedProjectId.Value, Role = ProjectPartyAssignmentRole.TeamMember };
        Log("Prepare allocation create");
        return Task.FromResult(true);
    }

    public Task<bool> SaveDraftAsync()
    {
        if (!SelectedProjectId.HasValue || DraftAssignment.PartyId == Guid.Empty)
        {
            Log("Save relationship draft ignored: no party selected");
            return Task.FromResult(false);
        }

        AddOrUpdateRow(DraftAssignment, isAllocation: false);
        Log("Save relationship draft");
        return Task.FromResult(true);
    }

    public Task<bool> SaveAllocationAsync()
    {
        if (!SelectedProjectId.HasValue || AllocationDraft.PartyId == Guid.Empty)
        {
            Log("Save allocation draft ignored: no party selected");
            return Task.FromResult(false);
        }

        AddOrUpdateRow(AllocationDraft, isAllocation: true);
        Log("Save allocation draft");
        return Task.FromResult(true);
    }

    public Task<bool> SaveStaffingRequestAsync()
    {
        if (!SelectedProjectId.HasValue || string.IsNullOrWhiteSpace(StaffingRequestDraft.Title))
        {
            Log("Save staffing request ignored: title required");
            return Task.FromResult(false);
        }

        var requests = GetStaffingRequests(SelectedProjectId.Value);
        var id = StaffingRequestDraft.Id ?? CrmHrWorkspaceSandboxIds.Id($"staffing-{Guid.NewGuid():N}");
        requests.RemoveAll(item => item.Id == id);
        requests.Add(new StaffingRequestItemModel(id, SelectedProjectId, SelectedProject?.Name ?? "", StaffingRequestDraft.RequestedByPartyId,
            StaffingRequestDraft.RequestedByPartyId.HasValue ? partyStore.DisplayNameOf(StaffingRequestDraft.RequestedByPartyId.Value) : "",
            StaffingRequestDraft.DeliveryUnitPartyId, StaffingRequestDraft.DeliveryUnitPartyId.HasValue ? partyStore.DisplayNameOf(StaffingRequestDraft.DeliveryUnitPartyId.Value) : "",
            StaffingRequestDraft.Title, StaffingRequestDraft.NeededRole,
            SkillCatalog.Where(skill => StaffingRequestDraft.SkillIds.Contains(skill.Id)).ToList(),
            StaffingRequestDraft.StartDate, StaffingRequestDraft.EndDate, StaffingRequestDraft.AllocationPercent,
            StaffingRequestDraft.Status, StaffingRequestDraft.Notes));
        RefreshStaffingRequests();
        RefreshDashboard();
        Log($"Save staffing request: {StaffingRequestDraft.Title}");
        return Task.FromResult(true);
    }

    public Task SearchCandidatesAsync()
    {
        RefreshCandidates();
        Log($"Search candidates: '{CandidateSearchText}'");
        return Task.CompletedTask;
    }

    public Task HandleCandidateSearchTextChanged(string value)
    {
        CandidateSearchText = value;
        RefreshCandidates();
        Log($"Change candidate search text: '{value}'");
        return Task.CompletedTask;
    }

    public Task HandleCandidateSkillFilterChanged(string value)
    {
        CandidateSkillFilter = value;
        RefreshCandidates();
        Log($"Change candidate skill filter: '{value}'");
        return Task.CompletedTask;
    }

    public Task HandleCandidateAvailabilityFilterChanged(string value)
    {
        CandidateAvailabilityFilter = value;
        RefreshCandidates();
        Log($"Change candidate availability filter: '{value}'");
        return Task.CompletedTask;
    }

    public Task HandleStaffingRequestSearchTextChangedAsync(string value)
    {
        StaffingRequestSearchText = value;
        RefreshStaffingRequests();
        Log($"Change staffing request search text: '{value}'");
        return Task.CompletedTask;
    }

    public Task HandleStaffingRequestStatusFilterChangedAsync(StaffingRequestStatus? status)
    {
        StaffingRequestStatusFilter = status;
        RefreshStaffingRequests();
        Log($"Change staffing request status filter: {status}");
        return Task.CompletedTask;
    }

    public Task HandleRelationshipSearchTextChangedAsync(string value)
    {
        RelationshipAssignmentSearchText = value;
        RefreshProjectAssignments();
        Log($"Change relationship search text: '{value}'");
        return Task.CompletedTask;
    }

    public Task HandleAllocationSearchTextChangedAsync(string value)
    {
        AllocationAssignmentSearchText = value;
        RefreshProjectAssignments();
        Log($"Change allocation search text: '{value}'");
        return Task.CompletedTask;
    }

    public Task HandleRelationshipRoleFilterChangedAsync(ProjectPartyAssignmentRole? role)
    {
        RelationshipAssignmentRoleFilter = role;
        RefreshProjectAssignments();
        Log($"Change relationship role filter: {role}");
        return Task.CompletedTask;
    }

    public Task HandleAllocationRoleFilterChangedAsync(ProjectPartyAssignmentRole? role)
    {
        AllocationAssignmentRoleFilter = role;
        RefreshProjectAssignments();
        Log($"Change allocation role filter: {role}");
        return Task.CompletedTask;
    }

    public Task HandleRelationshipPageChangedAsync(int pageIndex)
    {
        RelationshipAssignmentPageIndex = pageIndex;
        RefreshProjectAssignments();
        Log($"Change relationship page: {pageIndex}");
        return Task.CompletedTask;
    }

    public Task HandleAllocationPageChangedAsync(int pageIndex)
    {
        AllocationAssignmentPageIndex = pageIndex;
        RefreshProjectAssignments();
        Log($"Change allocation page: {pageIndex}");
        return Task.CompletedTask;
    }

    public Task HandleStaffingRequestPageChangedAsync(int pageIndex)
    {
        StaffingRequestPageIndex = pageIndex;
        RefreshStaffingRequests();
        Log($"Change staffing request page: {pageIndex}");
        return Task.CompletedTask;
    }

    public Task HandleCandidatePageChangedAsync(int pageIndex)
    {
        StaffingCandidatePageIndex = pageIndex;
        RefreshCandidates();
        Log($"Change candidate page: {pageIndex}");
        return Task.CompletedTask;
    }

    public Task ResetDraftAsync()
    {
        DraftAssignment = new ProjectPartyAssignmentUpsertRequest { ProjectId = SelectedProjectId ?? Guid.Empty };
        Log("Reset relationship draft");
        return Task.CompletedTask;
    }

    public Task ResetAllocationAsync()
    {
        AllocationDraft = new ProjectPartyAssignmentUpsertRequest { ProjectId = SelectedProjectId ?? Guid.Empty };
        Log("Reset allocation draft");
        return Task.CompletedTask;
    }

    public Task ResetStaffingRequestAsync()
    {
        StaffingRequestDraft = new StaffingRequestEditorModel { ProjectId = SelectedProjectId };
        Log("Reset staffing request draft");
        return Task.CompletedTask;
    }

    public Task ResetCandidateFiltersAsync()
    {
        CandidateSearchText = "";
        CandidateSkillFilter = "";
        CandidateAvailabilityFilter = "";
        RefreshCandidates();
        Log("Reset candidate filters");
        return Task.CompletedTask;
    }

    public Task DeleteAssignmentAsync(Guid assignmentId)
    {
        if (SelectedProjectId.HasValue)
        {
            assignmentsByProject[SelectedProjectId.Value].RemoveAll(row => row.Detail.Id == assignmentId);
            RefreshProjectAssignments();
        }

        Log($"Delete assignment: {assignmentId:D}");
        return Task.CompletedTask;
    }

    public Task DeleteAllocationAsync(Guid assignmentId)
    {
        if (SelectedProjectId.HasValue)
        {
            assignmentsByProject[SelectedProjectId.Value].RemoveAll(row => row.Detail.Id == assignmentId);
            RefreshProjectAssignments();
        }

        Log($"Delete allocation: {assignmentId:D}");
        return Task.CompletedTask;
    }

    public Task DeleteStaffingRequestAsync(Guid staffingRequestId)
    {
        if (SelectedProjectId.HasValue)
        {
            GetStaffingRequests(SelectedProjectId.Value).RemoveAll(item => item.Id == staffingRequestId);
            RefreshStaffingRequests();
            RefreshDashboard();
        }

        Log($"Delete staffing request: {staffingRequestId:D}");
        return Task.CompletedTask;
    }

    public bool IsLoading(AssignmentSelectionData data) => IsProjectContextLoading;

    public Task<ProjectNodeDetails?> LoadTaskDetailsAsync(ProjectPartyAssignmentDetail assignment)
        => Task.FromResult<ProjectNodeDetails?>(new ProjectNodeDetails(
            assignment.ProjectId, assignment.NodeKey, ProjectObjectType.WorkItem, "Task", assignment.PartyDisplayName,
            assignment.Role.ToString(), "In progress", "Manual", 40, null, null, ""));

    public void HandleProjectPickerLoadFailed(Exception exception) => Log($"Project picker load failed: {exception.Message}");

    public Task OpenStructure(Guid projectId)
    {
        Log($"Open structure: {ProjectNameOf(projectId)}");
        return Task.CompletedTask;
    }

    public Task OpenGantt(Guid projectId)
    {
        Log($"Open Gantt: {ProjectNameOf(projectId)}");
        return Task.CompletedTask;
    }

    public Task OpenSelectedProjectStructure()
    {
        Log($"Open selected project structure: {SelectedProject?.Name}");
        return Task.CompletedTask;
    }

    public Task OpenSelectedProjectGantt()
    {
        Log($"Open selected project Gantt: {SelectedProject?.Name}");
        return Task.CompletedTask;
    }

    private void SelectProject(Guid projectId)
    {
        SelectedProjectId = projectId;
        var project = CrmHrSandboxData.Projects.First(item => item.Id == projectId);
        SelectedProject = new ProjectRecordQueryItem(project.Id, project.Name, project.Status, project.CurrentPhase, project.Description, project.UpdatedAtUtc)
        {
            LifetimeId = project.LifetimeId
        };
        if (!assignmentsByProject.ContainsKey(projectId))
        {
            assignmentsByProject[projectId] = [];
        }

        RelationshipAssignmentPageIndex = 0;
        AllocationAssignmentPageIndex = 0;
        RefreshProjectAssignments();
        RefreshStaffingRequests();
    }

    private void SeedProject(Guid projectId)
    {
        assignmentsByProject[projectId] =
        [
            new CrmHrSandboxAssignmentRow
            {
                IsAllocation = false,
                Detail = new ProjectPartyAssignmentDetail(CrmHrWorkspaceSandboxIds.Id("assignment-jonas-customer"),
                    projectId, CrmHrSandboxData.JonasKeller, ProjectPartyAssignmentRole.Customer, "Jonas Keller",
                    "Person", ProjectPartyType.Person, "customer", true, null, null, null, "manual", "Primary customer stakeholder.")
            },
            new CrmHrSandboxAssignmentRow
            {
                IsAllocation = true,
                Detail = new ProjectPartyAssignmentDetail(CrmHrWorkspaceSandboxIds.Id("assignment-elena-delivery"),
                    projectId, CrmHrSandboxData.ElenaWard, ProjectPartyAssignmentRole.DeliveryUnit, "Elena Ward",
                    "Person", ProjectPartyType.Person, "delivery-lead", true, 80m,
                    new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero), null, "manual", "")
            },
            new CrmHrSandboxAssignmentRow
            {
                IsAllocation = true,
                Detail = new ProjectPartyAssignmentDetail(CrmHrWorkspaceSandboxIds.Id("assignment-atlas-agent"),
                    projectId, CrmHrSandboxData.AtlasOpsAgent, ProjectPartyAssignmentRole.AiAgent, "Atlas Ops Agent",
                    "AI agent", ProjectPartyType.AiAgent, "ops-agent", false, 20m,
                    new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero), null, "manual", "Automated status reporting.")
            }
        ];
        staffingRequestsByProject[projectId] =
        [
            new StaffingRequestItemModel(CrmHrWorkspaceSandboxIds.Id("staffing-warehouse-engineer"), projectId,
                "Northwind Warehouse Automation", CrmHrSandboxData.ElenaWard, "Elena Ward", CrmHrSandboxData.NorthwindFulfillmentUnit,
                "Northwind Fulfillment Unit", "Backend engineer for routing service", "Software Engineer",
                [SkillCatalog[0]], new DateOnly(2026, 4, 1), new DateOnly(2026, 8, 31), 100m, StaffingRequestStatus.Open, "")
        ];
    }

    private void AddOrUpdateRow(ProjectPartyAssignmentUpsertRequest request, bool isAllocation)
    {
        if (!SelectedProjectId.HasValue)
        {
            return;
        }

        var rows = assignmentsByProject[SelectedProjectId.Value];
        var id = request.AssignmentId ?? CrmHrWorkspaceSandboxIds.Id($"assignment-{Guid.NewGuid():N}");
        rows.RemoveAll(row => row.Detail.Id == id);
        var party = partyStore.Find(request.PartyId);
        rows.Add(new CrmHrSandboxAssignmentRow
        {
            IsAllocation = isAllocation,
            Detail = new ProjectPartyAssignmentDetail(id, SelectedProjectId.Value, request.PartyId, request.Role,
                party?.DisplayName ?? "Unknown party", party?.PartyType.ToString() ?? "Person",
                party?.PartyType == PartyType.AiAgent ? ProjectPartyType.AiAgent : ProjectPartyType.Person,
                string.IsNullOrWhiteSpace(request.NodeKey) ? "manual-node" : request.NodeKey, request.IsPrimary,
                request.AllocationPercent, request.StartsOn.HasValue ? new DateTimeOffset(request.StartsOn.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null,
                request.EndsOn.HasValue ? new DateTimeOffset(request.EndsOn.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null,
                string.IsNullOrWhiteSpace(request.Source) ? "manual" : request.Source, request.Notes)
        });
        RefreshProjectAssignments();
    }

    private void RefreshProjectAssignments()
    {
        if (!SelectedProjectId.HasValue)
        {
            ScheduleAssignments = [];
            RelationshipAssignments = [];
            AllocationAssignments = [];
            AssignmentCounts = ProjectPartyAssignmentCounts.Empty;
            ScheduleAssignmentTotalCount = 0;
            RelationshipAssignmentTotalCount = 0;
            AllocationAssignmentTotalCount = 0;
            return;
        }

        var rows = assignmentsByProject.TryGetValue(SelectedProjectId.Value, out var projectRows) ? projectRows : [];
        ScheduleAssignments = rows.Select(row => row.Detail).ToList();
        ScheduleAssignmentTotalCount = rows.Count;

        var relationshipRows = rows.Where(row => !row.IsAllocation);
        if (RelationshipAssignmentRoleFilter.HasValue)
        {
            relationshipRows = relationshipRows.Where(row => row.Detail.Role == RelationshipAssignmentRoleFilter);
        }

        if (!string.IsNullOrWhiteSpace(RelationshipAssignmentSearchText))
        {
            var searchText = RelationshipAssignmentSearchText.Trim();
            relationshipRows = relationshipRows.Where(row => row.Detail.PartyDisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        var relationshipOrdered = relationshipRows.ToList();
        RelationshipAssignmentTotalCount = relationshipOrdered.Count;
        RelationshipAssignments = relationshipOrdered
            .Skip(RelationshipAssignmentPageIndex * CrmHrAssignmentsWorkspacePresentation.AssignmentPageSize)
            .Take(CrmHrAssignmentsWorkspacePresentation.AssignmentPageSize)
            .Select(row => row.Detail).ToList();

        var allocationRows = rows.Where(row => row.IsAllocation);
        if (AllocationAssignmentRoleFilter.HasValue)
        {
            allocationRows = allocationRows.Where(row => row.Detail.Role == AllocationAssignmentRoleFilter);
        }

        if (!string.IsNullOrWhiteSpace(AllocationAssignmentSearchText))
        {
            var searchText = AllocationAssignmentSearchText.Trim();
            allocationRows = allocationRows.Where(row => row.Detail.PartyDisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        var allocationOrdered = allocationRows.ToList();
        AllocationAssignmentTotalCount = allocationOrdered.Count;
        AllocationAssignments = allocationOrdered
            .Skip(AllocationAssignmentPageIndex * CrmHrAssignmentsWorkspacePresentation.AssignmentPageSize)
            .Take(CrmHrAssignmentsWorkspacePresentation.AssignmentPageSize)
            .Select(row => row.Detail).ToList();

        AssignmentCounts = new ProjectPartyAssignmentCounts(rows.Count, allocationOrdered.Count, rows.Count);
    }

    private void RefreshStaffingRequests()
    {
        if (!SelectedProjectId.HasValue)
        {
            StaffingRequests = [];
            StaffingRequestTotalCount = 0;
            return;
        }

        var requests = GetStaffingRequests(SelectedProjectId.Value).AsEnumerable();
        if (StaffingRequestStatusFilter.HasValue)
        {
            requests = requests.Where(item => item.Status == StaffingRequestStatusFilter);
        }

        if (!string.IsNullOrWhiteSpace(StaffingRequestSearchText))
        {
            var searchText = StaffingRequestSearchText.Trim();
            requests = requests.Where(item => item.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        var ordered = requests.ToList();
        StaffingRequestTotalCount = ordered.Count;
        StaffingRequests = ordered
            .Skip(StaffingRequestPageIndex * CrmHrAssignmentsWorkspacePresentation.StaffingPageSize)
            .Take(CrmHrAssignmentsWorkspacePresentation.StaffingPageSize).ToList();
    }

    private void RefreshCandidates()
    {
        var candidates = partyStore.Snapshot()
            .Where(party => party.Classification.HasValue && party.Classification != WorkforceRecordClassification.ExternalContact
                && party.Classification != WorkforceRecordClassification.DeliveryUnit);

        if (!string.IsNullOrWhiteSpace(CandidateSearchText))
        {
            var searchText = CandidateSearchText.Trim();
            candidates = candidates.Where(party => party.DisplayName.Contains(searchText, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(CandidateSkillFilter))
        {
            candidates = candidates.Where(party => party.SkillSummary.Contains(CandidateSkillFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(CandidateAvailabilityFilter) &&
            Enum.TryParse<WorkforceAvailabilityState>(CandidateAvailabilityFilter, ignoreCase: true, out var state))
        {
            candidates = candidates.Where(party => party.AvailabilityState == state);
        }

        var ordered = candidates.OrderBy(party => party.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        StaffingCandidateTotalCount = ordered.Count;
        StaffingCandidates = ordered
            .Skip(StaffingCandidatePageIndex * CrmHrAssignmentsWorkspacePresentation.StaffingPageSize)
            .Take(CrmHrAssignmentsWorkspacePresentation.StaffingPageSize)
            .Select(party => new StaffingCandidateItemModel(party.Id, party.DisplayName, party.PartyType, party.JobTitle,
                party.Discipline, party.Seniority, party.Location, party.SkillSummary,
                party.AvailabilityState ?? WorkforceAvailabilityState.Bench, party.AvailablePercent, party.NextAvailabilityOn,
                party.Classification ?? WorkforceRecordClassification.ExternalContact, "", ""))
            .ToList();
    }

    private void RefreshDashboard()
    {
        var allRequests = staffingRequestsByProject.Values.SelectMany(list => list).ToList();
        var openRequests = allRequests.Where(item => item.Status == StaffingRequestStatus.Open).ToList();
        var demand = openRequests.Count == 0 ? 0m : openRequests.Average(item => item.AllocationPercent);
        var benchCount = partyStore.Snapshot().Count(party => party.AvailabilityState == WorkforceAvailabilityState.Bench);
        var overallocatedCount = partyStore.Snapshot().Count(party => party.AvailabilityState == WorkforceAvailabilityState.Overallocated);
        StaffingDashboard = new StaffingDashboardModel(openRequests.Count, demand, benchCount, overallocatedCount);
    }

    private List<StaffingRequestItemModel> GetStaffingRequests(Guid projectId)
    {
        if (!staffingRequestsByProject.TryGetValue(projectId, out var list))
        {
            list = [];
            staffingRequestsByProject[projectId] = list;
        }

        return list;
    }

    private static string ProjectNameOf(Guid projectId)
        => CrmHrSandboxData.Projects.FirstOrDefault(project => project.Id == projectId)?.Name ?? "Unknown project";

    private void Log(string message) => IntentLog = message;
}
