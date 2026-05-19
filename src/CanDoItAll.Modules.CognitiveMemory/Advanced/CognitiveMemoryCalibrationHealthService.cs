using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryCalibrationHealthService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICognitiveMemoryScoreGeometryDriver scoreGeometryDriver,
    IClock clock) : ICognitiveMemoryCalibrationHealthService
{
    public async ValueTask<CognitiveMemoryCalibrationEventRecord> RecordOutcomeAsync(
        CognitiveMemoryCalibrationOutcomeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        CognitiveMemoryScoreGuard.EnsureUnitInterval(request.PredictedConfidence, nameof(request.PredictedConfidence));
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var calibrationEvent = new CognitiveMemoryCalibrationEventRecord
        {
            ProjectId = request.ProjectId,
            DomainKey = Normalize(request.DomainKey),
            TaskTypeKey = Normalize(request.TaskTypeKey),
            ModelProfileId = NormalizeModelProfileId(request.ModelProfileId),
            RiskKey = NormalizeRiskKey(request.RiskKey),
            FeaturePatternKey = Normalize(request.FeaturePatternKey),
            ProfileVersion = Normalize(request.ProfileVersion),
            PredictedConfidence = request.PredictedConfidence,
            ActualCorrect = request.ActualCorrect,
            OutcomeKind = request.OutcomeKind,
            ProbeTurnId = request.ProbeTurnId,
            RecallTraceId = request.RecallTraceId,
            ReviewItemId = request.ReviewItemId,
            ProfessorReviewId = request.ProfessorReviewId,
            ObservedAtUtc = now
        };
        dbContext.Add(calibrationEvent);
        await dbContext.SaveChangesAsync(cancellationToken);
        await RecalculateAggregateAsync(dbContext, request, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return calibrationEvent;
    }

    public async ValueTask<CognitiveMemoryCalibrationHealthSnapshot?> GetAggregateAsync(
        Guid? projectId,
        string domainKey,
        string taskTypeKey,
        string modelProfileId,
        string riskKey,
        string featurePatternKey,
        string profileVersion,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var aggregate = await FindAggregateQuery(
                dbContext,
                projectId,
                Normalize(domainKey),
                Normalize(taskTypeKey),
                new CognitiveMemoryModelProfileId(Normalize(modelProfileId)),
                new CognitiveMemoryRiskKey(Normalize(riskKey)),
                Normalize(featurePatternKey),
                Normalize(profileVersion))
            .AsNoTracking()
            .SingleOrDefaultAsync(cancellationToken);
        if (aggregate is null)
        {
            return null;
        }

        var bins = await dbContext.Set<CognitiveMemoryCalibrationBinRecord>()
            .AsNoTracking()
            .Where(item => item.CalibrationAggregateId == aggregate.Id)
            .OrderBy(item => item.BinIndex)
            .ToListAsync(cancellationToken);
        return new CognitiveMemoryCalibrationHealthSnapshot(aggregate, bins);
    }

    private async Task RecalculateAggregateAsync(
        AppDbContext dbContext,
        CognitiveMemoryCalibrationOutcomeRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var domainKey = Normalize(request.DomainKey);
        var taskTypeKey = Normalize(request.TaskTypeKey);
        var modelProfileId = NormalizeModelProfileId(request.ModelProfileId);
        var riskKey = NormalizeRiskKey(request.RiskKey);
        var featurePatternKey = Normalize(request.FeaturePatternKey);
        var profileVersion = Normalize(request.ProfileVersion);
        var events = await dbContext.Set<CognitiveMemoryCalibrationEventRecord>()
            .Where(item => item.ProjectId == request.ProjectId &&
                           item.DomainKey == domainKey &&
                           item.TaskTypeKey == taskTypeKey &&
                           item.ModelProfileId == modelProfileId &&
                           item.RiskKey == riskKey &&
                           item.FeaturePatternKey == featurePatternKey &&
                           item.ProfileVersion == profileVersion)
            .ToListAsync(cancellationToken);
        if (events.Count == 0)
        {
            return;
        }

        var aggregate = await FindAggregateQuery(
                dbContext,
                request.ProjectId,
                domainKey,
                taskTypeKey,
                modelProfileId,
                riskKey,
                featurePatternKey,
                profileVersion)
            .SingleOrDefaultAsync(cancellationToken);
        if (aggregate is null)
        {
            aggregate = new CognitiveMemoryCalibrationAggregateRecord
            {
                ProjectId = request.ProjectId,
                DomainKey = domainKey,
                TaskTypeKey = taskTypeKey,
                ModelProfileId = modelProfileId,
                RiskKey = riskKey,
                FeaturePatternKey = featurePatternKey,
                ProfileVersion = profileVersion
            };
            dbContext.Add(aggregate);
        }

        aggregate.ObservationCount = events.Count;
        aggregate.BrierScore = events.Average(item => Math.Pow(item.PredictedConfidence - (item.ActualCorrect ? 1 : 0), 2));
        aggregate.SignedBias = events.Average(item => item.PredictedConfidence - (item.ActualCorrect ? 1 : 0));
        aggregate.OverconfidenceRate = events.Count(IsOverconfidence) / (double)events.Count;
        aggregate.UnderconfidenceRate = events.Count(IsUnderconfidence) / (double)events.Count;
        aggregate.AbstentionQualityRate = events.Count(item => item.OutcomeKind == CognitiveMemoryCalibrationOutcomeKind.AbstentionAppropriate) / (double)events.Count;
        aggregate.WrongScopeRate = events.Count(item => item.OutcomeKind == CognitiveMemoryCalibrationOutcomeKind.WrongScope) / (double)events.Count;
        aggregate.SourceInsufficientRate = events.Count(item => item.OutcomeKind == CognitiveMemoryCalibrationOutcomeKind.SourceInsufficient) / (double)events.Count;
        aggregate.ExpectedCalibrationError = CalculateExpectedCalibrationError(events);
        var trace = await CognitiveMemoryAdvancedScoring.EvaluateAndPersistAsync(
            dbContext,
            scoreGeometryDriver,
            request.ProjectId,
            CognitiveMemoryScoreOwnerKind.CalibrationAggregate,
            aggregate.Id,
            CognitiveMemoryScoreSpaceKind.CalibrationHealth,
            [
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.OverconfidenceRate, aggregate.OverconfidenceRate),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.UnderconfidenceRate, aggregate.UnderconfidenceRate),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.CalibrationRisk, Math.Clamp(aggregate.ExpectedCalibrationError, 0, 1)),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.AbstentionQuality, aggregate.AbstentionQualityRate),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.WrongScopeRecurrence, aggregate.WrongScopeRate),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.SourceInsufficientRecurrence, aggregate.SourceInsufficientRate)
            ],
            aggregate.OverconfidenceRate >= 0.4 || aggregate.SourceInsufficientRate >= 0.4
                ? CognitiveMemoryScoreProjectionBucket.NeedsReview
                : CognitiveMemoryScoreProjectionBucket.WeakAccept,
            now,
            cancellationToken);
        aggregate.CalibrationScoreEvaluationTraceId = trace.Id.Value;
        aggregate.UpdatedAtUtc = now;

        var oldBins = await dbContext.Set<CognitiveMemoryCalibrationBinRecord>()
            .Where(item => item.CalibrationAggregateId == aggregate.Id)
            .ToListAsync(cancellationToken);
        dbContext.RemoveRange(oldBins);
        foreach (var bin in BuildBins(aggregate.Id, request.ProjectId, events, now))
        {
            dbContext.Add(bin);
        }
    }

    private static IQueryable<CognitiveMemoryCalibrationAggregateRecord> FindAggregateQuery(
        AppDbContext dbContext,
        Guid? projectId,
        string domainKey,
        string taskTypeKey,
        CognitiveMemoryModelProfileId modelProfileId,
        CognitiveMemoryRiskKey riskKey,
        string featurePatternKey,
        string profileVersion)
        => dbContext.Set<CognitiveMemoryCalibrationAggregateRecord>()
            .Where(item => item.ProjectId == projectId &&
                           item.DomainKey == domainKey &&
                           item.TaskTypeKey == taskTypeKey &&
                           item.ModelProfileId == modelProfileId &&
                           item.RiskKey == riskKey &&
                           item.FeaturePatternKey == featurePatternKey &&
                           item.ProfileVersion == profileVersion);

    private static IReadOnlyList<CognitiveMemoryCalibrationBinRecord> BuildBins(
        Guid aggregateId,
        Guid? projectId,
        IReadOnlyList<CognitiveMemoryCalibrationEventRecord> events,
        DateTimeOffset now)
        => Enumerable.Range(0, 10)
            .Select(index =>
            {
                var lower = index / 10d;
                var upper = (index + 1) / 10d;
                var binEvents = events
                    .Where(item => item.PredictedConfidence >= lower &&
                                   (index == 9 ? item.PredictedConfidence <= upper : item.PredictedConfidence < upper))
                    .ToArray();
                return new CognitiveMemoryCalibrationBinRecord
                {
                    CalibrationAggregateId = aggregateId,
                    ProjectId = projectId,
                    BinIndex = index,
                    LowerBound = lower,
                    UpperBound = upper,
                    ObservationCount = binEvents.Length,
                    AveragePredictedConfidence = binEvents.Length == 0 ? 0 : binEvents.Average(item => item.PredictedConfidence),
                    ActualAccuracy = binEvents.Length == 0 ? 0 : binEvents.Count(item => item.ActualCorrect) / (double)binEvents.Length,
                    UpdatedAtUtc = now
                };
            })
            .ToArray();

    private static double CalculateExpectedCalibrationError(IReadOnlyList<CognitiveMemoryCalibrationEventRecord> events)
    {
        var bins = BuildBins(Guid.NewGuid(), null, events, DateTimeOffset.UnixEpoch);
        return bins.Sum(bin =>
            bin.ObservationCount == 0
                ? 0
                : (bin.ObservationCount / (double)events.Count) * Math.Abs(bin.AveragePredictedConfidence - bin.ActualAccuracy));
    }

    private static bool IsOverconfidence(CognitiveMemoryCalibrationEventRecord item)
        => item.OutcomeKind is CognitiveMemoryCalibrationOutcomeKind.IncorrectHighConfidence or CognitiveMemoryCalibrationOutcomeKind.HumanReviewRejected or CognitiveMemoryCalibrationOutcomeKind.ProfessorDisagreed ||
           item.PredictedConfidence >= 0.7 && !item.ActualCorrect;

    private static bool IsUnderconfidence(CognitiveMemoryCalibrationEventRecord item)
        => item.OutcomeKind == CognitiveMemoryCalibrationOutcomeKind.CorrectLowConfidence ||
           item.PredictedConfidence <= 0.4 && item.ActualCorrect;

    private static string Normalize(string value)
        => CognitiveMemorySelfModelStore.NormalizeKey(value);

    private static CognitiveMemoryModelProfileId NormalizeModelProfileId(CognitiveMemoryModelProfileId value)
        => new(Normalize(value.Value));

    private static CognitiveMemoryRiskKey NormalizeRiskKey(CognitiveMemoryRiskKey value)
        => new(Normalize(value.Value));
}

