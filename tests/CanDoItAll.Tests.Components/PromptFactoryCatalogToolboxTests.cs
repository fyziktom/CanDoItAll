using CanDoItAll.Modules.Factory;
using CanDoItAll.Modules.Factory.CanvasAdapters;

namespace CanDoItAll.Tests.Components;

public sealed class PromptFactoryCatalogToolboxTests
{
    [Fact]
    public void Session_context_actions_expose_catalog_roots_and_save_flow()
    {
        var catalog = new PromptLibraryCatalogSummary([], [], [], 0, 0, 0);

        var actions = PromptFactoryCatalogToolbox.BuildSessionContextActions(catalog);

        Assert.Contains(actions, action => action.ActionId == "catalog-components");
        Assert.Contains(actions, action => action.ActionId == "catalog-blueprints");
        Assert.Contains(actions, action => action.ActionId == "catalog-flows");
        Assert.Contains(actions, action => action.ActionId == "catalog-inputs");
        Assert.Contains(actions, action => action.ActionId == "apply-recommendations");
        Assert.Contains(actions, action => action.ActionId == "build-flow");
        Assert.Contains(actions, action => action.ActionId == "save-session");
    }
}


