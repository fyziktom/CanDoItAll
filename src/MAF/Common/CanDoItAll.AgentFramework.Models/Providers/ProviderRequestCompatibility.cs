namespace CanDoItAll.AgentFramework.Models;

[Flags]
public enum ProviderInvocationFeatures
{
    None = 0,
    FunctionTools = 1 << 0
}

public enum ProviderRequestCompatibilityDisposition
{
    Preserved = 0,
    Adjusted = 1
}

public enum ProviderModelParameterAdjustment
{
    None = 0,
    ReasoningDisabledForFunctionTools = 1
}

public sealed record ProviderReasoningEffortResolution(
    AgentReasoningEffortLevel? RequestedEffort,
    AgentReasoningEffortLevel? EffectiveEffort,
    ProviderRequestCompatibilityDisposition Disposition,
    ProviderModelParameterAdjustment Adjustment);

public sealed record ProviderRequestCompatibilityEvidence(
    int SchemaVersion,
    ProviderKind ProviderKind,
    Guid? ProviderProfileId,
    ProviderTransportKind Transport,
    string RequestedModel,
    string EffectiveModel,
    ProviderInvocationFeatures InvocationFeatures,
    AgentReasoningEffortLevel? RequestedEffort,
    AgentReasoningEffortLevel? EffectiveEffort,
    ProviderRequestCompatibilityDisposition Disposition,
    ProviderModelParameterAdjustment Adjustment)
{
    public const int CurrentSchemaVersion = 1;
}
