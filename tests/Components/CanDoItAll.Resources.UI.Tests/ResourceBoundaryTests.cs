using System.Reflection;
using CanDoItAll.Resources.UI;
using CanDoItAll.Resources.UiSandbox;
using CanDoItAll.Modules.Resources;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components.ResourcesUi;

public sealed class ResourceBoundaryTests {
    [Fact]
    public void Renderer_and_sandbox_transitive_closures_contain_only_allowed_feature_and_component_assemblies() {
        foreach (var root in new[] { typeof(ResourcesWorkspaceSurface).Assembly, typeof(ResourceScenarioStore).Assembly }) {
            Visit(root.GetName().Name!, name => Assembly.Load(name).GetReferencedAssemblies().Select(item => item.Name!).ToArray());
        }
    }

    [Fact]
    public void Traversal_rejects_forbidden_transitive_and_unresolved_edges() {
        Assert.Throws<InvalidOperationException>(() => Visit("CanDoItAll.Resources.UI", name => name == "CanDoItAll.Resources.UI" ? ["CanDoItAll.Components.BaseLib"] : ["CanDoItAll.Infrastructure"]));
        Assert.Throws<FileNotFoundException>(() => Visit("CanDoItAll.Resources.UI", _ => throw new FileNotFoundException("Missing dependency")));
    }

    [Fact]
    public void Public_contract_types_are_light_and_renderers_do_not_inject_owners() {
        var assembly = typeof(ResourcesWorkspaceSurface).Assembly;
        var types = assembly.GetExportedTypes().Concat(typeof(ResourceEditorModel).Assembly.GetExportedTypes());
        foreach (var type in types) {
            var exposed = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(item => item.PropertyType)
                .Concat(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).SelectMany(item => item.GetParameters().Select(parameter => parameter.ParameterType).Append(item.ReturnType)));
            foreach (var value in exposed.SelectMany(Expand).Where(item => !item.IsGenericParameter)) {
                Assert.True(Allowed(value.Assembly.GetName().Name!), $"{type.Name} exposes {value}");
            }
        }
        Assert.DoesNotContain(typeof(ResourcesWorkspaceSurface).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
            item => item.GetCustomAttribute<InjectAttribute>() is not null);
        Assert.Equal("CanDoItAll.Modules.Resources.Contracts", typeof(ResourceEditorModel).Assembly.GetName().Name);
    }

    private static IEnumerable<Type> Expand(Type type) {
        yield return type;
        foreach (var argument in type.GetGenericArguments().SelectMany(Expand)) {
            yield return argument;
        }
        if (type.HasElementType) {
            foreach (var element in Expand(type.GetElementType()!)) {
                yield return element;
            }
        }
    }

    private static void Visit(string root, Func<string, IReadOnlyList<string>> references) {
        var pending = new Stack<string>([root]);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (pending.TryPop(out var name)) {
            if (!seen.Add(name)) {
                continue;
            }
            if (!Allowed(name)) {
                throw new InvalidOperationException($"Forbidden dependency: {name}");
            }
            if (Framework(name)) {
                continue;
            }
            foreach (var reference in references(name)) {
                pending.Push(reference);
            }
        }
    }

    private static bool Framework(string name) => name is "mscorlib" or "netstandard" or "Microsoft.AspNetCore" || name.StartsWith("System", StringComparison.Ordinal) || name.StartsWith("Microsoft.AspNetCore.", StringComparison.Ordinal) || name.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal) || name == "Microsoft.JSInterop";
    private static bool Allowed(string name) => Framework(name) || AllowedFeatureAssemblies.Contains(name);
    private static readonly HashSet<string> AllowedFeatureAssemblies = [
        "CanDoItAll.Resources.UI", "CanDoItAll.Resources.UiSandbox", "CanDoItAll.Modules.Resources.Contracts", "CanDoItAll.Modules.Resources.Presentation",
        "CanDoItAll.Configuration.UI", "CanDoItAll.Modules.Projects.Contracts", "CanDoItAll.SharedKernel", "CanDoItAll.FileTools.Integration.Abstractions",
        "CanDoItAll.FileTools.Abstractions", "CanDoItAll.FileTools.FileBrowser.Core", "CanDoItAll.FileTools.FileBrowser.Components",
        "CanDoItAll.FileTools.FileInteraction.Core", "CanDoItAll.FileTools.FileInteraction.Components", "CanDoItAll.AppComponents",
        "CanDoItAll.AppComponents.RecordBrowsing", "CanDoItAll.Conversations.Components", "CanDoItAll.Components.CanvasLib",
        "CanDoItAll.Components.BaseLib", "CanDoItAll.Components.Common", "CanDoItAll.Components.OverlayLib", "Markdig"
    ];
}
