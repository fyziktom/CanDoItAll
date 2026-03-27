using CanDoItAll.Components.CanvasLib;

namespace CanDoItAll.Modules.Factory.CanvasAdapters;

public static class PromptFactoryCatalogToolbox
{
    public static IReadOnlyList<CanvasWorkbenchAction> BuildSessionContextActions(PromptLibraryCatalogSummary catalog)
        => PromptFactoryCanvasCatalog.BuildSessionContextActions(catalog);

    public static IReadOnlyList<CanvasWorkbenchAction> BuildSelectionContextActions(PromptLibraryCatalogSummary catalog, string selectionKind)
        => PromptFactoryCanvasCatalog.BuildSelectionContextActions(catalog, selectionKind);

    public static IReadOnlyList<CanvasWorkbenchAction> BuildComponentNodeActions(PromptBlockSummary block)
        => PromptFactoryCanvasCatalog.BuildComponentNodeActions(block);

    public static IReadOnlyList<CanvasWorkbenchAction> BuildFlowNodeActions(PromptLibraryCatalogSummary catalog)
        => PromptFactoryCanvasCatalog.BuildFlowNodeActions(catalog);

    public static IReadOnlyList<CanvasWorkbenchAction> BuildBlueprintNodeActions(PromptLibraryCatalogSummary catalog)
        => PromptFactoryCanvasCatalog.BuildBlueprintNodeActions(catalog);

    public static IReadOnlyList<CanvasWorkbenchAction> BuildInputNodeActions(string attachmentId)
        => PromptFactoryCanvasCatalog.BuildInputNodeActions(attachmentId);
}


