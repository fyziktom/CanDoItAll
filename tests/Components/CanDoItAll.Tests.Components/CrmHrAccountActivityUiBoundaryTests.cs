using System.Reflection;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.CrmHr.UI.Accounts;
using CanDoItAll.CrmHr.UI.Activity;
using CanDoItAll.CrmHr.UiSandbox.Components;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Components;
using CanDoItAll.Modules.CrmHr.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

// Guards the boundary of the account summary and activity history surfaces: their public inputs stay in the light
// graph, they inject nothing, and both the module adapters and the sandbox compose the same renderer types in their
// real component trees.
public sealed class CrmHrAccountActivityUiBoundaryTests
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

    [Theory]
    [InlineData(typeof(CrmHrAccountSummarySurface))]
    [InlineData(typeof(CrmHrActivitySurface))]
    public void Surface_parameters_inject_nothing_and_expose_only_light_graph_types(Type surface)
    {
        var properties = surface.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.Empty(properties.Where(property => property.GetCustomAttribute<InjectAttribute>() is not null));

        var parameterAssemblies = properties
            .Where(property => property.GetCustomAttribute<ParameterAttribute>() is not null)
            .Select(property => property.PropertyType)
            .SelectMany(Expand)
            .Select(type => type.Assembly.GetName().Name ?? string.Empty)
            .Distinct()
            .ToArray();
        Assert.NotEmpty(parameterAssemblies);
        Assert.All(parameterAssemblies, name =>
            Assert.True(
                AllowedReferencePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)) || name == surface.Assembly.GetName().Name,
                $"{surface.Name} exposes assembly {name} in a parameter"));
        Assert.Same(typeof(CrmHrAccountSummarySurface).Assembly, surface.Assembly);
    }

    [Fact]
    public void Presentation_records_carry_no_module_types()
    {
        var assembly = typeof(CrmHrAccountSummary).Assembly;
        Assert.Equal("CanDoItAll.CrmHr.UI", assembly.GetName().Name);
        Assert.Same(assembly, typeof(CrmHrActivityPage).Assembly);
        Assert.Same(assembly, typeof(CrmHrActivityCopy).Assembly);
        Assert.Same(assembly, typeof(CrmHrAccountSummaryIntent).Assembly);

        // The library as a whole may use the lightweight contracts; these presentation records stay private to it.
        Type[] records = [typeof(CrmHrAccountSummary), typeof(CrmHrActivityPage), typeof(CrmHrActivityEntry), typeof(CrmHrActivityPresentation), typeof(CrmHrActivityCopy)];
        var exposed = records
            .SelectMany(record => record.GetProperties().Select(property => property.PropertyType))
            .SelectMany(Expand)
            .Select(type => type.Assembly.GetName().Name ?? string.Empty)
            .Distinct()
            .ToArray();
        Assert.All(exposed, name =>
            Assert.True(
                name.StartsWith("System", StringComparison.Ordinal) || name == "CanDoItAll.CrmHr.UI",
                $"A presentation record exposes assembly {name}"));
        Assert.All(
            assembly.GetReferencedAssemblies().Select(reference => reference.Name ?? string.Empty),
            name => Assert.False(CrmHrUiBoundary.IsForbidden(name), $"Forbidden reference from the rendering library: {name}"));
    }

    [Fact]
    public void Module_adapters_compose_the_shared_surfaces_in_their_rendered_trees()
    {
        using var context = CreateContext();

        var panel = context.Render<AccountSummaryPanel>(parameters => parameters.Add(component => component.Account, null));
        var timeline = context.Render<InteractionTimeline>(parameters => parameters.Add(component => component.Presentation, CrmHrActivityPresentation.NotAccepted));

        Assert.Single(panel.FindComponents<CrmHrAccountSummarySurface>());
        Assert.Single(timeline.FindComponents<CrmHrActivitySurface>());
        Assert.Contains(typeof(CrmHrCrmPage).Assembly.GetReferencedAssemblies(), reference => reference.Name == typeof(CrmHrActivitySurface).Assembly.GetName().Name);
    }

    [Fact]
    public void Sandbox_specimen_composes_the_shared_surfaces_without_the_module()
    {
        using var context = CreateContext();
        context.Services.GetRequiredService<NavigationManager>().NavigateTo("/crm-hr/account-activity?scenario=populated");

        var specimen = context.Render<Routes>();

        specimen.WaitForAssertion(() => Assert.Single(specimen.FindComponents<CrmHrAccountSummarySurface>()));
        Assert.Equal(3, specimen.FindComponents<CrmHrActivitySurface>().Count);
        Assert.DoesNotContain(
            typeof(CrmHrAccountActivitySpecimen).Assembly.GetReferencedAssemblies(),
            reference => reference.Name == typeof(CrmHrCrmPage).Assembly.GetName().Name);
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

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }
}
