using System.Text.Json;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed class ProjectsOperationPerformanceProbeTests {
    [Fact]
    public void Explain_summary_preserves_fractional_actual_rows_and_only_approved_plan_fields() {
        using var plan = JsonDocument.Parse("""
            {
              "Planning Time": 0.125,
              "Execution Time": 0.75,
              "Plan": {
                "Node Type": "Nested Loop",
                "Plan Rows": 1,
                "Actual Rows": 1.00,
                "Actual Loops": 1,
                "Actual Total Time": 0.5,
                "Shared Hit Blocks": 4,
                "Shared Read Blocks": 0,
                "Filter": "private predicate",
                "Plans": [{
                  "Node Type": "Index Scan",
                  "Index Name": "synthetic_index",
                  "Plan Rows": 1,
                  "Actual Rows": 0.50,
                  "Actual Loops": 2,
                  "Actual Total Time": 0.125,
                  "Shared Hit Blocks": 2,
                  "Shared Read Blocks": 0,
                  "Index Cond": "private predicate"
                }]
              }
            }
            """);

        var summary = JsonSerializer.SerializeToElement(ProjectsOperationStorageMeasurement.SummarizeChildReadPlan(plan.RootElement));
        var nodes = summary.GetProperty("Nodes").EnumerateArray().ToArray();
        Assert.Equal(2, nodes.Length);
        Assert.Equal(1, nodes[0].GetProperty("ActualRows").GetDouble());
        Assert.Equal(0.5, nodes[1].GetProperty("ActualRows").GetDouble());
        Assert.Equal(2, nodes[1].GetProperty("ActualLoops").GetInt64());
        Assert.Equal(0.125, summary.GetProperty("PlanningMilliseconds").GetDouble());
        Assert.Equal(0.75, summary.GetProperty("ExecutionMilliseconds").GetDouble());
        Assert.All(nodes, node => Assert.Equal(new[] { "Node", "Index", "EstimatedRows", "ActualRows", "ActualLoops",
            "ActualTotalMilliseconds", "SharedHitBlocks", "SharedReadBlocks" }, node.EnumerateObject().Select(property => property.Name)));
        Assert.DoesNotContain("private predicate", summary.GetRawText(), StringComparison.Ordinal);
    }
}
