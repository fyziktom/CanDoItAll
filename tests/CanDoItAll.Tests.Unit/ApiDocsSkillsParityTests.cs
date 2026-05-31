using System.Runtime.CompilerServices;

namespace CanDoItAll.Tests.Unit;

public sealed class ApiDocsSkillsParityTests
{
    [Fact]
    public void Cognitive_memory_contract_and_operations_routes_are_documented_and_skilled()
    {
        var source = ReadRepositoryFile("src", "CanDoItAll.Web", "Api", "CognitiveMemoryApi.ContractEndpoints.cs") +
                     ReadRepositoryFile("src", "CanDoItAll.Web", "Api", "CognitiveMemoryApi.OperationsEndpoints.cs") +
                     ReadRepositoryFile("src", "CanDoItAll.Web", "Api", "CognitiveMemoryApi.DatabaseEndpoints.cs");
        var docs = ReadRepositoryFile("docs", "cognitive-memory", "operations", "api.md");
        var skill = ReadRepositoryFile("codex", "skills", "candoitall-api-cognitive-memory", "SKILL.md");

        Assert.Contains("38 routes per surface", docs, StringComparison.Ordinal);

        foreach (var route in new[]
                 {
                     "/contract",
                     "/database/transfer/sources/{targetProfileId}",
                     "/database/transfer/preview",
                     "/database/transfer",
                     "/projections/rebuild",
                     "/automation/run",
                     "/retention/cleanup"
                 })
        {
            Assert.Contains(route, source, StringComparison.Ordinal);
            Assert.Contains(route, docs, StringComparison.Ordinal);
            Assert.Contains(route, skill, StringComparison.Ordinal);
        }

        Assert.Contains("/api/cognitive-memory/v1/contract", docs, StringComparison.Ordinal);
        Assert.Contains("/api/cognitive-memory/v1", skill, StringComparison.Ordinal);
    }

    [Fact]
    public void Api_control_plane_lists_current_skills_and_runtime_tool_boundary()
    {
        var docs = ReadRepositoryFile("docs", "api-control-plane.md");

        Assert.Contains("candoitall-api-workflows", docs, StringComparison.Ordinal);
        Assert.Contains("candoitall-api-cognitive-memory", docs, StringComparison.Ordinal);
        Assert.Contains("agent-runtime-tool-surface.md", docs, StringComparison.Ordinal);
        Assert.Contains("provider-capability-and-pricing.md", docs, StringComparison.Ordinal);
    }

    [Fact]
    public void Api_skills_include_high_risk_route_and_dto_guidance()
    {
        var agents = ReadRepositoryFile("codex", "skills", "candoitall-api-agents", "SKILL.md");
        var workflows = ReadRepositoryFile("codex", "skills", "candoitall-api-workflows", "SKILL.md");
        var processes = ReadRepositoryFile("codex", "skills", "candoitall-api-processes", "SKILL.md");
        var projectStructure = ReadRepositoryFile("codex", "skills", "candoitall-api-project-structure", "SKILL.md");
        var cognitiveMemory = ReadRepositoryFile("codex", "skills", "candoitall-api-cognitive-memory", "SKILL.md");

        Assert.Contains("/api/agents/teams/{teamId}", agents, StringComparison.Ordinal);
        Assert.Contains("AgentExecutionRunApiQuery", agents, StringComparison.Ordinal);
        Assert.Contains("modelParameters.reasoningEffort", agents, StringComparison.Ordinal);

        Assert.Contains("/api/workflows/runs/{runId}/artifacts/{artifactId}/content", workflows, StringComparison.Ordinal);
        Assert.Contains("sourceProcessRunId", workflows, StringComparison.Ordinal);
        Assert.Contains("WorkflowEventListApiQuery", workflows, StringComparison.Ordinal);

        Assert.Contains("23 direct tools", processes, StringComparison.Ordinal);
        Assert.Contains("externalReferenceKey", processes, StringComparison.Ordinal);
        Assert.Contains("includeAttemptTimeline", processes, StringComparison.Ordinal);

        Assert.Contains("/nodes/{nodeId}/workflow/status", projectStructure, StringComparison.Ordinal);
        Assert.Contains("/leases/renew", projectStructure, StringComparison.Ordinal);
        Assert.Contains("28 direct tools", projectStructure, StringComparison.Ordinal);

        Assert.Contains("38 routes per surface", cognitiveMemory, StringComparison.Ordinal);
        Assert.Contains("CognitiveMemoryRetentionCleanupApiRequest", cognitiveMemory, StringComparison.Ordinal);
        Assert.Contains("dryRun", cognitiveMemory, StringComparison.Ordinal);
    }

    private static string ReadRepositoryFile(params string[] pathParts)
    {
        return File.ReadAllText(Path.Combine([FindRepositoryRoot(), .. pathParts]));
    }

    private static string FindRepositoryRoot([CallerFilePath] string sourceFilePath = "")
    {
        foreach (var startPath in new[]
                 {
                     AppContext.BaseDirectory,
                     Directory.GetCurrentDirectory(),
                     Path.GetDirectoryName(sourceFilePath) ?? string.Empty
                 })
        {
            var directory = new DirectoryInfo(startPath);
            while (directory is not null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
