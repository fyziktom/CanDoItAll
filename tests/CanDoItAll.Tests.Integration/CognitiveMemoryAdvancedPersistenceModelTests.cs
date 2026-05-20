using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CanDoItAll.Tests.Integration;

public sealed class CognitiveMemoryAdvancedPersistenceModelTests
{
    [Fact]
    public async Task AdvancedPersistenceModel_IndexesGovernanceLearningAndDistributedComputeState()
    {
        await using var fixture = await CreateFixtureAsync();
        var entityTypes = fixture.DbContext.Model.GetEntityTypes().ToList();

        AssertEntityTable<CognitiveMemoryProbeSessionRecord>(entityTypes, "CognitiveMemory_ProbeSessions");
        AssertEntityTable<CognitiveMemoryProbeTurnRecord>(entityTypes, "CognitiveMemory_ProbeTurns");
        AssertEntityTable<CognitiveMemoryProbeFeedbackRecord>(entityTypes, "CognitiveMemory_ProbeFeedback");
        AssertEntityTable<CognitiveMemoryCuratorSessionRecord>(entityTypes, "CognitiveMemory_CuratorSessions");
        AssertEntityTable<CognitiveMemoryCuratorTurnRecord>(entityTypes, "CognitiveMemory_CuratorTurns");
        AssertEntityTable<CognitiveMemoryCuratorCapturedImprovementRecord>(entityTypes, "CognitiveMemory_CuratorCapturedImprovements");
        AssertEntityTable<CognitiveMemoryProbeRegressionTestCaseRecord>(entityTypes, "CognitiveMemory_ProbeRegressionTestCases");
        AssertEntityTable<CognitiveMemorySelfModelProfileRecord>(entityTypes, "CognitiveMemory_SelfModelProfiles");
        AssertEntityTable<CognitiveMemoryDomainCompetenceProfileRecord>(entityTypes, "CognitiveMemory_DomainCompetenceProfiles");
        AssertEntityTable<CognitiveMemoryCalibrationAggregateRecord>(entityTypes, "CognitiveMemory_CalibrationAggregates");
        AssertEntityTable<CognitiveMemorySelfRegulationAssessmentRecord>(entityTypes, "CognitiveMemory_SelfRegulationAssessments");
        AssertEntityTable<CognitiveMemoryAnswerPostureDecisionRecord>(entityTypes, "CognitiveMemory_AnswerPostureDecisions");
        AssertEntityTable<CognitiveMemoryProfessorReviewRecord>(entityTypes, "CognitiveMemory_ProfessorReviews");
        AssertEntityTable<CognitiveMemoryAnswerGateDecisionRecord>(entityTypes, "CognitiveMemory_AnswerGateDecisions");
        AssertEntityTable<CognitiveMemoryKnowledgeRegionRecord>(entityTypes, "CognitiveMemory_KnowledgeRegions");
        AssertEntityTable<CognitiveMemoryLearningProposalRecord>(entityTypes, "CognitiveMemory_LearningProposals");
        AssertEntityTable<CognitiveMemoryCrossProjectPromotionCandidateRecord>(entityTypes, "CognitiveMemory_CrossProjectPromotionCandidates");
        AssertEntityTable<CognitiveMemoryDistributedWorkerRecord>(entityTypes, "CognitiveMemory_DistributedWorkers");
        AssertEntityTable<CognitiveMemoryDistributedJobRecord>(entityTypes, "CognitiveMemory_DistributedJobs");
        AssertEntityTable<CognitiveMemoryDistributedWorkerResultRecord>(entityTypes, "CognitiveMemory_DistributedWorkerResults");

        foreach (var expectation in CognitiveMemoryEfGuardrails.AdvancedIndexExpectations)
        {
            var entityType = Assert.Single(entityTypes, entityType => entityType.ClrType == expectation.EntityType);
            Assert.True(
                CognitiveMemoryEfGuardrails.HasExpectedIndex(entityType, expectation),
                $"Missing expected advanced index on {expectation.EntityType.Name}: {string.Join(", ", expectation.PropertyNames)}.");
        }

        AssertEnumStateFieldsAreNotPersistedAsStrings(entityTypes);
    }

    private static async Task<AdvancedFixture> CreateFixtureAsync()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return new AdvancedFixture(connection, dbContext);
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
            [typeof(CognitiveMemoryProbeSessionRecord)] =
            [
                nameof(CognitiveMemoryProbeSessionRecord.Status),
                nameof(CognitiveMemoryProbeSessionRecord.RecallMode)
            ],
            [typeof(CognitiveMemoryProbeFeedbackRecord)] =
            [
                nameof(CognitiveMemoryProbeFeedbackRecord.Action),
                nameof(CognitiveMemoryProbeFeedbackRecord.CalibrationOutcome),
                nameof(CognitiveMemoryProbeFeedbackRecord.RiskLevel)
            ],
            [typeof(CognitiveMemoryCuratorSessionRecord)] =
            [
                nameof(CognitiveMemoryCuratorSessionRecord.Status),
                nameof(CognitiveMemoryCuratorSessionRecord.RuntimeMode),
                nameof(CognitiveMemoryCuratorSessionRecord.ConversationDepth)
            ],
            [typeof(CognitiveMemoryCuratorTurnRecord)] =
            [
                nameof(CognitiveMemoryCuratorTurnRecord.RuntimeMode),
                nameof(CognitiveMemoryCuratorTurnRecord.ConversationDepth)
            ],
            [typeof(CognitiveMemoryCuratorCapturedImprovementRecord)] =
            [
                nameof(CognitiveMemoryCuratorCapturedImprovementRecord.CaptureKind),
                nameof(CognitiveMemoryCuratorCapturedImprovementRecord.ConversationDepth),
                nameof(CognitiveMemoryCuratorCapturedImprovementRecord.Status)
            ],
            [typeof(CognitiveMemorySelfRegulationAssessmentRecord)] =
            [
                nameof(CognitiveMemorySelfRegulationAssessmentRecord.State)
            ],
            [typeof(CognitiveMemoryAnswerGateDecisionRecord)] =
            [
                nameof(CognitiveMemoryAnswerGateDecisionRecord.DecisionKind),
                nameof(CognitiveMemoryAnswerGateDecisionRecord.DecisionBucket)
            ],
            [typeof(CognitiveMemoryProfessorReviewRecord)] =
            [
                nameof(CognitiveMemoryProfessorReviewRecord.ReviewMode),
                nameof(CognitiveMemoryProfessorReviewRecord.Status),
                nameof(CognitiveMemoryProfessorReviewRecord.RecommendedPosture)
            ],
            [typeof(CognitiveMemoryLearningProposalRecord)] =
            [
                nameof(CognitiveMemoryLearningProposalRecord.Status),
                nameof(CognitiveMemoryLearningProposalRecord.NeedBucket)
            ],
            [typeof(CognitiveMemoryDistributedJobRecord)] =
            [
                nameof(CognitiveMemoryDistributedJobRecord.JobKind),
                nameof(CognitiveMemoryDistributedJobRecord.State),
                nameof(CognitiveMemoryDistributedJobRecord.InputHashAlgorithm)
            ],
            [typeof(CognitiveMemoryDistributedWorkerResultRecord)] =
            [
                nameof(CognitiveMemoryDistributedWorkerResultRecord.Status)
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

    private sealed class AdvancedFixture(
        SqliteConnection connection,
        AppDbContext dbContext) : IAsyncDisposable
    {
        public AppDbContext DbContext { get; } = dbContext;

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
