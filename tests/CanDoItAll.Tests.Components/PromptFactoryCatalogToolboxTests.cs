using CanDoItAll.Components.CanvasLib;
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
        Assert.Contains(actions, action => action.ActionId == "catalog-components" && string.IsNullOrWhiteSpace(action.SubmenuLayout));
    }

    [Fact]
    public void Create_action_deduplicator_ignores_rapid_identical_requests()
    {
        var deduplicator = new PromptFactoryCreateActionDeduplicator(TimeSpan.FromMilliseconds(450));
        var request = new CanvasWorkbenchCreateActionRequest(
            "component:add:mission-scope",
            "session-root",
            0,
            0,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            "child",
            "create",
            string.Empty,
            null,
            []);

        var firstResult = deduplicator.ShouldProcess(request, DateTimeOffset.Parse("2026-03-26T10:00:00Z"));
        var duplicateResult = deduplicator.ShouldProcess(request, DateTimeOffset.Parse("2026-03-26T10:00:00.200Z"));
        var laterResult = deduplicator.ShouldProcess(request, DateTimeOffset.Parse("2026-03-26T10:00:01Z"));

        Assert.True(firstResult);
        Assert.False(duplicateResult);
        Assert.True(laterResult);
    }
}


