using System.Reflection;
using CanDoItAll.Application.UI.Dashboard;
using CanDoItAll.Application.UiSandbox;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components.ApplicationUI;

public sealed class ApplicationBoundaryTests {
    [Fact]
    public void Renderer_and_scenario_host_resolve_without_native_owners() {
        foreach (var root in new[] { typeof(DashboardSurface).Assembly, typeof(DashboardScenarios).Assembly }) {
            Visit(root.GetName().Name!, name => Assembly.Load(name).GetReferencedAssemblies().Select(reference => reference.Name!));
        }
    }

    [Theory]
    [InlineData("CanDoItAll.Web")]
    [InlineData("CanDoItAll.Composition")]
    [InlineData("CanDoItAll.Infrastructure")]
    [InlineData("CanDoItAll.Modules.Workbench")]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Npgsql")]
    public void Direct_and_transitive_native_references_are_rejected(string forbidden) {
        Assert.Throws<InvalidOperationException>(() => Visit(forbidden, _ => []));
        Assert.Throws<InvalidOperationException>(() => Visit("CanDoItAll.Application.UI",
            name => name == "CanDoItAll.Application.UI" ? ["CanDoItAll.Components.BaseLib"] : [forbidden]));
    }

    [Fact]
    public void Unresolved_dependencies_are_not_a_pass() {
        Assert.Throws<FileNotFoundException>(() => Visit("CanDoItAll.Application.UI",
            _ => throw new FileNotFoundException("Unresolved reference")));
    }

    [Fact]
    public void Public_contracts_are_presentation_values_without_service_authority() {
        foreach (var type in typeof(DashboardSurface).Assembly.GetExportedTypes()) {
            Assert.DoesNotContain(type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic),
                property => property.GetCustomAttribute<InjectAttribute>() is not null);
            RequirePresentationContract(type);
        }
    }

    internal static void RequirePresentationContract(Type type) {
        var properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Select(property => property.PropertyType);
        var signatures = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType))
            .Concat(type.GetConstructors().SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType)));
        foreach (var exposed in properties.Concat(signatures).SelectMany(Expand)) {
            if (!Allowed(exposed.Assembly.GetName().Name!) || typeof(IServiceProvider).IsAssignableFrom(exposed)) {
                throw new InvalidOperationException($"Non-presentation contract on {type.Name}: {exposed}");
            }
        }
    }

    internal static void RequirePresentationDescendants(IEnumerable<Type> components) {
        foreach (var component in components) {
            if (!Allowed(component.Assembly.GetName().Name!)) {
                throw new InvalidOperationException($"Non-presentation visual descendant: {component}");
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
        "CanDoItAll.Application.UI" or "CanDoItAll.Application.UiSandbox" or "CanDoItAll.AppComponents" or
        "CanDoItAll.AppComponents.RecordBrowsing" or "CanDoItAll.AppComponents.EmbeddedBrowser" or
        "CanDoItAll.Components.BaseLib" or "CanDoItAll.Components.CanvasLib" or "CanDoItAll.Components.Common" or
        "CanDoItAll.Components.OverlayLib" or "CanDoItAll.Conversations.Components" or "CanDoItAll.SharedKernel" or
        "CanDoItAll.FileTools.Abstractions" or "CanDoItAll.FileTools.Integration.Abstractions" or
        "CanDoItAll.FileTools.FileBrowser.Core" or "CanDoItAll.FileTools.FileBrowser.Components" or
        "CanDoItAll.FileTools.FileInteraction.Core" or "CanDoItAll.FileTools.FileInteraction.Components";

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
