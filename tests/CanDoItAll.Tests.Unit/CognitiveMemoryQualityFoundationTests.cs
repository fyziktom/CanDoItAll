using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Unit;

public sealed class CognitiveMemoryQualityFoundationTests
{
    [Fact]
    public async Task Diagnostics_ReportClusterAndDreamShallowRisks()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            "Deployment rollback procedure",
            "Deployment rollback procedure uses runbook validation.",
            "Deployment rollback procedure uses runbook validation.");
        fixture.DbContext.Add(new CognitiveMemoryDreamRunRecord
        {
            ProjectId = projectId,
            Mode = CognitiveMemoryConsolidationMode.ProjectNightly,
            TriggerKind = CognitiveMemoryConsolidationTriggerKind.Nightly,
            Status = CognitiveMemoryRunStatus.Succeeded,
            IdempotencyKey = "diagnostic-shallow",
            PolicyProfileId = "policy:test",
            AlgorithmVersion = "unit-test",
            StartedAtUtc = fixture.Clock.GetUtcNow(),
            CompletedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        });
        await fixture.DbContext.SaveChangesAsync();
        var service = new CognitiveMemoryQualityDiagnosticsService(fixture.Factory, fixture.Clock);

        var report = await service.CreateReportAsync(new CognitiveMemoryQualityDiagnosticsRequest(projectId, Policy(projectId)));

        Assert.True(report.IsShallowDreamRun);
        Assert.Contains(report.Warnings, warning => warning.Code == "quality.clusters.missing");
        Assert.Contains(report.Warnings, warning => warning.Code == "quality.dream.shallow");
    }

    [Fact]
    public async Task ClusterPlanner_PersistsAllRequiredClusterKeyFamilies()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var first = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000011"),
            "Deployment rollback procedure",
            "Deployment rollback procedure uses a runbook and validation checklist.",
            "Deployment rollback source evidence.",
            topicKey: "deployment.rollback.procedure",
            sourceSystem: "ProcessRuntime",
            sourceItemType: "ProcessRun");
        var second = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000012"),
            "Deployment rollback procedure",
            "Deployment rollback procedure requires workflow approval before production release.",
            "Deployment rollback source evidence.",
            topicKey: "deployment.rollback.procedure",
            sourceSystem: "ProcessRuntime",
            sourceItemType: "ProcessRun");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000013"),
            "Deployment rollback procedure",
            "Deployment rollback procedure captures failure-learning evidence after incidents.",
            "Deployment rollback source evidence.",
            topicKey: "deployment.rollback.procedure",
            sourceSystem: "ProcessRuntime",
            sourceItemType: "ProcessRun");
        fixture.DbContext.Add(new CognitiveMemoryRelationRecord
        {
            ProjectId = projectId,
            SourceMemoryRecordId = first.RecordId,
            TargetMemoryRecordId = second.RecordId,
            RelationKind = CognitiveMemoryRelationKind.Supports,
            EvidenceCount = 1,
            RelationBucket = CognitiveMemoryScoreProjectionBucket.StrongAccept,
            Reason = "Procedure records support the same deployment rollback workflow.",
            AlgorithmVersion = "unit-test",
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        });
        await fixture.DbContext.SaveChangesAsync();
        var planner = new CognitiveMemoryClusterPlanner(fixture.Factory, fixture.Clock);

        var result = await planner.PlanAsync(new CognitiveMemoryClusterPlanningRequest(projectId, Policy(projectId)));

        Assert.Empty(result.Warnings);
        Assert.True(result.Metrics.ClustersCreated >= 8);
        var primaryFamilies = result.Clusters.Select(cluster => cluster.PrimaryKeyFamily).ToHashSet();
        Assert.Contains(CognitiveMemoryQualityClusterKeyFamily.ProjectScope, primaryFamilies);
        Assert.Contains(CognitiveMemoryQualityClusterKeyFamily.SourceTopology, primaryFamilies);
        Assert.Contains(CognitiveMemoryQualityClusterKeyFamily.SemanticTopic, primaryFamilies);
        Assert.Contains(CognitiveMemoryQualityClusterKeyFamily.Entity, primaryFamilies);
        Assert.Contains(CognitiveMemoryQualityClusterKeyFamily.TaskIntent, primaryFamilies);
        Assert.Contains(CognitiveMemoryQualityClusterKeyFamily.Temporal, primaryFamilies);
        Assert.Contains(CognitiveMemoryQualityClusterKeyFamily.EvidenceOverlap, primaryFamilies);
        Assert.Contains(CognitiveMemoryQualityClusterKeyFamily.Relation, primaryFamilies);
        Assert.Contains(CognitiveMemoryQualityClusterKeyFamily.AccessRisk, primaryFamilies);
        Assert.Equal(result.Metrics.ClustersCreated, await fixture.DbContext.Set<CognitiveMemoryQualityClusterRecord>().CountAsync());
        Assert.True(await fixture.DbContext.Set<CognitiveMemoryQualityClusterMemberRecord>().CountAsync() > 0);
    }

    [Fact]
    public async Task ClusterPlanner_ReusesPersistedClusterIdsAndPersistsSourceItemMembers()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000061"),
            "Incident rollback workflow",
            "Incident rollback workflow uses a documented procedure.",
            "Incident rollback workflow source evidence.",
            topicKey: "incident.rollback.workflow");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000062"),
            "Incident rollback workflow",
            "Incident rollback workflow captures operator validation.",
            "Incident rollback workflow source evidence.",
            topicKey: "incident.rollback.workflow");
        var planner = new CognitiveMemoryClusterPlanner(fixture.Factory, fixture.Clock);

        var first = await planner.PlanAsync(new CognitiveMemoryClusterPlanningRequest(projectId, Policy(projectId)));
        fixture.DbContext.ChangeTracker.Clear();
        var second = await planner.PlanAsync(new CognitiveMemoryClusterPlanningRequest(projectId, Policy(projectId)));

        var firstIdsByHash = first.Clusters.ToDictionary(cluster => cluster.ClusterHash, cluster => cluster.ClusterId);
        Assert.NotEmpty(firstIdsByHash);
        Assert.All(second.Clusters, cluster => Assert.Equal(firstIdsByHash[cluster.ClusterHash], cluster.ClusterId));
        Assert.Equal(first.Clusters.Count, await fixture.DbContext.Set<CognitiveMemoryQualityClusterRecord>().CountAsync());
        Assert.True(await fixture.DbContext.Set<CognitiveMemoryQualityClusterMemberRecord>()
            .AnyAsync(member => member.MemberKind == CognitiveMemoryQualityClusterMemberKind.SourceItem));
    }

    [Fact]
    public async Task DreamRun_ProjectNightlyCreatesApprovedCandidateAndMetrics()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000021"),
            "Offline deployment procedure",
            "Offline deployment procedure uses package staging before release.",
            "Offline deployment package staging evidence.",
            topicKey: "offline.deployment.procedure");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000022"),
            "Offline deployment procedure",
            "Offline deployment procedure requires release validation before production.",
            "Offline deployment release validation evidence.",
            topicKey: "offline.deployment.procedure");
        var dream = CreateDreamService(fixture);

        var result = await dream.RunAsync(new CognitiveMemoryDreamRunRequest(
            projectId,
            CognitiveMemoryConsolidationMode.ProjectNightly,
            CognitiveMemoryConsolidationTriggerKind.Nightly,
            Policy(projectId),
            new CognitiveMemoryIdempotencyKey("dream-project-nightly")));

        Assert.Equal(CognitiveMemoryRunStatus.Succeeded, result.Status);
        Assert.True(result.Metrics.ClustersConsidered > 0);
        Assert.True(result.Metrics.ClusterMembersRead > 0);
        Assert.True(result.Metrics.AggregateCandidatesCreated > 0);
        Assert.True(result.Metrics.AggregateClaimsCreated > 0);
        Assert.True(result.Metrics.AggregateClaimSourceMapsCreated >= result.Metrics.AggregateClaimsCreated);
        Assert.True(result.Metrics.ValidationRecordsCreated > 0);
        Assert.True(result.Metrics.ApprovedCandidates > 0);
        Assert.All(result.AggregateCandidates, candidate => Assert.NotEqual(CognitiveMemoryDreamAggregateCandidateStatus.Proposed, candidate.Status));
        Assert.Contains(result.AggregateCandidates, candidate => candidate.Title.Contains("offline.deployment.procedure", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, await fixture.DbContext.Set<CognitiveMemoryReviewItemRecord>().CountAsync());

        var diagnostics = new CognitiveMemoryQualityDiagnosticsService(fixture.Factory, fixture.Clock);
        var report = await diagnostics.CreateReportAsync(new CognitiveMemoryQualityDiagnosticsRequest(projectId, Policy(projectId)));
        Assert.False(report.IsShallowDreamRun);
    }

    [Fact]
    public async Task DreamRun_SecondRunUsesExistingClustersWithoutForeignKeyFailures()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000071"),
            "Release validation workflow",
            "Release validation workflow checks health before rollout.",
            "Release validation health evidence.",
            topicKey: "release.validation.workflow");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000072"),
            "Release validation workflow",
            "Release validation workflow records operator approval.",
            "Release validation approval evidence.",
            topicKey: "release.validation.workflow");
        var dream = CreateDreamService(fixture);

        var first = await dream.RunAsync(new CognitiveMemoryDreamRunRequest(
            projectId,
            CognitiveMemoryConsolidationMode.ProjectNightly,
            CognitiveMemoryConsolidationTriggerKind.Nightly,
            Policy(projectId),
            new CognitiveMemoryIdempotencyKey("dream-existing-cluster-first")));
        var second = await dream.RunAsync(new CognitiveMemoryDreamRunRequest(
            projectId,
            CognitiveMemoryConsolidationMode.ProjectNightly,
            CognitiveMemoryConsolidationTriggerKind.Nightly,
            Policy(projectId),
            new CognitiveMemoryIdempotencyKey("dream-existing-cluster-second")));

        Assert.Equal(CognitiveMemoryRunStatus.Succeeded, first.Status);
        Assert.Equal(CognitiveMemoryRunStatus.Succeeded, second.Status);
        Assert.True(second.Metrics.AggregateCandidatesCreated > 0);
        Assert.Equal(2, await fixture.DbContext.Set<CognitiveMemoryDreamRunRecord>().CountAsync());
    }

    [Fact]
    public async Task DreamRun_DryRunDoesNotPersistQualityRecords()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000073"),
            "Dry run workflow",
            "Dry run workflow prepares a release report.",
            "Dry run release evidence.",
            topicKey: "dry.run.workflow");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000074"),
            "Dry run workflow",
            "Dry run workflow verifies release report completeness.",
            "Dry run report evidence.",
            topicKey: "dry.run.workflow");
        var dream = CreateDreamService(fixture);

        var result = await dream.RunAsync(new CognitiveMemoryDreamRunRequest(
            projectId,
            CognitiveMemoryConsolidationMode.ProjectNightly,
            CognitiveMemoryConsolidationTriggerKind.Manual,
            Policy(projectId),
            new CognitiveMemoryIdempotencyKey("dream-dry-run"),
            persistChanges: false));

        Assert.Equal(CognitiveMemoryRunStatus.Succeeded, result.Status);
        Assert.NotEmpty(result.AggregateCandidates);
        Assert.Equal(0, await fixture.DbContext.Set<CognitiveMemoryDreamRunRecord>().CountAsync());
        Assert.Equal(0, await fixture.DbContext.Set<CognitiveMemoryQualityClusterRecord>().CountAsync());
        Assert.Equal(0, await fixture.DbContext.Set<CognitiveMemoryDreamAggregateCandidateRecord>().CountAsync());
        Assert.Equal(0, await fixture.DbContext.Set<CognitiveMemoryDreamValidationRecord>().CountAsync());
    }

    [Fact]
    public async Task DreamRun_UnsupportedModesFailBeforeWritingRunState()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var dream = CreateDreamService(fixture);

        await Assert.ThrowsAsync<NotSupportedException>(() => dream.RunAsync(new CognitiveMemoryDreamRunRequest(
            projectId,
            CognitiveMemoryConsolidationMode.ProjectionRebuild,
            CognitiveMemoryConsolidationTriggerKind.Manual,
            Policy(projectId),
            new CognitiveMemoryIdempotencyKey("dream-unsupported"))).AsTask());

        Assert.Equal(0, await fixture.DbContext.Set<CognitiveMemoryDreamRunRecord>().CountAsync());
    }

    [Fact]
    public async Task DreamRun_RecordsFailedStateWhenPlannerFails()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var dream = new CognitiveMemoryDreamConsolidationService(
            fixture.Factory,
            new ThrowingClusterPlanner(),
            new CognitiveMemoryDreamValidator(fixture.Factory, fixture.Clock),
            fixture.Clock);

        var result = await dream.RunAsync(new CognitiveMemoryDreamRunRequest(
            projectId,
            CognitiveMemoryConsolidationMode.ProjectNightly,
            CognitiveMemoryConsolidationTriggerKind.Manual,
            Policy(projectId),
            new CognitiveMemoryIdempotencyKey("dream-planner-failure")));

        Assert.Equal(CognitiveMemoryRunStatus.Failed, result.Status);
        var failedRun = await fixture.DbContext.Set<CognitiveMemoryDreamRunRecord>().SingleAsync();
        Assert.Equal(CognitiveMemoryRunStatus.Failed, failedRun.Status);
        Assert.Equal("quality.dream.run-failed", failedRun.FailureCode);
        Assert.DoesNotContain("SECRET", failedRun.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DreamRun_IdempotentReplayDoesNotDuplicateValidationOrReviewRecords()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000075"),
            "Replay workflow",
            "Replay workflow stores deployment evidence.",
            "Replay deployment evidence.",
            topicKey: "replay.workflow");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000076"),
            "Replay workflow",
            "Replay workflow stores validation evidence.",
            "Replay validation evidence.",
            topicKey: "replay.workflow");
        var dream = CreateDreamService(fixture);

        await dream.RunAsync(new CognitiveMemoryDreamRunRequest(
            projectId,
            CognitiveMemoryConsolidationMode.ProjectNightly,
            CognitiveMemoryConsolidationTriggerKind.Nightly,
            Policy(projectId),
            new CognitiveMemoryIdempotencyKey("dream-replay")));
        var validationsBeforeReplay = await fixture.DbContext.Set<CognitiveMemoryDreamValidationRecord>().CountAsync();
        var reviewsBeforeReplay = await fixture.DbContext.Set<CognitiveMemoryReviewItemRecord>().CountAsync();
        await dream.RunAsync(new CognitiveMemoryDreamRunRequest(
            projectId,
            CognitiveMemoryConsolidationMode.ProjectNightly,
            CognitiveMemoryConsolidationTriggerKind.Nightly,
            Policy(projectId),
            new CognitiveMemoryIdempotencyKey("dream-replay")));

        Assert.Equal(validationsBeforeReplay, await fixture.DbContext.Set<CognitiveMemoryDreamValidationRecord>().CountAsync());
        Assert.Equal(reviewsBeforeReplay, await fixture.DbContext.Set<CognitiveMemoryReviewItemRecord>().CountAsync());
    }

    [Fact]
    public async Task DreamValidation_RoutesRestrictedAggregateToReview()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000031"),
            "Credential rotation runbook",
            "Credential rotation runbook uses release approval.",
            "Credential rotation release evidence.",
            topicKey: "credential.rotation.runbook");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000032"),
            "Credential rotation runbook",
            "Credential rotation runbook includes restricted vault owner notes.",
            "SECRET_TOKEN=do-not-leak",
            topicKey: "credential.rotation.runbook",
            sourceAccessLevel: CognitiveMemoryAccessLevel.Restricted,
            sourceRedactionState: CognitiveMemoryRedactionState.Restricted);
        var dream = CreateDreamService(fixture);

        var result = await dream.RunAsync(new CognitiveMemoryDreamRunRequest(
            projectId,
            CognitiveMemoryConsolidationMode.ProjectNightly,
            CognitiveMemoryConsolidationTriggerKind.Nightly,
            Policy(projectId),
            new CognitiveMemoryIdempotencyKey("dream-restricted-review")));

        Assert.True(result.Metrics.NeedsReviewCandidates > 0);
        Assert.True(result.Metrics.ReviewItemsCreated > 0);
        Assert.Contains(result.AggregateCandidates, candidate => candidate.Status == CognitiveMemoryDreamAggregateCandidateStatus.NeedsHumanReview);
        var reviews = await fixture.DbContext.Set<CognitiveMemoryReviewItemRecord>().ToListAsync();
        Assert.NotEmpty(reviews);
        var review = reviews[0];
        Assert.Equal(CognitiveMemoryReviewStatus.Pending, review.Status);
        Assert.Equal(CognitiveMemoryReviewSubjectKind.Run, review.SubjectKind);
    }

    [Fact]
    public async Task DreamValidation_RoutesContradictoryClusterToReview()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var first = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000077"),
            "Database backup procedure",
            "Database backup procedure runs before migration.",
            "Database backup before migration evidence.",
            topicKey: "database.backup.procedure");
        var second = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000078"),
            "Database backup procedure",
            "Database backup procedure is skipped before migration.",
            "Database backup skipped evidence.",
            topicKey: "database.backup.procedure");
        fixture.DbContext.Add(new CognitiveMemoryRelationRecord
        {
            ProjectId = projectId,
            SourceMemoryRecordId = first.RecordId,
            TargetMemoryRecordId = second.RecordId,
            RelationKind = CognitiveMemoryRelationKind.Contradicts,
            EvidenceCount = 2,
            RelationBucket = CognitiveMemoryScoreProjectionBucket.StrongAccept,
            Reason = "Migration backup requirement conflicts.",
            AlgorithmVersion = "unit-test",
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        });
        await fixture.DbContext.SaveChangesAsync();
        var dream = CreateDreamService(fixture);

        var result = await dream.RunAsync(new CognitiveMemoryDreamRunRequest(
            projectId,
            CognitiveMemoryConsolidationMode.ProjectNightly,
            CognitiveMemoryConsolidationTriggerKind.Nightly,
            Policy(projectId),
            new CognitiveMemoryIdempotencyKey("dream-contradictory-review")));

        Assert.True(result.Metrics.NeedsReviewCandidates > 0);
        Assert.Contains(result.AggregateCandidates, candidate => candidate.Status == CognitiveMemoryDreamAggregateCandidateStatus.NeedsHumanReview);
        Assert.Contains(await fixture.DbContext.Set<CognitiveMemoryDreamValidationRecord>().ToListAsync(), validation => validation.Decision == CognitiveMemoryDreamValidationDecision.NeedsHumanReview);
    }

    [Fact]
    public async Task DreamAggregate_DoesNotCopyRestrictedSourceTextIntoCandidateText()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000079"),
            "Credential rotation procedure",
            "Credential rotation procedure requires approval.",
            "Credential rotation approval evidence.",
            topicKey: "credential.rotation.procedure");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000080"),
            "Credential rotation procedure",
            "Credential rotation procedure uses SECRET_TOKEN=do-not-leak.",
            "SECRET_TOKEN=do-not-leak",
            topicKey: "credential.rotation.procedure",
            sourceAccessLevel: CognitiveMemoryAccessLevel.Restricted,
            sourceRedactionState: CognitiveMemoryRedactionState.Restricted);
        var dream = CreateDreamService(fixture);

        var result = await dream.RunAsync(new CognitiveMemoryDreamRunRequest(
            projectId,
            CognitiveMemoryConsolidationMode.ProjectNightly,
            CognitiveMemoryConsolidationTriggerKind.Nightly,
            Policy(projectId),
            new CognitiveMemoryIdempotencyKey("dream-redacted-candidate")));

        Assert.All(result.AggregateCandidates, candidate =>
        {
            Assert.DoesNotContain("SECRET_TOKEN", candidate.CanonicalText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SECRET_TOKEN", candidate.SummaryText, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task AggregateApplicator_AppliesApprovedCandidateWithClaimLevelProvenance()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000041"),
            "Mobile sync procedure",
            "Mobile sync procedure queues work offline.",
            "Mobile sync queue evidence.",
            topicKey: "mobile.sync.procedure");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000042"),
            "Mobile sync procedure",
            "Mobile sync procedure reconciles conflicts after reconnect.",
            "Mobile sync conflict evidence.",
            topicKey: "mobile.sync.procedure");
        var dream = CreateDreamService(fixture);
        await dream.RunAsync(new CognitiveMemoryDreamRunRequest(
            projectId,
            CognitiveMemoryConsolidationMode.ProjectNightly,
            CognitiveMemoryConsolidationTriggerKind.Nightly,
            Policy(projectId),
            new CognitiveMemoryIdempotencyKey("dream-apply")));
        var candidate = await fixture.DbContext.Set<CognitiveMemoryDreamAggregateCandidateRecord>()
            .FirstAsync(candidate => candidate.Status == CognitiveMemoryDreamAggregateCandidateStatus.Approved);
        var applicator = new CognitiveMemoryAggregateMemoryApplicator(
            fixture.Factory,
            new CognitiveMemoryRecordValidator(),
            fixture.Clock);

        var result = await applicator.ApplyAsync(new CognitiveMemoryAggregateMemoryApplyRequest(
            new CognitiveMemoryDreamAggregateCandidateId(candidate.Id),
            "agent:unit-test",
            Policy(projectId)));

        fixture.DbContext.ChangeTracker.Clear();
        Assert.True(result.Created);
        Assert.NotEmpty(result.ClaimIds);
        var appliedCandidate = await fixture.DbContext.Set<CognitiveMemoryDreamAggregateCandidateRecord>().SingleAsync(row => row.Id == candidate.Id);
        Assert.Equal(CognitiveMemoryDreamAggregateCandidateStatus.Applied, appliedCandidate.Status);
        Assert.Equal(result.MemoryRecordId.Value, appliedCandidate.MemoryRecordId);
        Assert.True(await fixture.DbContext.Set<CognitiveMemoryClaimEvidenceLinkRecord>()
            .AnyAsync(link => result.ClaimIds.Select(id => id.Value).Contains(link.ClaimId)));
        Assert.True(await fixture.DbContext.Set<CognitiveMemoryRecordEvidenceAnchorRecord>()
            .AnyAsync(link => link.MemoryRecordId == result.MemoryRecordId.Value));
        Assert.True(await fixture.DbContext.Set<CognitiveMemorySourceLinkRecord>()
            .AnyAsync(link => link.MemoryRecordId == result.MemoryRecordId.Value));
    }

    [Fact]
    public async Task AggregateApplicator_RepeatedApplyReturnsExistingMemoryWithoutDuplicates()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000081"),
            "Cache rebuild procedure",
            "Cache rebuild procedure warms primary cache.",
            "Cache rebuild primary evidence.",
            topicKey: "cache.rebuild.procedure");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000082"),
            "Cache rebuild procedure",
            "Cache rebuild procedure warms secondary cache.",
            "Cache rebuild secondary evidence.",
            topicKey: "cache.rebuild.procedure");
        var dream = CreateDreamService(fixture);
        await dream.RunAsync(new CognitiveMemoryDreamRunRequest(
            projectId,
            CognitiveMemoryConsolidationMode.ProjectNightly,
            CognitiveMemoryConsolidationTriggerKind.Nightly,
            Policy(projectId),
            new CognitiveMemoryIdempotencyKey("dream-repeat-apply")));
        var candidate = await fixture.DbContext.Set<CognitiveMemoryDreamAggregateCandidateRecord>()
            .FirstAsync(candidate => candidate.Status == CognitiveMemoryDreamAggregateCandidateStatus.Approved);
        var applicator = new CognitiveMemoryAggregateMemoryApplicator(
            fixture.Factory,
            new CognitiveMemoryRecordValidator(),
            fixture.Clock);

        var first = await applicator.ApplyAsync(new CognitiveMemoryAggregateMemoryApplyRequest(
            new CognitiveMemoryDreamAggregateCandidateId(candidate.Id),
            "agent:unit-test",
            Policy(projectId)));
        var second = await applicator.ApplyAsync(new CognitiveMemoryAggregateMemoryApplyRequest(
            new CognitiveMemoryDreamAggregateCandidateId(candidate.Id),
            "agent:unit-test",
            Policy(projectId)));

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(first.MemoryRecordId, second.MemoryRecordId);
        Assert.Equal(1, await fixture.DbContext.Set<CognitiveMemoryDreamAggregateCandidateRecord>().CountAsync(row => row.MemoryRecordId == first.MemoryRecordId.Value));
    }

    [Fact]
    public async Task RecallSynthesis_PersistsBriefAndResolvesReferencesOnlyOnDemand()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var seeded = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000051"),
            "Deployment rollback",
            "Use rollback runbook when production health checks fail.",
            "Use rollback runbook when production health checks fail.");
        var traceId = Guid.Parse("20000000-0000-0000-0000-000000000051");
        fixture.DbContext.Add(new CognitiveMemoryRecallTraceRecord
        {
            Id = traceId,
            ProjectId = projectId,
            OperationMode = CognitiveMemoryOperationMode.Recall,
            RecallMode = CognitiveMemoryRecallMode.FocusedTaskContext,
            RequestedByActorId = "agent:test",
            PolicyProfileId = "policy:test",
            RequestHash = CognitiveMemoryHash.FromUtf8("recall-synthesis").Value,
            AlgorithmVersion = "unit-test",
            Outcome = CognitiveMemoryRunStatus.Succeeded,
            StartedAtUtc = fixture.Clock.GetUtcNow(),
            CompletedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        });
        await fixture.DbContext.SaveChangesAsync();
        var sourceRef = new CognitiveMemoryRecallSourceRef(
            new CognitiveMemoryRecordId(seeded.RecordId),
            new CognitiveMemorySourceItemId(seeded.SourceItemId),
            new CognitiveMemoryEvidenceAnchorId(seeded.EvidenceAnchorId),
            "unit-test",
            $"/unit/{seeded.RecordId:D}",
            "Use rollback runbook when production health checks fail.",
            CognitiveMemoryAccessLevel.Project,
            CognitiveMemoryRedactionState.Safe,
            IncludedInContext: true,
            CognitiveMemoryRecallExclusionReasonKind.None);
        var contextPack = new CognitiveMemoryRecallContextPack(
            CognitiveMemoryRecallContextPackId.New(),
            projectId,
            null,
            "Recall context",
            "Selected 1 source-backed memory candidate(s).",
            [
                new CognitiveMemoryRecallContextSection(
                    new CognitiveMemorySectionId("selected-0"),
                    CognitiveMemoryRecallContextSectionKind.SelectedMemory,
                    "Deployment rollback",
                    "Use rollback runbook when production health checks fail.",
                    [new CognitiveMemoryRecordId(seeded.RecordId)],
                    [],
                    [sourceRef])
            ],
            [sourceRef],
            [],
            new Dictionary<string, string>());
        var recallResult = new CognitiveMemoryRecallResult(traceId, contextPack, [], [], []);
        var synthesis = new CognitiveMemoryRecallSynthesisService(fixture.Factory, fixture.Clock);

        var result = await synthesis.SynthesizeAsync(new CognitiveMemoryRecallSynthesisRequest(
            recallResult,
            Policy(projectId)));

        Assert.False(result.ReferencesShownByDefault);
        Assert.Contains("Use rollback runbook", result.Brief, StringComparison.Ordinal);
        Assert.DoesNotContain($"/unit/{seeded.RecordId:D}", result.Brief, StringComparison.Ordinal);
        var statement = Assert.Single(result.Statements);
        var resolver = new CognitiveMemoryReferenceResolver(fixture.Factory);
        var references = await resolver.ResolveAsync(new CognitiveMemoryReferenceResolverRequest(statement.StatementId, Policy(projectId)));
        var resolved = Assert.Single(references.References);
        Assert.True(resolved.Included);
        Assert.Equal($"/unit/{seeded.RecordId:D}", resolved.Locator);
    }

    [Fact]
    public async Task RecallSynthesis_MergesRelatedSelectedMemoriesIntoSingleGroundedStatement()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var first = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000083"),
            "Deployment rollback",
            "Use rollback runbook when health checks fail.",
            "Use rollback runbook when health checks fail.");
        var second = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000084"),
            "Deployment rollback",
            "Notify release owner after rollback starts.",
            "Notify release owner after rollback starts.");
        var traceId = await SeedRecallTraceAsync(fixture, projectId, "recall-synthesis-merge");
        var firstRef = CreateSourceRef(first, "Use rollback runbook when health checks fail.");
        var secondRef = CreateSourceRef(second, "Notify release owner after rollback starts.");
        var recallResult = new CognitiveMemoryRecallResult(
            traceId,
            new CognitiveMemoryRecallContextPack(
                CognitiveMemoryRecallContextPackId.New(),
                projectId,
                null,
                "Recall context",
                "Selected 2 source-backed memory candidate(s).",
                [
                    new CognitiveMemoryRecallContextSection(
                        new CognitiveMemorySectionId("selected-0"),
                        CognitiveMemoryRecallContextSectionKind.SelectedMemory,
                        "Deployment rollback",
                        "Use rollback runbook when health checks fail.",
                        [new CognitiveMemoryRecordId(first.RecordId)],
                        [],
                        [firstRef]),
                    new CognitiveMemoryRecallContextSection(
                        new CognitiveMemorySectionId("selected-1"),
                        CognitiveMemoryRecallContextSectionKind.SelectedMemory,
                        "Deployment rollback",
                        "Notify release owner after rollback starts.",
                        [new CognitiveMemoryRecordId(second.RecordId)],
                        [],
                        [secondRef])
                ],
                [firstRef, secondRef],
                [],
                new Dictionary<string, string>()),
            [],
            [],
            []);
        var synthesis = new CognitiveMemoryRecallSynthesisService(fixture.Factory, fixture.Clock);

        var result = await synthesis.SynthesizeAsync(new CognitiveMemoryRecallSynthesisRequest(recallResult, Policy(projectId)));

        var statement = Assert.Single(result.Statements);
        Assert.Contains("Use rollback runbook", statement.Text, StringComparison.Ordinal);
        Assert.Contains("Notify release owner", statement.Text, StringComparison.Ordinal);
        Assert.Equal(2, statement.SourceRefs.Count);
        Assert.DoesNotContain($"/unit/{first.RecordId:D}", result.Brief, StringComparison.Ordinal);
        Assert.DoesNotContain($"/unit/{second.RecordId:D}", result.Brief, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReferenceResolver_DeniesRestrictedReferenceWithoutLocatorOrSummary()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var seeded = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000085"),
            "Credential rotation",
            "Credential rotation has restricted evidence.",
            "Credential rotation restricted evidence.");
        var traceId = await SeedRecallTraceAsync(fixture, projectId, "recall-restricted-reference");
        var sourceRef = CreateSourceRef(
            seeded,
            "Restricted source summary",
            accessLevel: CognitiveMemoryAccessLevel.Project,
            redactionState: CognitiveMemoryRedactionState.Restricted);
        var recallResult = new CognitiveMemoryRecallResult(
            traceId,
            new CognitiveMemoryRecallContextPack(
                CognitiveMemoryRecallContextPackId.New(),
                projectId,
                null,
                "Recall context",
                "Selected 1 source-backed memory candidate(s).",
                [
                    new CognitiveMemoryRecallContextSection(
                        new CognitiveMemorySectionId("selected-0"),
                        CognitiveMemoryRecallContextSectionKind.SelectedMemory,
                        "Credential rotation",
                        "Credential rotation has restricted evidence.",
                        [new CognitiveMemoryRecordId(seeded.RecordId)],
                        [],
                        [sourceRef])
                ],
                [sourceRef],
                [],
                new Dictionary<string, string>()),
            [],
            [],
            []);
        var synthesis = new CognitiveMemoryRecallSynthesisService(fixture.Factory, fixture.Clock);
        var result = await synthesis.SynthesizeAsync(new CognitiveMemoryRecallSynthesisRequest(recallResult, Policy(projectId)));
        var statement = Assert.Single(result.Statements);
        var resolver = new CognitiveMemoryReferenceResolver(fixture.Factory);

        var references = await resolver.ResolveAsync(new CognitiveMemoryReferenceResolverRequest(statement.StatementId, Policy(projectId)));

        var resolved = Assert.Single(references.References);
        Assert.False(resolved.Included);
        Assert.Equal(string.Empty, resolved.Locator);
        Assert.Equal(string.Empty, resolved.Summary);
        Assert.Equal(CognitiveMemoryRecallExclusionReasonKind.RedactedSource, resolved.ExclusionReasonKind);
    }

    private static ICognitiveMemoryDreamConsolidationService CreateDreamService(QualityFixture fixture)
    {
        var planner = new CognitiveMemoryClusterPlanner(fixture.Factory, fixture.Clock);
        var validator = new CognitiveMemoryDreamValidator(fixture.Factory, fixture.Clock);
        return new CognitiveMemoryDreamConsolidationService(
            fixture.Factory,
            planner,
            validator,
            fixture.Clock);
    }

    private static async Task<Guid> SeedRecallTraceAsync(
        QualityFixture fixture,
        Guid projectId,
        string requestMaterial)
    {
        var traceId = Guid.NewGuid();
        fixture.DbContext.Add(new CognitiveMemoryRecallTraceRecord
        {
            Id = traceId,
            ProjectId = projectId,
            OperationMode = CognitiveMemoryOperationMode.Recall,
            RecallMode = CognitiveMemoryRecallMode.FocusedTaskContext,
            RequestedByActorId = "agent:test",
            PolicyProfileId = "policy:test",
            RequestHash = CognitiveMemoryHash.FromUtf8(requestMaterial).Value,
            AlgorithmVersion = "unit-test",
            Outcome = CognitiveMemoryRunStatus.Succeeded,
            StartedAtUtc = fixture.Clock.GetUtcNow(),
            CompletedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        });
        await fixture.DbContext.SaveChangesAsync();
        return traceId;
    }

    private static CognitiveMemoryRecallSourceRef CreateSourceRef(
        SeededMemory seeded,
        string summary,
        CognitiveMemoryAccessLevel accessLevel = CognitiveMemoryAccessLevel.Project,
        CognitiveMemoryRedactionState redactionState = CognitiveMemoryRedactionState.Safe)
        => new(
            new CognitiveMemoryRecordId(seeded.RecordId),
            new CognitiveMemorySourceItemId(seeded.SourceItemId),
            new CognitiveMemoryEvidenceAnchorId(seeded.EvidenceAnchorId),
            "unit-test",
            $"/unit/{seeded.RecordId:D}",
            summary,
            accessLevel,
            redactionState,
            IncludedInContext: true,
            CognitiveMemoryRecallExclusionReasonKind.None);

    private sealed class ThrowingClusterPlanner : ICognitiveMemoryClusterPlanner
    {
        public ValueTask<CognitiveMemoryClusterPlanningResult> PlanAsync(
            CognitiveMemoryClusterPlanningRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Planner failure with SECRET_TOKEN=masked.");
    }

    private static CognitiveMemoryPolicyContext Policy(Guid projectId)
        => new(
            projectId,
            "agent:test",
            CognitiveMemoryAccessLevel.Project,
            new CognitiveMemoryPolicyProfileId("policy:test"),
            CognitiveMemoryRiskLevel.Low,
            AllowRestrictedContent: false);

    private static async Task<SeededMemory> SeedLinkedMemoryAsync(
        QualityFixture fixture,
        Guid projectId,
        Guid recordId,
        string title,
        string canonicalText,
        string sourceText,
        string? topicKey = null,
        string sourceSystem = "unit-test",
        string sourceItemType = "test-node",
        CognitiveMemoryAccessLevel sourceAccessLevel = CognitiveMemoryAccessLevel.Project,
        CognitiveMemoryRedactionState sourceRedactionState = CognitiveMemoryRedactionState.Safe)
    {
        var sourceHash = CognitiveMemoryHash.FromUtf8(sourceText).Value;
        var manifest = new CognitiveMemorySourceManifestRecord
        {
            ProjectId = projectId,
            SourceSystem = sourceSystem,
            SourceScopeKey = projectId.ToString("D"),
            SourceSnapshotId = $"snapshot-{recordId:D}",
            SnapshotHash = CognitiveMemoryHash.FromUtf8($"snapshot-{recordId:D}").Value,
            ProviderVersion = "unit-test-v1",
            ScanStatus = CognitiveMemoryRunStatus.Succeeded,
            ObservedAtUtc = fixture.Clock.GetUtcNow(),
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            UpdatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        var sourceItem = new CognitiveMemorySourceItemRecord
        {
            ProjectId = projectId,
            SourceManifestId = manifest.Id,
            SourceSystem = sourceSystem,
            SourceItemKey = $"source-{recordId:D}",
            SourceItemType = sourceItemType,
            Title = title,
            ContentText = sourceText,
            Locator = $"/unit/{recordId:D}",
            ContentHash = sourceHash,
            RedactionState = sourceRedactionState,
            AccessLevel = sourceAccessLevel,
            AccessScope = "unit",
            ObservedAtUtc = fixture.Clock.GetUtcNow(),
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            UpdatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        var anchor = new CognitiveMemoryEvidenceAnchorRecord
        {
            ProjectId = projectId,
            AnchorKind = CognitiveMemoryEvidenceAnchorKind.TextSpan,
            SourceManifestId = manifest.Id,
            SourceItemId = sourceItem.Id,
            SourceSystem = sourceSystem,
            Locator = sourceItem.Locator,
            StructuredPath = "$.content",
            TextStart = 0,
            TextEnd = sourceText.Length,
            QuoteHash = CognitiveMemoryHash.FromUtf8($"{recordId:D}:quote").Value,
            TrustLevel = CognitiveMemorySourceTrustLevel.RuntimeSource,
            RedactionState = sourceRedactionState,
            SourceHash = sourceHash,
            ObservedAtUtc = fixture.Clock.GetUtcNow(),
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        var memory = new CognitiveMemoryRecord
        {
            Id = recordId,
            ProjectId = projectId,
            Kind = CognitiveMemoryRecordKind.Semantic,
            Origin = CognitiveMemoryRecordOrigin.SourceDerived,
            Title = title,
            CanonicalText = canonicalText,
            SummaryText = canonicalText,
            TopicKey = topicKey ?? title.ToLowerInvariant().Replace(' ', '.'),
            ValidationState = CognitiveMemoryValidationState.Approved,
            StabilityState = CognitiveMemoryStabilityState.Active,
            CreatedInMode = CognitiveMemoryOperationMode.Observe,
            AlgorithmVersion = "unit-test",
            ContentHash = CognitiveMemoryHash.FromUtf8(canonicalText).Value,
            SourceEvidenceCount = 1,
            EvidenceAnchorCount = 1,
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            RiskLevel = CognitiveMemoryRiskLevel.Low,
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            UpdatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        fixture.DbContext.AddRange(
            manifest,
            sourceItem,
            anchor,
            memory,
            new CognitiveMemorySourceLinkRecord
            {
                MemoryRecordId = memory.Id,
                SourceManifestId = manifest.Id,
                SourceItemId = sourceItem.Id,
                EvidenceRole = CognitiveMemoryEvidenceRole.PrimarySource,
                Locator = sourceItem.Locator,
                QuoteHash = anchor.QuoteHash,
                Summary = sourceText,
                CreatedAtUtc = fixture.Clock.GetUtcNow()
            },
            new CognitiveMemoryRecordEvidenceAnchorRecord
            {
                MemoryRecordId = memory.Id,
                EvidenceAnchorId = anchor.Id,
                EvidenceRole = CognitiveMemoryEvidenceRole.PrimarySource,
                Summary = sourceText,
                CreatedAtUtc = fixture.Clock.GetUtcNow()
            });
        await fixture.DbContext.SaveChangesAsync();
        return new SeededMemory(memory.Id, sourceItem.Id, anchor.Id);
    }

    private static async Task<QualityFixture> CreateFixtureAsync()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return new QualityFixture(connection, new TestDbContextFactory(options), dbContext, new FixedClock());
    }

    private sealed record SeededMemory(
        Guid RecordId,
        Guid SourceItemId,
        Guid EvidenceAnchorId);

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
    }

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed class QualityFixture(
        SqliteConnection connection,
        TestDbContextFactory factory,
        AppDbContext dbContext,
        FixedClock clock) : IAsyncDisposable
    {
        public TestDbContextFactory Factory { get; } = factory;

        public AppDbContext DbContext { get; } = dbContext;

        public FixedClock Clock { get; } = clock;

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
