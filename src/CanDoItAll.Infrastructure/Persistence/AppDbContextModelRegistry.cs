using System.Reflection;

namespace CanDoItAll.Infrastructure.Persistence;

public static class AppDbContextModelRegistry
{
    private static readonly object Gate = new();
    private static IReadOnlyList<Assembly> _assemblies = [];

    public static IReadOnlyList<Assembly> Assemblies
    {
        get
        {
            lock (Gate)
            {
                return _assemblies;
            }
        }
    }

    public static void ConfigureAssemblies(IEnumerable<Assembly> assemblies)
    {
        lock (Gate)
        {
            _assemblies = assemblies.Distinct().ToArray();
        }
    }
}
