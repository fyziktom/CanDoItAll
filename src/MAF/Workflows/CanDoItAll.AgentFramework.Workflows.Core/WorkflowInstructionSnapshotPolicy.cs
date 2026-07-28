namespace CanDoItAll.AgentFramework.Core;

public static class WorkflowInstructionSnapshotPolicy
{
    public const string LegacyTemplatePlaceholder =
        "Run the prepared LLM component and return the strict JSON contract.";

    public static bool RequiresComponentBackfill(string? instructions)
        => string.IsNullOrWhiteSpace(instructions) ||
           string.Equals(
               instructions.Trim(),
               LegacyTemplatePlaceholder,
               StringComparison.Ordinal);
}
