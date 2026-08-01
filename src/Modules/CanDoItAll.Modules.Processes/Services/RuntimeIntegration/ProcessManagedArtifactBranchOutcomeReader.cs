using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessManagedArtifactBranchOutcomeReader
{
    private static readonly Regex BranchOutcomeHeadingRegex = new(
        @"^\s*#{2,6}\s+Branch Outcome\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex MarkdownHeadingRegex = new(
        @"^\s*#{1,6}\s+",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex BranchOutcomeKeyRegex = new(
        @"^\s*-\s*Key\s*:\s*(?<key>[A-Za-z0-9][A-Za-z0-9._-]*)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ExplicitBranchOutcomeKeyRegex = new(
        @"^\s*(?:[-*]\s*)?Branch outcome key\s*:\s*(?<key>[A-Za-z0-9][A-Za-z0-9._-]*)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static IEnumerable<string> ReadKeys(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            yield break;
        }

        var lines = ParentSubprocessRuntimeEnvelopeFraming
            .EnumerateLinesOutsideCodeFences(content)
            .ToArray();
        var inBranchOutcomeSection = false;
        for (var index = 0; index < lines.Length; index++)
        {
            var explicitKeyMatch = ExplicitBranchOutcomeKeyRegex.Match(lines[index]);
            if (explicitKeyMatch.Success)
            {
                yield return explicitKeyMatch.Groups["key"].Value.Trim();
                continue;
            }

            if (BranchOutcomeHeadingRegex.IsMatch(lines[index]))
            {
                inBranchOutcomeSection = true;
                continue;
            }

            if (MarkdownHeadingRegex.IsMatch(lines[index]))
            {
                inBranchOutcomeSection = false;
                continue;
            }

            if (!inBranchOutcomeSection)
            {
                continue;
            }

            var keyMatch = BranchOutcomeKeyRegex.Match(lines[index]);
            if (keyMatch.Success)
            {
                yield return keyMatch.Groups["key"].Value.Trim();
            }
        }
    }

}
