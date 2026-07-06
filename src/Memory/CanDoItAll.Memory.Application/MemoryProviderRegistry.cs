using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

public interface IMemoryProviderRegistry
{
    IReadOnlyList<MemoryProviderProfile> Providers { get; }

    IReadOnlyList<MemoryProviderProfile> GetEnabledProviders();

    IReadOnlyList<MemoryProviderProfile> GetProvidersForCapability(MemoryCapabilityId capability);

    MemoryProviderSelectionResult SelectProvider(
        MemoryProviderSelectionPolicy policy,
        MemoryProviderSelectionContext context);
}

public sealed class InMemoryMemoryProviderRegistry : IMemoryProviderRegistry
{
    private readonly IReadOnlyList<MemoryProviderProfile> providers;

    public InMemoryMemoryProviderRegistry(IReadOnlyList<MemoryProviderProfile> providers)
    {
        this.providers = providers.ToArray();
    }

    public IReadOnlyList<MemoryProviderProfile> Providers => providers;

    public IReadOnlyList<MemoryProviderProfile> GetEnabledProviders() =>
        providers.Where(provider => provider.IsEnabled).ToArray();

    public IReadOnlyList<MemoryProviderProfile> GetProvidersForCapability(MemoryCapabilityId capability) =>
        GetEnabledProviders()
            .Where(provider => SupportsCapability(provider, capability))
            .ToArray();

    public MemoryProviderSelectionResult SelectProvider(
        MemoryProviderSelectionPolicy policy,
        MemoryProviderSelectionContext context)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(context);

        if (policy.DeniedCapabilities.Contains(policy.RequiredCapability))
        {
            return MemoryProviderSelectionResult.Rejected(
                MemoryProviderSelectionStatus.CapabilityDenied,
                MemoryProviderSelectionReason.None,
                policy.RequiredCapability,
                $"Capability '{policy.RequiredCapability}' is denied by memory provider selection policy.",
                []);
        }

        if (policy.AllowedCapabilities.Count > 0 &&
            !policy.AllowedCapabilities.Contains(policy.RequiredCapability))
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
            return EvaluateSelectedProvider(
                explicitProviderId,
                MemoryProviderSelectionReason.ExplicitProvider,
                policy.RequiredCapability);
        }

        if (TryResolveAssignment(policy, context, out var assignedProviderId))
        {
            return EvaluateSelectedProvider(
                assignedProviderId,
                MemoryProviderSelectionReason.AssignmentOverride,
                policy.RequiredCapability);
        }

        if (policy.DefaultProviderId is { } defaultProviderId)
        {
            return EvaluateSelectedProvider(
                defaultProviderId,
                MemoryProviderSelectionReason.DefaultProvider,
                policy.RequiredCapability);
        }

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

        var matchingProvider = enabledProviders.FirstOrDefault(provider => SupportsCapability(provider, policy.RequiredCapability));
        if (matchingProvider is null)
        {
            return MemoryProviderSelectionResult.Rejected(
                MemoryProviderSelectionStatus.CapabilityUnavailable,
                MemoryProviderSelectionReason.None,
                policy.RequiredCapability,
                $"No enabled memory provider supports capability '{policy.RequiredCapability}'.",
                enabledProviders.Select(provider => provider.InstanceId).ToArray());
        }

        return MemoryProviderSelectionResult.Selected(
            matchingProvider,
            MemoryProviderSelectionReason.DefaultProvider,
            policy.RequiredCapability);
    }

    private MemoryProviderSelectionResult EvaluateSelectedProvider(
        MemoryProviderInstanceId providerId,
        MemoryProviderSelectionReason reason,
        MemoryCapabilityId requiredCapability)
    {
        var provider = providers.FirstOrDefault(candidate => candidate.InstanceId == providerId);
        if (provider is null)
        {
            return MemoryProviderSelectionResult.Rejected(
                MemoryProviderSelectionStatus.ProviderNotFound,
                reason,
                requiredCapability,
                $"Memory provider '{providerId}' was not found; dispatch is not allowed.",
                [providerId]);
        }

        if (!provider.IsEnabled)
        {
            return MemoryProviderSelectionResult.Rejected(
                MemoryProviderSelectionStatus.ProviderDisabled,
                reason,
                requiredCapability,
                $"Memory provider '{providerId}' is disabled; implicit fallback is denied.",
                [providerId]);
        }

        if (!SupportsCapability(provider, requiredCapability))
        {
            return MemoryProviderSelectionResult.Rejected(
                MemoryProviderSelectionStatus.CapabilityUnavailable,
                reason,
                requiredCapability,
                $"Memory provider '{providerId}' does not support capability '{requiredCapability}'; dispatch is not allowed.",
                [providerId]);
        }

        return MemoryProviderSelectionResult.Selected(provider, reason, requiredCapability);
    }

    private static bool SupportsCapability(
        MemoryProviderProfile provider,
        MemoryCapabilityId capability)
    {
        return provider.Manifest.Capabilities.Any(candidate => candidate.Supported && candidate.Id == capability);
    }

    private static bool TryResolveAssignment(
        MemoryProviderSelectionPolicy policy,
        MemoryProviderSelectionContext context,
        out MemoryProviderInstanceId providerId)
    {
        foreach (var assignment in policy.Assignments)
        {
            if (AssignmentMatches(assignment, context))
            {
                providerId = assignment.ProviderInstanceId;
                return true;
            }
        }

        providerId = default;
        return false;
    }

    private static bool AssignmentMatches(
        MemoryProviderAssignment assignment,
        MemoryProviderSelectionContext context)
    {
        return assignment.Scope switch
        {
            MemoryProviderAssignmentScope.Agent => Matches(context.AgentId, assignment.Key),
            MemoryProviderAssignmentScope.AgentRole => Matches(context.AgentRole, assignment.Key),
            MemoryProviderAssignmentScope.Workflow => Matches(context.WorkflowId, assignment.Key),
            MemoryProviderAssignmentScope.WorkflowNode => Matches(context.WorkflowNodeId, assignment.Key),
            MemoryProviderAssignmentScope.Process => Matches(context.ProcessId, assignment.Key),
            _ => false
        };
    }

    private static bool Matches(
        string? actual,
        string expected)
    {
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }
}
