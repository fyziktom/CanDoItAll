using CanDoItAll.Modules.CognitiveMemory;

namespace CanDoItAll.Tests.Unit;

public sealed class CognitiveMemoryQualityCollaboratorTests
{
    [Fact]
    public void QualityAlgorithmOptions_CurrentVersionNamesAllOwnedDomains()
    {
        var options = CognitiveMemoryQualityAlgorithmOptions.Current;

        Assert.Equal("quality-clustering-v3", options.Cluster.AlgorithmVersion.Value);
        Assert.Equal("quality-dream-v3-claim-synthesis", options.Dream.AlgorithmVersion.Value);
        Assert.Equal("quality-aggregate-apply-v3-semantic-calibrated", options.AggregateApply.AlgorithmVersion.Value);
        Assert.Equal(2, options.ProfessorLifecycle.RequiredRepeatedUseCount);
        Assert.Equal(4, options.ProfessorLifecycle.DescendantTraversalDepth);
        Assert.Equal(4, options.Recall.MaxFragmentsPerStatement);
    }

    [Fact]
    public void SemanticInvariant_DreamModeClusterSelectorKeepsModePolicyOutsideRunOrchestration()
    {
        var selector = new CognitiveMemoryDreamModeClusterSelector();
        var projectNightlyReviewCluster = CreateClusterPlan(
            CognitiveMemoryQualityClusterReadiness.NeedsHumanReview,
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var crossProjectSingleProjectCluster = CreateClusterPlan(
            CognitiveMemoryQualityClusterReadiness.AggregateReady,
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var crossProjectAggregateCluster = CreateClusterPlan(
            CognitiveMemoryQualityClusterReadiness.AggregateReady,
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

        Assert.True(selector.IsSelected(CognitiveMemoryConsolidationMode.ProjectNightly, projectNightlyReviewCluster));
        Assert.False(selector.IsSelected(CognitiveMemoryConsolidationMode.CrossProjectWeekly, crossProjectSingleProjectCluster));
        Assert.True(selector.IsSelected(CognitiveMemoryConsolidationMode.CrossProjectWeekly, crossProjectAggregateCluster));
        Assert.Equal("dream.cross-project-weekly.policy-constrained-cross-project", selector.ResolveSelectionReasonCode(CognitiveMemoryConsolidationMode.CrossProjectWeekly, crossProjectAggregateCluster));
        Assert.Equal(
            CognitiveMemoryClusterPlanningScope.PolicyConstrainedCrossProject,
            selector.ResolvePlanningScope(new CognitiveMemoryDreamRunRequest(
                null,
                CognitiveMemoryConsolidationMode.CrossProjectWeekly,
                CognitiveMemoryConsolidationTriggerKind.Nightly,
                Policy(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")),
                new CognitiveMemoryIdempotencyKey("selector-cross-project"))));
    }

    [Fact]
    public void ClusterTextSignals_NormalizesAliasesAndPluralForms()
    {
        Assert.Equal("release", CognitiveMemoryClusterTextSignals.NormalizeSignal("deployments"));
        Assert.Equal("artifact", CognitiveMemoryClusterTextSignals.NormalizeSignal("packages"));
        Assert.Equal("policy", CognitiveMemoryClusterTextSignals.NormalizeSignal("policies"));
    }

    [Fact]
    public void DreamClaimSynthesizer_ComposesComplementaryClaims()
    {
        var synthesized = CognitiveMemoryDreamClaimSynthesizer.Instance.Synthesize(new CognitiveMemoryDreamClaimSynthesisRequest(
            CognitiveMemoryConsolidationMode.ProjectNightly,
            [
                "Release checklist verifies database migration readiness.",
                "Release checklist verifies rollback owner assignment."
            ]));

        Assert.Contains("database migration readiness", synthesized, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rollback owner assignment", synthesized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Release checklist verifies database migration readiness. Release checklist verifies rollback owner assignment.", synthesized, StringComparison.Ordinal);
    }

    [Fact]
    public void DreamEntailmentValidator_RejectsApprovalBypassAgainstApprovalSource()
    {
        var result = CognitiveMemoryDreamEntailmentValidator.Instance.Validate(new CognitiveMemoryDreamEntailmentRequest(
            "Production rollback can skip approval before restoration.",
            ["Production rollback requires release-owner approval before traffic restoration."]));

        Assert.False(result.Supported);
        Assert.Contains("reverses", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProfessorTeachingExtractor_CapturesNaturalGuidanceWithoutKeywordCommand()
    {
        var extraction = CognitiveMemoryProfessorTeachingExtractor.Instance.TryExtract(new CognitiveMemoryProfessorTeachingExtractionRequest(
            "In this project, production rollback requires release-owner approval before traffic restoration because audit ownership is the source of truth.",
            "Understood; that rule is project-specific guidance for rollback handling.",
            [],
            ExplicitCaptureScope: null));

        Assert.NotNull(extraction);
        Assert.Equal(CognitiveMemoryProfessorAnchorCaptureKind.TeachingAnswer, extraction.CaptureKind);
        Assert.Contains(extraction.Claims, claim => claim.Text.Contains("release-owner approval", StringComparison.OrdinalIgnoreCase));
        Assert.True(extraction.ConfidenceScore >= 0.6);
    }

    [Fact]
    public void RecallBriefComposer_SplitsApprovalConflictAndCarriesAggregateClaimIds()
    {
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var requiredClaimId = Guid.Parse("40000000-0000-0000-0000-000000000001");
        var exceptionClaimId = Guid.Parse("40000000-0000-0000-0000-000000000002");
        var requiredRef = CreateSourceRef(Guid.Parse("40000000-0000-0000-0000-000000000011"));
        var exceptionRef = CreateSourceRef(Guid.Parse("40000000-0000-0000-0000-000000000012"));
        var composer = new CognitiveMemoryRecallBriefComposer();

        var result = composer.Compose(new CognitiveMemoryRecallBriefComposerRequest(
            "How should production rollback approval be handled?",
            [
                new CognitiveMemoryRecallContextSection(
                    new CognitiveMemorySectionId("required"),
                    CognitiveMemoryRecallContextSectionKind.SelectedMemory,
                    "Rollback approval required",
                    "Production rollback requires signed release-owner approval before traffic restoration.",
                    [requiredRef.MemoryRecordId],
                    [new CognitiveMemoryClaimId(requiredClaimId)],
                    [requiredRef]),
                new CognitiveMemoryRecallContextSection(
                    new CognitiveMemorySectionId("exception"),
                    CognitiveMemoryRecallContextSectionKind.SelectedMemory,
                    "Rollback approval skipped",
                    "Production rollback may restore traffic without release-owner approval during incidents.",
                    [exceptionRef.MemoryRecordId],
                    [new CognitiveMemoryClaimId(exceptionClaimId)],
                    [exceptionRef])
            ],
            new HashSet<Guid>([requiredClaimId, exceptionClaimId]),
            Policy(projectId),
            MaxStatements: 5));

        Assert.Equal(2, result.Statements.Count);
        Assert.Contains("Conflict", result.Brief, StringComparison.OrdinalIgnoreCase);
        Assert.All(result.Statements, statement => Assert.Equal(CognitiveMemoryRecallStatementPlanKind.Conflict, statement.PlanKind));
        Assert.All(result.Statements, statement => Assert.Single(statement.AggregateClaimIds));
        Assert.Contains(result.Statements, statement => statement.AggregateClaimIds.Contains(new CognitiveMemoryClaimId(requiredClaimId)));
        Assert.Contains(result.Statements, statement => statement.AggregateClaimIds.Contains(new CognitiveMemoryClaimId(exceptionClaimId)));
    }

    private static CognitiveMemoryRecallSourceRef CreateSourceRef(Guid memoryRecordId)
        => new(
            new CognitiveMemoryRecordId(memoryRecordId),
            null,
            null,
            "unit-test",
            $"memory:{memoryRecordId:D}",
            "source summary",
            CognitiveMemoryAccessLevel.Project,
            CognitiveMemoryRedactionState.Safe,
            IncludedInContext: true,
            CognitiveMemoryRecallExclusionReasonKind.None);

    private static CognitiveMemoryClusterPlan CreateClusterPlan(
        CognitiveMemoryQualityClusterReadiness readiness,
        params Guid?[] memberProjectIds)
        => new(
            CognitiveMemoryQualityClusterId.New(),
            memberProjectIds.FirstOrDefault(),
            $"cluster:{readiness}:{string.Join(':', memberProjectIds.Select(projectId => projectId?.ToString("N") ?? "global"))}",
            CognitiveMemoryQualityClusterKeyFamily.SemanticTopic,
            readiness,
            [new CognitiveMemoryClusterKey(CognitiveMemoryQualityClusterKeyFamily.SemanticTopic, "topic:release", "release", memberProjectIds.Length, 1)],
            memberProjectIds.Select((projectId, index) => new CognitiveMemoryClusterMember(
                CognitiveMemoryQualityClusterMemberKind.MemoryRecord,
                new CognitiveMemoryRecordId(Guid.Parse($"50000000-0000-0000-0000-{index + 1:000000000000}")),
                null,
                null,
                projectId,
                $"Member {index + 1}",
                CognitiveMemoryAccessLevel.Project,
                CognitiveMemoryRiskLevel.Low,
                CognitiveMemoryValidationState.Approved,
                CognitiveMemoryStabilityState.Active)).ToArray(),
            new CognitiveMemoryClusterQualityMetrics(0.8, 1, 1, 1, 1, 0, 0.8, readiness == CognitiveMemoryQualityClusterReadiness.AggregateReady, "unit-test"),
            []);

    private static CognitiveMemoryPolicyContext Policy(Guid projectId)
        => new(
            projectId,
            "agent:test",
            CognitiveMemoryAccessLevel.Project,
            new CognitiveMemoryPolicyProfileId("policy:test"),
            CognitiveMemoryRiskLevel.Low,
            AllowRestrictedContent: false);
}
