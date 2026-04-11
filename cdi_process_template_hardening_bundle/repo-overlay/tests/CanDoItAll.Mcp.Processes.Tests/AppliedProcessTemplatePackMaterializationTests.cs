using System.Text.Json;
using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Mcp.Processes.Tests;

public sealed class AppliedProcessTemplatePackMaterializationTests
{
    [Fact]
    public void Applied_pack_contains_manifest_toolbox_and_seed_catalog()
    {
        var root = ProcessTemplatePackLoader.FindPackRoot();

        Assert.True(File.Exists(Path.Combine(root, "manifest.json")));
        Assert.True(File.Exists(Path.Combine(root, "framework-sources.json")));
        Assert.True(File.Exists(Path.Combine(root, "toolbox", "role-templates.json")));
        Assert.True(File.Exists(Path.Combine(root, "toolbox", "step-templates.json")));
        Assert.True(File.Exists(Path.Combine(root, "toolbox", "chrome-actions.json")));
        Assert.True(File.Exists(Path.Combine(root, "seed-catalog", "baseline-scenarios.json")));
    }

    [Fact]
    public void Every_manifested_process_contains_required_core_sidecars()
    {
        var root = ProcessTemplatePackLoader.FindPackRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "manifest.json")));

        var processes = document.RootElement.GetProperty("Processes").EnumerateArray().ToList();
        Assert.Equal(9, processes.Count);

        foreach (var process in processes)
        {
            var relativePath = process.GetProperty("RelativePath").GetString();
            Assert.False(string.IsNullOrWhiteSpace(relativePath));

            var processRoot = Path.Combine(root, relativePath!);
            Assert.True(File.Exists(Path.Combine(processRoot, "definition.json")));
            Assert.True(File.Exists(Path.Combine(processRoot, "definition.md")));
            Assert.True(File.Exists(Path.Combine(processRoot, "mermaid", "flowchart.mmd")));
            Assert.True(File.Exists(Path.Combine(processRoot, "mermaid", "sequence.mmd")));
            Assert.True(File.Exists(Path.Combine(processRoot, "projection", "current-module.import-envelope.json")));
            Assert.True(File.Exists(Path.Combine(processRoot, "projection", "current-module.compatibility-report.json")));
            Assert.True(File.Exists(Path.Combine(processRoot, "projection", "current-module.compatibility-report.md")));
        }
    }
}
