using System.Reflection;
using CanDoItAll.AgentFramework.SharedProviders.UI;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components.SharedProvidersUi;

public sealed class SharedProviderBoundaryTests {
    [Fact]
    public void Actual_leaf_transitive_closure_has_no_runtime_owners() =>
        Visit(typeof(SharedProviderSharingSurface).Assembly.GetName().Name!,
            name => Assembly.Load(name).GetReferencedAssemblies().Select(reference => reference.Name!));

    [Theory]
    [InlineData("CanDoItAll.Modules.AgentFramework")]
    [InlineData("CanDoItAll.Modules.AgentFramework.ProviderManagement")]
    [InlineData("CanDoItAll.Modules.Security")]
    [InlineData("CanDoItAll.AgentFramework.Models")]
    [InlineData("CanDoItAll.AgentFramework.Core")]
    [InlineData("CanDoItAll.AgentFramework.Providers.UI")]
    [InlineData("CanDoItAll.AgentFramework.Editor.UI")]
    [InlineData("CanDoItAll.Infrastructure")]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("CanDoItAll.Web")]
    public void Forbidden_reference_is_rejected_through_a_transitive_edge(string forbidden) =>
        Assert.Throws<InvalidOperationException>(() => Visit("CanDoItAll.AgentFramework.SharedProviders.UI", name =>
            name == "CanDoItAll.AgentFramework.SharedProviders.UI" ? ["CanDoItAll.Components.BaseLib"] : [forbidden]));

    [Fact]
    public void Unresolved_and_cyclic_edges_fail_explicitly() {
        Assert.Throws<FileNotFoundException>(() => Visit("CanDoItAll.AgentFramework.SharedProviders.UI", _ => throw new FileNotFoundException()));
        Assert.Throws<InvalidOperationException>(() => Visit("CanDoItAll.AgentFramework.SharedProviders.UI", name =>
            name == "CanDoItAll.AgentFramework.SharedProviders.UI" ? ["CanDoItAll.Components.BaseLib"] : ["CanDoItAll.AgentFramework.SharedProviders.UI"]));
    }

    [Fact]
    public void Entire_leaf_signatures_and_component_injections_remain_safe() {
        foreach (var type in typeof(SharedProviderSharingSurface).Assembly.GetExportedTypes()) {
            Assert.DoesNotContain(type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                property => property.GetCustomAttribute<InjectAttribute>() is not null);
            var signatures = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(property => property.PropertyType)
                .Concat(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType)))
                .Concat(type.GetConstructors().SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType)));
            foreach (var exposed in signatures.SelectMany(Expand)) {
                Assert.True(Allowed(exposed.Assembly.GetName().Name!), $"{type.FullName} exposes {exposed}");
                Assert.NotEqual(typeof(IServiceProvider), exposed);
            }
        }
    }

    private static IEnumerable<Type> Expand(Type type) => new[] { type }.Concat(type.GetGenericArguments().SelectMany(Expand));
    private static bool Framework(string name) => !name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) &&
        (name is "mscorlib" or "netstandard" || name.StartsWith("System", StringComparison.Ordinal) || name.StartsWith("Microsoft.", StringComparison.Ordinal));
    private static bool Allowed(string name) => Framework(name) || name is "CanDoItAll.AgentFramework.SharedProviders.UI" or
        "CanDoItAll.AgentFramework.SharedProviders.UiSandbox" or "CanDoItAll.Components.BaseLib" or
        "CanDoItAll.Components.Common" or "CanDoItAll.SharedProviders.Abstractions";

    internal static void Visit(string root, Func<string, IEnumerable<string>> references) {
        var complete = new HashSet<string>(StringComparer.Ordinal);
        var active = new HashSet<string>(StringComparer.Ordinal);
        Walk(root);
        void Walk(string name) {
            if (!Allowed(name)) {
                throw new InvalidOperationException($"Forbidden reference: {name}");
            }
            if (Framework(name) || complete.Contains(name)) {
                return;
            }
            if (!active.Add(name)) {
                throw new InvalidOperationException($"Cyclic reference: {name}");
            }
            foreach (var reference in references(name)) {
                Walk(reference);
            }
            active.Remove(name);
            complete.Add(name);
        }
    }
}
