using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Mcp.Processes.Tests;

public sealed class TemplatePackSharedLocalResourceParityTests
{
    [Fact]
    public void Shared_and_local_resources_are_materialized_on_disk()
    {
        var loader = new ProcessTemplatePackLoader();
        var pack = loader.Load();

        Assert.True(pack.SharedRoles.ContainsKey("product-owner"));
        Assert.True(pack.SharedArtifacts.ContainsKey("scope-boundary-packet"));
        Assert.True(pack.SharedChecklists.ContainsKey("intake-completeness-checklist"));
        Assert.True(pack.SharedValidations.ContainsKey("validation-intake-complete"));
        Assert.True(pack.SharedPrompts.ContainsKey("prompt-intake-summarizer"));

        Assert.True(File.Exists(Path.Combine(pack.RootPath, "shared", "roles", "product-owner.md")));
        Assert.True(File.Exists(Path.Combine(pack.RootPath, "shared", "artifacts", "scope-boundary-packet.md")));

        var software = pack.Processes["software-delivery"];
        Assert.Contains(software.LocalRoles, item => item.Key == "lead-engineer");
        Assert.Contains(software.LocalArtifacts, item => item.Key == "migration-rehearsal-pack");
        Assert.True(File.Exists(Path.Combine(pack.RootPath, "processes", "software-delivery", "roles", "lead-engineer.json")));
        Assert.True(File.Exists(Path.Combine(pack.RootPath, "processes", "software-delivery", "roles", "lead-engineer.md")));
        Assert.True(File.Exists(Path.Combine(pack.RootPath, "processes", "software-delivery", "artifacts", "migration-rehearsal-pack.json")));
        Assert.True(File.Exists(Path.Combine(pack.RootPath, "processes", "software-delivery", "artifacts", "migration-rehearsal-pack.md")));
    }

    [Fact]
    public void Every_step_docref_points_to_an_existing_markdown_sidecar()
    {
        var loader = new ProcessTemplatePackLoader();
        var pack = loader.Load();

        foreach (var process in pack.Processes.Values)
        {
            foreach (var step in process.Steps)
            {
                foreach (var docRef in step.DocRefs)
                {
                    var path = Path.Combine(pack.RootPath, docRef.Replace('/', Path.DirectorySeparatorChar));
                    Assert.True(File.Exists(path), $"Missing step sidecar: {docRef}");
                }
            }
        }
    }
}
