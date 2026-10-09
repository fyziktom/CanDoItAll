using System.Reflection;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Processes.UI;
using CanDoItAll.Processes.UiSandbox;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components.ProcessesUI;

public sealed class ProcessBoundaryTests {
    [Fact]
    public void Actual_renderer_and_sandbox_closures_resolve_without_native_owners() {
        foreach (var root in new[] { typeof(ProcessWorkspaceSurface).Assembly, typeof(ProcessesAssets).Assembly }) {
            Visit(root.GetName().Name!, name => Assembly.Load(name).GetReferencedAssemblies().Select(reference => reference.Name!));
        }
    }

    [Fact]
    public void Forbidden_transitive_and_unresolved_edges_fail_closed() {
        foreach (var forbidden in new[] { "CanDoItAll.Modules.Workbench", "CanDoItAll.Modules.Processes",
            "CanDoItAll.Infrastructure", "Microsoft.EntityFrameworkCore", "Microsoft.Agents.AI",
            "CanDoItAll.AgentFramework.Core", "CanDoItAll.FileTools" }) {
            Assert.Throws<InvalidOperationException>(() => Visit("CanDoItAll.Processes.UI",
                name => name == "CanDoItAll.Processes.UI" ? ["CanDoItAll.Components.BaseLib"] : [forbidden]));
        }
        Assert.Throws<FileNotFoundException>(() => Visit("CanDoItAll.Processes.UI",
            _ => throw new FileNotFoundException("Unresolved reference")));
    }

    [Fact]
    public void Public_contracts_and_actual_children_expose_no_native_authority_or_credentials() {
        var assembly = typeof(ProcessWorkspaceSurface).Assembly;
        foreach (var type in assembly.GetExportedTypes()) {
            foreach (var injected in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(property => property.GetCustomAttribute<InjectAttribute>() is not null)) {
                Assert.Equal(typeof(TooltipService), injected.PropertyType);
            }
            var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .Select(property => property.PropertyType);
            var signatures = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
                .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType))
                .Concat(type.GetConstructors().SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType)));
            foreach (var exposed in properties.Concat(signatures).SelectMany(Expand)) {
                Assert.True(Allowed(exposed.Assembly.GetName().Name!), $"{type.Name}: {exposed}");
                Assert.NotEqual(typeof(ProviderProfile), exposed);
                Assert.NotEqual(typeof(ProviderProfileEditorModel), exposed);
                Assert.False(typeof(IServiceProvider).IsAssignableFrom(exposed), $"Service provider exposed by {type.Name}");
            }
        }
    }

    private static IEnumerable<Type> Expand(Type type) {
        yield return type;
        foreach (var child in type.GetGenericArguments().Concat(type.HasElementType ? [type.GetElementType()!] : [])) {
            foreach (var expanded in Expand(child)) {
                yield return expanded;
            }
        }
    }

    private static bool Framework(string name) => name is "mscorlib" or "netstandard" or "Microsoft.CSharp" or "Microsoft.AspNetCore" ||
        name.StartsWith("System.", StringComparison.Ordinal) || name == "System" ||
        name.StartsWith("Microsoft.AspNetCore.", StringComparison.Ordinal) ||
        name.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal) ||
        name.StartsWith("Microsoft.JSInterop", StringComparison.Ordinal);

    private static bool Allowed(string name) => Framework(name) || name is
        "CanDoItAll.Processes.UI" or "CanDoItAll.Processes.UiSandbox" or
        "CanDoItAll.AppComponents.RecordBrowsing" or "CanDoItAll.Components.BaseLib" or "CanDoItAll.Components.CanvasLib" or
        "CanDoItAll.Components.Common" or "CanDoItAll.Components.OverlayLib" or "CanDoItAll.AgentFramework.Models" or
        "CanDoItAll.AgentFramework.Capabilities.Abstractions" or "CanDoItAll.Memory.Abstractions" or
        "CanDoItAll.AgentFramework.ProviderHistory.Abstractions" or "CanDoItAll.Infrastructure.Abstractions" or "CanDoItAll.SharedKernel" or "CanDoItAll.Components.Charts" or "CanDoItAll.Conversations.Components" or
        "CanDoItAll.AgentFramework.UI" or "CanDoItAll.AgentFramework.Usage" or
        "CanDoItAll.Processes.Abstractions" or "CanDoItAll.Processes.Contracts" or "CanDoItAll.Processes.Core" or "CanDoItAll.Processes.Projections" or
        "CanDoItAll.FileTools.Abstractions" or "CanDoItAll.FileTools.FileBrowser.Core" or "CanDoItAll.FileTools.FileBrowser.Components" or
        "CanDoItAll.FileTools.FileInteraction.Core" or "CanDoItAll.FileTools.FileInteraction.Components" or "CanDoItAll.FileTools.FileInteraction.Markdown" or
        "CanDoItAll.Components.Mermaid" or "Blazor-ApexCharts" or "Markdig";

    private static void Visit(string root, Func<string, IEnumerable<string>> references) {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>([root]);
        while (pending.TryPop(out var name)) {
            if (!seen.Add(name)) {
                continue;
            }
            if (!Allowed(name)) {
                throw new InvalidOperationException($"Forbidden reference: {name}");
            }
            if (!Framework(name)) {
                foreach (var reference in references(name)) {
                    pending.Push(reference);
                }
            }
        }
    }
}
