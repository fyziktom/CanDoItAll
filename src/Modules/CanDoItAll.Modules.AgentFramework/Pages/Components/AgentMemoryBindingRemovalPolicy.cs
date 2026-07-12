using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

internal static class AgentMemoryBindingRemovalPolicy
{
    public static void Remove(
        AgentMemoryAccessSettings settings,
        AgentMemoryProviderAlias alias)
    {
        var removed = settings.ProviderBindings.FirstOrDefault(binding => binding.Alias == alias);
        settings.ProviderBindings = settings.ProviderBindings
            .Where(binding => binding.Alias != alias)
            .ToArray();
        if (removed is null)
        {
            return;
        }

        var providerId = removed.ProviderInstanceId;
        if (SameProvider(settings.PreferredProviderInstanceId, providerId))
        {
            settings.PreferredProviderInstanceId = null;
        }

        if (SameProvider(settings.DefaultProviderInstanceId, providerId))
        {
            settings.DefaultProviderInstanceId = null;
        }

        settings.ProviderAssignments = settings.ProviderAssignments
            .Where(assignment => !SameProvider(assignment.ProviderInstanceId, providerId))
            .ToArray();
        settings.AllowedProviderInstanceIds = settings.AllowedProviderInstanceIds
            .Where(allowed => !SameProvider(allowed, providerId))
            .ToArray();
    }

    private static bool SameProvider(
        MemoryProviderInstanceId? candidate,
        MemoryProviderInstanceId expected) =>
        candidate.HasValue && string.Equals(
            candidate.Value.Value,
            expected.Value,
            StringComparison.OrdinalIgnoreCase);
}
