using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessExternalTargetGroundingService
{
    internal const string AliasRoot = "external-target";

    private static readonly Regex WorkspacePathInToolRequestRegex = new(
        @"(?<path>[A-Za-z]:\\[^`""'\r\n\s]+|external-target[\\/][^\s`""']+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static ProcessExternalTargetGroundingResult ResolveProjectStructureGroundingTarget(string? groundingSummary)
    {
        if (string.IsNullOrWhiteSpace(groundingSummary))
        {
            return ProcessExternalTargetGroundingResult.Empty;
        }

        var candidates = EnumerateAbsoluteExternalPathCandidates(groundingSummary)
            .Where(candidate => !IsProhibitedGroundingCandidate(groundingSummary, candidate.Index))
            .Select(candidate => ResolveExternalTargetCandidate(candidate.Path))
            .Where(candidate => candidate is not null)
            .Select(candidate => candidate!)
            .Where(candidate => !IsNonProductExternalTargetAlias(candidate.MappedAlias))
            .GroupBy(candidate => candidate.MappedAlias, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderByDescending(candidate => GetExternalTargetHintPriority(candidate.MappedAlias))
            .ThenByDescending(candidate => candidate.MappedAlias.Length)
            .ToList();

        if (candidates.Count == 0)
        {
            return ProcessExternalTargetGroundingResult.Empty;
        }

        var resolvedCandidate = candidates[0];
        var scaffoldTarget = TrySplitExternalTargetAliasForScaffold(
            resolvedCandidate.MappedAlias,
            out var parentAlias,
            out var leafName)
                ? new ProcessExternalTargetScaffoldTarget(parentAlias, leafName)
                : null;
        return new ProcessExternalTargetGroundingResult(
            true,
            resolvedCandidate.AbsolutePath,
            resolvedCandidate.MappedAlias,
            scaffoldTarget);
    }

    internal static ProcessExternalTargetReferenceInspection InspectReferences(
        string? text,
        IReadOnlyList<string> allowedAliases)
    {
        if (string.IsNullOrWhiteSpace(text) || allowedAliases.Count == 0)
        {
            return ProcessExternalTargetReferenceInspection.Empty;
        }

        var normalizedAllowedAliases = PruneAllowedExternalTargetAliasesForCurrentRun(allowedAliases);
        if (normalizedAllowedAliases.Count == 0)
        {
            return ProcessExternalTargetReferenceInspection.Empty;
        }

        var outOfScopeReferenceCount = 0;
        foreach (Match match in WorkspacePathInToolRequestRegex.Matches(text))
        {
            var rawPath = match.Groups["path"].Value;
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                continue;
            }

            var rawPathIsExternalTargetAlias = IsExternalTargetAlias(rawPath);
            var referencedAlias = rawPathIsExternalTargetAlias
                ? NormalizeExternalTargetAlias(rawPath)
                : TryMapAbsoluteExternalPathToAlias(rawPath, out var mappedAlias)
                    ? mappedAlias
                    : string.Empty;
            if (string.IsNullOrWhiteSpace(referencedAlias) ||
                IsAllowedExternalTargetReference(referencedAlias, normalizedAllowedAliases) ||
                IsDocumentedScaffoldParentReference(text, match.Index, referencedAlias, normalizedAllowedAliases) ||
                IsDocumentedRunBoundaryParentReference(text, match.Index, referencedAlias, normalizedAllowedAliases) ||
                IsProhibitedExternalTargetReference(text, match.Index))
            {
                continue;
            }

            if (!rawPathIsExternalTargetAlias &&
                !IsLikelyOutOfScopeExternalProductReference(referencedAlias, normalizedAllowedAliases))
            {
                continue;
            }

            outOfScopeReferenceCount++;
        }

        return outOfScopeReferenceCount == 0
            ? ProcessExternalTargetReferenceInspection.Empty
            : new ProcessExternalTargetReferenceInspection(
                outOfScopeReferenceCount,
                "the output references one or more external-target paths outside the current grounded product root; exact stale paths are omitted to prevent reuse");
    }

    internal static string RedactUnallowedReferencesForPrompt(
        string text,
        IReadOnlyList<string> allowedAliases)
    {
        if (string.IsNullOrWhiteSpace(text) || allowedAliases.Count == 0)
        {
            return text;
        }

        var normalizedAllowedAliases = PruneAllowedExternalTargetAliasesForCurrentRun(allowedAliases);
        if (normalizedAllowedAliases.Count == 0)
        {
            return text;
        }

        return WorkspacePathInToolRequestRegex.Replace(
            text,
            match =>
            {
                var rawPath = match.Groups["path"].Value;
                if (string.IsNullOrWhiteSpace(rawPath))
                {
                    return rawPath;
                }

                var referencedAlias = IsExternalTargetAlias(rawPath)
                    ? NormalizeExternalTargetAlias(rawPath)
                    : TryMapAbsoluteExternalPathToAlias(rawPath, out var mappedAlias)
                        ? mappedAlias
                        : string.Empty;
                if (string.IsNullOrWhiteSpace(referencedAlias) ||
                    IsAllowedExternalTargetReference(referencedAlias, normalizedAllowedAliases) ||
                    IsDocumentedScaffoldParentReference(text, match.Index, referencedAlias, normalizedAllowedAliases))
                {
                    return rawPath;
                }

                return "[stale external-target path omitted]";
            });
    }

    internal static IReadOnlyList<string> ExtractExternalTargetAliasesFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in WorkspacePathInToolRequestRegex.Matches(text))
        {
            var path = match.Groups["path"].Value;
            if (string.IsNullOrWhiteSpace(path) ||
                IsProhibitedGroundingCandidate(text, match.Index))
            {
                continue;
            }

            if (IsExternalTargetAlias(path))
            {
                var alias = NormalizeExternalTargetAlias(path);
                if (!string.IsNullOrWhiteSpace(alias))
                {
                    aliases.Add(alias);
                }

                continue;
            }

            if (TryMapAbsoluteExternalPathToAlias(path, out var mappedAlias))
            {
                aliases.Add(mappedAlias);
            }
        }

        foreach (var candidatePath in EnumerateAbsoluteExternalPathCandidates(text)
                     .Where(candidate => !IsProhibitedGroundingCandidate(text, candidate.Index)))
        {
            if (TryMapAbsoluteExternalPathToAlias(candidatePath.Path, out var mappedAlias))
            {
                aliases.Add(mappedAlias);
            }
        }

        return aliases
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .ToArray();
    }

    internal static IReadOnlyList<string> PruneAllowedExternalTargetAliasesForCurrentRun(IEnumerable<string> aliases)
    {
        ArgumentNullException.ThrowIfNull(aliases);

        var normalizedAliases = aliases
            .Select(NormalizeExternalTargetAlias)
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Where(alias => alias.StartsWith(AliasRoot + "/", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return normalizedAliases
            .Where(alias => !IsLikelyExternalTargetFileAlias(alias) ||
                            !normalizedAliases.Any(other =>
                                !string.Equals(alias, other, StringComparison.OrdinalIgnoreCase) &&
                                IsExternalTargetAliasAncestor(other, alias)))
            .Where(alias => IsPreferredProductExternalTargetAlias(alias) ||
                            !normalizedAliases.Any(other =>
                !string.Equals(alias, other, StringComparison.OrdinalIgnoreCase) &&
                IsExternalTargetAliasAncestor(alias, other) &&
                !IsLikelyExternalTargetFileAlias(other)))
            .Where(alias => !normalizedAliases.Any(other =>
                !string.Equals(alias, other, StringComparison.OrdinalIgnoreCase) &&
                IsAmbiguousExternalTargetPrefixAlias(alias, other)))
            .OrderByDescending(alias => alias.Length)
            .ToArray();
    }

    internal static bool TryMapAbsoluteExternalPathToAlias(
        string path,
        out string mappedAlias)
    {
        mappedAlias = string.Empty;
        if (!TryNormalizeAbsoluteExternalPathCandidate(path, out var normalizedPath))
        {
            return false;
        }

        var driveLetter = char.ToUpperInvariant(normalizedPath[0]);
        var remainder = normalizedPath.Length == 3
            ? string.Empty
            : CollapseExternalTargetAliasSeparators(normalizedPath[3..]).Trim('/');
        mappedAlias = string.IsNullOrWhiteSpace(remainder)
            ? $"{AliasRoot}/{driveLetter}"
            : $"{AliasRoot}/{driveLetter}/{remainder}";
        mappedAlias = NormalizeExternalTargetAlias(mappedAlias);
        return !string.IsNullOrWhiteSpace(mappedAlias);
    }

    internal static string NormalizeExternalTargetAlias(string? alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return string.Empty;
        }

        var normalizedAlias = alias
            .Replace('\\', '/')
            .Trim()
            .TrimEnd('/', '.', ',', ';', ':', ')', ']', '}');
        normalizedAlias = StripEscapedLineBreakPathAnnotations(normalizedAlias)
            .TrimEnd('/', '.', ',', ';', ':', ')', ']', '}');
        normalizedAlias = StripInlinePathAnnotations(normalizedAlias)
            .TrimEnd('/', '.', ',', ';', ':', ')', ']', '}');
        normalizedAlias = CollapseExternalTargetAliasSeparators(normalizedAlias);

        return normalizedAlias.StartsWith(AliasRoot + "/", StringComparison.OrdinalIgnoreCase)
            ? NormalizeExternalTargetAliasSegments(normalizedAlias)
            : normalizedAlias;
    }

    internal static bool IsAllowedExternalTargetReference(
        string referencedAlias,
        IReadOnlyList<string> allowedAliases)
    {
        return allowedAliases.Any(allowedAlias =>
            string.Equals(referencedAlias, allowedAlias, StringComparison.OrdinalIgnoreCase) ||
            referencedAlias.StartsWith(allowedAlias + "/", StringComparison.OrdinalIgnoreCase));
    }

    internal static bool IsDocumentedScaffoldParentReference(
        string text,
        int referenceIndex,
        string referencedAlias,
        IReadOnlyList<string> allowedAliases)
    {
        if (!allowedAliases.Any(allowedAlias =>
                TryResolveExternalTargetParentAlias(allowedAlias, out var parentAlias) &&
                string.Equals(referencedAlias, parentAlias, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var start = Math.Max(0, referenceIndex - 120);
        var length = Math.Min(text.Length - start, 260);
        var context = text.Substring(start, length);
        return context.Contains("scaffold parent", StringComparison.OrdinalIgnoreCase) ||
               context.Contains("parentDirectory", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsDocumentedRunBoundaryParentReference(
        string text,
        int referenceIndex,
        string referencedAlias,
        IReadOnlyList<string> allowedAliases)
    {
        if (!allowedAliases.Any(allowedAlias =>
                TryResolveExternalTargetParentAlias(allowedAlias, out var parentAlias) &&
                string.Equals(referencedAlias, parentAlias, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var start = Math.Max(0, referenceIndex - 160);
        var length = Math.Min(text.Length - start, 340);
        var context = CollapsePromptWhitespace(text.Substring(start, length)).ToLowerInvariant();
        return context.Contains("output root", StringComparison.Ordinal) ||
               context.Contains("run folder", StringComparison.Ordinal) ||
               context.Contains("run root", StringComparison.Ordinal) ||
               context.Contains("run boundary", StringComparison.Ordinal) ||
               context.Contains("approved run", StringComparison.Ordinal) ||
               context.Contains("agent evidence root", StringComparison.Ordinal) ||
               context.Contains("backup root", StringComparison.Ordinal);
    }

    internal static bool IsProhibitedExternalTargetReference(string text, int referenceIndex)
    {
        var start = Math.Max(0, referenceIndex - 180);
        var length = Math.Min(text.Length - start, 380);
        var context = CollapsePromptWhitespace(text.Substring(start, length)).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(context))
        {
            return false;
        }

        return ContainsProhibitionSignal(context) &&
               ContainsExternalTargetActionSignal(context);
    }

    internal static bool IsLikelyOutOfScopeExternalProductReference(
        string referencedAlias,
        IReadOnlyList<string> allowedAliases)
    {
        if (!referencedAlias.StartsWith(AliasRoot + "/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return allowedAliases.Any(allowedAlias =>
        {
            if (!allowedAlias.StartsWith(AliasRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (TryResolveExternalTargetParentAlias(allowedAlias, out var parentAlias) &&
                (string.Equals(referencedAlias, parentAlias, StringComparison.OrdinalIgnoreCase) ||
                 referencedAlias.StartsWith(parentAlias + "/", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            return CountCommonExternalTargetSegments(referencedAlias, allowedAlias) >= 3;
        });
    }

    internal static bool TryResolveExternalTargetParentAlias(string alias, out string parentAlias)
    {
        parentAlias = string.Empty;
        var normalizedAlias = NormalizeExternalTargetAlias(alias);
        var lastSlashIndex = normalizedAlias.LastIndexOf('/');
        if (lastSlashIndex <= AliasRoot.Length)
        {
            return false;
        }

        parentAlias = normalizedAlias[..lastSlashIndex];
        return parentAlias.StartsWith(AliasRoot + "/", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool TrySplitExternalTargetAliasForScaffold(
        string? mappedAlias,
        out string parentAlias,
        out string leafName)
    {
        parentAlias = string.Empty;
        leafName = string.Empty;
        if (string.IsNullOrWhiteSpace(mappedAlias))
        {
            return false;
        }

        var normalized = NormalizeExternalTargetAlias(mappedAlias);
        if (!normalized.StartsWith($"{AliasRoot}/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var lastSlashIndex = normalized.LastIndexOf('/');
        if (lastSlashIndex < AliasRoot.Length + 2 ||
            lastSlashIndex >= normalized.Length - 1)
        {
            return false;
        }

        parentAlias = normalized[..lastSlashIndex];
        leafName = normalized[(lastSlashIndex + 1)..];
        return !string.IsNullOrWhiteSpace(parentAlias) &&
               !string.IsNullOrWhiteSpace(leafName);
    }

    internal static int GetExternalTargetHintPriority(string path)
    {
        var alias = TryMapAbsoluteExternalPathToAlias(path, out var mappedAlias)
            ? mappedAlias
            : path.Replace('\\', '/');
        if (IsNonProductExternalTargetAlias(alias))
        {
            return -100;
        }

        var leaf = alias.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault() ?? string.Empty;
        return leaf switch
        {
            "product" => 100,
            "app" => 90,
            "output" => 85,
            "dist" => 85,
            "publish" => 85,
            "src" => 80,
            "source" => 80,
            _ => 10
        };
    }

    internal static bool IsNonProductExternalTargetAlias(string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return false;
        }

        var segments = alias.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Any(segment =>
            string.Equals(segment, "project-structure-backup", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "agent-evidence", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "api-snapshots", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "launch-plan", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "observation", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "process-definition", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "process-definition-corrected", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "project-structure-mutations", StringComparison.OrdinalIgnoreCase));
    }

    internal static bool IsExternalTargetAliasAncestor(string alias, string other)
        => other.StartsWith(alias + "/", StringComparison.OrdinalIgnoreCase);

    internal static bool IsLikelyExternalTargetFileAlias(string alias)
    {
        var lastSlashIndex = alias.LastIndexOf('/');
        if (lastSlashIndex < 0 || lastSlashIndex >= alias.Length - 1)
        {
            return false;
        }

        var leaf = alias[(lastSlashIndex + 1)..];
        return leaf.StartsWith(".", StringComparison.Ordinal) ||
               leaf.Contains('.');
    }

    internal static bool IsAliasCoveredByAny(string alias, IReadOnlyCollection<string> roots)
        => roots.Any(root =>
            string.Equals(alias, root, StringComparison.OrdinalIgnoreCase) ||
            alias.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase));

    private static ProcessExternalTargetCandidate? ResolveExternalTargetCandidate(string path)
    {
        if (!TryNormalizeAbsoluteExternalPathCandidate(path, out var normalizedPath) ||
            !TryMapAbsoluteExternalPathToAlias(normalizedPath, out var alias))
        {
            return null;
        }

        return new ProcessExternalTargetCandidate(normalizedPath, alias);
    }

    private static bool IsExternalTargetAlias(string rawPath)
        => rawPath.StartsWith(AliasRoot + "/", StringComparison.OrdinalIgnoreCase) ||
           rawPath.StartsWith(AliasRoot + "\\", StringComparison.OrdinalIgnoreCase);

    private static bool IsPreferredProductExternalTargetAlias(string alias)
    {
        var leaf = alias.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .LastOrDefault();
        return string.Equals(leaf, "product", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(leaf, "app", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(leaf, "source", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(leaf, "src", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAmbiguousExternalTargetPrefixAlias(string alias, string other)
    {
        if (!other.StartsWith(alias, StringComparison.OrdinalIgnoreCase) ||
            other.Length <= alias.Length)
        {
            return false;
        }

        var suffix = other[alias.Length..];
        return suffix[0] != '/' && suffix.Contains('/', StringComparison.Ordinal);
    }

    private static int CountCommonExternalTargetSegments(string left, string right)
    {
        var leftSegments = NormalizeExternalTargetAlias(left)
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var rightSegments = NormalizeExternalTargetAlias(right)
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var count = 0;
        while (count < leftSegments.Length &&
               count < rightSegments.Length &&
               string.Equals(leftSegments[count], rightSegments[count], StringComparison.OrdinalIgnoreCase))
        {
            count++;
        }

        return count;
    }

    private static IEnumerable<ExternalTargetPathCandidate> EnumerateAbsoluteExternalPathCandidates(string text)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in WorkspacePathInToolRequestRegex.Matches(text))
        {
            var path = match.Groups["path"].Value;
            if (!string.IsNullOrWhiteSpace(path) &&
                path.Length >= 3 &&
                path[1] == ':' &&
                path[2] == '\\' &&
                seen.Add(path))
            {
                yield return new ExternalTargetPathCandidate(path, match.Index);
            }
        }

        foreach (Match match in Regex.Matches(
                     text,
                     @"\b(?<path>[A-Za-z]:\\\\[^\r\n`""']+)",
                     RegexOptions.CultureInvariant))
        {
            var path = match.Groups["path"].Value.Replace(@"\\", @"\");
            if (!string.IsNullOrWhiteSpace(path) && seen.Add(path))
            {
                yield return new ExternalTargetPathCandidate(path, match.Index);
            }
        }

        foreach (Match match in Regex.Matches(
                     text,
                     @"\b(?<path>[A-Za-z]:\\[^\r\n`""']+)",
                     RegexOptions.CultureInvariant))
        {
            var path = match.Groups["path"].Value;
            if (!string.IsNullOrWhiteSpace(path) && seen.Add(path))
            {
                yield return new ExternalTargetPathCandidate(path, match.Index);
            }
        }
    }

    private static bool TryNormalizeAbsoluteExternalPathCandidate(
        string? path,
        out string normalizedPath)
    {
        normalizedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var trimmed = path.Trim().Trim('`', '"', '\'');
        trimmed = Regex.Replace(
            trimmed,
            @"\\{2,}",
            "\\",
            RegexOptions.CultureInvariant);
        trimmed = StripEscapedLineBreakPathAnnotations(trimmed);
        trimmed = StripInlinePathAnnotations(trimmed);
        trimmed = Regex.Replace(
            trimmed,
            @"(?i)(?:\.\s*(?:[-*]\s*)?|\s+(?:[-*]\s*)?)(?:Acceptance|Accepted|Architecture|Archetype|Backend|Deliverable|Escalation|Exact|Feature|Features|Hosting|Requirement|Requirements|Required|Evidence|Validation|Validate|Tests?|Startup|Browser|Agents?|Use|The|This|Then|Next|No-go|Include|Includes)\b.*$",
            string.Empty,
            RegexOptions.CultureInvariant);
        trimmed = Regex.Replace(
            trimmed,
            @"(?i)\s+(?:and|or)\s+(?:one|another|a|an|the|business|scenario|process|app|analysis|case)\b.*$",
            string.Empty,
            RegexOptions.CultureInvariant);
        trimmed = Regex.Replace(
            trimmed,
            @"(?i)\s+(?:with|without)\s+(?:stack|process|business|scenario|analysis|app|application|tooling|assumptions)\b.*$",
            string.Empty,
            RegexOptions.CultureInvariant);
        trimmed = trimmed.Trim().TrimEnd('\\', '/', '.', ',', ';', ':', ')', ']');

        if (trimmed.Length < 3 || trimmed[1] != ':' || trimmed[2] != '\\')
        {
            return false;
        }

        normalizedPath = NormalizeAbsoluteWindowsPathSegments(trimmed);
        return !string.IsNullOrWhiteSpace(normalizedPath);
    }

    private static string StripEscapedLineBreakPathAnnotations(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(
                value,
            @"(?i)[\\/](?:r[\\/]?)?n\s*(?:[-*]\s*)?(?:Acceptance|Accepted|Approved|Alias|Aliases|All|App|Application|Architecture|Archetype|Backend|Code|Deliverable|Directory|Escalation|Exact|Feature|Features|Files?|Generated|Hosting|Include|Includes|Mapped|Mapping|Node|No-go|Notes?|Output|Path|Product|Project|Requirement|Requirements|Required|Root|Source|Status|Workspace|Worksp|Evidence|Validation|Validate|Tests?|Startup|Browser|Agents?|Use|The|This|Then|Next)\b.*$",
                string.Empty,
                RegexOptions.CultureInvariant)
            .Trim();
    }

    private static string StripInlinePathAnnotations(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var stripped = Regex.Replace(
            value,
            @"(?i)(?:;\s*(?:notes?|type|status|subtitle|metadata|source|project|node|mapped)\b.*$|\s+(?:[-*]\s*)?(?:Acceptance|Accepted|Approved|Architecture|Archetype|Backend|Deliverable|Escalation|Exact|Feature|Features|Hosting|Requirement|Requirements|Required|Evidence|Validation|Validate|Tests?|Startup|Browser|Agents?|Use|The|This|Then|Next|No-go|Include|Includes)\b.*$|\s+\([a-z][a-z0-9_-]*:[^)]+\)?$|\s+\((?:maps?|mapped)\s+to\b.*$|\s+mapped\s+to\b.*$|\s+from\s+[^\\/]*$)",
            string.Empty,
            RegexOptions.CultureInvariant);
        stripped = Regex.Replace(
            stripped,
            @"(?i)\s+\([a-z][a-z0-9_-]*:[^)]+\)?$",
            string.Empty,
            RegexOptions.CultureInvariant);
        stripped = Regex.Replace(
            stripped,
            @"(?i)\.[\\/]+n(?:all|generated|app(?:lication)?|archetype|deliverable|exact|include|includes|no-go|source|code|files?|root|directory)\b.*$",
            string.Empty,
            RegexOptions.CultureInvariant);
        stripped = Regex.Replace(
            stripped,
            @"(?i)\.\s+(?:all|generated|app(?:lication)?|archetype|deliverable|exact|include|includes|no-go|source|code|files?|root|directory)\b.*$",
            string.Empty,
            RegexOptions.CultureInvariant);
        stripped = Regex.Replace(
            stripped,
            @"(?i)\s+(?:[-*]\s*)?(?:Workspace\s+alias|Mapped\s+alias|Business-analysis|Business\s+analysis|All\s+generated|All\s+app(?:lication)?|Generated\s+app(?:lication)?|App(?:lication)?\s+source|App\s+project\s+path|Expected\s+project\s+source\s+path|Expected\s+base\s+URL|Run\s+command|Source\s+root|Source\s+belongs|Code\s+belongs|Files?\s+belong|Output\s+directory|Acceptance|Approved|Archetype|Backend|Deliverable|Exact|Hosting|Include|Includes|No-go|Preservation\s+rule|Agents?\s+must|Use\s+only|Do\s+not|The\s+app|This\s+app)\b.*$",
            string.Empty,
            RegexOptions.CultureInvariant);
        stripped = Regex.Replace(
            stripped,
            @"(?i)\s+(?:and|or)\s*$",
            string.Empty,
            RegexOptions.CultureInvariant);
        stripped = Regex.Replace(
            stripped,
            @"(?i)\s+(?:and|or)\s+(?:one|another|a|an|the|business|scenario|process|app|analysis|case)\b.*$",
            string.Empty,
            RegexOptions.CultureInvariant);
        stripped = Regex.Replace(
            stripped,
            @"(?i)\s+(?:with|without)\s+(?:stack|process|business|scenario|analysis|app|application|tooling|assumptions)\b.*$",
            string.Empty,
            RegexOptions.CultureInvariant);
        return stripped.Trim();
    }

    private static string NormalizeAbsoluteWindowsPathSegments(string path)
    {
        if (path.Length < 3 || path[1] != ':' || path[2] != '\\')
        {
            return string.Empty;
        }

        var root = $"{char.ToUpperInvariant(path[0])}:\\";
        var segments = new List<string>();
        foreach (var segment in path[3..].Split('\\', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (segments.Count == 0)
                {
                    return string.Empty;
                }

                segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(segment);
        }

        return segments.Count == 0
            ? root
            : root + string.Join('\\', segments);
    }

    private static string NormalizeExternalTargetAliasSegments(string alias)
    {
        var segments = alias.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length < 2 ||
            !string.Equals(segments[0], AliasRoot, StringComparison.OrdinalIgnoreCase))
        {
            return alias;
        }

        var normalizedSegments = new List<string>();
        foreach (var segment in segments)
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                if (normalizedSegments.Count <= 2)
                {
                    return string.Empty;
                }

                normalizedSegments.RemoveAt(normalizedSegments.Count - 1);
                continue;
            }

            normalizedSegments.Add(segment);
        }

        return normalizedSegments.Count < 2
            ? string.Empty
            : string.Join('/', normalizedSegments);
    }

    private static string CollapseExternalTargetAliasSeparators(string value)
    {
        return Regex.Replace(
            value.Replace('\\', '/'),
            "/{2,}",
            "/",
            RegexOptions.CultureInvariant);
    }

    private static bool IsProhibitedGroundingCandidate(string text, int referenceIndex)
    {
        var lineStart = text.LastIndexOfAny(['\r', '\n'], Math.Max(0, referenceIndex));
        var start = lineStart < 0 ? 0 : lineStart + 1;
        var lineEnd = text.IndexOfAny(['\r', '\n'], referenceIndex);
        var end = lineEnd < 0 ? text.Length : lineEnd;
        if (end <= start)
        {
            return false;
        }

        var context = CollapsePromptWhitespace(text[start..end]).ToLowerInvariant();
        return ContainsProhibitionSignal(context) &&
               ContainsExternalTargetActionSignal(context);
    }

    private static bool ContainsProhibitionSignal(string context)
    {
        return context.Contains("do not", StringComparison.Ordinal) ||
               context.Contains("don't", StringComparison.Ordinal) ||
               context.Contains("must not", StringComparison.Ordinal) ||
               context.Contains("should not", StringComparison.Ordinal) ||
               context.Contains("never", StringComparison.Ordinal) ||
               context.Contains("prohibited", StringComparison.Ordinal) ||
               context.Contains("forbidden", StringComparison.Ordinal) ||
               context.Contains("no-go", StringComparison.Ordinal) ||
               context.Contains("out of scope", StringComparison.Ordinal) ||
               context.Contains("excluded", StringComparison.Ordinal);
    }

    private static bool ContainsExternalTargetActionSignal(string context)
    {
        return context.Contains("inspect", StringComparison.Ordinal) ||
               context.Contains("copy", StringComparison.Ordinal) ||
               context.Contains("read", StringComparison.Ordinal) ||
               context.Contains("cite", StringComparison.Ordinal) ||
               context.Contains("use", StringComparison.Ordinal) ||
               context.Contains("reuse", StringComparison.Ordinal) ||
               context.Contains("modify", StringComparison.Ordinal) ||
               context.Contains("write", StringComparison.Ordinal) ||
               context.Contains("target", StringComparison.Ordinal) ||
               context.Contains("sibling", StringComparison.Ordinal);
    }

    private static string CollapsePromptWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(
                value,
                @"\s+",
                " ",
                RegexOptions.CultureInvariant)
            .Trim();
    }

    private sealed record ExternalTargetPathCandidate(string Path, int Index);

    private sealed record ProcessExternalTargetCandidate(string AbsolutePath, string MappedAlias);
}

internal sealed record ProcessExternalTargetGroundingResult(
    bool HasTarget,
    string AbsolutePath,
    string MappedAlias,
    ProcessExternalTargetScaffoldTarget? ScaffoldTarget)
{
    public static ProcessExternalTargetGroundingResult Empty { get; } = new(false, string.Empty, string.Empty, null);
}

internal sealed record ProcessExternalTargetScaffoldTarget(string ParentAlias, string LeafName);

internal sealed record ProcessExternalTargetReferenceInspection(int OutOfScopeReferenceCount, string Summary)
{
    public static ProcessExternalTargetReferenceInspection Empty { get; } = new(0, string.Empty);

    public bool HasOutOfScopeReference => OutOfScopeReferenceCount > 0;
}
