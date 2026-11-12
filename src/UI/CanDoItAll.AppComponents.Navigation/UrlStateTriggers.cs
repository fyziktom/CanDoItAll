using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.AppComponents.Navigation;

public static class UrlStateTriggers {
    public static IReadOnlyList<string> Missing(Type componentType, IEnumerable<string> keys) {
        ArgumentNullException.ThrowIfNull(componentType);
        ArgumentNullException.ThrowIfNull(keys);
        var declared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var type = componentType; type is not null && type != typeof(object); type = type.BaseType) {
            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)) {
                if (property.GetCustomAttribute<SupplyParameterFromQueryAttribute>() is { } trigger) {
                    declared.Add(trigger.Name ?? property.Name);
                }
            }
        }
        return keys.Where(key => !declared.Contains(key)).ToArray();
    }
}
