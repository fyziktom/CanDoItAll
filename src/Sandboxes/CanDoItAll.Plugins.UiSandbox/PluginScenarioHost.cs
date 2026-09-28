using CanDoItAll.Modules.Plugins.Presentation;
using CanDoItAll.Plugins.UI;

namespace CanDoItAll.Plugins.UiSandbox;

public sealed class PluginScenarioHost : IDisposable {
    public PluginScenarioStore Store { get; private set; } = new();
    public PluginsWorkspace Workspace { get; private set; }

    public PluginScenarioHost() => Workspace = CreateWorkspace();

    public async Task ChangeAsync(PluginScenario scenario) {
        Workspace.Dispose();
        Store.ReleaseAll();
        Store = new(scenario);
        Workspace = CreateWorkspace();
        await Workspace.RefreshAsync();
        if (scenario == PluginScenario.InvalidFields) {
            var editor = Workspace.View.Editors.Values.First();
            editor.SetField("clientId", "unfinished-");
            Workspace.SelectSection(PluginSection.Settings);
        }
    }

    private PluginsWorkspace CreateWorkspace() => new(Store, "http://sandbox.invalid/api/plugins/oauth/callback");

    public void Dispose() {
        Workspace.Dispose();
        Store.ReleaseAll();
    }
}
