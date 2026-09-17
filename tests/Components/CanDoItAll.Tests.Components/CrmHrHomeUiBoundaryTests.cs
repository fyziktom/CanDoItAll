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
    // The Home surface itself stays on UI primitives: its parameters expose the library's own presentation records only.
    private static readonly string[] HomeParameterAssemblies =
    [
        "System",
        "Microsoft.AspNetCore.Components",
        "CanDoItAll.Components.BaseLib",
        "CanDoItAll.Components.Common"
    ];

    [Fact]
    public void Rendering_library_references_only_the_allowed_dependency_categories()
    {
        var references = CrmHrUiBoundary.RenderingLibrary.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty).ToArray();

        Assert.All(references, reference =>
            Assert.True(CrmHrUiBoundary.IsAllowedDirectReference(reference), $"Unexpected reference from the rendering library: {reference}"));
        Assert.All(references, reference =>
            Assert.False(CrmHrUiBoundary.IsForbidden(reference), $"Forbidden reference from the rendering library: {reference}"));
        Assert.Contains("CanDoItAll.Components.BaseLib", references);
    }

    [Fact]
    public void Home_surface_injects_nothing_and_its_parameters_stay_on_ui_primitives()
    {
        var surface = typeof(CrmHrHomeSurface);
        var properties = surface.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.Empty(properties.Where(property => property.GetCustomAttribute<InjectAttribute>() is not null));

        var parameterAssemblies = properties
            .Where(property => property.GetCustomAttribute<ParameterAttribute>() is not null)
            .Select(property => property.PropertyType)
            .SelectMany(CrmHrUiBoundary.Expand)
            .Select(type => type.Assembly.GetName().Name ?? string.Empty)
            .Distinct()
            .ToArray();
        Assert.All(parameterAssemblies, name =>
            Assert.True(
                HomeParameterAssemblies.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)) || name == surface.Assembly.GetName().Name,
                $"The Home surface exposes assembly {name} in a parameter"));
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

}
