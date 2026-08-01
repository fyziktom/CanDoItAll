namespace CanDoItAll.AgentFramework.Capabilities.Abstractions;

public sealed record CapabilityIdentity(
    CapabilityKind Kind,
    CapabilityKey Key);

public sealed record CapabilitySideEffectProfile(
    CapabilitySideEffectKind Kind,
    bool RequiresApprovalByDefault,
    bool IsStateChanging);

public sealed record CapabilityExposureDescriptor(
    CapabilityIdentity Identity,
    string DisplayName,
    string Description,
    ImplementationKey? ImplementationKey,
    RuntimeToolName? RuntimeToolName,
    McpServerKey? McpServerKey,
    McpToolName? McpToolName,
    IReadOnlySet<CapabilityTag> Tags,
    IReadOnlySet<CapabilityOperationClassification> OperationClassifications,
    CapabilitySideEffectProfile SideEffectProfile,
    CapabilityAvailabilityState AvailabilityState,
    TemplatePath? SourcePath);

public sealed record CapabilityDiagnostic(
    CapabilityDiagnosticCategory Category,
    CapabilityValidationSeverity Severity,
    CapabilityKind? CapabilityKind,
    CapabilityKey? CapabilityKey,
    TemplatePath? TemplatePath,
    string FieldPath,
    ImplementationKey? ImplementationKey,
    CapabilityTransportKind? Transport,
    int? ExitCode,
    int? HttpStatusCode,
    TimeSpan? Timeout,
    string CorrelationId,
    string MaskedDetail,
    string RepairHint);

public sealed record CapabilityValidationIssue(
    CapabilityDiagnosticCategory Category,
    CapabilityValidationSeverity Severity,
    CapabilityKind? CapabilityKind,
    CapabilityKey? CapabilityKey,
    TemplatePath? TemplatePath,
    string FieldPath,
    string Message,
    string RepairHint);

public sealed record CapabilityValidationResult(IReadOnlyList<CapabilityValidationIssue> Issues)
{
    public bool IsValid => Issues.All(issue => issue.Severity != CapabilityValidationSeverity.Error);

    public static CapabilityValidationResult Passed { get; } = new([]);
}

public sealed record CapabilitySetupTestResult(
    bool IsSuccess,
    CapabilityIdentity Identity,
    string CorrelationId,
    IReadOnlyList<CapabilityDiagnostic> Diagnostics);

public sealed record CapabilitySelector(
    CapabilitySelectorKind Kind,
    CapabilityKind? CapabilityKind = null,
    CapabilityKey? CapabilityKey = null,
    CapabilityTag? Tag = null,
    CapabilityOperationClassification? OperationClassification = null,
    RuntimeToolName? RuntimeToolName = null,
    McpServerKey? McpServerKey = null,
    McpToolName? McpToolName = null,
    ImplementationKey? ImplementationKey = null)
{
    public static CapabilitySelector All { get; } = new(CapabilitySelectorKind.All);

    public static CapabilitySelector ByKind(CapabilityKind kind)
        => new(CapabilitySelectorKind.Kind, CapabilityKind: kind);

    public static CapabilitySelector ByCapabilityKey(CapabilityKey key)
        => new(CapabilitySelectorKind.CapabilityKey, CapabilityKey: key);

    public static CapabilitySelector ByTag(CapabilityTag tag)
        => new(CapabilitySelectorKind.Tag, Tag: tag);

    public static CapabilitySelector ByOperationClassification(CapabilityOperationClassification classification)
        => new(CapabilitySelectorKind.OperationClassification, OperationClassification: classification);

    public static CapabilitySelector ByRuntimeToolName(RuntimeToolName name)
        => new(CapabilitySelectorKind.RuntimeToolName, RuntimeToolName: name);

    public static CapabilitySelector ByMcpServerKey(McpServerKey key)
        => new(CapabilitySelectorKind.McpServerKey, McpServerKey: key);

    public static CapabilitySelector ByMcpToolName(McpServerKey serverKey, McpToolName toolName)
        => new(CapabilitySelectorKind.McpToolName, McpServerKey: serverKey, McpToolName: toolName);

    public static CapabilitySelector ByImplementationKey(ImplementationKey key)
        => new(CapabilitySelectorKind.ImplementationKey, ImplementationKey: key);
}

public sealed record CapabilityAccessRule(
    CapabilityRuleId Id,
    CapabilityAccessEffect Effect,
    CapabilityAccessScope Scope,
    CapabilitySelector Selector,
    string Reason);

public sealed record CapabilityAccessPolicy(
    IReadOnlyList<CapabilityAccessRule> Rules,
    CapabilityAccessDefaultEffect DefaultEffect = CapabilityAccessDefaultEffect.Inherit,
    CapabilityAccessScope? DefaultScope = null,
    string DefaultReason = "");

public sealed record CapabilityAccessEvaluationContext(
    IReadOnlyList<CapabilityExposureDescriptor> CandidateCapabilities,
    IReadOnlyList<CapabilityIdentity> RequiredCapabilities,
    IReadOnlyList<CapabilityAccessPolicy> Policies,
    string CorrelationId);

public sealed record SuppressedCapabilityDiagnostic(
    CapabilityIdentity Identity,
    CapabilityRuleId? RuleId,
    CapabilityAccessScope? Scope,
    CapabilitySelectorKind? SelectorKind,
    CapabilityDiagnosticCategory Category,
    string Reason,
    string RepairHint,
    string CorrelationId);

public sealed record CapabilityAccessEvaluationResult(
    IReadOnlyList<CapabilityExposureDescriptor> AllowedCapabilities,
    IReadOnlyList<SuppressedCapabilityDiagnostic> Diagnostics)
{
    public EffectiveCapabilitySet ToEffectiveSet()
        => new(AllowedCapabilities, Diagnostics);
}

public sealed record EffectiveCapabilitySet(
    IReadOnlyList<CapabilityExposureDescriptor> AllowedCapabilities,
    IReadOnlyList<SuppressedCapabilityDiagnostic> Diagnostics);

public interface ICapabilityAccessPolicyEvaluator
{
    CapabilityAccessEvaluationResult Evaluate(CapabilityAccessEvaluationContext context);
}
