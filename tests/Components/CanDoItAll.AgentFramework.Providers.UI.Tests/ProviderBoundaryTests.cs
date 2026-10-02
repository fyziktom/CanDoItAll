using System.Reflection;
using CanDoItAll.AgentFramework.Providers.UI;
using CanDoItAll.AgentFramework.Providers.UiSandbox;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components.ProviderProfilesUi;

public sealed class ProviderBoundaryTests {
    [Fact]
    public void Actual_leaf_and_sandbox_transitive_closures_have_no_runtime_owners() {
        foreach (var root in new[] { typeof(ProviderProfilesSurface).Assembly, typeof(ProviderScenarioSession).Assembly }) {
            Visit(root.GetName().Name!, name => Assembly.Load(name).GetReferencedAssemblies().Select(value => value.Name!));
        }
    }

    [Theory]
    [InlineData("CanDoItAll.Modules.AgentFramework")]
    [InlineData("CanDoItAll.AgentFramework.Core")]
    [InlineData("CanDoItAll.AgentFramework.Providers")]
    [InlineData("CanDoItAll.Infrastructure")]
    [InlineData("CanDoItAll.Modules.AgentFramework.ProviderManagement")]
    [InlineData("CanDoItAll.Components.Canvas")]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("CanDoItAll.AgentFramework.Editor.UI")]
    [InlineData("CanDoItAll.Web")]
    public void Transitive_forbidden_reference_is_rejected(string forbidden) =>
        Assert.Throws<InvalidOperationException>(() => Visit("CanDoItAll.AgentFramework.Providers.UI", name =>
            name == "CanDoItAll.AgentFramework.Providers.UI" ? ["CanDoItAll.Components.BaseLib"] : [forbidden]));

    [Fact]
    public void Missing_and_cyclic_edges_fail_explicitly() {
        Assert.Throws<FileNotFoundException>(() => Visit("CanDoItAll.AgentFramework.Providers.UI", _ => throw new FileNotFoundException("Unresolved edge")));
        Assert.Throws<InvalidOperationException>(() => Visit("CanDoItAll.AgentFramework.Providers.UI", name =>
            name == "CanDoItAll.AgentFramework.Providers.UI" ? ["CanDoItAll.Components.BaseLib"] : ["CanDoItAll.AgentFramework.Providers.UI"]));
    }

    [Fact]
    public void Public_signatures_and_injections_remain_safe() {
        foreach (var type in typeof(ProviderProfilesSurface).Assembly.GetExportedTypes()) {
            Assert.DoesNotContain(type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                property => property.GetCustomAttribute<InjectAttribute>() is not null);
            var signatures = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Select(property => property.PropertyType)
                .Concat(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType)))
                .Concat(type.GetConstructors().SelectMany(constructor => constructor.GetParameters().Select(parameter => parameter.ParameterType)));
            foreach (var exposed in signatures.SelectMany(Expand)) {
                Assert.True(Allowed(exposed.Assembly.GetName().Name!), $"{type.FullName} exposes {exposed}");
            }
        }
        Assert.Equal(["Reference", "Name"], typeof(ProviderSecretChoice).GetProperties().Select(property => property.Name));
    }

    private static IEnumerable<Type> Expand(Type type) => new[] { type }.Concat(type.GetGenericArguments().SelectMany(Expand));
    private static bool Framework(string name) => !name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) &&
        (name is "mscorlib" or "netstandard" || name.StartsWith("System", StringComparison.Ordinal) || name.StartsWith("Microsoft.", StringComparison.Ordinal));
    private static bool Allowed(string name) => Framework(name) || name is
        "CanDoItAll.AgentFramework.Providers.UI" or "CanDoItAll.AgentFramework.Providers.UiSandbox" or
        "CanDoItAll.Components.BaseLib" or "CanDoItAll.Components.Common" or "CanDoItAll.AgentFramework.Models" or
        "CanDoItAll.AgentFramework.Capabilities.Abstractions" or "CanDoItAll.Memory.Abstractions" or
        "CanDoItAll.AgentFramework.ProviderHistory.Abstractions" or "CanDoItAll.Infrastructure.Abstractions" or "CanDoItAll.SharedKernel";

    private static void Visit(string root, Func<string, IEnumerable<string>> references) {
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
