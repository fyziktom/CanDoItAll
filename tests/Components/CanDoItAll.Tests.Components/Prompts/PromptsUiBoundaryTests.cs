using System.Reflection;
using CanDoItAll.Prompts.UI.Gallery;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.Tests.Components.Prompts;

public sealed class PromptsUiBoundaryTests
{
    private static readonly string[] AllowedReferencePrefixes =
    [
        "System",
        "netstandard",
        "Microsoft.AspNetCore.Components",
        "Microsoft.Extensions",
        "CanDoItAll.Components.BaseLib",
        "CanDoItAll.Components.Common",
        "CanDoItAll.Modules.Prompts.Contracts",
        "CanDoItAll.SharedKernel"
    ];

    private static readonly string[] ForbiddenReferenceFragments =
    [
        "CanDoItAll.Modules.Prompts,",
        "CanDoItAll.Modules.AgentFramework",
        "CanDoItAll.AgentFramework",
        "CanDoItAll.Infrastructure",
        "CanDoItAll.Web",
        "CanDoItAll.Composition",
        "CanDoItAll.AppComponents",
        "EntityFrameworkCore"
    ];

    [Fact]
    public void Rendering_library_references_only_contracts_and_ui_primitives()
    {
        var assembly = typeof(PromptGallerySearchSurface).Assembly;
        var references = assembly.GetReferencedAssemblies().Select(reference => reference.FullName).ToArray();

        Assert.All(references, reference =>
            Assert.True(
                AllowedReferencePrefixes.Any(prefix => reference.StartsWith(prefix, StringComparison.Ordinal)),
                $"Unexpected reference from the rendering library: {reference}"));
        Assert.All(references, reference =>
            Assert.DoesNotContain(ForbiddenReferenceFragments, fragment => reference.Contains(fragment, StringComparison.Ordinal)));
        Assert.Contains(references, reference => reference.StartsWith("CanDoItAll.Modules.Prompts.Contracts", StringComparison.Ordinal));
    }

    [Fact]
    public void Rendering_components_inject_no_services_and_public_signatures_stay_in_the_light_graph()
    {
        var assembly = typeof(PromptGallerySearchSurface).Assembly;
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
