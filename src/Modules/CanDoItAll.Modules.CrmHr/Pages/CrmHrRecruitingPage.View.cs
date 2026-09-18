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
using CanDoItAll.CrmHr.UI.Recruiting;

namespace CanDoItAll.Modules.CrmHr.Pages;

// The page is the Recruiting workspace's view: the surface binds to these members and to nothing else of the host.
public partial class CrmHrRecruitingPage : ICrmHrRecruitingWorkspaceView
{
    void ICrmHrWorkspaceView.RequestRender() => _ = InvokeAsync(StateHasChanged);

    RecruitmentWorkspaceModel? ICrmHrRecruitingWorkspaceView.CurrentWorkspace => currentWorkspace;
    RecruitmentApplicationEditorModel ICrmHrRecruitingWorkspaceView.ApplicationEditor => applicationEditor;
    RecruitmentInterviewEditorModel ICrmHrRecruitingWorkspaceView.InterviewEditor => interviewEditor;
    LifecycleTaskEditorModel ICrmHrRecruitingWorkspaceView.TaskEditor => taskEditor;
    RecruitmentSupportAssignmentsEditorModel ICrmHrRecruitingWorkspaceView.SupportEditor => supportEditor;
    RecruitmentConversionEditorModel ICrmHrRecruitingWorkspaceView.ConversionEditor => conversionEditor;
    RecruitmentApplicationSummary ICrmHrRecruitingWorkspaceView.ApplicationSummary => applicationSummary;
    int ICrmHrRecruitingWorkspaceView.ApplicationBrowserRevision => applicationBrowserRevision;
    bool ICrmHrRecruitingWorkspaceView.IsRecruitmentDialogOpen => isRecruitmentDialogOpen;
    Guid? ICrmHrRecruitingWorkspaceView.SelectedPartyId => SelectedPartyId;
    Error? ICrmHrRecruitingWorkspaceView.ConversionEligibilityError => ConversionEligibilityError;
    PagedRecordSelection<Guid>? ICrmHrRecruitingWorkspaceView.ApplicationSelection => ApplicationSelection;
    int ICrmHrRecruitingWorkspaceView.SelectedRecruitingTabIndex => SelectedRecruitingTabIndex;
    string ICrmHrRecruitingWorkspaceView.RecruitmentDialogTitle => RecruitmentDialogTitle;
    Task<PagedRecordPage<Guid>> ICrmHrRecruitingWorkspaceView.LoadApplicationPageAsync(PagedRecordRequest<RecruitmentApplicationScope> request, CancellationToken cancellationToken) => LoadApplicationPageAsync(request, cancellationToken);
    Task ICrmHrRecruitingWorkspaceView.SelectApplicationAsync(Guid applicationId) => SelectApplicationAsync(applicationId);
    Task ICrmHrRecruitingWorkspaceView.HandleApplicationPageLoadFailed(Exception exception) => HandleApplicationPageLoadFailed(exception);
    Task ICrmHrRecruitingWorkspaceView.CreateNewApplicationAsync() => CreateNewApplicationAsync();
    Task ICrmHrRecruitingWorkspaceView.CloseRecruitmentDialogAsync() => CloseRecruitmentDialogAsync();
    Task ICrmHrRecruitingWorkspaceView.HandleRecruitingTabChangedAsync(int selectedIndex) => HandleRecruitingTabChangedAsync(selectedIndex);
    Task ICrmHrRecruitingWorkspaceView.SaveApplicationAsync() => SaveApplicationAsync();
    Task ICrmHrRecruitingWorkspaceView.ResetApplicationAsync() => ResetApplicationAsync();
    Task ICrmHrRecruitingWorkspaceView.SaveInterviewAsync() => SaveInterviewAsync();
    Task ICrmHrRecruitingWorkspaceView.ResetInterviewAsync() => ResetInterviewAsync();
    Task ICrmHrRecruitingWorkspaceView.DeleteInterviewAsync(Guid interviewId) => DeleteInterviewAsync(interviewId);
    Task ICrmHrRecruitingWorkspaceView.SaveTaskAsync() => SaveTaskAsync();
    Task ICrmHrRecruitingWorkspaceView.ResetTaskAsync() => ResetTaskAsync();
    Task ICrmHrRecruitingWorkspaceView.DeleteTaskAsync(Guid taskId) => DeleteTaskAsync(taskId);
    Task ICrmHrRecruitingWorkspaceView.SaveSupportAssignmentsAsync() => SaveSupportAssignmentsAsync();
    Task ICrmHrRecruitingWorkspaceView.ConvertCandidateAsync() => ConvertCandidateAsync();
    Task ICrmHrRecruitingWorkspaceView.SetConversionHomeUnitAsync(Guid? partyId) => SetConversionHomeUnitAsync(partyId);
    Task ICrmHrRecruitingWorkspaceView.SetConversionManagerAsync(Guid? partyId) => SetConversionManagerAsync(partyId);
    Task ICrmHrRecruitingWorkspaceView.OpenWorkforceAsync() => OpenWorkforceAsync();
}
