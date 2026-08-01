using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Infrastructure.Persistence;

public static class AppDbContextModelRegistry
{
    private static readonly object Gate = new();
    private static readonly Type EntityTypeConfigurationType = typeof(IEntityTypeConfiguration<>);
    private static IReadOnlyList<Assembly> _assemblies = [];
    private static string _modelCacheKey = string.Empty;

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

    public static string ModelCacheKey
    {
        get
        {
            lock (Gate)
            {
                return _modelCacheKey;
            }
        }
    }

    public static void ConfigureAssemblies(IEnumerable<Assembly> assemblies)
    {
        lock (Gate)
        {
            var configuredAssemblies = assemblies
                .Distinct()
                .Where(ContainsEntityTypeConfiguration)
                .ToArray();
            var mergedAssemblies = _assemblies
                .Concat(configuredAssemblies)
                .Distinct()
                .OrderBy(assembly => assembly.FullName, StringComparer.Ordinal)
                .ToArray();

            if (_assemblies.SequenceEqual(mergedAssemblies))
            {
                return;
            }

            _assemblies = mergedAssemblies;
            _modelCacheKey = string.Join(
                "|",
                _assemblies.Select(assembly => assembly.FullName ?? assembly.GetName().Name));
        }
    }

    internal static IDisposable UseIsolatedAssembliesForTesting()
    {
        lock (Gate)
        {
            var previousAssemblies = _assemblies;
            var previousModelCacheKey = _modelCacheKey;
            _assemblies = [];
            _modelCacheKey = string.Empty;
            return new TestIsolationScope(previousAssemblies, previousModelCacheKey);
        }
    }

    private static bool ContainsEntityTypeConfiguration(Assembly assembly)
        => GetLoadableTypes(assembly).Any(type =>
            type is { IsAbstract: false, IsInterface: false } &&
            !type.ContainsGenericParameters &&
            type.GetInterfaces().Any(@interface =>
                @interface.IsGenericType &&
                @interface.GetGenericTypeDefinition() == EntityTypeConfigurationType));

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.OfType<Type>();
        }
    }

    private sealed class TestIsolationScope(
        IReadOnlyList<Assembly> previousAssemblies,
        string previousModelCacheKey) : IDisposable
    {
        private int disposed;

        public void Dispose()
        {
            if (System.Threading.Interlocked.Exchange(ref disposed, 1) == 1)
            {
                return;
            }

            lock (Gate)
            {
                _assemblies = previousAssemblies;
                _modelCacheKey = previousModelCacheKey;
            }
        }
    }
}
