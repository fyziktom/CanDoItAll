using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.Processes.Contracts;
using AgentRuntimeCapabilityScopeOverride = CanDoItAll.AgentFramework.Models.AgentRuntimeCapabilityScopeOverride;
using AgentRuntimeRequiredToolReceipt = CanDoItAll.AgentFramework.Models.AgentRuntimeRequiredToolReceipt;
using AgentRuntimeRequiredToolReceiptActivation = CanDoItAll.AgentFramework.Models.AgentRuntimeRequiredToolReceiptActivation;
using AgentRuntimeRequiredToolReceiptKind = CanDoItAll.AgentFramework.Models.AgentRuntimeRequiredToolReceiptKind;

namespace CanDoItAll.Modules.Processes;

internal static class AgentFrameworkProcessCapabilityScopeTranslator
{
    private const string DefaultRuleReason = "Process step capability scope directive.";
    private const string AllowOnlyDefaultReason = "Capability is outside this process step allow-only capability scope.";
    private const string MissingValueMessage = "Capability scope target value is required.";

    public static AgentRuntimeCapabilityScopeOverride Translate(ProcessCapabilityScope? scope)
    {
        var normalized = ProcessCapabilityScope.Normalize(scope);
        if (normalized.Directives.Count == 0 && normalized.RequiredReceipts.Count == 0)
        {
            return AgentRuntimeCapabilityScopeOverride.Empty;
        }

        var hasAllowOnlyDirective = normalized.Directives.Any(directive =>
            directive.Kind == ProcessCapabilityScopeDirectiveKind.AllowOnly);
        var rules = new List<CapabilityAccessRule>(normalized.Directives.Count);
        var requiredCapabilities = new List<CapabilityIdentity>();
        var requiredReceipts = normalized.RequiredReceipts
            .Select(CreateRequiredReceipt)
            .ToArray();

        for (var index = 0; index < normalized.Directives.Count; index++)
        {
            var directive = normalized.Directives[index];
            var selector = CreateSelector(directive.Target);
            var effect = directive.Kind switch
            {
                ProcessCapabilityScopeDirectiveKind.Allow or ProcessCapabilityScopeDirectiveKind.AllowOnly => CapabilityAccessEffect.Allow,
                ProcessCapabilityScopeDirectiveKind.Deny => CapabilityAccessEffect.Deny,
                ProcessCapabilityScopeDirectiveKind.Require => CapabilityAccessEffect.Require,
                _ => throw new InvalidOperationException($"Unsupported process capability scope directive kind '{directive.Kind}'.")
            };

            rules.Add(new CapabilityAccessRule(
                CapabilityRuleId.Create($"process-scope-{index + 1}"),
                effect,
                CapabilityAccessScope.ProcessStep,
                selector,
                string.IsNullOrWhiteSpace(directive.Reason)
                    ? DefaultRuleReason
                    : directive.Reason.Trim()));

            if (directive.Kind == ProcessCapabilityScopeDirectiveKind.Require &&
                TryCreateRequiredCapability(directive.Target, out var requiredCapability))
            {
                requiredCapabilities.Add(requiredCapability);
            }
        }

        IReadOnlyList<CapabilityAccessPolicy> policies = rules.Count == 0
            ? []
            :
            [
                new CapabilityAccessPolicy(
                    rules,
                    hasAllowOnlyDirective ? CapabilityAccessDefaultEffect.DenyAll : CapabilityAccessDefaultEffect.Inherit,
                    CapabilityAccessScope.ProcessStep,
                    hasAllowOnlyDirective ? AllowOnlyDefaultReason : string.Empty)
            ];
        return new AgentRuntimeCapabilityScopeOverride(
            policies,
            requiredCapabilities
                .Distinct()
                .ToArray(),
            requiredReceipts);
    }

    private static AgentRuntimeRequiredToolReceipt CreateRequiredReceipt(ProcessRequiredToolReceipt receipt)
    {
        return new AgentRuntimeRequiredToolReceipt(
            receipt.Key,
            receipt.Kind switch
            {
                ProcessRequiredToolReceiptKind.RuntimeToolName => AgentRuntimeRequiredToolReceiptKind.RuntimeToolName,
                ProcessRequiredToolReceiptKind.RuntimeToolProviderKey => AgentRuntimeRequiredToolReceiptKind.RuntimeToolProviderKey,
                ProcessRequiredToolReceiptKind.RuntimeToolNameWithProvider => AgentRuntimeRequiredToolReceiptKind.RuntimeToolNameWithProvider,
                ProcessRequiredToolReceiptKind.McpToolName => AgentRuntimeRequiredToolReceiptKind.McpToolName,
                _ => throw new InvalidOperationException($"Unsupported required receipt kind '{receipt.Kind}'.")
            },
            receipt.ToolName,
            receipt.RuntimeToolProviderKey,
            receipt.McpServerKey,
            receipt.MinimumCount,
            receipt.RequireSuccessfulExit,
            receipt.RequireCurrentRun,
            receipt.Activation switch
            {
                ProcessRequiredToolReceiptActivation.Always => AgentRuntimeRequiredToolReceiptActivation.Always,
                ProcessRequiredToolReceiptActivation.WhenLaunchContextDeclaresTool => AgentRuntimeRequiredToolReceiptActivation.WhenLaunchContextDeclaresTool,
                _ => throw new InvalidOperationException($"Unsupported required receipt activation '{receipt.Activation}'.")
            },
            receipt.Reason);
    }

