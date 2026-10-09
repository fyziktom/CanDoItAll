using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Processes.UI;

public sealed class ProcessTemplateBrowserState {
    private ProcessWorkspaceShellScope? scope;
    private ProcessDefinitionCatalogItemKey? definition;
    private string? acceptedSearch;
    public string PendingSearchText { get; set; } = string.Empty;
    public ProcessDefinitionStepKey? SelectedTargetStepKey { get; set; }

    public void Observe(ProcessWorkspaceShellScope currentScope, ProcessTemplateCatalogProjection catalog) {
        if (scope != currentScope || definition != catalog.TargetDefinitionKey) {
            scope = currentScope;
            definition = catalog.TargetDefinitionKey;
            PendingSearchText = catalog.Query.SearchText ?? string.Empty;
            SelectedTargetStepKey = catalog.ImportTargets.FirstOrDefault(target => target.IsDefaultTarget)?.StepKey
                ?? catalog.ImportTargets.FirstOrDefault()?.StepKey;
        } else if (acceptedSearch != catalog.Query.SearchText) {
            PendingSearchText = catalog.Query.SearchText ?? string.Empty;
        }
        acceptedSearch = catalog.Query.SearchText;
    }
}
