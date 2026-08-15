namespace CanDoItAll.Modules.LlmChats.Application;

public sealed record LlmChatExecutionLeaseOptions
{
    public const string SectionName = "LlmChats:Dispatcher";

    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(1);

    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(2);

    public TimeSpan LeaseDuration { get; init; } = TimeSpan.FromSeconds(10);

    public int CandidateBatchSize { get; init; } = 16;

    public void Validate()
    {
        if (PollInterval < TimeSpan.FromMilliseconds(100) || PollInterval > TimeSpan.FromSeconds(30))
        {
            throw new InvalidOperationException("LLM Chat dispatcher polling must be between 100 milliseconds and 30 seconds.");
        }

        if (HeartbeatInterval < TimeSpan.FromMilliseconds(100) || HeartbeatInterval > TimeSpan.FromMinutes(1))
        {
            throw new InvalidOperationException("LLM Chat execution heartbeat must be between 100 milliseconds and one minute.");
        }

        if (LeaseDuration < HeartbeatInterval * 3 || LeaseDuration > TimeSpan.FromMinutes(10))
        {
            throw new InvalidOperationException("LLM Chat execution lease duration must be at least three heartbeat intervals and at most ten minutes.");
        }

        if (CandidateBatchSize is < 1 or > 100)
        {
            throw new InvalidOperationException("LLM Chat dispatcher candidate batch size must be between 1 and 100.");
        }
    }
}
