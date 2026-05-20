namespace CanDoItAll.Modules.CognitiveMemory;

internal sealed record CognitiveMemoryClusterKeyWithRecord(
    Guid RecordId,
    CognitiveMemoryQualityClusterKeyFamily Family,
    string Key,
    string DisplayText,
    int SupportCount = 1,
    double CoverageRatio = 1);

internal sealed record CognitiveMemoryClusterRecordEntry(
    CognitiveMemoryRecord Record,
    CognitiveMemoryRecordSupport Support,
    IReadOnlyList<CognitiveMemoryClusterKeyWithRecord> Keys);

internal readonly record struct CognitiveMemoryClusterCandidatePair(
    CognitiveMemoryClusterRecordEntry Left,
    CognitiveMemoryClusterRecordEntry Right);

internal sealed record CognitiveMemoryClusterCandidatePairSelection(
    IReadOnlyDictionary<string, CognitiveMemoryClusterCandidatePair> Pairs,
    bool PairBudgetReached,
    int ExactPairsGenerated,
    int ApproximatePairsGenerated,
    int SkippedPairs);

internal sealed record CognitiveMemoryClusterSemanticSimilarity(
    double Score,
    IReadOnlyList<string> SharedSignals);

internal interface ICognitiveMemoryClusterKeyExtractor
{
    IReadOnlyList<CognitiveMemoryClusterKeyWithRecord> CreateKeys(
        CognitiveMemoryRecord record,
        CognitiveMemoryRecordSupport support,
        IReadOnlyList<string> relationKeys,
        IReadOnlyList<CognitiveMemoryQualityClusterKeyFamily> enabledFamilies);
}

internal interface ICognitiveMemoryCandidatePairSelector
{
    CognitiveMemoryClusterCandidatePairSelection SelectCandidatePairs(
        IReadOnlyList<CognitiveMemoryClusterRecordEntry> records,
        IReadOnlySet<string> requiredPairKeys,
        CognitiveMemoryClusterPlanningScope scope);
}

internal interface ICognitiveMemoryClusterSemanticSimilarityProvider
{
    IReadOnlySet<string> ExtractSignals(CognitiveMemoryClusterRecordEntry record);

    CognitiveMemoryClusterSemanticSimilarity Score(
        CognitiveMemoryClusterRecordEntry left,
        CognitiveMemoryClusterRecordEntry right);
}

internal sealed class CognitiveMemoryClusterKeyExtractor : ICognitiveMemoryClusterKeyExtractor
{
    public static readonly CognitiveMemoryClusterKeyExtractor Instance = new();

    private CognitiveMemoryClusterKeyExtractor()
    {
    }

