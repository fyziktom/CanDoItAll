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
    // The section of the open agent record.
    CrmHrAgentRecordSection RecordSection { get; }
    PagedRecordSelection<Guid>? AgentSelection { get; }
    // The agent catalog's committed query when the host keeps it; null lets the catalog keep its own.
    PagedRecordQueryState<AiAgentValidationFilter>? CatalogQuery { get; }
    bool ShouldShowListLoadingState { get; }
    Task<PagedRecordPage<Guid>> LoadAgentPageAsync(PagedRecordRequest<AiAgentValidationFilter> request, CancellationToken cancellationToken);
    void HandleAgentDirectoryLoadFailed(Exception _);
    AiAgentDirectoryItemModel GetCurrentPageItem(Guid partyId);
    Task SelectPartyAsync(Guid partyId);
    // A committed catalog change: a debounced search, a validation filter, reset, a page, or a corrected page.
    Task HandleCatalogQueryChangedAsync(PagedRecordQueryChange<AiAgentValidationFilter> change);
    Task SelectRecordSectionAsync(CrmHrAgentRecordSection section);
    Task CloseRecordDialogAsync();
    void OpenTechnicalCatalog();
    void OpenTechnicalRecord();
    void OpenDirectoryRecord();
}
