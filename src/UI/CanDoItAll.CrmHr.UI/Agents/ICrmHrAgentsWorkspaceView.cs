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

namespace CanDoItAll.CrmHr.UI.Agents;

// What the Agents workspace renders and what it can ask its host to do. The routed host implements it: it owns the
// state, the reads, the mutations, navigation and the agent context; the surface only binds to it.
public interface ICrmHrAgentsWorkspaceView : ICrmHrWorkspaceView
{
    AiAgentDirectoryPage AgentPage { get; }
    AiAgentDirectoryItemModel? SelectedDirectoryItem { get; }
    AiAgentWorkspaceModel? SelectedWorkspace { get; }
    bool IsWorkspaceLoading { get; }
    bool IsRecordDialogOpen { get; }
    PagedRecordSelection<Guid>? AgentSelection { get; }
    bool ShouldShowListLoadingState { get; }
    Task<PagedRecordPage<Guid>> LoadAgentPageAsync(PagedRecordRequest<AiAgentValidationFilter> request, CancellationToken cancellationToken);
    void HandleAgentDirectoryLoadFailed(Exception _);
    AiAgentDirectoryItemModel GetCurrentPageItem(Guid partyId);
    Task SelectPartyAsync(Guid partyId);
    Task CloseRecordDialogAsync();
    void OpenTechnicalCatalog();
    void OpenTechnicalRecord();
    void OpenDirectoryRecord();
}
