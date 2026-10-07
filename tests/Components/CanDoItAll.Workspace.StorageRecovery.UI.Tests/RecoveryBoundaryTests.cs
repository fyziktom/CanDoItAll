using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using CanDoItAll.Modules.Workspace.StorageRecovery.Contracts;
using CanDoItAll.Workspace.StorageRecovery.UI;
using CanDoItAll.Workspace.StorageRecovery.UiSandbox;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components.WorkspaceStorageRecoveryUi;

[Trait("Category", "HostPlatform")]
public sealed class RecoveryBoundaryTests {
    [Fact]
    public void Runtime_closure_and_public_contracts_have_only_the_leaf_and_component_dependencies() {
        foreach (var root in new[] { typeof(RecoveryDialog).Assembly, typeof(RecoveryScenarioOwner).Assembly }) {
            Visit(root.GetName().Name!, name => Assembly.Load(name).GetReferencedAssemblies().Select(reference => reference.Name!).ToArray());
        }
        foreach (var type in typeof(RecoveryDialog).Assembly.GetExportedTypes().Concat(typeof(RecoveryItem).Assembly.GetExportedTypes())) {
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
        Assert.Throws<InvalidOperationException>(() => Visit("CanDoItAll.Workspace.StorageRecovery.UI", _ => ["CanDoItAll.Infrastructure"]));
        Assert.Throws<FileNotFoundException>(() => Visit("CanDoItAll.Workspace.StorageRecovery.UI", _ => throw new FileNotFoundException()));
        Assert.Throws<InvalidOperationException>(() => Visit("CanDoItAll.Workspace.StorageRecovery.UI", _ => ["CanDoItAll.Workspace.StorageRecovery.UI"]));
    }

    [Fact]
    public void Restored_sandbox_graph_is_light_and_core_and_foundation_gain_no_reverse_edge() {
        var root = RepositoryRoot();
        var project = Path.Combine(root, "src", "Sandboxes", "CanDoItAll.Workspace.StorageRecovery.UiSandbox");
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
            "src/UI/CanDoItAll.Workspace.StorageCatalog.UI/CanDoItAll.Workspace.StorageCatalog.UI.csproj",
            "src/Modules/CanDoItAll.Modules.Workspace.StorageCatalog.Contracts/CanDoItAll.Modules.Workspace.StorageCatalog.Contracts.csproj",
            "src/Sandboxes/CanDoItAll.Workspace.StorageCatalog.UiSandbox/CanDoItAll.Workspace.StorageCatalog.UiSandbox.csproj",
            "src/UI/CanDoItAll.Workspace.StorageSelection.UI/CanDoItAll.Workspace.StorageSelection.UI.csproj",
            "src/Modules/CanDoItAll.Modules.Workspace.StorageSelection.Contracts/CanDoItAll.Modules.Workspace.StorageSelection.Contracts.csproj",
            "src/Sandboxes/CanDoItAll.Workspace.StorageSelection.UiSandbox/CanDoItAll.Workspace.StorageSelection.UiSandbox.csproj",
            "src/UI/CanDoItAll.Workspace.ApiAccess.UI/CanDoItAll.Workspace.ApiAccess.UI.csproj",
            "src/Modules/CanDoItAll.Modules.Workspace.ApiAccess.Contracts/CanDoItAll.Modules.Workspace.ApiAccess.Contracts.csproj",
            "src/Sandboxes/CanDoItAll.Workspace.ApiAccess.UiSandbox/CanDoItAll.Workspace.ApiAccess.UiSandbox.csproj",
            "src/UI/CanDoItAll.Workspace.UI/CanDoItAll.Workspace.UI.csproj",
            "src/Modules/CanDoItAll.Modules.Workspace.Contracts/CanDoItAll.Modules.Workspace.Contracts.csproj",
            "src/Modules/CanDoItAll.Modules.Workspace.Presentation/CanDoItAll.Modules.Workspace.Presentation.csproj",
            "src/Sandboxes/CanDoItAll.Workspace.UiSandbox/CanDoItAll.Workspace.UiSandbox.csproj"
        }.Concat(Directory.EnumerateFiles(Path.Combine(root, "src", "Foundation"), "*.csproj", SearchOption.AllDirectories))) {
            var document = XDocument.Load(Path.IsPathRooted(path) ? path : Path.Combine(root, path));
            Assert.DoesNotContain(document.Descendants("ProjectReference"), reference => reference.Attribute("Include")!.Value.Contains("Workspace.StorageRecovery", StringComparison.Ordinal));
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
        "CanDoItAll.Modules.Workspace.StorageRecovery.Contracts", "CanDoItAll.Workspace.StorageRecovery.UI", "CanDoItAll.Workspace.StorageRecovery.UiSandbox",
        "CanDoItAll.Components.BaseLib", "CanDoItAll.Components.Common"
    ];
}
