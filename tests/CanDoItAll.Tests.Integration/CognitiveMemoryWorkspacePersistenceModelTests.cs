using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CognitiveMemory;
using CanDoItAll.SharedKernel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace CanDoItAll.Tests.Integration;

public sealed class CognitiveMemoryWorkspacePersistenceModelTests
{
    [Fact]
    public async Task WorkspacePersistenceModel_IndexesWorkspaceAttentionAndTraceHooks()
    {
        await using var fixture = await CreateFixtureAsync();
        var entityTypes = fixture.DbContext.Model.GetEntityTypes().ToList();

        AssertEntityTable<CognitiveMemoryWorkspaceFrameRecord>(entityTypes, "CognitiveMemory_WorkspaceFrames");
        AssertEntityTable<CognitiveMemoryWorkspaceGoalRecord>(entityTypes, "CognitiveMemory_WorkspaceGoals");
        AssertEntityTable<CognitiveMemoryWorkingMemorySlotRecord>(entityTypes, "CognitiveMemory_WorkspaceFocusSlots");
        AssertEntityTable<CognitiveMemoryWorkspaceSlotEvidenceAnchorRecord>(entityTypes, "CognitiveMemory_WorkspaceSlotEvidenceAnchors");
        AssertEntityTable<CognitiveMemoryWorkspaceOpenQuestionRecord>(entityTypes, "CognitiveMemory_WorkspaceOpenQuestions");
        AssertEntityTable<CognitiveMemoryInhibitedCandidateRecord>(entityTypes, "CognitiveMemory_WorkspaceInhibitedCandidates");
        AssertEntityTable<CognitiveMemoryAttentionDecisionRecord>(entityTypes, "CognitiveMemory_AttentionDecisions");

        foreach (var expectation in CognitiveMemoryEfGuardrails.WorkspaceIndexExpectations)
        {
            var entityType = Assert.Single(entityTypes, entityType => entityType.ClrType == expectation.EntityType);
            Assert.True(
                CognitiveMemoryEfGuardrails.HasExpectedIndex(entityType, expectation),
                $"Missing expected workspace index on {expectation.EntityType.Name}: {string.Join(", ", expectation.PropertyNames)}.");
        }

        var recallTraceType = Assert.Single(entityTypes, entityType => entityType.ClrType == typeof(CognitiveMemoryRecallTraceRecord));
        Assert.NotNull(recallTraceType.FindProperty(nameof(CognitiveMemoryRecallTraceRecord.WorkspaceFrameId)));
        Assert.NotNull(recallTraceType.FindProperty(nameof(CognitiveMemoryRecallTraceRecord.AttentionDecisionId)));
        Assert.NotNull(recallTraceType.FindProperty(nameof(CognitiveMemoryRecallTraceRecord.InhibitedCandidateCount)));
        AssertEnumStateFieldsAreNotPersistedAsStrings(entityTypes);
    }

