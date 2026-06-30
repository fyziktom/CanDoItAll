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
    CognitiveMemoryClusterRecordEntry Right,
    CognitiveMemoryCandidatePairDiscoveryKind DiscoveryKind = CognitiveMemoryCandidatePairDiscoveryKind.Exact,
    double SimilarityScore = 0,
    string Explanation = "");

internal enum CognitiveMemoryCandidatePairDiscoveryKind
{
    Exact = 0,
    LexicalApproximate = 1,
    EmbeddingApproximate = 2
}

internal sealed record CognitiveMemoryClusterCandidatePairSelection(
    IReadOnlyDictionary<string, CognitiveMemoryClusterCandidatePair> Pairs,
    bool PairBudgetReached,
    int ExactPairsGenerated,
    int ApproximatePairsGenerated,
    int SkippedPairs);

internal sealed record CognitiveMemoryApproximateClusterCandidateRequest(
    IReadOnlyList<CognitiveMemoryClusterRecordEntry> Records,
    IReadOnlyList<IReadOnlyList<CognitiveMemoryClusterRecordEntry>> FanoutGroups,
    CognitiveMemoryClusterPlanningScope Scope,
    int MaxPairs,
    string? ContinuationCursor = null,
    string EmbeddingProfileId = "lexical-signal-embedding-v1");

internal sealed record CognitiveMemoryApproximateClusterCandidateResult(
    IReadOnlyList<CognitiveMemoryClusterCandidatePair> Pairs,
    int SkippedPairs,
    string? ContinuationCursor,
    int ApproximateCandidatePairsGenerated);

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
    ValueTask<CognitiveMemoryClusterCandidatePairSelection> SelectCandidatePairsAsync(
        IReadOnlyList<CognitiveMemoryClusterRecordEntry> records,
        IReadOnlySet<string> requiredPairKeys,
        CognitiveMemoryClusterPlanningScope scope,
        CancellationToken cancellationToken = default);
}

internal interface ICognitiveMemoryApproximateClusterCandidateProvider
{
    ValueTask<CognitiveMemoryApproximateClusterCandidateResult> FindApproximatePairsAsync(
        CognitiveMemoryApproximateClusterCandidateRequest request,
        CancellationToken cancellationToken = default);
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

internal sealed class CognitiveMemoryLexicalApproximateClusterCandidateProvider : ICognitiveMemoryApproximateClusterCandidateProvider
{
    private readonly ICognitiveMemoryClusterSemanticSimilarityProvider semanticSimilarityProvider;
    private readonly CognitiveMemoryQualityClusterAlgorithmOptions options;

    public CognitiveMemoryLexicalApproximateClusterCandidateProvider(
        ICognitiveMemoryClusterSemanticSimilarityProvider semanticSimilarityProvider,
        CognitiveMemoryQualityAlgorithmOptions? algorithmOptions = null)
    {
        this.semanticSimilarityProvider = semanticSimilarityProvider ?? throw new ArgumentNullException(nameof(semanticSimilarityProvider));
        options = (algorithmOptions ?? CognitiveMemoryQualityAlgorithmOptions.Current).Cluster;
    }

    public ValueTask<CognitiveMemoryApproximateClusterCandidateResult> FindApproximatePairsAsync(
        CognitiveMemoryApproximateClusterCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        var pairs = new List<CognitiveMemoryClusterCandidatePair>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var skipped = 0;
        foreach (var fanoutGroup in request.FanoutGroups)
        {
            AddSemanticSignalPairs(fanoutGroup, request.Scope, request.MaxPairs, pairs, seen, ref skipped);
            if (pairs.Count >= request.MaxPairs)
            {
                return ValueTask.FromResult(ToResult(request.ContinuationCursor, pairs, skipped));
            }
        }

        AddSemanticSignalPairs(request.Records, request.Scope, request.MaxPairs, pairs, seen, ref skipped);
        return ValueTask.FromResult(ToResult(request.ContinuationCursor, pairs, skipped));
    }

    private void AddSemanticSignalPairs(
        IReadOnlyList<CognitiveMemoryClusterRecordEntry> records,
        CognitiveMemoryClusterPlanningScope scope,
        int maxPairs,
        List<CognitiveMemoryClusterCandidatePair> pairs,
        HashSet<string> seen,
        ref int skipped)
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
                    if (!AllowsCrossProjectPairs(scope) && left.Record.ProjectId != right.Record.ProjectId)
                    {
                        skipped++;
                        continue;
                    }

                    var pairKey = NormalizePair(left.Record.Id, right.Record.Id);
                    if (!seen.Add(pairKey))
                    {
                        skipped++;
                        continue;
                    }

