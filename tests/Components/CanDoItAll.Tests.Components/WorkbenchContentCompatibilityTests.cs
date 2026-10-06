using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed class WorkbenchContentCompatibilityTests {
    [Fact]
    public void Original_assembly_names_resolve_the_actual_content_renderers() {
        var native = typeof(ProjectWorkbenchService).Assembly;
        foreach (var renderer in new[] { typeof(WorkbenchMermaidFileView), typeof(WorkbenchMarkdownMermaidBlock) }) {
            Assert.Equal("CanDoItAll.Workbench.Content.UI", renderer.Assembly.GetName().Name);
            Assert.Contains(renderer, native.GetForwardedTypes());
            Assert.Same(renderer, Type.GetType($"{renderer.FullName}, {native.GetName().Name}", throwOnError: true));
        }
    }

    [Fact]
    public void Original_native_media_and_status_callers_keep_their_signatures() {
        var owner = typeof(ProjectWorkbenchService);
        Assert.NotNull(owner.GetMethod(nameof(ProjectWorkbenchService.ReplaceObjectMediaAsync),
            [typeof(Guid), typeof(string), typeof(ProjectObjectMediaPayload), typeof(string), typeof(string), typeof(string), typeof(CancellationToken)]));
        Assert.NotNull(owner.GetMethod(nameof(ProjectWorkbenchService.UpdateObjectStatusesDetailedAsync),
            [typeof(Guid), typeof(IReadOnlyCollection<string>), typeof(string), typeof(CancellationToken),
                typeof(CanDoItAll.Modules.Projects.ProjectWriteAdmission), typeof(ProjectProcessMutationAdmission), typeof(ProjectAgentMutationAdmission)]));
    }
}