    [Fact]
    public async Task AttentionDecisionAndRecallTrace_CanReferenceWorkspaceWithoutCreatingSourceTruth()
    {
        await using var fixture = await CreateFixtureAsync();
        var workspaceService = new CognitiveMemoryWorkspaceService(fixture.Factory, fixture.Clock);
        var router = new CognitiveMemoryAttentionRouter(
            fixture.Factory,
            new CognitiveMemoryScoreGeometryDriver(new CognitiveMemoryScoreSpaceRegistry()),
            fixture.Clock);
        var projectId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var workspace = await workspaceService.GetOrCreateAsync(new CognitiveMemoryWorkspaceOpenRequest(
            new CognitiveMemoryWorkspaceScope(projectId, CognitiveMemoryWorkspaceFrameKind.WorkflowRun, workflowRunId: Guid.Parse("10000000-0000-0000-0000-000000000001")),
            new CognitiveMemoryWorkspaceContextBudget(2048, 4, 8),
            ExpiresAtUtc: fixture.Clock.GetUtcNow().AddHours(1),
            GoalStack:
            [
                new CognitiveMemoryWorkspaceGoalDraft(new CognitiveMemoryWorkspaceGoalKey("answer.from.workspace"), "Answer only when source-backed workspace focus is enough.", 0)
            ]));

        var decision = await router.RouteAsync(new CognitiveMemoryAttentionRoutingRequest(
            projectId,
            new CognitiveMemoryWorkspaceFrameId(workspace.Frame.Id),
            "Can the active workspace answer this?",
            new CognitiveMemoryAttentionSignalSet(0.9, 0.1, RiskImpact: 0.1, AvailableWorkspaceEvidence: 0.9)));

        fixture.DbContext.Add(new CognitiveMemoryRecallTraceRecord
        {
            ProjectId = projectId,
            OperationMode = CognitiveMemoryOperationMode.Recall,
            RequestedByActorId = "agent:test",
            PolicyProfileId = "policy:test",
            WorkspaceFrameId = workspace.Frame.Id,
            AttentionDecisionId = decision.Id.Value,
            SelfRegulationAssessmentId = Guid.Parse("20000000-0000-0000-0000-000000000001"),
            AnswerPostureDecisionId = Guid.Parse("20000000-0000-0000-0000-000000000002"),
            RequestHash = CognitiveMemoryHash.FromUtf8("recall trace").Value,
            AlgorithmVersion = "workspace-attention-v1",
            Outcome = CognitiveMemoryRunStatus.Succeeded,
            IncludedRecordCount = 1,
            ExcludedRecordCount = 1,
            SelectedClaimCount = 1,
            SelectedEvidenceAnchorCount = 1,
            InhibitedCandidateCount = 1,
            LimitingBudget = CognitiveMemoryBudgetLimit.TokenCount,
            TraceJson = "{}",
            StartedAtUtc = fixture.Clock.GetUtcNow(),
            CompletedAtUtc = fixture.Clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        });
        await fixture.DbContext.SaveChangesAsync();

        var persistedDecision = await fixture.DbContext.Set<CognitiveMemoryAttentionDecisionRecord>()
            .SingleAsync(item => item.Id == decision.Id.Value);
        var persistedFrame = await fixture.DbContext.Set<CognitiveMemoryWorkspaceFrameRecord>()
            .SingleAsync(item => item.Id == workspace.Frame.Id);
        var trace = await fixture.DbContext.Set<CognitiveMemoryRecallTraceRecord>()
            .SingleAsync(item => item.AttentionDecisionId == decision.Id.Value);

        Assert.Equal(CognitiveMemoryAttentionDecisionKind.AnswerFromWorkspace, persistedDecision.DecisionKind);
        Assert.Equal(decision.Id.Value, persistedFrame.LastAttentionDecisionId);
        Assert.Equal(workspace.Frame.Id, trace.WorkspaceFrameId);
        Assert.Equal(CognitiveMemoryBudgetLimit.TokenCount, trace.LimitingBudget);
        Assert.Equal(1, await fixture.DbContext.Set<CognitiveMemoryScoreEvaluationTraceRecord>().CountAsync());
        Assert.Equal(0, await fixture.DbContext.Set<CognitiveMemorySourceItemRecord>().CountAsync());
        Assert.Equal(0, await fixture.DbContext.Set<CognitiveMemoryMutationCommandRecord>().CountAsync());
    }

    private static async Task<WorkspaceFixture> CreateFixtureAsync()
    {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        var dbContext = new AppDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();
        return new WorkspaceFixture(connection, new TestDbContextFactory(options), dbContext, new FixedClock());
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
            [typeof(CognitiveMemoryWorkspaceFrameRecord)] =
            [
                nameof(CognitiveMemoryWorkspaceFrameRecord.FrameKind),
                nameof(CognitiveMemoryWorkspaceFrameRecord.Status),
                nameof(CognitiveMemoryWorkspaceFrameRecord.CognitiveLoadBucket),
                nameof(CognitiveMemoryWorkspaceFrameRecord.LimitingBudget)
            ],
            [typeof(CognitiveMemoryWorkingMemorySlotRecord)] =
            [
                nameof(CognitiveMemoryWorkingMemorySlotRecord.SlotKind),
                nameof(CognitiveMemoryWorkingMemorySlotRecord.InclusionReasonKind),
                nameof(CognitiveMemoryWorkingMemorySlotRecord.SourceSufficiency)
            ],
            [typeof(CognitiveMemoryInhibitedCandidateRecord)] =
            [
                nameof(CognitiveMemoryInhibitedCandidateRecord.CandidateKind),
                nameof(CognitiveMemoryInhibitedCandidateRecord.ReasonKind),
                nameof(CognitiveMemoryInhibitedCandidateRecord.InhibitionBucket)
            ],
            [typeof(CognitiveMemoryAttentionDecisionRecord)] =
            [
                nameof(CognitiveMemoryAttentionDecisionRecord.DecisionKind),
                nameof(CognitiveMemoryAttentionDecisionRecord.ReasonKind),
                nameof(CognitiveMemoryAttentionDecisionRecord.RoutingBucket)
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

    private sealed class WorkspaceFixture(
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