                    var similarity = semanticSimilarityProvider.Score(left, right);
                    if (similarity.Score < options.SemanticFallbackThreshold)
                    {
                        skipped++;
                        continue;
                    }

                    pairs.Add(new CognitiveMemoryClusterCandidatePair(
                        left,
                        right,
                        CognitiveMemoryCandidatePairDiscoveryKind.LexicalApproximate,
                        similarity.Score,
                        $"lexical:{string.Join(',', similarity.SharedSignals.Take(6))}"));
                    if (pairs.Count >= maxPairs)
                    {
                        return;
                    }
                }
            }
        }
    }

    private static CognitiveMemoryApproximateClusterCandidateResult ToResult(
        string? continuationCursor,
        IReadOnlyList<CognitiveMemoryClusterCandidatePair> pairs,
        int skipped)
        => new(pairs, skipped, continuationCursor, pairs.Count);

    private static string NormalizePair(Guid first, Guid second)
        => first.CompareTo(second) <= 0
            ? $"{first:D}:{second:D}"
            : $"{second:D}:{first:D}";

    private static bool AllowsCrossProjectPairs(CognitiveMemoryClusterPlanningScope scope)
        => scope is CognitiveMemoryClusterPlanningScope.CrossProject
            or CognitiveMemoryClusterPlanningScope.PolicyConstrainedCrossProject;
}

internal sealed class CognitiveMemoryEmbeddingApproximateClusterCandidateProvider : ICognitiveMemoryApproximateClusterCandidateProvider
{
    private readonly ICognitiveMemoryEmbeddingProvider embeddingProvider;
    private readonly ICognitiveMemoryApproximateClusterCandidateProvider lexicalFallbackProvider;
    private readonly CognitiveMemoryQualityClusterAlgorithmOptions options;

    public CognitiveMemoryEmbeddingApproximateClusterCandidateProvider(
        ICognitiveMemoryEmbeddingProvider embeddingProvider,
        ICognitiveMemoryClusterSemanticSimilarityProvider lexicalSimilarityProvider,
        CognitiveMemoryQualityAlgorithmOptions? algorithmOptions = null)
        : this(
            embeddingProvider,
            new CognitiveMemoryLexicalApproximateClusterCandidateProvider(lexicalSimilarityProvider, algorithmOptions),
            algorithmOptions)
    {
    }

    public CognitiveMemoryEmbeddingApproximateClusterCandidateProvider(
        ICognitiveMemoryEmbeddingProvider embeddingProvider,
        ICognitiveMemoryApproximateClusterCandidateProvider lexicalFallbackProvider,
        CognitiveMemoryQualityAlgorithmOptions? algorithmOptions = null)
    {
        this.embeddingProvider = embeddingProvider ?? throw new ArgumentNullException(nameof(embeddingProvider));
        this.lexicalFallbackProvider = lexicalFallbackProvider ?? throw new ArgumentNullException(nameof(lexicalFallbackProvider));
        options = (algorithmOptions ?? CognitiveMemoryQualityAlgorithmOptions.Current).Cluster;
    }

