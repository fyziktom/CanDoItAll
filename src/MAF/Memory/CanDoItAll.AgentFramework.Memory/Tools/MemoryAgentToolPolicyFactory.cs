using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;

namespace CanDoItAll.AgentFramework.Memory.Tools;

internal sealed record MemoryAgentToolPolicyContext(
    MemoryMafProviderPolicyResolution Resolution,
    MemoryLedgerRequester Requester,
    MemoryRequestContext RequestContext);

internal static class MemoryAgentToolPolicyFactory
{
    public static MemoryAgentToolPolicyContext Resolve(
        AgentRuntimeToolProviderContext context,
        AgentMemoryAccessSettings access,
        MemoryCapabilityId requiredCapability,
        string? requestedProviderAliasOrId,
        bool providerRequired)
    {
        var requestedProvider = ResolveRequestedProvider(
            access,
            requestedProviderAliasOrId,
            out var requestedProviderDiagnostic);
        if (requestedProviderDiagnostic is not null)
        {
            return new MemoryAgentToolPolicyContext(
                MemoryMafProviderPolicyResolution.Rejected(
                    MemoryToolResultStatus.ProviderDenied,
                    requestedProviderDiagnostic),
                MemoryAgentRuntimeContextFactory.CreateRequester(
                    context.Agent,
                    context.ContextIntent,
                    context.RuntimeSessionKey),
                MemoryAgentRuntimeContextFactory.CreateRequestContext(
                    context.ContextIntent.WorkspaceScope ?? WorkspaceScopeDescriptor.Sandbox,
                    context.ContextIntent,
                    access));
        }

        var matchedAssignment = MemoryAgentRuntimeContextFactory.ResolveAssignmentProvider(
            context.Agent,
            context.ContextIntent,
            access);
        if (requestedProvider is null &&
            access.PreferredProviderInstanceId is null &&
            matchedAssignment is null &&
            access.DefaultProviderInstanceId is null &&
            access.ProviderBindings.Count == 1)
        {
            requestedProvider = access.ProviderBindings[0].ProviderInstanceId;
        }

        var boundProviders = access.ProviderBindings
            .Select(binding => binding.ProviderInstanceId)
            .ToArray();
        var effectiveAllowedProviders = access.AllowedProviderInstanceIds.Count > 0
            ? boundProviders.Where(bound => access.AllowedProviderInstanceIds.Any(allowed =>
                string.Equals(allowed.Value, bound.Value, StringComparison.OrdinalIgnoreCase))).ToArray()
            : boundProviders;
        var resolution = boundProviders.Length == 0
            ? MemoryMafProviderPolicyResolution.Rejected(
                MemoryToolResultStatus.NoProviderConfigured,
                "This memory tool requires at least one bound provider.")
            : access.AllowedProviderInstanceIds.Count > 0 && effectiveAllowedProviders.Length == 0
                ? MemoryMafProviderPolicyResolution.Rejected(
                    MemoryToolResultStatus.ProviderDenied,
                    "No bound memory provider is inside the agent's provider allowlist.")
                : MemoryMafProviderPolicyResolver.Resolve(new MemoryMafProviderPolicyRequest(
                    requiredCapability,
                    requestedProvider,
                    access.PreferredProviderInstanceId,
                    access.DefaultProviderInstanceId,
                    effectiveAllowedProviders,
                    access.AllowedCapabilityIds,
                    access.DeniedCapabilityIds,
                    access.ProviderAssignments.Select(MemoryMafProviderPolicyResolver.ToProviderAssignment).ToArray(),
                    matchedAssignment,
                    providerRequired,
                    "the agent's allowed memory capability policy",
                    "the agent's bound memory provider policy",
                    access.ProviderBindings.Count > 1
                        ? "This memory tool requires a configured provider alias because the agent has multiple providers."
                        : "This memory tool requires a configured provider."));
        return new MemoryAgentToolPolicyContext(
            resolution,
            MemoryAgentRuntimeContextFactory.CreateRequester(
                context.Agent,
                context.ContextIntent,
                context.RuntimeSessionKey),
            MemoryAgentRuntimeContextFactory.CreateRequestContext(
                context.ContextIntent.WorkspaceScope ?? WorkspaceScopeDescriptor.Sandbox,
                context.ContextIntent,
                access));
    }

    public static MemoryOperationCaller CreateCaller(
        MemoryAgentToolPolicyContext context,
        string route)
    {
        return MemoryOperationCaller.Tool(route, context.Requester);
    }

    private static MemoryProviderInstanceId? ResolveRequestedProvider(
        AgentMemoryAccessSettings access,
        string? requestedProviderAliasOrId,
        out string? diagnostic)
    {
        diagnostic = null;
        if (string.IsNullOrWhiteSpace(requestedProviderAliasOrId))
        {
            return null;
        }

        var requested = requestedProviderAliasOrId.Trim();
        var binding = access.ProviderBindings.FirstOrDefault(item =>
            string.Equals(item.Alias.Value, requested, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(item.ProviderInstanceId.Value, requested, StringComparison.OrdinalIgnoreCase));
        if (binding is not null)
        {
            return binding.ProviderInstanceId;
        }

        diagnostic = $"Memory provider alias or id '{requested}' is not bound to this agent.";
        return null;
    }
}
