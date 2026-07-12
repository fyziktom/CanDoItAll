using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

public sealed class InMemoryMemoryProviderRegistry : IMemoryProviderRegistry
{
    private readonly IReadOnlyList<MemoryProviderProfile> providers;
    private readonly MemoryProviderSelectionEvaluator evaluator;

    public InMemoryMemoryProviderRegistry(IReadOnlyList<MemoryProviderProfile> providers)
    {
        this.providers = providers.ToArray();
        evaluator = new MemoryProviderSelectionEvaluator(this.providers);
    }

    public IReadOnlyList<MemoryProviderProfile> Providers => providers;

    public IReadOnlyList<MemoryProviderProfile> GetEnabledProviders() =>
        providers.Where(provider => provider.IsEnabled).ToArray();

    public IReadOnlyList<MemoryProviderProfile> GetProvidersForCapability(MemoryCapabilityId capability) =>
        GetEnabledProviders()
            .Where(provider =>
                MemoryProviderSelectionRules.SupportsWorkspaceScope(provider) &&
                MemoryProviderSelectionRules.SupportsCapability(provider, capability))
            .ToArray();

    public MemoryProviderSelectionResult SelectProvider(
        MemoryProviderSelectionPolicy policy,
        MemoryProviderSelectionContext context)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(context);
        if (policy.DeniedCapabilities.Contains(policy.RequiredCapability) ||
            (policy.AllowedCapabilities.Count > 0 &&
             !policy.AllowedCapabilities.Contains(policy.RequiredCapability)))
        {
            return MemoryProviderSelectionResult.Rejected(
                MemoryProviderSelectionStatus.CapabilityDenied,
                MemoryProviderSelectionReason.None,
                policy.RequiredCapability,
                $"Capability '{policy.RequiredCapability}' is not allowed by memory provider selection policy.",
                []);
        }

        if (providers.Count == 0)
        {
            return MemoryProviderSelectionResult.Rejected(
                MemoryProviderSelectionStatus.NoProviderConfigured,
                MemoryProviderSelectionReason.None,
                policy.RequiredCapability,
                "No memory provider is configured; dispatch is not allowed.",
                []);
        }

        if (policy.ExplicitProviderId is { } explicitProviderId)
        {
            return evaluator.Evaluate(
                explicitProviderId,
                MemoryProviderSelectionReason.ExplicitProvider,
                policy);
        }

        if (MemoryProviderAssignmentResolver.TryResolve(policy, context, out var assignedProviderId))
        {
            return evaluator.Evaluate(
                assignedProviderId,
                MemoryProviderSelectionReason.AssignmentOverride,
                policy);
        }

        if (policy.DefaultProviderId is { } defaultProviderId)
        {
            if (policy.FallbackBehavior != MemoryProviderFallbackBehavior.AllowDefaultProviderWhenNoAssignment)
            {
                return MemoryProviderSelectionResult.Rejected(
                    MemoryProviderSelectionStatus.ProviderSelectionRequired,
                    MemoryProviderSelectionReason.DefaultProvider,
                    policy.RequiredCapability,
                    "A default memory provider was configured, but implicit default fallback is denied by policy.",
                    [defaultProviderId]);
            }

            return evaluator.Evaluate(
                defaultProviderId,
                MemoryProviderSelectionReason.DefaultProvider,
                policy);
        }

        return EvaluateUnassignedProvider(policy);
    }

    private MemoryProviderSelectionResult EvaluateUnassignedProvider(
        MemoryProviderSelectionPolicy policy)
    {
        var enabledProviders = GetEnabledProviders();
        if (enabledProviders.Count == 0)
        {
            return MemoryProviderSelectionResult.Rejected(
                MemoryProviderSelectionStatus.NoEnabledProvider,
                MemoryProviderSelectionReason.None,
                policy.RequiredCapability,
                "No enabled memory provider is configured; dispatch is not allowed.",
                providers.Select(provider => provider.InstanceId).ToArray());
        }

        var matchingProviders = enabledProviders
            .Where(provider => MemoryProviderSelectionRules.SupportsCapability(provider, policy.RequiredCapability))
            .ToArray();
        if (matchingProviders.Length == 0)
        {
            return MemoryProviderSelectionResult.Rejected(
                MemoryProviderSelectionStatus.CapabilityUnavailable,
                MemoryProviderSelectionReason.None,
                policy.RequiredCapability,
                $"No enabled memory provider supports capability '{policy.RequiredCapability}'.",
                enabledProviders.Select(provider => provider.InstanceId).ToArray());
        }

        var scopedProviders = matchingProviders
            .Where(MemoryProviderSelectionRules.SupportsWorkspaceScope)
            .ToArray();
        if (scopedProviders.Length == 0)
        {
            return MemoryProviderSelectionResult.Rejected(
                MemoryProviderSelectionStatus.ProviderDenied,
                MemoryProviderSelectionReason.None,
                policy.RequiredCapability,
                "Compatible memory providers use a single-workspace scope that cannot be validated; dispatch is not allowed.",
                matchingProviders.Select(provider => provider.InstanceId).ToArray());
        }

        var allowedProviders = scopedProviders
            .Where(provider => MemoryProviderSelectionRules.IsProviderAllowed(policy, provider.InstanceId))
            .ToArray();
        if (allowedProviders.Length == 0)
        {
            return MemoryProviderSelectionResult.Rejected(
                MemoryProviderSelectionStatus.ProviderDenied,
                MemoryProviderSelectionReason.None,
                policy.RequiredCapability,
                "Compatible memory providers are excluded by the allowed-provider policy; dispatch is not allowed.",
                scopedProviders.Select(provider => provider.InstanceId).ToArray());
        }

        return MemoryProviderSelectionResult.Rejected(
            MemoryProviderSelectionStatus.ProviderSelectionRequired,
            MemoryProviderSelectionReason.None,
            policy.RequiredCapability,
            "A compatible memory provider exists, but no explicit provider, assignment, or default provider was selected; dispatch is not allowed.",
            allowedProviders.Select(provider => provider.InstanceId).ToArray());
    }
}
