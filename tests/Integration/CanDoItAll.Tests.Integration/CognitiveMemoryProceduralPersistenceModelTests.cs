using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging.Abstractions;

using CanDoItAll.Tests.Support;
namespace CanDoItAll.Tests.Integration;

public sealed class CognitiveMemoryProceduralPersistenceModelTests
{
    [Fact]
    public async Task ProceduralPersistenceModel_IndexesEntitiesTypedEnumsAndScoreSpaces()
    {
        await using var fixture = await CreateFixtureAsync();
        var entityTypes = fixture.DbContext.Model.GetEntityTypes().ToList();

        AssertEntityTable<CognitiveMemoryProcedureSkillRecord>(entityTypes, "CognitiveMemory_ProcedureSkills");
        AssertEntityTable<CognitiveMemoryProcedureStepRecord>(entityTypes, "CognitiveMemory_ProcedureSteps");
        AssertEntityTable<CognitiveMemoryProcedureStepEvidenceRecord>(entityTypes, "CognitiveMemory_ProcedureStepEvidence");
        AssertEntityTable<CognitiveMemoryProcedureFailureModeRecord>(entityTypes, "CognitiveMemory_ProcedureFailureModes");
        AssertEntityTable<CognitiveMemoryProcedureFailureModePredictionErrorRecord>(entityTypes, "CognitiveMemory_ProcedureFailureModePredictionErrors");
        AssertEntityTable<CognitiveMemoryProcedureFailureModeEpisodeRecord>(entityTypes, "CognitiveMemory_ProcedureFailureModeEpisodes");
        AssertEntityTable<CognitiveMemoryProcedureValidationEvidenceRecord>(entityTypes, "CognitiveMemory_ProcedureValidationEvidence");
        AssertEntityTable<CognitiveMemoryProcedureAutomationBindingRecord>(entityTypes, "CognitiveMemory_ProcedureAutomationBindings");
        AssertEntityTable<CognitiveMemoryProcedureSimulationRecord>(entityTypes, "CognitiveMemory_ProcedureSimulations");
        AssertEntityTable<CognitiveMemoryProcedureSimulationSkillRecord>(entityTypes, "CognitiveMemory_ProcedureSimulationSkills");
        AssertEntityTable<CognitiveMemoryProcedureSimulationEvidenceRecord>(entityTypes, "CognitiveMemory_ProcedureSimulationEvidence");

        foreach (var expectation in CognitiveMemoryEfGuardrails.ProceduralIndexExpectations)
        {
            var entityType = Assert.Single(entityTypes, entityType => entityType.ClrType == expectation.EntityType);
            Assert.True(
                CognitiveMemoryEfGuardrails.HasExpectedIndex(entityType, expectation),
                $"Missing expected procedural index on {expectation.EntityType.Name}: {string.Join(", ", expectation.PropertyNames)}.");
        }

        var registry = new CognitiveMemoryScoreSpaceRegistry();
        var procedureMaturity = await registry.GetDefinitionAsync(
            CognitiveMemoryScoreSpaceKind.ProcedureMaturity,
            CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion);
        var simulationRisk = await registry.GetDefinitionAsync(
            CognitiveMemoryScoreSpaceKind.SimulationRisk,
            CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion);

        Assert.Contains(procedureMaturity.Dimensions, dimension => dimension.Kind == CognitiveMemoryScoreDimensionKind.ProcedureMaturity && dimension.Required);
        Assert.Contains(procedureMaturity.Dimensions, dimension => dimension.Kind == CognitiveMemoryScoreDimensionKind.EvidenceStrength && dimension.Required);
        Assert.Contains(simulationRisk.Dimensions, dimension => dimension.Kind == CognitiveMemoryScoreDimensionKind.RiskImpact && dimension.Required);
        Assert.Contains(simulationRisk.Dimensions, dimension => dimension.Kind == CognitiveMemoryScoreDimensionKind.SourceSufficiency && dimension.Required);
        AssertEnumStateFieldsAreNotPersistedAsStrings(entityTypes);
    }

