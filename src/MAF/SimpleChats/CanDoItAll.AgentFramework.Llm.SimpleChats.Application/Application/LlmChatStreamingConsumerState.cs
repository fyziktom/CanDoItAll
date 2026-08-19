using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

public enum LlmChatStreamingConsumerAbortReason
{
    StreamLimitExceeded
}

public sealed class LlmChatStreamingConsumerState
{
    public LlmChatStreamingConsumerAbortReason? AbortReason { get; private set; }

    public void Reset()
        => AbortReason = null;

    public void Abort(LlmChatStreamingConsumerAbortReason reason)
    {
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown consumer abort reason.");
        }

        AbortReason ??= reason;
    }

    public string ResolveFailureCode()
        => AbortReason switch
        {
            LlmChatStreamingConsumerAbortReason.StreamLimitExceeded => LlmChatErrorCodes.StreamLimitExceeded,
            null => string.Empty,
            _ => throw new ArgumentOutOfRangeException(nameof(AbortReason), AbortReason, "Unknown consumer abort reason.")
        };
}
