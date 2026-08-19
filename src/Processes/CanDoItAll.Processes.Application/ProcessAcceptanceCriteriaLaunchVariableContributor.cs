using System.Text;
using System.Text.RegularExpressions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessAcceptanceCriteriaLaunchVariableContributor : IProcessLaunchVariableContributor
{
    private const string PlannedValidationProof = "planned-validation";
    private const string SourceContextProof = "source-context";

    public void Enrich(
        ProcessLaunchPreparationContext context,
        IDictionary<string, string> variables)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(variables);

        if (context.IsSubprocess &&
            TryCanonicalizeInheritedContract(variables))
        {
            return;
        }

        if (!TryBuildMatrix(context.Source, out var matrix, out var contract))
        {
            return;
        }

        variables[ProcessRuntimeLaunchVariables.AcceptanceCriteriaMatrix] =
            ProcessAcceptanceCriteriaMatrixJson.Serialize(matrix);
        variables[ProcessRuntimeLaunchVariables.ProductAcceptanceCriteriaContract] =
            contract;
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
                VerificationMethods =
                [
                    candidate.Kind == ProcessAcceptanceCriterionKind.ProductAcceptance
                        ? PlannedValidationProof
                        : SourceContextProof
                ],
                RequiredForAcceptance =
                    candidate.Kind == ProcessAcceptanceCriterionKind.ProductAcceptance,
                Kind = candidate.Kind
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
        if (source.SelectedItem.IsIncludedInProcessContext)
        {
            AddCandidates(
                source.SelectedItem,
                candidates,
                ProcessAcceptanceCriterionKind.ProductAcceptance);
        }

        foreach (var item in source.ContextItems)
        {
            if (string.Equals(item.Id, source.SelectedItem.Id, StringComparison.Ordinal))
            {
                continue;
            }

            var implicitKind = item.Kind is
                ProcessLaunchSourceItemKind.ImageAsset or
                ProcessLaunchSourceItemKind.ProductRequirement
                ? ProcessAcceptanceCriterionKind.ProductAcceptance
                : ProcessAcceptanceCriterionKind.DeliveryPlanning;
            AddCandidates(
                item,
                candidates,
                implicitKind,
                explicitSectionOnly: item.Kind == ProcessLaunchSourceItemKind.WorkItem);
        }

        return candidates
            .OrderBy(candidate =>
                candidate.Kind == ProcessAcceptanceCriterionKind.ProductAcceptance
                    ? 0
                    : 1)
            .DistinctBy(candidate => NormalizeSummary(candidate.Summary))
            .Take(20)
            .ToArray();
    }

    private static void AddCandidates(
        ProcessLaunchSourceItem item,
        List<AcceptanceCriteriaCandidate> candidates,
        ProcessAcceptanceCriterionKind implicitKind,
        bool explicitSectionOnly = false)
    {
        var sourceText = string.Join(
            Environment.NewLine,
            item.Title,
            item.Subtitle,
            item.Notes);
        var explicitSectionLines = ExtractExplicitSection(sourceText);
        var hasExplicitSection = explicitSectionLines.Count > 0;
        if (explicitSectionOnly && !hasExplicitSection)
        {
            return;
        }

        var segments = hasExplicitSection
            ? explicitSectionLines.Select(value => new AcceptanceCriteriaSegment(
                value,
                ProcessAcceptanceCriterionKind.ProductAcceptance))
            : SplitCriteriaTextByKind(sourceText, implicitKind);

        var initialCandidateCount = candidates.Count;
        foreach (var summary in segments
                     .Select(segment => new AcceptanceCriteriaSegment(
                         CleanCriterion(segment.Summary),
                         segment.Kind))
                     .Where(segment => IsSubstantive(segment.Summary, hasExplicitSection))
                     .Take(12))
        {
            candidates.Add(new AcceptanceCriteriaCandidate(
                item.Id,
                summary.Summary,
                summary.Kind));
        }

        if (candidates.Count == initialCandidateCount &&
            item.Kind is ProcessLaunchSourceItemKind.ImageAsset or
                ProcessLaunchSourceItemKind.ProductRequirement)
        {
            candidates.Add(new AcceptanceCriteriaCandidate(
                item.Id,
                BuildTypedSourceFallback(item),
                ProcessAcceptanceCriterionKind.ProductAcceptance));
        }
    }

    private static string BuildTypedSourceFallback(ProcessLaunchSourceItem item)
    {
        var descriptiveText = new[] { item.Notes, item.Subtitle }
            .Select(CleanCriterion)
            .FirstOrDefault(value => value.Length is >= 18 and <= 260);
        if (!string.IsNullOrWhiteSpace(descriptiveText))
        {
            return descriptiveText;
        }

        var title = string.IsNullOrWhiteSpace(item.Title)
            ? item.Id
            : item.Title.Trim();
        return item.Kind == ProcessLaunchSourceItemKind.ImageAsset
            ? $"Use the source asset '{title}' as a visual acceptance target."
            : $"Honor the source requirement '{title}'.";
    }

    private static IReadOnlyList<AcceptanceCriteriaSegment> SplitCriteriaTextByKind(
        string text,
        ProcessAcceptanceCriterionKind implicitKind)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var segments = new List<AcceptanceCriteriaSegment>();
        var currentKind = implicitKind;
        foreach (var line in text
                     .Replace("\r\n", "\n", StringComparison.Ordinal)
                     .Split('\n', StringSplitOptions.TrimEntries))
        {
            if (TryGetSectionKind(line, implicitKind, out var sectionKind))
            {
                currentKind = sectionKind;
                continue;
            }

            segments.AddRange(SplitCriteriaText(line)
                .Select(value => new AcceptanceCriteriaSegment(value, currentKind)));
        }

        return segments;
    }

    private static bool TryGetSectionKind(
        string line,
        ProcessAcceptanceCriterionKind implicitKind,
        out ProcessAcceptanceCriterionKind kind)
    {
        kind = implicitKind;
        if (!TryGetSectionTitle(line, out var title))
        {
            return false;
        }

        kind = Regex.IsMatch(
            title,
            @"^(?:recommended\s+)?next\s+actions?$|^open\s+(?:gaps?|questions?)$|^pending\s+(?:decisions?|questions?)$|^assumptions?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            ? ProcessAcceptanceCriterionKind.DeliveryPlanning
            : implicitKind;
        return true;
    }

    private static bool TryGetSectionTitle(string line, out string title)
    {
        title = string.Empty;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var trimmed = line.Trim();
        if (Regex.IsMatch(
                trimmed,
                @"^#{1,6}\s+\S",
                RegexOptions.CultureInvariant))
        {
            title = Regex.Replace(
                    trimmed,
                    @"^#{1,6}\s+",
                    string.Empty,
                    RegexOptions.CultureInvariant)
                .Trim()
                .TrimEnd(':');
            return title.Length > 0;
        }

        if (!LooksLikeSectionHeader(trimmed))
        {
            return false;
        }

        title = trimmed.TrimEnd(':').Trim();
        return title.Length > 0;
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

        return selected
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
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
           ((Regex.IsMatch(
                 line.Trim(),
                 @"^#{1,6}\s+\S",
                 RegexOptions.CultureInvariant)) ||
            (line.Length <= 80 &&
             line.EndsWith(':') &&
             !line.Contains("must", StringComparison.OrdinalIgnoreCase)));

    private static string BuildContract(IReadOnlyList<ProcessAcceptanceCriterion> criteria)
    {
        var requiredCriteria = criteria
            .Where(criterion =>
                criterion.RequiredForAcceptance &&
                criterion.Kind == ProcessAcceptanceCriterionKind.ProductAcceptance)
            .ToArray();
        var planningCriteria = criteria
            .Where(criterion =>
                criterion.Kind == ProcessAcceptanceCriterionKind.DeliveryPlanning)
            .ToArray();
        var builder = new StringBuilder();
        builder.AppendLine("AcceptanceCriteriaContract: an accepted branch must cite every required product-acceptance criterion id with concrete evidence. Delivery-planning context is non-blocking and must not request fresh human confirmation during autonomous implementation unless a separate typed decision gate explicitly owns it. The architecture and validation-plan steps choose the appropriate proof methods.");
        foreach (var criterion in requiredCriteria)
        {
            builder.AppendLine(
                $"{criterion.Id}: {criterion.Summary} [kind=ProductAcceptance; required=true; proof={PlannedValidationProof}]");
        }

        if (planningCriteria.Length > 0)
        {
            builder.AppendLine();
            builder.AppendLine("DeliveryPlanningContext: the following items remain visible for planning but do not block product acceptance and require no criterion-by-criterion implementation proof.");
            foreach (var criterion in planningCriteria)
            {
                builder.AppendLine(
                    $"{criterion.Id}: {criterion.Summary} [kind=DeliveryPlanning; required=false; proof={SourceContextProof}]");
            }
        }

        return builder.ToString().Trim();
    }

    private static string NormalizeSummary(string value)
        => Regex.Replace(value.Trim().ToLowerInvariant(), @"\s+", " ", RegexOptions.CultureInvariant);

    private static bool TryCanonicalizeInheritedContract(
        IDictionary<string, string> variables)
    {
        if (!variables.TryGetValue(
                ProcessRuntimeLaunchVariables.AcceptanceCriteriaMatrix,
                out var rawMatrix))
        {
            return false;
        }

        if (ProcessAcceptanceCriteriaMatrixJson.TryDeserialize(
                rawMatrix,
                out var matrix))
        {
            variables[ProcessRuntimeLaunchVariables.AcceptanceCriteriaMatrix] =
                ProcessAcceptanceCriteriaMatrixJson.Serialize(matrix);
            variables[ProcessRuntimeLaunchVariables.ProductAcceptanceCriteriaContract] =
                BuildContract(matrix.Criteria);
        }

        return true;
    }

    private sealed record AcceptanceCriteriaCandidate(
        string SourceId,
        string Summary,
        ProcessAcceptanceCriterionKind Kind);

    private sealed record AcceptanceCriteriaSegment(
        string Summary,
        ProcessAcceptanceCriterionKind Kind);
}