    private static CapabilitySelector CreateSelector(ProcessCapabilityScopeTarget target)
    {
        return target.Kind switch
        {
            ProcessCapabilityScopeTargetKind.All => CapabilitySelector.All,
            ProcessCapabilityScopeTargetKind.CapabilityKind => CapabilitySelector.ByKind(ReadEnum<CapabilityKind>(target.Value, target.Kind)),
            ProcessCapabilityScopeTargetKind.CapabilityKey => CapabilitySelector.ByCapabilityKey(CapabilityKey.Create(RequireValue(target))),
            ProcessCapabilityScopeTargetKind.CapabilityIdentity => CapabilitySelector.ByCapabilityKey(CreateCapabilityIdentity(target).Key),
            ProcessCapabilityScopeTargetKind.CapabilityTag => CapabilitySelector.ByTag(CapabilityTag.Create(RequireValue(target))),
            ProcessCapabilityScopeTargetKind.RuntimeToolName => CapabilitySelector.ByRuntimeToolName(RuntimeToolName.Create(RequireValue(target))),
            ProcessCapabilityScopeTargetKind.RuntimeToolProviderKey => CapabilitySelector.ByTag(RuntimeToolProviderCapabilityTags.CreateProviderKeyTag(RequireValue(target))),
            ProcessCapabilityScopeTargetKind.McpServerKey => CapabilitySelector.ByMcpServerKey(McpServerKey.Create(RequireValue(target))),
            ProcessCapabilityScopeTargetKind.McpToolName => CapabilitySelector.ByMcpToolName(
                McpServerKey.Create(RequireValue(target)),
                McpToolName.Create(RequireSecondaryValue(target))),
            ProcessCapabilityScopeTargetKind.ImplementationKey => CapabilitySelector.ByImplementationKey(ImplementationKey.Create(RequireValue(target))),
            ProcessCapabilityScopeTargetKind.OperationClassification => CapabilitySelector.ByOperationClassification(ReadEnum<CapabilityOperationClassification>(target.Value, target.Kind)),
            ProcessCapabilityScopeTargetKind.Unspecified => throw new InvalidOperationException("Capability scope target kind is required."),
            _ => throw new InvalidOperationException($"Unsupported process capability scope target kind '{target.Kind}'.")
        };
    }

    private static bool TryCreateRequiredCapability(
        ProcessCapabilityScopeTarget target,
        out CapabilityIdentity identity)
    {
        if (target.Kind == ProcessCapabilityScopeTargetKind.CapabilityIdentity)
        {
            identity = CreateCapabilityIdentity(target);
            return true;
        }

        identity = default!;
        return false;
    }

    private static CapabilityIdentity CreateCapabilityIdentity(ProcessCapabilityScopeTarget target)
    {
        return new CapabilityIdentity(
            ReadEnum<CapabilityKind>(target.Value, target.Kind),
            CapabilityKey.Create(RequireSecondaryValue(target)));
    }

    private static string RequireValue(ProcessCapabilityScopeTarget target)
    {
        if (string.IsNullOrWhiteSpace(target.Value))
        {
            throw new InvalidOperationException(MissingValueMessage);
        }

        return target.Value.Trim();
    }

    private static string RequireSecondaryValue(ProcessCapabilityScopeTarget target)
    {
        if (string.IsNullOrWhiteSpace(target.SecondaryValue))
        {
            throw new InvalidOperationException("Capability scope target secondary value is required.");
        }

        return target.SecondaryValue.Trim();
    }

    private static TEnum ReadEnum<TEnum>(
        string value,
        ProcessCapabilityScopeTargetKind targetKind)
        where TEnum : struct, Enum
    {
        if (Enum.TryParse<TEnum>(RequireValue(new ProcessCapabilityScopeTarget
            {
                Kind = targetKind,
                Value = value
            }), ignoreCase: true, out var parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException(
            $"Capability scope target value '{value}' is not a valid {typeof(TEnum).Name}.");
    }
}
