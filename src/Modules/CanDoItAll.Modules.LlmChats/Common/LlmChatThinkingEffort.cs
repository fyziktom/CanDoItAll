using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.LlmChats.Common;

public enum LlmChatThinkingEffort
{
    None,
    Minimal,
    Low,
    Medium,
    High,
    ExtraHigh,
    Max
}

public enum LlmChatThinkingEffortSupport
{
    Supported,
    Unsupported,
    Unknown
}

public enum LlmChatThinkingEffortControl
{
    Unspecified,
    BooleanToggle,
    EffortLevels
}

public static class LlmChatThinkingEffortMapper
{
    public static LlmChatThinkingEffortSupport FromProvider(AgentThinkingEffortSupportStatus support)
        => support switch
        {
            AgentThinkingEffortSupportStatus.Supported => LlmChatThinkingEffortSupport.Supported,
            AgentThinkingEffortSupportStatus.Unsupported => LlmChatThinkingEffortSupport.Unsupported,
            AgentThinkingEffortSupportStatus.Unknown => LlmChatThinkingEffortSupport.Unknown,
            _ => throw new ArgumentOutOfRangeException(nameof(support), support, "Unknown thinking-effort support status.")
        };

    public static LlmChatThinkingEffortControl FromProvider(AgentThinkingEffortControlMode control)
        => control switch
        {
            AgentThinkingEffortControlMode.Unspecified => LlmChatThinkingEffortControl.Unspecified,
            AgentThinkingEffortControlMode.BooleanToggle => LlmChatThinkingEffortControl.BooleanToggle,
            AgentThinkingEffortControlMode.EffortLevels => LlmChatThinkingEffortControl.EffortLevels,
            _ => throw new ArgumentOutOfRangeException(nameof(control), control, "Unknown thinking-effort control mode.")
        };

    public static LlmChatThinkingEffort? FromProvider(AgentReasoningEffortLevel? effort)
        => effort is { } value ? FromProvider(value) : null;

    public static LlmChatThinkingEffort FromProvider(AgentReasoningEffortLevel effort)
        => effort switch
        {
            AgentReasoningEffortLevel.None => LlmChatThinkingEffort.None,
            AgentReasoningEffortLevel.Minimal => LlmChatThinkingEffort.Minimal,
            AgentReasoningEffortLevel.Low => LlmChatThinkingEffort.Low,
            AgentReasoningEffortLevel.Medium => LlmChatThinkingEffort.Medium,
            AgentReasoningEffortLevel.High => LlmChatThinkingEffort.High,
            AgentReasoningEffortLevel.ExtraHigh => LlmChatThinkingEffort.ExtraHigh,
            AgentReasoningEffortLevel.Max => LlmChatThinkingEffort.Max,
            _ => throw new ArgumentOutOfRangeException(nameof(effort), effort, "Unknown thinking effort.")
        };

    public static AgentReasoningEffortLevel? ToProvider(LlmChatThinkingEffort? effort)
        => effort is { } value ? ToProvider(value) : null;

    public static AgentReasoningEffortLevel ToProvider(LlmChatThinkingEffort effort)
        => effort switch
        {
            LlmChatThinkingEffort.None => AgentReasoningEffortLevel.None,
            LlmChatThinkingEffort.Minimal => AgentReasoningEffortLevel.Minimal,
            LlmChatThinkingEffort.Low => AgentReasoningEffortLevel.Low,
            LlmChatThinkingEffort.Medium => AgentReasoningEffortLevel.Medium,
            LlmChatThinkingEffort.High => AgentReasoningEffortLevel.High,
            LlmChatThinkingEffort.ExtraHigh => AgentReasoningEffortLevel.ExtraHigh,
            LlmChatThinkingEffort.Max => AgentReasoningEffortLevel.Max,
            _ => throw new ArgumentOutOfRangeException(nameof(effort), effort, "Unknown thinking effort.")
        };
}
