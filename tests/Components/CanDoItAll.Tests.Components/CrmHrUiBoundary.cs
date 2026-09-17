using System.Reflection;
using CanDoItAll.CrmHr.UI.Home;

namespace CanDoItAll.Tests.Components.CrmHr;

// The dependency categories the CRM / HR rendering library may use, shared by the boundary guards. The categories are
// the meaningful part: UI primitives, real shared UI families, lightweight contracts. An exact reference count is not.
internal static class CrmHrUiBoundary
{
    public static Assembly RenderingLibrary => typeof(CrmHrHomeSurface).Assembly;

    // Framework and UI primitives.
    private static readonly string[] Primitives =
    [
        "System",
        "netstandard",
        "Microsoft.AspNetCore.Components",
        "Microsoft.Extensions",
        "CanDoItAll.Components.BaseLib",
        "CanDoItAll.Components.Charts",
        "CanDoItAll.Components.Common",
        "CanDoItAll.Components.Gantt"
    ];

    // Real shared UI families the renderers compose: the paged record browser and picker, and the agent cards.
    private static readonly string[] SharedUiFamilies =
    [
        "CanDoItAll.AppComponents.RecordBrowsing",
        "CanDoItAll.AgentFramework.UI"
    ];

    // Lightweight contracts: the CRM / HR and Projects contract assemblies, the AgentFramework models the agent
    // projection renders, and the shared kernel result types.
    private static readonly string[] Contracts =
    [
        "CanDoItAll.Modules.CrmHr.Contracts",
        "CanDoItAll.Modules.Projects.Contracts",
        "CanDoItAll.AgentFramework.Models",
        "CanDoItAll.SharedKernel"
    ];

    // Implementation, persistence and composition assemblies that must never enter the rendering graph, directly or
    // transitively.
    private static readonly string[] ForbiddenExact =
    [
        "CanDoItAll.Modules.CrmHr",
        "CanDoItAll.Modules.Projects",
        "CanDoItAll.Modules.Workbench",
        "CanDoItAll.Modules.AgentFramework",
        "CanDoItAll.Modules.Workspace",
        "CanDoItAll.Infrastructure",
        "CanDoItAll.AppComponents",
        "CanDoItAll.AgentFramework.Core",
        "CanDoItAll.AgentFramework.Maf",
        "CanDoItAll.AgentFramework.Tooling",
        "CanDoItAll.Composition",
        "CanDoItAll.Web"
    ];

    private static readonly string[] ForbiddenPrefixes =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "Microsoft.Data.Sqlite"
    ];

    public static bool IsAllowedDirectReference(string assemblyName)
        => StartsWithAny(assemblyName, Primitives) ||
           SharedUiFamilies.Contains(assemblyName, StringComparer.Ordinal) ||
           Contracts.Contains(assemblyName, StringComparer.Ordinal);

    // Assemblies whose types a public signature of the library may expose.
    public static bool IsLightGraphAssembly(string assemblyName)
        => IsAllowedDirectReference(assemblyName) ||
           assemblyName == RenderingLibrary.GetName().Name ||
           // Contract assemblies expose their own lightweight dependencies (capability and memory abstractions).
           assemblyName.EndsWith(".Abstractions", StringComparison.Ordinal);

    public static bool IsForbidden(string assemblyName)
        => ForbiddenExact.Contains(assemblyName, StringComparer.Ordinal) || StartsWithAny(assemblyName, ForbiddenPrefixes);

    // The transitive closure of the library's references, loaded from the test output.
    public static IReadOnlyCollection<string> TransitiveReferenceNames()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<Assembly>();
        pending.Push(RenderingLibrary);
        while (pending.Count > 0)
        {
            foreach (var reference in pending.Pop().GetReferencedAssemblies())
            {
                var name = reference.Name ?? string.Empty;
                if (!seen.Add(name) || name.StartsWith("System", StringComparison.Ordinal) || name == "netstandard")
                {
                    continue;
                }

                try
                {
                    pending.Push(Assembly.Load(reference));
                }
                catch (FileNotFoundException)
                {
                    // A reference that is not deployed with the tests cannot contribute further references.
                }
            }
        }

        return seen;
    }

    public static IEnumerable<Type> Expand(Type type)
    {
        yield return type;
        if (type.HasElementType && type.GetElementType() is { } element)
        {
            foreach (var nested in Expand(element))
            {
                yield return nested;
            }
        }

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments().SelectMany(Expand))
            {
                yield return argument;
            }
        }
    }

    private static bool StartsWithAny(string value, IEnumerable<string> prefixes)
        => prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.Ordinal));
}
