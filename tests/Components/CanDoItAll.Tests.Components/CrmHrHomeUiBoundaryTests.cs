using System.Reflection;
using CanDoItAll.CrmHr.UI.Home;
using CanDoItAll.CrmHr.UiSandbox.Components;
using CanDoItAll.Modules.CrmHr.Pages;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components.CrmHr;

// Guards the meaningful boundaries of the Home renderer: its direct assembly references, the absence of injected
// services, the assemblies its public signatures expose, and that both hosts compose the same renderer type. The
// reference assertions cover the direct set, not the transitive graph, which the record states separately.
public sealed class CrmHrHomeUiBoundaryTests
{
    private static readonly string[] AllowedReferencePrefixes =
    [
        "System",
        "netstandard",
        "Microsoft.AspNetCore.Components",
        "Microsoft.Extensions",
        "CanDoItAll.Components.BaseLib",
        "CanDoItAll.Components.Charts",
        "CanDoItAll.Components.Common"
    ];

    private static readonly string[] ForbiddenReferenceFragments =
    [
        "CanDoItAll.Modules.",
        "CanDoItAll.AgentFramework",
        "CanDoItAll.Infrastructure",
        "CanDoItAll.Web",
        "CanDoItAll.Composition",
        "CanDoItAll.AppComponents",
        "CanDoItAll.SharedKernel",
        "EntityFrameworkCore"
    ];

    [Fact]
    public void Rendering_library_references_only_ui_primitives()
    {
        var references = typeof(CrmHrHomeSurface).Assembly.GetReferencedAssemblies().Select(reference => reference.FullName).ToArray();

        Assert.All(references, reference =>
            Assert.True(
                AllowedReferencePrefixes.Any(prefix => reference.StartsWith(prefix, StringComparison.Ordinal)),
                $"Unexpected reference from the rendering library: {reference}"));
        Assert.All(references, reference =>
            Assert.DoesNotContain(ForbiddenReferenceFragments, fragment => reference.Contains(fragment, StringComparison.Ordinal)));
        Assert.Contains(references, reference => reference.StartsWith("CanDoItAll.Components.BaseLib", StringComparison.Ordinal));
    }

    [Fact]
    public void Rendering_components_inject_no_services_and_public_signatures_stay_in_the_light_graph()
    {
        var assembly = typeof(CrmHrHomeSurface).Assembly;
        var components = assembly.GetTypes().Where(type => typeof(IComponent).IsAssignableFrom(type) && !type.IsAbstract).ToArray();
        Assert.NotEmpty(components);

        foreach (var component in components)
        {
            var injected = component
                .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(property => property.GetCustomAttribute<InjectAttribute>() is not null)
                .Select(property => $"{component.Name}.{property.Name}")
                .ToArray();
            Assert.Empty(injected);
        }

        var publicTypeAssemblies = assembly.GetExportedTypes()
            .SelectMany(type => type.GetProperties().Select(property => property.PropertyType)
                .Concat(type.GetMethods().SelectMany(method => method.GetParameters().Select(parameter => parameter.ParameterType))))
            .SelectMany(Expand)
            .Select(type => type.Assembly.GetName().Name ?? string.Empty)
            .Distinct()
            .ToArray();
        Assert.All(publicTypeAssemblies, name =>
            Assert.True(
                AllowedReferencePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)) ||
                name == assembly.GetName().Name,
                $"Public signature leaks assembly {name}"));
    }

    [Fact]
    public void Production_host_and_sandbox_compose_the_same_home_renderer_type()
    {
        // Both hosts declare CrmHrHomeSurface in their own assemblies' component graphs; the renderer type is shared.
        var rendererAssembly = typeof(CrmHrHomeSurface).Assembly.GetName().Name;

        Assert.Equal("CanDoItAll.CrmHr.UI", rendererAssembly);
        Assert.Contains(typeof(CrmHrHomePage).Assembly.GetReferencedAssemblies(), reference => reference.Name == rendererAssembly);
        Assert.Contains(typeof(CrmHrHomeSpecimen).Assembly.GetReferencedAssemblies(), reference => reference.Name == rendererAssembly);
        Assert.DoesNotContain(
            typeof(CrmHrHomeSpecimen).Assembly.GetReferencedAssemblies(),
            reference => reference.Name == typeof(CrmHrHomePage).Assembly.GetName().Name);
    }

    private static IEnumerable<Type> Expand(Type type)
    {
        yield return type;
        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments().SelectMany(Expand))
            {
                yield return argument;
            }
        }
    }
}