    public async ValueTask<CognitiveMemoryApproximateClusterCandidateResult> FindApproximatePairsAsync(
        CognitiveMemoryApproximateClusterCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            return await FindEmbeddingPairsAsync(request, cancellationToken);
        }
        catch (InvalidOperationException exception) when (IsUnavailableEmbeddingProvider(exception))
        {
            return await lexicalFallbackProvider.FindApproximatePairsAsync(request, cancellationToken);
        }
    }

    private async ValueTask<CognitiveMemoryApproximateClusterCandidateResult> FindEmbeddingPairsAsync(
        CognitiveMemoryApproximateClusterCandidateRequest request,
        CancellationToken cancellationToken)
    {
        var pairs = new List<CognitiveMemoryClusterCandidatePair>();
        var skipped = 0;
        var embeddingProfileId = new CognitiveMemoryEmbeddingProfileId(
            string.IsNullOrWhiteSpace(request.EmbeddingProfileId)
                ? options.EmbeddingProfileId
                : request.EmbeddingProfileId);
        var embeddings = await EmbedRecordsAsync(request.Records, embeddingProfileId, cancellationToken);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in request.FanoutGroups.Append(request.Records))
        {
            AddEmbeddingPairs(group, request.Scope, request.MaxPairs, embeddings, pairs, seen, ref skipped);
            if (pairs.Count >= request.MaxPairs)
            {
                break;
            }
        }

        return new CognitiveMemoryApproximateClusterCandidateResult(
            pairs,
            skipped,
            request.ContinuationCursor,
            pairs.Count);
    }

    private async Task<IReadOnlyDictionary<Guid, CognitiveMemoryVector>> EmbedRecordsAsync(
        IReadOnlyList<CognitiveMemoryClusterRecordEntry> records,
        CognitiveMemoryEmbeddingProfileId embeddingProfileId,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, CognitiveMemoryVector>();
        foreach (var record in records.OrderBy(record => record.Record.Id))
        {
            var embedding = await embeddingProvider.EmbedAsync(
                new CognitiveMemoryEmbeddingRequest(
                    embeddingProfileId,
                    BuildEmbeddingInput(record),
                    new CognitiveMemoryProcessingBudget(1, 4096, TimeSpan.FromSeconds(10))),
                cancellationToken);
            result[record.Record.Id] = embedding.Vector;
        }

        return result;
    }

    private void AddEmbeddingPairs(
        IReadOnlyList<CognitiveMemoryClusterRecordEntry> records,
        CognitiveMemoryClusterPlanningScope scope,
        int maxPairs,
        IReadOnlyDictionary<Guid, CognitiveMemoryVector> embeddings,
        List<CognitiveMemoryClusterCandidatePair> pairs,
        HashSet<string> seen,
        ref int skipped)
    {
        var ordered = records
            .DistinctBy(record => record.Record.Id)
            .OrderBy(record => record.Record.Id)
            .ToArray();
        for (var leftIndex = 0; leftIndex < ordered.Length - 1; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < ordered.Length; rightIndex++)
            {
                var left = ordered[leftIndex];
                var right = ordered[rightIndex];
                if (!AllowsCrossProjectPairs(scope) && left.Record.ProjectId != right.Record.ProjectId)
                {
                    skipped++;
                    continue;
                }

                if (BlocksEmbeddingApproximation(scope, left, right))
                {
                    skipped++;
                    continue;
                }

                var pairKey = NormalizePair(left.Record.Id, right.Record.Id);
                if (!seen.Add(pairKey) ||
                    !embeddings.TryGetValue(left.Record.Id, out var leftVector) ||
                    !embeddings.TryGetValue(right.Record.Id, out var rightVector))
                {
                    skipped++;
                    continue;
                }

                var similarity = CosineSimilarity(leftVector, rightVector);
                if (similarity < options.EmbeddingSimilarityThreshold)
                {
                    skipped++;
                    continue;
                }

                pairs.Add(new CognitiveMemoryClusterCandidatePair(
                    left,
                    right,
                    CognitiveMemoryCandidatePairDiscoveryKind.EmbeddingApproximate,
                    similarity,
                    $"embedding:{similarity:0.###}"));
                if (pairs.Count >= maxPairs)
                {
                    return;
                }
            }
        }
    }

    private static bool BlocksEmbeddingApproximation(
        CognitiveMemoryClusterPlanningScope scope,
        CognitiveMemoryClusterRecordEntry left,
        CognitiveMemoryClusterRecordEntry right)
    {
        if (scope == CognitiveMemoryClusterPlanningScope.PolicyConstrainedCrossProject &&
            left.Record.ProjectId != right.Record.ProjectId &&
            (left.Record.AccessLevel == CognitiveMemoryAccessLevel.Restricted ||
             right.Record.AccessLevel == CognitiveMemoryAccessLevel.Restricted ||
             left.Support.HighestRedactionState is CognitiveMemoryRedactionState.Redacted or CognitiveMemoryRedactionState.Restricted ||
             right.Support.HighestRedactionState is CognitiveMemoryRedactionState.Redacted or CognitiveMemoryRedactionState.Restricted))
        {
            return true;
        }

        return left.Record.AccessLevel != right.Record.AccessLevel ||
               left.Support.HighestRedactionState != right.Support.HighestRedactionState;
    }

    private static double CosineSimilarity(
        CognitiveMemoryVector left,
        CognitiveMemoryVector right)
    {
        if (left.Length != right.Length)
        {
            return 0;
        }

        var leftValues = left.Values.Span;
        var rightValues = right.Values.Span;
        var dot = 0d;
        var leftMagnitude = 0d;
        var rightMagnitude = 0d;
        for (var index = 0; index < leftValues.Length; index++)
        {
            dot += leftValues[index] * rightValues[index];
            leftMagnitude += leftValues[index] * leftValues[index];
            rightMagnitude += rightValues[index] * rightValues[index];
        }

        if (leftMagnitude <= 0 || rightMagnitude <= 0)
        {
            return 0;
        }

        return dot / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
    }

    private static string BuildEmbeddingInput(CognitiveMemoryClusterRecordEntry record)
        => string.Join(
            Environment.NewLine,
            new[]
            {
                record.Record.Title,
                record.Record.TopicKey,
                record.Record.CanonicalText,
                record.Record.SummaryText,
                string.Join(' ', record.Support.Claims.Select(claim => claim.ClaimText))
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

    private static bool IsUnavailableEmbeddingProvider(InvalidOperationException exception)
        => exception.Message.Contains("embedding", StringComparison.OrdinalIgnoreCase) &&
           exception.Message.Contains("provider", StringComparison.OrdinalIgnoreCase);

    private static string NormalizePair(Guid first, Guid second)
        => first.CompareTo(second) <= 0
            ? $"{first:D}:{second:D}"
            : $"{second:D}:{first:D}";

    private static bool AllowsCrossProjectPairs(CognitiveMemoryClusterPlanningScope scope)
        => scope is CognitiveMemoryClusterPlanningScope.CrossProject
            or CognitiveMemoryClusterPlanningScope.PolicyConstrainedCrossProject;
}

internal sealed class CognitiveMemoryCandidatePairSelector : ICognitiveMemoryCandidatePairSelector
{
    public static readonly CognitiveMemoryCandidatePairSelector Default = new(
        CognitiveMemoryAliasClusterSemanticSimilarityProvider.Instance,
        CognitiveMemoryQualityAlgorithmOptions.Current);

    private readonly ICognitiveMemoryApproximateClusterCandidateProvider approximateCandidateProvider;
    private readonly CognitiveMemoryQualityClusterAlgorithmOptions options;

    public CognitiveMemoryCandidatePairSelector(
        ICognitiveMemoryClusterSemanticSimilarityProvider semanticSimilarityProvider,
        CognitiveMemoryQualityAlgorithmOptions? algorithmOptions = null,
        ICognitiveMemoryApproximateClusterCandidateProvider? approximateCandidateProvider = null)
    {
        ArgumentNullException.ThrowIfNull(semanticSimilarityProvider);
        var resolvedOptions = algorithmOptions ?? CognitiveMemoryQualityAlgorithmOptions.Current;
        options = resolvedOptions.Cluster;
        this.approximateCandidateProvider = approximateCandidateProvider ??
            new CognitiveMemoryLexicalApproximateClusterCandidateProvider(semanticSimilarityProvider, resolvedOptions);
    }

    public async ValueTask<CognitiveMemoryClusterCandidatePairSelection> SelectCandidatePairsAsync(
        IReadOnlyList<CognitiveMemoryClusterRecordEntry> records,
        IReadOnlySet<string> requiredPairKeys,
        CognitiveMemoryClusterPlanningScope scope,
        CancellationToken cancellationToken = default)
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

        if (!await AddApproximatePairsAsync(records, overFanoutGroups, scope, pairs, cancellationToken))
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

    private async ValueTask<bool> AddApproximatePairsAsync(
        IReadOnlyList<CognitiveMemoryClusterRecordEntry> records,
        IReadOnlyList<IReadOnlyList<CognitiveMemoryClusterRecordEntry>> overFanoutGroups,
        CognitiveMemoryClusterPlanningScope scope,
        CandidatePairAccumulator pairs,
        CancellationToken cancellationToken)
    {
        var result = await approximateCandidateProvider.FindApproximatePairsAsync(
            new CognitiveMemoryApproximateClusterCandidateRequest(
                records,
                overFanoutGroups,
                scope,
                options.MaxCandidatePairs,
                ContinuationCursor: null,
                EmbeddingProfileId: options.EmbeddingProfileId),
            cancellationToken);
        pairs.Skip(result.SkippedPairs);
        foreach (var pair in result.Pairs)
        {
            if (pairs.Contains(pair.Left, pair.Right))
            {
                pairs.Skip();
                continue;
            }

            if (!pairs.TryAdd(pair))
            {
                return false;
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

        public void Skip(int count)
        {
            if (count > 0)
            {
                SkippedPairs += count;
            }
        }

        public bool TryAdd(
            CognitiveMemoryClusterRecordEntry left,
            CognitiveMemoryClusterRecordEntry right,
            CognitiveMemoryCandidatePairDiscoveryKind discoveryKind)
            => TryAdd(new CognitiveMemoryClusterCandidatePair(left, right, discoveryKind));

        public bool TryAdd(CognitiveMemoryClusterCandidatePair pair)
        {
            var left = pair.Left;
            var right = pair.Right;
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

            pairs.Add(key, pair);
            if (pair.DiscoveryKind == CognitiveMemoryCandidatePairDiscoveryKind.Exact)
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
