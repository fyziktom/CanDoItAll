using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.AgentFramework.Models;

internal static class AgentMemoryAccessNormalizer
{
    public static AgentMemoryAccessSettings Normalize(AgentMemoryAccessSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var allowedProviders = NormalizeProviderIds(settings.AllowedProviderInstanceIds);
        var preferredProvider = NormalizeProviderId(settings.PreferredProviderInstanceId);
        var defaultProvider = NormalizeProviderId(settings.DefaultProviderInstanceId);
        var assignments = NormalizeAssignments(settings.ProviderAssignments);
        var bindings = NormalizeBindings(settings.ProviderBindings);
        var allowedCapabilities = NormalizeCapabilities(settings.AllowedCapabilityIds);
        var deniedCapabilities = NormalizeCapabilities(settings.DeniedCapabilityIds);

        AgentMemoryAccessValidator.Validate(
            settings,
            allowedProviders,
            bindings,
            preferredProvider,
            defaultProvider,
            allowedCapabilities,
            deniedCapabilities,
            assignments);

        return new AgentMemoryAccessSettings
        {
            InvocationMode = settings.InvocationMode,
            CanUseMemoryTools = settings.InvocationMode == AgentMemoryInvocationMode.Automatic &&
                                settings.CanUseMemoryTools,
            RequireContextContributions = settings.RequireContextContributions,
            AllowAsyncContextContributions = false,
            CanIngestSources = false,
            PreferredProviderInstanceId = preferredProvider,
            DefaultProviderInstanceId = defaultProvider,
            AllowedProviderInstanceIds = allowedProviders,
            ProviderBindings = bindings,
            AllowedCapabilityIds = allowedCapabilities,
            DeniedCapabilityIds = deniedCapabilities,
            AllowedSourceScopes = settings.AllowedSourceScopes.Distinct().ToArray(),
            ProviderAssignments = assignments
        };
    }

    public static bool IsDefault(AgentMemoryAccessSettings settings)
    {
        return settings.InvocationMode == AgentMemoryInvocationMode.Disabled &&
               !settings.CanUseMemoryTools &&
               !settings.RequireContextContributions &&
               !settings.AllowAsyncContextContributions &&
               !settings.CanIngestSources &&
               settings.PreferredProviderInstanceId is null &&
               settings.DefaultProviderInstanceId is null &&
               settings.AllowedProviderInstanceIds.Count == 0 &&
               settings.ProviderBindings.Count == 0 &&
               settings.AllowedCapabilityIds.Count == 0 &&
               settings.DeniedCapabilityIds.Count == 0 &&
               settings.AllowedSourceScopes.Count == 0 &&
               settings.ProviderAssignments.Count == 0;
    }

    private static IReadOnlyList<AgentMemoryProviderBindingSetting> NormalizeBindings(
        IEnumerable<AgentMemoryProviderBindingSetting> bindings)
    {
        return bindings
            .Select(binding => binding with
            {
                Alias = AgentMemoryProviderAlias.Parse(binding.Alias.Value),
                ProviderInstanceId = MemoryProviderInstanceId.Parse(binding.ProviderInstanceId.Value.Trim())
            })
            .ToArray();
    }

    private static IReadOnlyList<MemoryProviderInstanceId> NormalizeProviderIds(
        IEnumerable<MemoryProviderInstanceId> providers)
    {
        return providers
            .Select(provider => MemoryProviderInstanceId.Parse(provider.Value.Trim()))
            .DistinctBy(provider => provider.Value, StringComparer.OrdinalIgnoreCase)
            .OrderBy(provider => provider.Value, StringComparer.Ordinal)
            .ToArray();
    }

    private static MemoryProviderInstanceId? NormalizeProviderId(MemoryProviderInstanceId? provider)
    {
        return provider is null
            ? null
            : MemoryProviderInstanceId.Parse(provider.Value.Value.Trim());
    }

    private static IReadOnlyList<MemoryCapabilityId> NormalizeCapabilities(
        IEnumerable<MemoryCapabilityId> capabilities)
    {
        return capabilities
            .Select(capability => MemoryCapabilityId.Parse(capability.Value.Trim()))
            .Distinct()
            .ToArray();
    }

    private static IReadOnlyList<AgentMemoryProviderAssignmentSetting> NormalizeAssignments(
        IEnumerable<AgentMemoryProviderAssignmentSetting> assignments)
    {
        var normalized = assignments.Select(assignment => assignment with
        {
            Scope = Enum.IsDefined(assignment.Scope)
                ? assignment.Scope
                : throw new AgentMemoryConfigurationException(
                    $"Unsupported memory provider assignment scope '{assignment.Scope}'."),
            Key = string.IsNullOrWhiteSpace(assignment.Key)
                ? throw new AgentMemoryConfigurationException("Memory provider assignment keys cannot be empty.")
                : assignment.Key.Trim(),
            ProviderInstanceId = MemoryProviderInstanceId.Parse(assignment.ProviderInstanceId.Value.Trim())
        }).ToArray();

        var duplicate = normalized
            .GroupBy(assignment => assignment.Scope)
            .SelectMany(group => group.GroupBy(assignment => assignment.Key, StringComparer.OrdinalIgnoreCase))
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            var entries = duplicate.ToArray();
            var providerCount = entries
                .Select(entry => entry.ProviderInstanceId.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            var conflict = providerCount > 1 ? "conflicting providers" : "more than once";
            throw new AgentMemoryConfigurationException(
                $"Memory provider assignment '{entries[0].Scope}:{entries[0].Key}' is configured with {conflict}.");
        }

        return normalized;
    }

}