    public IReadOnlyList<CognitiveMemoryClusterKeyWithRecord> CreateKeys(
        CognitiveMemoryRecord record,
        CognitiveMemoryRecordSupport support,
        IReadOnlyList<string> relationKeys,
        IReadOnlyList<CognitiveMemoryQualityClusterKeyFamily> enabledFamilies)
    {
        var keys = new List<CognitiveMemoryClusterKeyWithRecord>();
        void Add(CognitiveMemoryQualityClusterKeyFamily family, string key, string displayText)
        {
            if (!enabledFamilies.Contains(family) || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            keys.Add(new CognitiveMemoryClusterKeyWithRecord(record.Id, family, key, displayText));
        }

        Add(CognitiveMemoryQualityClusterKeyFamily.ProjectScope, $"project:{record.ProjectId?.ToString("D") ?? "global"}", "Project scope");
        foreach (var sourceItem in support.SourceItems)
        {
            Add(
                CognitiveMemoryQualityClusterKeyFamily.SourceTopology,
                $"source:{CognitiveMemoryQualityText.NormalizeKey(sourceItem.SourceSystem)}:{CognitiveMemoryQualityText.NormalizeKey(sourceItem.SourceItemType)}",
                $"{sourceItem.SourceSystem}/{sourceItem.SourceItemType}");
        }

        Add(
            CognitiveMemoryQualityClusterKeyFamily.SemanticTopic,
            $"topic:{CognitiveMemoryQualityText.NormalizeKey(FirstNonEmpty(record.TopicKey, record.Title))}",
            FirstNonEmpty(record.TopicKey, record.Title));
        foreach (var signal in CognitiveMemoryClusterTextSignals.ExtractSignals(
            $"{record.Title} {record.TopicKey} {record.CanonicalText} {record.SummaryText} {string.Join(' ', support.Claims.Select(claim => $"{claim.SubjectKey} {claim.ObjectKey}"))}",
            maxSignals: 28))
        {
            Add(CognitiveMemoryQualityClusterKeyFamily.Entity, $"entity:{signal}", signal);
        }

        foreach (var keyphrase in CognitiveMemoryClusterTextSignals.ExtractKeyphrases(
            $"{record.Title} {record.TopicKey} {record.CanonicalText} {record.SummaryText}",
            maxKeyphrases: 8))
        {
            Add(CognitiveMemoryQualityClusterKeyFamily.Entity, $"entity-phrase:{keyphrase}", keyphrase.Replace('.', ' '));
        }

        foreach (var intent in CognitiveMemoryQualityText.ResolveTaskIntents($"{record.Title} {record.CanonicalText} {record.SummaryText}"))
        {
            Add(CognitiveMemoryQualityClusterKeyFamily.TaskIntent, $"intent:{intent}", intent);
        }

        Add(
            CognitiveMemoryQualityClusterKeyFamily.Temporal,
            $"updated:{record.UpdatedAtUtc:yyyy-MM}",
            $"Updated {record.UpdatedAtUtc:yyyy-MM}");
        foreach (var evidenceKey in support.EvidenceAnchors
            .SelectMany(anchor => new[] { anchor.SourceHash, anchor.QuoteHash })
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Take(4))
        {
            Add(CognitiveMemoryQualityClusterKeyFamily.EvidenceOverlap, $"evidence:{evidenceKey}", "Evidence overlap");
        }

        foreach (var relationKey in relationKeys)
        {
            Add(CognitiveMemoryQualityClusterKeyFamily.Relation, relationKey, relationKey);
        }

        Add(
            CognitiveMemoryQualityClusterKeyFamily.AccessRisk,
            $"access:{record.AccessLevel}:risk:{record.RiskLevel}:redaction:{support.HighestRedactionState}",
            $"{record.AccessLevel}/{record.RiskLevel}/{support.HighestRedactionState}");
        return keys
            .GroupBy(key => new { key.Family, key.Key })
            .Select(group => group.First())
            .ToArray();
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}

internal sealed class CognitiveMemoryCandidatePairSelector : ICognitiveMemoryCandidatePairSelector
{
    public static readonly CognitiveMemoryCandidatePairSelector Default = new(
        CognitiveMemoryAliasClusterSemanticSimilarityProvider.Instance,
        new CognitiveMemoryQualityAlgorithmOptions());

    private readonly ICognitiveMemoryClusterSemanticSimilarityProvider semanticSimilarityProvider;
    private readonly CognitiveMemoryQualityClusterAlgorithmOptions options;

    public CognitiveMemoryCandidatePairSelector(
        ICognitiveMemoryClusterSemanticSimilarityProvider semanticSimilarityProvider,
        CognitiveMemoryQualityAlgorithmOptions? algorithmOptions = null)
    {
        this.semanticSimilarityProvider = semanticSimilarityProvider ?? throw new ArgumentNullException(nameof(semanticSimilarityProvider));
        options = (algorithmOptions ?? new CognitiveMemoryQualityAlgorithmOptions()).Cluster;
    }

    public CognitiveMemoryClusterCandidatePairSelection SelectCandidatePairs(
        IReadOnlyList<CognitiveMemoryClusterRecordEntry> records,
        IReadOnlySet<string> requiredPairKeys,
        CognitiveMemoryClusterPlanningScope scope)
    {
        var pairs = new CandidatePairAccumulator(options.MaxCandidatePairs, AllowsCrossProjectPairs(scope));
        var overFanoutGroups = new List<IReadOnlyList<CognitiveMemoryClusterRecordEntry>>();
        var indexedRecords = records
            .SelectMany(record => record.Keys
                .Where(IsCandidatePreselectionKey)
                .Select(key => new { Key = $"{key.Family}:{key.Key}", Record = record }))
            .GroupBy(entry => entry.Key, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal);

        foreach (var group in indexedRecords)
        {
            var groupRecords = group
                .Select(entry => entry.Record)
                .DistinctBy(entry => entry.Record.Id)
                .OrderBy(entry => entry.Record.Id)
                .ToArray();
            if (groupRecords.Length < 2)
            {
                continue;
            }

            if (groupRecords.Length > options.MaxCandidateKeyFanout)
            {
                overFanoutGroups.Add(groupRecords);
                continue;
            }

            if (!AddAllPairs(groupRecords, pairs, CognitiveMemoryCandidatePairDiscoveryKind.Exact))
            {
                return pairs.ToSelection();
            }
        }

        if (!AddRequiredPairs(records, requiredPairKeys, pairs))
        {
            return pairs.ToSelection();
        }

        if (!AddFallbackPairs(overFanoutGroups, pairs))
        {
            return pairs.ToSelection();
        }

        if (!AddApproximateSemanticPairs(records, pairs))
        {
            return pairs.ToSelection();
        }

        return pairs.ToSelection();
    }

    private static bool AddAllPairs(
        IReadOnlyList<CognitiveMemoryClusterRecordEntry> records,
        CandidatePairAccumulator pairs,
        CognitiveMemoryCandidatePairDiscoveryKind discoveryKind)
    {
        for (var leftIndex = 0; leftIndex < records.Count - 1; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < records.Count; rightIndex++)
            {
                if (!pairs.TryAdd(records[leftIndex], records[rightIndex], discoveryKind))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool AddRequiredPairs(
        IReadOnlyList<CognitiveMemoryClusterRecordEntry> records,
        IReadOnlySet<string> requiredPairKeys,
        CandidatePairAccumulator pairs)
    {
        if (requiredPairKeys.Count == 0)
        {
            return true;
        }

        var recordsById = records.ToDictionary(record => record.Record.Id);
        foreach (var pairKey in requiredPairKeys.OrderBy(pair => pair, StringComparer.Ordinal))
        {
            if (!TryParsePairKey(pairKey, out var leftRecordId, out var rightRecordId) ||
                !recordsById.TryGetValue(leftRecordId, out var left) ||
                !recordsById.TryGetValue(rightRecordId, out var right))
            {
                continue;
            }

            if (!pairs.TryAdd(left, right, CognitiveMemoryCandidatePairDiscoveryKind.Exact))
            {
                return false;
            }
        }

        return true;
    }

    private bool AddFallbackPairs(
        IReadOnlyList<IReadOnlyList<CognitiveMemoryClusterRecordEntry>> overFanoutGroups,
        CandidatePairAccumulator pairs)
    {
        var seenGroupSignatures = new HashSet<string>(StringComparer.Ordinal);
        foreach (var groupRecords in overFanoutGroups)
        {
            var signature = string.Join('|', groupRecords.Select(record => record.Record.Id).OrderBy(recordId => recordId));
            if (!seenGroupSignatures.Add(signature))
            {
                continue;
            }

            var signalEntries = groupRecords
                .Select(record => new
                {
                    Record = record,
                    Signals = semanticSimilarityProvider.ExtractSignals(record)
                })
                .ToArray();
            var rareSignalGroups = signalEntries
                .SelectMany(entry => entry.Signals.Select(signal => new { entry.Record, Signal = signal }))
                .GroupBy(entry => entry.Signal, StringComparer.Ordinal)
                .Where(group => group.Count() >= 2 && group.Count() <= options.MaxFallbackSignalFanout)
                .OrderBy(group => group.Key, StringComparer.Ordinal);
            foreach (var signalGroup in rareSignalGroups)
            {
                var records = signalGroup
                    .Select(entry => entry.Record)
                    .DistinctBy(record => record.Record.Id)
                    .OrderBy(record => record.Record.Id)
                    .ToArray();
                for (var leftIndex = 0; leftIndex < records.Length - 1; leftIndex++)
                {
                    for (var rightIndex = leftIndex + 1; rightIndex < records.Length; rightIndex++)
                    {
                        var left = records[leftIndex];
                        var right = records[rightIndex];
                        var similarity = semanticSimilarityProvider.Score(left, right);
                        if (similarity.Score < options.SemanticFallbackThreshold)
                        {
                            pairs.Skip();
                            continue;
                        }

                        if (!pairs.TryAdd(left, right, CognitiveMemoryCandidatePairDiscoveryKind.Approximate))
                        {
                            return false;
                        }
                    }
                }
            }
        }

        return true;
    }

    private bool AddApproximateSemanticPairs(
        IReadOnlyList<CognitiveMemoryClusterRecordEntry> records,
        CandidatePairAccumulator pairs)
    {
        var signalEntries = records
            .Select(record => new
            {
                Record = record,
                Signals = semanticSimilarityProvider.ExtractSignals(record)
            })
            .ToArray();
        var rareSignalGroups = signalEntries
            .SelectMany(entry => entry.Signals.Select(signal => new { entry.Record, Signal = signal }))
            .GroupBy(entry => entry.Signal, StringComparer.Ordinal)
            .Where(group => group.Count() >= 2 && group.Count() <= options.MaxFallbackSignalFanout)
            .OrderBy(group => group.Key, StringComparer.Ordinal);
        foreach (var signalGroup in rareSignalGroups)
        {
            var groupedRecords = signalGroup
                .Select(entry => entry.Record)
                .DistinctBy(record => record.Record.Id)
                .OrderBy(record => record.Record.Id)
                .ToArray();
            for (var leftIndex = 0; leftIndex < groupedRecords.Length - 1; leftIndex++)
            {
                for (var rightIndex = leftIndex + 1; rightIndex < groupedRecords.Length; rightIndex++)
                {
                    var left = groupedRecords[leftIndex];
                    var right = groupedRecords[rightIndex];
                    if (pairs.Contains(left, right))
                    {
                        continue;
                    }

                    var similarity = semanticSimilarityProvider.Score(left, right);
                    if (similarity.Score < options.SemanticFallbackThreshold)
                    {
                        pairs.Skip();
                        continue;
                    }

                    if (!pairs.TryAdd(left, right, CognitiveMemoryCandidatePairDiscoveryKind.Approximate))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private static bool IsCandidatePreselectionKey(CognitiveMemoryClusterKeyWithRecord key)
    {
        if (key.Family == CognitiveMemoryQualityClusterKeyFamily.TaskIntent &&
            string.Equals(key.Key, "intent:general", StringComparison.Ordinal))
        {
            return false;
        }

        return key.Family is CognitiveMemoryQualityClusterKeyFamily.SemanticTopic
            or CognitiveMemoryQualityClusterKeyFamily.Entity
            or CognitiveMemoryQualityClusterKeyFamily.TaskIntent
            or CognitiveMemoryQualityClusterKeyFamily.EvidenceOverlap
            or CognitiveMemoryQualityClusterKeyFamily.Relation;
    }

    private static bool TryParsePairKey(string pairKey, out Guid leftRecordId, out Guid rightRecordId)
    {
        leftRecordId = Guid.Empty;
        rightRecordId = Guid.Empty;
        var parts = pairKey.Split(':', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2 &&
               Guid.TryParse(parts[0], out leftRecordId) &&
               Guid.TryParse(parts[1], out rightRecordId);
    }

    private static string NormalizePair(Guid first, Guid second)
        => first.CompareTo(second) <= 0
            ? $"{first:D}:{second:D}"
            : $"{second:D}:{first:D}";

    private static bool AllowsCrossProjectPairs(CognitiveMemoryClusterPlanningScope scope)
        => scope is CognitiveMemoryClusterPlanningScope.CrossProject
            or CognitiveMemoryClusterPlanningScope.PolicyConstrainedCrossProject;

    private enum CognitiveMemoryCandidatePairDiscoveryKind
    {
        Exact = 0,
        Approximate = 1
    }

    private sealed class CandidatePairAccumulator
    {
        private readonly Dictionary<string, CognitiveMemoryClusterCandidatePair> pairs = new(StringComparer.Ordinal);
        private readonly int maxCandidatePairs;
        private readonly bool allowCrossProjectPairs;

        public CandidatePairAccumulator(
            int maxCandidatePairs,
            bool allowCrossProjectPairs)
        {
            this.maxCandidatePairs = maxCandidatePairs;
            this.allowCrossProjectPairs = allowCrossProjectPairs;
        }

        public bool PairBudgetReached { get; private set; }

        public int ExactPairsGenerated { get; private set; }

        public int ApproximatePairsGenerated { get; private set; }

        public int SkippedPairs { get; private set; }

        public bool Contains(
            CognitiveMemoryClusterRecordEntry left,
            CognitiveMemoryClusterRecordEntry right)
            => pairs.ContainsKey(NormalizePair(left.Record.Id, right.Record.Id));

        public void Skip() => SkippedPairs++;

        public bool TryAdd(
            CognitiveMemoryClusterRecordEntry left,
            CognitiveMemoryClusterRecordEntry right,
            CognitiveMemoryCandidatePairDiscoveryKind discoveryKind)
        {
            if (!allowCrossProjectPairs && left.Record.ProjectId != right.Record.ProjectId)
            {
                SkippedPairs++;
                return true;
            }

            var key = NormalizePair(left.Record.Id, right.Record.Id);
            if (pairs.ContainsKey(key))
            {
                SkippedPairs++;
                return true;
            }

            if (pairs.Count >= maxCandidatePairs)
            {
                PairBudgetReached = true;
                SkippedPairs++;
                return false;
            }

            pairs.Add(key, new CognitiveMemoryClusterCandidatePair(left, right));
            if (discoveryKind == CognitiveMemoryCandidatePairDiscoveryKind.Exact)
            {
                ExactPairsGenerated++;
            }
            else
            {
                ApproximatePairsGenerated++;
            }

            PairBudgetReached = pairs.Count >= maxCandidatePairs;
            return !PairBudgetReached;
        }

        public CognitiveMemoryClusterCandidatePairSelection ToSelection()
            => new(
                pairs,
                PairBudgetReached,
                ExactPairsGenerated,
                ApproximatePairsGenerated,
                SkippedPairs);
    }
}

internal sealed class CognitiveMemoryAliasClusterSemanticSimilarityProvider : ICognitiveMemoryClusterSemanticSimilarityProvider
{
    public static readonly CognitiveMemoryAliasClusterSemanticSimilarityProvider Instance = new();

    private CognitiveMemoryAliasClusterSemanticSimilarityProvider()
    {
    }

    public IReadOnlySet<string> ExtractSignals(CognitiveMemoryClusterRecordEntry record)
        => CognitiveMemoryClusterSemanticSignals
            .ExtractSignals($"{record.Record.Title} {record.Record.TopicKey} {record.Record.CanonicalText} {record.Record.SummaryText}", maxSignals: 36)
            .ToHashSet(StringComparer.Ordinal);

    public CognitiveMemoryClusterSemanticSimilarity Score(
        CognitiveMemoryClusterRecordEntry left,
        CognitiveMemoryClusterRecordEntry right)
    {
        var leftSignals = ExtractSignals(left);
        var sharedSignals = ExtractSignals(right)
            .Where(leftSignals.Contains)
            .OrderBy(signal => signal, StringComparer.Ordinal)
            .ToArray();
        var score = sharedSignals.Length switch
        {
            >= 5 => 0.9,
            4 => 0.78,
            3 => 0.66,
            2 => 0.45,
            1 => 0.25,
            _ => 0
        };
        return new CognitiveMemoryClusterSemanticSimilarity(score, sharedSignals);
    }
}

internal static class CognitiveMemoryClusterSemanticSignals
{
    private static readonly IReadOnlySet<string> NonSemanticSignals = new HashSet<string>([
        "about",
        "after",
        "before",
        "both",
        "evidence",
        "material",
        "note",
        "notes",
        "ordinary",
        "record",
        "records",
        "require",
        "requires",
        "source",
        "status",
        "until"
    ], StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, string> SemanticAliases = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["archived"] = "recorded",
        ["archive"] = "recorded",
        ["blocked"] = "blocked",
        ["blocker"] = "blocked",
        ["cannot"] = "blocked",
        ["certificate"] = "compliance-certification",
        ["conformity"] = "compliance-certification",
        ["filed"] = "recorded",
        ["gateway"] = "release-gate",
        ["paperwork"] = "compliance-certification",
        ["ship"] = "release"
    };

    public static IReadOnlyList<string> ExtractSignals(string text, int maxSignals)
        => CognitiveMemoryQualityText.ExtractMeaningfulTokens(text, maxTokens: maxSignals * 3)
            .Select(NormalizeSignal)
            .Where(signal => signal.Length >= 4 && !NonSemanticSignals.Contains(signal))
            .Distinct(StringComparer.Ordinal)
            .Take(maxSignals)
            .ToArray();

    private static string NormalizeSignal(string token)
    {
        var clusterSignal = CognitiveMemoryClusterTextSignals.NormalizeSignal(token);
        return SemanticAliases.TryGetValue(clusterSignal, out var alias)
            ? alias
            : clusterSignal;
    }
}

internal static class CognitiveMemoryClusterTextSignals
{
    private static readonly IReadOnlySet<string> NonSemanticSignals = new HashSet<string>([
        "evidence",
        "material",
        "note",
        "notes",
        "ordinary",
        "before",
        "both",
        "record",
        "records",
        "require",
        "requires",
        "source",
        "status"
    ], StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["activate"] = "activation",
        ["activated"] = "activation",
        ["activating"] = "activation",
        ["artifact"] = "artifact",
        ["artifacts"] = "artifact",
        ["canary"] = "release",
        ["deploy"] = "release",
        ["deployed"] = "release",
        ["deployment"] = "release",
        ["deployments"] = "release",
        ["freeze"] = "pause",
        ["freezes"] = "pause",
        ["package"] = "artifact",
        ["packages"] = "artifact",
        ["pause"] = "pause",
        ["pauses"] = "pause",
        ["postgres"] = "postgres",
        ["promote"] = "activation",
        ["promoting"] = "activation",
        ["promotion"] = "activation",
        ["request"] = "traffic",
        ["requests"] = "traffic",
        ["rollout"] = "release",
        ["traffic"] = "traffic"
    };

    public static IReadOnlyList<string> ExtractSignals(string text, int maxSignals)
        => CognitiveMemoryQualityText.ExtractMeaningfulTokens(text, maxTokens: maxSignals * 2)
            .Select(NormalizeSignal)
            .Where(signal => signal.Length >= 4 && !NonSemanticSignals.Contains(signal))
            .Distinct(StringComparer.Ordinal)
            .Take(maxSignals)
            .ToArray();

    public static IReadOnlyList<string> ExtractKeyphrases(string text, int maxKeyphrases)
    {
        var signals = ExtractSignals(text, maxSignals: maxKeyphrases * 3);
        return signals
            .Zip(signals.Skip(1), (left, right) => left == right ? string.Empty : $"{left}.{right}")
            .Where(phrase => !string.IsNullOrWhiteSpace(phrase))
            .Distinct(StringComparer.Ordinal)
            .Take(maxKeyphrases)
            .ToArray();
    }

    public static string NormalizeSignal(string token)
    {
        var normalized = CognitiveMemoryQualityText.NormalizeKey(token).Replace(".", string.Empty, StringComparison.Ordinal);
        if (Aliases.TryGetValue(normalized, out var alias))
        {
            return alias;
        }

        if (normalized.EndsWith("ies", StringComparison.Ordinal) && normalized.Length > 5)
        {
            return $"{normalized[..^3]}y";
        }

        if (normalized.EndsWith("s", StringComparison.Ordinal) && normalized.Length > 5)
        {
            return normalized[..^1];
        }

        return normalized;
    }
}
