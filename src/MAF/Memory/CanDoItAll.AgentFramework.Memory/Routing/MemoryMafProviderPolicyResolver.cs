using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.AgentFramework.Memory;

public sealed record MemoryMafProviderPolicyRequest(
    MemoryCapabilityId RequiredCapability,
    MemoryProviderInstanceId? RequestedProviderInstanceId,
    MemoryProviderInstanceId? PreferredProviderInstanceId,
    MemoryProviderInstanceId? DefaultProviderInstanceId,
    IReadOnlyList<MemoryProviderInstanceId> AllowedProviderInstanceIds,
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

        var explicitProvider = request.RequestedProviderInstanceId ?? request.PreferredProviderInstanceId;
        var providerForPayload = explicitProvider ?? request.MatchedAssignmentProvider ?? request.DefaultProviderInstanceId;
        if (providerForPayload is { } selectedProvider && !IsProviderAllowed(request, selectedProvider))
        {
            return MemoryMafProviderPolicyResolution.Rejected(
                MemoryToolResultStatus.ProviderDenied,
                $"Memory provider '{selectedProvider}' is outside {request.ProviderPolicyDescription}.");
        }

        if (request.ProviderRequired && providerForPayload is null)
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
            request.DefaultProviderInstanceId,
            assignments,
            request.AllowedCapabilityIds.ToArray(),
            request.DeniedCapabilityIds.ToArray(),
            explicitProvider is not null || request.DefaultProviderInstanceId is null
                ? MemoryProviderFallbackBehavior.DenyImplicitFallback
                : MemoryProviderFallbackBehavior.AllowDefaultProviderWhenNoAssignment)
        {
            AllowedProviderIds = request.AllowedProviderInstanceIds
        };
        return MemoryMafProviderPolicyResolution.Selected(policy, providerForPayload);
    }

    public static MemoryProviderInstanceId? ParseOptionalProviderId(string? providerId)
    {
        return string.IsNullOrWhiteSpace(providerId)
            ? null
            : MemoryProviderInstanceId.Parse(providerId.Trim());
    }

    public static IReadOnlyList<MemoryCapabilityId> ParseCapabilityIds(IReadOnlyList<string> capabilityIds)
    {
        return capabilityIds
            .Select(value => string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("Memory capability ids cannot be empty.", nameof(capabilityIds))
                : MemoryCapabilityId.Parse(value.Trim()))
            .ToArray();
    }

    public static MemoryProviderAssignment ToProviderAssignment(AgentMemoryProviderAssignmentSetting assignment)
    {
        return new MemoryProviderAssignment(
            assignment.Scope,
            assignment.Key,
            assignment.ProviderInstanceId);
    }

    private static bool IsCapabilityAllowed(
        MemoryMafProviderPolicyRequest request,
        MemoryCapabilityId capability)
    {
        return !request.DeniedCapabilityIds.Contains(capability) &&
               (request.AllowedCapabilityIds.Count == 0 || request.AllowedCapabilityIds.Contains(capability));
    }

    private static bool IsProviderAllowed(
        MemoryMafProviderPolicyRequest request,
        MemoryProviderInstanceId providerInstanceId)
    {
        return request.AllowedProviderInstanceIds.Count == 0 ||
               request.AllowedProviderInstanceIds.Any(allowed =>
                   string.Equals(allowed.Value, providerInstanceId.Value, StringComparison.OrdinalIgnoreCase));
    }
}
