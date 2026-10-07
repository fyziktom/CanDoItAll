using System.Reflection;
using CanDoItAll.Projects.Files.UI;
using CanDoItAll.Projects.Files.UiSandbox;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components.ProjectsFilesUi;

public sealed class FilesBoundaryTests {
    [Fact]
    public void Actual_renderer_and_sandbox_closures_exclude_module_and_runtime_owners() {
        foreach (var root in new[] { typeof(ProjectFilesDialogView).Assembly, typeof(FilesScenarioSurface).Assembly }) {
            Visit(root.GetName().Name!, name => Assembly.Load(name).GetReferencedAssemblies().Select(value => value.Name!));
        }
    }

    [Fact]
    public void Transitive_forbidden_and_missing_edges_are_rejected() {
        Assert.Throws<InvalidOperationException>(() => Visit("CanDoItAll.Projects.Files.UI", name => name == "CanDoItAll.Projects.Files.UI" ? ["CanDoItAll.Components.BaseLib"] : ["CanDoItAll.Infrastructure"]));
        Assert.Throws<InvalidOperationException>(() => Visit("CanDoItAll.Projects.Files.UI", _ => ["Microsoft.EntityFrameworkCore"]));
        Assert.Throws<FileNotFoundException>(() => Visit("CanDoItAll.Projects.Files.UI", _ => throw new FileNotFoundException("Unresolved reference")));
    }

    [Fact]
    public void Renderer_contracts_expose_only_neutral_sessions_content_and_intents() {
        foreach (var type in typeof(ProjectFilesDialogView).Assembly.GetExportedTypes()) {
            Assert.DoesNotContain(type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), property => property.GetCustomAttribute<InjectAttribute>() is not null);
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)) {
                foreach (var exposed in Expand(property.PropertyType)) {
                    Assert.True(Allowed(exposed.Assembly.GetName().Name!), $"{type.Name}.{property.Name}: {exposed}");
                }
            }
        }
    }

    private static IEnumerable<Type> Expand(Type type) => new[] { type }.Concat(type.GetGenericArguments().SelectMany(Expand));
    private static bool Framework(string name) => !name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) &&
        (name is "mscorlib" or "netstandard" || name.StartsWith("System", StringComparison.Ordinal) || name.StartsWith("Microsoft.", StringComparison.Ordinal));
    private static bool Allowed(string name) => Framework(name) || name is
        "CanDoItAll.Projects.Files.UI" or "CanDoItAll.Projects.Files.UiSandbox" or
        "CanDoItAll.Components.BaseLib" or "CanDoItAll.Components.Common" or "CanDoItAll.Components.Mermaid" or
        "CanDoItAll.FileTools.Abstractions" or "CanDoItAll.FileTools.FileBrowser.Core" or "CanDoItAll.FileTools.FileBrowser.Components" or
        "CanDoItAll.FileTools.FileInteraction.Core" or "CanDoItAll.FileTools.FileInteraction.Components" or "CanDoItAll.FileTools.FileInteraction.Markdown" or "Markdig";

    private static void Visit(string root, Func<string, IEnumerable<string>> references) {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>([root]);
        while (pending.TryPop(out string? name)) {
            if (!seen.Add(name)) {
                continue;
            }
            if (!Allowed(name)) {
                throw new InvalidOperationException($"Forbidden reference: {name}");
            }
            if (!Framework(name)) {
                foreach (string reference in references(name)) {
                    pending.Push(reference);
                }
            }
        }
    }
}
