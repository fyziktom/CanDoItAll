using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Infrastructure.Persistence;

public static class AppDbContextModelRegistry
{
    private static readonly object Gate = new();
    private static readonly Type EntityTypeConfigurationType = typeof(IEntityTypeConfiguration<>);
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
            _assemblies = assemblies
                .Distinct()
                .Where(ContainsEntityTypeConfiguration)
                .ToArray();
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
}