    [Fact]
    public async Task ProcedureServices_PersistSkillBindingSimulationAndNoTruthMutation()
    {
        await using var fixture = await CreateFixtureAsync();
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var evidenceAnchorId = await SeedEvidenceAnchorAsync(fixture, projectId);
        var episodeId = await SeedEpisodeAsync(fixture, projectId);
        var service = CreateService(fixture);

        var skill = await service.ProposeSkillAsync(new CognitiveMemoryProcedureSkillProposalRequest(
            projectId,
            "Docker deployment procedure",
            "Deploy a Docker-backed service with validation.",
            Policy(projectId),
            Steps:
            [
                new CognitiveMemoryProcedureStepDraft(
                    "deploy",
                    1,
                    "Deploy Docker image.",
                    "Tagged image.",
                    "Running service.",
                    "Service starts.",
                    "Roll back image.",
                    [evidenceAnchorId],
                    ToolBindingKey: "docker")
            ],
            FailureModes:
            [
                new CognitiveMemoryProcedureFailureModeDraft(
                    "wrong-context",
                    "Deployment uses wrong Docker context.",
                    "Target host differs from selected project.",
                    "Context selection failed.",
                    "Switch to approved context.",
                    "Stop deployment and restore previous target.",
                    RelatedEpisodeIds: [episodeId])
            ],
            ValidationEvidence:
            [
                new CognitiveMemoryProcedureValidationEvidenceDraft(
                    CognitiveMemoryProcedureValidationEvidenceRole.HumanReview,
                    evidenceAnchorId,
                    "Human-reviewed run evidence.",
                    episodeId)
            ],
            Preconditions: ["Target Docker context is approved."],
            Postconditions: ["Service is healthy."],
            RiskLevel: CognitiveMemoryRiskLevel.Low,
            InitialMaturity: CognitiveMemoryProcedureSkillMaturity.Automatable,
            ValidationState: CognitiveMemoryValidationState.Approved,
            AccessLevel: CognitiveMemoryAccessLevel.Project,
            RequiredToolKeys: ["docker"],
            InputSchemaJson: """{"type":"object"}""",
            OutputSchemaJson: """{"type":"object"}"""));
        var binding = await service.RequestAutomationBindingAsync(new CognitiveMemoryProcedureAutomationBindingRequest(
            new CognitiveMemoryProcedureSkillId(skill.Id),
            CognitiveMemoryProcedureAutomationBindingKind.WorkflowExecutorGuidance,
            "workflow:docker-deploy",
            Policy(projectId),
            HumanReviewApproved: true));
        var simulation = await service.SimulateAsync(new CognitiveMemoryProcedureSimulationRequest(
            projectId,
            CognitiveMemoryProcedureSimulationOutputKind.CandidatePlan,
            "Speculatively compare a safer Docker deployment sequence.",
            Policy(projectId),
            [new CognitiveMemoryProcedureSkillId(skill.Id)],
            [evidenceAnchorId],
            ["Run a regression deployment probe.", "Human-review before automation."],
            CognitiveMemoryRiskLevel.Low));

        fixture.DbContext.ChangeTracker.Clear();
        var persistedSkill = await fixture.DbContext.Set<CognitiveMemoryProcedureSkillRecord>().SingleAsync();
        var persistedBinding = await fixture.DbContext.Set<CognitiveMemoryProcedureAutomationBindingRecord>().SingleAsync();
        var persistedSimulation = await fixture.DbContext.Set<CognitiveMemoryProcedureSimulationRecord>().SingleAsync();
        var scoreComponents = await fixture.DbContext.Set<CognitiveMemoryScoreComponentRecord>()
            .Where(component =>
                component.SpaceKind == CognitiveMemoryScoreSpaceKind.ProcedureMaturity ||
                component.SpaceKind == CognitiveMemoryScoreSpaceKind.SimulationRisk)
            .ToListAsync();

        Assert.Equal(skill.Id, persistedSkill.Id);
        Assert.Equal(CognitiveMemoryProcedureSkillMaturity.Automatable, persistedSkill.Maturity);
        Assert.Equal(CognitiveMemoryProcedureAutomationBindingState.Bound, persistedBinding.State);
        Assert.Equal(binding.Id, persistedBinding.Id);
        Assert.True(persistedSimulation.IsSpeculative);
        Assert.Equal("speculative-hypothesis", persistedSimulation.SpeculationLabel);
        Assert.Equal(simulation.Id, persistedSimulation.Id);
        Assert.Contains(scoreComponents, component => component.DimensionKind == CognitiveMemoryScoreDimensionKind.ProcedureMaturity);
        Assert.Contains(scoreComponents, component => component.DimensionKind == CognitiveMemoryScoreDimensionKind.RiskImpact);
        Assert.Equal(0, await fixture.DbContext.Set<CognitiveMemoryMutationCommandRecord>().CountAsync());
        Assert.Equal(0, await fixture.DbContext.Set<CognitiveMemoryRecord>().CountAsync());
    }

    private static CognitiveMemoryProcedureSkillService CreateService(ProceduralFixture fixture)
        => new(
            fixture.Factory,
            new CognitiveMemoryScoreGeometryDriver(new CognitiveMemoryScoreSpaceRegistry()),
            fixture.Clock,
            NullLogger<CognitiveMemoryProcedureSkillService>.Instance);

