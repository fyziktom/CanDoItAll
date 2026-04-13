namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService
{
    private static string BuildTransitionJournalDescription(
        string stepTitle,
        ProcessStepRunStatus targetStatus,
        string reason,
        string? selectedBranchOutcomeTitle)
    {
        var description = $"{stepTitle} moved to {targetStatus}.";
        if (!string.IsNullOrWhiteSpace(selectedBranchOutcomeTitle))
        {
            description += $" Selected branch outcome: {selectedBranchOutcomeTitle}.";
        }

        if (!string.IsNullOrWhiteSpace(reason))
        {
            description += $" {reason.Trim()}";
        }

        return description.Trim();
    }
}
