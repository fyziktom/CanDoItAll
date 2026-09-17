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
using CanDoItAll.CrmHr.UI.Agents;

namespace CanDoItAll.Modules.CrmHr.Pages;

// The page is the Agents workspace's view: the surface binds to these members and to nothing else of the host.
public partial class CrmHrAgentsPage : ICrmHrAgentsWorkspaceView
{
    void ICrmHrWorkspaceView.RequestRender() => _ = InvokeAsync(StateHasChanged);

    AiAgentDirectoryPage ICrmHrAgentsWorkspaceView.AgentPage => agentPage;
    AiAgentDirectoryItemModel? ICrmHrAgentsWorkspaceView.SelectedDirectoryItem => selectedDirectoryItem;
    AiAgentWorkspaceModel? ICrmHrAgentsWorkspaceView.SelectedWorkspace => selectedWorkspace;
    bool ICrmHrAgentsWorkspaceView.IsWorkspaceLoading => isWorkspaceLoading;
    bool ICrmHrAgentsWorkspaceView.IsRecordDialogOpen => isRecordDialogOpen;
    PagedRecordSelection<Guid>? ICrmHrAgentsWorkspaceView.AgentSelection => AgentSelection;
    bool ICrmHrAgentsWorkspaceView.ShouldShowListLoadingState => ShouldShowListLoadingState;
    Task<PagedRecordPage<Guid>> ICrmHrAgentsWorkspaceView.LoadAgentPageAsync(PagedRecordRequest<AiAgentValidationFilter> request, CancellationToken cancellationToken) => LoadAgentPageAsync(request, cancellationToken);
    void ICrmHrAgentsWorkspaceView.HandleAgentDirectoryLoadFailed(Exception _) => HandleAgentDirectoryLoadFailed(_);
    AiAgentDirectoryItemModel ICrmHrAgentsWorkspaceView.GetCurrentPageItem(Guid partyId) => GetCurrentPageItem(partyId);
    Task ICrmHrAgentsWorkspaceView.SelectPartyAsync(Guid partyId) => SelectPartyAsync(partyId);
    Task ICrmHrAgentsWorkspaceView.CloseRecordDialogAsync() => CloseRecordDialogAsync();
    void ICrmHrAgentsWorkspaceView.OpenTechnicalCatalog() => OpenTechnicalCatalog();
    void ICrmHrAgentsWorkspaceView.OpenTechnicalRecord() => OpenTechnicalRecord();
    void ICrmHrAgentsWorkspaceView.OpenDirectoryRecord() => OpenDirectoryRecord();
}
