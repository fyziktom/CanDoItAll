using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support.CognitiveMemory;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit;

public sealed class CognitiveMemoryRecallOrchestratorTests
{
    [Fact]
    public async Task RecallAsync_InhibitsDockerTestContextWhenProductionContextIsActive()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var production = await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            "Docker production deployment",
            "Use production deployment Docker files and deployment runbooks for production releases.",
            "Production Docker deployment evidence.");
        var test = await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000002"),
            "Docker test simulation",
            "Use the local Docker simulation only for test validation.",
            "Test Docker simulation evidence.");
        fixture.DbContext.Add(new CognitiveMemoryRelationRecord
        {
            ProjectId = projectId,
            SourceMemoryRecordId = production.RecordId,
            TargetMemoryRecordId = test.RecordId,
            RelationKind = CognitiveMemoryRelationKind.SemanticallyRelatedButContextSeparated,
            EvidenceCount = 1,
            RelationBucket = CognitiveMemoryScoreProjectionBucket.Inhibit,
            DisplayStrengthProjection = 0.95,
            Reason = "Local/test Docker simulation is related but not substitutable for production deployment.",
            AlgorithmVersion = "taxonomy-v1",
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        });
        await fixture.DbContext.SaveChangesAsync();

        var adapter = new RecordingProjectionAdapter(
            [
                new CognitiveMemoryProjectionSearchHit(
                    new CognitiveMemoryProjectionPointId("prod"),
                    new CognitiveMemoryRecordId(production.RecordId),
                    CognitiveMemoryHash.FromUtf8("prod-payload"),
                    0.96,
                    new Dictionary<string, object?>()),
                new CognitiveMemoryProjectionSearchHit(
                    new CognitiveMemoryProjectionPointId("test"),
                    new CognitiveMemoryRecordId(test.RecordId),
                    CognitiveMemoryHash.FromUtf8("test-payload"),
                    0.95,
                    new Dictionary<string, object?>())
            ]);
        var orchestrator = CreateOrchestrator(fixture, adapter);

        var result = await orchestrator.RecallAsync(CreateRequest(projectId, "How should we use Docker for production deployment?"));

        Assert.Contains(result.Candidates, candidate =>
            candidate.MemoryRecordId.Value == production.RecordId &&
            candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected);
        var inhibited = Assert.Single(result.Candidates, candidate => candidate.MemoryRecordId.Value == test.RecordId);
        Assert.Equal(CognitiveMemoryRecallCandidateDecisionKind.Inhibited, inhibited.DecisionKind);
        Assert.Equal(CognitiveMemoryRecallExclusionReasonKind.ContextBoundary, inhibited.ExclusionReasonKind);
        Assert.Contains(inhibited.ScoreTrace.InputVectors.Single().Components, component => component.DimensionKind == CognitiveMemoryScoreDimensionKind.ContextSeparation);
        Assert.Contains(result.ContextPack.Sections, section => section.SectionKind == CognitiveMemoryRecallContextSectionKind.DoNotConfuseWith);
        Assert.Single(adapter.SearchRequests);
        Assert.Equal(projectId, adapter.SearchRequests[0].Filter?.ProjectId);
        Assert.NotNull(adapter.SearchRequests[0].Filter);
    }

    [Fact]
    public async Task RecallAsync_RecordsProjectionUnavailableAndFallsBackToLexicalRecall()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var memory = await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000003"),
            "Docker production deployment",
            "Production Docker deployment uses release runbooks.",
            "Production source evidence.");
        var adapter = new RecordingProjectionAdapter([], supportsFilters: false);
        var orchestrator = CreateOrchestrator(fixture, adapter);

        var result = await orchestrator.RecallAsync(CreateRequest(projectId, "Docker production deployment"));

        Assert.Contains(result.Candidates, candidate =>
            candidate.MemoryRecordId.Value == memory.RecordId &&
            candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected);
        Assert.Contains(result.Stages, stage =>
            stage.ChannelKind == CognitiveMemoryRecallChannelKind.VectorProjection &&
            stage.Status == CognitiveMemoryRecallStageStatus.Unavailable);
        Assert.Contains(result.Warnings, warning => warning.Contains("typed filters", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(adapter.SearchRequests);
    }

    [Fact]
    public async Task RecallAsync_ExcludesActiveProfessorAnchorMemoryByDefault()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var activeAnchorMemory = await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000201"),
            "Temporary professor rollback approval",
            "Temporary professor anchor says rollback approval requires release-owner approval.",
            "Professor anchor source evidence.");
        var stableMemory = await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000202"),
            "Stable rollback approval rule",
            "Release audit stable memory says rollback approval requires release-owner approval.",
            "Stable release audit evidence.");
        fixture.DbContext.Add(new CognitiveMemoryCuratorCapturedImprovementRecord
        {
            CuratorSessionId = Guid.NewGuid(),
            CuratorTurnId = Guid.NewGuid(),
            ProjectId = projectId,
            CaptureKind = CognitiveMemoryCuratorCaptureKind.NewKnowledge,
            ConversationDepth = CognitiveMemoryCuratorConversationDepth.Medium,
            Status = CognitiveMemoryCuratorCaptureStatus.Applied,
            TargetingStatus = CognitiveMemoryCuratorTargetingStatus.Untargeted,
            AnchorState = CognitiveMemoryProfessorAnchorState.Active,
            AppliedMemoryRecordId = activeAnchorMemory.RecordId,
            ActorId = "agent:test",
            ConfidenceScore = 0.82,
            PriorityScore = 0.82,
            TargetConfidenceScore = 0.82,
            CaptureLanguage = "en",
            CaptureScope = "rollback approval",
            Summary = "Claim: rollback approval requires release-owner approval.",
            CorrectionText = "Structured professor anchor fixture.",
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        });
        await fixture.DbContext.SaveChangesAsync();
        var orchestrator = CreateOrchestrator(fixture, new RecordingProjectionAdapter([]));

        var defaultResult = await orchestrator.RecallAsync(CreateRequest(projectId, "rollback approval release-owner"));
        var includeResult = await orchestrator.RecallAsync(CreateRequest(
            projectId,
            "rollback approval release-owner",
            metadata: new Dictionary<string, string>
            {
                ["includeProfessorAnchors"] = "true"
            }));

        Assert.DoesNotContain(defaultResult.Candidates, candidate => candidate.MemoryRecordId.Value == activeAnchorMemory.RecordId);
        Assert.Contains(defaultResult.Candidates, candidate =>
            candidate.MemoryRecordId.Value == stableMemory.RecordId &&
            candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected);
        Assert.Contains(includeResult.Candidates, candidate =>
            candidate.MemoryRecordId.Value == activeAnchorMemory.RecordId &&
            candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected);
    }

    [Fact]
    public async Task RecallAsync_UsesScoredProjectLexicalScanWhenFirstTermMisses()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var memory = await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000103"),
            "FieldOps Mobile App",
            "Pilot added barcode scanning for asset identity and a route-day view for customer-site grouping.",
            "Operational update source evidence.");
        var adapter = new RecordingProjectionAdapter([]);
        var orchestrator = CreateOrchestrator(fixture, adapter);

        var result = await orchestrator.RecallAsync(CreateRequest(
            projectId,
            "What changed after the operational update for FieldOps Mobile App?"));

        Assert.Contains(result.Candidates, candidate =>
            candidate.MemoryRecordId.Value == memory.RecordId &&
            candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected);
        Assert.Contains(result.Stages, stage =>
            stage.ChannelKind == CognitiveMemoryRecallChannelKind.Lexical &&
            stage.ProviderTrace.Contains("lexical:terms", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RecallAsync_UsesSpecificTermsWhenBroadFirstTermFillsBudget()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        for (var index = 0; index < 6; index++)
        {
            await SeedMemoryAsync(
                fixture,
                projectId,
                Guid.Parse($"20000000-0000-0000-0000-{index + 1:000000000000}"),
                $"AI Tap market assumption {index}",
                "AI Tap market channel household safety distributor planning.",
                "AI Tap market source evidence.");
        }

        var target = await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("20000000-0000-0000-0000-000000000100"),
            "Technical architecture latency messaging",
            "VisionSoftware targets a 0.25 second interval, publishes MQTT JSON, runs on Raspberry Pi-class hardware, and must avoid GPL dependencies.",
            "Technical source evidence.");
        var orchestrator = CreateOrchestrator(fixture, new RecordingProjectionAdapter([]));

        var result = await orchestrator.RecallAsync(CreateRequest(
            projectId,
            "For AI Tap, summarize the technical architecture timing hardware and license risks.",
            new CognitiveMemoryRecallBudget(4, 0, 4, 3, 3, 4096, 4096)));

        Assert.Contains(result.Candidates, candidate =>
            candidate.MemoryRecordId.Value == target.RecordId &&
            candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected);
    }

    [Fact]
    public async Task RecallAsync_UsesSourceTextForLexicalRecallWhenMemorySummaryMissesExactFact()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var memory = await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("20000000-0000-0000-0000-000000000101"),
            "Runtime constraints",
            "Technical runtime constraints were approved for the prototype.",
            "GNU/GPL dependencies are disallowed; MIT, BSD, or otherwise free licenses are acceptable.");
        var orchestrator = CreateOrchestrator(fixture, new RecordingProjectionAdapter([]));

        var result = await orchestrator.RecallAsync(CreateRequest(
            projectId,
            "GPL license",
            new CognitiveMemoryRecallBudget(4, 0, 4, 2, 2, 4096, 4096)));

        Assert.Contains(result.Candidates, candidate =>
            candidate.MemoryRecordId.Value == memory.RecordId &&
            candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected);
        Assert.Contains(
            "GNU/GPL dependencies are disallowed",
            string.Join("\n", result.ContextPack.Sections.Select(section => section.Content)),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecallAsync_BridgesEnglishQueryTermsToCzechSourceTerms()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("20000000-0000-0000-0000-000000000104"),
            "General planning note",
            "General project planning note without financial or compliance facts.",
            "General source text.");
        var memory = await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("20000000-0000-0000-0000-000000000105"),
            "LB4U source finance note",
            "Czech business-plan source evidence is available.",
            "Základní sada stojí v pořizovacích nákladech 10 309 Kč. Navrhovaná prodejní cena je 40 980 Kč. Zařízení zatím nemá medical certifikace.");
        var orchestrator = CreateOrchestrator(fixture, new RecordingProjectionAdapter([]));

        var result = await orchestrator.RecallAsync(CreateRequest(
            projectId,
            "price cost certification",
            new CognitiveMemoryRecallBudget(1, 0, 1, 1, 1, 4096, 4096)));

        Assert.Contains(result.Candidates, candidate =>
            candidate.MemoryRecordId.Value == memory.RecordId &&
            candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected);
        Assert.Contains(
            "40 980 Kč",
            string.Join("\n", result.ContextPack.Sections.Select(section => section.Content)),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecallAsync_RedactsContactLinesFromContextPack()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("20000000-0000-0000-0000-000000000106"),
            "Deployment contact note",
            "Deployment runbook source evidence is available.",
            "Deployment rollback source.\nContact: lucy@example.com, +420 123 456 789.\nUse the deployment rollback runbook when production health checks fail.");
        var orchestrator = CreateOrchestrator(fixture, new RecordingProjectionAdapter([]));

        var result = await orchestrator.RecallAsync(CreateRequest(projectId, "deployment rollback"));
        var content = string.Join("\n", result.ContextPack.Sections.Select(section => section.Content));
        var sourceRefSummaries = string.Join("\n", result.ContextPack.SourceRefs.Select(sourceRef => sourceRef.Summary));

        Assert.DoesNotContain("lucy@example.com", content, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("+420 123 456 789", content, StringComparison.Ordinal);
        Assert.DoesNotContain("lucy@example.com", sourceRefSummaries, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("+420 123 456 789", sourceRefSummaries, StringComparison.Ordinal);
        Assert.Contains("[redacted-contact]", content, StringComparison.Ordinal);
        Assert.Contains("[redacted-contact]", sourceRefSummaries, StringComparison.Ordinal);
        Assert.Contains("Use the deployment rollback runbook", content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RecallAsync_ExpandsProjectStructureChildrenFromSelectedParent()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var manifest = CreateManifest(
            fixture,
            projectId,
            Guid.Parse("20000000-0000-0000-0000-000000000201"),
            "WorkbenchProjectStructure");
        await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("20000000-0000-0000-0000-000000000202"),
            "S02 Technical Architecture",
            "The S02 technical architecture parent groups hardware and runtime constraints.",
            "Title: S02 Technical Architecture",
            existingManifest: manifest,
            sourceSystem: "WorkbenchProjectStructure",
            sourceItemType: "ProjectNode",
            sourceItemKey: "project-node-parent",
            provenanceJson: ProjectNodeProvenance("parent", "root"));
        var license = await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("20000000-0000-0000-0000-000000000203"),
            "Runtime And License Constraints",
            "GNU/GPL dependencies are disallowed; MIT and BSD licenses are acceptable.",
            "GNU/GPL dependencies are disallowed; MIT and BSD licenses are acceptable.",
            existingManifest: manifest,
            sourceSystem: "WorkbenchProjectStructure",
            sourceItemType: "ProjectNode",
            sourceItemKey: "project-node-license",
            provenanceJson: ProjectNodeProvenance("license", "parent"));
        var compute = await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("20000000-0000-0000-0000-000000000204"),
            "Compute Unit",
            "Raspberry Pi-class hardware is enough for the first prototype.",
            "Raspberry Pi-class hardware is enough for the first prototype.",
            existingManifest: manifest,
            sourceSystem: "WorkbenchProjectStructure",
            sourceItemType: "ProjectNode",
            sourceItemKey: "project-node-compute",
            provenanceJson: ProjectNodeProvenance("compute", "parent"));
        var orchestrator = CreateOrchestrator(fixture, new RecordingProjectionAdapter([]));

        var result = await orchestrator.RecallAsync(CreateRequest(
            projectId,
            "technical architecture",
            new CognitiveMemoryRecallBudget(4, 2, 4, 4, 4, 4096, 4096)));

        Assert.Contains(result.Candidates, candidate =>
            candidate.MemoryRecordId.Value == license.RecordId &&
            candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected);
        Assert.Contains(result.Candidates, candidate =>
            candidate.MemoryRecordId.Value == compute.RecordId &&
            candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected);
    }

    [Fact]
    public async Task RecallAsync_DeduplicatesProjectStructureNeighborsBeforeApplyingExpansionLimit()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var firstManifest = CreateManifest(
            fixture,
            projectId,
            Guid.Parse("20000000-0000-0000-0000-000000000211"),
            "WorkbenchProjectStructure");
        var secondManifest = CreateManifest(
            fixture,
            projectId,
            Guid.Parse("20000000-0000-0000-0000-000000000212"),
            "WorkbenchProjectStructure");
        await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("20000000-0000-0000-0000-000000000213"),
            "Technical Architecture",
            "Technical architecture parent groups detailed child facts.",
            "Title: Technical Architecture",
            existingManifest: firstManifest,
            sourceSystem: "WorkbenchProjectStructure",
            sourceItemType: "ProjectNode",
            sourceItemKey: "project-node-parent",
            provenanceJson: ProjectNodeProvenance("parent", "root"));
        await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("20000000-0000-0000-0000-000000000214"),
            "Duplicate Child",
            "First duplicated child.",
            "First duplicated child.",
            existingManifest: firstManifest,
            sourceSystem: "WorkbenchProjectStructure",
            sourceItemType: "ProjectNode",
            sourceItemKey: "project-node-duplicate-child",
            provenanceJson: ProjectNodeProvenance("duplicate", "parent"));
        await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("20000000-0000-0000-0000-000000000215"),
            "Duplicate Child",
            "Second duplicated child from another source manifest.",
            "Second duplicated child from another source manifest.",
            existingManifest: secondManifest,
            sourceSystem: "WorkbenchProjectStructure",
            sourceItemType: "ProjectNode",
            sourceItemKey: "project-node-duplicate-child",
            provenanceJson: ProjectNodeProvenance("duplicate", "parent"));
        var target = await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("20000000-0000-0000-0000-000000000216"),
            "Runtime And License Constraints",
            "GNU/GPL dependencies are disallowed; MIT and BSD licenses are acceptable.",
            "GNU/GPL dependencies are disallowed; MIT and BSD licenses are acceptable.",
            existingManifest: firstManifest,
            sourceSystem: "WorkbenchProjectStructure",
            sourceItemType: "ProjectNode",
            sourceItemKey: "project-node-runtime",
            provenanceJson: ProjectNodeProvenance("runtime", "parent"));
        var orchestrator = CreateOrchestrator(fixture, new RecordingProjectionAdapter([]));

        var result = await orchestrator.RecallAsync(CreateRequest(
            projectId,
            "technical architecture",
            new CognitiveMemoryRecallBudget(2, 1, 4, 4, 4, 4096, 4096)));

        Assert.Contains(result.Candidates, candidate =>
            candidate.MemoryRecordId.Value == target.RecordId &&
            candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected);
    }

    [Fact]
    public async Task RecallAsync_UsesStageMetadataToPreferMatchingSourceSlice()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var earlierStage = await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("20000000-0000-0000-0000-000000000401"),
            "S01 Shared Plan",
            "Subtitle: Source truth S01 level 4\nShared plan covers early market assumptions.",
            "Shared plan covers early market assumptions.");
        var requestedStage = await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("20000000-0000-0000-0000-000000000402"),
            "S02 Shared Plan",
            "Subtitle: Source truth S02 level 4\nShared plan covers the technical architecture.",
            "Shared plan covers the technical architecture.");
        var orchestrator = CreateOrchestrator(fixture, new RecordingProjectionAdapter([]));

        var result = await orchestrator.RecallAsync(CreateRequest(
            projectId,
            "shared plan",
            new CognitiveMemoryRecallBudget(8, 0, 8, 1, 1, 4096, 4096),
            metadata: new Dictionary<string, string>
            {
                ["stageId"] = "S02"
            }));

        Assert.Contains(result.Candidates, candidate =>
            candidate.MemoryRecordId.Value == requestedStage.RecordId &&
            candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected);
        Assert.Contains(result.Candidates, candidate =>
            candidate.MemoryRecordId.Value == earlierStage.RecordId &&
            candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Excluded &&
            candidate.ExclusionReasonKind == CognitiveMemoryRecallExclusionReasonKind.BudgetLimit);
    }

    [Fact]
    public async Task RecallAsync_DeduplicatesEquivalentFocusCandidatesBeforeApplyingBudget()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var duplicateText = "Deployment parent context describes the shared rollout plan.";
        await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("20000000-0000-0000-0000-000000000301"),
            "A Deployment Parent",
            duplicateText,
            duplicateText);
        var duplicate = await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("20000000-0000-0000-0000-000000000302"),
            "A Deployment Parent",
            duplicateText,
            duplicateText);
        var target = await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("20000000-0000-0000-0000-000000000303"),
            "Z Deployment Leaf",
            "Deployment leaf context has the concrete rollout gate.",
            "Deployment leaf source evidence.");
        var orchestrator = CreateOrchestrator(fixture, new RecordingProjectionAdapter([]));

        var result = await orchestrator.RecallAsync(CreateRequest(
            projectId,
            "deployment",
            new CognitiveMemoryRecallBudget(8, 0, 8, 2, 2, 4096, 4096)));

        Assert.Equal(1, result.Candidates.Count(candidate =>
            candidate.Title == "A Deployment Parent" &&
            candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected));
        Assert.Contains(result.Candidates, candidate =>
            candidate.MemoryRecordId.Value == duplicate.RecordId &&
            candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Excluded &&
            candidate.ExclusionReasonKind == CognitiveMemoryRecallExclusionReasonKind.NotInFocus);
        Assert.Contains(result.Candidates, candidate =>
            candidate.MemoryRecordId.Value == target.RecordId &&
            candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected);
    }

    [Fact]
    public async Task RecallAsync_RecordsBudgetExclusionsInsteadOfSilentTruncation()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000004"),
            "Docker production deployment",
            "Production Docker deployment memory.",
            "Production source evidence.");
        await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000005"),
            "Docker deployment validation",
            "Docker deployment validation memory.",
            "Validation source evidence.");
        var orchestrator = CreateOrchestrator(fixture, new RecordingProjectionAdapter([]));

        var result = await orchestrator.RecallAsync(CreateRequest(
            projectId,
            "Docker deployment",
            new CognitiveMemoryRecallBudget(8, 0, 4, 1, 1, 4096, 4096)));

        Assert.Single(result.Candidates, candidate => candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected);
        Assert.Contains(result.Candidates, candidate =>
            candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Excluded &&
            candidate.ExclusionReasonKind == CognitiveMemoryRecallExclusionReasonKind.BudgetLimit);
        Assert.Contains(result.Stages, stage =>
            stage.StageKind == CognitiveMemoryRecallTraceStageKind.FocusSelection &&
            stage.LimitingBudget == CognitiveMemoryBudgetLimit.ItemCount);
    }

    [Fact]
    public async Task RecallAsync_RetainsSourceInsufficientCandidateAsSideContext()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var recordId = Guid.Parse("10000000-0000-0000-0000-000000000306");
        await SeedUnsupportedMemoryAsync(
            fixture,
            projectId,
            recordId,
            "Deployment rollback note",
            "Deployment rollback note has lexical relevance but no source evidence.");
        var orchestrator = CreateOrchestrator(fixture, new RecordingProjectionAdapter([]));

        var result = await orchestrator.RecallAsync(CreateRequest(projectId, "deployment rollback"));

        var candidate = Assert.Single(result.Candidates, candidate => candidate.MemoryRecordId.Value == recordId);
        Assert.Equal(CognitiveMemoryRecallCandidateDecisionKind.SideContext, candidate.DecisionKind);
        Assert.Equal(CognitiveMemoryRecallExclusionReasonKind.SourceInsufficient, candidate.ExclusionReasonKind);
        Assert.DoesNotContain(result.ContextPack.Sections, section =>
            section.SectionKind == CognitiveMemoryRecallContextSectionKind.SelectedMemory &&
            section.MemoryRecordIds.Any(memoryRecordId => memoryRecordId.Value == recordId));
    }

    [Fact]
    public async Task RecallAsync_DoesNotInjectRestrictedSourceContentIntoContextPack()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var memory = await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000006"),
            "Docker deployment secret handling",
            "Deployment memory must not leak restricted source text.",
            "SECRET_TOKEN=do-not-inject",
            sourceAccessLevel: CognitiveMemoryAccessLevel.Restricted,
            sourceRedactionState: CognitiveMemoryRedactionState.Restricted);
        var orchestrator = CreateOrchestrator(fixture, new RecordingProjectionAdapter([]));

        var result = await orchestrator.RecallAsync(CreateRequest(projectId, "Docker deployment secret handling"));

        var context = string.Join("\n", result.ContextPack.Sections.Select(section => section.Content));
        Assert.DoesNotContain("SECRET_TOKEN", context, StringComparison.Ordinal);
        Assert.Contains(result.ContextPack.SourceRefs, sourceRef =>
            sourceRef.MemoryRecordId.Value == memory.RecordId &&
            !sourceRef.IncludedInContext &&
            sourceRef.ExclusionReasonKind == CognitiveMemoryRecallExclusionReasonKind.AccessPolicy);
        Assert.Equal(0, await fixture.DbContext.Set<CognitiveMemoryMutationCommandRecord>().CountAsync());
    }

    [Fact]
    public async Task RecallAsync_IncludesRestrictedSourceContentWhenPolicyAllowsIt()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var memory = await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000106"),
            "Restricted deployment rotation plan",
            "Deployment memory requires the restricted credential rotation plan.",
            "ROTATION_PLAN=rotate-on-release",
            sourceAccessLevel: CognitiveMemoryAccessLevel.Restricted,
            sourceRedactionState: CognitiveMemoryRedactionState.Restricted,
            recordAccessLevel: CognitiveMemoryAccessLevel.Restricted);
        var orchestrator = CreateOrchestrator(fixture, new RecordingProjectionAdapter([]));

        var result = await orchestrator.RecallAsync(CreateRequest(
            projectId,
            "deployment restricted rotation plan",
            allowRestrictedContent: true,
            accessLevel: CognitiveMemoryAccessLevel.Restricted));

        var context = string.Join("\n", result.ContextPack.Sections.Select(section => section.Content));
        Assert.Contains("ROTATION_PLAN=rotate-on-release", context, StringComparison.Ordinal);
        Assert.Contains(result.ContextPack.SourceRefs, sourceRef =>
            sourceRef.MemoryRecordId.Value == memory.RecordId &&
            sourceRef.IncludedInContext &&
            sourceRef.ExclusionReasonKind == CognitiveMemoryRecallExclusionReasonKind.None);
        Assert.Equal(0, await fixture.DbContext.Set<CognitiveMemoryMutationCommandRecord>().CountAsync());
    }

    [Fact]
    public async Task RecallAsync_DeduplicatesRepeatedMemoryAndSourceTextInContextPack()
    {
        var fixture = CreateFixture();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var repeated = "Offline sync uses a local queue, idempotency keys, explicit conflict review, retry visibility, supervisor approval, and audit-safe evidence retention.";
        await SeedMemoryAsync(
            fixture,
            projectId,
            Guid.Parse("10000000-0000-0000-0000-000000000007"),
            "Offline sync architecture",
            repeated,
            $"{repeated} Additional source-only detail should not duplicate the memory summary prefix.");
        var orchestrator = CreateOrchestrator(fixture, new RecordingProjectionAdapter([]));

        var result = await orchestrator.RecallAsync(CreateRequest(projectId, "offline sync queue conflict"));

        var section = Assert.Single(result.ContextPack.Sections);
        Assert.Equal(1, CountOccurrences(section.Content, repeated));
        Assert.DoesNotContain($"Source detail: {repeated}", section.Content, StringComparison.Ordinal);
    }

    private static CognitiveMemoryRecallOrchestrator CreateOrchestrator(
        TestFixture fixture,
        RecordingProjectionAdapter adapter)
    {
        var registry = new CognitiveMemoryScoreSpaceRegistry();
        var driver = new CognitiveMemoryScoreGeometryDriver(registry);
        var signalLedger = new CognitiveMemorySignalLedger(
            fixture.Factory,
            registry,
            driver,
            fixture.Clock);
        var workspace = new CognitiveMemoryWorkspaceService(fixture.Factory, fixture.Clock);
        return new CognitiveMemoryRecallOrchestrator(
            fixture.Factory,
            new FakeCognitiveMemoryEmbeddingProvider(dimensions: 3),
            adapter,
            driver,
            signalLedger,
            workspace,
            fixture.Clock,
            NullLogger<CognitiveMemoryRecallOrchestrator>.Instance);
    }

    private static CognitiveMemoryRecallRequest CreateRequest(
        Guid projectId,
        string query,
        CognitiveMemoryRecallBudget? budget = null,
        bool allowRestrictedContent = false,
        CognitiveMemoryAccessLevel accessLevel = CognitiveMemoryAccessLevel.Project,
        IReadOnlyDictionary<string, string>? metadata = null)
        => new(
            projectId,
            query,
            CognitiveMemoryRecallIntentKind.Deployment,
            CognitiveMemoryRecallMode.FocusedTaskContext,
            Policy(projectId, allowRestrictedContent, accessLevel),
            budget ?? new CognitiveMemoryRecallBudget(8, 1, 8, 4, 4, 4096, 4096),
            ProjectionCollectionName: new CognitiveMemoryProjectionCollectionName("cm-test"),
            ProjectionProfileId: new CognitiveMemoryProjectionProfileId("projection-v1"),
            EmbeddingProfileId: new CognitiveMemoryEmbeddingProfileId("embedding-v1"),
            Metadata: metadata);

    private static CognitiveMemoryPolicyContext Policy(
        Guid projectId,
        bool allowRestrictedContent = false,
        CognitiveMemoryAccessLevel accessLevel = CognitiveMemoryAccessLevel.Project)
        => new(
            projectId,
            "agent:test",
            accessLevel,
            new CognitiveMemoryPolicyProfileId("policy:test"),
            CognitiveMemoryRiskLevel.Low,
            allowRestrictedContent);

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while (true)
        {
            index = text.IndexOf(value, index, StringComparison.Ordinal);
            if (index < 0)
            {
                return count;
            }

            count++;
            index += value.Length;
        }
    }

    private static CognitiveMemorySourceManifestRecord CreateManifest(
        TestFixture fixture,
        Guid projectId,
        Guid seedId,
        string sourceSystem)
    {
        var manifest = new CognitiveMemorySourceManifestRecord
        {
            ProjectId = projectId,
            SourceSystem = sourceSystem,
            SourceScopeKey = projectId.ToString("D"),
            SourceSnapshotId = $"snapshot-{seedId:D}",
            SnapshotHash = CognitiveMemoryHash.FromUtf8($"snapshot-{seedId:D}").Value,
            ProviderVersion = "unit-test-v1",
            ScanStatus = CognitiveMemoryRunStatus.Succeeded,
            ObservedAtUtc = fixture.Clock.GetUtcNow(),
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            UpdatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        fixture.DbContext.Add(manifest);
        fixture.DbContext.SaveChanges();
        return manifest;
    }

    private static string ProjectNodeProvenance(string sourceEntityId, string parentId)
        => $"{{\"sourceEntityId\":\"{sourceEntityId}\",\"metadata\":{{\"parentId\":\"{parentId}\"}}}}";

    private static async Task<SeededMemory> SeedMemoryAsync(
        TestFixture fixture,
        Guid projectId,
        Guid recordId,
        string title,
        string canonicalText,
        string sourceText,
        CognitiveMemoryAccessLevel sourceAccessLevel = CognitiveMemoryAccessLevel.Project,
        CognitiveMemoryRedactionState sourceRedactionState = CognitiveMemoryRedactionState.Safe,
        CognitiveMemoryAccessLevel recordAccessLevel = CognitiveMemoryAccessLevel.Project,
        CognitiveMemorySourceManifestRecord? existingManifest = null,
        string sourceSystem = "unit-test",
        string sourceItemType = "test-node",
        string? sourceItemKey = null,
        string provenanceJson = "{}")
    {
        var manifest = existingManifest ?? new CognitiveMemorySourceManifestRecord
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
            SourceItemKey = sourceItemKey ?? $"source-{recordId:D}",
            SourceItemType = sourceItemType,
            Title = title,
            ContentText = sourceText,
            Locator = $"/unit/{recordId:D}",
            ContentHash = CognitiveMemoryHash.FromUtf8(sourceText).Value,
            RedactionState = sourceRedactionState,
            AccessLevel = sourceAccessLevel,
            AccessScope = "unit",
            ProvenanceJson = provenanceJson,
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
            SourceSystem = "unit-test",
            Locator = sourceItem.Locator,
            StructuredPath = "$.content",
            TextStart = 0,
            TextEnd = Math.Min(sourceText.Length, 64),
            QuoteHash = CognitiveMemoryHash.FromUtf8($"{recordId:D}:quote").Value,
            TrustLevel = CognitiveMemorySourceTrustLevel.RuntimeSource,
            RedactionState = sourceRedactionState,
            SourceHash = sourceItem.ContentHash,
            ObservedAtUtc = fixture.Clock.GetUtcNow(),
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        var record = new CognitiveMemoryRecord
        {
            Id = recordId,
            ProjectId = projectId,
            Kind = CognitiveMemoryRecordKind.Semantic,
            Origin = CognitiveMemoryRecordOrigin.SourceDerived,
            Title = title,
            CanonicalText = canonicalText,
            SummaryText = canonicalText,
            TopicKey = title.ToLowerInvariant().Replace(' ', '.'),
            ValidationState = CognitiveMemoryValidationState.Approved,
            StabilityState = CognitiveMemoryStabilityState.Active,
            CreatedInMode = CognitiveMemoryOperationMode.Observe,
            AlgorithmVersion = "taxonomy-v1",
            ContentHash = CognitiveMemoryHash.FromUtf8(canonicalText).Value,
            SourceEvidenceCount = 1,
            EvidenceAnchorCount = 1,
            AccessLevel = recordAccessLevel,
            RiskLevel = CognitiveMemoryRiskLevel.Low,
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            UpdatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        if (existingManifest is null)
        {
            fixture.DbContext.Add(manifest);
        }

        fixture.DbContext.AddRange(
            sourceItem,
            anchor,
            record,
            new CognitiveMemorySourceLinkRecord
            {
                MemoryRecordId = record.Id,
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
                MemoryRecordId = record.Id,
                EvidenceAnchorId = anchor.Id,
                EvidenceRole = CognitiveMemoryEvidenceRole.PrimarySource,
                Summary = sourceText,
                CreatedAtUtc = fixture.Clock.GetUtcNow()
            });
        await fixture.DbContext.SaveChangesAsync();
        return new SeededMemory(record.Id, sourceItem.Id, anchor.Id);
    }

    private static async Task SeedUnsupportedMemoryAsync(
        TestFixture fixture,
        Guid projectId,
        Guid recordId,
        string title,
        string canonicalText)
    {
        fixture.DbContext.Add(new CognitiveMemoryRecord
        {
            Id = recordId,
            ProjectId = projectId,
            Kind = CognitiveMemoryRecordKind.Semantic,
            Origin = CognitiveMemoryRecordOrigin.HumanEntered,
            Title = title,
            CanonicalText = canonicalText,
            SummaryText = canonicalText,
            TopicKey = title.ToLowerInvariant().Replace(' ', '.'),
            ValidationState = CognitiveMemoryValidationState.Approved,
            StabilityState = CognitiveMemoryStabilityState.Active,
            CreatedInMode = CognitiveMemoryOperationMode.Observe,
            AlgorithmVersion = "taxonomy-v1",
            ContentHash = CognitiveMemoryHash.FromUtf8(canonicalText).Value,
            SourceEvidenceCount = 0,
            EvidenceAnchorCount = 0,
            AccessLevel = CognitiveMemoryAccessLevel.Project,
            RiskLevel = CognitiveMemoryRiskLevel.Low,
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            UpdatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        });
        await fixture.DbContext.SaveChangesAsync();
    }

    private static TestFixture CreateFixture()
    {
        AppDbContextModelRegistry.ConfigureAssemblies([typeof(CognitiveMemoryModuleAssemblyMarker).Assembly]);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"cognitive-memory-recall-{Guid.NewGuid():N}")
            .Options;
        return new TestFixture(new TestDbContextFactory(options), new FixedClock());
    }

    private sealed record SeededMemory(
        Guid RecordId,
        Guid SourceItemId,
        Guid EvidenceAnchorId);

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
    }

    private sealed record TestFixture(
        TestDbContextFactory Factory,
        FixedClock Clock)
    {
        public AppDbContext DbContext { get; } = Factory.CreateDbContext();
    }

    private sealed class TestDbContextFactory(DbContextOptions<AppDbContext> options) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => new(options);

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(CreateDbContext());
    }

    private sealed class RecordingProjectionAdapter(
        IReadOnlyList<CognitiveMemoryProjectionSearchHit> hits,
        bool supportsFilters = true) : ICognitiveMemoryProjectionAdapter
    {
        public CognitiveMemoryProjectionAdapterCapabilities Capabilities { get; } = new(
            "fake-rag",
            supportsFilters,
            SupportsPayloadIndexes: true,
            SupportsDeleteByFilter: true,
            SupportsNamedVectors: false);

        public List<CognitiveMemoryProjectionSearchRequest> SearchRequests { get; } = [];

        public ValueTask EnsureCollectionAsync(
            CognitiveMemoryProjectionCollectionRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<IReadOnlyList<CognitiveMemoryProjectionPayloadIndexResult>> EnsurePayloadIndexesAsync(
            CognitiveMemoryProjectionPayloadIndexRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult<IReadOnlyList<CognitiveMemoryProjectionPayloadIndexResult>>([]);

        public ValueTask ProjectAsync(
            CognitiveMemoryProjectionWriteRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;

        public ValueTask<CognitiveMemoryProjectionSearchResult> SearchAsync(
            CognitiveMemoryProjectionSearchRequest request,
            CancellationToken cancellationToken = default)
        {
            SearchRequests.Add(request);
            return ValueTask.FromResult(new CognitiveMemoryProjectionSearchResult(
                request.ProjectionProfileId,
                hits.Take(request.Page.Take).ToArray(),
                $"fake-rag:search:{hits.Count}"));
        }

        public ValueTask<CognitiveMemoryProjectionDeleteResult> DeleteBySourceAsync(
            CognitiveMemoryProjectionDeleteBySourceRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new CognitiveMemoryProjectionDeleteResult("fake-rag:delete"));
    }
}
