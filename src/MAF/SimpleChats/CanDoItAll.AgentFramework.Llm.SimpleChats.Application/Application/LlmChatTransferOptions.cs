namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Application;

public sealed record LlmChatTransferOptions
{
    public const string SectionName = "LlmChats:Transfer";

    public int MaximumRecordsPerCollection { get; init; } = 100_000;

    public int MaximumTotalRecords { get; init; } = 250_000;

    public void Validate()
    {
        if (MaximumRecordsPerCollection is < 1 or > 1_000_000)
        {
            throw new InvalidOperationException(
                "LLM Chat transfer collection limit must be between 1 and 1,000,000 records.");
        }

        if (MaximumTotalRecords < MaximumRecordsPerCollection || MaximumTotalRecords > 2_000_000)
        {
            throw new InvalidOperationException(
                "LLM Chat transfer total limit must be at least the collection limit and at most 2,000,000 records.");
        }
    }
}
