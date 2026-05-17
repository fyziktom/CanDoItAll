using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCognitiveMemoryAdvancedCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CognitiveMemory_AnswerGateDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RecallTraceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SelfRegulationAssessmentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AnswerPostureDecisionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProfessorReviewId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DecisionKind = table.Column<int>(type: "INTEGER", nullable: false),
                    ScoreEvaluationTraceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DecisionBucket = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayConfidenceProjection = table.Column<double>(type: "REAL", nullable: true),
                    WarningsJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    RequiredOperationsJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    DraftAnswerSummary = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_AnswerGateDecisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_AnswerPostureDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SelfRegulationAssessmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Posture = table.Column<int>(type: "INTEGER", nullable: false),
                    PostureScoreEvaluationTraceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PostureBucket = table.Column<int>(type: "INTEGER", nullable: false),
                    RequiredOperationsJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    WarningsJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_AnswerPostureDecisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_CalibrationAggregates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DomainKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    TaskTypeKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    ModelProfileId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    RiskKey = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    FeaturePatternKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    ProfileVersion = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ObservationCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ExpectedCalibrationError = table.Column<double>(type: "REAL", nullable: false),
                    BrierScore = table.Column<double>(type: "REAL", nullable: false),
                    SignedBias = table.Column<double>(type: "REAL", nullable: false),
                    OverconfidenceRate = table.Column<double>(type: "REAL", nullable: false),
                    UnderconfidenceRate = table.Column<double>(type: "REAL", nullable: false),
                    AbstentionQualityRate = table.Column<double>(type: "REAL", nullable: false),
                    WrongScopeRate = table.Column<double>(type: "REAL", nullable: false),
                    SourceInsufficientRate = table.Column<double>(type: "REAL", nullable: false),
                    CalibrationScoreEvaluationTraceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_CalibrationAggregates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_CalibrationBins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CalibrationAggregateId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    BinIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    LowerBound = table.Column<double>(type: "REAL", nullable: false),
                    UpperBound = table.Column<double>(type: "REAL", nullable: false),
                    ObservationCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AveragePredictedConfidence = table.Column<double>(type: "REAL", nullable: false),
                    ActualAccuracy = table.Column<double>(type: "REAL", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_CalibrationBins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_CalibrationEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DomainKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    TaskTypeKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    ModelProfileId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    RiskKey = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    FeaturePatternKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    ProfileVersion = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    PredictedConfidence = table.Column<double>(type: "REAL", nullable: false),
                    ActualCorrect = table.Column<bool>(type: "INTEGER", nullable: false),
                    OutcomeKind = table.Column<int>(type: "INTEGER", nullable: false),
                    ProbeTurnId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RecallTraceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReviewItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProfessorReviewId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_CalibrationEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ConfidenceReinforcements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SelfRegulationAssessmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReinforcementKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    EvidenceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ConfidenceReinforcements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_CoverageMaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    KnowledgeRegionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CoverageState = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceEvidenceCount = table.Column<int>(type: "INTEGER", nullable: false),
                    RecallFailureCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ProbeFailureCount = table.Column<int>(type: "INTEGER", nullable: false),
                    AbstentionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    RefreshedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_CoverageMaps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_CrossProjectPromotionCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceMemoryRecordId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    PromotionScoreEvaluationTraceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PromotionBucket = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestedByActorId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    ReviewItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DecidedByActorId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    DecisionNotes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_CrossProjectPromotionCandidates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_DistributedJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    JobKind = table.Column<int>(type: "INTEGER", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceScopeKey = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    InputPayloadJson = table.Column<string>(type: "TEXT", maxLength: 16000, nullable: false),
                    InputHashAlgorithm = table.Column<int>(type: "INTEGER", nullable: false),
                    InputHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ExpectedOutputSchema = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    PolicyProfileId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    LeaseToken = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    LeasedWorkerId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_DistributedJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_DistributedWorkerResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DistributedJobId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    WorkerId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    InputHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    OutputHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    OutputSchema = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    OutputPayloadJson = table.Column<string>(type: "TEXT", maxLength: 16000, nullable: false),
                    RejectionReason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_DistributedWorkerResults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_DistributedWorkers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    WorkerId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    MachineName = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    CapabilitiesJson = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_DistributedWorkers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_DomainCompetenceProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SelfModelProfileId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DomainKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    TaskTypeKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    ModelProfileId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    ProfileVersion = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CompetenceLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    CompetenceScoreEvaluationTraceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EvidenceCount = table.Column<int>(type: "INTEGER", nullable: false),
                    EvidenceRefsJson = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_DomainCompetenceProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_HumilityTriggers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SelfRegulationAssessmentId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TriggerKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    ScoreEvaluationTraceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_HumilityTriggers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_KnowledgeGaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    KnowledgeRegionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GapKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    EvidenceRefsJson = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_KnowledgeGaps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_KnowledgeRegions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RegionKind = table.Column<int>(type: "INTEGER", nullable: false),
                    RegionKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_KnowledgeRegions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_KnownFailurePatterns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SelfModelProfileId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PatternKind = table.Column<int>(type: "INTEGER", nullable: false),
                    DomainKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    TaskTypeKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    TriggerSummary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    Mitigation = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    RequiresReview = table.Column<bool>(type: "INTEGER", nullable: false),
                    PatternScoreEvaluationTraceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EvidenceRefsJson = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_KnownFailurePatterns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_LearningOutcomes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LearningTaskId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OutcomeKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    SourceRefsJson = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    ReviewItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MutationCommandId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_LearningOutcomes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_LearningProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    KnowledgeGapId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    Explanation = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    EvidenceRefsJson = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    Risks = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    AcceptanceCriteria = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    NeedScoreEvaluationTraceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    NeedBucket = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayPriorityProjection = table.Column<double>(type: "REAL", nullable: true),
                    DecidedByActorId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    DecisionNotes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_LearningProposals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_LearningTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LearningProposalId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    WorkflowExecutorKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ApprovalActorId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_LearningTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProbeFeedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProbeTurnId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProbeSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Action = table.Column<int>(type: "INTEGER", nullable: false),
                    CalibrationOutcome = table.Column<int>(type: "INTEGER", nullable: false),
                    RiskLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CorrectionText = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    ReviewItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RegressionTestCaseId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CalibrationEventId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProbeFeedback", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProbeFindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProbeTurnId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FindingKind = table.Column<int>(type: "INTEGER", nullable: false),
                    RiskLevel = table.Column<int>(type: "INTEGER", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    ReviewItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProbeFindings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProbeRegressionRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    RegressionTestCaseId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Outcome = table.Column<int>(type: "INTEGER", nullable: false),
                    RecallTraceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FailureReason = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    EvaluatorProfileVersion = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProbeRegressionRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProbeRegressionTestCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProbeTurnId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Question = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    ExpectedEvidenceText = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    ExpectedContextKey = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    AccessPolicyProfileId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    EvaluatorProfileVersion = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProbeRegressionTestCases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProbeSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RecallMode = table.Column<int>(type: "INTEGER", nullable: false),
                    WorkspaceFrameId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Title = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    ActorId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    PolicyProfileId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    TurnCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProbeSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProbeTurns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProbeSessionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    Intent = table.Column<int>(type: "INTEGER", nullable: false),
                    Question = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    AnswerSummary = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    RecallTraceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ContextPackId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SelfRegulationAssessmentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AnswerPostureDecisionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AnswerGateDecisionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProbeScoreEvaluationTraceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProbeScoreBucket = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayProbeScore = table.Column<double>(type: "REAL", nullable: true),
                    WarningCount = table.Column<int>(type: "INTEGER", nullable: false),
                    WarningsJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProbeTurns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProfessorReviewActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProfessorReviewId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SuggestionKind = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedReviewItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedLearningProposalId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedRegressionTestCaseId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProfessorReviewActions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProfessorReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReviewMode = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    RequestedByActorId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    ModelProfileId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    PromptProfileVersion = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    PolicyProfileId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    SelfRegulationAssessmentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AnswerPostureDecisionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RoutingScoreEvaluationTraceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    InputSummary = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    ContextSummary = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Critique = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    MissingEvidence = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    RecommendedPosture = table.Column<int>(type: "INTEGER", nullable: false),
                    OutputHashAlgorithm = table.Column<int>(type: "INTEGER", nullable: false),
                    OutputHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    RequiresHumanReview = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProfessorReviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SelfModelProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ModelProfileId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    RoleKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    ProfileVersion = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    OperatingPrinciples = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    AllowedTaskCategoriesJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    RestrictedTaskCategoriesJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SelfModelProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SelfModelUpdateProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ModelProfileId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    DomainKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    ProposedChange = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    EvidenceRefsJson = table.Column<string>(type: "TEXT", maxLength: 8000, nullable: false),
                    RequestedByActorId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SelfModelUpdateProposals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SelfRegulationAssessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SelfModelProfileId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DomainCompetenceProfileId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CalibrationAggregateId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RecallTraceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    WorkspaceFrameId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AttentionDecisionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ActorId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    ModelProfileId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    DomainKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    TaskTypeKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    AssessmentScoreEvaluationTraceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssessmentBucket = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayAssessmentScore = table.Column<double>(type: "REAL", nullable: true),
                    WarningsJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    RequiredOperationsJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SelfRegulationAssessments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SelfRegulationPolicyProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SelfModelProfileId = table.Column<Guid>(type: "TEXT", nullable: false),
                    PolicyKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    ProfileVersion = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    AllowedPosturesJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    RequiredOperationsJson = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    ReviewThreshold = table.Column<double>(type: "REAL", nullable: false),
                    AbstentionThreshold = table.Column<double>(type: "REAL", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SelfRegulationPolicyProfiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_AnswerGateDecisions_ProjectId_DecisionKind_CreatedAtUtc",
                table: "CognitiveMemory_AnswerGateDecisions",
                columns: new[] { "ProjectId", "DecisionKind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_AnswerGateDecisions_RecallTraceId",
                table: "CognitiveMemory_AnswerGateDecisions",
                column: "RecallTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_AnswerGateDecisions_ScoreEvaluationTraceId",
                table: "CognitiveMemory_AnswerGateDecisions",
                column: "ScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_AnswerGateDecisions_SelfRegulationAssessmentId",
                table: "CognitiveMemory_AnswerGateDecisions",
                column: "SelfRegulationAssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_AnswerPostureDecisions_PostureScoreEvaluationTraceId",
                table: "CognitiveMemory_AnswerPostureDecisions",
                column: "PostureScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_AnswerPostureDecisions_ProjectId_Posture_CreatedAtUtc",
                table: "CognitiveMemory_AnswerPostureDecisions",
                columns: new[] { "ProjectId", "Posture", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_AnswerPostureDecisions_SelfRegulationAssessmentId",
                table: "CognitiveMemory_AnswerPostureDecisions",
                column: "SelfRegulationAssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CalibrationAggregates_CalibrationScoreEvaluationTraceId",
                table: "CognitiveMemory_CalibrationAggregates",
                column: "CalibrationScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CalibrationAggregates_ProjectId_DomainKey_TaskTypeKey_ModelProfileId_RiskKey_FeaturePatternKey_ProfileVersion",
                table: "CognitiveMemory_CalibrationAggregates",
                columns: new[] { "ProjectId", "DomainKey", "TaskTypeKey", "ModelProfileId", "RiskKey", "FeaturePatternKey", "ProfileVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CalibrationBins_CalibrationAggregateId_BinIndex",
                table: "CognitiveMemory_CalibrationBins",
                columns: new[] { "CalibrationAggregateId", "BinIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CalibrationEvents_ProjectId_DomainKey_TaskTypeKey_ModelProfileId_ObservedAtUtc",
                table: "CognitiveMemory_CalibrationEvents",
                columns: new[] { "ProjectId", "DomainKey", "TaskTypeKey", "ModelProfileId", "ObservedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CalibrationEvents_ProjectId_OutcomeKind_ObservedAtUtc",
                table: "CognitiveMemory_CalibrationEvents",
                columns: new[] { "ProjectId", "OutcomeKind", "ObservedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConfidenceReinforcements_SelfRegulationAssessmentId_ReinforcementKind",
                table: "CognitiveMemory_ConfidenceReinforcements",
                columns: new[] { "SelfRegulationAssessmentId", "ReinforcementKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CoverageMaps_ProjectId_CoverageState_RefreshedAtUtc",
                table: "CognitiveMemory_CoverageMaps",
                columns: new[] { "ProjectId", "CoverageState", "RefreshedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CoverageMaps_ProjectId_KnowledgeRegionId",
                table: "CognitiveMemory_CoverageMaps",
                columns: new[] { "ProjectId", "KnowledgeRegionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CrossProjectPromotionCandidates_PromotionScoreEvaluationTraceId",
                table: "CognitiveMemory_CrossProjectPromotionCandidates",
                column: "PromotionScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CrossProjectPromotionCandidates_SourceProjectId_SourceMemoryRecordId_Status",
                table: "CognitiveMemory_CrossProjectPromotionCandidates",
                columns: new[] { "SourceProjectId", "SourceMemoryRecordId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DistributedJobs_LeasedWorkerId_LeaseExpiresAtUtc",
                table: "CognitiveMemory_DistributedJobs",
                columns: new[] { "LeasedWorkerId", "LeaseExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DistributedJobs_ProjectId_JobKind_InputHash",
                table: "CognitiveMemory_DistributedJobs",
                columns: new[] { "ProjectId", "JobKind", "InputHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DistributedJobs_ProjectId_JobKind_State_CreatedAtUtc",
                table: "CognitiveMemory_DistributedJobs",
                columns: new[] { "ProjectId", "JobKind", "State", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DistributedWorkerResults_DistributedJobId_WorkerId_SubmittedAtUtc",
                table: "CognitiveMemory_DistributedWorkerResults",
                columns: new[] { "DistributedJobId", "WorkerId", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DistributedWorkerResults_ProjectId_Status_SubmittedAtUtc",
                table: "CognitiveMemory_DistributedWorkerResults",
                columns: new[] { "ProjectId", "Status", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DistributedWorkers_Status_LastSeenAtUtc",
                table: "CognitiveMemory_DistributedWorkers",
                columns: new[] { "Status", "LastSeenAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DistributedWorkers_WorkerId",
                table: "CognitiveMemory_DistributedWorkers",
                column: "WorkerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DomainCompetenceProfiles_CompetenceScoreEvaluationTraceId",
                table: "CognitiveMemory_DomainCompetenceProfiles",
                column: "CompetenceScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DomainCompetenceProfiles_ProjectId_ModelProfileId_DomainKey_TaskTypeKey_ProfileVersion",
                table: "CognitiveMemory_DomainCompetenceProfiles",
                columns: new[] { "ProjectId", "ModelProfileId", "DomainKey", "TaskTypeKey", "ProfileVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_HumilityTriggers_SelfRegulationAssessmentId_TriggerKind",
                table: "CognitiveMemory_HumilityTriggers",
                columns: new[] { "SelfRegulationAssessmentId", "TriggerKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_KnowledgeGaps_ProjectId_KnowledgeRegionId_GapKind_CreatedAtUtc",
                table: "CognitiveMemory_KnowledgeGaps",
                columns: new[] { "ProjectId", "KnowledgeRegionId", "GapKind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_KnowledgeRegions_ProjectId_RegionKind_RegionKey",
                table: "CognitiveMemory_KnowledgeRegions",
                columns: new[] { "ProjectId", "RegionKind", "RegionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_KnownFailurePatterns_PatternScoreEvaluationTraceId",
                table: "CognitiveMemory_KnownFailurePatterns",
                column: "PatternScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_KnownFailurePatterns_ProjectId_PatternKind_DomainKey_TaskTypeKey",
                table: "CognitiveMemory_KnownFailurePatterns",
                columns: new[] { "ProjectId", "PatternKind", "DomainKey", "TaskTypeKey" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_LearningOutcomes_ProjectId_LearningTaskId_OutcomeKind",
                table: "CognitiveMemory_LearningOutcomes",
                columns: new[] { "ProjectId", "LearningTaskId", "OutcomeKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_LearningProposals_NeedScoreEvaluationTraceId",
                table: "CognitiveMemory_LearningProposals",
                column: "NeedScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_LearningProposals_ProjectId_Status_CreatedAtUtc",
                table: "CognitiveMemory_LearningProposals",
                columns: new[] { "ProjectId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_LearningTasks_ProjectId_LearningProposalId_Status",
                table: "CognitiveMemory_LearningTasks",
                columns: new[] { "ProjectId", "LearningProposalId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProbeFeedback_ProbeTurnId_Action_CreatedAtUtc",
                table: "CognitiveMemory_ProbeFeedback",
                columns: new[] { "ProbeTurnId", "Action", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProbeFeedback_ProjectId_CalibrationOutcome_CreatedAtUtc",
                table: "CognitiveMemory_ProbeFeedback",
                columns: new[] { "ProjectId", "CalibrationOutcome", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProbeFindings_ProbeTurnId_FindingKind",
                table: "CognitiveMemory_ProbeFindings",
                columns: new[] { "ProbeTurnId", "FindingKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProbeFindings_ProjectId_FindingKind_CreatedAtUtc",
                table: "CognitiveMemory_ProbeFindings",
                columns: new[] { "ProjectId", "FindingKind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProbeRegressionRuns_ProjectId_Outcome_StartedAtUtc",
                table: "CognitiveMemory_ProbeRegressionRuns",
                columns: new[] { "ProjectId", "Outcome", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProbeRegressionRuns_RegressionTestCaseId_StartedAtUtc",
                table: "CognitiveMemory_ProbeRegressionRuns",
                columns: new[] { "RegressionTestCaseId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProbeRegressionTestCases_ProbeTurnId",
                table: "CognitiveMemory_ProbeRegressionTestCases",
                column: "ProbeTurnId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProbeRegressionTestCases_ProjectId_Status_CreatedAtUtc",
                table: "CognitiveMemory_ProbeRegressionTestCases",
                columns: new[] { "ProjectId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProbeSessions_ProjectId_Status_CreatedAtUtc",
                table: "CognitiveMemory_ProbeSessions",
                columns: new[] { "ProjectId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProbeSessions_ProjectId_WorkspaceFrameId_Status",
                table: "CognitiveMemory_ProbeSessions",
                columns: new[] { "ProjectId", "WorkspaceFrameId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProbeTurns_AnswerGateDecisionId",
                table: "CognitiveMemory_ProbeTurns",
                column: "AnswerGateDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProbeTurns_ProbeSessionId_Sequence",
                table: "CognitiveMemory_ProbeTurns",
                columns: new[] { "ProbeSessionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProbeTurns_ProjectId_Status_CreatedAtUtc",
                table: "CognitiveMemory_ProbeTurns",
                columns: new[] { "ProjectId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProbeTurns_RecallTraceId",
                table: "CognitiveMemory_ProbeTurns",
                column: "RecallTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProfessorReviewActions_ProfessorReviewId_SuggestionKind",
                table: "CognitiveMemory_ProfessorReviewActions",
                columns: new[] { "ProfessorReviewId", "SuggestionKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProfessorReviews_ProjectId_ReviewMode_Status_CreatedAtUtc",
                table: "CognitiveMemory_ProfessorReviews",
                columns: new[] { "ProjectId", "ReviewMode", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProfessorReviews_RoutingScoreEvaluationTraceId",
                table: "CognitiveMemory_ProfessorReviews",
                column: "RoutingScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProfessorReviews_SelfRegulationAssessmentId",
                table: "CognitiveMemory_ProfessorReviews",
                column: "SelfRegulationAssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SelfModelProfiles_ProjectId_ModelProfileId_RoleKey_Status",
                table: "CognitiveMemory_SelfModelProfiles",
                columns: new[] { "ProjectId", "ModelProfileId", "RoleKey", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SelfModelUpdateProposals_ProjectId_Status_CreatedAtUtc",
                table: "CognitiveMemory_SelfModelUpdateProposals",
                columns: new[] { "ProjectId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SelfRegulationAssessments_AssessmentScoreEvaluationTraceId",
                table: "CognitiveMemory_SelfRegulationAssessments",
                column: "AssessmentScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SelfRegulationAssessments_ProjectId_State_CreatedAtUtc",
                table: "CognitiveMemory_SelfRegulationAssessments",
                columns: new[] { "ProjectId", "State", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SelfRegulationAssessments_RecallTraceId",
                table: "CognitiveMemory_SelfRegulationAssessments",
                column: "RecallTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SelfRegulationPolicyProfiles_ProjectId_PolicyKey_ProfileVersion",
                table: "CognitiveMemory_SelfRegulationPolicyProfiles",
                columns: new[] { "ProjectId", "PolicyKey", "ProfileVersion" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CognitiveMemory_AnswerGateDecisions");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_AnswerPostureDecisions");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_CalibrationAggregates");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_CalibrationBins");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_CalibrationEvents");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ConfidenceReinforcements");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_CoverageMaps");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_CrossProjectPromotionCandidates");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_DistributedJobs");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_DistributedWorkerResults");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_DistributedWorkers");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_DomainCompetenceProfiles");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_HumilityTriggers");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_KnowledgeGaps");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_KnowledgeRegions");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_KnownFailurePatterns");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_LearningOutcomes");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_LearningProposals");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_LearningTasks");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProbeFeedback");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProbeFindings");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProbeRegressionRuns");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProbeRegressionTestCases");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProbeSessions");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProbeTurns");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProfessorReviewActions");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProfessorReviews");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SelfModelProfiles");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SelfModelUpdateProposals");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SelfRegulationAssessments");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SelfRegulationPolicyProfiles");
        }
    }
}
