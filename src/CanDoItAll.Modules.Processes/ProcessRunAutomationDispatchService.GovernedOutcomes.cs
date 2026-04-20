using System.Text;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static bool TryResolveDeclaredStepOutcome(
        DispatchCandidate candidate,
        string? responseText,
        out DeclaredStepOutcome declaredOutcome)
    {
        declaredOutcome = default;
        if (!TryResolveDeclaredStepOutcome(responseText, out var parsedOutcome))
        {
            return false;
        }

        declaredOutcome = parsedOutcome with
        {
            SelectedBranchOutcomeId = ResolveSelectedBranchOutcomeId(
                candidate,
                parsedOutcome.Status,
                parsedOutcome.BranchOutcomeKey,
                parsedOutcome.BranchOutcomeTitle)
        };
        return true;
    }

    private static Guid? ResolveSelectedBranchOutcomeId(
        DispatchCandidate candidate,
        ProcessStepRunStatus completionStatus,
        string? responseText)
    {
        if (completionStatus != ProcessStepRunStatus.Completed ||
            !TryResolveDeclaredStepOutcome(responseText, out var declaredOutcome))
        {
            return null;
        }

        return ResolveSelectedBranchOutcomeId(
            candidate,
            completionStatus,
            declaredOutcome.BranchOutcomeKey,
            declaredOutcome.BranchOutcomeTitle);
    }

    private static Guid? ResolveSelectedBranchOutcomeId(
        DispatchCandidate candidate,
        ProcessStepRunStatus completionStatus,
        string? branchOutcomeKey,
        string? branchOutcomeTitle)
    {
        if (completionStatus != ProcessStepRunStatus.Completed || candidate.BranchOutcomes.Count == 0)
        {
            return null;
        }

        var normalizedBranchOutcomeKey = NormalizeBranchOutcomeToken(branchOutcomeKey);
        if (!string.IsNullOrWhiteSpace(normalizedBranchOutcomeKey))
        {
            var matchByKey = candidate.BranchOutcomes.FirstOrDefault(
                item => NormalizeBranchOutcomeToken(item.Key).Equals(normalizedBranchOutcomeKey, StringComparison.Ordinal));
            if (matchByKey is not null)
            {
                return matchByKey.Id;
            }
        }

        var normalizedBranchOutcomeTitle = NormalizeBranchOutcomeToken(branchOutcomeTitle);
        if (string.IsNullOrWhiteSpace(normalizedBranchOutcomeTitle))
        {
            return null;
        }

        var matchByTitle = candidate.BranchOutcomes.FirstOrDefault(
            item => NormalizeBranchOutcomeToken(item.Title).Equals(normalizedBranchOutcomeTitle, StringComparison.Ordinal));
        return matchByTitle?.Id;
    }

    private static string? ResolveBranchOutcomeSelectionFailure(
        DispatchCandidate candidate,
        DeclaredStepOutcome declaredOutcome)
    {
        if (declaredOutcome.Status != ProcessStepRunStatus.Completed || !candidate.RequiresExplicitBranchOutcomeSelection)
        {
            return null;
        }

        if (declaredOutcome.SelectedBranchOutcomeId.HasValue)
        {
            return null;
        }

        var availableOutcomes = string.Join(
            ", ",
            candidate.BranchOutcomes.Select(item => string.IsNullOrWhiteSpace(item.Key) ? item.Title : $"{item.Key} ({item.Title})"));
        if (string.IsNullOrWhiteSpace(declaredOutcome.BranchOutcomeKey) &&
            string.IsNullOrWhiteSpace(declaredOutcome.BranchOutcomeTitle))
        {
            return $"Step '{candidate.StepRun.Title}' completed without selecting a required branch outcome. Available branch outcomes: {availableOutcomes}.";
        }

        var declaredOutcomeLabel = string.IsNullOrWhiteSpace(declaredOutcome.BranchOutcomeKey)
            ? declaredOutcome.BranchOutcomeTitle
            : declaredOutcome.BranchOutcomeKey;
        return $"Step '{candidate.StepRun.Title}' declared branch outcome '{declaredOutcomeLabel}', but it is not valid for this step. Available branch outcomes: {availableOutcomes}.";
    }

    private static string BuildBranchOutcomePromptSummary(IReadOnlyList<DispatchBranchOutcome> branchOutcomes)
    {
        if (branchOutcomes.Count == 0)
        {
            return "None";
        }

        var builder = new StringBuilder();
        foreach (var outcome in branchOutcomes.OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase))
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append("- ");
            builder.Append(string.IsNullOrWhiteSpace(outcome.Key) ? outcome.Title : $"{outcome.Key} ({outcome.Title})");
            if (!string.IsNullOrWhiteSpace(outcome.Description))
            {
                builder.Append(": ");
                builder.Append(outcome.Description.Trim());
            }
        }

        return builder.ToString();
    }

    private static string NormalizeBranchOutcomeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value
            .Trim()
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }
}
