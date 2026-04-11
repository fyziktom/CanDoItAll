using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Mcp.Processes.Tests;

public sealed class ProcessTemplatePackLoaderTests
{
    [Fact]
    public void Load_returns_current_architecture_pack_shape()
    {
        var loader = new ProcessTemplatePackLoader();
        var pack = loader.Load();

        Assert.Equal("candoitall-software-process-template-pack", pack.Manifest.PackKey);
        Assert.Equal(9, pack.Processes.Count);
        Assert.Equal(5, pack.BaselineScenarios.Count);
        Assert.True(pack.SharedRoles.ContainsKey("review-lead"));
        Assert.True(pack.Processes.ContainsKey("branching-code-review"));
        Assert.True(pack.Processes.ContainsKey("ai-assisted-change-delivery"));
        Assert.NotEmpty(pack.ChromeActions.DefinitionQuickCreateActions);
    }

    [Fact]
    public void FindPackRoot_resolves_manifest_from_build_output()
    {
        var root = ProcessTemplatePackLoader.FindPackRoot();

        Assert.True(File.Exists(Path.Combine(root, "manifest.json")));
        Assert.Contains(Path.Combine("output", "process-template-pack"), root, StringComparison.OrdinalIgnoreCase);
    }
}
