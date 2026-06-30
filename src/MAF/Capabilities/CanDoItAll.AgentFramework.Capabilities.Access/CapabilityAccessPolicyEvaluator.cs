using CanDoItAll.AgentFramework.Capabilities.Abstractions;

namespace CanDoItAll.AgentFramework.Capabilities.Access;

public sealed class CapabilityAccessPolicyEvaluator : ICapabilityAccessPolicyEvaluator
{
    public CapabilityAccessEvaluationResult Evaluate(CapabilityAccessEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var rules = context.Policies
            .SelectMany(policy => policy.Rules)
            .Where(rule => rule.Effect != CapabilityAccessEffect.Inherit)
            .ToArray();
        var required = context.RequiredCapabilities.ToHashSet();
        var allowed = new List<CapabilityExposureDescriptor>();
        var diagnostics = new List<SuppressedCapabilityDiagnostic>();

        foreach (var candidate in context.CandidateCapabilities)
        {
            if (candidate.AvailabilityState != CapabilityAvailabilityState.Available)
            {
                diagnostics.Add(CreateDiagnostic(
                    candidate.Identity,
                    null,
                    CapabilityDiagnosticCategory.CapabilityUnavailable,
                    $"Capability '{candidate.Identity.Key}' is {candidate.AvailabilityState} and cannot be attached.",
                    "Repair or retire the capability template/setup state before assigning it.",
                    context.CorrelationId));
                continue;
            }

            var denyRule = SelectWinningDenyRule(candidate, rules);
            if (denyRule is not null)
            {
                diagnostics.Add(CreateDiagnostic(
                    candidate.Identity,
                    denyRule,
                    required.Contains(candidate.Identity)
                        ? CapabilityDiagnosticCategory.RequiredCapabilityDenied
                        : CapabilityDiagnosticCategory.AccessPolicy,
                    denyRule.Reason,
                    required.Contains(candidate.Identity)
                        ? "A required capability was denied by policy. Remove the deny rule or restaff the agent/process so the requirement is not declared."
                        : "Edit the capability access policy if this capability should remain available in this scope.",
                    context.CorrelationId));
                continue;
            }

            allowed.Add(candidate);
        }

        foreach (var requirement in required)
        {
            if (allowed.Any(candidate => candidate.Identity == requirement) ||
                diagnostics.Any(diagnostic => diagnostic.Identity == requirement))
            {
                continue;
            }

            diagnostics.Add(new SuppressedCapabilityDiagnostic(
                requirement,
                null,
                null,
                null,
                CapabilityDiagnosticCategory.RequiredCapabilityDenied,
                $"Required capability '{requirement.Key}' is not present in the candidate set.",
                "Assign or enable the required capability before runtime composition.",
                context.CorrelationId));
        }

        foreach (var requireRule in rules.Where(rule => rule.Effect == CapabilityAccessEffect.Require))
        {
            if (allowed.Any(candidate => Matches(candidate, requireRule.Selector)))
            {
                continue;
            }

            diagnostics.Add(new SuppressedCapabilityDiagnostic(
                new CapabilityIdentity(CapabilityKind.Tool, CapabilityKey.Create("required-capability-missing")),
                requireRule.Id,
                requireRule.Scope,
                requireRule.Selector.Kind,
                CapabilityDiagnosticCategory.RequiredCapabilityDenied,
                requireRule.Reason,
                "A require rule matched no allowed candidate. Assign the capability or loosen the selector.",
                context.CorrelationId));
        }

        return new CapabilityAccessEvaluationResult(allowed, diagnostics);
    }

    private static CapabilityAccessRule? SelectWinningDenyRule(
        CapabilityExposureDescriptor candidate,
        IReadOnlyList<CapabilityAccessRule> rules)
    {
        return rules
            .Where(rule => rule.Effect == CapabilityAccessEffect.Deny && Matches(candidate, rule.Selector))
            .OrderBy(rule => ResolveScopePriority(rule.Scope))
            .FirstOrDefault();
    }

    private static int ResolveScopePriority(CapabilityAccessScope scope)
    {
        return scope switch
        {
            CapabilityAccessScope.System => 0,
            CapabilityAccessScope.AgentDefault => 1,
            CapabilityAccessScope.WorkflowDefinition => 2,
            CapabilityAccessScope.WorkflowNode => 3,
            CapabilityAccessScope.ProcessDefinition => 4,
            CapabilityAccessScope.ProcessStep => 5,
            CapabilityAccessScope.RuntimeOverride => 6,
            CapabilityAccessScope.UiPreview => 7,
            _ => 100
        };
    }

    private static bool Matches(CapabilityExposureDescriptor candidate, CapabilitySelector selector)
    {
        return selector.Kind switch
        {
            CapabilitySelectorKind.All => true,
            CapabilitySelectorKind.Kind => selector.CapabilityKind == candidate.Identity.Kind,
            CapabilitySelectorKind.CapabilityKey => selector.CapabilityKey == candidate.Identity.Key,
            CapabilitySelectorKind.Tag => selector.Tag is not null && candidate.Tags.Contains(selector.Tag.Value),
            CapabilitySelectorKind.OperationClassification => selector.OperationClassification is not null &&
                                                              candidate.OperationClassifications.Contains(selector.OperationClassification.Value),
            CapabilitySelectorKind.RuntimeToolName => selector.RuntimeToolName is not null &&
                                                      selector.RuntimeToolName == candidate.RuntimeToolName,
            CapabilitySelectorKind.McpServerKey => selector.McpServerKey is not null &&
                                                   selector.McpServerKey == candidate.McpServerKey,
            CapabilitySelectorKind.McpToolName => selector.McpServerKey is not null &&
                                                  selector.McpToolName is not null &&
                                                  selector.McpServerKey == candidate.McpServerKey &&
                                                  selector.McpToolName == candidate.McpToolName,
            CapabilitySelectorKind.ImplementationKey => selector.ImplementationKey is not null &&
                                                        selector.ImplementationKey == candidate.ImplementationKey,
            _ => false
        };
    }

    private static SuppressedCapabilityDiagnostic CreateDiagnostic(
        CapabilityIdentity identity,
        CapabilityAccessRule? rule,
        CapabilityDiagnosticCategory category,
        string reason,
        string repairHint,
        string correlationId)
    {
        return new SuppressedCapabilityDiagnostic(
            identity,
            rule?.Id,
            rule?.Scope,
            rule?.Selector.Kind,
            category,
            reason,
            repairHint,
            correlationId);
    }
}
