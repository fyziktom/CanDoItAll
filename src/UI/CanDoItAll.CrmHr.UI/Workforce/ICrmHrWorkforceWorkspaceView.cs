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

namespace CanDoItAll.CrmHr.UI.Workforce;

// What the Workforce workspace renders and what it can ask its host to do. The routed host implements it: it owns the
// state, the reads, the mutations, navigation and the agent context; the surface only binds to it.
public interface ICrmHrWorkforceWorkspaceView : ICrmHrWorkspaceView
{
    CrmHrActivityPresentation WorkforceHistoryPresentation { get; }
    string? WorkforceHistoryFailureMessage { get; }
    DeliveryUnitQuickCreateModel DeliveryUnitEditor { get; }
    SkillDefinitionEditorModel SkillDefinitionEditor { get; }
    WorkforceProfileWorkspaceModel? SelectedWorkspace { get; }
    WorkforceCapacityWorkspaceModel? CapacityWorkspace { get; }
    WorkforceProfileEditorModel ProfileEditor { get; }
    PartySkillEditorModel SkillEditor { get; }
    CapacityBlockEditorModel CapacityBlockEditor { get; }
    string CapacityWorkspaceError { get; }
    int SelectedDetailTabIndex { get; }
    int WorkforceBrowserVersion { get; }
    bool IsCapacityWorkspaceLoading { get; }
    bool IsRecordDialogOpen { get; }
    bool IsProfileCreationRequested { get; }
    bool IsDeliveryUnitDialogOpen { get; }
    bool IsDeliveryUnitSaving { get; }
    WorkforceAvailabilityState? SelectedAvailabilityState { get; }
    bool HasSelectedAvailabilityDetails { get; }
    decimal? SelectedAvailablePercent { get; }
    DateOnly? SelectedNextAvailabilityOn { get; }
    string SelectedAvailabilityMessage { get; }
    bool SelectedIsExternalContactWithoutProfile { get; }
    string SelectedClassificationLabel { get; }
    string SelectedClassificationTone { get; }
    string AllocationBadgeText { get; }
    IReadOnlyList<WorkforceKind> AvailableWorkforceKinds { get; }
    Task SelectPartyAsync(Guid partyId);
    Task CloseRecordDialogAsync();
    Task HandleWorkforceBrowserLoadFailedAsync(Exception exception);
    Task HandleHomeUnitChangedAsync(Guid? partyId);
    Task HandleManagerChangedAsync(Guid? partyId);
    Task HandleDetailTabChangedAsync(int index);
    Task RequestProfileCreation();
    Task RetryHistoryAsync();
    Task RetryCapacityWorkspaceAsync();
    void OpenDeliveryUnitDialog();
    Task CloseDeliveryUnitDialogAsync();
    void OpenDirectory();
    Task OpenProjectAsync(Guid projectId);
    Task SaveWorkforceProfileAsync();
    Task SaveSkillDefinitionAsync();
    Task SavePartySkillAsync();
    Task DeletePartySkillAsync(Guid partySkillId);
    Task SaveCapacityBlockAsync();
    Task DeleteCapacityBlockAsync(Guid capacityBlockId);
    Task CreateDeliveryUnitAsync();
}
