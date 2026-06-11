namespace CanDoItAll.Modules.AgentFramework.Hosting;

internal sealed partial class ProcessMockAgentRuntime
{
    private static bool IsApprovalQaPass(string prompt)
    {
        return prompt.Contains("qa recheck", StringComparison.OrdinalIgnoreCase) ||
               prompt.Contains("recheck repaired", StringComparison.OrdinalIgnoreCase) ||
               prompt.Contains("repaired mock implementation", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldApproveQaPrompt(string prompt)
    {
        if (MentionsLegacyRepairBranch(prompt) &&
            !prompt.Contains("quality-accepted", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var branchOutcomes = ResolvePromptBranchOutcomes(prompt);
        if (branchOutcomes.Any(IsRepairBranchOutcome) &&
            !branchOutcomes.Any(outcome => string.Equals(outcome.Key, "quality-accepted", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (branchOutcomes.Any(outcome => string.Equals(outcome.Key, "quality-accepted", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return !MentionsFirstPassMockDefect(prompt) &&
               IsApprovalQaPass(prompt);
    }

    private static bool MentionsLegacyRepairBranch(string prompt)
    {
        return prompt.Contains(ProcessMockAgentCatalog.BranchRepairsRequired, StringComparison.OrdinalIgnoreCase) ||
               prompt.Contains("Repairs required", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MentionsFirstPassMockDefect(string prompt)
    {
        return prompt.Contains("Review first sample implementation", StringComparison.OrdinalIgnoreCase) ||
               prompt.Contains("Sample QA rejection artifact", StringComparison.OrdinalIgnoreCase) ||
               prompt.Contains("First implementation artifact", StringComparison.OrdinalIgnoreCase) ||
               prompt.Contains("first-pass mock implementation", StringComparison.OrdinalIgnoreCase) ||
               prompt.Contains("Known Mock Defect", StringComparison.OrdinalIgnoreCase) ||
               prompt.Contains("intentionally accepts blank input", StringComparison.OrdinalIgnoreCase) ||
               prompt.Contains("accepts blank input", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveAcceptingBranchOutcomeKey(string prompt)
    {
        var branchOutcomes = ResolvePromptBranchOutcomes(prompt);
        var acceptingOutcome = branchOutcomes.FirstOrDefault(IsAcceptingBranchOutcome);
        if (acceptingOutcome is not null)
        {
            return acceptingOutcome.BranchOutcomeKey;
        }

        var defaultOutcome = branchOutcomes.FirstOrDefault(IsDefaultBranchOutcome);
        return defaultOutcome?.BranchOutcomeKey;
    }

    private static string? ResolveRepairBranchOutcomeKey(string prompt)
    {
        return ResolvePromptBranchOutcomes(prompt)
            .FirstOrDefault(IsRepairBranchOutcome)
            ?.BranchOutcomeKey;
    }

    private static IReadOnlyList<PromptBranchOutcome> ResolvePromptBranchOutcomes(string prompt)
    {
        var outcomes = new List<PromptBranchOutcome>();
        var inSection = false;

        foreach (var rawLine in prompt.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var line = rawLine.TrimEnd();
            if (string.Equals(line.Trim(), "Available branch outcomes:", StringComparison.OrdinalIgnoreCase))
            {
                inSection = true;
                continue;
            }

            if (!inSection)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                if (outcomes.Count > 0)
                {
                    break;
                }

                continue;
            }

            var match = BranchOutcomeLineRegex.Match(line.Trim());
            if (!match.Success)
            {
                continue;
            }

            var key = match.Groups["key"].Value.Trim();
            var title = match.Groups["title"].Success
                ? match.Groups["title"].Value.Trim()
                : match.Groups["titleOnly"].Value.Trim();
            var description = match.Groups["description"].Value.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                key = title;
            }

            outcomes.Add(new PromptBranchOutcome(key, title, description));
        }

        return outcomes;
    }

    private static bool IsAcceptingBranchOutcome(PromptBranchOutcome outcome)
    {
        var keyTitleToken = NormalizeBranchOutcomeToken($"{outcome.Key} {outcome.Title}");
        if (ContainsAnyBranchToken(
                keyTitleToken,
                [
                    "accepted",
                    "approval",
                    "approved",
                    "qualityaccepted"
                ]))
        {
            return true;
        }

        var token = NormalizeBranchOutcomeToken($"{outcome.Key} {outcome.Title} {outcome.Description}");
        return !ContainsAnyBranchToken(
                   token,
                   [
                       "blocked",
                       "error",
                       "escalation",
                       "failed",
                       "failure",
                       "halt",
                       "rejected",
                       "rejection",
                       "remediation",
                       "repair",
                       "rework"
                   ]) &&
               ContainsAnyBranchToken(
                   token,
                   [
                       "accepted",
                       "approval",
                       "approved",
                       "qualityaccepted",
                       "sufficient"
                   ]);
    }

    private static bool IsRepairBranchOutcome(PromptBranchOutcome outcome)
    {
        var keyTitleToken = NormalizeBranchOutcomeToken($"{outcome.Key} {outcome.Title}");
        if (IsAcceptingBranchOutcome(outcome))
        {
            return false;
        }

        if (ContainsAnyBranchToken(
                keyTitleToken,
                [
                    "changesrequired",
                    "repair",
                    "repairrequired",
                    "repairsrequired",
                    "rework"
                ]))
        {
            return true;
        }

        var token = NormalizeBranchOutcomeToken($"{outcome.Key} {outcome.Title} {outcome.Description}");
        return ContainsAnyBranchToken(
                   token,
                   [
                       "changesrequired",
                       "defect",
                       "missingproof",
                       "remediation",
                       "repair",
                       "repairrequired",
                       "repairsrequired",
                       "rework"
                   ]);
    }

    private static bool IsDefaultBranchOutcome(PromptBranchOutcome outcome)
    {
        var token = NormalizeBranchOutcomeToken($"{outcome.Key} {outcome.Title}");
        return token.Contains("default", StringComparison.Ordinal) ||
               token.Contains("continue", StringComparison.Ordinal);
    }

    private static bool ContainsAnyBranchToken(string token, IReadOnlyList<string> candidates)
    {
        return candidates.Any(candidate => token.Contains(candidate, StringComparison.Ordinal));
    }

    private static string NormalizeBranchOutcomeToken(string value)
    {
        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

}
