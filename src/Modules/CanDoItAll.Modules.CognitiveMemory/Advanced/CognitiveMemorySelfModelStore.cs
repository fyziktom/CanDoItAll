using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemorySelfModelStore(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICognitiveMemoryScoreGeometryDriver scoreGeometryDriver,
    IClock clock) : ICognitiveMemorySelfModelStore
{
    public async ValueTask<CognitiveMemorySelfModelProfileRecord> EnsureSeedProfileAsync(
        CognitiveMemorySelfModelQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existing = await FindSelfModelAsync(dbContext, query, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var now = clock.GetUtcNow();
        var selfModel = new CognitiveMemorySelfModelProfileRecord
        {
            ProjectId = query.ProjectId,
            Status = CognitiveMemorySelfModelStatus.Active,
            ModelProfileId = NormalizeModelProfileId(query.ModelProfileId),
            RoleKey = NormalizeRoleKey(query.RoleKey),
            ProfileVersion = "self-model-seed-v1",
            OperatingPrinciples = "Use source-backed memory, expose uncertainty, and route risky or source-poor answers to review.",
            AllowedTaskCategoriesJson = JsonSerializer.Serialize(
                new[] { "development", "architecture", "testing", "analysis" },
                CognitiveMemoryAdvancedJson.Options),
            RestrictedTaskCategoriesJson = JsonSerializer.Serialize(
                new[] { "unreviewed-high-risk-procedure", "redacted-source-disclosure" },
                CognitiveMemoryAdvancedJson.Options),
            AlgorithmVersion = "self-model-v1",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.Add(selfModel);
        await dbContext.SaveChangesAsync(cancellationToken);

        var competenceTrace = await CognitiveMemoryAdvancedScoring.EvaluateAndPersistAsync(
            dbContext,
            scoreGeometryDriver,
            query.ProjectId,
            CognitiveMemoryScoreOwnerKind.CalibrationAggregate,
            selfModel.Id,
            CognitiveMemoryScoreSpaceKind.SelfModelCompetence,
            [
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.SourceSufficiency, 0.55),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.RegressionFailure, 0.2),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.SelfModelStability, 0.7)
            ],
            CognitiveMemoryScoreProjectionBucket.WeakAccept,
            now,
            cancellationToken);
        dbContext.Add(new CognitiveMemoryDomainCompetenceProfileRecord
        {
            ProjectId = query.ProjectId,
            SelfModelProfileId = selfModel.Id,
            DomainKey = NormalizeKey(query.DomainKey),
            TaskTypeKey = NormalizeKey(query.TaskTypeKey),
            ModelProfileId = NormalizeModelProfileId(query.ModelProfileId),
            ProfileVersion = selfModel.ProfileVersion,
            CompetenceLevel = CognitiveMemoryCompetenceLevel.Developing,
            CompetenceScoreEvaluationTraceId = competenceTrace.Id.Value,
            EvidenceCount = 1,
            EvidenceRefsJson = "[]",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        dbContext.Add(new CognitiveMemoryKnownFailurePatternRecord
        {
            ProjectId = query.ProjectId,
            SelfModelProfileId = selfModel.Id,
            PatternKind = CognitiveMemoryKnownFailurePatternKind.SourceInsufficientAnswer,
            DomainKey = NormalizeKey(query.DomainKey),
            TaskTypeKey = NormalizeKey(query.TaskTypeKey),
            TriggerSummary = "Source insufficiency or wrong-scope evidence should trigger source audit or probing.",
            Mitigation = "Require source-backed context or ask for clarification before producing a confident answer.",
            RequiresReview = true,
            EvidenceRefsJson = "[]",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        dbContext.Add(new CognitiveMemorySelfRegulationPolicyProfileRecord
        {
            ProjectId = query.ProjectId,
            SelfModelProfileId = selfModel.Id,
            PolicyKey = "default",
            ProfileVersion = selfModel.ProfileVersion,
            AllowedPosturesJson = JsonSerializer.Serialize(Enum.GetNames<CognitiveMemoryAnswerPostureKind>(), CognitiveMemoryAdvancedJson.Options),
            RequiredOperationsJson = JsonSerializer.Serialize(Enum.GetNames<CognitiveMemoryRequiredOperationKind>(), CognitiveMemoryAdvancedJson.Options),
            ReviewThreshold = 0.65,
            AbstentionThreshold = 0.85,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        return selfModel;
    }

    public async ValueTask<CognitiveMemorySelfModelSnapshot> LoadAsync(
        CognitiveMemorySelfModelQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var selfModel = await FindSelfModelAsync(dbContext, query, cancellationToken)
            ?? throw new InvalidOperationException($"No active cognitive self-model exists for model profile '{query.ModelProfileId}' and role '{query.RoleKey}'.");
        var competence = await dbContext.Set<CognitiveMemoryDomainCompetenceProfileRecord>()
            .AsNoTracking()
            .Where(item => item.ProjectId == query.ProjectId &&
                           item.ModelProfileId == NormalizeModelProfileId(query.ModelProfileId) &&
                           item.DomainKey == NormalizeKey(query.DomainKey) &&
                           item.TaskTypeKey == NormalizeKey(query.TaskTypeKey) &&
                           item.ProfileVersion == selfModel.ProfileVersion)
            .SingleOrDefaultAsync(cancellationToken);
        var patterns = await dbContext.Set<CognitiveMemoryKnownFailurePatternRecord>()
            .AsNoTracking()
            .Where(item => item.SelfModelProfileId == selfModel.Id &&
                           item.DomainKey == NormalizeKey(query.DomainKey) &&
                           item.TaskTypeKey == NormalizeKey(query.TaskTypeKey))
            .OrderBy(item => item.PatternKind)
            .ToListAsync(cancellationToken);
        var policy = await dbContext.Set<CognitiveMemorySelfRegulationPolicyProfileRecord>()
            .AsNoTracking()
            .Where(item => item.SelfModelProfileId == selfModel.Id &&
                           item.PolicyKey == "default" &&
                           item.ProfileVersion == selfModel.ProfileVersion)
            .SingleOrDefaultAsync(cancellationToken);
        return new CognitiveMemorySelfModelSnapshot(selfModel, competence, patterns, policy);
    }

    public async ValueTask<CognitiveMemorySelfModelUpdateProposalRecord> ProposeUpdateAsync(
        CognitiveMemorySelfModelUpdateProposalRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.EvidenceRefs.Count == 0)
        {
            throw new InvalidOperationException("Self-model updates require evidence references.");
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var proposal = new CognitiveMemorySelfModelUpdateProposalRecord
        {
            ProjectId = request.ProjectId,
            Status = CognitiveMemorySelfModelUpdateProposalStatus.PendingReview,
            ModelProfileId = NormalizeModelProfileId(request.ModelProfileId),
            DomainKey = NormalizeKey(request.DomainKey),
            ProposedChange = CognitiveMemoryGuard.EnsureText(request.ProposedChange, nameof(request.ProposedChange)),
            EvidenceRefsJson = JsonSerializer.Serialize(request.EvidenceRefs, CognitiveMemoryAdvancedJson.Options),
            RequestedByActorId = CognitiveMemoryGuard.EnsureText(request.RequestedByActorId, nameof(request.RequestedByActorId)),
            CreatedAtUtc = clock.GetUtcNow()
        };
        dbContext.Add(proposal);
        await dbContext.SaveChangesAsync(cancellationToken);
        return proposal;
    }

    private static Task<CognitiveMemorySelfModelProfileRecord?> FindSelfModelAsync(
        AppDbContext dbContext,
        CognitiveMemorySelfModelQuery query,
        CancellationToken cancellationToken)
        => dbContext.Set<CognitiveMemorySelfModelProfileRecord>()
            .Where(item => item.ProjectId == query.ProjectId &&
                           item.ModelProfileId == NormalizeModelProfileId(query.ModelProfileId) &&
                           item.RoleKey == NormalizeRoleKey(query.RoleKey) &&
                           item.Status == CognitiveMemorySelfModelStatus.Active)
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

    internal static string NormalizeKey(string value)
        => CognitiveMemoryGuard.EnsureText(value, nameof(value)).ToLowerInvariant();

    internal static CognitiveMemoryModelProfileId NormalizeModelProfileId(CognitiveMemoryModelProfileId value)
        => new(NormalizeKey(value.Value));

    internal static CognitiveMemoryRoleKey NormalizeRoleKey(CognitiveMemoryRoleKey value)
        => new(NormalizeKey(value.Value));
}