    private static async Task<CognitiveMemoryEvidenceAnchorId> SeedEvidenceAnchorAsync(
        ProceduralFixture fixture,
        Guid projectId)
    {
        var anchor = new CognitiveMemoryEvidenceAnchorRecord
        {
            ProjectId = projectId,
            AnchorKind = CognitiveMemoryEvidenceAnchorKind.TextSpan,
            SourceSystem = "integration-test",
            Locator = "/integration/procedure",
            StructuredPath = "$.procedure",
            TextStart = 0,
            TextEnd = 12,
            QuoteHash = CognitiveMemoryHash.FromUtf8("procedure quote").Value,
            TrustLevel = CognitiveMemorySourceTrustLevel.RuntimeSource,
            RedactionState = CognitiveMemoryRedactionState.Safe,
            SourceHashAlgorithm = CognitiveMemoryHashAlgorithm.Sha256,
            SourceHash = CognitiveMemoryHash.FromUtf8("source hash").Value,
            ObservedAtUtc = fixture.Clock.GetUtcNow(),
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        fixture.DbContext.Add(anchor);
        await fixture.DbContext.SaveChangesAsync();
        return new CognitiveMemoryEvidenceAnchorId(anchor.Id);
    }

    private static async Task<CognitiveMemoryTemporalEpisodeId> SeedEpisodeAsync(
        ProceduralFixture fixture,
        Guid projectId)
    {
        var episode = new CognitiveMemoryTemporalEpisodeRecord
        {
            ProjectId = projectId,
            EpisodeKind = CognitiveMemoryTemporalEpisodeKind.Deployment,
            Goal = "Deploy Docker service.",
            ExpectedOutcome = "Deployment succeeds.",
            ActualOutcome = "Deployment succeeded.",
            OutcomeSummary = "Deployment succeeded.",
            StartedAtUtc = fixture.Clock.GetUtcNow(),
            AlgorithmVersion = "integration-test",
            CreatedAtUtc = fixture.Clock.GetUtcNow(),
            UpdatedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };
        fixture.DbContext.Add(episode);
        await fixture.DbContext.SaveChangesAsync();
        return new CognitiveMemoryTemporalEpisodeId(episode.Id);
    }

    private static CognitiveMemoryPolicyContext Policy(Guid projectId)
        => new(
            projectId,
            "agent:test",
            CognitiveMemoryAccessLevel.Project,
            new CognitiveMemoryPolicyProfileId("policy:test"),
            CognitiveMemoryRiskLevel.Low,
            AllowRestrictedContent: false);

    private static async Task<ProceduralFixture> CreateFixtureAsync()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        var database = PostgresTestDatabaseLease.Create("cognitivememoryproceduralpersistencemodeltests");

        var options = database.CreateAppDbContextOptions();
        var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return new ProceduralFixture(database, new TestDbContextFactory(options), dbContext, new FixedClock());
    }

    private static void AssertEntityTable<TEntity>(IReadOnlyList<IEntityType> entityTypes, string tableName)
    {
        var entityType = Assert.Single(entityTypes, entityType => entityType.ClrType == typeof(TEntity));
        Assert.Equal(tableName, entityType.GetTableName());
    }

    private static void AssertEnumStateFieldsAreNotPersistedAsStrings(IReadOnlyList<IEntityType> entityTypes)
    {
        var stateProperties = new Dictionary<Type, string[]>
        {
            [typeof(CognitiveMemoryProcedureSkillRecord)] =
            [
                nameof(CognitiveMemoryProcedureSkillRecord.Maturity),
                nameof(CognitiveMemoryProcedureSkillRecord.RiskLevel),
                nameof(CognitiveMemoryProcedureSkillRecord.ValidationState),
                nameof(CognitiveMemoryProcedureSkillRecord.AccessLevel)
            ],
            [typeof(CognitiveMemoryProcedureValidationEvidenceRecord)] =
            [
                nameof(CognitiveMemoryProcedureValidationEvidenceRecord.EvidenceRole)
            ],
            [typeof(CognitiveMemoryProcedureAutomationBindingRecord)] =
            [
                nameof(CognitiveMemoryProcedureAutomationBindingRecord.BindingKind),
                nameof(CognitiveMemoryProcedureAutomationBindingRecord.State)
            ],
            [typeof(CognitiveMemoryProcedureSimulationRecord)] =
            [
                nameof(CognitiveMemoryProcedureSimulationRecord.OutputKind),
                nameof(CognitiveMemoryProcedureSimulationRecord.Status),
                nameof(CognitiveMemoryProcedureSimulationRecord.RiskLevel)
            ]
        };

        foreach (var (entityClrType, propertyNames) in stateProperties)
        {
            var entityType = Assert.Single(entityTypes, entityType => entityType.ClrType == entityClrType);
            foreach (var propertyName in propertyNames)
            {
                var property = Assert.IsAssignableFrom<IProperty>(entityType.FindProperty(propertyName));
                Assert.True(property.ClrType.IsEnum || Nullable.GetUnderlyingType(property.ClrType)?.IsEnum == true, $"{entityClrType.Name}.{propertyName} should remain a typed enum.");
                Assert.NotEqual(typeof(string), property.ClrType);
            }
        }
    }

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

    private sealed class ProceduralFixture(
        PostgresTestDatabaseLease database,
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
            await database.DisposeAsync();
        }
    }
}
