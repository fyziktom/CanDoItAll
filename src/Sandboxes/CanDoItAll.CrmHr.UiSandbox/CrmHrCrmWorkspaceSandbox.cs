using CanDoItAll.CrmHr.UI.Activity;
using CanDoItAll.CrmHr.UI.Crm;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.CrmHr.UiSandbox;

public enum CrmHrCrmWorkspaceSandboxScenario
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

public sealed record CrmHrCrmWorkspaceSandboxContext(
    CrmHrCrmWorkspaceSandboxScenario Scenario = CrmHrCrmWorkspaceSandboxScenario.Catalog,
    CrmHrHomeSandboxLayout Layout = CrmHrHomeSandboxLayout.Matched)
{
    public static CrmHrCrmWorkspaceSandboxContext Parse(string? scenario, string? layout)
        => new(ParseScenario(scenario), CrmHrHomeSandboxContext.ParseLayout(layout));

    public static CrmHrCrmWorkspaceSandboxScenario ParseScenario(string? token)
        => Enum.GetValues<CrmHrCrmWorkspaceSandboxScenario>()
            .FirstOrDefault(scenario => string.Equals(Token(scenario), token?.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string Token(CrmHrCrmWorkspaceSandboxScenario scenario)
        => string.Concat(scenario.ToString().Select((character, index) =>
            char.IsUpper(character) && index > 0 ? $"-{char.ToLowerInvariant(character)}" : char.ToLowerInvariant(character).ToString()));

    public IReadOnlyDictionary<string, object?> ToQuery() => new Dictionary<string, object?>
    {
        ["scenario"] = Token(Scenario),
        ["layout"] = CrmHrHomeSandboxContext.Token(Layout)
    };
}

// Deterministic local state for the real Crm workspace surface. Nothing here calls a query service, persists,
// navigates or reaches an agent context. Saving an opportunity updates the shared opportunity store so the real
// pipeline picks it up on its next read.
public sealed class CrmHrCrmSandboxView(CrmHrSandboxPartyStore partyStore, CrmHrSandboxOpportunityStore opportunityStore) : ICrmHrCrmWorkspaceView
{
    public static readonly Guid SandboxProjectDatabaseProfileId = CrmHrWorkspaceSandboxIds.Id("sandbox-project-database-profile");

    public Action? RenderRequested { get; set; }

    public string IntentLog { get; private set; } = "No intent yet.";

    public Guid ProjectDatabaseProfileId => SandboxProjectDatabaseProfileId;

    public CrmHrActivityPresentation AccountActivityPresentation { get; private set; } = CrmHrActivityPresentation.Ready(CrmHrActivityPage.Empty());

    public string? AccountActivityFailureMessage { get; private set; }

    public CrmAccountWorkspaceModel? SelectedAccount { get; private set; }

    public CrmAccountProfileEditorModel ProfileEditor { get; private set; } = new();

    public List<CrmAccountConnectionEditorModel> ConnectedRecordEditors { get; private set; } = [];

    public CrmInteractionEditorModel InteractionEditor { get; private set; } = new();

    public CrmOpportunityEditorModel OpportunityEditor { get; private set; } = new();

    public CrmOpportunityConversionEditorModel OpportunityConversionEditor { get; private set; } = new();

    public int AccountCount => partyStore.Snapshot().Count(party => party.PartyType == PartyType.Organization);

    public Guid? SelectedOpportunityId { get; private set; }

    public int? ConnectionPartyPickerIndex { get; private set; }

    public string? Message { get; private set; }

    public long OpportunityPipelineRefreshVersion { get; private set; }

    public bool IsOpportunityBusy { get; private set; }

    public bool IsOpportunityCreateDialogOpen { get; private set; }

    public bool IsOpportunityDetailDialogOpen { get; private set; }

    public bool IsOpportunityEditDialogOpen { get; private set; }

    public bool IsOpportunityConversionDialogOpen { get; private set; }

    public bool IsAccountDialogOpen { get; private set; }

    public bool IsOpportunityConversionBusy { get; private set; }

    public int SelectedCrmRecordTabIndex { get; private set; }

    public IReadOnlyList<PartyOptionModel> ParticipantOptions => ConnectedRecordEditors
        .Where(editor => editor.RelatedPartyId != Guid.Empty)
        .Select(editor => new PartyOptionModel(editor.RelatedPartyId, ResolveConnectionPartyLabel(editor.RelatedPartyId), PartyType.Person))
        .ToList();

    public CrmOpportunityDetailModel? SelectedOpportunity { get; private set; }

    public Guid? SelectedConnectionPartyId => ConnectionPartyPickerIndex is { } index && index < ConnectedRecordEditors.Count
        ? ConnectedRecordEditors[index].RelatedPartyId
        : null;

    public void RequestRender() => RenderRequested?.Invoke();

    // Host-only navigation: the secondary tabs slot only logs the destination in this sandbox.
    public void LogNavigation(string route) => Log($"Navigate: {route}");

    public void Apply(CrmHrCrmWorkspaceSandboxScenario next)
    {
        IntentLog = "No intent yet.";
        IsOpportunityCreateDialogOpen = false;
        IsOpportunityDetailDialogOpen = false;
        IsOpportunityEditDialogOpen = false;
        IsOpportunityConversionDialogOpen = false;
        IsOpportunityBusy = false;
        IsOpportunityConversionBusy = false;
        ConnectionPartyPickerIndex = null;
        Message = null;
        AccountActivityFailureMessage = null;
        SelectedOpportunityId = null;
        SelectedOpportunity = null;
        SelectedCrmRecordTabIndex = 0;

        switch (next)
        {
            case CrmHrCrmWorkspaceSandboxScenario.Catalog:
                SelectedAccount = null;
                IsAccountDialogOpen = false;
                break;
            case CrmHrCrmWorkspaceSandboxScenario.NewDraft:
                OpenAccount(CrmHrSandboxData.NorthwindLogistics);
                OpportunityEditor = new CrmOpportunityEditorModel { AccountPartyId = CrmHrSandboxData.NorthwindLogistics };
                IsOpportunityCreateDialogOpen = true;
                break;
            case CrmHrCrmWorkspaceSandboxScenario.NonDefaultSection:
                OpenAccount(CrmHrSandboxData.NorthwindLogistics);
                SelectedCrmRecordTabIndex = 4;
                var opportunity = opportunityStore.Snapshot().First(item => item.Id == CrmHrSandboxData.OpportunityFulfillmentExpansion);
                SelectOpportunity(opportunity.Id);
                break;
            case CrmHrCrmWorkspaceSandboxScenario.Loading:
                OpenAccount(CrmHrSandboxData.NorthwindLogistics);
                SelectedCrmRecordTabIndex = 3;
                AccountActivityPresentation = CrmHrActivityPresentation.Loading();
                IsOpportunityBusy = true;
                break;
            case CrmHrCrmWorkspaceSandboxScenario.Failed:
                OpenAccount(CrmHrSandboxData.NorthwindLogistics);
                SelectedCrmRecordTabIndex = 3;
                AccountActivityFailureMessage = "The account activity history could not be loaded.";
                Message = "The opportunity could not be saved. Retry.";
                break;
            case CrmHrCrmWorkspaceSandboxScenario.UnavailableReferences:
                OpenAccount(CrmHrSandboxData.NorthwindLogistics);
                ConnectedRecordEditors =
                [
                    new CrmAccountConnectionEditorModel
                    {
                        Id = CrmHrWorkspaceSandboxIds.Id("unavailable-connection"),
                        RelatedPartyId = CrmHrSandboxData.MissingParty,
                        Role = CrmAccountConnectionRole.Stakeholder,
                        Notes = "This connected record is no longer in the directory."
                    }
                ];
                SelectedCrmRecordTabIndex = 2;
                break;
            case CrmHrCrmWorkspaceSandboxScenario.Empty:
                OpenAccount(CrmHrSandboxData.BeaconHillAdvisory);
                break;
            case CrmHrCrmWorkspaceSandboxScenario.Busy:
                OpenAccount(CrmHrSandboxData.NorthwindLogistics);
                var busyOpportunity = opportunityStore.Snapshot().First(item => item.Id == CrmHrSandboxData.OpportunityWarehouseRenewal);
                SelectOpportunity(busyOpportunity.Id);
                IsOpportunityDetailDialogOpen = false;
                IsOpportunityEditDialogOpen = true;
                IsOpportunityBusy = true;
                break;
            default:
                OpenAccount(CrmHrSandboxData.NorthwindLogistics);
                break;
        }
    }

    public Task HandleCrmRecordTabChanged(int selectedIndex)
    {
        SelectedCrmRecordTabIndex = selectedIndex;
        Log($"Change CRM record tab: {selectedIndex}");
        return Task.CompletedTask;
    }

    public Task RetryAccountActivityAsync()
    {
        AccountActivityFailureMessage = null;
        AccountActivityPresentation = CrmHrActivityPresentation.Ready(BuildActivityPage(SelectedAccount?.AccountPartyId ?? Guid.Empty));
        Log("Retry account activity load");
        return Task.CompletedTask;
    }

    public Task SelectAccountAsync(Guid accountPartyId)
    {
        OpenAccount(accountPartyId);
        Log($"Select account: {SelectedAccount?.DisplayName}");
        return Task.CompletedTask;
    }

    public Task CloseAccountDialogAsync()
    {
        IsAccountDialogOpen = false;
        Log("Close account dialog");
        return Task.CompletedTask;
    }

    public void OpenDirectory() => Log("Open directory");

    public Task SaveAccountProfileAsync()
    {
        Log($"Save CRM profile: {ProfileEditor.RelationshipStage}");
        return Task.CompletedTask;
    }

    public void AddConnectedRecord()
    {
        ConnectedRecordEditors.Add(new CrmAccountConnectionEditorModel());
        Log("Add connected record");
    }

    public void RemoveConnectedRecord(int index)
    {
        if (index >= 0 && index < ConnectedRecordEditors.Count)
        {
            ConnectedRecordEditors.RemoveAt(index);
        }

        Log($"Remove connected record: {index}");
    }

    public void OpenConnectionPartyPicker(int index)
    {
        ConnectionPartyPickerIndex = index;
        Log($"Open connection party picker: {index}");
    }

    public Task CloseConnectionPartyPickerAsync()
    {
        ConnectionPartyPickerIndex = null;
        Log("Close connection party picker");
        return Task.CompletedTask;
    }

    public Task ConfirmConnectionPartyAsync(Guid partyId)
    {
        if (ConnectionPartyPickerIndex is { } index && index < ConnectedRecordEditors.Count)
        {
            ConnectedRecordEditors[index].RelatedPartyId = partyId;
        }

        ConnectionPartyPickerIndex = null;
        Log($"Confirm connection party: {ResolveConnectionPartyLabel(partyId)}");
        return Task.CompletedTask;
    }

    public void ClearConnectionParty(int index)
    {
        if (index >= 0 && index < ConnectedRecordEditors.Count)
        {
            ConnectedRecordEditors[index].RelatedPartyId = Guid.Empty;
        }

        Log($"Clear connection party: {index}");
    }

    public void UpdateConnectedRecordProjects(int index, IReadOnlyList<Guid> projectIds)
    {
        if (index >= 0 && index < ConnectedRecordEditors.Count)
        {
            ConnectedRecordEditors[index].ProjectIds = projectIds.ToList();
        }

        Log($"Update connected record projects: {index} ({projectIds.Count})");
    }

    public string ResolveConnectionPartyLabel(Guid partyId)
    {
        if (partyId == Guid.Empty)
        {
            return "No record chosen";
        }

        var party = partyStore.Find(partyId);
        return party is null ? "Unavailable directory record" : party.DisplayName;
    }

    public Task SaveConnectedRecordsAsync()
    {
        Log($"Save connected records: {ConnectedRecordEditors.Count}");
        return Task.CompletedTask;
    }

    public Task SaveInteractionAsync()
    {
        Log($"Log interaction: {InteractionEditor.Subject}");
        InteractionEditor = new CrmInteractionEditorModel();
        return Task.CompletedTask;
    }

    public Task SelectOpportunityAsync(Guid opportunityId)
    {
        SelectOpportunity(opportunityId);
        IsOpportunityDetailDialogOpen = true;
        Log($"Select opportunity: {SelectedOpportunity?.Title}");
        return Task.CompletedTask;
    }

    public Task SaveOpportunityAsync(CrmOpportunityEditorModel model)
    {
        var id = model.Id ?? CrmHrWorkspaceSandboxIds.Id($"draft-opportunity-{Guid.NewGuid():N}");
        var record = new CrmHrSandboxOpportunityRecord(id, model.AccountPartyId, model.Title, model.Stage,
            model.OpportunitySource, model.OwnerPartyId, model.DeliveryUnitPartyId, model.CurrencyCode, model.Amount,
            model.ProbabilityPercent, model.ExpectedCloseOn, model.LinkedProjectId, DateTimeOffset.UtcNow);
        opportunityStore.Upsert(record);
        OpportunityPipelineRefreshVersion++;
        IsOpportunityCreateDialogOpen = false;
        IsOpportunityEditDialogOpen = false;
        IsOpportunityBusy = false;
        Message = null;
        SelectOpportunity(id);
        Log($"Save opportunity: {model.Title}");
        return Task.CompletedTask;
    }

    public Task AdvanceOpportunityStageAsync(Guid opportunityId)
    {
        var record = opportunityStore.Find(opportunityId);
        if (record is not null)
        {
            var nextStage = record.Stage == OpportunityStage.Lost ? record.Stage : record.Stage + 1;
            opportunityStore.Upsert(record with { Stage = nextStage, UpdatedAtUtc = DateTimeOffset.UtcNow });
            OpportunityPipelineRefreshVersion++;
            if (SelectedOpportunityId == opportunityId)
            {
                SelectOpportunity(opportunityId);
            }
        }

        Log($"Advance opportunity stage: {opportunityId:D}");
        return Task.CompletedTask;
    }

    public void OpenOpportunityCreateDialog()
    {
        OpportunityEditor = new CrmOpportunityEditorModel { AccountPartyId = SelectedAccount?.AccountPartyId ?? Guid.Empty };
        IsOpportunityCreateDialogOpen = true;
        Message = null;
        Log("Open opportunity create dialog");
    }

    public Task CloseOpportunityCreateDialogAsync()
    {
        IsOpportunityCreateDialogOpen = false;
        Log("Close opportunity create dialog");
        return Task.CompletedTask;
    }

    public Task CloseOpportunityDetailDialogAsync()
    {
        IsOpportunityDetailDialogOpen = false;
        Log("Close opportunity detail dialog");
        return Task.CompletedTask;
    }

    public void OpenOpportunityEditDialog()
    {
        if (SelectedOpportunity is { } opportunity)
        {
            OpportunityEditor = OpportunityEditorDrafts.FromDetail(opportunity);
        }

        IsOpportunityDetailDialogOpen = false;
        IsOpportunityEditDialogOpen = true;
        Message = null;
        Log("Open opportunity edit dialog");
    }

    public Task CloseOpportunityEditDialogAsync()
    {
        IsOpportunityEditDialogOpen = false;
        Log("Close opportunity edit dialog");
        return Task.CompletedTask;
    }

    public Task OpenConversionDialogAsync()
    {
        if (SelectedOpportunity is { } opportunity)
        {
            OpportunityConversionEditor = new CrmOpportunityConversionEditorModel
            {
                OpportunityId = opportunity.Id,
                ExpectedUpdatedAtUtc = opportunity.UpdatedAtUtc,
                ProjectName = opportunity.Title,
                ProjectDescription = opportunity.Summary
            };
        }

        IsOpportunityDetailDialogOpen = false;
        IsOpportunityConversionDialogOpen = true;
        Message = null;
        Log("Open opportunity conversion dialog");
        return Task.CompletedTask;
    }

    public Task CloseOpportunityConversionDialogAsync()
    {
        IsOpportunityConversionDialogOpen = false;
        Log("Close opportunity conversion dialog");
        return Task.CompletedTask;
    }

    public Task SaveOpportunityConversionAsync(CrmOpportunityConversionEditorModel model)
    {
        var record = opportunityStore.Find(model.OpportunityId);
        if (record is not null)
        {
            var projectId = model.LinkExistingProject && model.ExistingProjectId.HasValue
                ? model.ExistingProjectId.Value
                : CrmHrWorkspaceSandboxIds.Id($"converted-project-{model.OpportunityId}");
            opportunityStore.Upsert(record with { LinkedProjectId = projectId, UpdatedAtUtc = DateTimeOffset.UtcNow });
            OpportunityPipelineRefreshVersion++;
            SelectOpportunity(model.OpportunityId);
        }

        IsOpportunityConversionDialogOpen = false;
        IsOpportunityConversionBusy = false;
        Log($"Save opportunity conversion: {model.ProjectName}");
        return Task.CompletedTask;
    }

    public Task HandleOpportunityPipelineLoadFailedAsync(Exception exception)
    {
        Message = "The opportunity pipeline could not be loaded.";
        Log($"Opportunity pipeline load failed: {exception.Message}");
        return Task.CompletedTask;
    }

    public Task HandleAccountBrowserLoadFailedAsync(Exception exception)
    {
        Log($"Account browser load failed: {exception.Message}");
        return Task.CompletedTask;
    }

    public Task HandleOpportunityPickerLoadFailedAsync(Exception exception)
    {
        Log($"Opportunity picker load failed: {exception.Message}");
        return Task.CompletedTask;
    }

    public Task OpenLinkedProjectAsync()
    {
        Log($"Open linked project: {SelectedOpportunity?.LinkedProjectName}");
        return Task.CompletedTask;
    }

    public void ToggleInteractionParticipant(Guid partyId, ChangeEventArgs args)
    {
        var isChecked = args.Value is bool value && value;
        if (isChecked)
        {
            if (!InteractionEditor.ParticipantPartyIds.Contains(partyId))
            {
                InteractionEditor.ParticipantPartyIds.Add(partyId);
            }
        }
        else
        {
            InteractionEditor.ParticipantPartyIds.Remove(partyId);
        }

        Log($"Toggle interaction participant: {ResolveConnectionPartyLabel(partyId)}");
    }

    public void OpenAgentChats() => Log("Open agent chats");

    private void OpenAccount(Guid accountPartyId)
    {
        var party = partyStore.Find(accountPartyId) ?? CrmHrSandboxData.RequirePartyRecord(accountPartyId);
        var isNorthwind = accountPartyId == CrmHrSandboxData.NorthwindLogistics;
        var connections = isNorthwind
            ?
            [
                new CrmAccountConnectionEditorModel
                {
                    Id = CrmHrWorkspaceSandboxIds.Id("connection-jonas"),
                    RelatedPartyId = CrmHrSandboxData.JonasKeller,
                    Role = CrmAccountConnectionRole.PrimaryContact,
                    IsPrimary = true,
                    Notes = "Primary operations contact.",
                    ProjectIds = [CrmHrSandboxData.ProjectWarehouseAutomation]
                },
                new CrmAccountConnectionEditorModel
                {
                    Id = CrmHrWorkspaceSandboxIds.Id("connection-mia"),
                    RelatedPartyId = CrmHrSandboxData.MiaTorres,
                    Role = CrmAccountConnectionRole.BillingContact,
                    Notes = "Billing escalation contact."
                }
            ]
            : new List<CrmAccountConnectionEditorModel>();

        ProfileEditor = new CrmAccountProfileEditorModel
        {
            AccountPartyId = party.Id,
            RelationshipStage = isNorthwind ? CrmAccountRelationshipStage.ActiveCustomer : CrmAccountRelationshipStage.Prospect,
            CommercialNotes = isNorthwind ? "Renewal in negotiation; keep pricing consistent with last term." : "",
            LastChangedBy = "crm-hr-sandbox"
        };

        SelectedAccount = new CrmAccountWorkspaceModel(
            party.Id, party.DisplayName, party.Summary, party.LifecycleStatus,
            [PartyRoleKind.Customer], party.Tags, party.PrimaryEmail, party.PrimaryPhone,
            ProfileEditor,
            connections.Select(editor => new CrmAccountConnectedRecordItemModel(
                editor.Id ?? Guid.Empty, editor.RelatedPartyId, ResolveConnectionPartyLabel(editor.RelatedPartyId),
                PartyType.Person, editor.Role, editor.IsPrimary, editor.Notes,
                editor.ProjectIds.Select(projectId => new CrmAccountConnectionProjectItemModel(
                    projectId, ProjectNameOf(projectId), ProjectStatus.Active)).ToList())).ToList(),
            connections.Select(editor => new PartyOptionModel(editor.RelatedPartyId, ResolveConnectionPartyLabel(editor.RelatedPartyId), PartyType.Person)).ToList(),
            isNorthwind ? opportunityStore.Snapshot().Count(item => item.AccountPartyId == accountPartyId) : 0);

        ConnectedRecordEditors = connections;
        IsAccountDialogOpen = true;
        InteractionEditor = new CrmInteractionEditorModel();
        AccountActivityPresentation = CrmHrActivityPresentation.Ready(BuildActivityPage(accountPartyId));
    }

    private void SelectOpportunity(Guid opportunityId)
    {
        var record = opportunityStore.Find(opportunityId);
        if (record is null)
        {
            return;
        }

        SelectedOpportunityId = opportunityId;
        SelectedOpportunity = new CrmOpportunityDetailModel(
            record.Id, record.AccountPartyId, partyStore.DisplayNameOf(record.AccountPartyId), record.Title, record.Stage,
            ProfileEditor.RelationshipStage.ToString(), record.Source, record.OwnerPartyId, partyStore.DisplayNameOf(record.OwnerPartyId),
            record.DeliveryUnitPartyId, record.DeliveryUnitPartyId.HasValue ? partyStore.DisplayNameOf(record.DeliveryUnitPartyId.Value) : "",
            record.CurrencyCode, record.Amount, record.ProbabilityPercent, record.ExpectedCloseOn, "", "", "",
            $"Opportunity for {partyStore.DisplayNameOf(record.AccountPartyId)}.", "", record.LinkedProjectId,
            record.LinkedProjectId.HasValue ? "Northwind Warehouse Automation" : "",
            [], [new OpportunityStageHistoryItemModel(CrmHrWorkspaceSandboxIds.Id($"stage-{opportunityId}"), record.Stage, record.UpdatedAtUtc, "crm-hr-sandbox", "")],
            record.UpdatedAtUtc);
    }

    private static CrmHrActivityPage BuildActivityPage(Guid accountPartyId)
        => accountPartyId == CrmHrSandboxData.NorthwindLogistics
            ?
            new CrmHrActivityPage(
                [
                    new CrmHrActivityEntry(CrmHrWorkspaceSandboxIds.Id("crm-activity-1"), "Interaction", "Quarterly review",
                        "Confirmed the renewal scope.", "Meeting / Jonas Keller", CrmHrSandboxData.BaseUpdatedAtUtc, CrmHrActivityTone.Info, false),
                    new CrmHrActivityEntry(CrmHrWorkspaceSandboxIds.Id("crm-activity-2"), "Audit", "CRM account profile saved",
                        "Updated", "crm-hr-ui", CrmHrSandboxData.BaseUpdatedAtUtc.AddDays(-2), CrmHrActivityTone.Neutral, false)
                ], 0, 10, 2, 1, 0)
            : CrmHrActivityPage.Empty();

    private static string ProjectNameOf(Guid projectId)
        => CrmHrSandboxData.Projects.FirstOrDefault(project => project.Id == projectId)?.Name ?? "Unknown project";

    private void Log(string message) => IntentLog = message;
}
