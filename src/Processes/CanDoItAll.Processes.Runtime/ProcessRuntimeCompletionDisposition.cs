namespace CanDoItAll.Processes.Runtime;

public sealed record ProcessRuntimeCompletionDisposition(
    bool AllowsCompletedOutcomeWithOpenIssues,
    IReadOnlyList<string> OpenIssueBranchOutcomeKeys)
{
    public bool AllowsOpenIssuesFor(string? branchOutcomeKey)
        => AllowsCompletedOutcomeWithOpenIssues ||
           (!string.IsNullOrWhiteSpace(branchOutcomeKey) &&
            (OpenIssueBranchOutcomeKeys ?? []).Contains(branchOutcomeKey.Trim(), StringComparer.OrdinalIgnoreCase));
}
