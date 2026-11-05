using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Modules.AgentFramework.Pages.Authoring;

public sealed record WorkflowCatalogRead(WorkflowCatalogSearchPage Page, WorkflowCatalogItem? Selected);

public sealed class WorkflowCatalogReader(IWorkflowCatalogSearchService search, IWorkflowCatalogLookupService lookup) {
    public async Task<WorkflowCatalogRead> ReadAsync(WorkflowCatalogSearchQuery query, WorkflowId? selected,
        CancellationToken cancellationToken) {
        var page = await search.SearchDefinitionsAsync(query, cancellationToken);
        if (page.PageIndex > 0 && page.Items.Count == 0) {
            page = await search.SearchDefinitionsAsync(new(query.Text, query.Status,
                Math.Max(0, page.TotalPages - 1), query.PageSize), cancellationToken);
        }
        var item = selected is { } id ? page.Items.FirstOrDefault(item => item.Id == id) : null;
        if (selected is { } selectedId && item is null) {
            item = (await lookup.LookupDefinitionsAsync(new([selectedId]), cancellationToken)).SingleOrDefault();
        }
        return new(page, item);
    }
}
