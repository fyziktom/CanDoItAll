using System.Reflection;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Workbench.Execution.UI.Processes;
using CanDoItAll.Workbench.Execution.UiSandbox;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components.WorkbenchExecution;

public sealed class ExecutionBoundaryTests {
    [Fact]
    public void Actual_renderer_and_sandbox_closures_resolve_without_native_owners() {
        foreach (var root in new[] { typeof(ProcessAssignmentDialog).Assembly, typeof(ExecutionAssets).Assembly }) {
            Visit(root.GetName().Name!, name => Assembly.Load(name).GetReferencedAssemblies().Select(reference => reference.Name!));
        }
    }

    [Fact]
    public void Forbidden_transitive_and_unresolved_edges_fail_closed() {
        foreach (var forbidden in new[] { "CanDoItAll.Modules.Workbench", "CanDoItAll.Modules.Processes",
            "CanDoItAll.Infrastructure", "Microsoft.EntityFrameworkCore", "CanDoItAll.AgentFramework.Core", "CanDoItAll.FileTools" }) {
            Assert.Throws<InvalidOperationException>(() => Visit("CanDoItAll.Workbench.Execution.UI",
                name => name == "CanDoItAll.Workbench.Execution.UI" ? ["CanDoItAll.Components.BaseLib"] : [forbidden]));
        }
        Assert.Throws<FileNotFoundException>(() => Visit("CanDoItAll.Workbench.Execution.UI",
            _ => throw new FileNotFoundException("Unresolved reference")));
    }

    [Fact]
    public void Public_contracts_and_actual_children_expose_no_native_authority_or_credentials() {
        var assembly = typeof(ProcessAssignmentDialog).Assembly;
        Assert.Equal(assembly, typeof(CanDoItAll.Modules.Workbench.Pages.ProjectStructureProcessAgentPickerDialog).Assembly);
        Assert.Equal(assembly, typeof(CanDoItAll.Modules.Workbench.Pages.ProjectStructureProcessAgentDetailsDialog).Assembly);
        Assert.Equal(assembly, typeof(CanDoItAll.Modules.Workbench.Pages.ProjectStructureProcessAgentSwitchConfirmationDialog).Assembly);
        foreach (var type in assembly.GetExportedTypes()) {
            foreach (var injected in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(property => property.GetCustomAttribute<InjectAttribute>() is not null)) {
                Assert.Equal(typeof(DialogService), injected.PropertyType);
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

    private static bool Framework(string name) => !name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) &&
        (name is "mscorlib" or "netstandard" || name.StartsWith("System", StringComparison.Ordinal) || name.StartsWith("Microsoft.", StringComparison.Ordinal));

    private static bool Allowed(string name) => Framework(name) || name is
        "CanDoItAll.Workbench.Execution.UI" or "CanDoItAll.Workbench.Execution.UiSandbox" or
        "CanDoItAll.AppComponents.RecordBrowsing" or "CanDoItAll.Components.BaseLib" or "CanDoItAll.Components.CanvasLib" or
        "CanDoItAll.Components.Common" or "CanDoItAll.Components.OverlayLib" or "CanDoItAll.AgentFramework.Models" or
        "CanDoItAll.AgentFramework.Capabilities.Abstractions" or "CanDoItAll.Memory.Abstractions" or
        "CanDoItAll.AgentFramework.ProviderHistory.Abstractions" or "CanDoItAll.Infrastructure.Abstractions" or "CanDoItAll.SharedKernel";

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
