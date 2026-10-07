using System.Reflection;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Editor.UI;
using CanDoItAll.AgentFramework.Editor.UiSandbox;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components.AgentEditorUi;

public sealed class EditorBoundaryTests {
    [Fact]
    public void Actual_renderer_and_sandbox_closures_exclude_module_and_runtime_owners() {
        foreach (var root in new[] { typeof(AgentEditorCoreSurface).Assembly, typeof(AgentEditorScenarioSession).Assembly }) {
            Visit(root.GetName().Name!, name => Assembly.Load(name).GetReferencedAssemblies().Select(value => value.Name!));
        }
    }

    [Fact]
    public void Transitive_forbidden_and_missing_edges_are_rejected() {
        Assert.Throws<InvalidOperationException>(() => Visit("CanDoItAll.AgentFramework.Editor.UI", name => name == "CanDoItAll.AgentFramework.Editor.UI" ? ["CanDoItAll.Components.BaseLib"] : ["CanDoItAll.Infrastructure"]));
        Assert.Throws<InvalidOperationException>(() => Visit("CanDoItAll.AgentFramework.Editor.UI", _ => ["Microsoft.EntityFrameworkCore"]));
        Assert.Throws<InvalidOperationException>(() => Visit("CanDoItAll.AgentFramework.Editor.UI", _ => ["CanDoItAll.Components.Canvas"]));
        Assert.Throws<InvalidOperationException>(() => Visit("CanDoItAll.AgentFramework.Editor.UI", _ => ["CanDoItAll.FileTools"]));
        Assert.Throws<InvalidOperationException>(() => Visit("CanDoItAll.AgentFramework.Editor.UI", _ => ["CanDoItAll.AgentFramework.Core"]));
        Assert.Throws<FileNotFoundException>(() => Visit("CanDoItAll.AgentFramework.Editor.UI", _ => throw new FileNotFoundException("Unresolved reference")));
    }

    [Fact]
    public void Renderer_contracts_expose_only_neutral_sessions_content_and_intents() {
        foreach (var type in typeof(AgentEditorCoreSurface).Assembly.GetExportedTypes()) {
            Assert.DoesNotContain(type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), property => property.GetCustomAttribute<InjectAttribute>() is not null);
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)) {
                foreach (var exposed in Expand(property.PropertyType)) {
                    Assert.True(Allowed(exposed.Assembly.GetName().Name!), $"{type.Name}.{property.Name}: {exposed}");
                    Assert.NotEqual(typeof(ProviderProfile), exposed);
                    Assert.NotEqual(typeof(ProviderProfileEditorModel), exposed);
                }
            }
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly);
            var signatures = methods.SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType))
                .Concat(type.GetConstructors().SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType)));
            foreach (var exposed in signatures.SelectMany(Expand)) {
                Assert.True(Allowed(exposed.Assembly.GetName().Name!), $"{type.Name} signature: {exposed}");
                Assert.NotEqual(typeof(ProviderProfile), exposed);
                Assert.NotEqual(typeof(ProviderProfileEditorModel), exposed);
            }
        }
    }

    private static IEnumerable<Type> Expand(Type type) => new[] { type }.Concat(type.GetGenericArguments().SelectMany(Expand));
    private static bool Framework(string name) => !name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) &&
        (name is "mscorlib" or "netstandard" || name.StartsWith("System", StringComparison.Ordinal) || name.StartsWith("Microsoft.", StringComparison.Ordinal));
    private static bool Allowed(string name) => Framework(name) || name is
        "CanDoItAll.AgentFramework.Editor.UI" or "CanDoItAll.AgentFramework.Editor.UiSandbox" or
        "CanDoItAll.AppComponents.RecordBrowsing" or "CanDoItAll.Workspace.StorageSelection.UI" or
        "CanDoItAll.Modules.Workspace.StorageSelection.Contracts" or "CanDoItAll.AgentFramework.UI" or
        "CanDoItAll.AgentFramework.Usage" or "CanDoItAll.Components.Charts" or "Blazor-ApexCharts" or
        "CanDoItAll.Components.BaseLib" or "CanDoItAll.Components.Common" or "CanDoItAll.Components.OverlayLib" or
        "CanDoItAll.Conversations.Components" or "Markdig" or "CanDoItAll.AgentFramework.Models" or
        "CanDoItAll.AgentFramework.Capabilities.Abstractions" or "CanDoItAll.Memory.Abstractions" or
        "CanDoItAll.AgentFramework.ProviderHistory.Abstractions" or "CanDoItAll.Infrastructure.Abstractions" or "CanDoItAll.SharedKernel";

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
