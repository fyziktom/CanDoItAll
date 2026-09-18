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

namespace CanDoItAll.CrmHr.UI.Recruiting;

// What the Recruiting workspace renders and what it can ask its host to do. The routed host implements it: it owns the
// state, the reads, the mutations, navigation and the agent context; the surface only binds to it.
public interface ICrmHrRecruitingWorkspaceView : ICrmHrWorkspaceView
{
    RecruitmentWorkspaceModel? CurrentWorkspace { get; }
    RecruitmentApplicationEditorModel ApplicationEditor { get; }
    RecruitmentInterviewEditorModel InterviewEditor { get; }
    LifecycleTaskEditorModel TaskEditor { get; }
    RecruitmentSupportAssignmentsEditorModel SupportEditor { get; }
    RecruitmentConversionEditorModel ConversionEditor { get; }
    RecruitmentApplicationSummary ApplicationSummary { get; }
    int ApplicationBrowserRevision { get; }
    bool IsRecruitmentDialogOpen { get; }
    Guid? SelectedPartyId { get; }
    Error? ConversionEligibilityError { get; }
    PagedRecordSelection<Guid>? ApplicationSelection { get; }
    int SelectedRecruitingTabIndex { get; }
    string RecruitmentDialogTitle { get; }
    Task<PagedRecordPage<Guid>> LoadApplicationPageAsync(PagedRecordRequest<RecruitmentApplicationScope> request, CancellationToken cancellationToken);
    Task SelectApplicationAsync(Guid applicationId);
    Task HandleApplicationPageLoadFailed(Exception exception);
    Task CreateNewApplicationAsync();
    Task CloseRecruitmentDialogAsync();
    Task HandleRecruitingTabChangedAsync(int selectedIndex);
    Task SaveApplicationAsync();
    Task ResetApplicationAsync();
    Task SaveInterviewAsync();
    Task ResetInterviewAsync();
    Task DeleteInterviewAsync(Guid interviewId);
    Task SaveTaskAsync();
    Task ResetTaskAsync();
    Task DeleteTaskAsync(Guid taskId);
    Task SaveSupportAssignmentsAsync();
    Task ConvertCandidateAsync();
    Task SetConversionHomeUnitAsync(Guid? partyId);
    Task SetConversionManagerAsync(Guid? partyId);
    Task OpenWorkforceAsync();
}
