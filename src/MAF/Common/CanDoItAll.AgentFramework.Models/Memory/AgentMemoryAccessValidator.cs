using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.AgentFramework.Models;

internal static class AgentMemoryAccessValidator
{
    public static void Validate(
        AgentMemoryAccessSettings settings,
        IReadOnlyList<MemoryProviderInstanceId> allowedProviders,
        IReadOnlyList<AgentMemoryProviderBindingSetting> bindings,
        MemoryProviderInstanceId? preferredProvider,
        MemoryProviderInstanceId? defaultProvider,
        IReadOnlyList<MemoryCapabilityId> allowedCapabilities,
        IReadOnlyList<MemoryCapabilityId> deniedCapabilities,
        IReadOnlyList<AgentMemoryProviderAssignmentSetting> assignments)
    {
        if (!Enum.IsDefined(settings.InvocationMode))
        {
            throw new AgentMemoryConfigurationException(
                $"Unsupported agent memory invocation mode '{settings.InvocationMode}'.");
        }

        var invalidRequirement = bindings.FirstOrDefault(binding => !Enum.IsDefined(binding.Requirement));
        if (invalidRequirement is not null)
        {
            throw new AgentMemoryConfigurationException(
                $"Unsupported requirement '{invalidRequirement.Requirement}' for memory provider alias '{invalidRequirement.Alias}'.");
        }

        var duplicateAlias = bindings
            .GroupBy(binding => binding.Alias.Value, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateAlias is not null)
        {
            throw new AgentMemoryConfigurationException(
                $"Memory provider alias '{duplicateAlias.Key}' is configured more than once.");
        }

        var duplicateProvider = bindings
            .GroupBy(binding => binding.ProviderInstanceId.Value, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateProvider is not null)
        {
            throw new AgentMemoryConfigurationException(
                $"Memory provider instance '{duplicateProvider.Key}' is bound more than once.");
        }

        var conflictingCapability = allowedCapabilities.FirstOrDefault(deniedCapabilities.Contains);
        if (!string.IsNullOrWhiteSpace(conflictingCapability.Value))
        {
            throw new AgentMemoryConfigurationException(
                $"Memory capability '{conflictingCapability}' cannot be both allowed and denied.");
        }

        if (settings.InvocationMode == AgentMemoryInvocationMode.Disabled &&
            settings.RequireContextContributions)
        {
            throw new AgentMemoryConfigurationException(
                "Required memory context cannot be enabled while memory invocation is disabled.");
        }

        foreach (var binding in bindings)
        {
            EnsureProviderIsAllowed(allowedProviders, binding.ProviderInstanceId, $"alias '{binding.Alias}'");
        }

        if (preferredProvider.HasValue)
        {
            EnsureProviderIsAllowed(allowedProviders, preferredProvider.Value, "preferred provider");
            EnsureProviderIsBound(bindings, preferredProvider.Value, "preferred provider");
        }

        if (defaultProvider.HasValue)
        {
            EnsureProviderIsAllowed(allowedProviders, defaultProvider.Value, "default provider");
            EnsureProviderIsBound(bindings, defaultProvider.Value, "default provider");
        }

        foreach (var assignment in assignments)
        {
            EnsureProviderIsAllowed(allowedProviders, assignment.ProviderInstanceId, "assignment");
            EnsureProviderIsBound(bindings, assignment.ProviderInstanceId, "assignment");
        }
    }

    private static void EnsureProviderIsAllowed(
        IReadOnlyList<MemoryProviderInstanceId> allowedProviders,
        MemoryProviderInstanceId provider,
        string usage)
    {
        if (allowedProviders.Count > 0 &&
            !allowedProviders.Any(allowed =>
                string.Equals(allowed.Value, provider.Value, StringComparison.OrdinalIgnoreCase)))
        {
            throw new AgentMemoryConfigurationException(
                $"Memory provider '{provider}' used by {usage} is outside the agent allowlist.");
        }
    }

    private static void EnsureProviderIsBound(
        IReadOnlyList<AgentMemoryProviderBindingSetting> bindings,
        MemoryProviderInstanceId provider,
        string usage)
    {
        if (!bindings.Any(binding => string.Equals(
                binding.ProviderInstanceId.Value,
                provider.Value,
                StringComparison.OrdinalIgnoreCase)))
        {
            throw new AgentMemoryConfigurationException(
                $"Memory provider '{provider}' used by {usage} is not bound to the agent.");
        }
    }
}
