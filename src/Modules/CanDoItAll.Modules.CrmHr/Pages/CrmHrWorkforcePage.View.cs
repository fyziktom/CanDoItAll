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
using CanDoItAll.CrmHr.UI;
using CanDoItAll.CrmHr.UI.Workforce;

namespace CanDoItAll.Modules.CrmHr.Pages;

// The page is the Workforce workspace's view: the surface binds to these members and to nothing else of the host.
public partial class CrmHrWorkforcePage : ICrmHrWorkforceWorkspaceView
{
    void ICrmHrWorkspaceView.RequestRender() => _ = InvokeAsync(StateHasChanged);

    CrmHrActivityPresentation ICrmHrWorkforceWorkspaceView.WorkforceHistoryPresentation => WorkforceHistoryPresentation;
    string? ICrmHrWorkforceWorkspaceView.WorkforceHistoryFailureMessage => WorkforceHistoryFailureMessage;
    DeliveryUnitQuickCreateModel ICrmHrWorkforceWorkspaceView.DeliveryUnitEditor => deliveryUnitEditor;
    SkillDefinitionEditorModel ICrmHrWorkforceWorkspaceView.SkillDefinitionEditor => skillDefinitionEditor;
    WorkforceProfileWorkspaceModel? ICrmHrWorkforceWorkspaceView.SelectedWorkspace => selectedWorkspace;
    WorkforceCapacityWorkspaceModel? ICrmHrWorkforceWorkspaceView.CapacityWorkspace => capacityWorkspace;
    WorkforceProfileEditorModel ICrmHrWorkforceWorkspaceView.ProfileEditor => profileEditor;
    PartySkillEditorModel ICrmHrWorkforceWorkspaceView.SkillEditor => skillEditor;
    CapacityBlockEditorModel ICrmHrWorkforceWorkspaceView.CapacityBlockEditor => capacityBlockEditor;
    string ICrmHrWorkforceWorkspaceView.CapacityWorkspaceError => capacityWorkspaceError;
    int ICrmHrWorkforceWorkspaceView.SelectedDetailTabIndex => selectedDetailTabIndex;
    int ICrmHrWorkforceWorkspaceView.WorkforceBrowserVersion => workforceBrowserVersion;
    bool ICrmHrWorkforceWorkspaceView.IsCapacityWorkspaceLoading => isCapacityWorkspaceLoading;
    bool ICrmHrWorkforceWorkspaceView.IsRecordDialogOpen => isRecordDialogOpen;
    bool ICrmHrWorkforceWorkspaceView.IsProfileCreationRequested => isProfileCreationRequested;
    bool ICrmHrWorkforceWorkspaceView.IsDeliveryUnitDialogOpen => isDeliveryUnitDialogOpen;
    bool ICrmHrWorkforceWorkspaceView.IsDeliveryUnitSaving => isDeliveryUnitSaving;
    WorkforceAvailabilityState? ICrmHrWorkforceWorkspaceView.SelectedAvailabilityState => SelectedAvailabilityState;
    bool ICrmHrWorkforceWorkspaceView.HasSelectedAvailabilityDetails => HasSelectedAvailabilityDetails;
    decimal? ICrmHrWorkforceWorkspaceView.SelectedAvailablePercent => SelectedAvailablePercent;
    DateOnly? ICrmHrWorkforceWorkspaceView.SelectedNextAvailabilityOn => SelectedNextAvailabilityOn;
    string ICrmHrWorkforceWorkspaceView.SelectedAvailabilityMessage => SelectedAvailabilityMessage;
    bool ICrmHrWorkforceWorkspaceView.SelectedIsExternalContactWithoutProfile => SelectedIsExternalContactWithoutProfile;
    string ICrmHrWorkforceWorkspaceView.SelectedClassificationLabel => SelectedClassificationLabel;
    string ICrmHrWorkforceWorkspaceView.SelectedClassificationTone => SelectedClassificationTone;
    string ICrmHrWorkforceWorkspaceView.AllocationBadgeText => AllocationBadgeText;
    IReadOnlyList<WorkforceKind> ICrmHrWorkforceWorkspaceView.AvailableWorkforceKinds => AvailableWorkforceKinds;
    Task ICrmHrWorkforceWorkspaceView.SelectPartyAsync(Guid partyId) => SelectPartyAsync(partyId);
    Task ICrmHrWorkforceWorkspaceView.CloseRecordDialogAsync() => CloseRecordDialogAsync();
    Task ICrmHrWorkforceWorkspaceView.HandleWorkforceBrowserLoadFailedAsync(Exception exception) => HandleWorkforceBrowserLoadFailedAsync(exception);
    Task ICrmHrWorkforceWorkspaceView.HandleHomeUnitChangedAsync(Guid? partyId) => HandleHomeUnitChangedAsync(partyId);
    Task ICrmHrWorkforceWorkspaceView.HandleManagerChangedAsync(Guid? partyId) => HandleManagerChangedAsync(partyId);
    Task ICrmHrWorkforceWorkspaceView.HandleDetailTabChangedAsync(int index) => HandleDetailTabChangedAsync(index);
    Task ICrmHrWorkforceWorkspaceView.RequestProfileCreation() => RequestProfileCreation();
    Task ICrmHrWorkforceWorkspaceView.RetryHistoryAsync() => RetryHistoryAsync();
    Task ICrmHrWorkforceWorkspaceView.RetryCapacityWorkspaceAsync() => RetryCapacityWorkspaceAsync();
    void ICrmHrWorkforceWorkspaceView.OpenDeliveryUnitDialog() => OpenDeliveryUnitDialog();
    Task ICrmHrWorkforceWorkspaceView.CloseDeliveryUnitDialogAsync() => CloseDeliveryUnitDialogAsync();
    void ICrmHrWorkforceWorkspaceView.OpenDirectory() => OpenDirectory();
    Task ICrmHrWorkforceWorkspaceView.OpenProjectAsync(Guid projectId) => OpenProjectAsync(projectId);
    Task ICrmHrWorkforceWorkspaceView.SaveWorkforceProfileAsync() => SaveWorkforceProfileAsync();
    Task ICrmHrWorkforceWorkspaceView.SaveSkillDefinitionAsync() => SaveSkillDefinitionAsync();
    Task ICrmHrWorkforceWorkspaceView.SavePartySkillAsync() => SavePartySkillAsync();
    Task ICrmHrWorkforceWorkspaceView.DeletePartySkillAsync(Guid partySkillId) => DeletePartySkillAsync(partySkillId);
    Task ICrmHrWorkforceWorkspaceView.SaveCapacityBlockAsync() => SaveCapacityBlockAsync();
    Task ICrmHrWorkforceWorkspaceView.DeleteCapacityBlockAsync(Guid capacityBlockId) => DeleteCapacityBlockAsync(capacityBlockId);
    Task ICrmHrWorkforceWorkspaceView.CreateDeliveryUnitAsync() => CreateDeliveryUnitAsync();
}
