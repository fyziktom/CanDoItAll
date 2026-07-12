using System.Text;
using System.Text.RegularExpressions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessAcceptanceCriteriaLaunchVariableContributor : IProcessLaunchVariableContributor
{
    public void Enrich(
        ProcessLaunchPreparationContext context,
        IDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(variables);

        if (!TryBuildMatrix(context.Source, out var matrix, out var contract))
        {
            return;
        }

        AddIfMissing(
            variables,
            ProcessRuntimeLaunchVariables.AcceptanceCriteriaMatrix,
            ProcessAcceptanceCriteriaMatrixJson.Serialize(matrix));
        AddIfMissing(
            variables,
            ProcessRuntimeLaunchVariables.ProductAcceptanceCriteriaContract,
            contract);
    }

    private static bool TryBuildMatrix(
        ProcessLaunchSourceSnapshot source,
        out ProcessAcceptanceCriteriaMatrix matrix,
        out string contract)
    {
        matrix = new ProcessAcceptanceCriteriaMatrix();
        contract = string.Empty;

        var criteria = EnumerateCandidates(source)
            .Select((candidate, index) => new ProcessAcceptanceCriterion
            {
                Id = $"AC-{index + 1:000}",
                SourceNodeId = candidate.SourceId,
                Summary = candidate.Summary,
                VerificationMethods = ["planned-validation"],
                RequiredForAcceptance = true
            })
            .ToArray();
        if (criteria.Length == 0)
        {
            return false;
        }

        matrix.Criteria.AddRange(criteria);
        contract = BuildContract(criteria);
        return true;
    }

    private static IReadOnlyList<AcceptanceCriteriaCandidate> EnumerateCandidates(
        ProcessLaunchSourceSnapshot source)
    {
        var candidates = new List<AcceptanceCriteriaCandidate>();
        foreach (var item in source.ContextItems)
        {
            AddCandidates(item, candidates);
        }

        if (candidates.Count == 0 && source.SelectedItem.IsIncludedInProcessContext)
        {
            AddCandidates(source.SelectedItem, candidates);
        }

        return candidates
            .DistinctBy(candidate => NormalizeSummary(candidate.Summary))
            .Take(20)
            .ToArray();
    }

    private static void AddCandidates(
        ProcessLaunchSourceItem item,
        List<AcceptanceCriteriaCandidate> candidates)
    {
        var sourceText = string.Join(
            Environment.NewLine,
            item.Title,
            item.Subtitle,
            item.Notes);
        var explicitSectionLines = ExtractExplicitSection(sourceText);
        var hasExplicitSection = explicitSectionLines.Count > 0;
        var segments = hasExplicitSection
            ? explicitSectionLines
            : SplitCriteriaText(sourceText);

        foreach (var summary in segments
                     .Select(CleanCriterion)
                     .Where(value => IsSubstantive(value, hasExplicitSection))
                     .Take(12))
        {
            candidates.Add(new AcceptanceCriteriaCandidate(item.Id, summary));
        }
    }

    private static IReadOnlyList<string> ExtractExplicitSection(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var lines = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.TrimEntries)
            .ToArray();
        var selected = new List<string>();
        var inSection = false;
        foreach (var line in lines)
        {
            if (line.Contains("acceptance criteria", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("definition of done", StringComparison.OrdinalIgnoreCase))
            {
                inSection = true;
                var tail = Regex.Replace(
                    line,
                    ".*?(acceptance criteria|definition of done)\\s*[:\\-]\\s*",
                    string.Empty,
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (!string.IsNullOrWhiteSpace(tail) &&
                    !string.Equals(tail, line, StringComparison.Ordinal))
                {
                    selected.Add(tail);
                }

                continue;
            }

            if (!inSection)
            {
                continue;
            }

            if (LooksLikeSectionHeader(line) && selected.Count > 0)
            {
                break;
            }

            selected.Add(line);
        }

        return selected.Count == 0
            ? []
            : SplitCriteriaText(string.Join(Environment.NewLine, selected));
    }

    private static IReadOnlyList<string> SplitCriteriaText(string text)
        => string.IsNullOrWhiteSpace(text)
            ? []
            : Regex.Split(text, @"(?:\r?\n)+|(?:^|\s)[-*]\s+|;\s+|\.\s+(?=[A-Z0-9])", RegexOptions.CultureInvariant)
                .Select(value => value.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();

    private static string CleanCriterion(string value)
    {
        var cleaned = Regex.Replace(
            value.Trim(),
            @"^\s*(?:[-*]|\d+[\).\:]|AC[-\s]?\d+[\).\:]?)\s*",
            string.Empty,
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        return Regex.Replace(
                cleaned,
                @"^(?:must|should|shall)\s+",
                string.Empty,
                RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)
            .Trim();
    }

    private static bool IsSubstantive(string value, bool hasExplicitSection)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 18 || value.Length > 260)
        {
            return false;
        }

        return hasExplicitSection || HasNormativeCriteriaSignal(value);
    }

    private static bool HasNormativeCriteriaSignal(string value)
        => Regex.IsMatch(
               value,
               @"\b(?:must|shall|required|requires|only|do\s+not|does\s+not|cannot|can't|without|belongs?|remains?)\b",
               RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
           Regex.IsMatch(
               value.TrimStart(),
               @"^(?:use|include|keep|show|display|allow|support|validate|reject|persist|store|save|fit|wrap|fail|route|expose|render|provide|prevent|retain|avoid)\b",
               RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static bool LooksLikeSectionHeader(string line)
        => !string.IsNullOrWhiteSpace(line) &&
           line.Length <= 80 &&
           line.EndsWith(':') &&
           !line.Contains("must", StringComparison.OrdinalIgnoreCase);

    private static string BuildContract(IReadOnlyList<ProcessAcceptanceCriterion> criteria)
    {
        var builder = new StringBuilder();
        builder.AppendLine("AcceptanceCriteriaContract: an accepted branch must cite every required criterion id with concrete evidence. The architecture and validation-plan steps choose the appropriate proof methods.");
        foreach (var criterion in criteria)
        {
            builder.AppendLine($"{criterion.Id}: {criterion.Summary} [proof=planned-validation]");
        }

        return builder.ToString().Trim();
    }

    private static string NormalizeSummary(string value)
        => Regex.Replace(value.Trim().ToLowerInvariant(), @"\s+", " ", RegexOptions.CultureInvariant);

    private static void AddIfMissing(
        IDictionary<string, string> variables,
        string key,
        string value)
    {
        if (!string.IsNullOrWhiteSpace(value) &&
            (!variables.TryGetValue(key, out var existing) || string.IsNullOrWhiteSpace(existing)))
        {
            variables[key] = value;
        }
    }

    private sealed record AcceptanceCriteriaCandidate(
        string SourceId,
        string Summary);
}
