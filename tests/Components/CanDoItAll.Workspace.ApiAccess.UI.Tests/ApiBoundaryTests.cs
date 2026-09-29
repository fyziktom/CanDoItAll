using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using CanDoItAll.Modules.Workspace.ApiAccess.Contracts;
using CanDoItAll.Workspace.ApiAccess.UI;
using CanDoItAll.Workspace.ApiAccess.UiSandbox;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components.WorkspaceApiUi;

[Trait("Category", "HostPlatform")]
public sealed class ApiBoundaryTests {
    [Fact]
    public void Runtime_closure_and_public_contracts_have_only_the_leaf_and_component_dependencies() {
        foreach (var root in new[] { typeof(ApiAccessSurface).Assembly, typeof(ApiScenarioStore).Assembly }) {
            Visit(root.GetName().Name!, name => Assembly.Load(name).GetReferencedAssemblies().Select(reference => reference.Name!).ToArray());
        }
        foreach (var type in typeof(ApiAccessSurface).Assembly.GetExportedTypes().Concat(typeof(ApiAccessConfiguration).Assembly.GetExportedTypes())) {
            var exposed = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(property => property.PropertyType)
                .Concat(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType)));
            foreach (var value in exposed.SelectMany(Expand).Where(value => !value.IsGenericParameter)) {
                Assert.True(Allowed(value.Assembly.GetName().Name!), $"{type.Name} exposes {value}");
            }
            if (typeof(ComponentBase).IsAssignableFrom(type)) {
                Assert.DoesNotContain(type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly), property => property.GetCustomAttribute<InjectAttribute>() is not null);
            }
        }
    }

    [Fact]
    public void Traversal_rejects_forbidden_unresolved_and_cyclic_dependencies() {
        Assert.Throws<InvalidOperationException>(() => Visit("CanDoItAll.Workspace.ApiAccess.UI", _ => ["CanDoItAll.Infrastructure"]));
        Assert.Throws<FileNotFoundException>(() => Visit("CanDoItAll.Workspace.ApiAccess.UI", _ => throw new FileNotFoundException()));
        Assert.Throws<InvalidOperationException>(() => Visit("CanDoItAll.Workspace.ApiAccess.UI", _ => ["CanDoItAll.Workspace.ApiAccess.UI"]));
    }

    [Fact]
    public void Restored_sandbox_graph_is_light_and_core_and_foundation_gain_no_reverse_edge() {
        var root = RepositoryRoot();
        var project = Path.Combine(root, "src", "Sandboxes", "CanDoItAll.Workspace.ApiAccess.UiSandbox");
        using var assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(project, "obj", "project.assets.json")));
        var libraries = assets.RootElement.GetProperty("libraries");
        var projects = libraries.EnumerateObject().Where(library => library.Value.GetProperty("type").GetString() == "project").Select(library => library.Name.Split('/')[0]).ToArray();
        Assert.Equal(4, projects.Length);
        Assert.All(projects, name => Assert.Contains(name, FeatureAssemblies));
        foreach (var target in assets.RootElement.GetProperty("targets").EnumerateObject()) {
            foreach (var library in target.Value.EnumerateObject()) {
                Assert.False(library.Value.TryGetProperty("native", out _));
                if (library.Value.TryGetProperty("runtimeTargets", out var runtimeTargets)) {
                    Assert.DoesNotContain(runtimeTargets.EnumerateObject(), asset => asset.Value.GetProperty("assetType").GetString() == "native");
                }
            }
        }
        foreach (var path in new[] {
            "src/UI/CanDoItAll.Workspace.UI/CanDoItAll.Workspace.UI.csproj",
            "src/Modules/CanDoItAll.Modules.Workspace.Contracts/CanDoItAll.Modules.Workspace.Contracts.csproj",
            "src/Modules/CanDoItAll.Modules.Workspace.Presentation/CanDoItAll.Modules.Workspace.Presentation.csproj",
            "src/Sandboxes/CanDoItAll.Workspace.UiSandbox/CanDoItAll.Workspace.UiSandbox.csproj"
        }.Concat(Directory.EnumerateFiles(Path.Combine(root, "src", "Foundation"), "*.csproj", SearchOption.AllDirectories))) {
            var document = XDocument.Load(Path.IsPathRooted(path) ? path : Path.Combine(root, path));
            Assert.DoesNotContain(document.Descendants("ProjectReference"), reference => reference.Attribute("Include")!.Value.Contains("Workspace.ApiAccess", StringComparison.Ordinal));
        }
    }

    private static string RepositoryRoot() {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent) {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx"))) {
                return directory.FullName;
            }
        }
        throw new DirectoryNotFoundException("Repository root not found.");
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
        var complete = new HashSet<string>(StringComparer.Ordinal);
        var active = new HashSet<string>(StringComparer.Ordinal);
        Walk(root);
        void Walk(string name) {
            if (!Allowed(name)) {
                throw new InvalidOperationException($"Forbidden dependency: {name}");
            }
            if (Framework(name) || complete.Contains(name)) {
                return;
            }
            if (!active.Add(name)) {
                throw new InvalidOperationException($"Dependency cycle: {name}");
            }
            foreach (var reference in references(name)) {
                Walk(reference);
            }
            active.Remove(name);
            complete.Add(name);
        }
    }
    private static bool Framework(string name) => name is "mscorlib" or "netstandard" or "Microsoft.AspNetCore" or "Microsoft.JSInterop" || name.StartsWith("System", StringComparison.Ordinal) || name.StartsWith("Microsoft.AspNetCore.", StringComparison.Ordinal) || name.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal);
    private static bool Allowed(string name) => Framework(name) || FeatureAssemblies.Contains(name);
    private static readonly HashSet<string> FeatureAssemblies = [
        "CanDoItAll.Modules.Workspace.ApiAccess.Contracts", "CanDoItAll.Workspace.ApiAccess.UI", "CanDoItAll.Workspace.ApiAccess.UiSandbox",
        "CanDoItAll.Components.BaseLib", "CanDoItAll.Components.Common"
    ];
}
