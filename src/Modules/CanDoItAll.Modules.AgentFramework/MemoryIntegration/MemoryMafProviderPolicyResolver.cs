using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Modules.AgentFramework;

public sealed record MemoryMafProviderPolicyRequest(
    MemoryCapabilityId RequiredCapability,
    string? RequestedProviderInstanceId,
    string? PreferredProviderInstanceId,
    string? DefaultProviderInstanceId,
    IReadOnlyList<string> AllowedProviderInstanceIds,
    IReadOnlyList<MemoryCapabilityId> AllowedCapabilityIds,
    IReadOnlyList<MemoryCapabilityId> DeniedCapabilityIds,
    IReadOnlyList<MemoryProviderAssignment> ProviderAssignments,
    MemoryProviderInstanceId? MatchedAssignmentProvider,
    bool ProviderRequired,
    string CapabilityPolicyDescription,
    string ProviderPolicyDescription,
    string ProviderRequiredDiagnostic);

public sealed record MemoryMafProviderPolicyResolution(
    MemoryProviderSelectionPolicy SelectionPolicy,
    MemoryProviderInstanceId? ProviderForPayload,
    MemoryMafProviderPolicyRejection? Rejection)
{
    public static MemoryMafProviderPolicyResolution Selected(
        MemoryProviderSelectionPolicy selectionPolicy,
        MemoryProviderInstanceId? providerForPayload) =>
        new(selectionPolicy, providerForPayload, Rejection: null);

    public static MemoryMafProviderPolicyResolution Rejected(
        MemoryToolResultStatus status,
        string diagnostic) =>
        new(
            MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.ContextQuerySync),
            ProviderForPayload: null,
            new MemoryMafProviderPolicyRejection(status, diagnostic));
}

public sealed record MemoryMafProviderPolicyRejection(
    MemoryToolResultStatus Status,
    string Diagnostic);

public static class MemoryMafProviderPolicyResolver
{
    public static MemoryMafProviderPolicyResolution Resolve(MemoryMafProviderPolicyRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!IsCapabilityAllowed(request, request.RequiredCapability))
        {
            return MemoryMafProviderPolicyResolution.Rejected(
                MemoryToolResultStatus.CapabilityDenied,
                $"Memory capability '{request.RequiredCapability}' is outside {request.CapabilityPolicyDescription}.");
        }

        var explicitProvider = NormalizeProviderId(request.RequestedProviderInstanceId)
            ?? NormalizeProviderId(request.PreferredProviderInstanceId);
        var defaultProvider = NormalizeProviderId(request.DefaultProviderInstanceId);
        var providerForPayload = explicitProvider ?? request.MatchedAssignmentProvider ?? defaultProvider;

        if (providerForPayload is not null &&
            !IsProviderAllowed(request, providerForPayload.Value))
        {
            return MemoryMafProviderPolicyResolution.Rejected(
                MemoryToolResultStatus.ProviderDenied,
                $"Memory provider '{providerForPayload.Value}' is outside {request.ProviderPolicyDescription}.");
        }

        if (request.ProviderRequired &&
            providerForPayload is null)
        {
            return MemoryMafProviderPolicyResolution.Rejected(
                MemoryToolResultStatus.NoProviderConfigured,
                request.ProviderRequiredDiagnostic);
        }

        var assignments = request.ProviderAssignments
            .Where(assignment => IsProviderAllowed(request, assignment.ProviderInstanceId))
            .ToArray();
        var policy = new MemoryProviderSelectionPolicy(
            request.RequiredCapability,
            explicitProvider,
            defaultProvider,
            assignments,
            request.AllowedCapabilityIds.ToArray(),
            request.DeniedCapabilityIds.ToArray(),
            defaultProvider is null
                ? MemoryProviderFallbackBehavior.DenyImplicitFallback
                : MemoryProviderFallbackBehavior.AllowDefaultProviderWhenNoAssignment);
        return MemoryMafProviderPolicyResolution.Selected(policy, providerForPayload);
    }

    public static MemoryProviderInstanceId? NormalizeProviderId(string? providerId)
    {
        return string.IsNullOrWhiteSpace(providerId)
            ? null
            : MemoryProviderInstanceId.Parse(providerId.Trim());
    }

    public static IReadOnlyList<MemoryCapabilityId> ParseCapabilityIds(IReadOnlyList<string> capabilityIds)
    {
        return capabilityIds
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => MemoryCapabilityId.Parse(value.Trim()))
            .ToArray();
    }

    public static MemoryProviderAssignment ToProviderAssignment(AgentMemoryProviderAssignmentSetting assignment)
    {
        return new MemoryProviderAssignment(
            assignment.Scope,
            assignment.Key,
            MemoryProviderInstanceId.Parse(assignment.ProviderInstanceId));
    }

    public static MemoryProviderAssignment ToProviderAssignment(MemoryWorkflowProviderAssignmentSetting assignment)
    {
        return new MemoryProviderAssignment(
            assignment.Scope,
            assignment.Key,
            MemoryProviderInstanceId.Parse(assignment.ProviderInstanceId));
    }

    private static bool IsCapabilityAllowed(
        MemoryMafProviderPolicyRequest request,
        MemoryCapabilityId capability)
    {
        if (request.DeniedCapabilityIds.Contains(capability))
        {
            return false;
        }

        return request.AllowedCapabilityIds.Count == 0 ||
               request.AllowedCapabilityIds.Contains(capability);
    }

    private static bool IsProviderAllowed(
        MemoryMafProviderPolicyRequest request,
        MemoryProviderInstanceId providerInstanceId)
    {
        return request.AllowedProviderInstanceIds.Count == 0 ||
               request.AllowedProviderInstanceIds.Contains(providerInstanceId.Value, StringComparer.OrdinalIgnoreCase);
    }
}
