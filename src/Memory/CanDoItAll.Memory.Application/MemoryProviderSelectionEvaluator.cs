using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

internal sealed class MemoryProviderSelectionEvaluator(
    IReadOnlyList<MemoryProviderProfile> providers)
{
    public MemoryProviderSelectionResult Evaluate(
        MemoryProviderInstanceId providerId,
        MemoryProviderSelectionReason reason,
        MemoryProviderSelectionPolicy policy)
    {
        if (!MemoryProviderSelectionRules.IsProviderAllowed(policy, providerId))
        {
            return Reject(
                MemoryProviderSelectionStatus.ProviderDenied,
                reason,
                policy,
                $"Memory provider '{providerId}' is excluded by the allowed-provider policy; dispatch is not allowed.",
                providerId);
        }

        var provider = providers.FirstOrDefault(candidate => candidate.InstanceId == providerId);
        if (provider is null)
        {
            return Reject(
                MemoryProviderSelectionStatus.ProviderNotFound,
                reason,
                policy,
                $"Memory provider '{providerId}' was not found; dispatch is not allowed.",
                providerId);
        }

        if (!provider.IsEnabled)
        {
            return Reject(
                MemoryProviderSelectionStatus.ProviderDisabled,
                reason,
                policy,
                $"Memory provider '{providerId}' is disabled; implicit fallback is denied.",
                providerId);
        }

        if (reason == MemoryProviderSelectionReason.DefaultProvider &&
            provider.DefaultPolicy.FallbackBehavior != MemoryProviderFallbackBehavior.AllowDefaultProviderWhenNoAssignment)
        {
            return Reject(
                MemoryProviderSelectionStatus.ProviderDenied,
                reason,
                policy,
                $"Memory provider '{providerId}' does not allow selection as an implicit default; dispatch is not allowed.",
                providerId);
        }

        if (!MemoryProviderSelectionRules.SupportsWorkspaceScope(provider))
        {
            return Reject(
                MemoryProviderSelectionStatus.ProviderDenied,
                reason,
                policy,
                $"Memory provider '{providerId}' uses a single-workspace scope that cannot be validated; dispatch is not allowed.",
                providerId);
        }

        if (!MemoryProviderSelectionRules.SupportsCapability(provider, policy.RequiredCapability))
        {
            return Reject(
                MemoryProviderSelectionStatus.CapabilityUnavailable,
                reason,
                policy,
                $"Memory provider '{providerId}' does not support capability '{policy.RequiredCapability}'; dispatch is not allowed.",
                providerId);
        }

        return MemoryProviderSelectionResult.Selected(provider, reason, policy.RequiredCapability);
    }

    private static MemoryProviderSelectionResult Reject(
        MemoryProviderSelectionStatus status,
        MemoryProviderSelectionReason reason,
        MemoryProviderSelectionPolicy policy,
        string diagnostic,
        MemoryProviderInstanceId providerId) =>
        MemoryProviderSelectionResult.Rejected(
            status,
            reason,
            policy.RequiredCapability,
            diagnostic,
            [providerId]);
}
