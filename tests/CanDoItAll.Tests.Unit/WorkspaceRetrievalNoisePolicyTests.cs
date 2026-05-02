using CanDoItAll.AgentFramework.Core;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkspaceRetrievalNoisePolicyTests
{
    [Fact]
    public void BuildSeedWorkspaceRagExcludedPaths_includes_the_data_root()
    {
        var excludedPaths = WorkspaceRetrievalNoisePolicy.BuildSeedWorkspaceRagExcludedPaths();

        Assert.Contains("data", excludedPaths, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void ShouldExcludeFromAmbientRetrieval_matches_nested_generated_runtime_segments()
    {
        var workspaceRoot = Path.Combine("C:", "repo", "workspace");
        var runtimeNoisePath = Path.Combine(workspaceRoot, "deliveries", "workflow-suite", ".playwright-mcp", "qa-validation", "page.yml");

        var shouldExclude = WorkspaceRetrievalNoisePolicy.ShouldExcludeFromAmbientRetrieval(workspaceRoot, runtimeNoisePath);

        Assert.True(shouldExclude);
    }
}
