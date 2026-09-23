using CanDoItAll.CrmHr.UI.Activity;
using CanDoItAll.CrmHr.UI.Parties;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.CrmHr.UiSandbox;

public enum CrmHrDirectoryWorkspaceSandboxScenario
{
    Catalog,
    SelectedExisting,
    NewDraft,
    NonDefaultSection,
    Loading,
    Failed,
    UnavailableReferences,
    Empty
}

public sealed record CrmHrDirectoryWorkspaceSandboxContext(
    CrmHrDirectoryWorkspaceSandboxScenario Scenario = CrmHrDirectoryWorkspaceSandboxScenario.Catalog,
    CrmHrHomeSandboxLayout Layout = CrmHrHomeSandboxLayout.Matched)
{
    public static CrmHrDirectoryWorkspaceSandboxContext Parse(string? scenario, string? layout)
        => new(ParseScenario(scenario), CrmHrHomeSandboxContext.ParseLayout(layout));

    public static CrmHrDirectoryWorkspaceSandboxScenario ParseScenario(string? token)
        => Enum.GetValues<CrmHrDirectoryWorkspaceSandboxScenario>()
            .FirstOrDefault(scenario => string.Equals(Token(scenario), token?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string Token(CrmHrDirectoryWorkspaceSandboxScenario scenario)
        => string.Concat(scenario.ToString().Select((character, index) =>
            char.IsUpper(character) && index > 0 ? $"-{char.ToLowerInvariant(character)}" : char.ToLowerInvariant(character).ToString()));

    public IReadOnlyDictionary<string, object?> ToQuery() => new Dictionary<string, object?>
    {
        ["scenario"] = Token(Scenario),
        ["layout"] = CrmHrHomeSandboxContext.Token(Layout)
    };
}

// Deterministic local state for the real Directory workspace surface. Nothing here calls a query service, persists,
// navigates or reaches an agent context; every mutation-shaped method only updates local state and appends an
// intent line. The shared party store makes a saved or merged-away record visibly reflect in the real catalog.
public sealed class CrmHrDirectorySandboxView(CrmHrSandboxPartyStore partyStore) : ICrmHrDirectoryWorkspaceView
{
    private CrmHrDirectoryWorkspaceSandboxScenario scenario = CrmHrDirectoryWorkspaceSandboxScenario.Catalog;

    public Action? RenderRequested { get; set; }

    public string IntentLog { get; private set; } = "No intent yet.";

    public CrmHrActivityPresentation PartyActivityPresentation { get; private set; } = CrmHrActivityPresentation.Ready(CrmHrActivityPage.Empty());

    public string? PartyActivityFailureMessage { get; private set; }

    public PartyEditorViewModel Editor { get; private set; } = PartyEditorViewModel.CreateNew();

    public IReadOnlyList<PartyOrganizationAffiliationEditorModel> AffiliationEditors { get; private set; } = [];

    public IReadOnlyDictionary<Guid, string> AffiliationPartyDisplayNames { get; private set; } = new Dictionary<Guid, string>();

    public List<PartyRelationshipEditorModel> Relationships { get; private set; } = [];

    public IReadOnlyList<PartyDuplicateCandidateModel> DuplicateCandidates { get; private set; } = [];

    public PartyProjectAssignmentPage ProjectAssignmentsPage { get; private set; } = PartyProjectAssignmentPage.Empty();

    public PartyDuplicateCandidateModel? MergeCandidate { get; private set; }

    public bool IsEditorDialogOpen { get; private set; }

    public bool IsMergeDialogOpen { get; private set; }

    public bool IsImportExportDialogOpen { get; private set; }

    public bool IsRelationshipsLoading { get; private set; }

    public bool IsProjectAssignmentsLoading { get; private set; }

    public long DirectoryBrowserVersion { get; private set; }

    public int SelectedEditorTabIndex { get; private set; }

    public string RelationshipsLoadError { get; private set; } = "";

    public string ProjectAssignmentsLoadError { get; private set; } = "";

    public string EditorTitle => Editor.Id.HasValue
        ? (string.IsNullOrWhiteSpace(Editor.DisplayName) ? "Party record" : Editor.DisplayName)
        : "New party record";

    public PartyOrganizationAffiliationListItemModel? CurrentPrimaryAffiliation { get; private set; }

    public string OtherCurrentAffiliationsTooltip { get; private set; } = "No other current affiliations.";

    public string ProjectAssignmentsPageText => ProjectAssignmentsPage.TotalPages == 0
        ? "No pages"
        : $"Page {ProjectAssignmentsPage.PageIndex + 1} of {ProjectAssignmentsPage.TotalPages}";

    public bool CanMoveProjectAssignmentsPrevious => ProjectAssignmentsPage.PageIndex > 0;

    public bool CanMoveProjectAssignmentsNext => ProjectAssignmentsPage.PageIndex + 1 < ProjectAssignmentsPage.TotalPages;

    public void RequestRender() => RenderRequested?.Invoke();

    // Host-only navigation: the secondary tabs slot only logs the destination in this sandbox.
    public void LogNavigation(string route) => Log($"Navigate: {route}");

    public void Apply(CrmHrDirectoryWorkspaceSandboxScenario next)
    {
        scenario = next;
        IntentLog = "No intent yet.";
        IsMergeDialogOpen = false;
        MergeCandidate = null;
        IsImportExportDialogOpen = false;
        PartyActivityFailureMessage = null;
        PartyActivityPresentation = CrmHrActivityPresentation.Ready(CrmHrActivityPage.Empty());

        switch (next)
        {
            case CrmHrDirectoryWorkspaceSandboxScenario.NewDraft:
                OpenNewDraft();
                break;
            case CrmHrDirectoryWorkspaceSandboxScenario.SelectedExisting:
                OpenExisting(CrmHrSandboxData.JonasKeller, tabIndex: 0);
                break;
            case CrmHrDirectoryWorkspaceSandboxScenario.NonDefaultSection:
                OpenExisting(CrmHrSandboxData.JonasKeller, tabIndex: 4);
                DuplicateCandidates =
                [
                    new(CrmHrSandboxData.MiaTorres, "Mia Torres", PartyType.Person, PartyLifecycleStatus.Active,
                        "Finance manager at Northwind Logistics; billing contact.",
                        ["Shared external code prefix", "Overlapping phone area code"]),
                    new(CrmHrSandboxData.LiamOConnor, "Liam O'Connor", PartyType.Person, PartyLifecycleStatus.Active,
                        "Primary contact at Beacon Hill Advisory.", ["Similar display name"])
                ];
                break;
            case CrmHrDirectoryWorkspaceSandboxScenario.Loading:
                OpenExisting(CrmHrSandboxData.ElenaWard, tabIndex: 4);
                IsRelationshipsLoading = true;
                IsProjectAssignmentsLoading = true;
                PartyActivityPresentation = CrmHrActivityPresentation.Loading();
                break;
            case CrmHrDirectoryWorkspaceSandboxScenario.Failed:
                OpenExisting(CrmHrSandboxData.ElenaWard, tabIndex: 4);
                RelationshipsLoadError = "Relationship data could not be loaded.";
                ProjectAssignmentsLoadError = "Assignment history could not be loaded.";
                PartyActivityFailureMessage = "The party activity history could not be loaded.";
                break;
            case CrmHrDirectoryWorkspaceSandboxScenario.UnavailableReferences:
                OpenUnavailableReferences();
                break;
            case CrmHrDirectoryWorkspaceSandboxScenario.Empty:
                OpenExisting(CrmHrSandboxData.GraceKim, tabIndex: 4);
                Relationships = [];
                DuplicateCandidates = [];
                ProjectAssignmentsPage = PartyProjectAssignmentPage.Empty();
                break;
            default:
                IsEditorDialogOpen = false;
                Editor = PartyEditorViewModel.CreateNew();
                SelectedEditorTabIndex = 0;
                Relationships = [];
                AffiliationEditors = [];
                AffiliationPartyDisplayNames = new Dictionary<Guid, string>();
                DuplicateCandidates = [];
                ProjectAssignmentsPage = PartyProjectAssignmentPage.Empty();
                break;
        }
    }

    public Task SelectPartyAsync(Guid partyId)
    {
        OpenExisting(partyId, tabIndex: 0);
        Log($"Select party: {Editor.DisplayName}");
        return Task.CompletedTask;
    }

    public Task CreateNewAsync()
    {
        OpenNewDraft();
        Log("Create new party");
        return Task.CompletedTask;
    }

    public Task CloseEditorDialogAsync()
    {
        IsEditorDialogOpen = false;
        Log("Close editor dialog");
        return Task.CompletedTask;
    }

    public Task SaveAsync()
    {
        var isNew = !Editor.Id.HasValue;
        var id = Editor.Id ?? CrmHrWorkspaceSandboxIds.Id($"draft-party-{Guid.NewGuid():N}");
        Editor.Id = id;
        Editor.UpdatedAtUtc = DateTimeOffset.UtcNow;
        Editor.LastChangedBy = "crm-hr-sandbox";

        var record = BuildPartyRecord(id);
        partyStore.Upsert(record);
        DirectoryBrowserVersion++;

        Log(isNew ? $"Save party: created {Editor.DisplayName}" : $"Save party: updated {Editor.DisplayName}");
        return Task.CompletedTask;
    }

    public Task<string> ExportPartyCsvAsync(PartyCsvExportScope scope, IReadOnlyList<Guid> partyIds)
    {
        var rows = scope == PartyCsvExportScope.EntireDirectory
            ? partyStore.Snapshot()
            : partyStore.Snapshot().Where(party => partyIds.Contains(party.Id)).ToList();
        var csv = string.Join('\n', rows.Select(party => $"{party.ExternalCode},{party.DisplayName},{party.PartyType}"));
        Log($"Export party CSV: {scope} ({rows.Count} row(s))");
        return Task.FromResult(csv);
    }

    public Task<Result<PartyCsvImportPreviewModel>> PreviewPartyImportAsync(string csvContent)
    {
        var rows = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select((line, index) =>
            {
                var columns = line.Split(',');
                var displayName = columns.Length > 0 ? columns[0] : $"Imported party {index + 1}";
                return new PartyCsvImportPreviewRowModel
                {
                    RowNumber = index + 1,
                    Party = new PartyEditorModel { DisplayName = displayName, PartyType = PartyType.Person },
                    Messages = [],
                    DuplicateCandidates = [],
                    CanImport = true
                };
            })
            .ToList();
        Log($"Preview party import: {rows.Count} row(s)");
        return Task.FromResult(Result<PartyCsvImportPreviewModel>.Success(new PartyCsvImportPreviewModel { Rows = rows }));
    }

    public Task<Result<int>> ApplyPartyImportAsync(IReadOnlyList<PartyCsvImportPreviewRowModel> rows)
    {
        foreach (var row in rows.Where(item => item.CanImport))
        {
            var id = CrmHrWorkspaceSandboxIds.Id($"import-{row.RowNumber}-{row.Party.DisplayName}");
            partyStore.Upsert(BuildPartyRecord(id, row.Party.DisplayName, row.Party.PartyType));
        }

        DirectoryBrowserVersion++;
        Log($"Apply party import: {rows.Count(item => item.CanImport)} row(s)");
        return Task.FromResult(Result<int>.Success(rows.Count(item => item.CanImport)));
    }

    public Task HandleImportAppliedAsync()
    {
        IsImportExportDialogOpen = false;
        Log("Import applied");
        return Task.CompletedTask;
    }

    public void OpenImportExportDialog()
    {
        IsImportExportDialogOpen = true;
        Log("Open import/export dialog");
    }

    public Task CloseImportExportDialogAsync()
    {
        IsImportExportDialogOpen = false;
        Log("Close import/export dialog");
        return Task.CompletedTask;
    }

    public Task HandleEditorTabChangedAsync(int index)
    {
        SelectedEditorTabIndex = index;
        Log($"Change editor tab: {index}");
        return Task.CompletedTask;
    }

    public Task HandleProjectAssignmentsPageRequestedAsync(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= Math.Max(ProjectAssignmentsPage.TotalPages, 1))
        {
            Log($"Request project assignments page ignored: {pageIndex}");
            return Task.CompletedTask;
        }

        ProjectAssignmentsPage = ProjectAssignmentsPage with { PageIndex = pageIndex };
        Log($"Request project assignments page: {pageIndex}");
        return Task.CompletedTask;
    }

    public Task RetryRelationshipsLoadAsync()
    {
        IsRelationshipsLoading = false;
        RelationshipsLoadError = "";
        Relationships = BuildRelationships(Editor.Id ?? Guid.Empty);
        Log("Retry relationships load");
        return Task.CompletedTask;
    }

    public Task RetryActivityLoadAsync()
    {
        PartyActivityFailureMessage = null;
        PartyActivityPresentation = CrmHrActivityPresentation.Ready(CrmHrActivityPage.Empty());
        Log("Retry activity load");
        return Task.CompletedTask;
    }

    public Task RetryProjectAssignmentsLoadAsync()
    {
        IsProjectAssignmentsLoading = false;
        ProjectAssignmentsLoadError = "";
        ProjectAssignmentsPage = BuildProjectAssignments(Editor.Id ?? Guid.Empty);
        Log("Retry project assignments load");
        return Task.CompletedTask;
    }

    public void AddAdditionalRole()
    {
        Editor.AdditionalRoles.Add(new PartyRoleAssignmentEditorModel { RoleKind = PartyRoleKind.Stakeholder });
        Log("Add additional role");
    }

    public void AddConfidentialNote()
    {
        Editor.ConfidentialNotes.Add(new PartyConfidentialNoteEditorModel
        {
            Category = PartyConfidentialNoteCategories.HumanResources,
            CreatedBy = "crm-hr-sandbox",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        Log("Add confidential note");
    }

    public void RemoveAdditionalRole(PartyRoleAssignmentEditorModel role)
    {
        var removed = Editor.AdditionalRoles.Remove(role);
        Log(removed ? $"Remove additional role: {role.RoleKind}" : "Remove additional role: already removed");
    }

    public void RemoveConfidentialNote(PartyConfidentialNoteEditorModel note)
    {
        var removed = Editor.ConfidentialNotes.Remove(note);
        Log(removed ? $"Remove confidential note: {note.Category}" : "Remove confidential note: already removed");
    }

    public void OpenMergeDialog(PartyDuplicateCandidateModel candidate)
    {
        MergeCandidate = candidate;
        IsMergeDialogOpen = true;
        Log($"Open merge dialog: {candidate.DisplayName}");
    }

    public Task HandleMergeCloseAsync()
    {
        IsMergeDialogOpen = false;
        MergeCandidate = null;
        Log("Close merge dialog");
        return Task.CompletedTask;
    }

    public Task HandleMergeConfirmAsync(string reason)
    {
        if (MergeCandidate is { } candidate)
        {
            partyStore.Remove(candidate.Id);
            DuplicateCandidates = DuplicateCandidates.Where(item => item.Id != candidate.Id).ToList();
            DirectoryBrowserVersion++;
            Log($"Merge confirmed: {candidate.DisplayName} into {Editor.DisplayName} ({reason})");
        }

        IsMergeDialogOpen = false;
        MergeCandidate = null;
        return Task.CompletedTask;
    }

    public Task HandleAffiliationsChangedAsync(IReadOnlyList<PartyOrganizationAffiliationEditorModel> affiliations)
    {
        AffiliationEditors = affiliations;
        Log($"Change affiliations: {affiliations.Count} entr{(affiliations.Count == 1 ? "y" : "ies")}");
        return Task.CompletedTask;
    }

    private void OpenNewDraft()
    {
        IsEditorDialogOpen = true;
        Editor = PartyEditorViewModel.CreateNew();
        SelectedEditorTabIndex = 0;
        Relationships = [];
        AffiliationEditors = [];
        AffiliationPartyDisplayNames = new Dictionary<Guid, string>();
        DuplicateCandidates = [];
        ProjectAssignmentsPage = PartyProjectAssignmentPage.Empty();
        CurrentPrimaryAffiliation = null;
    }

    private void OpenExisting(Guid partyId, int tabIndex)
    {
        var party = partyStore.Find(partyId) ?? CrmHrSandboxData.RequirePartyRecord(partyId);
        IsEditorDialogOpen = true;
        SelectedEditorTabIndex = tabIndex;
        IsRelationshipsLoading = false;
        IsProjectAssignmentsLoading = false;
        RelationshipsLoadError = "";
        ProjectAssignmentsLoadError = "";
        Editor = PartyEditorViewModel.FromEditorModel(new PartyEditorModel
        {
            Id = party.Id,
            PartyType = party.PartyType,
            LifecycleStatus = party.LifecycleStatus,
            DisplayName = party.DisplayName,
            LegalName = party.DisplayName,
            ExternalCode = party.ExternalCode,
            Summary = party.Summary,
            Notes = party.PartyType == PartyType.Person
                ? string.Concat(Enumerable.Repeat($"Stewardship note for {party.DisplayName}. ", 6))
                : "",
            Tags = party.Tags.ToList(),
            IsSensitive = party.IsSensitive,
            LastChangedBy = "crm-hr-sandbox",
            UpdatedAtUtc = party.UpdatedAtUtc,
            ContactPoints = string.IsNullOrWhiteSpace(party.PrimaryEmail)
                ? []
                :
                [
                    new PartyContactPointEditorModel
                    {
                        ContactType = PartyContactType.Email,
                        Label = "Primary email",
                        Value = party.PrimaryEmail,
                        NormalizedValue = party.PrimaryEmail.ToLowerInvariant(),
                        IsPrimary = true,
                        IsPublic = true
                    }
                ]
        });

        var affiliations = CrmHrSandboxData.Affiliations.Where(item => item.PersonPartyId == partyId).ToList();
        AffiliationEditors = affiliations.Select(item => new PartyOrganizationAffiliationEditorModel
        {
            Id = item.Id,
            PersonPartyId = item.PersonPartyId,
            OrganizationPartyId = item.OrganizationPartyId,
            AffiliationKind = item.AffiliationKind,
            IsPrimary = item.IsPrimary,
            JobTitle = item.JobTitle,
            ValidFrom = item.ValidFrom,
            ValidTo = item.ValidTo,
            Notes = item.Notes
        }).ToList();
        AffiliationPartyDisplayNames = affiliations
            .Select(item => item.OrganizationPartyId)
            .Distinct()
            .ToDictionary(orgId => orgId, orgId => partyStore.DisplayNameOf(orgId));
        CurrentPrimaryAffiliation = affiliations.FirstOrDefault(item => item.IsPrimary && item.IsCurrent);
        var otherCount = affiliations.Count(item => item.IsCurrent) - (CurrentPrimaryAffiliation is null ? 0 : 1);
        OtherCurrentAffiliationsTooltip = otherCount > 0
            ? $"{otherCount} other current affiliation(s)."
            : "No other current affiliations.";

        Relationships = BuildRelationships(partyId);
        DuplicateCandidates = [];
        ProjectAssignmentsPage = BuildProjectAssignments(partyId);
        PartyActivityPresentation = CrmHrActivityPresentation.Ready(new CrmHrActivityPage(
            [
                new CrmHrActivityEntry(CrmHrWorkspaceSandboxIds.Id($"activity-{partyId}"), "Audit", "Party record saved",
                    "Updated", "crm-hr-ui", CrmHrSandboxData.BaseUpdatedAtUtc, CrmHrActivityTone.Neutral, false)
            ], 0, 10, 1, 0, 0));
    }

    private void OpenUnavailableReferences()
    {
        OpenExisting(CrmHrSandboxData.ElenaWard, tabIndex: 4);
        AffiliationEditors =
        [
            new PartyOrganizationAffiliationEditorModel
            {
                Id = CrmHrWorkspaceSandboxIds.Id("unavailable-affiliation"),
                PersonPartyId = CrmHrSandboxData.ElenaWard,
                OrganizationPartyId = CrmHrSandboxData.MissingParty,
                AffiliationKind = PartyOrganizationAffiliationKind.Employee,
                IsPrimary = true,
                JobTitle = "Archived organization link",
                Notes = "The organization this affiliation points to is no longer in the directory."
            }
        ];
        AffiliationPartyDisplayNames = new Dictionary<Guid, string>();
        CurrentPrimaryAffiliation = null;
        OtherCurrentAffiliationsTooltip = "Referenced organization is unavailable.";
        Relationships =
        [
            new PartyRelationshipEditorModel
            {
                Id = CrmHrWorkspaceSandboxIds.Id("unavailable-relationship"),
                RelatedPartyId = CrmHrSandboxData.MissingParty,
                RelatedPartyDisplayName = "Unknown party",
                RelatedPartyType = null,
                RelationshipKind = PartyRelationshipKind.ReportsTo,
                IsOutgoing = true,
                Notes = "This related party no longer exists in the directory."
            }
        ];
        ProjectAssignmentsPage = new PartyProjectAssignmentPage(
            [
                new PartyProjectAssignmentItemModel(CrmHrWorkspaceSandboxIds.Id("unavailable-assignment"),
                    CrmHrSandboxData.MissingProject, "Archived Pilot Program (unavailable)", ProjectPartyAssignmentKind.TeamMember,
                    "unavailable-node", 50m, new DateOnly(2025, 1, 1), new DateOnly(2025, 6, 30), false,
                    "This project no longer appears in the project catalog.")
            ], 0, PartyProjectAssignmentQueryLimits.DefaultPageSize, 1);
    }

    private static List<PartyRelationshipEditorModel> BuildRelationships(Guid partyId)
    {
        if (partyId == CrmHrSandboxData.JonasKeller)
        {
            return
            [
                new PartyRelationshipEditorModel
                {
                    Id = CrmHrWorkspaceSandboxIds.Id("relationship-jonas-northwind"),
                    RelatedPartyId = CrmHrSandboxData.NorthwindLogistics,
                    RelatedPartyDisplayName = "Northwind Logistics",
                    RelatedPartyType = PartyType.Organization,
                    RelationshipKind = PartyRelationshipKind.MemberOf,
                    IsOutgoing = true,
                    IsPrimary = true,
                    Notes = "Primary commercial contact for the account."
                },
                new PartyRelationshipEditorModel
                {
                    Id = CrmHrWorkspaceSandboxIds.Id("relationship-jonas-mia"),
                    RelatedPartyId = CrmHrSandboxData.MiaTorres,
                    RelatedPartyDisplayName = "Mia Torres",
                    RelatedPartyType = PartyType.Person,
                    RelationshipKind = PartyRelationshipKind.Supports,
                    IsOutgoing = true,
                    Notes = "Works together on billing escalations."
                }
            ];
        }

        if (partyId == CrmHrSandboxData.ElenaWard)
        {
            return
            [
                new PartyRelationshipEditorModel
                {
                    Id = CrmHrWorkspaceSandboxIds.Id("relationship-elena-victor"),
                    RelatedPartyId = CrmHrSandboxData.VictorHughes,
                    RelatedPartyDisplayName = "Victor Hughes",
                    RelatedPartyType = PartyType.Person,
                    RelationshipKind = PartyRelationshipKind.ReportsTo,
                    IsOutgoing = true,
                    IsPrimary = true
                }
            ];
        }

        return [];
    }

    private static PartyProjectAssignmentPage BuildProjectAssignments(Guid partyId)
    {
        if (partyId == CrmHrSandboxData.JonasKeller)
        {
            return new PartyProjectAssignmentPage(
                [
                    new PartyProjectAssignmentItemModel(CrmHrWorkspaceSandboxIds.Id("assignment-jonas-warehouse"),
                        CrmHrSandboxData.ProjectWarehouseAutomation, "Northwind Warehouse Automation",
                        ProjectPartyAssignmentKind.Customer, "customer", null, new DateOnly(2026, 1, 5), null, true,
                        "Primary customer stakeholder.")
                ], 0, PartyProjectAssignmentQueryLimits.DefaultPageSize, 1);
        }

        if (partyId == CrmHrSandboxData.ElenaWard)
        {
            return new PartyProjectAssignmentPage(
                [
                    new PartyProjectAssignmentItemModel(CrmHrWorkspaceSandboxIds.Id("assignment-elena-warehouse"),
                        CrmHrSandboxData.ProjectWarehouseAutomation, "Northwind Warehouse Automation",
                        ProjectPartyAssignmentKind.DeliveryUnit, "delivery-lead", 20m, new DateOnly(2026, 1, 5), null, true, ""),
                    new PartyProjectAssignmentItemModel(CrmHrWorkspaceSandboxIds.Id("assignment-elena-fulfillment"),
                        CrmHrSandboxData.ProjectFulfillmentRollout, "Northwind Fulfillment Rollout",
                        ProjectPartyAssignmentKind.TeamMember, "team-member", 20m, new DateOnly(2026, 2, 1), null, false, "")
                ], 0, PartyProjectAssignmentQueryLimits.DefaultPageSize, 2);
        }

        return PartyProjectAssignmentPage.Empty();
    }

    private static CrmHrSandboxPartyRecord BuildPartyRecord(Guid id, string? displayName = null, PartyType? partyType = null)
    {
        var record = CrmHrSandboxData.Parties.FirstOrDefault(party => party.Id == id);
        return record is null
            ? new CrmHrSandboxPartyRecord(id, displayName ?? "New party", partyType ?? PartyType.Person, PartyLifecycleStatus.Draft,
                "", "", [], false, "", "", null, "", "", "", "", "", null, 0m, null, null, DateTimeOffset.UtcNow)
            : record with { UpdatedAtUtc = DateTimeOffset.UtcNow };
    }

    private void Log(string message) => IntentLog = message;
}
