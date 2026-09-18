using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AppComponents;
using CanDoItAll.CrmHr.UI.Activity;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.CrmHr.UI.Pickers;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;

namespace CanDoItAll.CrmHr.UI.Crm;

// What the Crm workspace renders and what it can ask its host to do. The routed host implements it: it owns the
// state, the reads, the mutations, navigation and the agent context; the surface only binds to it.
public interface ICrmHrCrmWorkspaceView : ICrmHrWorkspaceView
{
    Guid ProjectDatabaseProfileId { get; }
    CrmHrActivityPresentation AccountActivityPresentation { get; }
    string? AccountActivityFailureMessage { get; }
    CrmAccountWorkspaceModel? SelectedAccount { get; }
    CrmAccountProfileEditorModel ProfileEditor { get; }
    List<CrmAccountConnectionEditorModel> ConnectedRecordEditors { get; }
    CrmInteractionEditorModel InteractionEditor { get; }
    CrmOpportunityEditorModel OpportunityEditor { get; }
    CrmOpportunityConversionEditorModel OpportunityConversionEditor { get; }
    int AccountCount { get; }
    Guid? SelectedOpportunityId { get; }
    int? ConnectionPartyPickerIndex { get; }
    string? Message { get; }
    long OpportunityPipelineRefreshVersion { get; }
    bool IsOpportunityBusy { get; }
    bool IsOpportunityCreateDialogOpen { get; }
    bool IsOpportunityDetailDialogOpen { get; }
    bool IsOpportunityEditDialogOpen { get; }
    bool IsOpportunityConversionDialogOpen { get; }
    bool IsAccountDialogOpen { get; }
    bool IsOpportunityConversionBusy { get; }
    int SelectedCrmRecordTabIndex { get; }
    IReadOnlyList<PartyOptionModel> ParticipantOptions { get; }
    CrmOpportunityDetailModel? SelectedOpportunity { get; }
    Guid? SelectedConnectionPartyId { get; }
    Task HandleCrmRecordTabChanged(int selectedIndex);
    Task RetryAccountActivityAsync();
    Task SelectAccountAsync(Guid accountPartyId);
    Task CloseAccountDialogAsync();
    void OpenDirectory();
    Task SaveAccountProfileAsync();
    void AddConnectedRecord();
    void RemoveConnectedRecord(int index);
    void OpenConnectionPartyPicker(int index);
    Task CloseConnectionPartyPickerAsync();
    Task ConfirmConnectionPartyAsync(Guid partyId);
    void ClearConnectionParty(int index);
    void UpdateConnectedRecordProjects(int index, IReadOnlyList<Guid> projectIds);
    string ResolveConnectionPartyLabel(Guid partyId);
    Task SaveConnectedRecordsAsync();
    Task SaveInteractionAsync();
    Task SelectOpportunityAsync(Guid opportunityId);
    Task SaveOpportunityAsync(CrmOpportunityEditorModel model);
    Task AdvanceOpportunityStageAsync(Guid opportunityId);
    void OpenOpportunityCreateDialog();
    Task CloseOpportunityCreateDialogAsync();
    Task CloseOpportunityDetailDialogAsync();
    void OpenOpportunityEditDialog();
    Task CloseOpportunityEditDialogAsync();
    Task OpenConversionDialogAsync();
    Task CloseOpportunityConversionDialogAsync();
    Task SaveOpportunityConversionAsync(CrmOpportunityConversionEditorModel model);
    Task HandleOpportunityPipelineLoadFailedAsync(Exception exception);
    Task HandleAccountBrowserLoadFailedAsync(Exception exception);
    Task HandleOpportunityPickerLoadFailedAsync(Exception exception);
    Task OpenLinkedProjectAsync();
    void ToggleInteractionParticipant(Guid partyId, ChangeEventArgs args);
    void OpenAgentChats();
}
