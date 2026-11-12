using System.Reflection;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.Workbench.Insights.UI;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkbenchInsightsBoundaryTests {
    [Fact]
    public void Native_public_identities_resolve_to_the_extracted_report_and_support_assemblies() {
        var native = typeof(ProjectManagerSummaryQueryService).Assembly;
        var contracts = typeof(ProjectManagerSummaryOptions).Assembly;
        var renderers = typeof(InsightsOrigin).Assembly;
        var forwarded = native.GetForwardedTypes().Where(type => type.Assembly == contracts || type.Assembly == renderers).ToArray();
        Assert.Equal(40, forwarded.Length);
        foreach (var type in forwarded) {
            Assert.Same(type, Type.GetType($"{type.FullName}, {native.GetName().Name}", throwOnError: true));
        }
        Assert.Same(renderers, typeof(ProjectStructureSelectionPanel).Assembly);
        Assert.Same(renderers, typeof(ProjectStructureNodeDetailPreview).Assembly);
        Assert.Same(native, typeof(ProjectManagerSummarySnapshot).Assembly);
    }

    [Fact]
    public void Insights_public_contract_graph_contains_only_feature_values_framework_and_shared_components() {
        var roots = new[] { typeof(InsightsOrigin).Assembly, typeof(ProjectManagerSummaryOptions).Assembly };
        var pending = new Queue<Assembly>(roots);
        var visited = new HashSet<Assembly>();
        while (pending.TryDequeue(out var assembly)) {
            if (!visited.Add(assembly)) {
                continue;
            }
            foreach (var reference in assembly.GetReferencedAssemblies()) {
                if (reference.Name?.StartsWith("CanDoItAll.", StringComparison.Ordinal) != true) {
                    continue;
                }
                var dependency = Assembly.Load(reference);
                Assert.True(roots.Contains(dependency) || reference.Name.StartsWith("CanDoItAll.Components.", StringComparison.Ordinal),
                    $"Unexpected product dependency: {assembly.GetName().Name} -> {reference.Name}");
                pending.Enqueue(dependency);
            }
        }
    }
}
