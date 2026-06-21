namespace CanDoItAll.Modules.Processes;

internal static class ProcessBranchOutcomeRouting
{
    public static bool IsExceptionRoutingBranchOutcome(ProcessStepBranchOutcomeDefinition selectedBranchOutcome)
    {
        ArgumentNullException.ThrowIfNull(selectedBranchOutcome);

        if (ProcessCanvasBranching.IsErrorOutcome(selectedBranchOutcome))
        {
            return true;
        }

        var token = NormalizeBranchDispositionToken(
            $"{selectedBranchOutcome.Key} {selectedBranchOutcome.Title} {selectedBranchOutcome.Description}");
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        return token.Contains("repair", StringComparison.Ordinal) ||
            token.Contains("remediation", StringComparison.Ordinal) ||
            token.Contains("remediate", StringComparison.Ordinal) ||
            token.Contains("rework", StringComparison.Ordinal) ||
            token.Contains("fixrequired", StringComparison.Ordinal) ||
            token.Contains("fixesrequired", StringComparison.Ordinal) ||
            token.Contains("changesrequired", StringComparison.Ordinal) ||
            token.Contains("defect", StringComparison.Ordinal) ||
            token.Contains("failedvalidation", StringComparison.Ordinal) ||
            token.Contains("validationrejected", StringComparison.Ordinal) ||
            token.Contains("qualityrejected", StringComparison.Ordinal) ||
            token.Contains("unresolved", StringComparison.Ordinal) ||
            token.Contains("escalation", StringComparison.Ordinal) ||
            token.Contains("exception", StringComparison.Ordinal) ||
            token.Contains("nogo", StringComparison.Ordinal) ||
            token.Contains("blocked", StringComparison.Ordinal);
    }

    private static string NormalizeBranchDispositionToken(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }
}
