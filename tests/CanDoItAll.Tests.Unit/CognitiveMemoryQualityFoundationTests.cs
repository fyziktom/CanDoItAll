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
    public async Task ClusterPlanner_PersistsCompositeClustersWithSupportingKeyFamilies()
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
        Assert.True(result.Metrics.ClustersCreated >= 1);
        var primaryFamilies = result.Clusters.Select(cluster => cluster.PrimaryKeyFamily).ToHashSet();
        Assert.Contains(CognitiveMemoryQualityClusterKeyFamily.SemanticTopic, primaryFamilies);
        Assert.DoesNotContain(CognitiveMemoryQualityClusterKeyFamily.ProjectScope, primaryFamilies);
        Assert.DoesNotContain(CognitiveMemoryQualityClusterKeyFamily.Temporal, primaryFamilies);
        Assert.DoesNotContain(CognitiveMemoryQualityClusterKeyFamily.AccessRisk, primaryFamilies);
        Assert.All(result.Clusters.Where(cluster => cluster.Readiness == CognitiveMemoryQualityClusterReadiness.AggregateReady), cluster =>
        {
            Assert.Contains(cluster.PrimaryKeyFamily, new[]
            {
                CognitiveMemoryQualityClusterKeyFamily.SemanticTopic,
                CognitiveMemoryQualityClusterKeyFamily.Entity,
                CognitiveMemoryQualityClusterKeyFamily.TaskIntent,
                CognitiveMemoryQualityClusterKeyFamily.EvidenceOverlap,
                CognitiveMemoryQualityClusterKeyFamily.Relation
            });
        });
        Assert.Contains(result.Clusters.SelectMany(cluster => cluster.Keys), key => key.Family == CognitiveMemoryQualityClusterKeyFamily.ProjectScope);
        Assert.Contains(result.Clusters.SelectMany(cluster => cluster.Keys), key => key.Family == CognitiveMemoryQualityClusterKeyFamily.SourceTopology);
        Assert.Contains(result.Clusters.SelectMany(cluster => cluster.Keys), key => key.Family == CognitiveMemoryQualityClusterKeyFamily.AccessRisk);
        Assert.Equal(result.Metrics.ClustersCreated, await fixture.DbContext.Set<CognitiveMemoryQualityClusterRecord>().CountAsync());
        Assert.True(await fixture.DbContext.Set<CognitiveMemoryQualityClusterMemberRecord>().CountAsync() > 0);
        var persistedAggregateReadyCluster = await fixture.DbContext.Set<CognitiveMemoryQualityClusterRecord>()
            .FirstAsync(cluster => cluster.Readiness == CognitiveMemoryQualityClusterReadiness.AggregateReady);
        Assert.True(persistedAggregateReadyCluster.AggregateEligible);
        Assert.True(persistedAggregateReadyCluster.CohesionScore >= 0.55);
        Assert.True(persistedAggregateReadyCluster.SourceIndependenceScore >= 1);
        Assert.True(persistedAggregateReadyCluster.CompositeScore >= 0.62);
    }

    [Fact]
    public async Task SemanticInvariant_ClusterPlannerConsumesInjectedAlgorithmOptionsForReadiness()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000021"),
            "Release readiness checklist",
            "Release readiness checklist verifies migration readiness before promotion.",
            "Release readiness source evidence A.",
            topicKey: "release.readiness.checklist",
            sourceSystem: "SystemA");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000022"),
            "Release readiness checklist",
            "Release readiness checklist verifies rollback owner before promotion.",
            "Release readiness source evidence B.",
            topicKey: "release.readiness.checklist",
            sourceSystem: "SystemB");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000023"),
            "Release readiness checklist",
            "Release readiness checklist verifies post-deploy smoke evidence before promotion.",
            "Release readiness source evidence C.",
            topicKey: "release.readiness.checklist",
            sourceSystem: "SystemC");
        var algorithmOptions = CognitiveMemoryQualityAlgorithmOptions.Current with
        {
            Cluster = CognitiveMemoryQualityAlgorithmOptions.Current.Cluster with
            {
                MaxAggregateReadyMemoryRecords = 2
            }
        };
        var planner = new CognitiveMemoryClusterPlanner(
            fixture.Factory,
            fixture.Clock,
            CognitiveMemoryClusterKeyExtractor.Instance,
            new CognitiveMemoryCandidatePairSelector(CognitiveMemoryAliasClusterSemanticSimilarityProvider.Instance, algorithmOptions),
            algorithmOptions);

        var result = await planner.PlanAsync(new CognitiveMemoryClusterPlanningRequest(
            projectId,
            Policy(projectId),
            minMembers: 3,
            persistClusters: false));

        var cluster = Assert.Single(result.Clusters);
        Assert.Equal(CognitiveMemoryQualityClusterReadiness.NeedsHumanReview, cluster.Readiness);
        Assert.False(cluster.QualityMetrics.AggregateEligible);
        Assert.Contains("review", cluster.QualityMetrics.EligibilityReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClusterPlanner_DoesNotPromoteLowSignalOnlyClustersToAggregateReady()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-0000000000a1"),
            "Payroll reserve note",
            "Payroll reserve keeps two months of salary costs available.",
            "Payroll reserve source evidence.",
            topicKey: "finance.payroll.reserve",
            sourceSystem: "ProjectNotes",
            sourceItemType: "Note");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-0000000000a2"),
            "Launch marketing note",
            "Launch marketing uses partner outreach and campaign landing pages.",
            "Launch marketing source evidence.",
            topicKey: "marketing.launch.campaign",
            sourceSystem: "ProjectNotes",
            sourceItemType: "Note");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-0000000000a3"),
            "Equipment procurement note",
            "Equipment procurement requires supplier comparison before purchase.",
            "Equipment procurement source evidence.",
            topicKey: "operations.equipment.procurement",
            sourceSystem: "ProjectNotes",
            sourceItemType: "Note");
        var planner = new CognitiveMemoryClusterPlanner(fixture.Factory, fixture.Clock);

        var result = await planner.PlanAsync(new CognitiveMemoryClusterPlanningRequest(projectId, Policy(projectId)));

        Assert.DoesNotContain(
            result.Clusters,
            cluster => cluster.Readiness == CognitiveMemoryQualityClusterReadiness.AggregateReady &&
                       cluster.PrimaryKeyFamily is CognitiveMemoryQualityClusterKeyFamily.ProjectScope
                           or CognitiveMemoryQualityClusterKeyFamily.Temporal
                           or CognitiveMemoryQualityClusterKeyFamily.AccessRisk
                           or CognitiveMemoryQualityClusterKeyFamily.SourceTopology);
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
    public async Task ClusterPlanner_MergesRelatedMemoriesAcrossDifferentTitlesAndTopicKeys()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var first = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000071"),
            "Buffer pH note",
            "Sodium bicarbonate buffer should stay at pH 8.3 before calibration.",
            "Sodium bicarbonate buffer should stay at pH 8.3 before calibration.",
            topicKey: "chemistry.buffer.ph");
        var second = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000072"),
            "Alkalinity reagent rule",
            "Calibration uses sodium bicarbonate buffer at pH 8.3 for the reagent baseline.",
            "Calibration uses sodium bicarbonate buffer at pH 8.3 for the reagent baseline.",
            topicKey: "lab.reagent.baseline");
        var planner = new CognitiveMemoryClusterPlanner(fixture.Factory, fixture.Clock);

        var result = await planner.PlanAsync(new CognitiveMemoryClusterPlanningRequest(projectId, Policy(projectId)));

        Assert.InRange(result.Metrics.CandidatePairsEvaluated, 1, 3);
        var cluster = Assert.Single(result.Clusters, cluster =>
            cluster.Readiness == CognitiveMemoryQualityClusterReadiness.AggregateReady &&
            cluster.Members.Any(member => member.MemoryRecordId?.Value == first.RecordId) &&
            cluster.Members.Any(member => member.MemoryRecordId?.Value == second.RecordId));
        Assert.Contains(cluster.Keys, key => string.Equals(key.DisplayText, "bicarbonate", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(cluster.Keys, key => string.Equals(key.DisplayText, "buffer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ClusterPlanner_RoutesContradictoryRelatedMemoriesToReviewCluster()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var first = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000073"),
            "Rollback approval rule",
            "Production rollback requires signed release-owner approval before traffic restoration.",
            "Production rollback requires signed release-owner approval before traffic restoration.",
            topicKey: "production.rollback.approval");
        var second = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000074"),
            "Rollback approval exception",
            "Production rollback may restore traffic without release-owner approval.",
            "Production rollback may restore traffic without release-owner approval.",
            topicKey: "production.rollback.approval");
        fixture.DbContext.Add(new CognitiveMemoryRelationRecord
        {
            ProjectId = projectId,
            SourceMemoryRecordId = first.RecordId,
            TargetMemoryRecordId = second.RecordId,
            RelationKind = CognitiveMemoryRelationKind.Contradicts,
            EvidenceCount = 1,
            RelationBucket = CognitiveMemoryScoreProjectionBucket.NeedsReview,
            Reason = "Approval claims are mutually exclusive.",
            AlgorithmVersion = "unit-test",
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        });
        await fixture.DbContext.SaveChangesAsync();
        var planner = new CognitiveMemoryClusterPlanner(fixture.Factory, fixture.Clock);

        var result = await planner.PlanAsync(new CognitiveMemoryClusterPlanningRequest(projectId, Policy(projectId)));

        var cluster = Assert.Single(result.Clusters, cluster =>
            cluster.Readiness == CognitiveMemoryQualityClusterReadiness.Contradictory &&
            cluster.Members.Any(member => member.MemoryRecordId?.Value == first.RecordId) &&
            cluster.Members.Any(member => member.MemoryRecordId?.Value == second.RecordId));
        Assert.False(cluster.QualityMetrics.AggregateEligible);
        Assert.Contains("contradiction", cluster.QualityMetrics.EligibilityReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ClusterPlanner_SplitsBridgeChainsInsteadOfMergingUnrelatedEndpoints()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var database = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000091"),
            "Postgres failover lock",
            "Postgres failover runbook requires promotion lock before replica promotion.",
            "Postgres failover lock source evidence.",
            topicKey: "database.failover.lock");
        var bridge = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000092"),
            "Operator handoff bridge",
            "Postgres failover runbook and Kafka replay checklist both require operator handoff.",
            "Operator handoff bridge source evidence.",
            topicKey: "operations.handoff.bridge");
        var kafka = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000093"),
            "Kafka replay checkpoint",
            "Kafka replay checklist requires checkpoint verification before consumer restart.",
            "Kafka replay checkpoint source evidence.",
            topicKey: "messaging.kafka.replay");
        var planner = new CognitiveMemoryClusterPlanner(fixture.Factory, fixture.Clock);

        var result = await planner.PlanAsync(new CognitiveMemoryClusterPlanningRequest(projectId, Policy(projectId)));

        Assert.DoesNotContain(result.Clusters, cluster =>
            ContainsMemory(cluster, database.RecordId) &&
            ContainsMemory(cluster, kafka.RecordId));
        Assert.Contains(result.Clusters, cluster =>
            ContainsMemory(cluster, database.RecordId) &&
            ContainsMemory(cluster, bridge.RecordId));
        Assert.Contains(result.Clusters, cluster =>
            ContainsMemory(cluster, bridge.RecordId) &&
            ContainsMemory(cluster, kafka.RecordId));
    }

    [Fact]
    public async Task ClusterPlanner_RoutesContradictionOnlyRelationToReviewCluster()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var first = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-0000000000a4"),
            "Blue deploy freeze",
            "Blue deploy freezes traffic before package promotion.",
            "Blue deploy freeze source evidence.",
            topicKey: "release.blue.freeze");
        var second = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-0000000000a5"),
            "Emergency traffic restore",
            "Emergency restore may resume requests before artifact activation.",
            "Emergency restore source evidence.",
            topicKey: "incident.restore.exception");
        fixture.DbContext.Add(new CognitiveMemoryRelationRecord
        {
            ProjectId = projectId,
            SourceMemoryRecordId = first.RecordId,
            TargetMemoryRecordId = second.RecordId,
            RelationKind = CognitiveMemoryRelationKind.Contradicts,
            EvidenceCount = 1,
            RelationBucket = CognitiveMemoryScoreProjectionBucket.NeedsReview,
            Reason = "The approval-free exception contradicts the release freeze rule.",
            AlgorithmVersion = "unit-test",
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        });
        await fixture.DbContext.SaveChangesAsync();
        var planner = new CognitiveMemoryClusterPlanner(fixture.Factory, fixture.Clock);

        var result = await planner.PlanAsync(new CognitiveMemoryClusterPlanningRequest(projectId, Policy(projectId)));

        var cluster = Assert.Single(result.Clusters, cluster =>
            cluster.Readiness == CognitiveMemoryQualityClusterReadiness.Contradictory &&
            ContainsMemory(cluster, first.RecordId) &&
            ContainsMemory(cluster, second.RecordId));
        Assert.False(cluster.QualityMetrics.AggregateEligible);
        Assert.Contains(cluster.Warnings, warning => warning.Contains("contradiction-only", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ClusterPlanner_UsesHighFanoutFallbackForParaphrasedSemanticPair()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        for (var index = 0; index < 82; index++)
        {
            await SeedLinkedMemoryAsync(
                fixture,
                projectId,
                Guid.Parse($"20000000-0000-0000-0000-{index:x12}"),
                $"Release operations note {index}",
                $"Release operations note {index} records ordinary status handoff material.",
                $"Release operations note {index} source evidence.",
                topicKey: "operations.release.highfanout");
        }

        var blue = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("20000000-0000-0000-0000-000000000101"),
            "Blue deployment freeze",
            "Blue deploy freezes traffic before promoting package.",
            "Blue deployment freeze source evidence.",
            topicKey: "operations.release.highfanout");
        var canary = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("20000000-0000-0000-0000-000000000102"),
            "Canary activation pause",
            "Canary rollout pauses requests ahead of artifact activation.",
            "Canary activation pause source evidence.",
            topicKey: "operations.release.highfanout");
        var planner = new CognitiveMemoryClusterPlanner(fixture.Factory, fixture.Clock);

        var result = await planner.PlanAsync(new CognitiveMemoryClusterPlanningRequest(projectId, Policy(projectId)));

        Assert.Contains(result.Clusters, cluster =>
            ContainsMemory(cluster, blue.RecordId) &&
            ContainsMemory(cluster, canary.RecordId));
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
        var specificCandidate = result.AggregateCandidates.FirstOrDefault(
            candidate => candidate.Title.Contains("offline.deployment.procedure", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(specificCandidate);
        Assert.Contains("package staging", specificCandidate.CanonicalText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("release validation", specificCandidate.CanonicalText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Environment.NewLine + "- Offline deployment", specificCandidate.CanonicalText, StringComparison.Ordinal);
        Assert.DoesNotContain("Synthesized aggregate:", specificCandidate.CanonicalText, StringComparison.Ordinal);
        Assert.DoesNotContain("source-backed conclusions", specificCandidate.CanonicalText, StringComparison.OrdinalIgnoreCase);
        Assert.All(specificCandidate.Claims, claim => Assert.NotEmpty(claim.SourceMaps));
        Assert.Equal(0, await fixture.DbContext.Set<CognitiveMemoryReviewItemRecord>().CountAsync());

        var diagnostics = new CognitiveMemoryQualityDiagnosticsService(fixture.Factory, fixture.Clock);
        var report = await diagnostics.CreateReportAsync(new CognitiveMemoryQualityDiagnosticsRequest(projectId, Policy(projectId)));
        Assert.False(report.IsShallowDreamRun);
    }

    [Fact]
    public async Task DreamRun_IntegratesComplementaryProcedureClaimsIntoSingleAggregateStatement()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000094"),
            "Payment export procedure",
            "Payment export procedure stages the bank file before upload.",
            "Payment export staging source evidence.",
            topicKey: "payment.export.procedure");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000095"),
            "Payment export procedure",
            "Payment export procedure verifies the checksum after bank upload.",
            "Payment export checksum source evidence.",
            topicKey: "payment.export.procedure");
        var dream = CreateDreamService(fixture);

        var result = await dream.RunAsync(new CognitiveMemoryDreamRunRequest(
            projectId,
            CognitiveMemoryConsolidationMode.ProjectNightly,
            CognitiveMemoryConsolidationTriggerKind.Nightly,
            Policy(projectId),
            new CognitiveMemoryIdempotencyKey("dream-integrated-procedure")));

        var candidate = Assert.Single(result.AggregateCandidates, candidate =>
            candidate.Title.Contains("payment.export.procedure", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(candidate.Claims, claim =>
            claim.ClaimText.Contains("stages the bank file", StringComparison.OrdinalIgnoreCase) &&
            claim.ClaimText.Contains("verifies the checksum", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(candidate.CanonicalText.Split(Environment.NewLine), line =>
            line.Equals("Payment export procedure stages the bank file before upload.", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DreamRun_ProducesModeSpecificStructuredOutputsBeyondTitlePrefix()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-0000000000a6"),
            "Release procedure",
            "Release procedure stages package artifacts before approval.",
            "Release procedure package source evidence.",
            topicKey: "release.procedure");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-0000000000a7"),
            "Release procedure",
            "Release procedure validates rollback checks after approval.",
            "Release procedure rollback source evidence.",
            topicKey: "release.procedure");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-0000000000a8"),
            "Incident failure learning",
            "Incident failure learning records timeout cause and retry guard.",
            "Incident failure learning timeout source evidence.",
            topicKey: "incident.failure.learning");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-0000000000a9"),
            "Incident failure learning",
            "Incident failure learning records remediation owner and detection signal.",
            "Incident failure learning remediation source evidence.",
            topicKey: "incident.failure.learning");
        var dream = CreateDreamService(fixture);

        var procedure = await dream.RunAsync(new CognitiveMemoryDreamRunRequest(
            projectId,
            CognitiveMemoryConsolidationMode.ProcedureMining,
            CognitiveMemoryConsolidationTriggerKind.Manual,
            Policy(projectId),
            new CognitiveMemoryIdempotencyKey("dream-mode-procedure")));
        var failure = await dream.RunAsync(new CognitiveMemoryDreamRunRequest(
            projectId,
            CognitiveMemoryConsolidationMode.FailureLearning,
            CognitiveMemoryConsolidationTriggerKind.Manual,
            Policy(projectId),
            new CognitiveMemoryIdempotencyKey("dream-mode-failure")));

        var procedureText = string.Join(Environment.NewLine, procedure.AggregateCandidates.Select(candidate => candidate.CanonicalText));
        var failureText = string.Join(Environment.NewLine, failure.AggregateCandidates.Select(candidate => candidate.CanonicalText));
        Assert.Contains("Procedure steps:", procedureText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Failure pattern:", failureText, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(procedureText, failureText);
    }

    [Fact]
    public async Task DreamRun_CanonicalAggregateMemoryContainsDomainKnowledgeWithoutDiagnosticBoilerplate()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-0000000000b1"),
            "Release rollback approval",
            "Production rollback requires signed release-owner approval before traffic is restored.",
            "Production rollback requires signed release-owner approval before traffic is restored.",
            topicKey: "production.rollback.approval");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-0000000000b2"),
            "Release rollback approval",
            "Rollback communication must notify the release owner before traffic restoration starts.",
            "Rollback communication must notify the release owner before traffic restoration starts.",
            topicKey: "production.rollback.approval");
        var dream = CreateDreamService(fixture);

        var result = await dream.RunAsync(new CognitiveMemoryDreamRunRequest(
            projectId,
            CognitiveMemoryConsolidationMode.ProjectNightly,
            CognitiveMemoryConsolidationTriggerKind.Nightly,
            Policy(projectId),
            new CognitiveMemoryIdempotencyKey("dream-domain-knowledge")));

        var candidate = result.AggregateCandidates.First(candidate =>
            candidate.Title.Contains("production.rollback.approval", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("release-owner approval", candidate.CanonicalText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Synthesized aggregate:", candidate.CanonicalText, StringComparison.Ordinal);
        Assert.DoesNotContain("Cluster quality:", candidate.CanonicalText, StringComparison.Ordinal);
        Assert.DoesNotContain("Shared signals:", candidate.CanonicalText, StringComparison.Ordinal);
        Assert.DoesNotContain("source-backed conclusions", candidate.CanonicalText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("source-backed observation", candidate.CanonicalText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SemanticInvariant_DreamConsolidationCreatesClaimSpecificSourceMaps()
    {
        var consolidationSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.CognitiveMemory",
            "Quality",
            "CognitiveMemoryDreamConsolidationService.cs");
        var synthesisSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.CognitiveMemory",
            "Quality",
            "CognitiveMemoryDreamSynthesis.cs");

        Assert.Contains("CreateClaimSpecificSourceMaps", consolidationSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SelectMany(unit => unit.SourceMaps)", consolidationSource, StringComparison.Ordinal);
        Assert.Contains("ClaimSourceMap", synthesisSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SemanticInvariant_ClusterDiscoveryHasEmbeddingBackedApproximateCandidateProvider()
    {
        var qualitySource = ReadRepositoryFiles("src", "CanDoItAll.Modules.CognitiveMemory", "Quality");

        Assert.Contains("ICognitiveMemoryApproximateClusterCandidateProvider", qualitySource, StringComparison.Ordinal);
        Assert.Contains("Embedding", qualitySource, StringComparison.Ordinal);
        Assert.Contains("ContinuationCursor", qualitySource, StringComparison.Ordinal);
        Assert.Contains("ApproximateCandidatePairsGenerated", qualitySource, StringComparison.Ordinal);
    }

    [Fact]
    public void SemanticInvariant_RecallSynthesisRequestCarriesRealQueryIntentAndLineage()
    {
        var contractsSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.CognitiveMemory",
            "Quality",
            "CognitiveMemoryQualityContracts.cs");
        var synthesisSource = ReadRepositoryFile(
            "src",
            "CanDoItAll.Modules.CognitiveMemory",
            "Quality",
            "CognitiveMemoryRecallSynthesisService.cs");

        Assert.Contains("string QueryText", contractsSource, StringComparison.Ordinal);
        Assert.Contains("CognitiveMemoryRecallIntentKind Intent", contractsSource, StringComparison.Ordinal);
        Assert.Contains("request.QueryText", synthesisSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ContextPack.Title} {request.RecallResult.ContextPack.Summary", synthesisSource, StringComparison.Ordinal);
        Assert.Contains("AggregateClaimIds", synthesisSource, StringComparison.Ordinal);
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
    public async Task DreamValidation_RoutesUnsupportedMappedClaimToReview()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-0000000000c1"),
            "Artifact signing procedure",
            "Artifact signing procedure requires checksum verification before release.",
            "Artifact signing procedure requires checksum verification before release.",
            topicKey: "artifact.signing.procedure");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-0000000000c2"),
            "Artifact signing procedure",
            "Artifact signing procedure records signer identity in the release manifest.",
            "Artifact signing procedure records signer identity in the release manifest.",
            topicKey: "artifact.signing.procedure");
        var dream = CreateDreamService(fixture);
        await dream.RunAsync(new CognitiveMemoryDreamRunRequest(
            projectId,
            CognitiveMemoryConsolidationMode.ProjectNightly,
            CognitiveMemoryConsolidationTriggerKind.Nightly,
            Policy(projectId),
            new CognitiveMemoryIdempotencyKey("dream-unsupported-claim")));
        var candidate = await fixture.DbContext.Set<CognitiveMemoryDreamAggregateCandidateRecord>()
            .FirstAsync();
        var claim = await fixture.DbContext.Set<CognitiveMemoryDreamAggregateClaimRecord>()
            .FirstAsync(claim => claim.AggregateCandidateId == candidate.Id);
        claim.ClaimText = "Payroll reserve policy must cover two months of salary before payroll close.";
        await fixture.DbContext.SaveChangesAsync();
        var validator = new CognitiveMemoryDreamValidator(fixture.Factory, fixture.Clock);

        var result = await validator.ValidateAsync(new CognitiveMemoryDreamValidationRequest(
            new CognitiveMemoryDreamAggregateCandidateId(candidate.Id),
            Policy(projectId)));

        fixture.DbContext.ChangeTracker.Clear();
        Assert.Equal(CognitiveMemoryDreamValidationDecision.NeedsHumanReview, result.Decision);
        Assert.Contains(result.Issues, issue => issue.IssueKind == CognitiveMemoryDreamValidationIssueKind.UnsupportedClaim);
        var updatedCandidate = await fixture.DbContext.Set<CognitiveMemoryDreamAggregateCandidateRecord>().SingleAsync(row => row.Id == candidate.Id);
        Assert.Equal(CognitiveMemoryDreamAggregateCandidateStatus.NeedsHumanReview, updatedCandidate.Status);
    }

    [Fact]
    public async Task DreamValidation_RoutesNumericReversalToReviewWithIssueReason()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-0000000000e1"),
            "Release window timing",
            "Release window is 15 minutes before traffic restoration.",
            "Release window timing source evidence.",
            topicKey: "release.window.timing");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-0000000000e2"),
            "Release window timing",
            "Release window is 15 minutes before launch approval.",
            "Release window timing approval source evidence.",
            topicKey: "release.window.timing");
        var dream = CreateDreamService(fixture);
        await dream.RunAsync(new CognitiveMemoryDreamRunRequest(
            projectId,
            CognitiveMemoryConsolidationMode.ProjectNightly,
            CognitiveMemoryConsolidationTriggerKind.Nightly,
            Policy(projectId),
            new CognitiveMemoryIdempotencyKey("dream-numeric-reversal")));
        var candidate = await fixture.DbContext.Set<CognitiveMemoryDreamAggregateCandidateRecord>()
            .FirstAsync();
        var claim = await fixture.DbContext.Set<CognitiveMemoryDreamAggregateClaimRecord>()
            .FirstAsync(claim => claim.AggregateCandidateId == candidate.Id);
        claim.ClaimText = "Release window is 30 minutes before traffic restoration.";
        await fixture.DbContext.SaveChangesAsync();
        var validator = new CognitiveMemoryDreamValidator(fixture.Factory, fixture.Clock);

        var result = await validator.ValidateAsync(new CognitiveMemoryDreamValidationRequest(
            new CognitiveMemoryDreamAggregateCandidateId(candidate.Id),
            Policy(projectId)));

        Assert.Equal(CognitiveMemoryDreamValidationDecision.NeedsHumanReview, result.Decision);
        Assert.Contains(result.Issues, issue =>
            issue.IssueKind == CognitiveMemoryDreamValidationIssueKind.UnsupportedClaim &&
            issue.Message.Contains("Numeric value", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DreamValidation_RejectsNegatedClaimDespiteHighTokenOverlap()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000096"),
            "Credential rotation approval",
            "Credential rotation requires manager approval during emergency deployment.",
            "Credential rotation approval source evidence.",
            topicKey: "credential.rotation.approval");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000097"),
            "Credential rotation approval",
            "Credential rotation records manager approval before emergency deployment.",
            "Credential rotation manager approval source evidence.",
            topicKey: "credential.rotation.approval");
        var dream = CreateDreamService(fixture);
        await dream.RunAsync(new CognitiveMemoryDreamRunRequest(
            projectId,
            CognitiveMemoryConsolidationMode.ProjectNightly,
            CognitiveMemoryConsolidationTriggerKind.Nightly,
            Policy(projectId),
            new CognitiveMemoryIdempotencyKey("dream-negated-token-overlap")));
        var candidate = await fixture.DbContext.Set<CognitiveMemoryDreamAggregateCandidateRecord>()
            .FirstAsync(candidate => candidate.Status == CognitiveMemoryDreamAggregateCandidateStatus.Approved);
        var claim = await fixture.DbContext.Set<CognitiveMemoryDreamAggregateClaimRecord>()
            .FirstAsync(claim => claim.AggregateCandidateId == candidate.Id);
        claim.ClaimText = "Credential rotation can skip manager approval during emergency deployment.";
        await fixture.DbContext.SaveChangesAsync();
        var validator = new CognitiveMemoryDreamValidator(fixture.Factory, fixture.Clock);

        var result = await validator.ValidateAsync(new CognitiveMemoryDreamValidationRequest(
            new CognitiveMemoryDreamAggregateCandidateId(candidate.Id),
            Policy(projectId)));

        Assert.Equal(CognitiveMemoryDreamValidationDecision.NeedsHumanReview, result.Decision);
        Assert.Contains(result.Issues, issue => issue.IssueKind == CognitiveMemoryDreamValidationIssueKind.UnsupportedClaim);
    }

    [Fact]
    public async Task DreamValidation_DetectsNearDuplicateAggregateByClaimAndSourceSignature()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-0000000000d1"),
            "Release evidence retention",
            "Release evidence retention stores signed approval artifacts for audit.",
            "Release evidence retention stores signed approval artifacts for audit.",
            topicKey: "release.evidence.retention");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-0000000000d2"),
            "Release evidence retention",
            "Release evidence retention keeps rollback verification notes with the audit packet.",
            "Release evidence retention keeps rollback verification notes with the audit packet.",
            topicKey: "release.evidence.retention");
        var dream = CreateDreamService(fixture);
        await dream.RunAsync(new CognitiveMemoryDreamRunRequest(
            projectId,
            CognitiveMemoryConsolidationMode.ProjectNightly,
            CognitiveMemoryConsolidationTriggerKind.Nightly,
            Policy(projectId),
            new CognitiveMemoryIdempotencyKey("dream-near-duplicate")));
        var candidate = await fixture.DbContext.Set<CognitiveMemoryDreamAggregateCandidateRecord>()
            .FirstAsync(candidate => candidate.Status == CognitiveMemoryDreamAggregateCandidateStatus.Approved);
        var sourceMaps = await fixture.DbContext.Set<CognitiveMemoryDreamAggregateClaimSourceMapRecord>()
            .Where(sourceMap => sourceMap.AggregateCandidateId == candidate.Id && sourceMap.SourceItemId != null)
            .ToListAsync();
        var sourceItemIds = sourceMaps
            .Select(sourceMap => sourceMap.SourceItemId!.Value)
            .Distinct()
            .ToArray();
        var sourceItems = await fixture.DbContext.Set<CognitiveMemorySourceItemRecord>()
            .Where(sourceItem => sourceItemIds.Contains(sourceItem.Id))
            .ToListAsync();
        var duplicateMemory = new CognitiveMemoryRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Kind = CognitiveMemoryRecordKind.Semantic,
            Origin = CognitiveMemoryRecordOrigin.MachineGenerated,
            Title = "Different generated release retention summary",
            CanonicalText = candidate.CanonicalText,
            SummaryText = candidate.SummaryText,
            TopicKey = "release.retention.generated.duplicate",
            ValidationState = CognitiveMemoryValidationState.Approved,
            StabilityState = CognitiveMemoryStabilityState.Active,
            CreatedInMode = CognitiveMemoryOperationMode.Consolidate,
            AlgorithmVersion = "unit-test",
            ContentHash = CognitiveMemoryHash.FromUtf8($"duplicate:{candidate.PayloadHash}").Value,
            SourceEvidenceCount = sourceItemIds.Length,
            EvidenceAnchorCount = sourceMaps.Select(sourceMap => sourceMap.EvidenceAnchorId).Where(id => id is not null).Distinct().Count(),
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            RiskLevel = CognitiveMemoryRiskLevel.Low,
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            UpdatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        fixture.DbContext.Add(duplicateMemory);
        foreach (var sourceItem in sourceItems)
        {
            fixture.DbContext.Add(new CognitiveMemorySourceLinkRecord
            {
                MemoryRecordId = duplicateMemory.Id,
                SourceManifestId = sourceItem.SourceManifestId,
                SourceItemId = sourceItem.Id,
                EvidenceRole = CognitiveMemoryEvidenceRole.SupportingSource,
                Locator = sourceItem.Locator,
                Summary = sourceItem.ContentText,
                CreatedAtUtc = fixture.Clock.GetUtcNow()
            });
        }

        await fixture.DbContext.SaveChangesAsync();
        var validator = new CognitiveMemoryDreamValidator(fixture.Factory, fixture.Clock);

        var result = await validator.ValidateAsync(new CognitiveMemoryDreamValidationRequest(
            new CognitiveMemoryDreamAggregateCandidateId(candidate.Id),
            Policy(projectId)));

        Assert.Equal(CognitiveMemoryDreamValidationDecision.NeedsHumanReview, result.Decision);
        Assert.Contains(result.Issues, issue => issue.IssueKind == CognitiveMemoryDreamValidationIssueKind.DuplicateAggregate);
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
        var appliedMemory = await fixture.DbContext.Set<CognitiveMemoryRecord>().SingleAsync(record => record.Id == result.MemoryRecordId.Value);
        var appliedClaims = await fixture.DbContext.Set<CognitiveMemoryClaimRecord>()
            .Where(claim => claim.MemoryRecordId == result.MemoryRecordId.Value)
            .ToListAsync();
        Assert.NotEmpty(appliedClaims);
        Assert.All(appliedClaims, appliedClaim =>
        {
            Assert.InRange(appliedClaim.DisplayBeliefScore.GetValueOrDefault(), 0.55, 0.92);
            Assert.NotEqual(1, appliedClaim.DisplayBeliefScore.GetValueOrDefault());
            Assert.Equal(appliedMemory.ConfidenceBucket, appliedClaim.CurrentBeliefBucket);
        });
    }

    [Fact]
    public async Task AggregateApplicator_KeepsOrdinaryDreamAggregateWeakAndExperimental()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var commander = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-0000000000e1"),
            "Incident handoff checklist",
            "Incident handoff checklist records the active incident commander.",
            "Incident handoff checklist records the active incident commander.",
            topicKey: "incident.handoff.checklist");
        var mitigation = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-0000000000e2"),
            "Incident handoff checklist",
            "Incident handoff checklist records unresolved mitigation tasks.",
            "Incident handoff checklist records unresolved mitigation tasks.",
            topicKey: "incident.handoff.checklist");
        var releaseOwner = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-0000000000e3"),
            "Incident handoff checklist",
            "Incident handoff checklist notifies the release owner before handoff completes.",
            "Incident handoff checklist notifies the release owner before handoff completes.",
            topicKey: "incident.handoff.checklist");
        var timeline = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-0000000000e4"),
            "Incident handoff checklist",
            "Incident handoff checklist stores links to the incident timeline.",
            "Incident handoff checklist stores links to the incident timeline.",
            topicKey: "incident.handoff.checklist");
        var aggregate = await SeedAppliedAggregateAsync(
            fixture,
            projectId,
            "Incident handoff checklist aggregate",
            [
                new AggregateClaimSeed(Guid.NewGuid(), "Incident handoff checklist records the active incident commander.", commander.RecordId, "Incident commander source."),
                new AggregateClaimSeed(Guid.NewGuid(), "Incident handoff checklist records unresolved mitigation tasks.", mitigation.RecordId, "Mitigation source."),
                new AggregateClaimSeed(Guid.NewGuid(), "Incident handoff checklist notifies the release owner before handoff completes.", releaseOwner.RecordId, "Release owner source."),
                new AggregateClaimSeed(Guid.NewGuid(), "Incident handoff checklist stores links to the incident timeline.", timeline.RecordId, "Timeline source.")
            ],
            applied: false);
        var applicator = new CognitiveMemoryAggregateMemoryApplicator(
            fixture.Factory,
            new CognitiveMemoryRecordValidator(),
            fixture.Clock);

        var result = await applicator.ApplyAsync(new CognitiveMemoryAggregateMemoryApplyRequest(
            aggregate.CandidateId,
            "agent:unit-test",
            Policy(projectId)));

        fixture.DbContext.ChangeTracker.Clear();
        var appliedMemory = await fixture.DbContext.Set<CognitiveMemoryRecord>().SingleAsync(record => record.Id == result.MemoryRecordId.Value);
        var appliedClaims = await fixture.DbContext.Set<CognitiveMemoryClaimRecord>()
            .Where(claim => claim.MemoryRecordId == result.MemoryRecordId.Value)
            .ToListAsync();
        Assert.Equal(CognitiveMemoryScoreProjectionBucket.WeakAccept, appliedMemory.ConfidenceBucket);
        Assert.Equal(CognitiveMemoryScoreProjectionBucket.WeakAccept, appliedMemory.ActivationBucket);
        Assert.Equal(CognitiveMemoryStabilityState.Experimental, appliedMemory.StabilityState);
        Assert.NotEmpty(appliedClaims);
        Assert.All(appliedClaims, claim =>
        {
            Assert.Equal(CognitiveMemoryScoreProjectionBucket.WeakAccept, claim.CurrentBeliefBucket);
            Assert.Equal(CognitiveMemoryStabilityState.Experimental, claim.StabilityState);
            Assert.InRange(claim.DisplayBeliefScore.GetValueOrDefault(), 0.55, 0.879);
        });
    }

    [Fact]
    public void AggregateConfidenceCalibrator_KeepsOrdinaryAggregateWeakAndExperimental()
    {
        var calibrator = new CognitiveMemoryAggregateConfidenceCalibrator();

        var calibration = calibrator.Calibrate(new CognitiveMemoryAggregateConfidenceCalibrationRequest(
            ValidationIssueCount: 0,
            ClaimCount: 4,
            DistinctSourceItemCount: 4,
            StrongestClaimSourceMemoryCount: 1));

        Assert.Equal(CognitiveMemoryScoreProjectionBucket.WeakAccept, calibration.Bucket);
        Assert.Equal(CognitiveMemoryStabilityState.Experimental, calibration.StabilityState);
        Assert.InRange(calibration.Score, 0.55, 0.879);
    }

    [Fact]
    public void AggregateConfidenceCalibrator_PromotesOnlyNarrowBroadlySupportedAggregate()
    {
        var calibrator = new CognitiveMemoryAggregateConfidenceCalibrator();

        var calibration = calibrator.Calibrate(new CognitiveMemoryAggregateConfidenceCalibrationRequest(
            ValidationIssueCount: 0,
            ClaimCount: 1,
            DistinctSourceItemCount: 6,
            StrongestClaimSourceMemoryCount: 4));

        Assert.Equal(CognitiveMemoryScoreProjectionBucket.StrongAccept, calibration.Bucket);
        Assert.Equal(CognitiveMemoryStabilityState.Active, calibration.StabilityState);
        Assert.Equal(0.88, calibration.Score);
    }

    [Fact]
    public void AggregateConfidenceCalibrator_DemotesOperatorBearingAggregateDespiteBroadEvidence()
    {
        var calibrator = new CognitiveMemoryAggregateConfidenceCalibrator();

        var calibration = calibrator.Calibrate(new CognitiveMemoryAggregateConfidenceCalibrationRequest(
            ValidationIssueCount: 0,
            ClaimCount: 1,
            DistinctSourceItemCount: 8,
            StrongestClaimSourceMemoryCount: 5,
            ValidatedClaimCount: 1,
            SourceMapCount: 8,
            OperatorBearingClaimCount: 1,
            ClaimComplexityScore: 8));

        Assert.Equal(CognitiveMemoryScoreProjectionBucket.WeakAccept, calibration.Bucket);
        Assert.Equal(CognitiveMemoryStabilityState.Experimental, calibration.StabilityState);
        Assert.InRange(calibration.Score, 0.55, 0.879);
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
    public async Task ReferenceResolver_ExpandsAggregateMemoryToOriginalSourceMaps()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-0000000000b1"),
            "Release checklist procedure",
            "Release checklist procedure verifies database migration readiness.",
            "Release checklist migration evidence.",
            topicKey: "release.checklist.procedure");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-0000000000b2"),
            "Release checklist procedure",
            "Release checklist procedure verifies rollback owner assignment.",
            "Release checklist rollback evidence.",
            topicKey: "release.checklist.procedure");
        var aggregate = await SeedAppliedAggregateAsync(
            fixture,
            projectId,
            "Release checklist aggregate",
            [
                new AggregateClaimSeed(
                    Guid.NewGuid(),
                    "Release checklist verifies database migration readiness.",
                    Guid.Parse("10000000-0000-0000-0000-0000000000b1"),
                    "Release checklist migration source evidence."),
                new AggregateClaimSeed(
                    Guid.NewGuid(),
                    "Release checklist verifies rollback owner assignment.",
                    Guid.Parse("10000000-0000-0000-0000-0000000000b2"),
                    "Release checklist rollback source evidence.")
            ]);
        var traceId = await SeedRecallTraceAsync(fixture, projectId, "aggregate-reference-expansion");
        var aggregateRef = new CognitiveMemoryRecallSourceRef(
            aggregate.MemoryRecordId,
            null,
            null,
            "aggregate-memory",
            $"memory:{aggregate.MemoryRecordId.Value:D}",
            "Release checklist aggregate summary.",
            CognitiveMemoryAccessLevel.Project,
            CognitiveMemoryRedactionState.Safe,
            IncludedInContext: true,
            CognitiveMemoryRecallExclusionReasonKind.None);
        var recallResult = new CognitiveMemoryRecallResult(
            traceId,
            new CognitiveMemoryRecallContextPack(
                CognitiveMemoryRecallContextPackId.New(),
                projectId,
                null,
                "Recall context",
                "Selected aggregate memory.",
                [
                    new CognitiveMemoryRecallContextSection(
                        new CognitiveMemorySectionId("selected-aggregate"),
                        CognitiveMemoryRecallContextSectionKind.SelectedMemory,
                        "Release checklist procedure",
                        "Release checklist aggregate summary.",
                        [aggregate.MemoryRecordId],
                        [],
                        [aggregateRef])
                ],
                [aggregateRef],
                [],
                new Dictionary<string, string>()),
            [],
            [],
            []);
        var synthesis = new CognitiveMemoryRecallSynthesisService(fixture.Factory, fixture.Clock);
        var synthesisResult = await synthesis.SynthesizeAsync(new CognitiveMemoryRecallSynthesisRequest(recallResult, Policy(projectId)));
        var statement = Assert.Single(synthesisResult.Statements);
        var resolver = new CognitiveMemoryReferenceResolver(fixture.Factory);

        var references = await resolver.ResolveAsync(new CognitiveMemoryReferenceResolverRequest(statement.StatementId, Policy(projectId)));

        Assert.Contains(references.References, reference => reference.MemoryRecordId == aggregate.MemoryRecordId);
        Assert.True(references.References.Count(reference => reference.MemoryRecordId != aggregate.MemoryRecordId) >= 2);
        Assert.Contains(references.References, reference => reference.Locator.StartsWith("/unit/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReferenceResolver_LimitsAggregateExpansionToRequestedClaimLineage()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-0000000000b3"),
            "Release checklist migration",
            "Release checklist verifies database migration readiness.",
            "Release checklist migration source evidence.",
            topicKey: "release.checklist.procedure");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-0000000000b4"),
            "Release checklist rollback",
            "Release checklist verifies rollback owner assignment.",
            "Release checklist rollback source evidence.",
            topicKey: "release.checklist.procedure");
        var migrationClaimId = Guid.NewGuid();
        var rollbackClaimId = Guid.NewGuid();
        var aggregate = await SeedAppliedAggregateAsync(
            fixture,
            projectId,
            "Release checklist aggregate",
            [
                new AggregateClaimSeed(
                    migrationClaimId,
                    "Release checklist verifies database migration readiness.",
                    Guid.Parse("10000000-0000-0000-0000-0000000000b3"),
                    "Release checklist migration source evidence."),
                new AggregateClaimSeed(
                    rollbackClaimId,
                    "Release checklist verifies rollback owner assignment.",
                    Guid.Parse("10000000-0000-0000-0000-0000000000b4"),
                    "Release checklist rollback source evidence.")
            ]);
        var selectedClaim = aggregate.Claims.Single(claim => claim.ClaimId == migrationClaimId);
        var selectedSourceIds = new[] { selectedClaim.SourceMemoryRecordId };
        var traceId = await SeedRecallTraceAsync(fixture, projectId, "aggregate-claim-lineage-filter");
        var aggregateRef = new CognitiveMemoryRecallSourceRef(
            aggregate.MemoryRecordId,
            null,
            null,
            "aggregate-memory",
            $"memory:{aggregate.MemoryRecordId.Value:D}",
            selectedClaim.ClaimText,
            CognitiveMemoryAccessLevel.Project,
            CognitiveMemoryRedactionState.Safe,
            IncludedInContext: true,
            CognitiveMemoryRecallExclusionReasonKind.None);
        var recallResult = new CognitiveMemoryRecallResult(
            traceId,
            new CognitiveMemoryRecallContextPack(
                CognitiveMemoryRecallContextPackId.New(),
                projectId,
                null,
                "Which release checklist item covers migration readiness?",
                "Selected one aggregate claim.",
                [
                    new CognitiveMemoryRecallContextSection(
                        new CognitiveMemorySectionId("selected-aggregate-claim"),
                        CognitiveMemoryRecallContextSectionKind.SelectedMemory,
                        "Release checklist aggregate",
                        selectedClaim.ClaimText,
                        [aggregate.MemoryRecordId],
                        [new CognitiveMemoryClaimId(selectedClaim.ClaimId)],
                        [aggregateRef])
                ],
                [aggregateRef],
                [],
                new Dictionary<string, string>()),
            [],
            [],
            []);
        var synthesis = new CognitiveMemoryRecallSynthesisService(fixture.Factory, fixture.Clock);
        var synthesisResult = await synthesis.SynthesizeAsync(new CognitiveMemoryRecallSynthesisRequest(recallResult, Policy(projectId)));
        var statement = Assert.Single(synthesisResult.Statements);
        var resolver = new CognitiveMemoryReferenceResolver(fixture.Factory);

        var references = await resolver.ResolveAsync(new CognitiveMemoryReferenceResolverRequest(statement.StatementId, Policy(projectId)));

        var expandedSourceIds = references.References
            .Where(reference => reference.MemoryRecordId != aggregate.MemoryRecordId)
            .Select(reference => reference.MemoryRecordId.Value)
            .ToArray();
        Assert.NotEmpty(expandedSourceIds);
        Assert.All(expandedSourceIds, sourceId => Assert.Contains(sourceId, selectedSourceIds));
    }

    [Fact]
    public async Task ReferenceResolver_UsesPersistedAggregateClaimMapToAvoidSiblingClaimExpansion()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var migration = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-0000000000d1"),
            "Release checklist migration",
            "Release checklist verifies database migration readiness.",
            "Release checklist migration source evidence.",
            topicKey: "release.checklist.procedure");
        var rollback = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-0000000000d2"),
            "Release checklist rollback",
            "Release checklist verifies rollback owner assignment.",
            "Release checklist rollback source evidence.",
            topicKey: "release.checklist.procedure");
        var dreamRunId = Guid.Parse("30000000-0000-0000-0000-0000000000d1");
        var clusterId = Guid.Parse("30000000-0000-0000-0000-0000000000d2");
        var candidateId = Guid.Parse("30000000-0000-0000-0000-0000000000d3");
        var migrationClaimId = Guid.Parse("30000000-0000-0000-0000-0000000000d4");
        var rollbackClaimId = Guid.Parse("30000000-0000-0000-0000-0000000000d5");
        var aggregateMemoryId = Guid.Parse("30000000-0000-0000-0000-0000000000d6");
        fixture.DbContext.AddRange(
            new CognitiveMemoryDreamRunRecord
            {
                Id = dreamRunId,
                ProjectId = projectId,
                Mode = CognitiveMemoryConsolidationMode.ProjectNightly,
                TriggerKind = CognitiveMemoryConsolidationTriggerKind.Nightly,
                Status = CognitiveMemoryRunStatus.Succeeded,
                IdempotencyKey = "dream-manual-claim-lineage",
                PolicyProfileId = "policy:test",
                AlgorithmVersion = "unit-test",
                StartedAtUtc = fixture.Clock.GetUtcNow(),
                CompletedAtUtc = fixture.Clock.GetUtcNow(),
                ConcurrencyToken = Guid.NewGuid()
            },
            new CognitiveMemoryQualityClusterRecord
            {
                Id = clusterId,
                ProjectId = projectId,
                ClusterHash = CognitiveMemoryHash.FromUtf8("manual-claim-lineage-cluster").Value,
                PrimaryKeyFamily = CognitiveMemoryQualityClusterKeyFamily.SemanticTopic,
                Readiness = CognitiveMemoryQualityClusterReadiness.AggregateReady,
                AccessLevel = CognitiveMemoryAccessLevel.Project,
                RiskLevel = CognitiveMemoryRiskLevel.Low,
                PolicyProfileId = "policy:test",
                AlgorithmVersion = "unit-test",
                MemberCount = 2,
                AggregateEligible = true,
                EligibilityReason = "unit-test",
                CreatedAtUtc = fixture.Clock.GetUtcNow(),
                UpdatedAtUtc = fixture.Clock.GetUtcNow(),
                ConcurrencyToken = Guid.NewGuid()
            },
            new CognitiveMemoryRecord
            {
                Id = aggregateMemoryId,
                ProjectId = projectId,
                Kind = CognitiveMemoryRecordKind.Semantic,
                Origin = CognitiveMemoryRecordOrigin.MachineGenerated,
                Title = "Release checklist aggregate",
                CanonicalText = "Release checklist includes database migration readiness and rollback owner assignment.",
                SummaryText = "Release checklist includes database migration readiness and rollback owner assignment.",
                TopicKey = "release.checklist.procedure",
                ValidationState = CognitiveMemoryValidationState.Approved,
                StabilityState = CognitiveMemoryStabilityState.Active,
                CreatedInMode = CognitiveMemoryOperationMode.Consolidate,
                AlgorithmVersion = "unit-test",
                ContentHash = CognitiveMemoryHash.FromUtf8("release-checklist-aggregate").Value,
                SourceEvidenceCount = 2,
                EvidenceAnchorCount = 2,
                AccessLevel = CognitiveMemoryAccessLevel.Project,
                RiskLevel = CognitiveMemoryRiskLevel.Low,
                CreatedAtUtc = fixture.Clock.GetUtcNow(),
                UpdatedAtUtc = fixture.Clock.GetUtcNow(),
                ConcurrencyToken = Guid.NewGuid()
            },
            new CognitiveMemoryDreamAggregateCandidateRecord
            {
                Id = candidateId,
                DreamRunId = dreamRunId,
                ClusterId = clusterId,
                ProjectId = projectId,
                Mode = CognitiveMemoryConsolidationMode.ProjectNightly,
                Status = CognitiveMemoryDreamAggregateCandidateStatus.Applied,
                Title = "Release checklist aggregate",
                SummaryText = "Release checklist includes database migration readiness and rollback owner assignment.",
                CanonicalText = "Release checklist includes database migration readiness and rollback owner assignment.",
                AccessLevel = CognitiveMemoryAccessLevel.Project,
                RiskLevel = CognitiveMemoryRiskLevel.Low,
                AlgorithmVersion = "unit-test",
                PayloadHash = CognitiveMemoryHash.FromUtf8("release-checklist-aggregate-payload").Value,
                MemoryRecordId = aggregateMemoryId,
                ClaimCount = 2,
                SourceMapCount = 2,
                CreatedAtUtc = fixture.Clock.GetUtcNow(),
                UpdatedAtUtc = fixture.Clock.GetUtcNow(),
                ConcurrencyToken = Guid.NewGuid()
            },
            new CognitiveMemoryDreamAggregateClaimRecord
            {
                Id = migrationClaimId,
                AggregateCandidateId = candidateId,
                ProjectId = projectId,
                Sequence = 0,
                ClaimKind = CognitiveMemoryClaimKind.Fact,
                ClaimText = "Release checklist verifies database migration readiness.",
                SubjectKey = "release.checklist",
                PredicateKey = "verifies",
                ObjectKey = "database.migration.readiness",
                CreatedAtUtc = fixture.Clock.GetUtcNow()
            },
            new CognitiveMemoryDreamAggregateClaimRecord
            {
                Id = rollbackClaimId,
                AggregateCandidateId = candidateId,
                ProjectId = projectId,
                Sequence = 1,
                ClaimKind = CognitiveMemoryClaimKind.Fact,
                ClaimText = "Release checklist verifies rollback owner assignment.",
                SubjectKey = "release.checklist",
                PredicateKey = "verifies",
                ObjectKey = "rollback.owner.assignment",
                CreatedAtUtc = fixture.Clock.GetUtcNow()
            },
            new CognitiveMemoryDreamAggregateClaimSourceMapRecord
            {
                AggregateCandidateId = candidateId,
                AggregateClaimId = migrationClaimId,
                ProjectId = projectId,
                SourceMemoryRecordId = migration.RecordId,
                SourceItemId = migration.SourceItemId,
                EvidenceAnchorId = migration.EvidenceAnchorId,
                Direction = CognitiveMemoryEvidenceDirection.Supports,
                AccessLevel = CognitiveMemoryAccessLevel.Project,
                RedactionState = CognitiveMemoryRedactionState.Safe,
                Summary = "Release checklist migration source evidence.",
                CreatedAtUtc = fixture.Clock.GetUtcNow()
            },
            new CognitiveMemoryDreamAggregateClaimSourceMapRecord
            {
                AggregateCandidateId = candidateId,
                AggregateClaimId = rollbackClaimId,
                ProjectId = projectId,
                SourceMemoryRecordId = rollback.RecordId,
                SourceItemId = rollback.SourceItemId,
                EvidenceAnchorId = rollback.EvidenceAnchorId,
                Direction = CognitiveMemoryEvidenceDirection.Supports,
                AccessLevel = CognitiveMemoryAccessLevel.Project,
                RedactionState = CognitiveMemoryRedactionState.Safe,
                Summary = "Release checklist rollback source evidence.",
                CreatedAtUtc = fixture.Clock.GetUtcNow()
            });
        await fixture.DbContext.SaveChangesAsync();
        var traceId = await SeedRecallTraceAsync(fixture, projectId, "manual aggregate claim lineage");
        var aggregateRef = new CognitiveMemoryRecallSourceRef(
            new CognitiveMemoryRecordId(aggregateMemoryId),
            null,
            null,
            "aggregate-memory",
            $"memory:{aggregateMemoryId:D}",
            "Release checklist verifies database migration readiness.",
            CognitiveMemoryAccessLevel.Project,
            CognitiveMemoryRedactionState.Safe,
            IncludedInContext: true,
            CognitiveMemoryRecallExclusionReasonKind.None);
        var recallResult = new CognitiveMemoryRecallResult(
            traceId,
            new CognitiveMemoryRecallContextPack(
                CognitiveMemoryRecallContextPackId.New(),
                projectId,
                null,
                "Which release checklist item covers migration readiness?",
                "Selected one aggregate claim.",
                [
                    new CognitiveMemoryRecallContextSection(
                        new CognitiveMemorySectionId("selected-manual-aggregate-claim"),
                        CognitiveMemoryRecallContextSectionKind.SelectedMemory,
                        "Release checklist aggregate",
                        "Release checklist verifies database migration readiness.",
                        [new CognitiveMemoryRecordId(aggregateMemoryId)],
                        [new CognitiveMemoryClaimId(migrationClaimId)],
                        [aggregateRef])
                ],
                [aggregateRef],
                [],
                new Dictionary<string, string>()),
            [],
            [],
            []);
        var synthesis = new CognitiveMemoryRecallSynthesisService(fixture.Factory, fixture.Clock);
        var synthesisResult = await synthesis.SynthesizeAsync(new CognitiveMemoryRecallSynthesisRequest(recallResult, Policy(projectId)));
        var statement = Assert.Single(synthesisResult.Statements);
        var persistedMap = Assert.Single(await fixture.DbContext.Set<CognitiveMemorySynthesizedStatementSourceMapRecord>()
            .Where(sourceMap => sourceMap.StatementId == statement.StatementId.Value)
            .ToListAsync());
        Assert.Equal(migrationClaimId, persistedMap.AggregateClaimId);
        var resolver = new CognitiveMemoryReferenceResolver(fixture.Factory);

        var references = await resolver.ResolveAsync(new CognitiveMemoryReferenceResolverRequest(statement.StatementId, Policy(projectId)));

        Assert.Contains(references.References, reference => reference.MemoryRecordId.Value == migration.RecordId);
        Assert.DoesNotContain(references.References, reference => reference.MemoryRecordId.Value == rollback.RecordId);
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
        Assert.Equal(CognitiveMemoryRecallStatementPlanKind.Action, statement.PlanKind);
        Assert.Contains("Use rollback runbook", statement.Text, StringComparison.Ordinal);
        Assert.Contains("Notify release owner", statement.Text, StringComparison.Ordinal);
        Assert.Equal(2, statement.SourceRefs.Count);
        Assert.DoesNotContain($"/unit/{first.RecordId:D}", result.Brief, StringComparison.Ordinal);
        Assert.DoesNotContain($"/unit/{second.RecordId:D}", result.Brief, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecallSynthesis_BuildsQueryShapedBriefInsteadOfTitleGroupedConcatenation()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var first = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-0000000000c1"),
            "Deployment rollback",
            "Use rollback runbook when health checks fail.",
            "Use rollback runbook when health checks fail.");
        var second = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-0000000000c2"),
            "Deployment rollback",
            "Notify release owner after rollback starts.",
            "Notify release owner after rollback starts.");
        var traceId = await SeedRecallTraceAsync(fixture, projectId, "what should happen during production rollback");
        var firstRef = CreateSourceRef(first, "Use rollback runbook when health checks fail.");
        var secondRef = CreateSourceRef(second, "Notify release owner after rollback starts.");
        var recallResult = new CognitiveMemoryRecallResult(
            traceId,
            new CognitiveMemoryRecallContextPack(
                CognitiveMemoryRecallContextPackId.New(),
                projectId,
                null,
                "What should happen during production rollback?",
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

        Assert.StartsWith("Production rollback", result.Brief, StringComparison.OrdinalIgnoreCase);
        Assert.All(result.Statements, statement => Assert.Equal(CognitiveMemoryRecallStatementPlanKind.Action, statement.PlanKind));
        Assert.DoesNotContain(
            "Use rollback runbook when health checks fail. Notify release owner after rollback starts.",
            result.Brief,
            StringComparison.Ordinal);
        Assert.All(result.Statements, statement => Assert.DoesNotContain("Deployment rollback:", statement.Text, StringComparison.Ordinal));
        Assert.False(result.ReferencesShownByDefault);
    }

    [Fact]
    public async Task RecallSynthesis_SeparatesConflictingClaimsIntoCaveatStatements()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var approvalRequired = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000098"),
            "Rollback approval required",
            "Production rollback requires signed release-owner approval before traffic restoration.",
            "Rollback approval required source evidence.");
        var approvalSkipped = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000099"),
            "Rollback approval skipped",
            "Production rollback may restore traffic without release-owner approval during incidents.",
            "Rollback approval skipped source evidence.");
        var traceId = await SeedRecallTraceAsync(fixture, projectId, "how should production rollback approval be handled");
        var requiredRef = CreateSourceRef(approvalRequired, "Rollback approval required source evidence.");
        var skippedRef = CreateSourceRef(approvalSkipped, "Rollback approval skipped source evidence.");
        var recallResult = new CognitiveMemoryRecallResult(
            traceId,
            new CognitiveMemoryRecallContextPack(
                CognitiveMemoryRecallContextPackId.New(),
                projectId,
                null,
                "How should production rollback approval be handled?",
                "Selected conflicting rollback memories.",
                [
                    new CognitiveMemoryRecallContextSection(
                        new CognitiveMemorySectionId("selected-required"),
                        CognitiveMemoryRecallContextSectionKind.SelectedMemory,
                        "Rollback approval required",
                        "Production rollback requires signed release-owner approval before traffic restoration.",
                        [new CognitiveMemoryRecordId(approvalRequired.RecordId)],
                        [],
                        [requiredRef]),
                    new CognitiveMemoryRecallContextSection(
                        new CognitiveMemorySectionId("selected-skipped"),
                        CognitiveMemoryRecallContextSectionKind.SelectedMemory,
                        "Rollback approval skipped",
                        "Production rollback may restore traffic without release-owner approval during incidents.",
                        [new CognitiveMemoryRecordId(approvalSkipped.RecordId)],
                        [],
                        [skippedRef])
                ],
                [requiredRef, skippedRef],
                [],
                new Dictionary<string, string>()),
            [],
            [],
            []);
        var synthesis = new CognitiveMemoryRecallSynthesisService(fixture.Factory, fixture.Clock);

        var result = await synthesis.SynthesizeAsync(new CognitiveMemoryRecallSynthesisRequest(recallResult, Policy(projectId)));
        var recallText = string.Join(
            Environment.NewLine,
            result.Warnings.Concat([result.Brief]).Concat(result.Statements.Select(statement => statement.Text)));

        Assert.Contains("conflict", recallText, StringComparison.OrdinalIgnoreCase);
        Assert.All(result.Statements, statement => Assert.Equal(CognitiveMemoryRecallStatementPlanKind.Conflict, statement.PlanKind));
        Assert.DoesNotContain(
            "requires signed release-owner approval before traffic restoration; Production rollback may restore traffic without release-owner approval during incidents",
            result.Brief,
            StringComparison.Ordinal);
        Assert.False(result.ReferencesShownByDefault);
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

    [Fact]
    public void RecallBriefComposer_ProducesTypedTaskFacingPlanKindsAndHidesDiagnostics()
    {
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var answerRef = CreateSourceRef(
            new SeededMemory(
                Guid.Parse("31000000-0000-0000-0000-000000000051"),
                Guid.Parse("31000000-0000-0000-0000-000000000052"),
                Guid.Parse("31000000-0000-0000-0000-000000000053")),
            "Deployment owner source.");
        var actionRef = CreateSourceRef(
            new SeededMemory(
                Guid.Parse("31000000-0000-0000-0000-000000000054"),
                Guid.Parse("31000000-0000-0000-0000-000000000055"),
                Guid.Parse("31000000-0000-0000-0000-000000000056")),
            "Smoke test source.");
        var caveatRef = CreateSourceRef(
            new SeededMemory(
                Guid.Parse("31000000-0000-0000-0000-000000000057"),
                Guid.Parse("31000000-0000-0000-0000-000000000058"),
                Guid.Parse("31000000-0000-0000-0000-000000000059")),
            "Stale rollout source.");
        var composer = new CognitiveMemoryRecallBriefComposer();

        var result = composer.Compose(new CognitiveMemoryRecallBriefComposerRequest(
            "Show deployment rollout references and debug provenance.",
            [
                new CognitiveMemoryRecallContextSection(
                    new CognitiveMemorySectionId("answer"),
                    CognitiveMemoryRecallContextSectionKind.SelectedMemory,
                    "Deployment owner",
                    "Deployment owner is Release Engineering.\nInternal score: 0.99\nSource: /internal/deployment-owner",
                    [answerRef.MemoryRecordId],
                    [],
                    [answerRef]),
                new CognitiveMemoryRecallContextSection(
                    new CognitiveMemorySectionId("action"),
                    CognitiveMemoryRecallContextSectionKind.SelectedMemory,
                    "Deployment smoke test",
                    "Run smoke tests before production promotion.",
                    [actionRef.MemoryRecordId],
                    [],
                    [actionRef]),
                new CognitiveMemoryRecallContextSection(
                    new CognitiveMemorySectionId("caveat"),
                    CognitiveMemoryRecallContextSectionKind.SelectedMemory,
                    "Legacy rollout note",
                    "Legacy rollout note is stale and superseded by the current runbook.",
                    [caveatRef.MemoryRecordId],
                    [],
                    [caveatRef]),
                new CognitiveMemoryRecallContextSection(
                    new CognitiveMemorySectionId("missing"),
                    CognitiveMemoryRecallContextSectionKind.SelectedMemory,
                    "Audit exception",
                    "Audit exception owner is not backed by a source map.",
                    [new CognitiveMemoryRecordId(Guid.Parse("31000000-0000-0000-0000-00000000005a"))],
                    [],
                    [])
            ],
            new HashSet<Guid>(),
            Policy(projectId),
            MaxStatements: 6));

        var planKinds = result.Statements.Select(statement => statement.PlanKind).ToHashSet();
        Assert.Contains(CognitiveMemoryRecallStatementPlanKind.Answer, planKinds);
        Assert.Contains(CognitiveMemoryRecallStatementPlanKind.Action, planKinds);
        Assert.Contains(CognitiveMemoryRecallStatementPlanKind.Caveat, planKinds);
        Assert.Contains(CognitiveMemoryRecallStatementPlanKind.MissingEvidence, planKinds);
        Assert.Contains(CognitiveMemoryRecallStatementPlanKind.ReferenceHint, planKinds);
        Assert.DoesNotContain("Internal score", result.Brief, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/internal/deployment-owner", result.Brief, StringComparison.Ordinal);
        Assert.Contains("reference resolution", result.Brief, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecallBriefComposer_WarnsWhenBudgetOmitsImportantCaveats()
    {
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var staleRef = CreateSourceRef(
            new SeededMemory(
                Guid.Parse("31000000-0000-0000-0000-000000000061"),
                Guid.Parse("31000000-0000-0000-0000-000000000062"),
                Guid.Parse("31000000-0000-0000-0000-000000000063")),
            "Stale procedure source.");
        var restrictedRef = CreateSourceRef(
            new SeededMemory(
                Guid.Parse("31000000-0000-0000-0000-000000000064"),
                Guid.Parse("31000000-0000-0000-0000-000000000065"),
                Guid.Parse("31000000-0000-0000-0000-000000000066")),
            "Restricted procedure source.",
            redactionState: CognitiveMemoryRedactionState.Restricted);
        var staleClaimId = new CognitiveMemoryClaimId(Guid.Parse("31000000-0000-0000-0000-000000000067"));
        var restrictedClaimId = new CognitiveMemoryClaimId(Guid.Parse("31000000-0000-0000-0000-000000000068"));
        var composer = new CognitiveMemoryRecallBriefComposer();

        var result = composer.Compose(new CognitiveMemoryRecallBriefComposerRequest(
            "What does the release note say?",
            [
                new CognitiveMemoryRecallContextSection(
                    new CognitiveMemorySectionId("stale"),
                    CognitiveMemoryRecallContextSectionKind.SelectedMemory,
                    "Stale release note",
                    "Release note is stale after the current incident runbook update.",
                    [staleRef.MemoryRecordId],
                    [staleClaimId],
                    [staleRef]),
                new CognitiveMemoryRecallContextSection(
                    new CognitiveMemorySectionId("restricted"),
                    CognitiveMemoryRecallContextSectionKind.SelectedMemory,
                    "Restricted release note",
                    "Release note contains restricted operator context.",
                    [restrictedRef.MemoryRecordId],
                    [restrictedClaimId],
                    [restrictedRef])
            ],
            new HashSet<Guid> { staleClaimId.Value, restrictedClaimId.Value },
            Policy(projectId),
            MaxStatements: 1));

        Assert.Single(result.Statements);
        Assert.Equal(CognitiveMemoryRecallStatementPlanKind.Caveat, result.Statements[0].PlanKind);
        Assert.Contains(result.Warnings, warning => warning.Contains("Omitted important caveat", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SemanticInvariant_CrossProjectWeeklyFormsOnlyPolicyAllowedCrossProjectClusters()
    {
        await using var fixture = await CreateFixtureAsync();
        var sourceProjectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var peerProjectId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var restrictedProjectId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var first = await SeedLinkedMemoryAsync(
            fixture,
            sourceProjectId,
            Guid.Parse("31000000-0000-0000-0000-000000000001"),
            "Rollback approval gate",
            "Production rollback requires release-owner approval before traffic restoration.",
            "Project A rollback approval source.",
            topicKey: "rollback.approval.cross-project");
        var second = await SeedLinkedMemoryAsync(
            fixture,
            peerProjectId,
            Guid.Parse("31000000-0000-0000-0000-000000000002"),
            "Rollback traffic gate",
            "Traffic restoration after rollback waits for release-owner approval.",
            "Project B rollback approval source.",
            topicKey: "rollback.approval.cross-project");
        var restricted = await SeedLinkedMemoryAsync(
            fixture,
            restrictedProjectId,
            Guid.Parse("31000000-0000-0000-0000-000000000003"),
            "Restricted rollback secret",
            "Restricted rollback plan stores SECRET_TOKEN=do-not-leak.",
            "SECRET_TOKEN=do-not-leak",
            topicKey: "rollback.approval.cross-project",
            sourceAccessLevel: CognitiveMemoryAccessLevel.Restricted,
            sourceRedactionState: CognitiveMemoryRedactionState.Restricted,
            recordAccessLevel: CognitiveMemoryAccessLevel.Restricted);
        var dream = CreateDreamService(fixture);

        var result = await dream.RunAsync(new CognitiveMemoryDreamRunRequest(
            null,
            CognitiveMemoryConsolidationMode.CrossProjectWeekly,
            CognitiveMemoryConsolidationTriggerKind.Manual,
            GlobalPolicy(),
            new CognitiveMemoryIdempotencyKey("semantic-cross-project-weekly")));

        var crossProjectCandidate = Assert.Single(result.AggregateCandidates, candidate =>
            CandidateContainsMemory(candidate, first.RecordId) &&
            CandidateContainsMemory(candidate, second.RecordId));
        Assert.DoesNotContain(crossProjectCandidate.Claims.SelectMany(claim => claim.SourceMaps), sourceMap =>
            sourceMap.SourceMemoryRecordId.Value == restricted.RecordId ||
            sourceMap.Summary.Contains("SECRET_TOKEN", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SemanticInvariant_ApproximateCandidateDiscoveryPairsParaphrasesWithoutExactSharedKeys()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var certificate = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("31000000-0000-0000-0000-000000000011"),
            "AeroGate certificate blocker",
            "The gateway cannot ship until the CE certificate is filed.",
            "The gateway cannot ship until the CE certificate is filed.",
            topicKey: "aerogate.certificate.blocker");
        var paperwork = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("31000000-0000-0000-0000-000000000012"),
            "European conformity paperwork",
            "Release is blocked until European conformity paperwork is archived.",
            "Release is blocked until European conformity paperwork is archived.",
            topicKey: "compliance.paperwork.release-hold");
        var planner = new CognitiveMemoryClusterPlanner(fixture.Factory, fixture.Clock);

        var result = await planner.PlanAsync(new CognitiveMemoryClusterPlanningRequest(projectId, Policy(projectId)));

        Assert.Contains(result.Clusters, cluster =>
            ContainsMemory(cluster, certificate.RecordId) &&
            ContainsMemory(cluster, paperwork.RecordId));
        Assert.InRange(result.Metrics.CandidatePairsEvaluated, 1, 20);
    }

    [Fact]
    public async Task SemanticInvariant_CrossProjectPlanningReportsPolicyBlockedPairsWithoutRestrictedMembers()
    {
        await using var fixture = await CreateFixtureAsync();
        var sourceProjectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var peerProjectId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var restrictedProjectId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var first = await SeedLinkedMemoryAsync(
            fixture,
            sourceProjectId,
            Guid.Parse("31000000-0000-0000-0000-000000000031"),
            "Rollback approval metric gate",
            "Production rollback requires release-owner approval before traffic restoration.",
            "Project A rollback metric source.",
            topicKey: "rollback.policy.metric.cross-project");
        var second = await SeedLinkedMemoryAsync(
            fixture,
            peerProjectId,
            Guid.Parse("31000000-0000-0000-0000-000000000032"),
            "Rollback approval peer metric",
            "Traffic restoration after rollback waits for release-owner approval.",
            "Project B rollback metric source.",
            topicKey: "rollback.policy.metric.cross-project");
        var restricted = await SeedLinkedMemoryAsync(
            fixture,
            restrictedProjectId,
            Guid.Parse("31000000-0000-0000-0000-000000000033"),
            "Restricted rollback metric secret",
            "Restricted rollback plan stores SECRET_TOKEN=do-not-leak.",
            "SECRET_TOKEN=do-not-leak",
            topicKey: "rollback.policy.metric.cross-project",
            sourceAccessLevel: CognitiveMemoryAccessLevel.Restricted,
            sourceRedactionState: CognitiveMemoryRedactionState.Restricted,
            recordAccessLevel: CognitiveMemoryAccessLevel.Restricted);
        var planner = new CognitiveMemoryClusterPlanner(fixture.Factory, fixture.Clock);

        var result = await planner.PlanAsync(new CognitiveMemoryClusterPlanningRequest(
            null,
            GlobalPolicy(),
            persistClusters: false,
            scope: CognitiveMemoryClusterPlanningScope.PolicyConstrainedCrossProject));

        Assert.True(result.Metrics.PolicyBlockedCandidatePairs > 0);
        Assert.Contains(result.Clusters, cluster =>
            ContainsMemory(cluster, first.RecordId) &&
            ContainsMemory(cluster, second.RecordId));
        Assert.DoesNotContain(result.Clusters.SelectMany(cluster => cluster.Members), member =>
            member.MemoryRecordId?.Value == restricted.RecordId);
    }

    [Fact]
    public async Task SemanticInvariant_ClusterKeysExcludeSignalsBelowCoverageThreshold()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var first = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("31000000-0000-0000-0000-000000000021"),
            "Release checklist cipher verification",
            "Release checklist requires cipher verification before promotion.",
            "Release checklist requires cipher verification before promotion.",
            topicKey: "release.checklist.coverage");
        var second = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("31000000-0000-0000-0000-000000000022"),
            "Release checklist cipher owner",
            "Release checklist records cipher rollback owner before rollout.",
            "Release checklist records cipher rollback owner before rollout.",
            topicKey: "release.checklist.coverage");
        var third = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("31000000-0000-0000-0000-000000000023"),
            "Release checklist migration rehearsal",
            "Release checklist validates database migration rehearsal.",
            "Release checklist validates database migration rehearsal.",
            topicKey: "release.checklist.coverage");
        var fourth = await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("31000000-0000-0000-0000-000000000024"),
            "Release checklist support handoff",
            "Release checklist verifies support handoff schedule.",
            "Release checklist verifies support handoff schedule.",
            topicKey: "release.checklist.coverage");
        var planner = new CognitiveMemoryClusterPlanner(fixture.Factory, fixture.Clock);

        var result = await planner.PlanAsync(new CognitiveMemoryClusterPlanningRequest(projectId, Policy(projectId)));

        var cluster = Assert.Single(result.Clusters, cluster =>
            ContainsMemory(cluster, first.RecordId) &&
            ContainsMemory(cluster, second.RecordId) &&
            ContainsMemory(cluster, third.RecordId) &&
            ContainsMemory(cluster, fourth.RecordId));
        var topicKey = Assert.Single(cluster.Keys, key => key.Family == CognitiveMemoryQualityClusterKeyFamily.SemanticTopic);
        Assert.Equal(4, topicKey.SupportCount);
        Assert.Equal(1, topicKey.CoverageRatio, precision: 3);
        Assert.All(cluster.Keys, key => Assert.True(key.CoverageRatio > 0.5));
        Assert.DoesNotContain(cluster.Keys, key => string.Equals(key.DisplayText, "cipher", StringComparison.OrdinalIgnoreCase));
        Assert.True(cluster.QualityMetrics.PrimaryKeyCoverageRatio > 0.5);
        Assert.True(cluster.QualityMetrics.LowCoverageKeyCount > 0);
        Assert.Contains("coverage", cluster.QualityMetrics.EligibilityReason, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(cluster.Warnings, warning => warning.Contains("pair-local", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SemanticInvariant_DreamRunSeparatesUnrelatedClaimsSharingPrimaryClusterKey()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("31000000-0000-0000-0000-000000000031"),
            "Operations policy data export",
            "Tenant data export logs DSR approvals before file delivery.",
            "Tenant data export logs DSR approvals before file delivery.",
            topicKey: "project.operations.policy");
        await SeedLinkedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("31000000-0000-0000-0000-000000000032"),
            "Operations policy payment export",
            "Payment batch export requires checksum verification after bank upload.",
            "Payment batch export requires checksum verification after bank upload.",
            topicKey: "project.operations.policy");
        var dream = CreateDreamService(fixture);

        var result = await dream.RunAsync(new CognitiveMemoryDreamRunRequest(
            projectId,
            CognitiveMemoryConsolidationMode.ProjectNightly,
            CognitiveMemoryConsolidationTriggerKind.Nightly,
            Policy(projectId),
            new CognitiveMemoryIdempotencyKey("semantic-unrelated-claim-separation")));

        var candidate = Assert.Single(result.AggregateCandidates, candidate =>
            candidate.Title.Contains("project.operations.policy", StringComparison.OrdinalIgnoreCase));
        Assert.True(candidate.Claims.Count >= 2);
        Assert.DoesNotContain(candidate.Claims, claim =>
            claim.ClaimText.Contains("Tenant data export", StringComparison.OrdinalIgnoreCase) &&
            claim.ClaimText.Contains("Payment batch export", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void SemanticInvariant_DreamClaimSynthesisProducesStructuredSlots()
    {
        var text = CognitiveMemoryDreamClaimSynthesizer.Instance.Synthesize(new CognitiveMemoryDreamClaimSynthesisRequest(
            CognitiveMemoryConsolidationMode.ProcedureMining,
            [
                "Release approval applies only after smoke tests pass.",
                "If smoke tests fail, the rollback owner must be assigned before traffic restoration."
            ]));

        Assert.Contains("Claim:", text, StringComparison.Ordinal);
        Assert.Contains("Evidence:", text, StringComparison.Ordinal);
        Assert.Contains("Condition:", text, StringComparison.Ordinal);
        Assert.Contains("Caveat:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Conclusion:", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Release window is 30 minutes.", "Release window is 15 minutes and must not be extended.")]
    [InlineData("Run database migration after traffic restoration.", "Run database migration before traffic restoration.")]
    [InlineData("Operators approve finance exceptions.", "Finance controllers approve operator exceptions.")]
    [InlineData("Deploy when smoke tests fail.", "Deploy only when smoke tests pass.")]
    [InlineData("Security review is optional before launch.", "Security review is required before launch.")]
    [InlineData("Local Docker simulation rules apply to production rollout.", "Local Docker simulation rules apply only to test validation.")]
    public void SemanticInvariant_DreamEntailmentRejectsNumericTemporalActorConditionalAndScopeReversals(
        string claim,
        string source)
    {
        var result = CognitiveMemoryDreamEntailmentValidator.Instance.Validate(new CognitiveMemoryDreamEntailmentRequest(
            claim,
            [source]));

        Assert.False(result.Supported, result.Reason);
    }

    [Theory]
    [InlineData("Release window is 15 minutes.", "Release window is 15 minutes and must not be extended.")]
    [InlineData("Run database migration before traffic restoration.", "Run database migration before traffic restoration.")]
    [InlineData("Finance controllers approve operator exceptions.", "Finance controllers approve operator exceptions.")]
    [InlineData("Deploy only when smoke tests pass.", "Deploy only when smoke tests pass.")]
    [InlineData("Security review is required before launch.", "Security review is required before launch.")]
    [InlineData("Local Docker simulation rules apply only to test validation.", "Local Docker simulation rules apply only to test validation.")]
    public void DreamEntailment_SupportsMatchingSemanticOperators(
        string claim,
        string source)
    {
        var result = CognitiveMemoryDreamEntailmentValidator.Instance.Validate(new CognitiveMemoryDreamEntailmentRequest(
            claim,
            [source]));

        Assert.True(result.Supported, result.Reason);
    }

    [Fact]
    public void SemanticInvariant_RecallBriefKeepsAggregateClaimLineageAtStatementLineLevel()
    {
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var approvalClaimId = new CognitiveMemoryClaimId(Guid.Parse("31000000-0000-0000-0000-000000000041"));
        var ownerClaimId = new CognitiveMemoryClaimId(Guid.Parse("31000000-0000-0000-0000-000000000042"));
        var approvalRef = CreateSourceRef(
            new SeededMemory(
                Guid.Parse("31000000-0000-0000-0000-000000000043"),
                Guid.Parse("31000000-0000-0000-0000-000000000044"),
                Guid.Parse("31000000-0000-0000-0000-000000000045")),
            "Release approval source.");
        var ownerRef = CreateSourceRef(
            new SeededMemory(
                Guid.Parse("31000000-0000-0000-0000-000000000046"),
                Guid.Parse("31000000-0000-0000-0000-000000000047"),
                Guid.Parse("31000000-0000-0000-0000-000000000048")),
            "Rollback owner source.");
        var sections = new[]
        {
            new CognitiveMemoryRecallContextSection(
                new CognitiveMemorySectionId("approval"),
                CognitiveMemoryRecallContextSectionKind.SelectedMemory,
                "Release approval",
                "Release approval requires signed owner approval before launch.",
                [approvalRef.MemoryRecordId],
                [approvalClaimId],
                [approvalRef]),
            new CognitiveMemoryRecallContextSection(
                new CognitiveMemorySectionId("owner"),
                CognitiveMemoryRecallContextSectionKind.SelectedMemory,
                "Rollback owner",
                "Rollback owner assignment must be confirmed before traffic restoration.",
                [ownerRef.MemoryRecordId],
                [ownerClaimId],
                [ownerRef])
        };
        var composer = new CognitiveMemoryRecallBriefComposer();

        var result = composer.Compose(new CognitiveMemoryRecallBriefComposerRequest(
            "How should release approval and rollback owner be handled?",
            sections,
            new HashSet<Guid> { approvalClaimId.Value, ownerClaimId.Value },
            Policy(projectId),
            MaxStatements: 5));

        Assert.Equal(2, result.Statements.Count);
        Assert.All(result.Statements, statement =>
        {
            Assert.Equal(CognitiveMemoryRecallStatementPlanKind.Action, statement.PlanKind);
            Assert.Single(statement.AggregateClaimIds);
            Assert.Single(statement.SourceRefs);
        });
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

    private static bool ContainsMemory(CognitiveMemoryClusterPlan cluster, Guid recordId)
        => cluster.Members.Any(member => member.MemoryRecordId?.Value == recordId);

    private static bool CandidateContainsMemory(CognitiveMemoryDreamAggregateCandidate candidate, Guid recordId)
        => candidate.Claims
            .SelectMany(claim => claim.SourceMaps)
            .Any(sourceMap => sourceMap.SourceMemoryRecordId.Value == recordId);

    private static async Task<SeededAggregate> SeedAppliedAggregateAsync(
        QualityFixture fixture,
        Guid projectId,
        string title,
        IReadOnlyList<AggregateClaimSeed> claims,
        bool applied = true)
    {
        if (claims.Count == 0)
        {
            throw new ArgumentException("At least one aggregate claim seed is required.", nameof(claims));
        }

        var sourceMemoryRecordIds = claims.Select(claim => claim.SourceMemoryRecordId).Distinct().ToArray();
        var sourceLinks = await fixture.DbContext.Set<CognitiveMemorySourceLinkRecord>()
            .Where(link => sourceMemoryRecordIds.Contains(link.MemoryRecordId))
            .ToDictionaryAsync(link => link.MemoryRecordId);
        var evidenceLinks = await fixture.DbContext.Set<CognitiveMemoryRecordEvidenceAnchorRecord>()
            .Where(link => sourceMemoryRecordIds.Contains(link.MemoryRecordId))
            .ToDictionaryAsync(link => link.MemoryRecordId);
        var dreamRunId = Guid.NewGuid();
        var clusterId = Guid.NewGuid();
        var candidateId = Guid.NewGuid();
        var aggregateMemoryId = applied ? Guid.NewGuid() : (Guid?)null;
        var canonicalText = string.Join(" ", claims.Select(claim => claim.ClaimText));
        var records = new List<object>
        {
            new CognitiveMemoryDreamRunRecord
            {
                Id = dreamRunId,
                ProjectId = projectId,
                Mode = CognitiveMemoryConsolidationMode.ProjectNightly,
                TriggerKind = CognitiveMemoryConsolidationTriggerKind.Nightly,
                Status = CognitiveMemoryRunStatus.Succeeded,
                IdempotencyKey = $"manual-applied-aggregate-{candidateId:D}",
                PolicyProfileId = "policy:test",
                AlgorithmVersion = "unit-test",
                StartedAtUtc = fixture.Clock.GetUtcNow(),
                CompletedAtUtc = fixture.Clock.GetUtcNow(),
                ConcurrencyToken = Guid.NewGuid()
            },
            new CognitiveMemoryQualityClusterRecord
            {
                Id = clusterId,
                ProjectId = projectId,
                ClusterHash = CognitiveMemoryHash.FromUtf8($"manual-applied-cluster-{clusterId:D}").Value,
                PrimaryKeyFamily = CognitiveMemoryQualityClusterKeyFamily.SemanticTopic,
                Readiness = CognitiveMemoryQualityClusterReadiness.AggregateReady,
                AccessLevel = CognitiveMemoryAccessLevel.Project,
                RiskLevel = CognitiveMemoryRiskLevel.Low,
                PolicyProfileId = "policy:test",
                AlgorithmVersion = "unit-test",
                MemberCount = sourceMemoryRecordIds.Length,
                SourceEvidenceCount = sourceMemoryRecordIds.Length,
                SourceIndependenceScore = sourceMemoryRecordIds.Length,
                AggregateEligible = true,
                EligibilityReason = "unit-test",
                CreatedAtUtc = fixture.Clock.GetUtcNow(),
                UpdatedAtUtc = fixture.Clock.GetUtcNow(),
                ConcurrencyToken = Guid.NewGuid()
            },
            new CognitiveMemoryDreamAggregateCandidateRecord
            {
                Id = candidateId,
                DreamRunId = dreamRunId,
                ClusterId = clusterId,
                ProjectId = projectId,
                Mode = CognitiveMemoryConsolidationMode.ProjectNightly,
                Status = applied
                    ? CognitiveMemoryDreamAggregateCandidateStatus.Applied
                    : CognitiveMemoryDreamAggregateCandidateStatus.Approved,
                Title = title,
                SummaryText = canonicalText,
                CanonicalText = canonicalText,
                AccessLevel = CognitiveMemoryAccessLevel.Project,
                RiskLevel = CognitiveMemoryRiskLevel.Low,
                AlgorithmVersion = "unit-test",
                PayloadHash = CognitiveMemoryHash.FromUtf8($"manual-applied-candidate-{candidateId:D}").Value,
                MemoryRecordId = aggregateMemoryId,
                ClaimCount = claims.Count,
                SourceMapCount = claims.Count,
                CreatedAtUtc = fixture.Clock.GetUtcNow(),
                UpdatedAtUtc = fixture.Clock.GetUtcNow(),
                ConcurrencyToken = Guid.NewGuid()
            }
        };
        if (aggregateMemoryId is { } appliedMemoryId)
        {
            records.Add(new CognitiveMemoryRecord
            {
                Id = appliedMemoryId,
                ProjectId = projectId,
                Kind = CognitiveMemoryRecordKind.Semantic,
                Origin = CognitiveMemoryRecordOrigin.MachineGenerated,
                Title = title,
                CanonicalText = canonicalText,
                SummaryText = canonicalText,
                TopicKey = "manual.applied.aggregate",
                ValidationState = CognitiveMemoryValidationState.Approved,
                StabilityState = CognitiveMemoryStabilityState.Active,
                CreatedInMode = CognitiveMemoryOperationMode.Consolidate,
                AlgorithmVersion = "unit-test",
                ContentHash = CognitiveMemoryHash.FromUtf8($"manual-applied-aggregate-{appliedMemoryId:D}").Value,
                SourceEvidenceCount = sourceMemoryRecordIds.Length,
                EvidenceAnchorCount = sourceMemoryRecordIds.Length,
                AccessLevel = CognitiveMemoryAccessLevel.Project,
                RiskLevel = CognitiveMemoryRiskLevel.Low,
                CreatedAtUtc = fixture.Clock.GetUtcNow(),
                UpdatedAtUtc = fixture.Clock.GetUtcNow(),
                ConcurrencyToken = Guid.NewGuid()
            });
        }
        else
        {
            records.Add(new CognitiveMemoryDreamValidationRecord
            {
                Id = Guid.NewGuid(),
                AggregateCandidateId = candidateId,
                ProjectId = projectId,
                Decision = CognitiveMemoryDreamValidationDecision.Approved,
                PolicyProfileId = "policy:test",
                IssueCount = 0,
                ClaimsChecked = claims.Count,
                SourceMapsChecked = claims.Count,
                IssuesJson = "[]",
                CreatedAtUtc = fixture.Clock.GetUtcNow(),
                ConcurrencyToken = Guid.NewGuid()
            });
        }

        fixture.DbContext.AddRange(records);

        var sequence = 0;
        foreach (var claim in claims)
        {
            if (!sourceLinks.TryGetValue(claim.SourceMemoryRecordId, out var sourceLink) ||
                !evidenceLinks.TryGetValue(claim.SourceMemoryRecordId, out var evidenceLink))
            {
                throw new InvalidOperationException($"Aggregate seed source memory '{claim.SourceMemoryRecordId:D}' is missing source or evidence linkage.");
            }

            fixture.DbContext.AddRange(
                new CognitiveMemoryDreamAggregateClaimRecord
                {
                    Id = claim.ClaimId,
                    AggregateCandidateId = candidateId,
                    ProjectId = projectId,
                    Sequence = sequence,
                    ClaimKind = CognitiveMemoryClaimKind.Fact,
                    ClaimText = claim.ClaimText,
                    SubjectKey = "release.checklist",
                    PredicateKey = "verifies",
                    ObjectKey = CognitiveMemoryHash.FromUtf8(claim.ClaimText).Value,
                    CreatedAtUtc = fixture.Clock.GetUtcNow()
                },
                new CognitiveMemoryDreamAggregateClaimSourceMapRecord
                {
                    AggregateCandidateId = candidateId,
                    AggregateClaimId = claim.ClaimId,
                    ProjectId = projectId,
                    SourceMemoryRecordId = claim.SourceMemoryRecordId,
                    SourceItemId = sourceLink.SourceItemId,
                    EvidenceAnchorId = evidenceLink.EvidenceAnchorId,
                    Direction = CognitiveMemoryEvidenceDirection.Supports,
                    AccessLevel = CognitiveMemoryAccessLevel.Project,
                    RedactionState = CognitiveMemoryRedactionState.Safe,
                    Summary = claim.SourceSummary,
                    CreatedAtUtc = fixture.Clock.GetUtcNow()
                });
            sequence++;
        }

        await fixture.DbContext.SaveChangesAsync();
        return new SeededAggregate(
            new CognitiveMemoryDreamAggregateCandidateId(candidateId),
            aggregateMemoryId is null ? null : new CognitiveMemoryRecordId(aggregateMemoryId.Value),
            claims
                .Select(claim => new SeededAggregateClaim(claim.ClaimId, claim.ClaimText, claim.SourceMemoryRecordId))
                .ToArray());
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

    private static CognitiveMemoryPolicyContext GlobalPolicy()
        => new(
            null,
            "agent:test",
            CognitiveMemoryAccessLevel.Project,
            new CognitiveMemoryPolicyProfileId("policy:test"),
            CognitiveMemoryRiskLevel.Low,
            AllowRestrictedContent: false);

    private static string ReadRepositoryFile(params string[] relativePathSegments)
    {
        var root = FindRepositoryRoot();
        var pathSegments = new[] { root }.Concat(relativePathSegments).ToArray();
        return File.ReadAllText(Path.Combine(pathSegments));
    }

    private static string ReadRepositoryFiles(params string[] relativePathSegments)
    {
        var root = FindRepositoryRoot();
        var pathSegments = new[] { root }.Concat(relativePathSegments).ToArray();
        var directory = Path.Combine(pathSegments);
        return string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(File.ReadAllText));
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "CanDoItAll.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root from the test working directory.");
    }

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
        CognitiveMemoryRedactionState sourceRedactionState = CognitiveMemoryRedactionState.Safe,
        CognitiveMemoryAccessLevel recordAccessLevel = CognitiveMemoryAccessLevel.Project)
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
            AccessLevel = recordAccessLevel,
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

    private sealed record AggregateClaimSeed(
        Guid ClaimId,
        string ClaimText,
        Guid SourceMemoryRecordId,
        string SourceSummary);

    private sealed record SeededAggregate(
        CognitiveMemoryDreamAggregateCandidateId CandidateId,
        CognitiveMemoryRecordId? AppliedMemoryRecordId,
        IReadOnlyList<SeededAggregateClaim> Claims)
    {
        public CognitiveMemoryRecordId MemoryRecordId => AppliedMemoryRecordId
            ?? throw new InvalidOperationException("The seeded aggregate has not been applied to a memory record.");
    }

    private sealed record SeededAggregateClaim(
        Guid ClaimId,
        string ClaimText,
        Guid SourceMemoryRecordId);

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
