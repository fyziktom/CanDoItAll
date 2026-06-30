using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;
public sealed class CognitiveMemoryRecallSynthesisService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    ICognitiveMemoryRecallBriefComposer? briefComposer = null) : ICognitiveMemoryRecallSynthesisService
{
    private readonly ICognitiveMemoryRecallBriefComposer briefComposer = briefComposer ?? new CognitiveMemoryRecallBriefComposer();

    public async ValueTask<CognitiveMemorySynthesizedRecallResult> SynthesizeAsync(
        CognitiveMemoryRecallSynthesisRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.MaxStatements <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Recall synthesis statement budget must be positive.");
        }

        var selectedSections = request.RecallResult.ContextPack.Sections
            .Where(section => section.SectionKind == CognitiveMemoryRecallContextSectionKind.SelectedMemory)
            .ToArray();
        var warnings = new List<string>();
        if (selectedSections.Length == 0)
        {
            warnings.Add("Recall synthesis received no selected memory sections.");
        }

        var aggregateClaimIds = await LoadAggregateClaimIdsAsync(selectedSections, cancellationToken);
        var queryText = ResolveSynthesisQueryText(request);
        var composition = briefComposer.Compose(new CognitiveMemoryRecallBriefComposerRequest(
            QueryText: queryText,
            SelectedSections: selectedSections,
            AggregateClaimIds: aggregateClaimIds,
            PolicyContext: request.PolicyContext,
            MaxStatements: request.MaxStatements,
            Intent: request.Intent));
        warnings.AddRange(composition.Warnings);
        var statements = composition.Statements;
        var brief = composition.Brief;
        var synthesisId = CognitiveMemorySynthesizedRecallId.New();

        if (request.PersistSynthesis)
        {
            await PersistAsync(request, synthesisId, brief, statements, cancellationToken);
        }

        return new CognitiveMemorySynthesizedRecallResult(
            synthesisId,
            request.RecallResult.ContextPack.ProjectId,
            request.RecallResult.TraceId,
            brief,
            statements,
            ReferencesShownByDefault: false,
            warnings);
    }

    private async Task PersistAsync(
        CognitiveMemoryRecallSynthesisRequest request,
        CognitiveMemorySynthesizedRecallId synthesisId,
        string brief,
        IReadOnlyList<CognitiveMemorySynthesizedRecallStatement> statements,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var traceExists = await dbContext.Set<CognitiveMemoryRecallTraceRecord>()
            .AnyAsync(trace => trace.Id == request.RecallResult.TraceId, cancellationToken);
        if (!traceExists)
        {
            throw new InvalidOperationException($"Recall trace '{request.RecallResult.TraceId:D}' was not found for synthesis persistence.");
        }

        var nowUtc = clock.GetUtcNow();
        dbContext.Add(new CognitiveMemorySynthesizedRecallRecord
        {
            Id = synthesisId.Value,
            ProjectId = request.RecallResult.ContextPack.ProjectId,
            RecallTraceId = request.RecallResult.TraceId,
            Brief = brief,
            ReferencesShownByDefault = false,
            StatementCount = statements.Count,
            SourceMapCount = statements.Sum(CountPersistedSourceMaps),
            CreatedAtUtc = nowUtc,
            ConcurrencyToken = Guid.NewGuid()
        });

        var sequence = 0;
        foreach (var statement in statements)
        {
            dbContext.Add(new CognitiveMemorySynthesizedStatementRecord
            {
                Id = statement.StatementId.Value,
                SynthesisId = synthesisId.Value,
                ProjectId = request.RecallResult.ContextPack.ProjectId,
                Sequence = sequence,
                Text = statement.Text,
                CreatedAtUtc = nowUtc
            });
            var aggregateClaimIds = statement.AggregateClaimIds.Count == 0
                ? new Guid?[] { null }
                : statement.AggregateClaimIds
                    .Select(claimId => (Guid?)claimId.Value)
                    .Distinct()
                    .ToArray();
            var seenSourceMaps = new HashSet<(Guid MemoryRecordId, Guid? AggregateClaimId, Guid? SourceItemId, Guid? EvidenceAnchorId)>();
            foreach (var sourceRef in statement.SourceRefs)
            {
                foreach (var aggregateClaimId in aggregateClaimIds)
                {
                    if (!seenSourceMaps.Add((
                        sourceRef.MemoryRecordId.Value,
                        aggregateClaimId,
                        sourceRef.SourceItemId?.Value,
                        sourceRef.EvidenceAnchorId?.Value)))
                    {
                        continue;
                    }

                    dbContext.Add(new CognitiveMemorySynthesizedStatementSourceMapRecord
                    {
                        Id = Guid.NewGuid(),
                        SynthesisId = synthesisId.Value,
                        StatementId = statement.StatementId.Value,
                        ProjectId = request.RecallResult.ContextPack.ProjectId,
                        MemoryRecordId = sourceRef.MemoryRecordId.Value,
                        AggregateClaimId = aggregateClaimId,
                        SourceItemId = sourceRef.SourceItemId?.Value,
                        EvidenceAnchorId = sourceRef.EvidenceAnchorId?.Value,
                        SourceSystem = sourceRef.SourceSystem,
                        Locator = sourceRef.Locator,
                        Summary = sourceRef.Summary,
                        AccessLevel = sourceRef.AccessLevel,
                        RedactionState = sourceRef.RedactionState,
                        CreatedAtUtc = nowUtc
                    });
                }
            }

            sequence++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<HashSet<Guid>> LoadAggregateClaimIdsAsync(
        IReadOnlyList<CognitiveMemoryRecallContextSection> selectedSections,
        CancellationToken cancellationToken)
    {
        var requestedClaimIds = selectedSections
            .SelectMany(section => section.ClaimIds)
            .Select(claimId => claimId.Value)
            .Where(claimId => claimId != Guid.Empty)
            .Distinct()
            .ToArray();
        if (requestedClaimIds.Length == 0)
        {
            return [];
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<CognitiveMemoryDreamAggregateClaimRecord>()
            .AsNoTracking()
            .Where(claim => requestedClaimIds.Contains(claim.Id))
            .Select(claim => claim.Id)
            .ToHashSetAsync(cancellationToken);
    }

    private static int CountPersistedSourceMaps(CognitiveMemorySynthesizedRecallStatement statement)
        => statement.SourceRefs.Count * Math.Max(1, statement.AggregateClaimIds.Count);

    private static string ResolveSynthesisQueryText(CognitiveMemoryRecallSynthesisRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.QueryText))
        {
            return request.QueryText.Trim();
        }

        return string.Join(
            ' ',
            new[] { request.RecallResult.ContextPack.Title, request.RecallResult.ContextPack.Summary }
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim()));
    }
}
