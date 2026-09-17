using CanDoItAll.CrmHr.UI.Activity;
using CanDoItAll.CrmHr.UI.Workforce;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.CrmHr.UiSandbox;

public enum CrmHrWorkforceWorkspaceSandboxScenario
{
    Catalog,
    SelectedExisting,
    NewDraft,
    NonDefaultSection,
    Loading,
    Failed,
    UnavailableReferences,
    Empty,
    Busy
}

public sealed record CrmHrWorkforceWorkspaceSandboxContext(
    CrmHrWorkforceWorkspaceSandboxScenario Scenario = CrmHrWorkforceWorkspaceSandboxScenario.Catalog,
    CrmHrHomeSandboxLayout Layout = CrmHrHomeSandboxLayout.Matched)
{
    public static CrmHrWorkforceWorkspaceSandboxContext Parse(string? scenario, string? layout)
        => new(ParseScenario(scenario), CrmHrHomeSandboxContext.ParseLayout(layout));

    public static CrmHrWorkforceWorkspaceSandboxScenario ParseScenario(string? token)
        => Enum.GetValues<CrmHrWorkforceWorkspaceSandboxScenario>()
            .FirstOrDefault(scenario => string.Equals(Token(scenario), token?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string Token(CrmHrWorkforceWorkspaceSandboxScenario scenario)
        => string.Concat(scenario.ToString().Select((character, index) =>
            char.IsUpper(character) && index > 0 ? $"-{char.ToLowerInvariant(character)}" : char.ToLowerInvariant(character).ToString()));

    public IReadOnlyDictionary<string, object?> ToQuery() => new Dictionary<string, object?>
    {
        ["scenario"] = Token(Scenario),
        ["layout"] = CrmHrHomeSandboxContext.Token(Layout)
    };
}

// Deterministic local state for the real Workforce workspace surface. Nothing here calls a query service, persists,
// navigates or reaches an agent context. A saved delivery unit updates the shared party store so it visibly appears
// in the real workforce catalog.
public sealed class CrmHrWorkforceSandboxView(CrmHrSandboxPartyStore partyStore) : ICrmHrWorkforceWorkspaceView
{
    private readonly Dictionary<Guid, List<PartySkillItemModel>> skillsByParty = [];
    private readonly Dictionary<Guid, List<CapacityBlockItemModel>> capacityBlocksByParty = [];

    private static readonly IReadOnlyList<SkillCatalogItemModel> SkillCatalog =
    [
        new(CrmHrWorkspaceSandboxIds.Id("skill-csharp"), "C#", "Engineering", "Backend and platform development.", true),
        new(CrmHrWorkspaceSandboxIds.Id("skill-python"), "Python", "Engineering", "Data pipelines and automation.", true),
        new(CrmHrWorkspaceSandboxIds.Id("skill-ux"), "UX Research", "Design", "User research and prototyping.", true)
    ];

    public Action? RenderRequested { get; set; }

    public string IntentLog { get; private set; } = "No intent yet.";

    public CrmHrActivityPresentation WorkforceHistoryPresentation { get; private set; } = CrmHrActivityPresentation.Ready(CrmHrActivityPage.Empty());

    public string? WorkforceHistoryFailureMessage { get; private set; }

    public DeliveryUnitQuickCreateModel DeliveryUnitEditor { get; private set; } = new();

    public SkillDefinitionEditorModel SkillDefinitionEditor { get; private set; } = new();

    public WorkforceProfileWorkspaceModel? SelectedWorkspace { get; private set; }

    public WorkforceCapacityWorkspaceModel? CapacityWorkspace { get; private set; }

    public WorkforceProfileEditorModel ProfileEditor { get; private set; } = new();

    public PartySkillEditorModel SkillEditor { get; private set; } = new();

    public CapacityBlockEditorModel CapacityBlockEditor { get; private set; } = new();

    public string CapacityWorkspaceError { get; private set; } = "";

    public int SelectedDetailTabIndex { get; private set; }

    public int WorkforceBrowserVersion { get; private set; }

    public bool IsCapacityWorkspaceLoading { get; private set; }

    public bool IsRecordDialogOpen { get; private set; }

    public bool IsProfileCreationRequested { get; private set; }

    public bool IsDeliveryUnitDialogOpen { get; private set; }

    public bool IsDeliveryUnitSaving { get; private set; }

    public WorkforceAvailabilityState? SelectedAvailabilityState { get; private set; }

    public bool HasSelectedAvailabilityDetails { get; private set; }

    public decimal? SelectedAvailablePercent { get; private set; }

    public DateOnly? SelectedNextAvailabilityOn { get; private set; }

    public string SelectedAvailabilityMessage { get; private set; } = "";

    public bool SelectedIsExternalContactWithoutProfile { get; private set; }

    public string SelectedClassificationLabel { get; private set; } = "";

    public string SelectedClassificationTone { get; private set; } = "neutral";

    public string AllocationBadgeText { get; private set; } = "0";

    public IReadOnlyList<WorkforceKind> AvailableWorkforceKinds { get; } = Enum.GetValues<WorkforceKind>();

    public void RequestRender() => RenderRequested?.Invoke();

    // Host-only navigation: the secondary tabs slot only logs the destination in this sandbox.
    public void LogNavigation(string route) => Log($"Navigate: {route}");

    public void Apply(CrmHrWorkforceWorkspaceSandboxScenario next)
    {
        IntentLog = "No intent yet.";
        IsDeliveryUnitDialogOpen = false;
        IsDeliveryUnitSaving = false;
        DeliveryUnitEditor = new DeliveryUnitQuickCreateModel();
        IsProfileCreationRequested = false;
        CapacityWorkspaceError = "";
        WorkforceHistoryFailureMessage = null;
        IsCapacityWorkspaceLoading = false;

        switch (next)
        {
            case CrmHrWorkforceWorkspaceSandboxScenario.Catalog:
                IsRecordDialogOpen = false;
                SelectedWorkspace = null;
                break;
            case CrmHrWorkforceWorkspaceSandboxScenario.NewDraft:
                OpenWorkspace(CrmHrSandboxData.JonasKeller, tabIndex: 0);
                IsProfileCreationRequested = true;
                SelectedDetailTabIndex = 1;
                ProfileEditor = new WorkforceProfileEditorModel { PartyId = CrmHrSandboxData.JonasKeller, WorkforceKind = WorkforceKind.Employee };
                break;
            case CrmHrWorkforceWorkspaceSandboxScenario.SelectedExisting:
                OpenWorkspace(CrmHrSandboxData.ElenaWard, tabIndex: 0);
                break;
            case CrmHrWorkforceWorkspaceSandboxScenario.NonDefaultSection:
                OpenWorkspace(CrmHrSandboxData.SofiaRamirez, tabIndex: 3);
                break;
            case CrmHrWorkforceWorkspaceSandboxScenario.Loading:
                OpenWorkspace(CrmHrSandboxData.ElenaWard, tabIndex: 3);
                IsCapacityWorkspaceLoading = true;
                WorkforceHistoryPresentation = CrmHrActivityPresentation.Loading();
                break;
            case CrmHrWorkforceWorkspaceSandboxScenario.Failed:
                OpenWorkspace(CrmHrSandboxData.ElenaWard, tabIndex: 3);
                CapacityWorkspaceError = "The allocation workspace could not be loaded.";
                WorkforceHistoryFailureMessage = "The workforce history could not be loaded.";
                break;
            case CrmHrWorkforceWorkspaceSandboxScenario.UnavailableReferences:
                OpenUnavailableReferences();
                break;
            case CrmHrWorkforceWorkspaceSandboxScenario.Empty:
                OpenWorkspace(CrmHrSandboxData.IvyChapman, tabIndex: 3);
                break;
            case CrmHrWorkforceWorkspaceSandboxScenario.Busy:
                IsRecordDialogOpen = false;
                SelectedWorkspace = null;
                DeliveryUnitEditor = new DeliveryUnitQuickCreateModel { Name = "Cascade Deployment Unit", ExternalCode = "UNIT-CDU" };
                IsDeliveryUnitDialogOpen = true;
                IsDeliveryUnitSaving = true;
                break;
            default:
                IsRecordDialogOpen = false;
                SelectedWorkspace = null;
                break;
        }
    }

    public Task SelectPartyAsync(Guid partyId)
    {
        OpenWorkspace(partyId, tabIndex: 0);
        Log($"Select party: {SelectedWorkspace?.DisplayName}");
        return Task.CompletedTask;
    }

    public Task CloseRecordDialogAsync()
    {
        IsRecordDialogOpen = false;
        Log("Close record dialog");
        return Task.CompletedTask;
    }

    public Task HandleWorkforceBrowserLoadFailedAsync(Exception exception)
    {
        Log($"Workforce browser load failed: {exception.Message}");
        return Task.CompletedTask;
    }

    public Task HandleHomeUnitChangedAsync(Guid? partyId)
    {
        ProfileEditor.HomeUnitPartyId = partyId;
        Log($"Change home unit: {(partyId.HasValue ? partyStore.DisplayNameOf(partyId.Value) : "None")}");
        return Task.CompletedTask;
    }

    public Task HandleManagerChangedAsync(Guid? partyId)
    {
        ProfileEditor.ManagerPartyId = partyId;
        Log($"Change manager: {(partyId.HasValue ? partyStore.DisplayNameOf(partyId.Value) : "None")}");
        return Task.CompletedTask;
    }

    public Task HandleDetailTabChangedAsync(int index)
    {
        SelectedDetailTabIndex = index;
        Log($"Change detail tab: {index}");
        return Task.CompletedTask;
    }

    public Task RequestProfileCreation()
    {
        IsProfileCreationRequested = true;
        SelectedDetailTabIndex = 1;
        if (SelectedWorkspace is { } workspace)
        {
            ProfileEditor = new WorkforceProfileEditorModel { PartyId = workspace.PartyId, WorkforceKind = WorkforceKind.Employee };
        }

        Log("Request profile creation");
        return Task.CompletedTask;
    }

    public Task RetryHistoryAsync()
    {
        WorkforceHistoryFailureMessage = null;
        WorkforceHistoryPresentation = CrmHrActivityPresentation.Ready(CrmHrActivityPage.Empty());
        Log("Retry workforce history load");
        return Task.CompletedTask;
    }

    public Task RetryCapacityWorkspaceAsync()
    {
        IsCapacityWorkspaceLoading = false;
        CapacityWorkspaceError = "";
        if (SelectedWorkspace is { } workspace)
        {
            CapacityWorkspace = BuildCapacityWorkspace(workspace.PartyId);
        }

        Log("Retry capacity workspace load");
        return Task.CompletedTask;
    }

    public void OpenDeliveryUnitDialog()
    {
        DeliveryUnitEditor = new DeliveryUnitQuickCreateModel();
        IsDeliveryUnitDialogOpen = true;
        Log("Open delivery unit dialog");
    }

    public Task CloseDeliveryUnitDialogAsync()
    {
        IsDeliveryUnitDialogOpen = false;
        IsDeliveryUnitSaving = false;
        Log("Close delivery unit dialog");
        return Task.CompletedTask;
    }

    public void OpenDirectory() => Log("Open directory");

    public Task OpenProjectAsync(Guid projectId)
    {
        Log($"Open project: {projectId:D}");
        return Task.CompletedTask;
    }

    public Task SaveWorkforceProfileAsync()
    {
        if (SelectedWorkspace is { } workspace)
        {
            var record = partyStore.Find(workspace.PartyId);
            if (record is not null)
            {
                partyStore.Upsert(record with
                {
                    Classification = ClassificationFor(ProfileEditor.WorkforceKind),
                    JobTitle = ProfileEditor.JobTitle,
                    Discipline = ProfileEditor.Discipline,
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                });
            }
        }

        IsProfileCreationRequested = false;
        Log("Save workforce profile");
        return Task.CompletedTask;
    }

    public Task SaveSkillDefinitionAsync()
    {
        Log($"Save skill definition: {SkillDefinitionEditor.Name}");
        SkillDefinitionEditor = new SkillDefinitionEditorModel();
        return Task.CompletedTask;
    }

    public Task SavePartySkillAsync()
    {
        if (SelectedWorkspace is { } workspace)
        {
            var skill = SkillCatalog.FirstOrDefault(item => item.Id == SkillEditor.SkillId) ?? SkillCatalog[0];
            var list = GetSkills(workspace.PartyId);
            list.Add(new PartySkillItemModel(CrmHrWorkspaceSandboxIds.Id($"skill-{Guid.NewGuid():N}"), skill.Id, skill.Name,
                skill.Category, SkillEditor.Proficiency, SkillEditor.YearsExperience, SkillEditor.CertificationStatus,
                SkillEditor.LastValidatedOn, SkillEditor.Notes));
            RefreshSelectedWorkspace();
        }

        Log("Save party skill");
        SkillEditor = new PartySkillEditorModel();
        return Task.CompletedTask;
    }

    public Task DeletePartySkillAsync(Guid partySkillId)
    {
        if (SelectedWorkspace is { } workspace)
        {
            GetSkills(workspace.PartyId).RemoveAll(item => item.Id == partySkillId);
            RefreshSelectedWorkspace();
        }

        Log($"Delete party skill: {partySkillId:D}");
        return Task.CompletedTask;
    }

    public Task SaveCapacityBlockAsync()
    {
        if (SelectedWorkspace is { } workspace)
        {
            var blocks = GetCapacityBlocks(workspace.PartyId);
            var start = CapacityBlockEditor.StartDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
            var end = CapacityBlockEditor.EndDate ?? start.AddDays(7);
            blocks.Add(new CapacityBlockItemModel(CrmHrWorkspaceSandboxIds.Id($"capacity-{Guid.NewGuid():N}"),
                CapacityBlockEditor.BlockKind, start, end, CapacityBlockEditor.Percentage, CapacityBlockEditor.RelatedProjectId,
                CapacityBlockEditor.RelatedProjectId.HasValue ? ProjectNameOf(CapacityBlockEditor.RelatedProjectId.Value) : "",
                CapacityBlockEditor.Notes, true, false));
            CapacityWorkspace = BuildCapacityWorkspace(workspace.PartyId);
        }

        Log("Save capacity block");
        CapacityBlockEditor = new CapacityBlockEditorModel();
        return Task.CompletedTask;
    }

    public Task DeleteCapacityBlockAsync(Guid capacityBlockId)
    {
        if (SelectedWorkspace is { } workspace)
        {
            GetCapacityBlocks(workspace.PartyId).RemoveAll(item => item.Id == capacityBlockId);
            CapacityWorkspace = BuildCapacityWorkspace(workspace.PartyId);
        }

        Log($"Delete capacity block: {capacityBlockId:D}");
        return Task.CompletedTask;
    }

    public Task CreateDeliveryUnitAsync()
    {
        var id = CrmHrWorkspaceSandboxIds.Id($"delivery-unit-{Guid.NewGuid():N}");
        partyStore.Upsert(new CrmHrSandboxPartyRecord(id, DeliveryUnitEditor.Name, PartyType.OrganizationUnit,
            PartyLifecycleStatus.Active, DeliveryUnitEditor.ExternalCode, DeliveryUnitEditor.Summary, [], false, "", "",
            WorkforceRecordClassification.DeliveryUnit, "", "", "", "", "", null, 0m, null, null, DateTimeOffset.UtcNow));
        WorkforceBrowserVersion++;
        IsDeliveryUnitDialogOpen = false;
        IsDeliveryUnitSaving = false;
        Log($"Create delivery unit: {DeliveryUnitEditor.Name}");
        return Task.CompletedTask;
    }

    private void OpenWorkspace(Guid partyId, int tabIndex)
    {
        var party = partyStore.Find(partyId) ?? CrmHrSandboxData.RequirePartyRecord(partyId);
        IsRecordDialogOpen = true;
        SelectedDetailTabIndex = tabIndex;

        var isExternalWithoutProfile = party.Classification == WorkforceRecordClassification.ExternalContact;
        SelectedIsExternalContactWithoutProfile = isExternalWithoutProfile && !IsProfileCreationRequested;

        ProfileEditor = new WorkforceProfileEditorModel
        {
            PartyId = party.Id,
            WorkforceKind = party.Classification switch
            {
                WorkforceRecordClassification.Contractor => WorkforceKind.Contractor,
                WorkforceRecordClassification.Freelancer => WorkforceKind.Freelancer,
                WorkforceRecordClassification.DeliveryUnit => WorkforceKind.DeliveryUnit,
                _ => WorkforceKind.Employee
            },
            JobTitle = party.JobTitle,
            Discipline = party.Discipline,
            Seniority = party.Seniority,
            Location = party.Location,
            CapacityHoursPerWeek = 40m,
            Status = party.LifecycleStatus == PartyLifecycleStatus.Active ? "Active" : "Planned",
            LastChangedBy = "crm-hr-sandbox"
        };

        SelectedWorkspace = new WorkforceProfileWorkspaceModel(
            party.Id, party.DisplayName, party.Summary, party.PartyType, party.LifecycleStatus, party.IsSensitive,
            "crm-hr-ui", party.UpdatedAtUtc, [], party.PrimaryEmail, party.PrimaryPhone, "", "",
            ProfileEditor, SkillCatalog, GetSkills(party.Id));

        SelectedAvailabilityState = party.AvailabilityState;
        HasSelectedAvailabilityDetails = party.AvailabilityState.HasValue;
        SelectedAvailablePercent = party.Classification.HasValue ? party.AvailablePercent : null;
        SelectedNextAvailabilityOn = party.NextAvailabilityOn;
        SelectedAvailabilityMessage = party.AvailabilityState switch
        {
            WorkforceAvailabilityState.Bench => "Bench or lightly committed.",
            WorkforceAvailabilityState.Overallocated => "Overallocated across active assignments.",
            WorkforceAvailabilityState.NearAvailable => "Nearing availability soon.",
            WorkforceAvailabilityState.Allocated => "Allocated to active project work.",
            _ => "Availability not tracked for this record."
        };
        SelectedClassificationLabel = party.Classification?.ToString() ?? "Not staffable";
        SelectedClassificationTone = party.Classification switch
        {
            WorkforceRecordClassification.Employee => "success",
            WorkforceRecordClassification.Contractor or WorkforceRecordClassification.Freelancer => "info",
            WorkforceRecordClassification.DeliveryUnit => "neutral",
            _ => "warning"
        };

        CapacityWorkspace = BuildCapacityWorkspace(party.Id);
        AllocationBadgeText = CapacityWorkspace.ProjectAllocations.Count.ToString();
        WorkforceHistoryPresentation = CrmHrActivityPresentation.Ready(new CrmHrActivityPage(
            [
                new CrmHrActivityEntry(CrmHrWorkspaceSandboxIds.Id($"workforce-history-{party.Id}"), "Audit",
                    "Workforce profile saved", "Updated", "crm-hr-ui", party.UpdatedAtUtc, CrmHrActivityTone.Neutral, false)
            ], 0, 10, 1, 0, 0));
    }

    private void OpenUnavailableReferences()
    {
        OpenWorkspace(CrmHrSandboxData.ChloeDubois, tabIndex: 3);
        SelectedWorkspace = SelectedWorkspace! with { HomeUnitName = "Archived Deployment Unit (unavailable)", ManagerName = "Unknown manager" };
        GetCapacityBlocks(CrmHrSandboxData.ChloeDubois).Add(new CapacityBlockItemModel(
            CrmHrWorkspaceSandboxIds.Id("unavailable-capacity-block"), CapacityBlockKind.Reserve,
            new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28), 30m, CrmHrSandboxData.MissingProject,
            "Unavailable project", "This project no longer appears in the catalog.", false, false));
        CapacityWorkspace = BuildCapacityWorkspace(CrmHrSandboxData.ChloeDubois);
    }

    private WorkforceCapacityWorkspaceModel BuildCapacityWorkspace(Guid partyId)
    {
        var blocks = GetCapacityBlocks(partyId);
        var allocations = BuildAllocations(partyId);
        var party = partyStore.Find(partyId);
        var summary = new WorkforceCapacitySummaryModel(
            40m, allocations.Sum(item => item.AllocationPercent), blocks.Sum(item => item.Percentage),
            party?.AvailablePercent ?? 100m, party?.AvailabilityState ?? WorkforceAvailabilityState.Bench,
            SelectedAvailabilityMessage, party?.NextAvailabilityOn,
            party?.AvailabilityState == WorkforceAvailabilityState.Overallocated,
            party?.AvailabilityState is null or WorkforceAvailabilityState.Bench);
        return new WorkforceCapacityWorkspaceModel(partyId, blocks, allocations, summary);
    }

    private static List<ProjectAllocationItemModel> BuildAllocations(Guid partyId)
    {
        if (partyId == CrmHrSandboxData.SofiaRamirez)
        {
            return
            [
                new ProjectAllocationItemModel(CrmHrWorkspaceSandboxIds.Id("allocation-sofia-rnd"),
                    CrmHrSandboxData.ProjectCascadeRnDSprint, "Cascade Robotics R&D Sprint", partyId, "Sofia Ramirez",
                    ProjectPartyAssignmentRole.TeamMember, 100m, new DateOnly(2026, 2, 1), new DateOnly(2026, 6, 30), "", true, false)
            ];
        }

        if (partyId == CrmHrSandboxData.ElenaWard)
        {
            return
            [
                new ProjectAllocationItemModel(CrmHrWorkspaceSandboxIds.Id("allocation-elena-warehouse"),
                    CrmHrSandboxData.ProjectWarehouseAutomation, "Northwind Warehouse Automation", partyId, "Elena Ward",
                    ProjectPartyAssignmentRole.DeliveryUnit, 80m, new DateOnly(2026, 1, 5), null, "", true, false)
            ];
        }

        return [];
    }

    private static WorkforceRecordClassification ClassificationFor(WorkforceKind kind)
        => kind switch
        {
            WorkforceKind.Contractor => WorkforceRecordClassification.Contractor,
            WorkforceKind.Freelancer => WorkforceRecordClassification.Freelancer,
            WorkforceKind.DeliveryUnit => WorkforceRecordClassification.DeliveryUnit,
            _ => WorkforceRecordClassification.Employee
        };

    private static string ProjectNameOf(Guid projectId)
        => CrmHrSandboxData.Projects.FirstOrDefault(project => project.Id == projectId)?.Name ?? "Unknown project";

    private List<PartySkillItemModel> GetSkills(Guid partyId)
    {
        if (!skillsByParty.TryGetValue(partyId, out var list))
        {
            list = [];
            skillsByParty[partyId] = list;
        }

        return list;
    }

    private List<CapacityBlockItemModel> GetCapacityBlocks(Guid partyId)
    {
        if (!capacityBlocksByParty.TryGetValue(partyId, out var list))
        {
            list = [];
            capacityBlocksByParty[partyId] = list;
        }

        return list;
    }

    private void RefreshSelectedWorkspace()
    {
        if (SelectedWorkspace is { } workspace)
        {
            SelectedWorkspace = workspace with { Skills = GetSkills(workspace.PartyId) };
        }
    }

    private void Log(string message) => IntentLog = message;
}
