using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;
public sealed class CognitiveMemoryQualityDiagnosticsService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock) : ICognitiveMemoryQualityDiagnosticsService
{
    public async ValueTask<CognitiveMemoryQualityDiagnosticsReport> CreateReportAsync(
        CognitiveMemoryQualityDiagnosticsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var started = clock.GetUtcNow();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var sourceItems = await CountProjectAsync<CognitiveMemorySourceItemRecord>(dbContext, request.ProjectId, cancellationToken);
        var memoryRecords = await CountProjectAsync<CognitiveMemoryRecord>(dbContext, request.ProjectId, cancellationToken);
        var clusters = await CountProjectAsync<CognitiveMemoryQualityClusterRecord>(dbContext, request.ProjectId, cancellationToken);
        var clusterMembers = await CountProjectAsync<CognitiveMemoryQualityClusterMemberRecord>(dbContext, request.ProjectId, cancellationToken);
        var dreamRuns = await CountProjectAsync<CognitiveMemoryDreamRunRecord>(dbContext, request.ProjectId, cancellationToken);
        var dreamRunClusters = await CountProjectAsync<CognitiveMemoryDreamRunClusterRecord>(dbContext, request.ProjectId, cancellationToken);
        var aggregateCandidates = await CountProjectAsync<CognitiveMemoryDreamAggregateCandidateRecord>(dbContext, request.ProjectId, cancellationToken);
        var aggregateClaims = await CountProjectAsync<CognitiveMemoryDreamAggregateClaimRecord>(dbContext, request.ProjectId, cancellationToken);
        var aggregateSourceMaps = await CountProjectAsync<CognitiveMemoryDreamAggregateClaimSourceMapRecord>(dbContext, request.ProjectId, cancellationToken);
        var validations = await CountProjectAsync<CognitiveMemoryDreamValidationRecord>(dbContext, request.ProjectId, cancellationToken);
        var reviewItems = await CountProjectAsync<CognitiveMemoryReviewItemRecord>(dbContext, request.ProjectId, cancellationToken);
        var synthesizedRecalls = await CountProjectAsync<CognitiveMemorySynthesizedRecallRecord>(dbContext, request.ProjectId, cancellationToken);
        var synthesizedStatements = await CountProjectAsync<CognitiveMemorySynthesizedStatementRecord>(dbContext, request.ProjectId, cancellationToken);

        var warnings = new List<CognitiveMemoryQualityDiagnosticWarning>();
        if (sourceItems > 0 && memoryRecords > 0 && clusters == 0)
        {
            warnings.Add(new CognitiveMemoryQualityDiagnosticWarning(
                "quality.clusters.missing",
                "Source-backed memories exist, but no quality clusters have been planned.",
                CognitiveMemoryRiskLevel.Medium));
        }

        if (dreamRuns > 0 && (dreamRunClusters == 0 || aggregateCandidates == 0))
        {
            warnings.Add(new CognitiveMemoryQualityDiagnosticWarning(
                "quality.dream.shallow",
                "A dream run exists without linked clusters or aggregate candidates.",
                CognitiveMemoryRiskLevel.High));
        }

        if (aggregateClaims > 0 && aggregateSourceMaps == 0)
        {
            warnings.Add(new CognitiveMemoryQualityDiagnosticWarning(
                "quality.aggregate.provenance-missing",
                "Aggregate claims exist without claim-level source maps.",
                CognitiveMemoryRiskLevel.High));
        }

        if (aggregateCandidates > 0 && validations == 0)
        {
            warnings.Add(new CognitiveMemoryQualityDiagnosticWarning(
                "quality.validation.missing",
                "Aggregate candidates exist without validation gate records.",
                CognitiveMemoryRiskLevel.High));
        }

        if (synthesizedRecalls > 0 && synthesizedStatements == 0)
        {
            warnings.Add(new CognitiveMemoryQualityDiagnosticWarning(
                "quality.recall.synthesis-empty",
                "A synthesized recall exists without user-facing statements.",
                CognitiveMemoryRiskLevel.Medium));
        }

        return new CognitiveMemoryQualityDiagnosticsReport(
            request.ProjectId,
            sourceItems,
            memoryRecords,
            clusters,
            clusterMembers,
            dreamRuns,
            dreamRunClusters,
            aggregateCandidates,
            aggregateClaims,
            aggregateSourceMaps,
            validations,
            reviewItems,
            synthesizedRecalls,
            synthesizedStatements,
            clock.GetUtcNow() - started,
            warnings);
    }

    private static Task<int> CountProjectAsync<TEntity>(
        AppDbContext dbContext,
        Guid? projectId,
        CancellationToken cancellationToken)
        where TEntity : class
        => projectId is null
            ? dbContext.Set<TEntity>().CountAsync(cancellationToken)
            : dbContext.Set<TEntity>().CountAsync(entity => EF.Property<Guid?>(entity, "ProjectId") == projectId, cancellationToken);
}
