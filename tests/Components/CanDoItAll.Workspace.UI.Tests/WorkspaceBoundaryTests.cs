using System.Reflection;
using CanDoItAll.Workspace.UI;
using CanDoItAll.Workspace.UiSandbox;
using CanDoItAll.Modules.Workspace;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components.WorkspaceUi;

public sealed class WorkspaceBoundaryTests {
    [Fact]
    public void Renderer_and_sandbox_transitive_closures_contain_only_allowed_feature_and_component_assemblies() {
        foreach (var root in new[] { typeof(WorkspaceSettingsSurface).Assembly, typeof(WorkspaceScenarioStore).Assembly }) {
            Visit(root.GetName().Name!, name => Assembly.Load(name).GetReferencedAssemblies().Select(item => item.Name!).ToArray());
        }
    }

    [Fact]
    public void Traversal_rejects_forbidden_transitive_and_unresolved_edges() {
        Assert.Throws<InvalidOperationException>(() => Visit("CanDoItAll.Workspace.UI", name => name == "CanDoItAll.Workspace.UI" ? ["CanDoItAll.Components.BaseLib"] : ["CanDoItAll.Infrastructure"]));
        Assert.Throws<FileNotFoundException>(() => Visit("CanDoItAll.Workspace.UI", _ => throw new FileNotFoundException("Missing dependency")));
    }

    [Fact]
    public void Public_contract_types_are_light_and_renderers_do_not_inject_owners() {
        var assembly = typeof(WorkspaceSettingsSurface).Assembly;
        var types = assembly.GetExportedTypes().Concat(typeof(WorkspaceSettingsModel).Assembly.GetExportedTypes());
        foreach (var type in types) {
            var exposed = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(item => item.PropertyType)
                .Concat(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).SelectMany(item => item.GetParameters().Select(parameter => parameter.ParameterType).Append(item.ReturnType)));
            foreach (var value in exposed.SelectMany(Expand).Where(item => !item.IsGenericParameter)) {
                Assert.True(Allowed(value.Assembly.GetName().Name!), $"{type.Name} exposes {value}");
            }
        }
        Assert.DoesNotContain(typeof(WorkspaceSettingsSurface).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
            item => item.GetCustomAttribute<InjectAttribute>() is not null);
        Assert.Equal("CanDoItAll.Modules.Workspace.Contracts", typeof(WorkspaceSettingsModel).Assembly.GetName().Name);
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
        "CanDoItAll.Workspace.UI", "CanDoItAll.Workspace.UiSandbox", "CanDoItAll.Modules.Workspace.Contracts", "CanDoItAll.Modules.Workspace.Presentation",
        "CanDoItAll.Security.Abstractions", "CanDoItAll.AgentFramework.ProviderHistory.Abstractions",
        "CanDoItAll.Components.BaseLib", "CanDoItAll.Components.Common"
    ];
}
