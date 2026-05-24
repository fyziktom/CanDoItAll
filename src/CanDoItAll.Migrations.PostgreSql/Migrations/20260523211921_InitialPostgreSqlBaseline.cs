using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgreSqlBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Activity_Entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ArtifactKind = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: true),
                    Route = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Actor = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activity_Entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentFramework_WorkflowArtifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    NodeId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StoragePath = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFramework_WorkflowArtifacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentFramework_WorkflowComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    ProviderProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    Model = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Modality = table.Column<int>(type: "integer", nullable: false),
                    ComponentJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFramework_WorkflowComponents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentFramework_WorkflowDefinitions",
                columns: table => new
                {
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PreferredBackend = table.Column<int>(type: "integer", nullable: false),
                    DefinitionJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFramework_WorkflowDefinitions", x => x.VersionId);
                });

            migrationBuilder.CreateTable(
                name: "AgentFramework_WorkflowEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    NodeId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFramework_WorkflowEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentFramework_WorkflowExternalRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    NodeId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EventName = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    RequestJson = table.Column<string>(type: "TEXT", nullable: false),
                    ResponseJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RespondedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFramework_WorkflowExternalRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentFramework_WorkflowRuns",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Backend = table.Column<int>(type: "integer", nullable: false),
                    BackendRunId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFramework_WorkflowRuns", x => x.RunId);
                });

            migrationBuilder.CreateTable(
                name: "AgentFramework_WorkflowSettings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SettingsJson = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFramework_WorkflowSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Automation_DeadLetters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvelopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvelopeType = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    HandlerKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    DedupeKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CausationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeadLetteredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Automation_DeadLetters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Automation_DeliveryAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvelopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryId = table.Column<Guid>(type: "uuid", nullable: false),
                    HandlerKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CausationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Automation_DeliveryAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Automation_Envelopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvelopeType = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    DedupeKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CausationId = table.Column<Guid>(type: "uuid", nullable: true),
                    AvailableAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Automation_Envelopes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Automation_ExecutionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventKind = table.Column<int>(type: "integer", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CausationId = table.Column<Guid>(type: "uuid", nullable: true),
                    Message = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    DetailsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Automation_ExecutionLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Automation_PluginIngressCursors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceKind = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SourceKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CursorValue = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Automation_PluginIngressCursors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Automation_PluginIngressEnvelopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceKind = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SourceKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ExternalMessageId = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    CursorValue = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    DedupeKey = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    MaterializerKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MaterializationSummary = table.Column<string>(type: "TEXT", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    MaterializedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Automation_PluginIngressEnvelopes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Automation_Triggers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerKind = table.Column<int>(type: "integer", nullable: false),
                    OwnerKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TriggerKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    TriggerKind = table.Column<int>(type: "integer", nullable: false),
                    CronExpression = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    StartAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MisfirePolicy = table.Column<int>(type: "integer", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    DedupeKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    NextPlannedFireAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastFiredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Automation_Triggers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_AnswerGateDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecallTraceId = table.Column<Guid>(type: "uuid", nullable: true),
                    SelfRegulationAssessmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnswerPostureDecisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProfessorReviewId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionKind = table.Column<int>(type: "integer", nullable: false),
                    ScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    DecisionBucket = table.Column<int>(type: "integer", nullable: false),
                    DisplayConfidenceProjection = table.Column<double>(type: "double precision", nullable: true),
                    WarningsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    RequiredOperationsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    DraftAnswerSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_AnswerGateDecisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_AnswerPostureDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SelfRegulationAssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Posture = table.Column<int>(type: "integer", nullable: false),
                    PostureScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostureBucket = table.Column<int>(type: "integer", nullable: false),
                    RequiredOperationsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    WarningsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_AnswerPostureDecisions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_AutomationSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SettingsKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ScheduleMode = table.Column<int>(type: "integer", nullable: false),
                    NightlyLocalTime = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IdleMinutes = table.Column<int>(type: "integer", nullable: false),
                    ScheduledLocalTimes = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    AutoIngestProjectStructure = table.Column<bool>(type: "boolean", nullable: false),
                    AutoIngestProcessRuntime = table.Column<bool>(type: "boolean", nullable: false),
                    AutoConsolidateAfterIngestion = table.Column<bool>(type: "boolean", nullable: false),
                    ModelAccessMode = table.Column<int>(type: "integer", nullable: false),
                    DefaultProviderProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultAgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    AllowedProviderProfileIds = table.Column<string>(type: "TEXT", nullable: false),
                    ModelExecutionProfilesJson = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedByActorId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_AutomationSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_CalibrationAggregates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    DomainKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TaskTypeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ModelProfileId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RiskKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    FeaturePatternKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ProfileVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ObservationCount = table.Column<int>(type: "integer", nullable: false),
                    ExpectedCalibrationError = table.Column<double>(type: "double precision", nullable: false),
                    BrierScore = table.Column<double>(type: "double precision", nullable: false),
                    SignedBias = table.Column<double>(type: "double precision", nullable: false),
                    OverconfidenceRate = table.Column<double>(type: "double precision", nullable: false),
                    UnderconfidenceRate = table.Column<double>(type: "double precision", nullable: false),
                    AbstentionQualityRate = table.Column<double>(type: "double precision", nullable: false),
                    WrongScopeRate = table.Column<double>(type: "double precision", nullable: false),
                    SourceInsufficientRate = table.Column<double>(type: "double precision", nullable: false),
                    CalibrationScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_CalibrationAggregates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_CalibrationBins",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CalibrationAggregateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    BinIndex = table.Column<int>(type: "integer", nullable: false),
                    LowerBound = table.Column<double>(type: "double precision", nullable: false),
                    UpperBound = table.Column<double>(type: "double precision", nullable: false),
                    ObservationCount = table.Column<int>(type: "integer", nullable: false),
                    AveragePredictedConfidence = table.Column<double>(type: "double precision", nullable: false),
                    ActualAccuracy = table.Column<double>(type: "double precision", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_CalibrationBins", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_CalibrationEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    DomainKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TaskTypeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ModelProfileId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RiskKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    FeaturePatternKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ProfileVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PredictedConfidence = table.Column<double>(type: "double precision", nullable: false),
                    ActualCorrect = table.Column<bool>(type: "boolean", nullable: false),
                    OutcomeKind = table.Column<int>(type: "integer", nullable: false),
                    ProbeTurnId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecallTraceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProfessorReviewId = table.Column<Guid>(type: "uuid", nullable: true),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_CalibrationEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ConfidenceReinforcements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SelfRegulationAssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReinforcementKind = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    EvidenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ConfidenceReinforcements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ConsolidationCursors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Cursor = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    LastSourceHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    LastSourceHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LastRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ConsolidationCursors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_CoverageMaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    KnowledgeRegionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CoverageState = table.Column<int>(type: "integer", nullable: false),
                    SourceEvidenceCount = table.Column<int>(type: "integer", nullable: false),
                    RecallFailureCount = table.Column<int>(type: "integer", nullable: false),
                    ProbeFailureCount = table.Column<int>(type: "integer", nullable: false),
                    AbstentionCount = table.Column<int>(type: "integer", nullable: false),
                    RefreshedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_CoverageMaps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_CrossProjectPromotionCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceMemoryRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PromotionScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PromotionBucket = table.Column<int>(type: "integer", nullable: false),
                    RequestedByActorId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ReviewItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecidedByActorId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    DecisionNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_CrossProjectPromotionCandidates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_CuratorCapturedImprovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CuratorSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CuratorTurnId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CaptureKind = table.Column<int>(type: "integer", nullable: false),
                    ConversationDepth = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RecallTraceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContextPackId = table.Column<Guid>(type: "uuid", nullable: true),
                    AffectedMemoryRecordIdsJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    TargetClaimIdsJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    TargetingStatus = table.Column<int>(type: "integer", nullable: false),
                    AnchorState = table.Column<int>(type: "integer", nullable: false),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: true),
                    MutationCommandId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConsolidationCandidateId = table.Column<Guid>(type: "uuid", nullable: true),
                    AppliedMemoryRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssimilatedMemoryRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ConfidenceScore = table.Column<double>(type: "double precision", nullable: false),
                    PriorityScore = table.Column<double>(type: "double precision", nullable: false),
                    TargetConfidenceScore = table.Column<double>(type: "double precision", nullable: false),
                    CaptureLanguage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CaptureScope = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CorrectionText = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AnchorRetiredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_CuratorCapturedImprovements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_CuratorSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RuntimeMode = table.Column<int>(type: "integer", nullable: false),
                    ConversationDepth = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PolicyProfileId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    AccessLevel = table.Column<int>(type: "integer", nullable: false),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    AllowRestrictedContent = table.Column<bool>(type: "boolean", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModelId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    AgentChatSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    AlgorithmVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TurnCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_CuratorSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_CuratorTurns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CuratorSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    RuntimeMode = table.Column<int>(type: "integer", nullable: false),
                    ConversationDepth = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    UserMessage = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    CuratorResponse = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: false),
                    RecallTraceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContextPackId = table.Column<Guid>(type: "uuid", nullable: true),
                    IncludedMemoryRecordIdsJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    ModelId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    CaptureCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_CuratorTurns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_DistributedJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobKind = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    SourceScopeKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    InputPayloadJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    InputHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    InputHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpectedOutputSchema = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PolicyProfileId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    LeaseToken = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    LeasedWorkerId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_DistributedJobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_DistributedWorkerResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DistributedJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    WorkerId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    InputHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OutputHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    OutputSchema = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    OutputPayloadJson = table.Column<string>(type: "character varying(16000)", maxLength: 16000, nullable: false),
                    RejectionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_DistributedWorkerResults", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_DistributedWorkers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkerId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    MachineName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CapabilitiesJson = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    LastSeenAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_DistributedWorkers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_DomainCompetenceProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SelfModelProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    DomainKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TaskTypeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ModelProfileId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ProfileVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CompetenceLevel = table.Column<int>(type: "integer", nullable: false),
                    CompetenceScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceCount = table.Column<int>(type: "integer", nullable: false),
                    EvidenceRefsJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_DomainCompetenceProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_DreamRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    TriggerKind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    PolicyProfileId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ClustersConsidered = table.Column<int>(type: "integer", nullable: false),
                    ClusterMembersRead = table.Column<int>(type: "integer", nullable: false),
                    ClaimsExtracted = table.Column<int>(type: "integer", nullable: false),
                    AggregateCandidatesCreated = table.Column<int>(type: "integer", nullable: false),
                    AggregateClaimsCreated = table.Column<int>(type: "integer", nullable: false),
                    AggregateClaimSourceMapsCreated = table.Column<int>(type: "integer", nullable: false),
                    ValidationRecordsCreated = table.Column<int>(type: "integer", nullable: false),
                    ReviewItemsCreated = table.Column<int>(type: "integer", nullable: false),
                    ApprovedCandidates = table.Column<int>(type: "integer", nullable: false),
                    RejectedCandidates = table.Column<int>(type: "integer", nullable: false),
                    NeedsReviewCandidates = table.Column<int>(type: "integer", nullable: false),
                    EvidenceCoverageRatio = table.Column<double>(type: "double precision", nullable: false),
                    WarningsJson = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FailureMessage = table.Column<string>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_DreamRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_HumilityTriggers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SelfRegulationAssessmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    TriggerKind = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_HumilityTriggers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_KnowledgeGaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    KnowledgeRegionId = table.Column<Guid>(type: "uuid", nullable: false),
                    GapKind = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    EvidenceRefsJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_KnowledgeGaps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_KnowledgeRegions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegionKind = table.Column<int>(type: "integer", nullable: false),
                    RegionKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_KnowledgeRegions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_KnownFailurePatterns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SelfModelProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatternKind = table.Column<int>(type: "integer", nullable: false),
                    DomainKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TaskTypeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TriggerSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Mitigation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    RequiresReview = table.Column<bool>(type: "boolean", nullable: false),
                    PatternScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceRefsJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_KnownFailurePatterns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_LearningOutcomes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    LearningTaskId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutcomeKind = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    SourceRefsJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    ReviewItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    MutationCommandId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_LearningOutcomes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_LearningProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    KnowledgeGapId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Explanation = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    EvidenceRefsJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    Risks = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AcceptanceCriteria = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    NeedScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    NeedBucket = table.Column<int>(type: "integer", nullable: false),
                    DisplayPriorityProjection = table.Column<double>(type: "double precision", nullable: true),
                    DecidedByActorId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    DecisionNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_LearningProposals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_LearningTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    LearningProposalId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    WorkflowExecutorKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApprovalActorId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_LearningTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_MutationCommands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommandKind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ActorKind = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    AffectedMemoryRecordIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    AffectedClaimIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    EvidenceAnchorIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedVersionToken = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RequiresHumanReview = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewReason = table.Column<string>(type: "TEXT", nullable: false),
                    ResultVersionToken = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_MutationCommands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProbeFeedback",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProbeTurnId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProbeSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<int>(type: "integer", nullable: false),
                    CalibrationOutcome = table.Column<int>(type: "integer", nullable: false),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CorrectionText = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    ReviewItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    RegressionTestCaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    CalibrationEventId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProbeFeedback", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProbeFindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProbeTurnId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    FindingKind = table.Column<int>(type: "integer", nullable: false),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ReviewItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProbeFindings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProbeRegressionRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegressionTestCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    RecallTraceId = table.Column<Guid>(type: "uuid", nullable: true),
                    FailureReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    EvaluatorProfileVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProbeRegressionRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProbeRegressionTestCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProbeTurnId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Question = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    ExpectedEvidenceText = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ExpectedContextKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    AccessPolicyProfileId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    EvaluatorProfileVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProbeRegressionTestCases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProbeSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RecallMode = table.Column<int>(type: "integer", nullable: false),
                    WorkspaceFrameId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PolicyProfileId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    AccessLevel = table.Column<int>(type: "integer", nullable: false),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    AllowRestrictedContent = table.Column<bool>(type: "boolean", nullable: false),
                    ProjectionCollectionName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ProjectionProfileId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    EmbeddingProfileId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TurnCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProbeSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProbeTurns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProbeSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Intent = table.Column<int>(type: "integer", nullable: false),
                    Question = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    AnswerSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    RecallTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextPackId = table.Column<Guid>(type: "uuid", nullable: true),
                    SelfRegulationAssessmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnswerPostureDecisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnswerGateDecisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProbeScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProbeScoreBucket = table.Column<int>(type: "integer", nullable: false),
                    DisplayProbeScore = table.Column<double>(type: "double precision", nullable: true),
                    WarningCount = table.Column<int>(type: "integer", nullable: false),
                    WarningsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    MetadataJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProbeTurns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProfessorReviewActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProfessorReviewId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SuggestionKind = table.Column<int>(type: "integer", nullable: false),
                    CreatedReviewItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedLearningProposalId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedRegressionTestCaseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProfessorReviewActions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProfessorReviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewMode = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    RequestedByActorId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ModelProfileId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PromptProfileVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PolicyProfileId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SelfRegulationAssessmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnswerPostureDecisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RoutingScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    InputSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ContextSummary = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Critique = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    MissingEvidence = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    RecommendedPosture = table.Column<int>(type: "integer", nullable: false),
                    OutputHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    OutputHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequiresHumanReview = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProfessorReviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProjectionStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectionKind = table.Column<int>(type: "integer", nullable: false),
                    TargetProvider = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ProjectionSchemaVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LastSourceHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LastProjectedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FailureMessage = table.Column<string>(type: "TEXT", nullable: false),
                    RebuildRequired = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProjectionStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_QualityClusters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClusterHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PrimaryKeyFamily = table.Column<int>(type: "integer", nullable: false),
                    Readiness = table.Column<int>(type: "integer", nullable: false),
                    AccessLevel = table.Column<int>(type: "integer", nullable: false),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    PolicyProfileId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    KeyCount = table.Column<int>(type: "integer", nullable: false),
                    MemberCount = table.Column<int>(type: "integer", nullable: false),
                    SourceEvidenceCount = table.Column<int>(type: "integer", nullable: false),
                    ContradictionCount = table.Column<int>(type: "integer", nullable: false),
                    CohesionScore = table.Column<double>(type: "double precision", nullable: false),
                    SourceIndependenceScore = table.Column<double>(type: "double precision", nullable: false),
                    SourceDiversityScore = table.Column<double>(type: "double precision", nullable: false),
                    SemanticSignalScore = table.Column<double>(type: "double precision", nullable: false),
                    SupportingSignalScore = table.Column<double>(type: "double precision", nullable: false),
                    GuardPenaltyScore = table.Column<double>(type: "double precision", nullable: false),
                    CompositeScore = table.Column<double>(type: "double precision", nullable: false),
                    AggregateEligible = table.Column<bool>(type: "boolean", nullable: false),
                    EligibilityReason = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_QualityClusters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ReviewItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewKind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SubjectKind = table.Column<int>(type: "integer", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ReasonText = table.Column<string>(type: "TEXT", nullable: false),
                    SourceEvidenceCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecidedByActorId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    DecisionNotes = table.Column<string>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ReviewItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_Runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    RunKind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OperationMode = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    InputHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    InputHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Cursor = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FailureMessage = table.Column<string>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_Runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ScoreEvaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerKind = table.Column<int>(type: "integer", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    SpaceKind = table.Column<int>(type: "integer", nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizationProfile = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    InputHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    InputHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ScalarProjectionKind = table.Column<int>(type: "integer", nullable: false),
                    ProjectionBucket = table.Column<int>(type: "integer", nullable: false),
                    DisplayScore = table.Column<double>(type: "double precision", nullable: true),
                    MissingRequiredDimensionCount = table.Column<int>(type: "integer", nullable: false),
                    MatchedShapeCount = table.Column<int>(type: "integer", nullable: false),
                    TracePayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    CalculatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ScoreEvaluations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SelfModelProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ModelProfileId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RoleKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ProfileVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    OperatingPrinciples = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AllowedTaskCategoriesJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    RestrictedTaskCategoriesJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SelfModelProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SelfModelUpdateProposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ModelProfileId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    DomainKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ProposedChange = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    EvidenceRefsJson = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    RequestedByActorId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SelfModelUpdateProposals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SelfRegulationAssessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SelfModelProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    DomainCompetenceProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    CalibrationAggregateId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecallTraceId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkspaceFrameId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttentionDecisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ModelProfileId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    DomainKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TaskTypeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    AssessmentScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssessmentBucket = table.Column<int>(type: "integer", nullable: false),
                    DisplayAssessmentScore = table.Column<double>(type: "double precision", nullable: true),
                    WarningsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    RequiredOperationsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SelfRegulationAssessments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SelfRegulationPolicyProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SelfModelProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ProfileVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    AllowedPosturesJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    RequiredOperationsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    ReviewThreshold = table.Column<double>(type: "double precision", nullable: false),
                    AbstentionThreshold = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SelfRegulationPolicyProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SourceItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceManifestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceSystem = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceItemKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SourceItemType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ContentText = table.Column<string>(type: "TEXT", nullable: false),
                    Locator = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ContentHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RedactionState = table.Column<int>(type: "integer", nullable: false),
                    AccessLevel = table.Column<int>(type: "integer", nullable: false),
                    AccessScope = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    ProvenanceJson = table.Column<string>(type: "TEXT", nullable: false),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SourceItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SourceManifests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceSystem = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceScopeKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    SourceSnapshotId = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    SnapshotHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    SnapshotHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Cursor = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ScanStatus = table.Column<int>(type: "integer", nullable: false),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SourceManifests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_TemporalEpisodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EpisodeKind = table.Column<int>(type: "integer", nullable: false),
                    Goal = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedOutcome = table.Column<string>(type: "TEXT", nullable: false),
                    ActualOutcome = table.Column<string>(type: "TEXT", nullable: false),
                    OutcomeSummary = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FirstStepAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastStepAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StepCount = table.Column<int>(type: "integer", nullable: false),
                    LinkCount = table.Column<int>(type: "integer", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_TemporalEpisodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Collaboration_InboxItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemKind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    PreviewText = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Route = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsUnread = table.Column<bool>(type: "boolean", nullable: false),
                    UnreadCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collaboration_InboxItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Collaboration_Messages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AuthorKind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AuthorKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AuthorName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    RaisesEscalation = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collaboration_Messages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Collaboration_Participants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParticipantKind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ParticipantKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RoleLabel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AddedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collaboration_Participants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Collaboration_Threads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Subject = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    ContextKind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ContextId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContextLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ContextRoute = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PrimaryItemKind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastActivityAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collaboration_Threads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_AccountProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountPartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationshipStage = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CommercialNotes = table.Column<string>(type: "TEXT", nullable: false),
                    ConstraintNotes = table.Column<string>(type: "TEXT", nullable: false),
                    TimingRiskNotes = table.Column<string>(type: "TEXT", nullable: false),
                    LastChangedBy = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_AccountProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_AccountStakeholders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountPartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelatedPartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_AccountStakeholders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_AiAgentProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultModel = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ExecutionMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    OwnerPartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CapabilityJson = table.Column<string>(type: "TEXT", nullable: false),
                    ValidationStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    LastReviewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    ExtendedDataJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_AiAgentProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_AiResourceBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TechnicalAgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    BindingStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    BindingReason = table.Column<string>(type: "TEXT", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_AiResourceBindings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_AuditEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Summary = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    DetailJson = table.Column<string>(type: "TEXT", nullable: false),
                    Actor = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsSensitive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_AuditEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_CapacityBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    StartDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Percentage = table.Column<decimal>(type: "numeric", nullable: false),
                    RelatedProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_CapacityBlocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_ConfidentialNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NoteText = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_ConfidentialNotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_InteractionParties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InteractionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_InteractionParties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_Interactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InteractionType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    NextActionText = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    NextActionOwnerPartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    NextActionDueUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RelatedOpportunityId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelatedProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_Interactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_LookupOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CatalogKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsSystemDefault = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_LookupOptions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_OnboardingTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaskKind = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OwnerPartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    DueDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    RelatedProjectId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_OnboardingTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_Opportunities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Stage = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RelationshipStage = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    AccountPartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerPartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    DeliveryUnitPartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkedProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: true),
                    ProbabilityPercent = table.Column<int>(type: "integer", nullable: false),
                    ExpectedCloseDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OpportunitySource = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LostReason = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    ExtendedDataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_Opportunities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_OpportunityParties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_OpportunityParties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_OpportunityStageHistory",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stage = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ChangedBy = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_OpportunityStageHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_Parties",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LifecycleStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LegalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PreferredName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExternalCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    TagsJson = table.Column<string>(type: "TEXT", nullable: false),
                    Region = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsSensitive = table.Column<bool>(type: "boolean", nullable: false),
                    ExtendedDataJson = table.Column<string>(type: "TEXT", nullable: false),
                    LastChangedBy = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_Parties", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_PartyAddresses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AddressType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Line1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Line2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    City = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Region = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PostalCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_PartyAddresses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_PartyContactPoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContactType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Value = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    NormalizedValue = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_PartyContactPoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_PartyRelationships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourcePartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetPartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationshipKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    StartDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_PartyRelationships", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_PartyRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleKind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    ValidFromUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ValidToUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_PartyRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_PartySkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    Proficiency = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    YearsExperience = table.Column<int>(type: "integer", nullable: false),
                    CertificationStatus = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    LastValidatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_PartySkills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_ProjectPartyAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentKind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    NodeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PhaseName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uuid", nullable: true),
                    AllocationPercent = table.Column<decimal>(type: "numeric", nullable: true),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_ProjectPartyAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_RecruitmentApplications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetUnitPartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecruiterPartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    HiringManagerPartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    DesiredRole = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Source = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Stage = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AvailableFromUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Decision = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_RecruitmentApplications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_RecruitmentInterviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    InterviewType = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    InterviewerPartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Feedback = table.Column<string>(type: "TEXT", nullable: false),
                    Recommendation = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_RecruitmentInterviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_Skills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Category = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_Skills", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_StaffingRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedByPartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryUnitPartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NeededRole = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    NeededSkillsJson = table.Column<string>(type: "TEXT", nullable: false),
                    StartDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AllocationPercent = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_StaffingRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_WorkforceProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkforceKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    EmployeeCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    JobTitle = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Discipline = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Seniority = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    HomeUnitPartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    ManagerPartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    StartDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndDateUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Location = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TimeZone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    InternalCostRate = table.Column<decimal>(type: "numeric", nullable: true),
                    ExternalBillingRate = table.Column<decimal>(type: "numeric", nullable: true),
                    CapacityHoursPerWeek = table.Column<decimal>(type: "numeric", nullable: false),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ExtendedDataJson = table.Column<string>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_WorkforceProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Factory_PromptBlocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    BlockKind = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    IsRecommendedByDefault = table.Column<bool>(type: "boolean", nullable: false),
                    PromptTypeRules = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    BlueprintRules = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PhaseRules = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    GroupKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TagsJson = table.Column<string>(type: "TEXT", nullable: false),
                    StackTagsJson = table.Column<string>(type: "TEXT", nullable: false),
                    TemplateTokensJson = table.Column<string>(type: "TEXT", nullable: false),
                    ToolboxEligible = table.Column<bool>(type: "boolean", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    CatalogSource = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factory_PromptBlocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Factory_PromptBlueprints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PromptType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Guidance = table.Column<string>(type: "TEXT", nullable: false),
                    RecommendedFlowTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecommendedFlowKey = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    RecommendedBlockKeysJson = table.Column<string>(type: "TEXT", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    CatalogSource = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factory_PromptBlueprints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Factory_PromptBuildSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Phase = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    BlueprintId = table.Column<Guid>(type: "uuid", nullable: true),
                    FlowTemplateId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromptArtifactId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromptRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    SelectedPromptRunNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    RepositoryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BranchName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CommitSha = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SelectedBlockIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    SelectedResourceIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    GeneratedPrompt = table.Column<string>(type: "TEXT", nullable: false),
                    WarningSummary = table.Column<string>(type: "TEXT", nullable: false),
                    CanvasUiStateJson = table.Column<string>(type: "TEXT", nullable: false),
                    ComponentCustomizationsJson = table.Column<string>(type: "TEXT", nullable: false),
                    SessionAttachmentsJson = table.Column<string>(type: "TEXT", nullable: false),
                    WizardStepIndex = table.Column<int>(type: "integer", nullable: false),
                    HasCustomizedBlocks = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factory_PromptBuildSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Factory_PromptFlowTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    BlockIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    BlockKeysJson = table.Column<string>(type: "TEXT", nullable: false),
                    AgentSequenceJson = table.Column<string>(type: "TEXT", nullable: false),
                    PromptTypeRules = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    CatalogSource = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factory_PromptFlowTemplates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Factory_PromptRunNodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PromptRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    PromptBlockDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromptArtifactId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentPromptRunNodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BranchKey = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    BranchLabel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factory_PromptRunNodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Factory_PromptRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    FlowTemplateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phase = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factory_PromptRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Infrastructure_BackgroundJobRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    JobType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorSummary = table.Column<string>(type: "TEXT", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Infrastructure_BackgroundJobRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Infrastructure_SearchDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SourceKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Category = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Body = table.Column<string>(type: "TEXT", nullable: false),
                    Route = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Infrastructure_SearchDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Plugins_CapabilityGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginId = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Capability = table.Column<int>(type: "integer", nullable: false),
                    RecipeId = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    ScopeKind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ScopeKey = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    State = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RiskKind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Reason = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plugins_CapabilityGrants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Plugins_Connections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginId = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    ConnectionKey = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    SettingsJson = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    HealthStatus = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    UpdatedBy = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plugins_Connections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Plugins_Installations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginId = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    PackageId = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    DisplayNameSnapshot = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Version = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Vendor = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    ManifestSnapshotJson = table.Column<string>(type: "TEXT", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    InstalledBy = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    InstalledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plugins_Installations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Plugins_Logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginId = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    PackageId = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    WorkflowExecutorId = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    StreamKind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OperationKind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Severity = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Message = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                    DetailsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plugins_Logs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Plugins_OAuthConnections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PluginId = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    ConnectionKey = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    TokenVaultKey = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    AccountDisplay = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    GrantedScopesJson = table.Column<string>(type: "TEXT", nullable: false),
                    AccessTokenExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RefreshTokenExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastErrorCode = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    LastErrorDescription = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plugins_OAuthConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Plugins_OAuthSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StateHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PluginId = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    ConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionKey = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    CodeVerifierVaultKey = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    RedirectUri = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ReturnPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RequestedScopesJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ErrorDescription = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plugins_OAuthSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Processes_Outbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommandKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: false),
                    LeaseToken = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_Outbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects_ProjectHierarchyLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChildProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects_ProjectHierarchyLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects_ProjectOptionSelections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    OptionName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects_ProjectOptionSelections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects_ProjectPhases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Goal = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    StartDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects_ProjectPhases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects_Projects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    Objective = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentPhase = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TargetDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects_Projects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Prompts_PromptArtifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    CollectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phase = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentDraftText = table.Column<string>(type: "TEXT", nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prompts_PromptArtifacts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Prompts_PromptArtifactTags",
                columns: table => new
                {
                    PromptArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    PromptTagId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prompts_PromptArtifactTags", x => new { x.PromptArtifactId, x.PromptTagId });
                });

            migrationBuilder.CreateTable(
                name: "Prompts_PromptCollections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prompts_PromptCollections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Prompts_PromptTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prompts_PromptTags", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Prompts_PromptUsageRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PromptArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    PromptVersionNumber = table.Column<int>(type: "integer", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Phase = table.Column<string>(type: "text", nullable: false),
                    ProviderName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RepositoryName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BranchName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CommitSha = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CommitUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UsageNote = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prompts_PromptUsageRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Prompts_PromptVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PromptArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    CreationReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OutputFormat = table.Column<string>(type: "text", nullable: false),
                    SourceBlueprintId = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prompts_PromptVersions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Resources_ProjectResources",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerPartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    MaintainerPartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResourceKind = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    ConnectorPluginKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ConfigSchemaVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LocationOrIdentifier = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ConfigJson = table.Column<string>(type: "TEXT", nullable: false),
                    LinkedSecretIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    ValidationStatus = table.Column<int>(type: "integer", nullable: false),
                    Sensitivity = table.Column<int>(type: "integer", nullable: false),
                    SupportsPreview = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsIndexing = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Resources_ProjectResources", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Security_SecretRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    EncryptedPayload = table.Column<string>(type: "TEXT", nullable: false),
                    Scope = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    RotationNote = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Security_SecretRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Security_SecretReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SecretRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ContextId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Purpose = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Security_SecretReferences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchedulerPlanner_Plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    TargetKind = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetNameSnapshot = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    CronExpression = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CronDescription = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    MisfirePolicy = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    StartAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    InputJson = table.Column<string>(type: "TEXT", nullable: false),
                    AutomationTriggerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AutomationTriggerKey = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    NextPlannedFireAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastFiredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulerPlanner_Plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Storage_Catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProviderKind = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsSystemDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsReadOnly = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    ConnectionMode = table.Column<int>(type: "integer", nullable: false),
                    EndpointOrRoot = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                    ConfigJson = table.Column<string>(type: "TEXT", nullable: false),
                    CapabilityMask = table.Column<int>(type: "integer", nullable: false),
                    HealthStatus = table.Column<int>(type: "integer", nullable: false),
                    LastTestedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastHealthMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CredentialSecretId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Storage_Catalog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Storage_RoutingRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    ScopeKind = table.Column<int>(type: "integer", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    NodeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    UsagePurpose = table.Column<int>(type: "integer", nullable: false),
                    ContentKind = table.Column<int>(type: "integer", nullable: false),
                    MimePattern = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MinimumContentLength = table.Column<long>(type: "bigint", nullable: true),
                    MaximumContentLength = table.Column<long>(type: "bigint", nullable: true),
                    EditIntent = table.Column<bool>(type: "boolean", nullable: false),
                    PreviewRequired = table.Column<bool>(type: "boolean", nullable: false),
                    PublishIntent = table.Column<bool>(type: "boolean", nullable: false),
                    RequiredCapabilities = table.Column<int>(type: "integer", nullable: false),
                    PreferredStorageId = table.Column<Guid>(type: "uuid", nullable: false),
                    AlternativeStorageIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Storage_RoutingRules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestLab_TestCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TestPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StoryOrFeature = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestLab_TestCases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestLab_TestEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TestPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ArtifactPath = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    EvidenceKind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestLab_TestEvidence", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestLab_TestPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResponsiblePartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Phase = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CoverageGoal = table.Column<string>(type: "TEXT", nullable: false),
                    PlaywrightSpecPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestLab_TestPlans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TestLab_TestRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TestPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExecutedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Runner = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Result = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TestLab_TestRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Validation_Findings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ValidationRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    RuleCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Detail = table.Column<string>(type: "TEXT", nullable: false),
                    RecommendedAction = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Validation_Findings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Validation_Checklists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ValidationType = table.Column<int>(type: "integer", nullable: false),
                    VersionLabel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ItemsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Validation_Checklists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Validation_Runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ChecklistId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResponsiblePartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    ValidationType = table.Column<int>(type: "integer", nullable: false),
                    ArtifactTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ArtifactRoute = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SourceContent = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Decision = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Validation_Runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workbench_ProjectCrossModuleMutations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeNodeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    MutationKind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovalState = table.Column<int>(type: "integer", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workbench_ProjectCrossModuleMutations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workbench_ProjectObjectLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceNodeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TargetNodeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    LinkKind = table.Column<int>(type: "integer", nullable: false),
                    IsSystemManaged = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workbench_ProjectObjectLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workbench_ProjectObjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ObjectType = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Subtitle = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Status = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    ObjectSubtype = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ProgressMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProgressPercent = table.Column<int>(type: "integer", nullable: false),
                    MarkersJson = table.Column<string>(type: "TEXT", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    ParentNodeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    PositionX = table.Column<double>(type: "double precision", nullable: false),
                    PositionY = table.Column<double>(type: "double precision", nullable: false),
                    StartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    IsSystemManaged = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workbench_ProjectObjects", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workbench_ProjectProjectionLayouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PositionX = table.Column<double>(type: "double precision", nullable: false),
                    PositionY = table.Column<double>(type: "double precision", nullable: false),
                    IsHidden = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workbench_ProjectProjectionLayouts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workbench_ProjectStructureLeases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeKind = table.Column<int>(type: "integer", nullable: false),
                    ScopeKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    LeaseToken = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AgentId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AgentName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MachineName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RepositoryRoot = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    BranchName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    AcquiredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RenewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReleasedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workbench_ProjectStructureLeases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workbench_ProjectStructureOperationAnalytics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    NodeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ScopeKind = table.Column<int>(type: "integer", nullable: true),
                    ScopeKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    AgentId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AgentName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MachineName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RepositoryRoot = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    BranchName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    WarningCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    RequestSummaryJson = table.Column<string>(type: "TEXT", nullable: false),
                    ResponseSummaryJson = table.Column<string>(type: "TEXT", nullable: false),
                    WarningsJson = table.Column<string>(type: "TEXT", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workbench_ProjectStructureOperationAnalytics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workbench_ViewStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SurfaceKind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    StateJson = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workbench_ViewStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workspace_ConnectorCommands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectorPluginKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CommandKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovalState = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: false),
                    ResultJson = table.Column<string>(type: "TEXT", nullable: false),
                    LeaseToken = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RequestedBy = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspace_ConnectorCommands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workspace_ProviderProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProviderKind = table.Column<int>(type: "integer", nullable: true),
                    ConnectorPluginKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ConfigSchemaVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ApiKeySecretId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultModel = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsStreaming = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsToolCalling = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsStructuredOutput = table.Column<bool>(type: "boolean", nullable: false),
                    SupportsVision = table.Column<bool>(type: "boolean", nullable: false),
                    LastHealthCheckAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastHealthStatus = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ExtraSettingsJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspace_ProviderProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workspace_Settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DefaultProviderProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefaultPromptOutputFormat = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspace_Settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Automation_EnvelopeDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvelopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvelopeType = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    HandlerKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    AvailableAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: false),
                    LockToken = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LockedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Automation_EnvelopeDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Automation_EnvelopeDeliveries_Automation_Envelopes_Envelope~",
                        column: x => x.EnvelopeId,
                        principalTable: "Automation_Envelopes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_MutationAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MutationCommandId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    EventKind = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_MutationAuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_MutationAuditEvents_CognitiveMemory_Mutatio~",
                        column: x => x.MutationCommandId,
                        principalTable: "CognitiveMemory_MutationCommands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_DreamRunClusters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DreamRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClusterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Readiness = table.Column<int>(type: "integer", nullable: false),
                    SelectionReasonCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    MemberCount = table.Column<int>(type: "integer", nullable: false),
                    ClaimCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_DreamRunClusters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_DreamRunClusters_CognitiveMemory_DreamRuns_~",
                        column: x => x.DreamRunId,
                        principalTable: "CognitiveMemory_DreamRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_DreamRunClusters_CognitiveMemory_QualityClu~",
                        column: x => x.ClusterId,
                        principalTable: "CognitiveMemory_QualityClusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_QualityClusterKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClusterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    KeyFamily = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DisplayText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_QualityClusterKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_QualityClusterKeys_CognitiveMemory_QualityC~",
                        column: x => x.ClusterId,
                        principalTable: "CognitiveMemory_QualityClusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ConsolidationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    TriggerKind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProfileName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    InputHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    InputHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OutputHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    OutputHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Cursor = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    NextCursor = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    LeaseOwnerId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SourceItemsScanned = table.Column<int>(type: "integer", nullable: false),
                    CandidatesCreated = table.Column<int>(type: "integer", nullable: false),
                    MutationCommandsSubmitted = table.Column<int>(type: "integer", nullable: false),
                    ReviewItemsCreated = table.Column<int>(type: "integer", nullable: false),
                    ProjectionInvalidations = table.Column<int>(type: "integer", nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FailureMessage = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ConsolidationRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ConsolidationRuns_CognitiveMemory_Runs_Id",
                        column: x => x.Id,
                        principalTable: "CognitiveMemory_Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SourceScanFailures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceSystem = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceScopeKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    CursorHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExceptionCategory = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RetryPolicy = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SourceScanFailures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SourceScanFailures_CognitiveMemory_Runs_Run~",
                        column: x => x.RunId,
                        principalTable: "CognitiveMemory_Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ContextFrames",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    FrameKind = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ConfidenceScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfidenceBucket = table.Column<int>(type: "integer", nullable: false),
                    DisplayConfidenceScore = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ContextFrames", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ContextFrames_CognitiveMemory_ScoreEvaluati~",
                        column: x => x.ConfidenceScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureSimulations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutputKind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    IsSpeculative = table.Column<bool>(type: "boolean", nullable: false),
                    SpeculationLabel = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    RiskScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RiskBucket = table.Column<int>(type: "integer", nullable: false),
                    DisplayRiskScore = table.Column<double>(type: "double precision", nullable: true),
                    PolicyProfileId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SourceScopeKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RequiredValidationStepsJson = table.Column<string>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureSimulations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureSimulations_CognitiveMemory_ScoreE~",
                        column: x => x.RiskScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ReplayJobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobKind = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    PriorityScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PriorityBucket = table.Column<int>(type: "integer", nullable: false),
                    DisplayPriorityProjection = table.Column<double>(type: "double precision", nullable: true),
                    QueuePriority = table.Column<int>(type: "integer", nullable: false),
                    InputHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    InputHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpectedOutputSchema = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PolicyProfileId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SourceScopeKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    LeaseToken = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ScheduledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FailureMessage = table.Column<string>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ReplayJobs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ReplayJobs_CognitiveMemory_ScoreEvaluations~",
                        column: x => x.PriorityScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ScoreComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    OwnerKind = table.Column<int>(type: "integer", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    SpaceKind = table.Column<int>(type: "integer", nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DimensionKind = table.Column<int>(type: "integer", nullable: false),
                    NormalizedValue = table.Column<double>(type: "double precision", nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    EvidenceKind = table.Column<int>(type: "integer", nullable: false),
                    EvidenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceConfidence = table.Column<double>(type: "double precision", nullable: true),
                    CalculatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ComponentPayloadJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ScoreComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ScoreComponents_CognitiveMemory_ScoreEvalua~",
                        column: x => x.ScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_WorkspaceFrames",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    FrameKind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OwnerUserId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    OwnerAgentId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkflowRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessStepId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProbeSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    LearningTaskId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContextBudgetTokenLimit = table.Column<int>(type: "integer", nullable: false),
                    ContextBudgetSectionLimit = table.Column<int>(type: "integer", nullable: false),
                    ContextBudgetDetailLimit = table.Column<int>(type: "integer", nullable: false),
                    CurrentTokenEstimate = table.Column<int>(type: "integer", nullable: false),
                    CurrentSectionEstimate = table.Column<int>(type: "integer", nullable: false),
                    CurrentDetailEstimate = table.Column<int>(type: "integer", nullable: false),
                    BudgetExhausted = table.Column<bool>(type: "boolean", nullable: false),
                    LimitingBudget = table.Column<int>(type: "integer", nullable: true),
                    CognitiveLoadScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CognitiveLoadBucket = table.Column<int>(type: "integer", nullable: false),
                    DisplayCognitiveLoadScore = table.Column<double>(type: "double precision", nullable: true),
                    LastAttentionDecisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastSelfRegulationAssessmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastAnswerPostureDecisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_WorkspaceFrames", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceFrames_CognitiveMemory_ScoreEvalua~",
                        column: x => x.CognitiveLoadScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SourceItemLayouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    X = table.Column<double>(type: "double precision", nullable: true),
                    Y = table.Column<double>(type: "double precision", nullable: true),
                    ZIndex = table.Column<int>(type: "integer", nullable: true),
                    StartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    SurfaceKind = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SourceItemLayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SourceItemLayouts_CognitiveMemory_SourceIte~",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_EvidenceAnchors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnchorKind = table.Column<int>(type: "integer", nullable: false),
                    SourceManifestId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceSystem = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Locator = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    StructuredPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    TextStart = table.Column<int>(type: "integer", nullable: true),
                    TextEnd = table.Column<int>(type: "integer", nullable: true),
                    QuoteHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TrustLevel = table.Column<int>(type: "integer", nullable: false),
                    RedactionState = table.Column<int>(type: "integer", nullable: false),
                    SourceHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    SourceHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_EvidenceAnchors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_EvidenceAnchors_CognitiveMemory_SourceItems~",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_EvidenceAnchors_CognitiveMemory_SourceManif~",
                        column: x => x.SourceManifestId,
                        principalTable: "CognitiveMemory_SourceManifests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SourceItemGraphLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceManifestId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceItemKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TargetSourceItemKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LinkKind = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsUserAuthored = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SourceItemGraphLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SourceItemGraphLinks_CognitiveMemory_Source~",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SourceItemGraphLinks_CognitiveMemory_Sourc~1",
                        column: x => x.SourceManifestId,
                        principalTable: "CognitiveMemory_SourceManifests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SourceTombstones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceSystem = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceScopeKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    SourceItemKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PreviousSourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    DetectedInManifestId = table.Column<Guid>(type: "uuid", nullable: false),
                    TombstonedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SourceTombstones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SourceTombstones_CognitiveMemory_SourceItem~",
                        column: x => x.PreviousSourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SourceTombstones_CognitiveMemory_SourceMani~",
                        column: x => x.DetectedInManifestId,
                        principalTable: "CognitiveMemory_SourceManifests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_EpisodeSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EpisodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SequenceIndex = table.Column<int>(type: "integer", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ActorKind = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ActionKind = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    ToolOrPluginKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ErrorSummary = table.Column<string>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_EpisodeSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_EpisodeSteps_CognitiveMemory_TemporalEpisod~",
                        column: x => x.EpisodeId,
                        principalTable: "CognitiveMemory_TemporalEpisodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_TemporalEpisodeLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EpisodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkKind = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_TemporalEpisodeLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_TemporalEpisodeLinks_CognitiveMemory_Tempor~",
                        column: x => x.EpisodeId,
                        principalTable: "CognitiveMemory_TemporalEpisodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SchedulerPlanner_Runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    DedupeKey = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    AutomationEnvelopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    FiredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    TargetRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetRunKind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: false),
                    DispatchedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulerPlanner_Runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SchedulerPlanner_Runs_SchedulerPlanner_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "SchedulerPlanner_Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Workbench_ProjectNodeBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectObjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Route = table.Column<string>(type: "TEXT", nullable: false),
                    ExternalArtifactKind = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ExternalArtifactId = table.Column<Guid>(type: "uuid", nullable: true),
                    MediaRelativePath = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
                    MediaContentType = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    MediaOriginalFileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    StorageObjectReferenceJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workbench_ProjectNodeBindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workbench_ProjectNodeBindings_Workbench_ProjectObjects_Proj~",
                        column: x => x.ProjectObjectId,
                        principalTable: "Workbench_ProjectObjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Workbench_ProjectNodeLifecycleEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectObjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TransitionMode = table.Column<int>(type: "integer", nullable: false),
                    SourceFamily = table.Column<int>(type: "integer", nullable: false),
                    TargetFamily = table.Column<int>(type: "integer", nullable: false),
                    SourceObjectType = table.Column<int>(type: "integer", nullable: false),
                    SourceObjectSubtype = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TargetObjectType = table.Column<int>(type: "integer", nullable: false),
                    TargetObjectSubtype = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SourceSnapshotJson = table.Column<string>(type: "TEXT", nullable: false),
                    TargetSnapshotJson = table.Column<string>(type: "TEXT", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workbench_ProjectNodeLifecycleEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workbench_ProjectNodeLifecycleEvents_Workbench_ProjectObjec~",
                        column: x => x.ProjectObjectId,
                        principalTable: "Workbench_ProjectObjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Workbench_ProjectNodeReferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectObjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReferenceKind = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ReferenceId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workbench_ProjectNodeReferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workbench_ProjectNodeReferences_Workbench_ProjectObjects_Pr~",
                        column: x => x.ProjectObjectId,
                        principalTable: "Workbench_ProjectObjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Workspace_ConnectorCommandAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectorCommandId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventKind = table.Column<int>(type: "integer", nullable: false),
                    Actor = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Message = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    DetailsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspace_ConnectorCommandAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workspace_ConnectorCommandAudits_Workspace_ConnectorCommand~",
                        column: x => x.ConnectorCommandId,
                        principalTable: "Workspace_ConnectorCommands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ConsolidationCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    CandidateKind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: true),
                    MemoryRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    MutationCommandId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScoreBucket = table.Column<int>(type: "integer", nullable: false),
                    DisplayPriorityProjection = table.Column<double>(type: "double precision", nullable: true),
                    SourceContentHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    SourceContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OutputHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    OutputHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ReasonText = table.Column<string>(type: "TEXT", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ConsolidationCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ConsolidationCandidates_CognitiveMemory_Con~",
                        column: x => x.RunId,
                        principalTable: "CognitiveMemory_ConsolidationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ConsolidationReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReportHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    ReportHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ReportJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ConsolidationReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ConsolidationReports_CognitiveMemory_Consol~",
                        column: x => x.RunId,
                        principalTable: "CognitiveMemory_ConsolidationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ContextBoundaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceContextFrameId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetContextFrameId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoundaryKind = table.Column<int>(type: "integer", nullable: false),
                    BoundaryPolicy = table.Column<int>(type: "integer", nullable: false),
                    ScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Explanation = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ContextBoundaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ContextBoundaries_CognitiveMemory_ContextFr~",
                        column: x => x.SourceContextFrameId,
                        principalTable: "CognitiveMemory_ContextFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ContextBoundaries_CognitiveMemory_ContextF~1",
                        column: x => x.TargetContextFrameId,
                        principalTable: "CognitiveMemory_ContextFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ContextBoundaries_CognitiveMemory_ScoreEval~",
                        column: x => x.ScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ContextFrameDimensions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextFrameId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    DimensionKind = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ValueKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ContextFrameDimensions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ContextFrameDimensions_CognitiveMemory_Cont~",
                        column: x => x.ContextFrameId,
                        principalTable: "CognitiveMemory_ContextFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_Entities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityKind = table.Column<int>(type: "integer", nullable: false),
                    CanonicalName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CanonicalNameKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PrimaryContextFrameId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfidenceScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfidenceBucket = table.Column<int>(type: "integer", nullable: false),
                    DisplayConfidenceScore = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_Entities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Entities_CognitiveMemory_ContextFrames_Prim~",
                        column: x => x.PrimaryContextFrameId,
                        principalTable: "CognitiveMemory_ContextFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Entities_CognitiveMemory_ScoreEvaluations_C~",
                        column: x => x.ConfidenceScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SourceItemContextHints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextFrameId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    DimensionKind = table.Column<int>(type: "integer", nullable: false),
                    ValueKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SourceItemContextHints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SourceItemContextHints_CognitiveMemory_Cont~",
                        column: x => x.ContextFrameId,
                        principalTable: "CognitiveMemory_ContextFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SourceItemContextHints_CognitiveMemory_Sour~",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ReplayJobTargets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReplayJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetKind = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: true),
                    TargetKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RequiredInputHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ReplayJobTargets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ReplayJobTargets_CognitiveMemory_ReplayJobs~",
                        column: x => x.ReplayJobId,
                        principalTable: "CognitiveMemory_ReplayJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ReplayOutputs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReplayJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    OutputKind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    PayloadHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    ReviewItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    MutationCommandId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ReplayOutputs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ReplayOutputs_CognitiveMemory_MutationComma~",
                        column: x => x.MutationCommandId,
                        principalTable: "CognitiveMemory_MutationCommands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ReplayOutputs_CognitiveMemory_ReplayJobs_Re~",
                        column: x => x.ReplayJobId,
                        principalTable: "CognitiveMemory_ReplayJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ReplayOutputs_CognitiveMemory_ReviewItems_R~",
                        column: x => x.ReviewItemId,
                        principalTable: "CognitiveMemory_ReviewItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ReplayWorkerResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReplayJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    WorkerId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    InputHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OutputHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SourceScopeKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PolicyProfileId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    OutputSchema = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ResultStorageReference = table.Column<string>(type: "TEXT", nullable: false),
                    RejectionReason = table.Column<string>(type: "TEXT", nullable: false),
                    WarningsJson = table.Column<string>(type: "TEXT", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ReplayWorkerResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ReplayWorkerResults_CognitiveMemory_ReplayJ~",
                        column: x => x.ReplayJobId,
                        principalTable: "CognitiveMemory_ReplayJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_AttentionDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceFrameId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelfRegulationAssessmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnswerPostureDecisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionKind = table.Column<int>(type: "integer", nullable: false),
                    ReasonKind = table.Column<int>(type: "integer", nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RequestPreview = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    RoutingScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoutingBucket = table.Column<int>(type: "integer", nullable: false),
                    DisplayPriorityProjection = table.Column<double>(type: "double precision", nullable: true),
                    MatchedShapeCount = table.Column<int>(type: "integer", nullable: false),
                    MissingRequiredDimensionCount = table.Column<int>(type: "integer", nullable: false),
                    Explanation = table.Column<string>(type: "TEXT", nullable: false),
                    RequiredNextActionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_AttentionDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_AttentionDecisions_CognitiveMemory_ScoreEva~",
                        column: x => x.RoutingScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_AttentionDecisions_CognitiveMemory_Workspac~",
                        column: x => x.WorkspaceFrameId,
                        principalTable: "CognitiveMemory_WorkspaceFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_WorkspaceGoals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceFrameId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    ParentGoalId = table.Column<Guid>(type: "uuid", nullable: true),
                    GoalKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_WorkspaceGoals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceGoals_CognitiveMemory_WorkspaceFra~",
                        column: x => x.WorkspaceFrameId,
                        principalTable: "CognitiveMemory_WorkspaceFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_WorkspaceOpenQuestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceFrameId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionText = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResolvedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_WorkspaceOpenQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceOpenQuestions_CognitiveMemory_Work~",
                        column: x => x.WorkspaceFrameId,
                        principalTable: "CognitiveMemory_WorkspaceFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ExternalSourceIngestions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceKind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Locator = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ContentLength = table.Column<long>(type: "bigint", nullable: false),
                    ProgressPercent = table.Column<int>(type: "integer", nullable: false),
                    StatusMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    SourceManifestId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: true),
                    FailureMessage = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ExternalSourceIngestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ExternalSourceIngestions_CognitiveMemory_Ev~",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ExternalSourceIngestions_CognitiveMemory_So~",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ExternalSourceIngestions_CognitiveMemory_S~1",
                        column: x => x.SourceManifestId,
                        principalTable: "CognitiveMemory_SourceManifests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureSimulationEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SimulationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureSimulationEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureSimulationEvidence_CognitiveMemory~",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureSimulationEvidence_CognitiveMemor~1",
                        column: x => x.SimulationId,
                        principalTable: "CognitiveMemory_ProcedureSimulations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_EpisodeStepEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StepId = table.Column<Guid>(type: "uuid", nullable: false),
                    EpisodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceRole = table.Column<int>(type: "integer", nullable: false),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_EpisodeStepEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_EpisodeStepEvidence_CognitiveMemory_Episode~",
                        column: x => x.StepId,
                        principalTable: "CognitiveMemory_EpisodeSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_EpisodeStepEvidence_CognitiveMemory_Evidenc~",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Purpose = table.Column<string>(type: "TEXT", nullable: false),
                    Maturity = table.Column<int>(type: "integer", nullable: false),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    ValidationState = table.Column<int>(type: "integer", nullable: false),
                    AccessLevel = table.Column<int>(type: "integer", nullable: false),
                    SourceConsolidationCandidateId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastSuccessfulEpisodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    MaturityScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaturityBucket = table.Column<int>(type: "integer", nullable: false),
                    DisplayMaturityScore = table.Column<double>(type: "double precision", nullable: true),
                    PreconditionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    PostconditionsJson = table.Column<string>(type: "TEXT", nullable: false),
                    RequiredParticipantsJson = table.Column<string>(type: "TEXT", nullable: false),
                    RequiredToolKeysJson = table.Column<string>(type: "TEXT", nullable: false),
                    InputSchemaJson = table.Column<string>(type: "TEXT", nullable: false),
                    OutputSchemaJson = table.Column<string>(type: "TEXT", nullable: false),
                    StepCount = table.Column<int>(type: "integer", nullable: false),
                    FailureModeCount = table.Column<int>(type: "integer", nullable: false),
                    ValidationEvidenceCount = table.Column<int>(type: "integer", nullable: false),
                    AutomationBindingCount = table.Column<int>(type: "integer", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureSkills_CognitiveMemory_Consolidati~",
                        column: x => x.SourceConsolidationCandidateId,
                        principalTable: "CognitiveMemory_ConsolidationCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureSkills_CognitiveMemory_ScoreEvalua~",
                        column: x => x.MaturityScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureSkills_CognitiveMemory_TemporalEpi~",
                        column: x => x.LastSuccessfulEpisodeId,
                        principalTable: "CognitiveMemory_TemporalEpisodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_EntityAliases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityKind = table.Column<int>(type: "integer", nullable: false),
                    Alias = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    AliasKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_EntityAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_EntityAliases_CognitiveMemory_Entities_Enti~",
                        column: x => x.EntityId,
                        principalTable: "CognitiveMemory_Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_RecallTraces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    OperationMode = table.Column<int>(type: "integer", nullable: false),
                    RecallMode = table.Column<int>(type: "integer", nullable: false),
                    RequestedByActorId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PolicyProfileId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    WorkspaceFrameId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttentionDecisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SelfRegulationAssessmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnswerPostureDecisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnswerGateDecisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContextPackId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    IncludedRecordCount = table.Column<int>(type: "integer", nullable: false),
                    ExcludedRecordCount = table.Column<int>(type: "integer", nullable: false),
                    SelectedClaimCount = table.Column<int>(type: "integer", nullable: false),
                    SelectedEvidenceAnchorCount = table.Column<int>(type: "integer", nullable: false),
                    InhibitedCandidateCount = table.Column<int>(type: "integer", nullable: false),
                    LimitingBudget = table.Column<int>(type: "integer", nullable: true),
                    TraceJson = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_RecallTraces", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallTraces_CognitiveMemory_AttentionDecis~",
                        column: x => x.AttentionDecisionId,
                        principalTable: "CognitiveMemory_AttentionDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallTraces_CognitiveMemory_WorkspaceFrame~",
                        column: x => x.WorkspaceFrameId,
                        principalTable: "CognitiveMemory_WorkspaceFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureAutomationBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedureSkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    BindingKind = table.Column<int>(type: "integer", nullable: false),
                    BindingKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    RequiresHumanReview = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectionCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RejectionReason = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureAutomationBindings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureAutomationBindings_CognitiveMemory~",
                        column: x => x.ProcedureSkillId,
                        principalTable: "CognitiveMemory_ProcedureSkills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureAutomationBindings_CognitiveMemor~1",
                        column: x => x.ReviewItemId,
                        principalTable: "CognitiveMemory_ReviewItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureFailureModes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedureSkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    FailureKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Condition = table.Column<string>(type: "TEXT", nullable: false),
                    DetectionSignal = table.Column<string>(type: "TEXT", nullable: false),
                    LikelyCause = table.Column<string>(type: "TEXT", nullable: false),
                    Mitigation = table.Column<string>(type: "TEXT", nullable: false),
                    RollbackOrCompensation = table.Column<string>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureFailureModes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureFailureModes_CognitiveMemory_Proce~",
                        column: x => x.ProcedureSkillId,
                        principalTable: "CognitiveMemory_ProcedureSkills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureSimulationSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SimulationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedureSkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureSimulationSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureSimulationSkills_CognitiveMemory_P~",
                        column: x => x.ProcedureSkillId,
                        principalTable: "CognitiveMemory_ProcedureSkills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureSimulationSkills_CognitiveMemory_~1",
                        column: x => x.SimulationId,
                        principalTable: "CognitiveMemory_ProcedureSimulations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureSteps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedureSkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SequenceIndex = table.Column<int>(type: "integer", nullable: false),
                    Action = table.Column<string>(type: "TEXT", nullable: false),
                    RequiredInput = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedOutput = table.Column<string>(type: "TEXT", nullable: false),
                    ValidationCheck = table.Column<string>(type: "TEXT", nullable: false),
                    FailureHandling = table.Column<string>(type: "TEXT", nullable: false),
                    ToolBindingKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "integer", nullable: true),
                    RetryLimit = table.Column<int>(type: "integer", nullable: false),
                    IsRollbackStep = table.Column<bool>(type: "boolean", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureSteps_CognitiveMemory_ProcedureSki~",
                        column: x => x.ProcedureSkillId,
                        principalTable: "CognitiveMemory_ProcedureSkills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureValidationEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedureSkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceRole = table.Column<int>(type: "integer", nullable: false),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: false),
                    EpisodeId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureValidationEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureValidationEvidence_CognitiveMemory~",
                        column: x => x.EpisodeId,
                        principalTable: "CognitiveMemory_TemporalEpisodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureValidationEvidence_CognitiveMemor~1",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureValidationEvidence_CognitiveMemor~2",
                        column: x => x.ProcedureSkillId,
                        principalTable: "CognitiveMemory_ProcedureSkills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureValidationEvidence_CognitiveMemor~3",
                        column: x => x.ReviewItemId,
                        principalTable: "CognitiveMemory_ReviewItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_RecallContextPacks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecallTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceFrameId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    CharacterBudget = table.Column<int>(type: "integer", nullable: false),
                    RenderedCharacterCount = table.Column<int>(type: "integer", nullable: false),
                    SectionCount = table.Column<int>(type: "integer", nullable: false),
                    SourceRefCount = table.Column<int>(type: "integer", nullable: false),
                    WarningCount = table.Column<int>(type: "integer", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_RecallContextPacks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallContextPacks_CognitiveMemory_RecallTr~",
                        column: x => x.RecallTraceId,
                        principalTable: "CognitiveMemory_RecallTraces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallContextPacks_CognitiveMemory_Workspac~",
                        column: x => x.WorkspaceFrameId,
                        principalTable: "CognitiveMemory_WorkspaceFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_RecallTraceStages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecallTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    StageKind = table.Column<int>(type: "integer", nullable: false),
                    ChannelKind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CandidateCount = table.Column<int>(type: "integer", nullable: false),
                    SelectedCount = table.Column<int>(type: "integer", nullable: false),
                    ExcludedCount = table.Column<int>(type: "integer", nullable: false),
                    LimitingBudget = table.Column<int>(type: "integer", nullable: true),
                    ProviderTrace = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FailureMessage = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_RecallTraceStages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallTraceStages_CognitiveMemory_RecallTra~",
                        column: x => x.RecallTraceId,
                        principalTable: "CognitiveMemory_RecallTraces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SynthesizedRecalls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecallTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Brief = table.Column<string>(type: "TEXT", nullable: false),
                    ReferencesShownByDefault = table.Column<bool>(type: "boolean", nullable: false),
                    StatementCount = table.Column<int>(type: "integer", nullable: false),
                    SourceMapCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SynthesizedRecalls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SynthesizedRecalls_CognitiveMemory_RecallTr~",
                        column: x => x.RecallTraceId,
                        principalTable: "CognitiveMemory_RecallTraces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureFailureModeEpisodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedureFailureModeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedureSkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EpisodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureFailureModeEpisodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureFailureModeEpisodes_CognitiveMemor~",
                        column: x => x.EpisodeId,
                        principalTable: "CognitiveMemory_TemporalEpisodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureFailureModeEpisodes_CognitiveMemo~1",
                        column: x => x.ProcedureFailureModeId,
                        principalTable: "CognitiveMemory_ProcedureFailureModes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureStepEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedureStepId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedureSkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureStepEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureStepEvidence_CognitiveMemory_Evide~",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureStepEvidence_CognitiveMemory_Proce~",
                        column: x => x.ProcedureStepId,
                        principalTable: "CognitiveMemory_ProcedureSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SynthesizedStatements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SynthesisId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SynthesizedStatements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SynthesizedStatements_CognitiveMemory_Synth~",
                        column: x => x.SynthesisId,
                        principalTable: "CognitiveMemory_SynthesizedRecalls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_BeliefStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    StateKind = table.Column<int>(type: "integer", nullable: false),
                    ScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectionBucket = table.Column<int>(type: "integer", nullable: false),
                    DisplayBeliefScore = table.Column<double>(type: "double precision", nullable: true),
                    Explanation = table.Column<string>(type: "TEXT", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CalculatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_BeliefStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_BeliefStates_CognitiveMemory_ScoreEvaluatio~",
                        column: x => x.ScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ClaimEvidenceLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    Explanation = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ClaimEvidenceLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ClaimEvidenceLinks_CognitiveMemory_Evidence~",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_Claims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    MemoryRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimKind = table.Column<int>(type: "integer", nullable: false),
                    ClaimText = table.Column<string>(type: "TEXT", nullable: false),
                    SubjectKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    PredicateKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    PrimaryContextFrameId = table.Column<Guid>(type: "uuid", nullable: true),
                    ValidFromUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ValidToUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CurrentBeliefState = table.Column<int>(type: "integer", nullable: false),
                    CurrentBeliefScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentBeliefBucket = table.Column<int>(type: "integer", nullable: false),
                    DisplayBeliefScore = table.Column<double>(type: "double precision", nullable: true),
                    ValidationState = table.Column<int>(type: "integer", nullable: false),
                    StabilityState = table.Column<int>(type: "integer", nullable: false),
                    SupersedesClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_Claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Claims_CognitiveMemory_ContextFrames_Primar~",
                        column: x => x.PrimaryContextFrameId,
                        principalTable: "CognitiveMemory_ContextFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Claims_CognitiveMemory_ScoreEvaluations_Cur~",
                        column: x => x.CurrentBeliefScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_Records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Origin = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CanonicalText = table.Column<string>(type: "TEXT", nullable: false),
                    SummaryText = table.Column<string>(type: "TEXT", nullable: false),
                    TopicKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    ValidationState = table.Column<int>(type: "integer", nullable: false),
                    StabilityState = table.Column<int>(type: "integer", nullable: false),
                    CreatedInMode = table.Column<int>(type: "integer", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ContentHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceEvidenceCount = table.Column<int>(type: "integer", nullable: false),
                    EvidenceAnchorCount = table.Column<int>(type: "integer", nullable: false),
                    GeneratedReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PrimaryClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    PrimaryContextFrameId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfidenceScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActivationScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfidenceBucket = table.Column<int>(type: "integer", nullable: false),
                    ActivationBucket = table.Column<int>(type: "integer", nullable: false),
                    AccessLevel = table.Column<int>(type: "integer", nullable: false),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_Records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Records_CognitiveMemory_Claims_PrimaryClaim~",
                        column: x => x.PrimaryClaimId,
                        principalTable: "CognitiveMemory_Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Records_CognitiveMemory_ContextFrames_Prima~",
                        column: x => x.PrimaryContextFrameId,
                        principalTable: "CognitiveMemory_ContextFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Records_CognitiveMemory_ScoreEvaluations_Ac~",
                        column: x => x.ActivationScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Records_CognitiveMemory_ScoreEvaluations_Co~",
                        column: x => x.ConfidenceScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_PredictionExpectations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectationKind = table.Column<int>(type: "integer", nullable: false),
                    ActorKind = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PolicyProfileId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    WorkspaceFrameId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttentionDecisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    MemoryRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcedureSkillId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkflowRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProbeSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpectedContextKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ExpectedSourceSufficiency = table.Column<int>(type: "integer", nullable: false),
                    MinimumExpectedConfidence = table.Column<double>(type: "double precision", nullable: true),
                    MaximumExpectedConfidence = table.Column<double>(type: "double precision", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedOutcome = table.Column<string>(type: "TEXT", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_PredictionExpectations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionExpectations_CognitiveMemory_Atte~",
                        column: x => x.AttentionDecisionId,
                        principalTable: "CognitiveMemory_AttentionDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionExpectations_CognitiveMemory_Clai~",
                        column: x => x.ClaimId,
                        principalTable: "CognitiveMemory_Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionExpectations_CognitiveMemory_Reco~",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionExpectations_CognitiveMemory_Sour~",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionExpectations_CognitiveMemory_Work~",
                        column: x => x.WorkspaceFrameId,
                        principalTable: "CognitiveMemory_WorkspaceFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_Projections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    MemoryRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectionStoreKind = table.Column<int>(type: "integer", nullable: false),
                    ProjectionKind = table.Column<int>(type: "integer", nullable: false),
                    TargetProviderName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CollectionName = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    PointId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ProjectionProfileId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    EmbeddingProfileId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ProjectionSchemaVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    VectorDimensions = table.Column<int>(type: "integer", nullable: false),
                    SourceHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    SourceHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PayloadHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StaleReason = table.Column<int>(type: "integer", nullable: false),
                    RebuildRequired = table.Column<bool>(type: "boolean", nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FailureMessage = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastProjectedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_Projections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Projections_CognitiveMemory_Records_MemoryR~",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_QualityClusterMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClusterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    MemberKind = table.Column<int>(type: "integer", nullable: false),
                    MemoryRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccessLevel = table.Column<int>(type: "integer", nullable: false),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    ValidationState = table.Column<int>(type: "integer", nullable: false),
                    StabilityState = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_QualityClusterMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_QualityClusterMembers_CognitiveMemory_Evide~",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_QualityClusterMembers_CognitiveMemory_Quali~",
                        column: x => x.ClusterId,
                        principalTable: "CognitiveMemory_QualityClusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_QualityClusterMembers_CognitiveMemory_Recor~",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_QualityClusterMembers_CognitiveMemory_Sourc~",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_RecallCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecallTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    PrimaryChannelKind = table.Column<int>(type: "integer", nullable: false),
                    DecisionKind = table.Column<int>(type: "integer", nullable: false),
                    ExclusionReasonKind = table.Column<int>(type: "integer", nullable: false),
                    MemoryRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemoryKind = table.Column<int>(type: "integer", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkspaceFrameId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContextFrameId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScoreBucket = table.Column<int>(type: "integer", nullable: false),
                    DisplayRankProjection = table.Column<double>(type: "double precision", nullable: true),
                    HasSourceDetail = table.Column<bool>(type: "boolean", nullable: false),
                    SourceRedacted = table.Column<bool>(type: "boolean", nullable: false),
                    EstimatedTokenCount = table.Column<int>(type: "integer", nullable: false),
                    SourceRefCount = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    ChannelTraceJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_RecallCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallCandidates_CognitiveMemory_Claims_Cla~",
                        column: x => x.ClaimId,
                        principalTable: "CognitiveMemory_Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallCandidates_CognitiveMemory_ContextFra~",
                        column: x => x.ContextFrameId,
                        principalTable: "CognitiveMemory_ContextFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallCandidates_CognitiveMemory_EvidenceAn~",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallCandidates_CognitiveMemory_RecallTrac~",
                        column: x => x.RecallTraceId,
                        principalTable: "CognitiveMemory_RecallTraces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallCandidates_CognitiveMemory_Records_Me~",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallCandidates_CognitiveMemory_ScoreEvalu~",
                        column: x => x.ScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallCandidates_CognitiveMemory_SourceItem~",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallCandidates_CognitiveMemory_WorkspaceF~",
                        column: x => x.WorkspaceFrameId,
                        principalTable: "CognitiveMemory_WorkspaceFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_RecallContextSections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextPackId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecallTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionKind = table.Column<int>(type: "integer", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    SectionKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    MemoryRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccessLevel = table.Column<int>(type: "integer", nullable: false),
                    RedactionState = table.Column<int>(type: "integer", nullable: false),
                    EstimatedTokenCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_RecallContextSections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallContextSections_CognitiveMemory_Claim~",
                        column: x => x.ClaimId,
                        principalTable: "CognitiveMemory_Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallContextSections_CognitiveMemory_Recal~",
                        column: x => x.ContextPackId,
                        principalTable: "CognitiveMemory_RecallContextPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallContextSections_CognitiveMemory_Reca~1",
                        column: x => x.RecallTraceId,
                        principalTable: "CognitiveMemory_RecallTraces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallContextSections_CognitiveMemory_Recor~",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallContextSections_CognitiveMemory_Sourc~",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_RecallSourceRefs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecallTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextPackId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    MemoryRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceSystem = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Locator = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    QuoteHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    AccessLevel = table.Column<int>(type: "integer", nullable: false),
                    RedactionState = table.Column<int>(type: "integer", nullable: false),
                    IncludedInContext = table.Column<bool>(type: "boolean", nullable: false),
                    ExclusionReasonKind = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_RecallSourceRefs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallSourceRefs_CognitiveMemory_Claims_Cla~",
                        column: x => x.ClaimId,
                        principalTable: "CognitiveMemory_Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallSourceRefs_CognitiveMemory_EvidenceAn~",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallSourceRefs_CognitiveMemory_RecallCont~",
                        column: x => x.ContextPackId,
                        principalTable: "CognitiveMemory_RecallContextPacks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallSourceRefs_CognitiveMemory_RecallTrac~",
                        column: x => x.RecallTraceId,
                        principalTable: "CognitiveMemory_RecallTraces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallSourceRefs_CognitiveMemory_Records_Me~",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecallSourceRefs_CognitiveMemory_SourceItem~",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_RecordEvidenceAnchors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MemoryRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceRole = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_RecordEvidenceAnchors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecordEvidenceAnchors_CognitiveMemory_Evide~",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecordEvidenceAnchors_CognitiveMemory_Recor~",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_Relations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceMemoryRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetMemoryRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationKind = table.Column<int>(type: "integer", nullable: false),
                    EvidenceCount = table.Column<int>(type: "integer", nullable: false),
                    RelationScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: true),
                    RelationBucket = table.Column<int>(type: "integer", nullable: false),
                    DisplayStrengthProjection = table.Column<double>(type: "double precision", nullable: true),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_Relations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Relations_CognitiveMemory_Records_SourceMem~",
                        column: x => x.SourceMemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Relations_CognitiveMemory_Records_TargetMem~",
                        column: x => x.TargetMemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Relations_CognitiveMemory_ScoreEvaluations_~",
                        column: x => x.RelationScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SourceLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MemoryRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceManifestId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceRole = table.Column<int>(type: "integer", nullable: false),
                    Locator = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    QuoteHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SourceLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SourceLinks_CognitiveMemory_Records_MemoryR~",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SourceLinks_CognitiveMemory_SourceItems_Sou~",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SourceLinks_CognitiveMemory_SourceManifests~",
                        column: x => x.SourceManifestId,
                        principalTable: "CognitiveMemory_SourceManifests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_WorkspaceFocusSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceFrameId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlotKind = table.Column<int>(type: "integer", nullable: false),
                    MemoryRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcedureSkillId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecallTraceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProbeTurnId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkflowArtifactId = table.Column<Guid>(type: "uuid", nullable: true),
                    OpenQuestionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalPlaceholderKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    AttentionScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttentionBucket = table.Column<int>(type: "integer", nullable: false),
                    DisplayAttentionScore = table.Column<double>(type: "double precision", nullable: true),
                    SourceSufficiency = table.Column<int>(type: "integer", nullable: false),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    ConfidenceBucket = table.Column<int>(type: "integer", nullable: false),
                    StalenessBucket = table.Column<int>(type: "integer", nullable: false),
                    InclusionReasonKind = table.Column<int>(type: "integer", nullable: false),
                    InclusionReason = table.Column<string>(type: "TEXT", nullable: false),
                    RelationToActiveGoal = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CompressionSummary = table.Column<string>(type: "TEXT", nullable: false),
                    EstimatedTokenCount = table.Column<int>(type: "integer", nullable: false),
                    EstimatedSectionCount = table.Column<int>(type: "integer", nullable: false),
                    EstimatedDetailCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_WorkspaceFocusSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceFocusSlots_CognitiveMemory_Claims_~",
                        column: x => x.ClaimId,
                        principalTable: "CognitiveMemory_Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceFocusSlots_CognitiveMemory_RecallT~",
                        column: x => x.RecallTraceId,
                        principalTable: "CognitiveMemory_RecallTraces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceFocusSlots_CognitiveMemory_Records~",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceFocusSlots_CognitiveMemory_ScoreEv~",
                        column: x => x.AttentionScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceFocusSlots_CognitiveMemory_SourceI~",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceFocusSlots_CognitiveMemory_Workspa~",
                        column: x => x.OpenQuestionId,
                        principalTable: "CognitiveMemory_WorkspaceOpenQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceFocusSlots_CognitiveMemory_Worksp~1",
                        column: x => x.WorkspaceFrameId,
                        principalTable: "CognitiveMemory_WorkspaceFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_WorkspaceInhibitedCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceFrameId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateKind = table.Column<int>(type: "integer", nullable: false),
                    MemoryRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExternalCandidateKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ReasonKind = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    InhibitionScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: true),
                    InhibitionBucket = table.Column<int>(type: "integer", nullable: false),
                    DisplayRelevanceScore = table.Column<double>(type: "double precision", nullable: true),
                    DisplayInhibitionStrength = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_WorkspaceInhibitedCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceInhibitedCandidates_CognitiveMemor~",
                        column: x => x.ClaimId,
                        principalTable: "CognitiveMemory_Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceInhibitedCandidates_CognitiveMemo~1",
                        column: x => x.InhibitionScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceInhibitedCandidates_CognitiveMemo~2",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceInhibitedCandidates_CognitiveMemo~3",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceInhibitedCandidates_CognitiveMemo~4",
                        column: x => x.WorkspaceFrameId,
                        principalTable: "CognitiveMemory_WorkspaceFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_PredictionErrors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    PredictionExpectationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ErrorKind = table.Column<int>(type: "integer", nullable: false),
                    ActorKind = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PolicyProfileId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    WorkspaceFrameId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttentionDecisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    MemoryRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcedureSkillId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkflowRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProbeTurnId = table.Column<Guid>(type: "uuid", nullable: true),
                    SeverityScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SeverityBucket = table.Column<int>(type: "integer", nullable: false),
                    DisplaySeverityProjection = table.Column<double>(type: "double precision", nullable: true),
                    SeverityComponentCount = table.Column<int>(type: "integer", nullable: false),
                    MatchedShapeCount = table.Column<int>(type: "integer", nullable: false),
                    MissingRequiredDimensionCount = table.Column<int>(type: "integer", nullable: false),
                    ObservationSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ObservedSummary = table.Column<string>(type: "TEXT", nullable: false),
                    CauseHypothesis = table.Column<string>(type: "TEXT", nullable: false),
                    SuggestedActionKind = table.Column<int>(type: "integer", nullable: false),
                    SuggestedAction = table.Column<string>(type: "TEXT", nullable: false),
                    RequiresReview = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedSignalCount = table.Column<int>(type: "integer", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_PredictionErrors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrors_CognitiveMemory_AttentionD~",
                        column: x => x.AttentionDecisionId,
                        principalTable: "CognitiveMemory_AttentionDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrors_CognitiveMemory_Claims_Cla~",
                        column: x => x.ClaimId,
                        principalTable: "CognitiveMemory_Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrors_CognitiveMemory_Prediction~",
                        column: x => x.PredictionExpectationId,
                        principalTable: "CognitiveMemory_PredictionExpectations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrors_CognitiveMemory_Records_Me~",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrors_CognitiveMemory_ScoreEvalu~",
                        column: x => x.SeverityScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrors_CognitiveMemory_SourceItem~",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrors_CognitiveMemory_WorkspaceF~",
                        column: x => x.WorkspaceFrameId,
                        principalTable: "CognitiveMemory_WorkspaceFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_PredictionExpectationEvidenceAnchors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PredictionExpectationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_PredictionExpectationEvidenceAnchors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionExpectationEvidenceAnchors_Cognit~",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionExpectationEvidenceAnchors_Cogni~1",
                        column: x => x.PredictionExpectationId,
                        principalTable: "CognitiveMemory_PredictionExpectations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_RelationEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_RelationEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RelationEvidence_CognitiveMemory_EvidenceAn~",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RelationEvidence_CognitiveMemory_Relations_~",
                        column: x => x.RelationId,
                        principalTable: "CognitiveMemory_Relations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_WorkspaceSlotEvidenceAnchors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceSlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkspaceFrameId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_WorkspaceSlotEvidenceAnchors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceSlotEvidenceAnchors_CognitiveMemor~",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceSlotEvidenceAnchors_CognitiveMemo~1",
                        column: x => x.WorkspaceFrameId,
                        principalTable: "CognitiveMemory_WorkspaceFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_WorkspaceSlotEvidenceAnchors_CognitiveMemo~2",
                        column: x => x.WorkspaceSlotId,
                        principalTable: "CognitiveMemory_WorkspaceFocusSlots",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_EpisodeCausalLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EpisodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkKind = table.Column<int>(type: "integer", nullable: false),
                    FromStepId = table.Column<Guid>(type: "uuid", nullable: true),
                    ToStepId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    PredictionErrorId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcedureSkillId = table.Column<Guid>(type: "uuid", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_EpisodeCausalLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_EpisodeCausalLinks_CognitiveMemory_Claims_C~",
                        column: x => x.ClaimId,
                        principalTable: "CognitiveMemory_Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_EpisodeCausalLinks_CognitiveMemory_EpisodeS~",
                        column: x => x.FromStepId,
                        principalTable: "CognitiveMemory_EpisodeSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_EpisodeCausalLinks_CognitiveMemory_Episode~1",
                        column: x => x.ToStepId,
                        principalTable: "CognitiveMemory_EpisodeSteps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_EpisodeCausalLinks_CognitiveMemory_Evidence~",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_EpisodeCausalLinks_CognitiveMemory_Predicti~",
                        column: x => x.PredictionErrorId,
                        principalTable: "CognitiveMemory_PredictionErrors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_EpisodeCausalLinks_CognitiveMemory_Temporal~",
                        column: x => x.EpisodeId,
                        principalTable: "CognitiveMemory_TemporalEpisodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_PredictionErrorEvidenceAnchors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PredictionErrorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_PredictionErrorEvidenceAnchors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrorEvidenceAnchors_CognitiveMem~",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrorEvidenceAnchors_CognitiveMe~1",
                        column: x => x.PredictionErrorId,
                        principalTable: "CognitiveMemory_PredictionErrors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProcedureFailureModePredictionErrors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedureFailureModeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcedureSkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    PredictionErrorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProcedureFailureModePredictionErrors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureFailureModePredictionErrors_Cognit~",
                        column: x => x.PredictionErrorId,
                        principalTable: "CognitiveMemory_PredictionErrors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ProcedureFailureModePredictionErrors_Cogni~1",
                        column: x => x.ProcedureFailureModeId,
                        principalTable: "CognitiveMemory_ProcedureFailureModes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ReplayJobPredictionErrors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReplayJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    PredictionErrorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ReplayJobPredictionErrors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ReplayJobPredictionErrors_CognitiveMemory_P~",
                        column: x => x.PredictionErrorId,
                        principalTable: "CognitiveMemory_PredictionErrors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ReplayJobPredictionErrors_CognitiveMemory_R~",
                        column: x => x.ReplayJobId,
                        principalTable: "CognitiveMemory_ReplayJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_Signals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    SignalKind = table.Column<int>(type: "integer", nullable: false),
                    SourceKind = table.Column<int>(type: "integer", nullable: false),
                    ActorKind = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PolicyProfileId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    AccessLevel = table.Column<int>(type: "integer", nullable: false),
                    RedactionState = table.Column<int>(type: "integer", nullable: false),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    RequiresReview = table.Column<bool>(type: "boolean", nullable: false),
                    WorkspaceFrameId = table.Column<Guid>(type: "uuid", nullable: true),
                    AttentionDecisionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PredictionErrorId = table.Column<Guid>(type: "uuid", nullable: true),
                    MemoryRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcedureSkillId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkflowRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProbeTurnId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    SignalScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScoreSchemaVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizationProfileId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ComponentCount = table.Column<int>(type: "integer", nullable: false),
                    MatchedShapeCount = table.Column<int>(type: "integer", nullable: false),
                    MissingRequiredDimensionCount = table.Column<int>(type: "integer", nullable: false),
                    DisplayMagnitudeProjection = table.Column<double>(type: "double precision", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_Signals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Signals_CognitiveMemory_AttentionDecisions_~",
                        column: x => x.AttentionDecisionId,
                        principalTable: "CognitiveMemory_AttentionDecisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Signals_CognitiveMemory_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "CognitiveMemory_Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Signals_CognitiveMemory_PredictionErrors_Pr~",
                        column: x => x.PredictionErrorId,
                        principalTable: "CognitiveMemory_PredictionErrors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Signals_CognitiveMemory_Records_MemoryRecor~",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Signals_CognitiveMemory_ScoreEvaluations_Si~",
                        column: x => x.SignalScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Signals_CognitiveMemory_SourceItems_SourceI~",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Signals_CognitiveMemory_WorkspaceFrames_Wor~",
                        column: x => x.WorkspaceFrameId,
                        principalTable: "CognitiveMemory_WorkspaceFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_PredictionErrorSignals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PredictionErrorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CognitiveSignalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_PredictionErrorSignals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrorSignals_CognitiveMemory_Pred~",
                        column: x => x.PredictionErrorId,
                        principalTable: "CognitiveMemory_PredictionErrors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_PredictionErrorSignals_CognitiveMemory_Sign~",
                        column: x => x.CognitiveSignalId,
                        principalTable: "CognitiveMemory_Signals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ReplayJobSignals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ReplayJobId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CognitiveSignalId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ReplayJobSignals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ReplayJobSignals_CognitiveMemory_ReplayJobs~",
                        column: x => x.ReplayJobId,
                        principalTable: "CognitiveMemory_ReplayJobs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ReplayJobSignals_CognitiveMemory_Signals_Co~",
                        column: x => x.CognitiveSignalId,
                        principalTable: "CognitiveMemory_Signals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SignalConsumerPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CognitiveSignalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumerKind = table.Column<int>(type: "integer", nullable: false),
                    MaximumAccessLevel = table.Column<int>(type: "integer", nullable: false),
                    RequiresReviewBeforeAction = table.Column<bool>(type: "boolean", nullable: false),
                    CanCreateTruthDirectly = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SignalConsumerPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SignalConsumerPolicies_CognitiveMemory_Sign~",
                        column: x => x.CognitiveSignalId,
                        principalTable: "CognitiveMemory_Signals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SignalEvidenceAnchors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CognitiveSignalId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SignalEvidenceAnchors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SignalEvidenceAnchors_CognitiveMemory_Evide~",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SignalEvidenceAnchors_CognitiveMemory_Signa~",
                        column: x => x.CognitiveSignalId,
                        principalTable: "CognitiveMemory_Signals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_DreamAggregateCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DreamRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClusterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SummaryText = table.Column<string>(type: "TEXT", nullable: false),
                    CanonicalText = table.Column<string>(type: "TEXT", nullable: false),
                    AccessLevel = table.Column<int>(type: "integer", nullable: false),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PayloadHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ValidationRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    MemoryRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimCount = table.Column<int>(type: "integer", nullable: false),
                    SourceMapCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_DreamAggregateCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_DreamAggregateCandidates_CognitiveMemory_Dr~",
                        column: x => x.DreamRunId,
                        principalTable: "CognitiveMemory_DreamRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_DreamAggregateCandidates_CognitiveMemory_Qu~",
                        column: x => x.ClusterId,
                        principalTable: "CognitiveMemory_QualityClusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_DreamAggregateCandidates_CognitiveMemory_Re~",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_DreamAggregateCandidates_CognitiveMemory_R~1",
                        column: x => x.ReviewItemId,
                        principalTable: "CognitiveMemory_ReviewItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_DreamAggregateClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AggregateCandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    ClaimKind = table.Column<int>(type: "integer", nullable: false),
                    ClaimText = table.Column<string>(type: "TEXT", nullable: false),
                    SubjectKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    PredicateKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_DreamAggregateClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_DreamAggregateClaims_CognitiveMemory_DreamA~",
                        column: x => x.AggregateCandidateId,
                        principalTable: "CognitiveMemory_DreamAggregateCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_DreamValidations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AggregateCandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Decision = table.Column<int>(type: "integer", nullable: false),
                    PolicyProfileId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IssueCount = table.Column<int>(type: "integer", nullable: false),
                    ClaimsChecked = table.Column<int>(type: "integer", nullable: false),
                    SourceMapsChecked = table.Column<int>(type: "integer", nullable: false),
                    IssuesJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_DreamValidations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_DreamValidations_CognitiveMemory_DreamAggre~",
                        column: x => x.AggregateCandidateId,
                        principalTable: "CognitiveMemory_DreamAggregateCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_DreamAggregateClaimSourceMaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AggregateCandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    AggregateClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceMemoryRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    AccessLevel = table.Column<int>(type: "integer", nullable: false),
                    RedactionState = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_DreamAggregateClaimSourceMaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_DreamAggregateClaimSourceMaps_CognitiveMemo~",
                        column: x => x.AggregateCandidateId,
                        principalTable: "CognitiveMemory_DreamAggregateCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_DreamAggregateClaimSourceMaps_CognitiveMem~1",
                        column: x => x.AggregateClaimId,
                        principalTable: "CognitiveMemory_DreamAggregateClaims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_DreamAggregateClaimSourceMaps_CognitiveMem~2",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_DreamAggregateClaimSourceMaps_CognitiveMem~3",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_DreamAggregateClaimSourceMaps_CognitiveMem~4",
                        column: x => x.SourceMemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SynthesizedStatementSourceMaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SynthesisId = table.Column<Guid>(type: "uuid", nullable: false),
                    StatementId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemoryRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    AggregateClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceSystem = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Locator = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    AccessLevel = table.Column<int>(type: "integer", nullable: false),
                    RedactionState = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SynthesizedStatementSourceMaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMem~",
                        column: x => x.AggregateClaimId,
                        principalTable: "CognitiveMemory_DreamAggregateClaims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMe~1",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMe~2",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMe~3",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMe~4",
                        column: x => x.StatementId,
                        principalTable: "CognitiveMemory_SynthesizedStatements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMe~5",
                        column: x => x.SynthesisId,
                        principalTable: "CognitiveMemory_SynthesizedRecalls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Processes_ArtifactExpectations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StepDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactKind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    TrustRequirement = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    SensitivityLevel = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    RetentionDays = table.Column<int>(type: "integer", nullable: false),
                    AllowedFutureUsageSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ValidationRequirementSummary = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_ArtifactExpectations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Processes_ArtifactRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ArtifactExpectationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ArtifactKind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    TrustStatus = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    SensitivityLevel = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    ProvenanceSummary = table.Column<string>(type: "TEXT", nullable: false),
                    AllowedFutureUsageSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ReviewSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ManagedStoragePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ExternalReferenceKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_ArtifactRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_ArtifactRecords_Processes_ArtifactExpectations_Ar~",
                        column: x => x.ArtifactExpectationId,
                        principalTable: "Processes_ArtifactExpectations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Processes_ConformanceObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    Severity = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Category = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Observation = table.Column<string>(type: "TEXT", nullable: false),
                    DeviationReason = table.Column<string>(type: "TEXT", nullable: false),
                    IsSafeNonAction = table.Column<bool>(type: "boolean", nullable: false),
                    ContainsSensitiveAssessment = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_ConformanceObservations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Processes_DecisionRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionKind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "TEXT", nullable: false),
                    PolicyEvaluation = table.Column<string>(type: "TEXT", nullable: false),
                    BranchOutcomeId = table.Column<Guid>(type: "uuid", nullable: true),
                    BranchOutcomeTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DecidedBy = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    OperatingMode = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_DecisionRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Processes_Definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    ValueStatement = table.Column<string>(type: "TEXT", nullable: false),
                    CustomerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OwnerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    InterfaceContractSummary = table.Column<string>(type: "TEXT", nullable: false),
                    GovernanceNotes = table.Column<string>(type: "TEXT", nullable: false),
                    Criticality = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    AutonomyLevel = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    ActivePublishedVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    NextVersionNumber = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_Definitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Processes_DefinitionVersions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    ChangeSummary = table.Column<string>(type: "TEXT", nullable: false),
                    GovernancePolicySummary = table.Column<string>(type: "TEXT", nullable: false),
                    ConstitutionRuleSummary = table.Column<string>(type: "TEXT", nullable: false),
                    OperatingModeSummary = table.Column<string>(type: "TEXT", nullable: false),
                    SimulationReadinessSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ManagerAgentOverrideId = table.Column<Guid>(type: "uuid", nullable: true),
                    ManagerAgentOverrideName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ImportedFrom = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ImportWarnings = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    PublishedBy = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_DefinitionVersions", x => x.Id);
                    table.UniqueConstraint("AK_Processes_DefinitionVersions_ProcessDefinitionId_Id", x => new { x.ProcessDefinitionId, x.Id });
                    table.ForeignKey(
                        name: "FK_Processes_DefinitionVersions_Processes_Definitions_ProcessD~",
                        column: x => x.ProcessDefinitionId,
                        principalTable: "Processes_Definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Processes_RoleRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessDefinitionVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Purpose = table.Column<string>(type: "TEXT", nullable: false),
                    StaffingIntent = table.Column<string>(type: "TEXT", nullable: false),
                    PreferredExecutorKind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PreferredWorkflowDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreferredWorkflowVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PreferredProjectAssignmentRole = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsFallback = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresExplicitApproval = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultAllocationPercent = table.Column<int>(type: "integer", nullable: false),
                    RoleTemplateSourceKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RoleTemplateSnapshotName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SnapshotSummary = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CanvasX = table.Column<double>(type: "double precision", nullable: false),
                    CanvasY = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_RoleRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_RoleRequirements_Processes_DefinitionVersions_Pro~",
                        column: x => x.ProcessDefinitionVersionId,
                        principalTable: "Processes_DefinitionVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Processes_RoleMessagingPolicies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessDefinitionVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceRoleRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetRoleRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_RoleMessagingPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_RoleMessagingPolicies_Processes_DefinitionVersion~",
                        column: x => x.ProcessDefinitionVersionId,
                        principalTable: "Processes_DefinitionVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Processes_RoleMessagingPolicies_Processes_RoleRequirements_~",
                        column: x => x.SourceRoleRequirementId,
                        principalTable: "Processes_RoleRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_RoleMessagingPolicies_Processes_RoleRequirements~1",
                        column: x => x.TargetRoleRequirementId,
                        principalTable: "Processes_RoleRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Processes_RoleSkillRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    SkillId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    MinimumYearsExperience = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_RoleSkillRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_RoleSkillRequirements_Processes_RoleRequirements_~",
                        column: x => x.RoleRequirementId,
                        principalTable: "Processes_RoleRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Processes_StepDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessDefinitionVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Subtitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    StepKind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    SubprocessDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    SubprocessDefinitionSnapshotName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AllowsManualSkip = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsSafeRefusal = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresDecisionRecord = table.Column<bool>(type: "boolean", nullable: false),
                    InputContractSummary = table.Column<string>(type: "TEXT", nullable: false),
                    OutputContractSummary = table.Column<string>(type: "TEXT", nullable: false),
                    EvidenceContractSummary = table.Column<string>(type: "TEXT", nullable: false),
                    DecisionRightsSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ExceptionPolicySummary = table.Column<string>(type: "TEXT", nullable: false),
                    TargetLeadHours = table.Column<int>(type: "integer", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    DecisionRoleRequirementId = table.Column<Guid>(type: "uuid", nullable: true),
                    CanvasX = table.Column<double>(type: "double precision", nullable: false),
                    CanvasY = table.Column<double>(type: "double precision", nullable: false),
                    BranchCanvasX = table.Column<double>(type: "double precision", nullable: false),
                    BranchCanvasY = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_StepDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_StepDefinitions_Processes_DefinitionVersions_Proc~",
                        column: x => x.ProcessDefinitionVersionId,
                        principalTable: "Processes_DefinitionVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Processes_StepDefinitions_Processes_Definitions_SubprocessD~",
                        column: x => x.SubprocessDefinitionId,
                        principalTable: "Processes_Definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_StepDefinitions_Processes_RoleRequirements_Decisi~",
                        column: x => x.DecisionRoleRequirementId,
                        principalTable: "Processes_RoleRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Processes_StepArtifactInputs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StepDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactExpectationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_StepArtifactInputs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_StepArtifactInputs_Processes_ArtifactExpectations~",
                        column: x => x.ArtifactExpectationId,
                        principalTable: "Processes_ArtifactExpectations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_StepArtifactInputs_Processes_StepDefinitions_Step~",
                        column: x => x.StepDefinitionId,
                        principalTable: "Processes_StepDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Processes_StepBranchOutcomes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StepDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_StepBranchOutcomes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_StepBranchOutcomes_Processes_StepDefinitions_Step~",
                        column: x => x.StepDefinitionId,
                        principalTable: "Processes_StepDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Processes_StepRoleRequirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StepDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResponsibilityKind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    FallbackOrder = table.Column<int>(type: "integer", nullable: false),
                    RebindPolicySummary = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_StepRoleRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_StepRoleRequirements_Processes_RoleRequirements_R~",
                        column: x => x.RoleRequirementId,
                        principalTable: "Processes_RoleRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_StepRoleRequirements_Processes_StepDefinitions_St~",
                        column: x => x.StepDefinitionId,
                        principalTable: "Processes_StepDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Processes_StepDependencies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StepDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DependsOnStepId = table.Column<Guid>(type: "uuid", nullable: false),
                    DependsOnBranchOutcomeId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_StepDependencies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_StepDependencies_Processes_StepBranchOutcomes_Dep~",
                        column: x => x.DependsOnBranchOutcomeId,
                        principalTable: "Processes_StepBranchOutcomes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_StepDependencies_Processes_StepDefinitions_Depend~",
                        column: x => x.DependsOnStepId,
                        principalTable: "Processes_StepDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_StepDependencies_Processes_StepDefinitions_StepDe~",
                        column: x => x.StepDefinitionId,
                        principalTable: "Processes_StepDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Processes_ImprovementCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ProblemSummary = table.Column<string>(type: "TEXT", nullable: false),
                    EvidenceSummary = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    IsTrainingOpportunity = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresGovernanceReview = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_ImprovementCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_ImprovementCandidates_Processes_Definitions_Proce~",
                        column: x => x.ProcessDefinitionId,
                        principalTable: "Processes_Definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Processes_JournalEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    EventType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    OperatingMode = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    PolicyVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EnvironmentMode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ReplayContextJson = table.Column<string>(type: "TEXT", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_JournalEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Processes_LaunchApprovals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LaunchPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    ApproverPartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    ApproverDisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ApproverKind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    HumanSubstitutePartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    HumanSubstituteName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CollaborationThreadId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestMessage = table.Column<string>(type: "TEXT", nullable: false),
                    ResolutionSummary = table.Column<string>(type: "TEXT", nullable: false),
                    DecidedBy = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_LaunchApprovals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Processes_LaunchCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LaunchPlanRoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CandidateKind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    TechnicalAgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExecutorKind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkflowVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    Score = table.Column<decimal>(type: "numeric", nullable: false),
                    IsRecommended = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsDirectMessaging = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresProvisioning = table.Column<bool>(type: "boolean", nullable: false),
                    RecommendationSummary = table.Column<string>(type: "TEXT", nullable: false),
                    AvailabilitySummary = table.Column<string>(type: "TEXT", nullable: false),
                    SourceRegistryKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_LaunchCandidates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Processes_LaunchPlanRoles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LaunchPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PreferredExecutorKind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RequiredSkillIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    RecommendationSummary = table.Column<string>(type: "TEXT", nullable: false),
                    SelectionSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ReadinessSummary = table.Column<string>(type: "TEXT", nullable: false),
                    SelectedCandidateId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsRequired = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresExplicitApproval = table.Column<bool>(type: "boolean", nullable: false),
                    RequiresProvisioning = table.Column<bool>(type: "boolean", nullable: false),
                    IsResolved = table.Column<bool>(type: "boolean", nullable: false),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_LaunchPlanRoles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_LaunchPlanRoles_Processes_RoleRequirements_RoleRe~",
                        column: x => x.RoleRequirementId,
                        principalTable: "Processes_RoleRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Processes_LaunchPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessDefinitionVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OperatingMode = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    TriggerReason = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    RecommendationStrategy = table.Column<string>(type: "TEXT", nullable: false),
                    FallbackStrategy = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    ApprovalThreadId = table.Column<Guid>(type: "uuid", nullable: true),
                    LatestApprovalRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    GeneratedRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestedBy = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ApprovedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExecutedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_LaunchPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_LaunchPlans_Processes_DefinitionVersions_ProcessD~",
                        columns: x => new { x.ProcessDefinitionId, x.ProcessDefinitionVersionId },
                        principalTable: "Processes_DefinitionVersions",
                        principalColumns: new[] { "ProcessDefinitionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_LaunchPlans_Processes_Definitions_ProcessDefiniti~",
                        column: x => x.ProcessDefinitionId,
                        principalTable: "Processes_Definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Processes_LaunchProvisioningRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LaunchPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    LaunchPlanRoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectedCandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    RequestKind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequestPayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    ResultPartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResultTechnicalAgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResultSummary = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_LaunchProvisioningRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_LaunchProvisioningRequests_Processes_LaunchCandid~",
                        column: x => x.SelectedCandidateId,
                        principalTable: "Processes_LaunchCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_LaunchProvisioningRequests_Processes_LaunchPlanRo~",
                        column: x => x.LaunchPlanRoleId,
                        principalTable: "Processes_LaunchPlanRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_LaunchProvisioningRequests_Processes_LaunchPlans_~",
                        column: x => x.LaunchPlanId,
                        principalTable: "Processes_LaunchPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Processes_RunAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleRequirementId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExecutorKind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkflowVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    BindingReason = table.Column<string>(type: "TEXT", nullable: false),
                    SourceRegistryKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SnapshotSummary = table.Column<string>(type: "TEXT", nullable: false),
                    IsFallback = table.Column<bool>(type: "boolean", nullable: false),
                    IsCapabilityGap = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsDirectMessaging = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_RunAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_RunAssignments_Processes_RoleRequirements_RoleReq~",
                        column: x => x.RoleRequirementId,
                        principalTable: "Processes_RoleRequirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_RunAssignments_Processes_StepDefinitions_StepDefi~",
                        column: x => x.StepDefinitionId,
                        principalTable: "Processes_StepDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Processes_Runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessDefinitionVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentStepRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    RootRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    HierarchyDepth = table.Column<int>(type: "integer", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    OperatingMode = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    TriggerReason = table.Column<string>(type: "TEXT", nullable: false),
                    GovernanceSnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    PolicySnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    ExecutorSnapshotSummary = table.Column<string>(type: "TEXT", nullable: false),
                    ManagerAgentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ManagerAgentName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReplayPackageKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EstimatedCost = table.Column<decimal>(type: "numeric", nullable: false),
                    ActualCost = table.Column<decimal>(type: "numeric", nullable: false),
                    FirstTimeRightPercent = table.Column<int>(type: "integer", nullable: false),
                    SlaAttainmentPercent = table.Column<int>(type: "integer", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_Runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_Runs_Processes_DefinitionVersions_ProcessDefiniti~",
                        columns: x => new { x.ProcessDefinitionId, x.ProcessDefinitionVersionId },
                        principalTable: "Processes_DefinitionVersions",
                        principalColumns: new[] { "ProcessDefinitionId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_Runs_Processes_Definitions_ProcessDefinitionId",
                        column: x => x.ProcessDefinitionId,
                        principalTable: "Processes_Definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Processes_Runs_Processes_Runs_ParentRunId",
                        column: x => x.ParentRunId,
                        principalTable: "Processes_Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Processes_StepRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StepKind = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Status = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    RoleSnapshotSummary = table.Column<string>(type: "TEXT", nullable: false),
                    CurrentExecutorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CurrentExecutorPartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionSummary = table.Column<string>(type: "TEXT", nullable: false),
                    BlockedReason = table.Column<string>(type: "TEXT", nullable: false),
                    RefusalReason = table.Column<string>(type: "TEXT", nullable: false),
                    ExceptionSummary = table.Column<string>(type: "TEXT", nullable: false),
                    InputQualitySummary = table.Column<string>(type: "TEXT", nullable: false),
                    SelectedBranchOutcomeId = table.Column<Guid>(type: "uuid", nullable: true),
                    SelectedBranchOutcomeTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ReadyAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    WaitMinutes = table.Column<int>(type: "integer", nullable: false),
                    TouchMinutes = table.Column<int>(type: "integer", nullable: false),
                    BlockedMinutes = table.Column<int>(type: "integer", nullable: false),
                    ReworkCount = table.Column<int>(type: "integer", nullable: false),
                    CapabilityGapSeverity = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_StepRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_StepRuns_Processes_Runs_ProcessRunId",
                        column: x => x.ProcessRunId,
                        principalTable: "Processes_Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Processes_StepRuns_Processes_StepBranchOutcomes_SelectedBra~",
                        column: x => x.SelectedBranchOutcomeId,
                        principalTable: "Processes_StepBranchOutcomes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Processes_StepRuns_Processes_StepDefinitions_StepDefinition~",
                        column: x => x.StepDefinitionId,
                        principalTable: "Processes_StepDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Processes_WorkBriefs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    WorkBriefText = table.Column<string>(type: "TEXT", nullable: false),
                    HandoffSummary = table.Column<string>(type: "TEXT", nullable: false),
                    AssignmentReason = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedOutcome = table.Column<string>(type: "TEXT", nullable: false),
                    EvidenceExpectationSummary = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_WorkBriefs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_WorkBriefs_Processes_Runs_ProcessRunId",
                        column: x => x.ProcessRunId,
                        principalTable: "Processes_Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Processes_WorkBriefs_Processes_StepRuns_StepRunId",
                        column: x => x.StepRunId,
                        principalTable: "Processes_StepRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Processes_WorkflowRunLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowBackend = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    WorkflowBackendRunId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_WorkflowRunLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Processes_WorkflowRunLinks_Processes_RunAssignments_Assignm~",
                        column: x => x.AssignmentId,
                        principalTable: "Processes_RunAssignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Processes_WorkflowRunLinks_Processes_Runs_ProcessRunId",
                        column: x => x.ProcessRunId,
                        principalTable: "Processes_Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Processes_WorkflowRunLinks_Processes_StepRuns_StepRunId",
                        column: x => x.StepRunId,
                        principalTable: "Processes_StepRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activity_Entries_CreatedAtUtc",
                table: "Activity_Entries",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Activity_Entries_IdempotencyKey",
                table: "Activity_Entries",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowArtifacts_RunId_CreatedAtUtc",
                table: "AgentFramework_WorkflowArtifacts",
                columns: new[] { "RunId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowComponents_Name",
                table: "AgentFramework_WorkflowComponents",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowComponents_ProviderProfileId",
                table: "AgentFramework_WorkflowComponents",
                column: "ProviderProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowDefinitions_WorkflowId",
                table: "AgentFramework_WorkflowDefinitions",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowDefinitions_WorkflowId_UpdatedAtUtc",
                table: "AgentFramework_WorkflowDefinitions",
                columns: new[] { "WorkflowId", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowEvents_RunId_CreatedAtUtc",
                table: "AgentFramework_WorkflowEvents",
                columns: new[] { "RunId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowExternalRequests_RunId_RespondedAtUtc",
                table: "AgentFramework_WorkflowExternalRequests",
                columns: new[] { "RunId", "RespondedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowRuns_UpdatedAtUtc",
                table: "AgentFramework_WorkflowRuns",
                column: "UpdatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowRuns_WorkflowId",
                table: "AgentFramework_WorkflowRuns",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_Automation_DeadLetters_DeadLetteredAtUtc_HandlerKey",
                table: "Automation_DeadLetters",
                columns: new[] { "DeadLetteredAtUtc", "HandlerKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Automation_DeadLetters_DeliveryId",
                table: "Automation_DeadLetters",
                column: "DeliveryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Automation_DeliveryAttempts_DeliveryId_AttemptNumber",
                table: "Automation_DeliveryAttempts",
                columns: new[] { "DeliveryId", "AttemptNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Automation_EnvelopeDeliveries_EnvelopeId_HandlerKey",
                table: "Automation_EnvelopeDeliveries",
                columns: new[] { "EnvelopeId", "HandlerKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Automation_EnvelopeDeliveries_State_AvailableAtUtc_LockedAt~",
                table: "Automation_EnvelopeDeliveries",
                columns: new[] { "State", "AvailableAtUtc", "LockedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Automation_Envelopes_EnvelopeType_DedupeKey",
                table: "Automation_Envelopes",
                columns: new[] { "EnvelopeType", "DedupeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Automation_Envelopes_State_AvailableAtUtc",
                table: "Automation_Envelopes",
                columns: new[] { "State", "AvailableAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Automation_ExecutionLogs_SourceType_SourceId_CreatedAtUtc",
                table: "Automation_ExecutionLogs",
                columns: new[] { "SourceType", "SourceId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Automation_PluginIngressCursors_SourceKind_SourceKey",
                table: "Automation_PluginIngressCursors",
                columns: new[] { "SourceKind", "SourceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Automation_PluginIngressEnvelopes_SourceKind_SourceKey_Dedu~",
                table: "Automation_PluginIngressEnvelopes",
                columns: new[] { "SourceKind", "SourceKey", "DedupeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Automation_PluginIngressEnvelopes_State_ReceivedAtUtc",
                table: "Automation_PluginIngressEnvelopes",
                columns: new[] { "State", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Automation_Triggers_OwnerKind_OwnerKey_TriggerKey",
                table: "Automation_Triggers",
                columns: new[] { "OwnerKind", "OwnerKey", "TriggerKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_AnswerGateDecisions_ProjectId_DecisionKind_~",
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
                name: "IX_CognitiveMemory_AnswerGateDecisions_SelfRegulationAssessmen~",
                table: "CognitiveMemory_AnswerGateDecisions",
                column: "SelfRegulationAssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_AnswerPostureDecisions_PostureScoreEvaluati~",
                table: "CognitiveMemory_AnswerPostureDecisions",
                column: "PostureScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_AnswerPostureDecisions_ProjectId_Posture_Cr~",
                table: "CognitiveMemory_AnswerPostureDecisions",
                columns: new[] { "ProjectId", "Posture", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_AnswerPostureDecisions_SelfRegulationAssess~",
                table: "CognitiveMemory_AnswerPostureDecisions",
                column: "SelfRegulationAssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_AttentionDecisions_ProjectId_DecisionKind_C~",
                table: "CognitiveMemory_AttentionDecisions",
                columns: new[] { "ProjectId", "DecisionKind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_AttentionDecisions_ProjectId_WorkspaceFrame~",
                table: "CognitiveMemory_AttentionDecisions",
                columns: new[] { "ProjectId", "WorkspaceFrameId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_AttentionDecisions_RequestHash",
                table: "CognitiveMemory_AttentionDecisions",
                column: "RequestHash");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_AttentionDecisions_RoutingScoreEvaluationTr~",
                table: "CognitiveMemory_AttentionDecisions",
                column: "RoutingScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_AttentionDecisions_WorkspaceFrameId_Decisio~",
                table: "CognitiveMemory_AttentionDecisions",
                columns: new[] { "WorkspaceFrameId", "DecisionKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_AutomationSettings_SettingsKey",
                table: "CognitiveMemory_AutomationSettings",
                column: "SettingsKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_BeliefStates_ClaimId_CalculatedAtUtc",
                table: "CognitiveMemory_BeliefStates",
                columns: new[] { "ClaimId", "CalculatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_BeliefStates_ScoreEvaluationTraceId",
                table: "CognitiveMemory_BeliefStates",
                column: "ScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_BeliefStates_StateKind_ProjectionBucket",
                table: "CognitiveMemory_BeliefStates",
                columns: new[] { "StateKind", "ProjectionBucket" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CalibrationAggregates_CalibrationScoreEvalu~",
                table: "CognitiveMemory_CalibrationAggregates",
                column: "CalibrationScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CalibrationAggregates_ProjectId_DomainKey_T~",
                table: "CognitiveMemory_CalibrationAggregates",
                columns: new[] { "ProjectId", "DomainKey", "TaskTypeKey", "ModelProfileId", "RiskKey", "FeaturePatternKey", "ProfileVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CalibrationBins_CalibrationAggregateId_BinI~",
                table: "CognitiveMemory_CalibrationBins",
                columns: new[] { "CalibrationAggregateId", "BinIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CalibrationEvents_ProjectId_DomainKey_TaskT~",
                table: "CognitiveMemory_CalibrationEvents",
                columns: new[] { "ProjectId", "DomainKey", "TaskTypeKey", "ModelProfileId", "ObservedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CalibrationEvents_ProjectId_OutcomeKind_Obs~",
                table: "CognitiveMemory_CalibrationEvents",
                columns: new[] { "ProjectId", "OutcomeKind", "ObservedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ClaimEvidenceLinks_ClaimId_EvidenceAnchorId~",
                table: "CognitiveMemory_ClaimEvidenceLinks",
                columns: new[] { "ClaimId", "EvidenceAnchorId", "Direction" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ClaimEvidenceLinks_EvidenceAnchorId_Directi~",
                table: "CognitiveMemory_ClaimEvidenceLinks",
                columns: new[] { "EvidenceAnchorId", "Direction" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Claims_CurrentBeliefScoreEvaluationTraceId",
                table: "CognitiveMemory_Claims",
                column: "CurrentBeliefScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Claims_MemoryRecordId",
                table: "CognitiveMemory_Claims",
                column: "MemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Claims_PrimaryContextFrameId",
                table: "CognitiveMemory_Claims",
                column: "PrimaryContextFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Claims_ProjectId_ClaimKind_CurrentBeliefSta~",
                table: "CognitiveMemory_Claims",
                columns: new[] { "ProjectId", "ClaimKind", "CurrentBeliefState", "ValidationState" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Claims_ProjectId_SubjectKey_PredicateKey_Ob~",
                table: "CognitiveMemory_Claims",
                columns: new[] { "ProjectId", "SubjectKey", "PredicateKey", "ObjectKey" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConfidenceReinforcements_SelfRegulationAsse~",
                table: "CognitiveMemory_ConfidenceReinforcements",
                columns: new[] { "SelfRegulationAssessmentId", "ReinforcementKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationCandidates_MutationCommandId",
                table: "CognitiveMemory_ConsolidationCandidates",
                column: "MutationCommandId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationCandidates_ProjectId_Candidate~",
                table: "CognitiveMemory_ConsolidationCandidates",
                columns: new[] { "ProjectId", "CandidateKind", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationCandidates_ProjectId_SourceIte~",
                table: "CognitiveMemory_ConsolidationCandidates",
                columns: new[] { "ProjectId", "SourceItemId", "CandidateKind", "SourceContentHash", "AlgorithmVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationCandidates_ReviewItemId",
                table: "CognitiveMemory_ConsolidationCandidates",
                column: "ReviewItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationCandidates_RunId_CandidateKind~",
                table: "CognitiveMemory_ConsolidationCandidates",
                columns: new[] { "RunId", "CandidateKind", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationCandidates_ScoreEvaluationTrac~",
                table: "CognitiveMemory_ConsolidationCandidates",
                column: "ScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationCursors_LastRunId",
                table: "CognitiveMemory_ConsolidationCursors",
                column: "LastRunId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationCursors_ProjectId_Mode_SourceS~",
                table: "CognitiveMemory_ConsolidationCursors",
                columns: new[] { "ProjectId", "Mode", "SourceSystem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationReports_ProjectId_CreatedAtUtc",
                table: "CognitiveMemory_ConsolidationReports",
                columns: new[] { "ProjectId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationReports_RunId",
                table: "CognitiveMemory_ConsolidationReports",
                column: "RunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationRuns_IdempotencyKey",
                table: "CognitiveMemory_ConsolidationRuns",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationRuns_InputHash",
                table: "CognitiveMemory_ConsolidationRuns",
                column: "InputHash");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationRuns_ProjectId_Mode_LeaseExpir~",
                table: "CognitiveMemory_ConsolidationRuns",
                columns: new[] { "ProjectId", "Mode", "LeaseExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationRuns_ProjectId_Mode_Status_Sta~",
                table: "CognitiveMemory_ConsolidationRuns",
                columns: new[] { "ProjectId", "Mode", "Status", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ContextBoundaries_ProjectId_BoundaryPolicy",
                table: "CognitiveMemory_ContextBoundaries",
                columns: new[] { "ProjectId", "BoundaryPolicy" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ContextBoundaries_ProjectId_SourceContextFr~",
                table: "CognitiveMemory_ContextBoundaries",
                columns: new[] { "ProjectId", "SourceContextFrameId", "TargetContextFrameId", "BoundaryKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ContextBoundaries_ScoreEvaluationTraceId",
                table: "CognitiveMemory_ContextBoundaries",
                column: "ScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ContextBoundaries_SourceContextFrameId",
                table: "CognitiveMemory_ContextBoundaries",
                column: "SourceContextFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ContextBoundaries_TargetContextFrameId",
                table: "CognitiveMemory_ContextBoundaries",
                column: "TargetContextFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ContextFrameDimensions_ContextFrameId_Dimen~",
                table: "CognitiveMemory_ContextFrameDimensions",
                columns: new[] { "ContextFrameId", "DimensionKind", "ValueKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ContextFrameDimensions_ProjectId_DimensionK~",
                table: "CognitiveMemory_ContextFrameDimensions",
                columns: new[] { "ProjectId", "DimensionKind", "ValueKey" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ContextFrames_ConfidenceScoreEvaluationTrac~",
                table: "CognitiveMemory_ContextFrames",
                column: "ConfidenceScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ContextFrames_ProjectId_FrameKind_DisplayNa~",
                table: "CognitiveMemory_ContextFrames",
                columns: new[] { "ProjectId", "FrameKind", "DisplayName" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CoverageMaps_ProjectId_CoverageState_Refres~",
                table: "CognitiveMemory_CoverageMaps",
                columns: new[] { "ProjectId", "CoverageState", "RefreshedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CoverageMaps_ProjectId_KnowledgeRegionId",
                table: "CognitiveMemory_CoverageMaps",
                columns: new[] { "ProjectId", "KnowledgeRegionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CrossProjectPromotionCandidates_PromotionSc~",
                table: "CognitiveMemory_CrossProjectPromotionCandidates",
                column: "PromotionScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CrossProjectPromotionCandidates_SourceProje~",
                table: "CognitiveMemory_CrossProjectPromotionCandidates",
                columns: new[] { "SourceProjectId", "SourceMemoryRecordId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorCapturedImprovements_AppliedMemoryRe~",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                column: "AppliedMemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorCapturedImprovements_AssimilatedMemo~",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                column: "AssimilatedMemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorCapturedImprovements_ConsolidationCa~",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                column: "ConsolidationCandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorCapturedImprovements_CuratorTurnId_C~",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                columns: new[] { "CuratorTurnId", "CaptureKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorCapturedImprovements_MutationCommand~",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                column: "MutationCommandId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorCapturedImprovements_ProjectId_Ancho~",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                columns: new[] { "ProjectId", "AnchorState", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorCapturedImprovements_ProjectId_Captu~",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                columns: new[] { "ProjectId", "CaptureKind", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorCapturedImprovements_ProjectId_Targe~",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                columns: new[] { "ProjectId", "TargetingStatus", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorCapturedImprovements_RecallTraceId",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                column: "RecallTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorCapturedImprovements_ReviewItemId",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                column: "ReviewItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorSessions_AgentChatSessionId",
                table: "CognitiveMemory_CuratorSessions",
                column: "AgentChatSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorSessions_ProjectId_RuntimeMode_Conve~",
                table: "CognitiveMemory_CuratorSessions",
                columns: new[] { "ProjectId", "RuntimeMode", "ConversationDepth", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorSessions_ProjectId_RuntimeMode_Status",
                table: "CognitiveMemory_CuratorSessions",
                columns: new[] { "ProjectId", "RuntimeMode", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorSessions_ProjectId_Status_UpdatedAtU~",
                table: "CognitiveMemory_CuratorSessions",
                columns: new[] { "ProjectId", "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorTurns_CuratorSessionId_Sequence",
                table: "CognitiveMemory_CuratorTurns",
                columns: new[] { "CuratorSessionId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorTurns_ProjectId_CreatedAtUtc",
                table: "CognitiveMemory_CuratorTurns",
                columns: new[] { "ProjectId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorTurns_RecallTraceId",
                table: "CognitiveMemory_CuratorTurns",
                column: "RecallTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DistributedJobs_LeasedWorkerId_LeaseExpires~",
                table: "CognitiveMemory_DistributedJobs",
                columns: new[] { "LeasedWorkerId", "LeaseExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DistributedJobs_ProjectId_JobKind_InputHash",
                table: "CognitiveMemory_DistributedJobs",
                columns: new[] { "ProjectId", "JobKind", "InputHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DistributedJobs_ProjectId_JobKind_State_Cre~",
                table: "CognitiveMemory_DistributedJobs",
                columns: new[] { "ProjectId", "JobKind", "State", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DistributedWorkerResults_DistributedJobId_W~",
                table: "CognitiveMemory_DistributedWorkerResults",
                columns: new[] { "DistributedJobId", "WorkerId", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DistributedWorkerResults_ProjectId_Status_S~",
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
                name: "IX_CognitiveMemory_DomainCompetenceProfiles_CompetenceScoreEva~",
                table: "CognitiveMemory_DomainCompetenceProfiles",
                column: "CompetenceScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DomainCompetenceProfiles_ProjectId_ModelPro~",
                table: "CognitiveMemory_DomainCompetenceProfiles",
                columns: new[] { "ProjectId", "ModelProfileId", "DomainKey", "TaskTypeKey", "ProfileVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateCandidates_ClusterId",
                table: "CognitiveMemory_DreamAggregateCandidates",
                column: "ClusterId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateCandidates_DreamRunId_Cluster~",
                table: "CognitiveMemory_DreamAggregateCandidates",
                columns: new[] { "DreamRunId", "ClusterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateCandidates_MemoryRecordId",
                table: "CognitiveMemory_DreamAggregateCandidates",
                column: "MemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateCandidates_PayloadHash",
                table: "CognitiveMemory_DreamAggregateCandidates",
                column: "PayloadHash");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateCandidates_ProjectId_Mode_Sta~",
                table: "CognitiveMemory_DreamAggregateCandidates",
                columns: new[] { "ProjectId", "Mode", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateCandidates_ReviewItemId",
                table: "CognitiveMemory_DreamAggregateCandidates",
                column: "ReviewItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateCandidates_ValidationRecordId",
                table: "CognitiveMemory_DreamAggregateCandidates",
                column: "ValidationRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateClaims_AggregateCandidateId_S~",
                table: "CognitiveMemory_DreamAggregateClaims",
                columns: new[] { "AggregateCandidateId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateClaims_ProjectId_SubjectKey_P~",
                table: "CognitiveMemory_DreamAggregateClaims",
                columns: new[] { "ProjectId", "SubjectKey", "PredicateKey", "ObjectKey" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateClaimSourceMaps_AggregateCand~",
                table: "CognitiveMemory_DreamAggregateClaimSourceMaps",
                column: "AggregateCandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateClaimSourceMaps_AggregateClai~",
                table: "CognitiveMemory_DreamAggregateClaimSourceMaps",
                columns: new[] { "AggregateClaimId", "SourceMemoryRecordId", "EvidenceAnchorId", "Direction" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateClaimSourceMaps_EvidenceAncho~",
                table: "CognitiveMemory_DreamAggregateClaimSourceMaps",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateClaimSourceMaps_ProjectId_Dir~",
                table: "CognitiveMemory_DreamAggregateClaimSourceMaps",
                columns: new[] { "ProjectId", "Direction" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateClaimSourceMaps_SourceItemId",
                table: "CognitiveMemory_DreamAggregateClaimSourceMaps",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateClaimSourceMaps_SourceMemoryR~",
                table: "CognitiveMemory_DreamAggregateClaimSourceMaps",
                column: "SourceMemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamRunClusters_ClusterId",
                table: "CognitiveMemory_DreamRunClusters",
                column: "ClusterId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamRunClusters_DreamRunId_ClusterId",
                table: "CognitiveMemory_DreamRunClusters",
                columns: new[] { "DreamRunId", "ClusterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamRunClusters_ProjectId_Readiness",
                table: "CognitiveMemory_DreamRunClusters",
                columns: new[] { "ProjectId", "Readiness" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamRuns_ProjectId_IdempotencyKey",
                table: "CognitiveMemory_DreamRuns",
                columns: new[] { "ProjectId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamRuns_ProjectId_Mode_Status_StartedAtUtc",
                table: "CognitiveMemory_DreamRuns",
                columns: new[] { "ProjectId", "Mode", "Status", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamValidations_AggregateCandidateId_Decis~",
                table: "CognitiveMemory_DreamValidations",
                columns: new[] { "AggregateCandidateId", "Decision" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamValidations_ProjectId_Decision_Created~",
                table: "CognitiveMemory_DreamValidations",
                columns: new[] { "ProjectId", "Decision", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Entities_ConfidenceScoreEvaluationTraceId",
                table: "CognitiveMemory_Entities",
                column: "ConfidenceScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Entities_PrimaryContextFrameId",
                table: "CognitiveMemory_Entities",
                column: "PrimaryContextFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Entities_ProjectId_EntityKind_CanonicalName~",
                table: "CognitiveMemory_Entities",
                columns: new[] { "ProjectId", "EntityKind", "CanonicalNameKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EntityAliases_EntityId",
                table: "CognitiveMemory_EntityAliases",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EntityAliases_ProjectId_EntityKind_AliasKey",
                table: "CognitiveMemory_EntityAliases",
                columns: new[] { "ProjectId", "EntityKind", "AliasKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EpisodeCausalLinks_ClaimId",
                table: "CognitiveMemory_EpisodeCausalLinks",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EpisodeCausalLinks_EpisodeId_LinkKind_FromS~",
                table: "CognitiveMemory_EpisodeCausalLinks",
                columns: new[] { "EpisodeId", "LinkKind", "FromStepId", "ToStepId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EpisodeCausalLinks_EvidenceAnchorId",
                table: "CognitiveMemory_EpisodeCausalLinks",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EpisodeCausalLinks_FromStepId",
                table: "CognitiveMemory_EpisodeCausalLinks",
                column: "FromStepId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EpisodeCausalLinks_PredictionErrorId",
                table: "CognitiveMemory_EpisodeCausalLinks",
                column: "PredictionErrorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EpisodeCausalLinks_ProjectId_ClaimId",
                table: "CognitiveMemory_EpisodeCausalLinks",
                columns: new[] { "ProjectId", "ClaimId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EpisodeCausalLinks_ProjectId_PredictionErro~",
                table: "CognitiveMemory_EpisodeCausalLinks",
                columns: new[] { "ProjectId", "PredictionErrorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EpisodeCausalLinks_ToStepId",
                table: "CognitiveMemory_EpisodeCausalLinks",
                column: "ToStepId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EpisodeStepEvidence_EvidenceAnchorId",
                table: "CognitiveMemory_EpisodeStepEvidence",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EpisodeStepEvidence_ProjectId_EvidenceAncho~",
                table: "CognitiveMemory_EpisodeStepEvidence",
                columns: new[] { "ProjectId", "EvidenceAnchorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EpisodeStepEvidence_StepId_EvidenceRole_Evi~",
                table: "CognitiveMemory_EpisodeStepEvidence",
                columns: new[] { "StepId", "EvidenceRole", "EvidenceAnchorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EpisodeSteps_EpisodeId_SequenceIndex",
                table: "CognitiveMemory_EpisodeSteps",
                columns: new[] { "EpisodeId", "SequenceIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EpisodeSteps_ProjectId_ActorKind_ActorId",
                table: "CognitiveMemory_EpisodeSteps",
                columns: new[] { "ProjectId", "ActorKind", "ActorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EpisodeSteps_ProjectId_OccurredAtUtc",
                table: "CognitiveMemory_EpisodeSteps",
                columns: new[] { "ProjectId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EvidenceAnchors_ProjectId_AnchorKind_Observ~",
                table: "CognitiveMemory_EvidenceAnchors",
                columns: new[] { "ProjectId", "AnchorKind", "ObservedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EvidenceAnchors_ProjectId_SourceManifestId_~",
                table: "CognitiveMemory_EvidenceAnchors",
                columns: new[] { "ProjectId", "SourceManifestId", "SourceItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EvidenceAnchors_QuoteHash",
                table: "CognitiveMemory_EvidenceAnchors",
                column: "QuoteHash");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EvidenceAnchors_SourceHash",
                table: "CognitiveMemory_EvidenceAnchors",
                column: "SourceHash");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EvidenceAnchors_SourceItemId",
                table: "CognitiveMemory_EvidenceAnchors",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EvidenceAnchors_SourceManifestId",
                table: "CognitiveMemory_EvidenceAnchors",
                column: "SourceManifestId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ExternalSourceIngestions_EvidenceAnchorId",
                table: "CognitiveMemory_ExternalSourceIngestions",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ExternalSourceIngestions_ProjectId_SourceKi~",
                table: "CognitiveMemory_ExternalSourceIngestions",
                columns: new[] { "ProjectId", "SourceKind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ExternalSourceIngestions_SourceItemId",
                table: "CognitiveMemory_ExternalSourceIngestions",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ExternalSourceIngestions_SourceManifestId",
                table: "CognitiveMemory_ExternalSourceIngestions",
                column: "SourceManifestId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ExternalSourceIngestions_Status_UpdatedAtUtc",
                table: "CognitiveMemory_ExternalSourceIngestions",
                columns: new[] { "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_HumilityTriggers_SelfRegulationAssessmentId~",
                table: "CognitiveMemory_HumilityTriggers",
                columns: new[] { "SelfRegulationAssessmentId", "TriggerKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_KnowledgeGaps_ProjectId_KnowledgeRegionId_G~",
                table: "CognitiveMemory_KnowledgeGaps",
                columns: new[] { "ProjectId", "KnowledgeRegionId", "GapKind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_KnowledgeRegions_ProjectId_RegionKind_Regio~",
                table: "CognitiveMemory_KnowledgeRegions",
                columns: new[] { "ProjectId", "RegionKind", "RegionKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_KnownFailurePatterns_PatternScoreEvaluation~",
                table: "CognitiveMemory_KnownFailurePatterns",
                column: "PatternScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_KnownFailurePatterns_ProjectId_PatternKind_~",
                table: "CognitiveMemory_KnownFailurePatterns",
                columns: new[] { "ProjectId", "PatternKind", "DomainKey", "TaskTypeKey" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_LearningOutcomes_ProjectId_LearningTaskId_O~",
                table: "CognitiveMemory_LearningOutcomes",
                columns: new[] { "ProjectId", "LearningTaskId", "OutcomeKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_LearningProposals_NeedScoreEvaluationTraceId",
                table: "CognitiveMemory_LearningProposals",
                column: "NeedScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_LearningProposals_ProjectId_Status_CreatedA~",
                table: "CognitiveMemory_LearningProposals",
                columns: new[] { "ProjectId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_LearningTasks_ProjectId_LearningProposalId_~",
                table: "CognitiveMemory_LearningTasks",
                columns: new[] { "ProjectId", "LearningProposalId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_MutationAuditEvents_MutationCommandId_Seque~",
                table: "CognitiveMemory_MutationAuditEvents",
                columns: new[] { "MutationCommandId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_MutationAuditEvents_ProjectId_EventKind_Cre~",
                table: "CognitiveMemory_MutationAuditEvents",
                columns: new[] { "ProjectId", "EventKind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_MutationCommands_ActorKind_ActorId",
                table: "CognitiveMemory_MutationCommands",
                columns: new[] { "ActorKind", "ActorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_MutationCommands_ProjectId_CommandKind_Stat~",
                table: "CognitiveMemory_MutationCommands",
                columns: new[] { "ProjectId", "CommandKind", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_MutationCommands_ProjectId_IdempotencyKey",
                table: "CognitiveMemory_MutationCommands",
                columns: new[] { "ProjectId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrorEvidenceAnchors_EvidenceAnch~",
                table: "CognitiveMemory_PredictionErrorEvidenceAnchors",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrorEvidenceAnchors_PredictionEr~",
                table: "CognitiveMemory_PredictionErrorEvidenceAnchors",
                columns: new[] { "PredictionErrorId", "EvidenceAnchorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrorEvidenceAnchors_ProjectId_Ev~",
                table: "CognitiveMemory_PredictionErrorEvidenceAnchors",
                columns: new[] { "ProjectId", "EvidenceAnchorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrors_AttentionDecisionId",
                table: "CognitiveMemory_PredictionErrors",
                column: "AttentionDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrors_ClaimId",
                table: "CognitiveMemory_PredictionErrors",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrors_MemoryRecordId",
                table: "CognitiveMemory_PredictionErrors",
                column: "MemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrors_PredictionExpectationId",
                table: "CognitiveMemory_PredictionErrors",
                column: "PredictionExpectationId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrors_ProjectId_ErrorKind_Observ~",
                table: "CognitiveMemory_PredictionErrors",
                columns: new[] { "ProjectId", "ErrorKind", "ObservedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrors_ProjectId_RequiresReview_O~",
                table: "CognitiveMemory_PredictionErrors",
                columns: new[] { "ProjectId", "RequiresReview", "ObservedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrors_SeverityScoreEvaluationTra~",
                table: "CognitiveMemory_PredictionErrors",
                column: "SeverityScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrors_SourceItemId",
                table: "CognitiveMemory_PredictionErrors",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrors_WorkspaceFrameId",
                table: "CognitiveMemory_PredictionErrors",
                column: "WorkspaceFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrorSignals_CognitiveSignalId",
                table: "CognitiveMemory_PredictionErrorSignals",
                column: "CognitiveSignalId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrorSignals_PredictionErrorId_Co~",
                table: "CognitiveMemory_PredictionErrorSignals",
                columns: new[] { "PredictionErrorId", "CognitiveSignalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionErrorSignals_ProjectId_CognitiveS~",
                table: "CognitiveMemory_PredictionErrorSignals",
                columns: new[] { "ProjectId", "CognitiveSignalId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionExpectationEvidenceAnchors_Eviden~",
                table: "CognitiveMemory_PredictionExpectationEvidenceAnchors",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionExpectationEvidenceAnchors_Predic~",
                table: "CognitiveMemory_PredictionExpectationEvidenceAnchors",
                columns: new[] { "PredictionExpectationId", "EvidenceAnchorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionExpectationEvidenceAnchors_Projec~",
                table: "CognitiveMemory_PredictionExpectationEvidenceAnchors",
                columns: new[] { "ProjectId", "EvidenceAnchorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionExpectations_AttentionDecisionId",
                table: "CognitiveMemory_PredictionExpectations",
                column: "AttentionDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionExpectations_ClaimId",
                table: "CognitiveMemory_PredictionExpectations",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionExpectations_MemoryRecordId",
                table: "CognitiveMemory_PredictionExpectations",
                column: "MemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionExpectations_ProjectId_ActorKind_~",
                table: "CognitiveMemory_PredictionExpectations",
                columns: new[] { "ProjectId", "ActorKind", "ActorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionExpectations_ProjectId_Expectatio~",
                table: "CognitiveMemory_PredictionExpectations",
                columns: new[] { "ProjectId", "ExpectationKind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionExpectations_SourceItemId",
                table: "CognitiveMemory_PredictionExpectations",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_PredictionExpectations_WorkspaceFrameId",
                table: "CognitiveMemory_PredictionExpectations",
                column: "WorkspaceFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProbeFeedback_ProbeTurnId_Action_CreatedAtU~",
                table: "CognitiveMemory_ProbeFeedback",
                columns: new[] { "ProbeTurnId", "Action", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProbeFeedback_ProjectId_CalibrationOutcome_~",
                table: "CognitiveMemory_ProbeFeedback",
                columns: new[] { "ProjectId", "CalibrationOutcome", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProbeFindings_ProbeTurnId_FindingKind",
                table: "CognitiveMemory_ProbeFindings",
                columns: new[] { "ProbeTurnId", "FindingKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProbeFindings_ProjectId_FindingKind_Created~",
                table: "CognitiveMemory_ProbeFindings",
                columns: new[] { "ProjectId", "FindingKind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProbeRegressionRuns_ProjectId_Outcome_Start~",
                table: "CognitiveMemory_ProbeRegressionRuns",
                columns: new[] { "ProjectId", "Outcome", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProbeRegressionRuns_RegressionTestCaseId_St~",
                table: "CognitiveMemory_ProbeRegressionRuns",
                columns: new[] { "RegressionTestCaseId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProbeRegressionTestCases_ProbeTurnId",
                table: "CognitiveMemory_ProbeRegressionTestCases",
                column: "ProbeTurnId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProbeRegressionTestCases_ProjectId_Status_C~",
                table: "CognitiveMemory_ProbeRegressionTestCases",
                columns: new[] { "ProjectId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProbeSessions_ProjectId_Status_CreatedAtUtc",
                table: "CognitiveMemory_ProbeSessions",
                columns: new[] { "ProjectId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProbeSessions_ProjectId_WorkspaceFrameId_St~",
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
                name: "IX_CognitiveMemory_ProcedureAutomationBindings_ProcedureSkillI~",
                table: "CognitiveMemory_ProcedureAutomationBindings",
                columns: new[] { "ProcedureSkillId", "BindingKind", "BindingKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureAutomationBindings_ProjectId_State~",
                table: "CognitiveMemory_ProcedureAutomationBindings",
                columns: new[] { "ProjectId", "State", "BindingKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureAutomationBindings_ReviewItemId",
                table: "CognitiveMemory_ProcedureAutomationBindings",
                column: "ReviewItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureFailureModeEpisodes_EpisodeId",
                table: "CognitiveMemory_ProcedureFailureModeEpisodes",
                column: "EpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureFailureModeEpisodes_ProcedureFailu~",
                table: "CognitiveMemory_ProcedureFailureModeEpisodes",
                columns: new[] { "ProcedureFailureModeId", "EpisodeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureFailureModeEpisodes_ProjectId_Epis~",
                table: "CognitiveMemory_ProcedureFailureModeEpisodes",
                columns: new[] { "ProjectId", "EpisodeId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureFailureModePredictionErrors_Predic~",
                table: "CognitiveMemory_ProcedureFailureModePredictionErrors",
                column: "PredictionErrorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureFailureModePredictionErrors_Proced~",
                table: "CognitiveMemory_ProcedureFailureModePredictionErrors",
                columns: new[] { "ProcedureFailureModeId", "PredictionErrorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureFailureModePredictionErrors_Projec~",
                table: "CognitiveMemory_ProcedureFailureModePredictionErrors",
                columns: new[] { "ProjectId", "PredictionErrorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureFailureModes_ProcedureSkillId_Fail~",
                table: "CognitiveMemory_ProcedureFailureModes",
                columns: new[] { "ProcedureSkillId", "FailureKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureFailureModes_ProjectId_CreatedAtUtc",
                table: "CognitiveMemory_ProcedureFailureModes",
                columns: new[] { "ProjectId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSimulationEvidence_EvidenceAnchorId",
                table: "CognitiveMemory_ProcedureSimulationEvidence",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSimulationEvidence_ProjectId_Evide~",
                table: "CognitiveMemory_ProcedureSimulationEvidence",
                columns: new[] { "ProjectId", "EvidenceAnchorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSimulationEvidence_SimulationId_Ev~",
                table: "CognitiveMemory_ProcedureSimulationEvidence",
                columns: new[] { "SimulationId", "EvidenceAnchorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSimulations_ProjectId_OutputKind_R~",
                table: "CognitiveMemory_ProcedureSimulations",
                columns: new[] { "ProjectId", "OutputKind", "RiskLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSimulations_ProjectId_Status_Creat~",
                table: "CognitiveMemory_ProcedureSimulations",
                columns: new[] { "ProjectId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSimulations_RiskScoreEvaluationTra~",
                table: "CognitiveMemory_ProcedureSimulations",
                column: "RiskScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSimulationSkills_ProcedureSkillId",
                table: "CognitiveMemory_ProcedureSimulationSkills",
                column: "ProcedureSkillId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSimulationSkills_ProjectId_Procedu~",
                table: "CognitiveMemory_ProcedureSimulationSkills",
                columns: new[] { "ProjectId", "ProcedureSkillId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSimulationSkills_SimulationId_Proc~",
                table: "CognitiveMemory_ProcedureSimulationSkills",
                columns: new[] { "SimulationId", "ProcedureSkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSkills_LastSuccessfulEpisodeId",
                table: "CognitiveMemory_ProcedureSkills",
                column: "LastSuccessfulEpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSkills_MaturityScoreEvaluationTrac~",
                table: "CognitiveMemory_ProcedureSkills",
                column: "MaturityScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSkills_ProjectId_Maturity_Validati~",
                table: "CognitiveMemory_ProcedureSkills",
                columns: new[] { "ProjectId", "Maturity", "ValidationState" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSkills_ProjectId_RiskLevel_Maturity",
                table: "CognitiveMemory_ProcedureSkills",
                columns: new[] { "ProjectId", "RiskLevel", "Maturity" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSkills_SourceConsolidationCandidat~",
                table: "CognitiveMemory_ProcedureSkills",
                column: "SourceConsolidationCandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureStepEvidence_EvidenceAnchorId",
                table: "CognitiveMemory_ProcedureStepEvidence",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureStepEvidence_ProcedureStepId_Evide~",
                table: "CognitiveMemory_ProcedureStepEvidence",
                columns: new[] { "ProcedureStepId", "EvidenceAnchorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureStepEvidence_ProjectId_EvidenceAnc~",
                table: "CognitiveMemory_ProcedureStepEvidence",
                columns: new[] { "ProjectId", "EvidenceAnchorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSteps_ProcedureSkillId_SequenceInd~",
                table: "CognitiveMemory_ProcedureSteps",
                columns: new[] { "ProcedureSkillId", "SequenceIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSteps_ProcedureSkillId_StepKey",
                table: "CognitiveMemory_ProcedureSteps",
                columns: new[] { "ProcedureSkillId", "StepKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureSteps_ProjectId_ToolBindingKey",
                table: "CognitiveMemory_ProcedureSteps",
                columns: new[] { "ProjectId", "ToolBindingKey" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureValidationEvidence_EpisodeId",
                table: "CognitiveMemory_ProcedureValidationEvidence",
                column: "EpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureValidationEvidence_EvidenceAnchorId",
                table: "CognitiveMemory_ProcedureValidationEvidence",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureValidationEvidence_ProcedureSkillI~",
                table: "CognitiveMemory_ProcedureValidationEvidence",
                columns: new[] { "ProcedureSkillId", "EvidenceRole", "EvidenceAnchorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureValidationEvidence_ProjectId_Evide~",
                table: "CognitiveMemory_ProcedureValidationEvidence",
                columns: new[] { "ProjectId", "EvidenceAnchorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProcedureValidationEvidence_ReviewItemId",
                table: "CognitiveMemory_ProcedureValidationEvidence",
                column: "ReviewItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProfessorReviewActions_ProfessorReviewId_Su~",
                table: "CognitiveMemory_ProfessorReviewActions",
                columns: new[] { "ProfessorReviewId", "SuggestionKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProfessorReviews_ProjectId_ReviewMode_Statu~",
                table: "CognitiveMemory_ProfessorReviews",
                columns: new[] { "ProjectId", "ReviewMode", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProfessorReviews_RoutingScoreEvaluationTrac~",
                table: "CognitiveMemory_ProfessorReviews",
                column: "RoutingScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProfessorReviews_SelfRegulationAssessmentId",
                table: "CognitiveMemory_ProfessorReviews",
                column: "SelfRegulationAssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Projections_MemoryRecordId_ProjectionStoreK~",
                table: "CognitiveMemory_Projections",
                columns: new[] { "MemoryRecordId", "ProjectionStoreKind", "ProjectionKind", "ProjectionProfileId", "EmbeddingProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Projections_PayloadHash",
                table: "CognitiveMemory_Projections",
                column: "PayloadHash");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Projections_PointId",
                table: "CognitiveMemory_Projections",
                column: "PointId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Projections_ProjectId_CollectionName_Status",
                table: "CognitiveMemory_Projections",
                columns: new[] { "ProjectId", "CollectionName", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Projections_ProjectId_RebuildRequired_Stale~",
                table: "CognitiveMemory_Projections",
                columns: new[] { "ProjectId", "RebuildRequired", "StaleReason" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Projections_SourceHash",
                table: "CognitiveMemory_Projections",
                column: "SourceHash");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProjectionStates_ProjectId_ProjectionKind_T~",
                table: "CognitiveMemory_ProjectionStates",
                columns: new[] { "ProjectId", "ProjectionKind", "TargetProvider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProjectionStates_Status_RebuildRequired",
                table: "CognitiveMemory_ProjectionStates",
                columns: new[] { "Status", "RebuildRequired" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_QualityClusterKeys_ClusterId_KeyFamily_Key",
                table: "CognitiveMemory_QualityClusterKeys",
                columns: new[] { "ClusterId", "KeyFamily", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_QualityClusterKeys_ProjectId_KeyFamily_Key",
                table: "CognitiveMemory_QualityClusterKeys",
                columns: new[] { "ProjectId", "KeyFamily", "Key" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_QualityClusterMembers_ClusterId_MemberKind_~",
                table: "CognitiveMemory_QualityClusterMembers",
                columns: new[] { "ClusterId", "MemberKind", "MemoryRecordId", "SourceItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_QualityClusterMembers_EvidenceAnchorId",
                table: "CognitiveMemory_QualityClusterMembers",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_QualityClusterMembers_MemoryRecordId",
                table: "CognitiveMemory_QualityClusterMembers",
                column: "MemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_QualityClusterMembers_ProjectId_MemberKind",
                table: "CognitiveMemory_QualityClusterMembers",
                columns: new[] { "ProjectId", "MemberKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_QualityClusterMembers_SourceItemId",
                table: "CognitiveMemory_QualityClusterMembers",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_QualityClusters_ProjectId_AccessLevel_RiskL~",
                table: "CognitiveMemory_QualityClusters",
                columns: new[] { "ProjectId", "AccessLevel", "RiskLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_QualityClusters_ProjectId_AggregateEligible~",
                table: "CognitiveMemory_QualityClusters",
                columns: new[] { "ProjectId", "AggregateEligible", "CompositeScore" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_QualityClusters_ProjectId_ClusterHash",
                table: "CognitiveMemory_QualityClusters",
                columns: new[] { "ProjectId", "ClusterHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_QualityClusters_ProjectId_PrimaryKeyFamily_~",
                table: "CognitiveMemory_QualityClusters",
                columns: new[] { "ProjectId", "PrimaryKeyFamily", "Readiness" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallCandidates_ClaimId",
                table: "CognitiveMemory_RecallCandidates",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallCandidates_ContextFrameId",
                table: "CognitiveMemory_RecallCandidates",
                column: "ContextFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallCandidates_EvidenceAnchorId",
                table: "CognitiveMemory_RecallCandidates",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallCandidates_MemoryRecordId",
                table: "CognitiveMemory_RecallCandidates",
                column: "MemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallCandidates_ProjectId_MemoryRecordId_D~",
                table: "CognitiveMemory_RecallCandidates",
                columns: new[] { "ProjectId", "MemoryRecordId", "DecisionKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallCandidates_ProjectId_PrimaryChannelKi~",
                table: "CognitiveMemory_RecallCandidates",
                columns: new[] { "ProjectId", "PrimaryChannelKind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallCandidates_RecallTraceId_DecisionKind~",
                table: "CognitiveMemory_RecallCandidates",
                columns: new[] { "RecallTraceId", "DecisionKind", "PrimaryChannelKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallCandidates_ScoreEvaluationTraceId",
                table: "CognitiveMemory_RecallCandidates",
                column: "ScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallCandidates_SourceItemId",
                table: "CognitiveMemory_RecallCandidates",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallCandidates_WorkspaceFrameId",
                table: "CognitiveMemory_RecallCandidates",
                column: "WorkspaceFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallContextPacks_ProjectId_CreatedAtUtc",
                table: "CognitiveMemory_RecallContextPacks",
                columns: new[] { "ProjectId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallContextPacks_RecallTraceId",
                table: "CognitiveMemory_RecallContextPacks",
                column: "RecallTraceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallContextPacks_WorkspaceFrameId",
                table: "CognitiveMemory_RecallContextPacks",
                column: "WorkspaceFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallContextSections_ClaimId",
                table: "CognitiveMemory_RecallContextSections",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallContextSections_ContextPackId_Sequence",
                table: "CognitiveMemory_RecallContextSections",
                columns: new[] { "ContextPackId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallContextSections_MemoryRecordId",
                table: "CognitiveMemory_RecallContextSections",
                column: "MemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallContextSections_ProjectId_SectionKind~",
                table: "CognitiveMemory_RecallContextSections",
                columns: new[] { "ProjectId", "SectionKind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallContextSections_RecallTraceId_Section~",
                table: "CognitiveMemory_RecallContextSections",
                columns: new[] { "RecallTraceId", "SectionKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallContextSections_SourceItemId",
                table: "CognitiveMemory_RecallContextSections",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallSourceRefs_ClaimId",
                table: "CognitiveMemory_RecallSourceRefs",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallSourceRefs_ContextPackId_IncludedInCo~",
                table: "CognitiveMemory_RecallSourceRefs",
                columns: new[] { "ContextPackId", "IncludedInContext" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallSourceRefs_EvidenceAnchorId",
                table: "CognitiveMemory_RecallSourceRefs",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallSourceRefs_MemoryRecordId",
                table: "CognitiveMemory_RecallSourceRefs",
                column: "MemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallSourceRefs_ProjectId_SourceSystem_Inc~",
                table: "CognitiveMemory_RecallSourceRefs",
                columns: new[] { "ProjectId", "SourceSystem", "IncludedInContext" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallSourceRefs_RecallTraceId_MemoryRecord~",
                table: "CognitiveMemory_RecallSourceRefs",
                columns: new[] { "RecallTraceId", "MemoryRecordId", "IncludedInContext" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallSourceRefs_SourceItemId",
                table: "CognitiveMemory_RecallSourceRefs",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallTraces_AnswerGateDecisionId",
                table: "CognitiveMemory_RecallTraces",
                column: "AnswerGateDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallTraces_AnswerPostureDecisionId",
                table: "CognitiveMemory_RecallTraces",
                column: "AnswerPostureDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallTraces_AttentionDecisionId",
                table: "CognitiveMemory_RecallTraces",
                column: "AttentionDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallTraces_ContextPackId",
                table: "CognitiveMemory_RecallTraces",
                column: "ContextPackId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallTraces_ProjectId_OperationMode_Starte~",
                table: "CognitiveMemory_RecallTraces",
                columns: new[] { "ProjectId", "OperationMode", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallTraces_ProjectId_RecallMode_Outcome_S~",
                table: "CognitiveMemory_RecallTraces",
                columns: new[] { "ProjectId", "RecallMode", "Outcome", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallTraces_RequestHash",
                table: "CognitiveMemory_RecallTraces",
                column: "RequestHash");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallTraces_SelfRegulationAssessmentId",
                table: "CognitiveMemory_RecallTraces",
                column: "SelfRegulationAssessmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallTraces_WorkspaceFrameId",
                table: "CognitiveMemory_RecallTraces",
                column: "WorkspaceFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallTraceStages_ProjectId_StageKind_Statu~",
                table: "CognitiveMemory_RecallTraceStages",
                columns: new[] { "ProjectId", "StageKind", "Status", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallTraceStages_RecallTraceId_StageKind_C~",
                table: "CognitiveMemory_RecallTraceStages",
                columns: new[] { "RecallTraceId", "StageKind", "ChannelKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecordEvidenceAnchors_EvidenceAnchorId_Evid~",
                table: "CognitiveMemory_RecordEvidenceAnchors",
                columns: new[] { "EvidenceAnchorId", "EvidenceRole" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecordEvidenceAnchors_MemoryRecordId_Eviden~",
                table: "CognitiveMemory_RecordEvidenceAnchors",
                columns: new[] { "MemoryRecordId", "EvidenceAnchorId", "EvidenceRole" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Records_ActivationScoreEvaluationTraceId",
                table: "CognitiveMemory_Records",
                column: "ActivationScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Records_ConfidenceScoreEvaluationTraceId",
                table: "CognitiveMemory_Records",
                column: "ConfidenceScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Records_ContentHash",
                table: "CognitiveMemory_Records",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Records_PrimaryClaimId",
                table: "CognitiveMemory_Records",
                column: "PrimaryClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Records_PrimaryContextFrameId",
                table: "CognitiveMemory_Records",
                column: "PrimaryContextFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Records_ProjectId_Kind_ValidationState",
                table: "CognitiveMemory_Records",
                columns: new[] { "ProjectId", "Kind", "ValidationState" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Records_ProjectId_StabilityState",
                table: "CognitiveMemory_Records",
                columns: new[] { "ProjectId", "StabilityState" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Records_ProjectId_TopicKey",
                table: "CognitiveMemory_Records",
                columns: new[] { "ProjectId", "TopicKey" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RelationEvidence_EvidenceAnchorId_Direction",
                table: "CognitiveMemory_RelationEvidence",
                columns: new[] { "EvidenceAnchorId", "Direction" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RelationEvidence_RelationId_EvidenceAnchorI~",
                table: "CognitiveMemory_RelationEvidence",
                columns: new[] { "RelationId", "EvidenceAnchorId", "Direction" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Relations_ProjectId_RelationKind",
                table: "CognitiveMemory_Relations",
                columns: new[] { "ProjectId", "RelationKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Relations_ProjectId_SourceMemoryRecordId_Ta~",
                table: "CognitiveMemory_Relations",
                columns: new[] { "ProjectId", "SourceMemoryRecordId", "TargetMemoryRecordId", "RelationKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Relations_RelationScoreEvaluationTraceId",
                table: "CognitiveMemory_Relations",
                column: "RelationScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Relations_SourceMemoryRecordId",
                table: "CognitiveMemory_Relations",
                column: "SourceMemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Relations_TargetMemoryRecordId",
                table: "CognitiveMemory_Relations",
                column: "TargetMemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayJobPredictionErrors_PredictionErrorId",
                table: "CognitiveMemory_ReplayJobPredictionErrors",
                column: "PredictionErrorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayJobPredictionErrors_ProjectId_Predict~",
                table: "CognitiveMemory_ReplayJobPredictionErrors",
                columns: new[] { "ProjectId", "PredictionErrorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayJobPredictionErrors_ReplayJobId_Predi~",
                table: "CognitiveMemory_ReplayJobPredictionErrors",
                columns: new[] { "ReplayJobId", "PredictionErrorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayJobs_PriorityScoreEvaluationTraceId",
                table: "CognitiveMemory_ReplayJobs",
                column: "PriorityScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayJobs_ProjectId_JobKind_InputHash",
                table: "CognitiveMemory_ReplayJobs",
                columns: new[] { "ProjectId", "JobKind", "InputHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayJobs_ProjectId_JobKind_QueuePriority",
                table: "CognitiveMemory_ReplayJobs",
                columns: new[] { "ProjectId", "JobKind", "QueuePriority" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayJobs_ProjectId_State_ScheduledAtUtc",
                table: "CognitiveMemory_ReplayJobs",
                columns: new[] { "ProjectId", "State", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayJobSignals_CognitiveSignalId",
                table: "CognitiveMemory_ReplayJobSignals",
                column: "CognitiveSignalId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayJobSignals_ProjectId_CognitiveSignalId",
                table: "CognitiveMemory_ReplayJobSignals",
                columns: new[] { "ProjectId", "CognitiveSignalId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayJobSignals_ReplayJobId_CognitiveSigna~",
                table: "CognitiveMemory_ReplayJobSignals",
                columns: new[] { "ReplayJobId", "CognitiveSignalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayJobTargets_ProjectId_TargetKind_Targe~",
                table: "CognitiveMemory_ReplayJobTargets",
                columns: new[] { "ProjectId", "TargetKind", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayJobTargets_ReplayJobId_TargetKind_Tar~",
                table: "CognitiveMemory_ReplayJobTargets",
                columns: new[] { "ReplayJobId", "TargetKind", "TargetId", "TargetKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayOutputs_MutationCommandId",
                table: "CognitiveMemory_ReplayOutputs",
                column: "MutationCommandId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayOutputs_ProjectId_OutputKind_Status",
                table: "CognitiveMemory_ReplayOutputs",
                columns: new[] { "ProjectId", "OutputKind", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayOutputs_ReplayJobId",
                table: "CognitiveMemory_ReplayOutputs",
                column: "ReplayJobId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayOutputs_ReviewItemId",
                table: "CognitiveMemory_ReplayOutputs",
                column: "ReviewItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayWorkerResults_ProjectId_Status_Submit~",
                table: "CognitiveMemory_ReplayWorkerResults",
                columns: new[] { "ProjectId", "Status", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReplayWorkerResults_ReplayJobId_WorkerId_Su~",
                table: "CognitiveMemory_ReplayWorkerResults",
                columns: new[] { "ReplayJobId", "WorkerId", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReviewItems_ProjectId_Status_RiskLevel",
                table: "CognitiveMemory_ReviewItems",
                columns: new[] { "ProjectId", "Status", "RiskLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReviewItems_SubjectKind_SubjectId",
                table: "CognitiveMemory_ReviewItems",
                columns: new[] { "SubjectKind", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Runs_IdempotencyKey",
                table: "CognitiveMemory_Runs",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Runs_ProjectId_RunKind_Status",
                table: "CognitiveMemory_Runs",
                columns: new[] { "ProjectId", "RunKind", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ScoreComponents_OwnerKind_OwnerId_Dimension~",
                table: "CognitiveMemory_ScoreComponents",
                columns: new[] { "OwnerKind", "OwnerId", "DimensionKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ScoreComponents_ProjectId_SpaceKind_Dimensi~",
                table: "CognitiveMemory_ScoreComponents",
                columns: new[] { "ProjectId", "SpaceKind", "DimensionKind", "CalculatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ScoreComponents_ScoreEvaluationTraceId_Dime~",
                table: "CognitiveMemory_ScoreComponents",
                columns: new[] { "ScoreEvaluationTraceId", "DimensionKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ScoreComponents_SchemaVersion_DimensionKind",
                table: "CognitiveMemory_ScoreComponents",
                columns: new[] { "SchemaVersion", "DimensionKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ScoreEvaluations_InputHash",
                table: "CognitiveMemory_ScoreEvaluations",
                column: "InputHash");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ScoreEvaluations_OwnerKind_OwnerId_SpaceKind",
                table: "CognitiveMemory_ScoreEvaluations",
                columns: new[] { "OwnerKind", "OwnerId", "SpaceKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ScoreEvaluations_ProjectId_SpaceKind_Schema~",
                table: "CognitiveMemory_ScoreEvaluations",
                columns: new[] { "ProjectId", "SpaceKind", "SchemaVersion", "CalculatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SelfModelProfiles_ProjectId_ModelProfileId_~",
                table: "CognitiveMemory_SelfModelProfiles",
                columns: new[] { "ProjectId", "ModelProfileId", "RoleKey", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SelfModelUpdateProposals_ProjectId_Status_C~",
                table: "CognitiveMemory_SelfModelUpdateProposals",
                columns: new[] { "ProjectId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SelfRegulationAssessments_AssessmentScoreEv~",
                table: "CognitiveMemory_SelfRegulationAssessments",
                column: "AssessmentScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SelfRegulationAssessments_ProjectId_State_C~",
                table: "CognitiveMemory_SelfRegulationAssessments",
                columns: new[] { "ProjectId", "State", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SelfRegulationAssessments_RecallTraceId",
                table: "CognitiveMemory_SelfRegulationAssessments",
                column: "RecallTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SelfRegulationPolicyProfiles_ProjectId_Poli~",
                table: "CognitiveMemory_SelfRegulationPolicyProfiles",
                columns: new[] { "ProjectId", "PolicyKey", "ProfileVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SignalConsumerPolicies_CognitiveSignalId_Co~",
                table: "CognitiveMemory_SignalConsumerPolicies",
                columns: new[] { "CognitiveSignalId", "ConsumerKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SignalConsumerPolicies_ProjectId_ConsumerKi~",
                table: "CognitiveMemory_SignalConsumerPolicies",
                columns: new[] { "ProjectId", "ConsumerKind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SignalEvidenceAnchors_CognitiveSignalId_Evi~",
                table: "CognitiveMemory_SignalEvidenceAnchors",
                columns: new[] { "CognitiveSignalId", "EvidenceAnchorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SignalEvidenceAnchors_EvidenceAnchorId",
                table: "CognitiveMemory_SignalEvidenceAnchors",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SignalEvidenceAnchors_ProjectId_EvidenceAnc~",
                table: "CognitiveMemory_SignalEvidenceAnchors",
                columns: new[] { "ProjectId", "EvidenceAnchorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Signals_AttentionDecisionId",
                table: "CognitiveMemory_Signals",
                column: "AttentionDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Signals_ClaimId",
                table: "CognitiveMemory_Signals",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Signals_MemoryRecordId",
                table: "CognitiveMemory_Signals",
                column: "MemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Signals_PredictionErrorId",
                table: "CognitiveMemory_Signals",
                column: "PredictionErrorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Signals_ProjectId_ActorKind_ActorId",
                table: "CognitiveMemory_Signals",
                columns: new[] { "ProjectId", "ActorKind", "ActorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Signals_ProjectId_RequiresReview_ObservedAt~",
                table: "CognitiveMemory_Signals",
                columns: new[] { "ProjectId", "RequiresReview", "ObservedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Signals_ProjectId_SignalKind_ObservedAtUtc",
                table: "CognitiveMemory_Signals",
                columns: new[] { "ProjectId", "SignalKind", "ObservedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Signals_ProjectId_SourceKind_ObservedAtUtc",
                table: "CognitiveMemory_Signals",
                columns: new[] { "ProjectId", "SourceKind", "ObservedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Signals_ProjectId_WorkspaceFrameId_Observed~",
                table: "CognitiveMemory_Signals",
                columns: new[] { "ProjectId", "WorkspaceFrameId", "ObservedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Signals_SignalScoreEvaluationTraceId",
                table: "CognitiveMemory_Signals",
                column: "SignalScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Signals_SourceItemId",
                table: "CognitiveMemory_Signals",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Signals_WorkspaceFrameId",
                table: "CognitiveMemory_Signals",
                column: "WorkspaceFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceItemContextHints_ContextFrameId",
                table: "CognitiveMemory_SourceItemContextHints",
                column: "ContextFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceItemContextHints_ProjectId_DimensionK~",
                table: "CognitiveMemory_SourceItemContextHints",
                columns: new[] { "ProjectId", "DimensionKind", "ValueKey" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceItemContextHints_SourceItemId_Context~",
                table: "CognitiveMemory_SourceItemContextHints",
                columns: new[] { "SourceItemId", "ContextFrameId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceItemGraphLinks_ProjectId_LinkKind",
                table: "CognitiveMemory_SourceItemGraphLinks",
                columns: new[] { "ProjectId", "LinkKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceItemGraphLinks_SourceItemId",
                table: "CognitiveMemory_SourceItemGraphLinks",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceItemGraphLinks_SourceManifestId_Sourc~",
                table: "CognitiveMemory_SourceItemGraphLinks",
                columns: new[] { "SourceManifestId", "SourceItemKey", "TargetSourceItemKey", "LinkKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceItemLayouts_ProjectId_SurfaceKind",
                table: "CognitiveMemory_SourceItemLayouts",
                columns: new[] { "ProjectId", "SurfaceKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceItemLayouts_SourceItemId",
                table: "CognitiveMemory_SourceItemLayouts",
                column: "SourceItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceItems_ContentHash",
                table: "CognitiveMemory_SourceItems",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceItems_ProjectId_SourceSystem_SourceIt~",
                table: "CognitiveMemory_SourceItems",
                columns: new[] { "ProjectId", "SourceSystem", "SourceItemType" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceItems_SourceManifestId_SourceItemKey",
                table: "CognitiveMemory_SourceItems",
                columns: new[] { "SourceManifestId", "SourceItemKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceLinks_MemoryRecordId_SourceItemId_Evi~",
                table: "CognitiveMemory_SourceLinks",
                columns: new[] { "MemoryRecordId", "SourceItemId", "EvidenceRole" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceLinks_SourceItemId",
                table: "CognitiveMemory_SourceLinks",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceLinks_SourceManifestId",
                table: "CognitiveMemory_SourceLinks",
                column: "SourceManifestId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceManifests_ProjectId_SourceSystem_Obse~",
                table: "CognitiveMemory_SourceManifests",
                columns: new[] { "ProjectId", "SourceSystem", "ObservedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceManifests_SourceSystem_SourceScopeKey~",
                table: "CognitiveMemory_SourceManifests",
                columns: new[] { "SourceSystem", "SourceScopeKey", "SourceSnapshotId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceScanFailures_ProjectId_SourceSystem_C~",
                table: "CognitiveMemory_SourceScanFailures",
                columns: new[] { "ProjectId", "SourceSystem", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceScanFailures_RunId",
                table: "CognitiveMemory_SourceScanFailures",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceScanFailures_SourceSystem_SourceScope~",
                table: "CognitiveMemory_SourceScanFailures",
                columns: new[] { "SourceSystem", "SourceScopeKey", "ExceptionCategory" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceTombstones_DetectedInManifestId",
                table: "CognitiveMemory_SourceTombstones",
                column: "DetectedInManifestId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceTombstones_PreviousSourceItemId",
                table: "CognitiveMemory_SourceTombstones",
                column: "PreviousSourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceTombstones_ProjectId_SourceSystem_Tom~",
                table: "CognitiveMemory_SourceTombstones",
                columns: new[] { "ProjectId", "SourceSystem", "TombstonedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceTombstones_SourceSystem_SourceScopeKe~",
                table: "CognitiveMemory_SourceTombstones",
                columns: new[] { "SourceSystem", "SourceScopeKey", "SourceItemKey", "DetectedInManifestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedRecalls_CreatedAtUtc",
                table: "CognitiveMemory_SynthesizedRecalls",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedRecalls_ProjectId_RecallTraceId",
                table: "CognitiveMemory_SynthesizedRecalls",
                columns: new[] { "ProjectId", "RecallTraceId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedRecalls_RecallTraceId",
                table: "CognitiveMemory_SynthesizedRecalls",
                column: "RecallTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedStatements_ProjectId_CreatedAtUtc",
                table: "CognitiveMemory_SynthesizedStatements",
                columns: new[] { "ProjectId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedStatements_SynthesisId_Sequence",
                table: "CognitiveMemory_SynthesizedStatements",
                columns: new[] { "SynthesisId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_AggregateCla~",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                column: "AggregateClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_EvidenceAnch~",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_MemoryRecord~",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                column: "MemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_ProjectId_Ac~",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                columns: new[] { "ProjectId", "AccessLevel", "RedactionState" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_SourceItemId",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_StatementId_~",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                columns: new[] { "StatementId", "AggregateClaimId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_StatementId~1",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                columns: new[] { "StatementId", "MemoryRecordId", "AggregateClaimId", "SourceItemId", "EvidenceAnchorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_SynthesisId",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                column: "SynthesisId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_TemporalEpisodeLinks_EpisodeId_LinkKind_Tar~",
                table: "CognitiveMemory_TemporalEpisodeLinks",
                columns: new[] { "EpisodeId", "LinkKind", "TargetId", "TargetKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_TemporalEpisodeLinks_ProjectId_LinkKind_Tar~",
                table: "CognitiveMemory_TemporalEpisodeLinks",
                columns: new[] { "ProjectId", "LinkKind", "TargetId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_TemporalEpisodes_ProjectId_EndedAtUtc",
                table: "CognitiveMemory_TemporalEpisodes",
                columns: new[] { "ProjectId", "EndedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_TemporalEpisodes_ProjectId_EpisodeKind_Star~",
                table: "CognitiveMemory_TemporalEpisodes",
                columns: new[] { "ProjectId", "EpisodeKind", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFocusSlots_AttentionScoreEvaluatio~",
                table: "CognitiveMemory_WorkspaceFocusSlots",
                column: "AttentionScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFocusSlots_ClaimId",
                table: "CognitiveMemory_WorkspaceFocusSlots",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFocusSlots_MemoryRecordId",
                table: "CognitiveMemory_WorkspaceFocusSlots",
                column: "MemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFocusSlots_OpenQuestionId",
                table: "CognitiveMemory_WorkspaceFocusSlots",
                column: "OpenQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFocusSlots_RecallTraceId",
                table: "CognitiveMemory_WorkspaceFocusSlots",
                column: "RecallTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFocusSlots_SourceItemId",
                table: "CognitiveMemory_WorkspaceFocusSlots",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFocusSlots_WorkspaceFrameId_SlotKi~",
                table: "CognitiveMemory_WorkspaceFocusSlots",
                columns: new[] { "WorkspaceFrameId", "SlotKind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFrames_CognitiveLoadScoreEvaluatio~",
                table: "CognitiveMemory_WorkspaceFrames",
                column: "CognitiveLoadScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFrames_LastAttentionDecisionId",
                table: "CognitiveMemory_WorkspaceFrames",
                column: "LastAttentionDecisionId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFrames_ProjectId_FrameKind_Status_~",
                table: "CognitiveMemory_WorkspaceFrames",
                columns: new[] { "ProjectId", "FrameKind", "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFrames_ProjectId_LearningTaskId_St~",
                table: "CognitiveMemory_WorkspaceFrames",
                columns: new[] { "ProjectId", "LearningTaskId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFrames_ProjectId_OwnerAgentId_Stat~",
                table: "CognitiveMemory_WorkspaceFrames",
                columns: new[] { "ProjectId", "OwnerAgentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFrames_ProjectId_OwnerUserId_Status",
                table: "CognitiveMemory_WorkspaceFrames",
                columns: new[] { "ProjectId", "OwnerUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFrames_ProjectId_ProbeSessionId_St~",
                table: "CognitiveMemory_WorkspaceFrames",
                columns: new[] { "ProjectId", "ProbeSessionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFrames_ProjectId_ProcessRunId_Proc~",
                table: "CognitiveMemory_WorkspaceFrames",
                columns: new[] { "ProjectId", "ProcessRunId", "ProcessStepId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFrames_ProjectId_ReviewSessionId_S~",
                table: "CognitiveMemory_WorkspaceFrames",
                columns: new[] { "ProjectId", "ReviewSessionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceFrames_ProjectId_WorkflowRunId_Sta~",
                table: "CognitiveMemory_WorkspaceFrames",
                columns: new[] { "ProjectId", "WorkflowRunId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceGoals_ProjectId_GoalKey",
                table: "CognitiveMemory_WorkspaceGoals",
                columns: new[] { "ProjectId", "GoalKey" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceGoals_WorkspaceFrameId_Sequence",
                table: "CognitiveMemory_WorkspaceGoals",
                columns: new[] { "WorkspaceFrameId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceInhibitedCandidates_ClaimId",
                table: "CognitiveMemory_WorkspaceInhibitedCandidates",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceInhibitedCandidates_InhibitionScor~",
                table: "CognitiveMemory_WorkspaceInhibitedCandidates",
                column: "InhibitionScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceInhibitedCandidates_MemoryRecordId",
                table: "CognitiveMemory_WorkspaceInhibitedCandidates",
                column: "MemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceInhibitedCandidates_SourceItemId",
                table: "CognitiveMemory_WorkspaceInhibitedCandidates",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceInhibitedCandidates_WorkspaceFrame~",
                table: "CognitiveMemory_WorkspaceInhibitedCandidates",
                columns: new[] { "WorkspaceFrameId", "ReasonKind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceOpenQuestions_ProjectId_Status",
                table: "CognitiveMemory_WorkspaceOpenQuestions",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceOpenQuestions_WorkspaceFrameId_Sta~",
                table: "CognitiveMemory_WorkspaceOpenQuestions",
                columns: new[] { "WorkspaceFrameId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceSlotEvidenceAnchors_EvidenceAnchor~",
                table: "CognitiveMemory_WorkspaceSlotEvidenceAnchors",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceSlotEvidenceAnchors_ProjectId_Evid~",
                table: "CognitiveMemory_WorkspaceSlotEvidenceAnchors",
                columns: new[] { "ProjectId", "EvidenceAnchorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceSlotEvidenceAnchors_WorkspaceFrame~",
                table: "CognitiveMemory_WorkspaceSlotEvidenceAnchors",
                columns: new[] { "WorkspaceFrameId", "EvidenceAnchorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_WorkspaceSlotEvidenceAnchors_WorkspaceSlotI~",
                table: "CognitiveMemory_WorkspaceSlotEvidenceAnchors",
                columns: new[] { "WorkspaceSlotId", "EvidenceAnchorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Collaboration_InboxItems_ItemKind_IsUnread",
                table: "Collaboration_InboxItems",
                columns: new[] { "ItemKind", "IsUnread" });

            migrationBuilder.CreateIndex(
                name: "IX_Collaboration_InboxItems_ThreadId",
                table: "Collaboration_InboxItems",
                column: "ThreadId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Collaboration_InboxItems_UpdatedAtUtc",
                table: "Collaboration_InboxItems",
                column: "UpdatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Collaboration_Messages_ThreadId_CreatedAtUtc",
                table: "Collaboration_Messages",
                columns: new[] { "ThreadId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Collaboration_Participants_ThreadId_ParticipantKey",
                table: "Collaboration_Participants",
                columns: new[] { "ThreadId", "ParticipantKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Collaboration_Threads_ContextKind_ContextId",
                table: "Collaboration_Threads",
                columns: new[] { "ContextKind", "ContextId" });

            migrationBuilder.CreateIndex(
                name: "IX_Collaboration_Threads_LastActivityAtUtc",
                table: "Collaboration_Threads",
                column: "LastActivityAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Collaboration_Threads_ProjectId",
                table: "Collaboration_Threads",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_AccountProfiles_AccountPartyId",
                table: "CrmHr_AccountProfiles",
                column: "AccountPartyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_AccountStakeholders_AccountPartyId_RelatedPartyId_Role",
                table: "CrmHr_AccountStakeholders",
                columns: new[] { "AccountPartyId", "RelatedPartyId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_AccountStakeholders_RelatedPartyId",
                table: "CrmHr_AccountStakeholders",
                column: "RelatedPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_AiAgentProfiles_PartyId",
                table: "CrmHr_AiAgentProfiles",
                column: "PartyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_AiAgentProfiles_ProviderProfileId",
                table: "CrmHr_AiAgentProfiles",
                column: "ProviderProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_AiResourceBindings_PartyId",
                table: "CrmHr_AiResourceBindings",
                column: "PartyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_AiResourceBindings_TechnicalAgentId",
                table: "CrmHr_AiResourceBindings",
                column: "TechnicalAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_AuditEntries_EntityType_EntityId",
                table: "CrmHr_AuditEntries",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_CapacityBlocks_PartyId_StartDateUtc_EndDateUtc",
                table: "CrmHr_CapacityBlocks",
                columns: new[] { "PartyId", "StartDateUtc", "EndDateUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_ConfidentialNotes_PartyId",
                table: "CrmHr_ConfidentialNotes",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_InteractionParties_InteractionId_PartyId_Role",
                table: "CrmHr_InteractionParties",
                columns: new[] { "InteractionId", "PartyId", "Role" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Interactions_RelatedOpportunityId",
                table: "CrmHr_Interactions",
                column: "RelatedOpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Interactions_RelatedProjectId",
                table: "CrmHr_Interactions",
                column: "RelatedProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_LookupOptions_CatalogKind_Key",
                table: "CrmHr_LookupOptions",
                columns: new[] { "CatalogKind", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_OnboardingTasks_PartyId_TaskKind_Status",
                table: "CrmHr_OnboardingTasks",
                columns: new[] { "PartyId", "TaskKind", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Opportunities_AccountPartyId",
                table: "CrmHr_Opportunities",
                column: "AccountPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Opportunities_LinkedProjectId",
                table: "CrmHr_Opportunities",
                column: "LinkedProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Opportunities_OwnerPartyId",
                table: "CrmHr_Opportunities",
                column: "OwnerPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Opportunities_Stage",
                table: "CrmHr_Opportunities",
                column: "Stage");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_OpportunityParties_OpportunityId_PartyId_Role",
                table: "CrmHr_OpportunityParties",
                columns: new[] { "OpportunityId", "PartyId", "Role" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_OpportunityStageHistory_OpportunityId_ChangedAtUtc",
                table: "CrmHr_OpportunityStageHistory",
                columns: new[] { "OpportunityId", "ChangedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Parties_DisplayName",
                table: "CrmHr_Parties",
                column: "DisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Parties_ExternalCode",
                table: "CrmHr_Parties",
                column: "ExternalCode");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Parties_PartyType_LifecycleStatus",
                table: "CrmHr_Parties",
                columns: new[] { "PartyType", "LifecycleStatus" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_PartyAddresses_PartyId_IsPrimary",
                table: "CrmHr_PartyAddresses",
                columns: new[] { "PartyId", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_PartyContactPoints_NormalizedValue",
                table: "CrmHr_PartyContactPoints",
                column: "NormalizedValue");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_PartyContactPoints_PartyId_IsPrimary",
                table: "CrmHr_PartyContactPoints",
                columns: new[] { "PartyId", "IsPrimary" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_PartyRelationships_SourcePartyId_TargetPartyId_Relati~",
                table: "CrmHr_PartyRelationships",
                columns: new[] { "SourcePartyId", "TargetPartyId", "RelationshipKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_PartyRelationships_TargetPartyId",
                table: "CrmHr_PartyRelationships",
                column: "TargetPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_PartyRoles_PartyId_RoleKind",
                table: "CrmHr_PartyRoles",
                columns: new[] { "PartyId", "RoleKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_PartySkills_PartyId_SkillId",
                table: "CrmHr_PartySkills",
                columns: new[] { "PartyId", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_ProjectPartyAssignments_OpportunityId",
                table: "CrmHr_ProjectPartyAssignments",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_ProjectPartyAssignments_PartyId",
                table: "CrmHr_ProjectPartyAssignments",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_ProjectPartyAssignments_ProjectId",
                table: "CrmHr_ProjectPartyAssignments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_ProjectPartyAssignments_ProjectId_PartyId_AssignmentK~",
                table: "CrmHr_ProjectPartyAssignments",
                columns: new[] { "ProjectId", "PartyId", "AssignmentKind", "NodeKey" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_RecruitmentApplications_PartyId_Stage",
                table: "CrmHr_RecruitmentApplications",
                columns: new[] { "PartyId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_RecruitmentInterviews_ApplicationId_ScheduledAtUtc",
                table: "CrmHr_RecruitmentInterviews",
                columns: new[] { "ApplicationId", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Skills_Name",
                table: "CrmHr_Skills",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_StaffingRequests_DeliveryUnitPartyId",
                table: "CrmHr_StaffingRequests",
                column: "DeliveryUnitPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_StaffingRequests_ProjectId",
                table: "CrmHr_StaffingRequests",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_WorkforceProfiles_HomeUnitPartyId",
                table: "CrmHr_WorkforceProfiles",
                column: "HomeUnitPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_WorkforceProfiles_ManagerPartyId",
                table: "CrmHr_WorkforceProfiles",
                column: "ManagerPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_WorkforceProfiles_PartyId",
                table: "CrmHr_WorkforceProfiles",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_WorkforceProfiles_Status",
                table: "CrmHr_WorkforceProfiles",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Infrastructure_SearchDocuments_SourceType_SourceKey",
                table: "Infrastructure_SearchDocuments",
                columns: new[] { "SourceType", "SourceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_CapabilityGrants_PluginId_Capability_RecipeId_Scope~",
                table: "Plugins_CapabilityGrants",
                columns: new[] { "PluginId", "Capability", "RecipeId", "ScopeKind", "ScopeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_CapabilityGrants_PluginId_State_UpdatedAtUtc",
                table: "Plugins_CapabilityGrants",
                columns: new[] { "PluginId", "State", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_Connections_PluginId_ConnectionKey",
                table: "Plugins_Connections",
                columns: new[] { "PluginId", "ConnectionKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_Connections_PluginId_ConnectionKey_DisplayName",
                table: "Plugins_Connections",
                columns: new[] { "PluginId", "ConnectionKey", "DisplayName" });

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_Installations_IsEnabled_UpdatedAtUtc",
                table: "Plugins_Installations",
                columns: new[] { "IsEnabled", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_Installations_PluginId",
                table: "Plugins_Installations",
                column: "PluginId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_Logs_PackageId_CreatedAtUtc",
                table: "Plugins_Logs",
                columns: new[] { "PackageId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_Logs_StreamKind_PluginId_CreatedAtUtc",
                table: "Plugins_Logs",
                columns: new[] { "StreamKind", "PluginId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_OAuthConnections_ConnectionId",
                table: "Plugins_OAuthConnections",
                column: "ConnectionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_OAuthConnections_PluginId_ConnectionKey",
                table: "Plugins_OAuthConnections",
                columns: new[] { "PluginId", "ConnectionKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_OAuthSessions_PluginId_ConnectionId_Status",
                table: "Plugins_OAuthSessions",
                columns: new[] { "PluginId", "ConnectionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Plugins_OAuthSessions_StateHash",
                table: "Plugins_OAuthSessions",
                column: "StateHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ArtifactExpectations_StepDefinitionId",
                table: "Processes_ArtifactExpectations",
                column: "StepDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ArtifactRecords_ArtifactExpectationId",
                table: "Processes_ArtifactRecords",
                column: "ArtifactExpectationId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ArtifactRecords_ProcessRunId",
                table: "Processes_ArtifactRecords",
                column: "ProcessRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ArtifactRecords_StepRunId",
                table: "Processes_ArtifactRecords",
                column: "StepRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ConformanceObservations_ProcessRunId",
                table: "Processes_ConformanceObservations",
                column: "ProcessRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ConformanceObservations_StepRunId",
                table: "Processes_ConformanceObservations",
                column: "StepRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_DecisionRecords_BranchOutcomeId",
                table: "Processes_DecisionRecords",
                column: "BranchOutcomeId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_DecisionRecords_ProcessRunId_CreatedAtUtc",
                table: "Processes_DecisionRecords",
                columns: new[] { "ProcessRunId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_DecisionRecords_StepRunId",
                table: "Processes_DecisionRecords",
                column: "StepRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Definitions_ActivePublishedVersionId",
                table: "Processes_Definitions",
                column: "ActivePublishedVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Definitions_Id_ActivePublishedVersionId",
                table: "Processes_Definitions",
                columns: new[] { "Id", "ActivePublishedVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Definitions_ProjectId",
                table: "Processes_Definitions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Definitions_Slug",
                table: "Processes_Definitions",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Definitions_Status",
                table: "Processes_Definitions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_DefinitionVersions_ManagerAgentOverrideId",
                table: "Processes_DefinitionVersions",
                column: "ManagerAgentOverrideId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_DefinitionVersions_ProcessDefinitionId_VersionNum~",
                table: "Processes_DefinitionVersions",
                columns: new[] { "ProcessDefinitionId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_ProcessVersions_DraftPerDef",
                table: "Processes_DefinitionVersions",
                columns: new[] { "ProcessDefinitionId", "Status" },
                unique: true,
                filter: "\"Status\" = 'Draft'");

            migrationBuilder.CreateIndex(
                name: "UX_ProcessVersions_PubPerDef",
                table: "Processes_DefinitionVersions",
                column: "ProcessDefinitionId",
                unique: true,
                filter: "\"Status\" = 'Published'");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ImprovementCandidates_ProcessDefinitionId",
                table: "Processes_ImprovementCandidates",
                column: "ProcessDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ImprovementCandidates_ProcessRunId",
                table: "Processes_ImprovementCandidates",
                column: "ProcessRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_ImprovementCandidates_Status",
                table: "Processes_ImprovementCandidates",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_JournalEntries_ProcessRunId_OccurredAtUtc",
                table: "Processes_JournalEntries",
                columns: new[] { "ProcessRunId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_JournalEntries_StepRunId",
                table: "Processes_JournalEntries",
                column: "StepRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchApprovals_CollaborationThreadId",
                table: "Processes_LaunchApprovals",
                column: "CollaborationThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchApprovals_LaunchPlanId_CreatedAtUtc",
                table: "Processes_LaunchApprovals",
                columns: new[] { "LaunchPlanId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchApprovals_Status",
                table: "Processes_LaunchApprovals",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchCandidates_LaunchPlanRoleId_Score",
                table: "Processes_LaunchCandidates",
                columns: new[] { "LaunchPlanRoleId", "Score" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchCandidates_PartyId",
                table: "Processes_LaunchCandidates",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchCandidates_TechnicalAgentId",
                table: "Processes_LaunchCandidates",
                column: "TechnicalAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchCandidates_WorkflowDefinitionId",
                table: "Processes_LaunchCandidates",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchCandidates_WorkflowVersionId",
                table: "Processes_LaunchCandidates",
                column: "WorkflowVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchPlanRoles_LaunchPlanId_DisplayOrder",
                table: "Processes_LaunchPlanRoles",
                columns: new[] { "LaunchPlanId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchPlanRoles_RoleRequirementId",
                table: "Processes_LaunchPlanRoles",
                column: "RoleRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchPlanRoles_SelectedCandidateId",
                table: "Processes_LaunchPlanRoles",
                column: "SelectedCandidateId");

            migrationBuilder.CreateIndex(
                name: "UX_ProcessLaunchPlanRoles_Role",
                table: "Processes_LaunchPlanRoles",
                columns: new[] { "LaunchPlanId", "RoleRequirementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchPlans_GeneratedRunId",
                table: "Processes_LaunchPlans",
                column: "GeneratedRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchPlans_ProcessDefinitionId_CreatedAtUtc",
                table: "Processes_LaunchPlans",
                columns: new[] { "ProcessDefinitionId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchPlans_ProcessDefinitionId_ProcessDefinition~",
                table: "Processes_LaunchPlans",
                columns: new[] { "ProcessDefinitionId", "ProcessDefinitionVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchPlans_ProjectId_CreatedAtUtc",
                table: "Processes_LaunchPlans",
                columns: new[] { "ProjectId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchPlans_Status",
                table: "Processes_LaunchPlans",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchProvisioningRequests_LaunchPlanId_Status",
                table: "Processes_LaunchProvisioningRequests",
                columns: new[] { "LaunchPlanId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchProvisioningRequests_LaunchPlanRoleId",
                table: "Processes_LaunchProvisioningRequests",
                column: "LaunchPlanRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_LaunchProvisioningRequests_SelectedCandidateId",
                table: "Processes_LaunchProvisioningRequests",
                column: "SelectedCandidateId");

            migrationBuilder.CreateIndex(
                name: "UX_ProcessLaunchProvisioning_Role",
                table: "Processes_LaunchProvisioningRequests",
                columns: new[] { "LaunchPlanId", "LaunchPlanRoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Outbox_ProcessDefinitionId_CreatedAtUtc",
                table: "Processes_Outbox",
                columns: new[] { "ProcessDefinitionId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Outbox_ProcessRunId_CreatedAtUtc",
                table: "Processes_Outbox",
                columns: new[] { "ProcessRunId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Outbox_ProjectId_CreatedAtUtc",
                table: "Processes_Outbox",
                columns: new[] { "ProjectId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Outbox_Status_NextAttemptAtUtc_LeaseExpiresAtUtc",
                table: "Processes_Outbox",
                columns: new[] { "Status", "NextAttemptAtUtc", "LeaseExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RoleMessagingPolicies_ProcessDefinitionVersionId_~",
                table: "Processes_RoleMessagingPolicies",
                columns: new[] { "ProcessDefinitionVersionId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RoleMessagingPolicies_SourceRoleRequirementId",
                table: "Processes_RoleMessagingPolicies",
                column: "SourceRoleRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RoleMessagingPolicies_TargetRoleRequirementId",
                table: "Processes_RoleMessagingPolicies",
                column: "TargetRoleRequirementId");

            migrationBuilder.CreateIndex(
                name: "UX_ProcessRoleMessagingPolicies_SourceTarget",
                table: "Processes_RoleMessagingPolicies",
                columns: new[] { "ProcessDefinitionVersionId", "SourceRoleRequirementId", "TargetRoleRequirementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RoleRequirements_PreferredWorkflowDefinitionId",
                table: "Processes_RoleRequirements",
                column: "PreferredWorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RoleRequirements_PreferredWorkflowVersionId",
                table: "Processes_RoleRequirements",
                column: "PreferredWorkflowVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RoleRequirements_ProcessDefinitionVersionId_Key",
                table: "Processes_RoleRequirements",
                columns: new[] { "ProcessDefinitionVersionId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RoleSkillRequirements_RoleRequirementId_SkillId",
                table: "Processes_RoleSkillRequirements",
                columns: new[] { "RoleRequirementId", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RoleSkillRequirements_SkillId",
                table: "Processes_RoleSkillRequirements",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RunAssignments_PartyId",
                table: "Processes_RunAssignments",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RunAssignments_RoleRequirementId",
                table: "Processes_RunAssignments",
                column: "RoleRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RunAssignments_StepDefinitionId",
                table: "Processes_RunAssignments",
                column: "StepDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RunAssignments_WorkflowDefinitionId",
                table: "Processes_RunAssignments",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_RunAssignments_WorkflowVersionId",
                table: "Processes_RunAssignments",
                column: "WorkflowVersionId");

            migrationBuilder.CreateIndex(
                name: "UX_ProcessRunAssignments_RunScoped",
                table: "Processes_RunAssignments",
                columns: new[] { "ProcessRunId", "RoleRequirementId" },
                unique: true,
                filter: "\"StepDefinitionId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ProcessRunAssignments_StepScoped",
                table: "Processes_RunAssignments",
                columns: new[] { "ProcessRunId", "RoleRequirementId", "StepDefinitionId" },
                unique: true,
                filter: "\"StepDefinitionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Runs_ManagerAgentId",
                table: "Processes_Runs",
                column: "ManagerAgentId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Runs_ParentRunId",
                table: "Processes_Runs",
                column: "ParentRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Runs_ProcessDefinitionId",
                table: "Processes_Runs",
                column: "ProcessDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Runs_ProcessDefinitionId_ProcessDefinitionVersion~",
                table: "Processes_Runs",
                columns: new[] { "ProcessDefinitionId", "ProcessDefinitionVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Runs_ProjectId",
                table: "Processes_Runs",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Runs_RootRunId",
                table: "Processes_Runs",
                column: "RootRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_Runs_Status",
                table: "Processes_Runs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "UX_ProcessRuns_ParentStepRun",
                table: "Processes_Runs",
                column: "ParentStepRunId",
                unique: true,
                filter: "\"ParentStepRunId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepArtifactInputs_ArtifactExpectationId",
                table: "Processes_StepArtifactInputs",
                column: "ArtifactExpectationId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepArtifactInputs_StepDefinitionId",
                table: "Processes_StepArtifactInputs",
                column: "StepDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepArtifactInputs_StepDefinitionId_ArtifactExpec~",
                table: "Processes_StepArtifactInputs",
                columns: new[] { "StepDefinitionId", "ArtifactExpectationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepArtifactInputs_StepDefinitionId_DisplayOrder",
                table: "Processes_StepArtifactInputs",
                columns: new[] { "StepDefinitionId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepBranchOutcomes_StepDefinitionId_DisplayOrder",
                table: "Processes_StepBranchOutcomes",
                columns: new[] { "StepDefinitionId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepBranchOutcomes_StepDefinitionId_Key",
                table: "Processes_StepBranchOutcomes",
                columns: new[] { "StepDefinitionId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepDefinitions_DecisionRoleRequirementId",
                table: "Processes_StepDefinitions",
                column: "DecisionRoleRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepDefinitions_ProcessDefinitionVersionId_Key",
                table: "Processes_StepDefinitions",
                columns: new[] { "ProcessDefinitionVersionId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepDefinitions_ProcessDefinitionVersionId_OrderI~",
                table: "Processes_StepDefinitions",
                columns: new[] { "ProcessDefinitionVersionId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepDefinitions_SubprocessDefinitionId",
                table: "Processes_StepDefinitions",
                column: "SubprocessDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepDependencies_DependsOnBranchOutcomeId",
                table: "Processes_StepDependencies",
                column: "DependsOnBranchOutcomeId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepDependencies_DependsOnStepId",
                table: "Processes_StepDependencies",
                column: "DependsOnStepId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepDependencies_StepDefinitionId",
                table: "Processes_StepDependencies",
                column: "StepDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepDependencies_StepDefinitionId_DisplayOrder",
                table: "Processes_StepDependencies",
                columns: new[] { "StepDefinitionId", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "UX_ProcessStepDeps_Conditional",
                table: "Processes_StepDependencies",
                columns: new[] { "StepDefinitionId", "DependsOnStepId", "DependsOnBranchOutcomeId" },
                unique: true,
                filter: "\"DependsOnBranchOutcomeId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ProcessStepDeps_Unconditional",
                table: "Processes_StepDependencies",
                columns: new[] { "StepDefinitionId", "DependsOnStepId" },
                unique: true,
                filter: "\"DependsOnBranchOutcomeId\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepRoleRequirements_RoleRequirementId",
                table: "Processes_StepRoleRequirements",
                column: "RoleRequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepRoleRequirements_StepDefinitionId_RoleRequire~",
                table: "Processes_StepRoleRequirements",
                columns: new[] { "StepDefinitionId", "RoleRequirementId", "ResponsibilityKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepRuns_ProcessRunId_Sequence",
                table: "Processes_StepRuns",
                columns: new[] { "ProcessRunId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepRuns_ProcessRunId_Status",
                table: "Processes_StepRuns",
                columns: new[] { "ProcessRunId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepRuns_SelectedBranchOutcomeId",
                table: "Processes_StepRuns",
                column: "SelectedBranchOutcomeId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_StepRuns_StepDefinitionId",
                table: "Processes_StepRuns",
                column: "StepDefinitionId");

            migrationBuilder.CreateIndex(
                name: "UX_ProcessStepRuns_RunStep",
                table: "Processes_StepRuns",
                columns: new[] { "ProcessRunId", "StepDefinitionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_WorkBriefs_ProcessRunId",
                table: "Processes_WorkBriefs",
                column: "ProcessRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_WorkBriefs_StepRunId",
                table: "Processes_WorkBriefs",
                column: "StepRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_WorkflowRunLinks_AssignmentId",
                table: "Processes_WorkflowRunLinks",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_WorkflowRunLinks_ProcessRunId",
                table: "Processes_WorkflowRunLinks",
                column: "ProcessRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_WorkflowRunLinks_StepRunId_AssignmentId",
                table: "Processes_WorkflowRunLinks",
                columns: new[] { "StepRunId", "AssignmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processes_WorkflowRunLinks_WorkflowDefinitionId",
                table: "Processes_WorkflowRunLinks",
                column: "WorkflowDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_WorkflowRunLinks_WorkflowRunId",
                table: "Processes_WorkflowRunLinks",
                column: "WorkflowRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjectHierarchyLinks_ChildProjectId",
                table: "Projects_ProjectHierarchyLinks",
                column: "ChildProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjectHierarchyLinks_ParentProjectId",
                table: "Projects_ProjectHierarchyLinks",
                column: "ParentProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjectHierarchyLinks_ParentProjectId_ChildProject~",
                table: "Projects_ProjectHierarchyLinks",
                columns: new[] { "ParentProjectId", "ChildProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjectOptionSelections_ProjectId_Category",
                table: "Projects_ProjectOptionSelections",
                columns: new[] { "ProjectId", "Category" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjectPhases_ProjectId_OrderIndex",
                table: "Projects_ProjectPhases",
                columns: new[] { "ProjectId", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_Prompts_PromptTags_Name",
                table: "Prompts_PromptTags",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prompts_PromptVersions_PromptArtifactId_VersionNumber",
                table: "Prompts_PromptVersions",
                columns: new[] { "PromptArtifactId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Security_SecretReferences_ContextType_ContextId",
                table: "Security_SecretReferences",
                columns: new[] { "ContextType", "ContextId" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulerPlanner_Plans_AutomationTriggerId",
                table: "SchedulerPlanner_Plans",
                column: "AutomationTriggerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulerPlanner_Plans_NextPlannedFireAtUtc",
                table: "SchedulerPlanner_Plans",
                column: "NextPlannedFireAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulerPlanner_Plans_TargetKind_TargetId_IsEnabled",
                table: "SchedulerPlanner_Plans",
                columns: new[] { "TargetKind", "TargetId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_SchedulerPlanner_Runs_DedupeKey",
                table: "SchedulerPlanner_Runs",
                column: "DedupeKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulerPlanner_Runs_PlanId_FiredAtUtc",
                table: "SchedulerPlanner_Runs",
                columns: new[] { "PlanId", "FiredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Storage_Catalog_Name",
                table: "Storage_Catalog",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Storage_Catalog_ProviderKind_IsEnabled",
                table: "Storage_Catalog",
                columns: new[] { "ProviderKind", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_Storage_RoutingRules_ScopeKind_ProjectId_NodeKey_Priority_P~",
                table: "Storage_RoutingRules",
                columns: new[] { "ScopeKind", "ProjectId", "NodeKey", "Priority", "PreferredStorageId" });

            migrationBuilder.CreateIndex(
                name: "IX_Validation_Runs_CreatedAtUtc",
                table: "Validation_Runs",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectCrossModuleMutations_ProjectId_ApprovalSta~",
                table: "Workbench_ProjectCrossModuleMutations",
                columns: new[] { "ProjectId", "ApprovalState", "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectCrossModuleMutations_ProjectId_ScopeNodeKe~",
                table: "Workbench_ProjectCrossModuleMutations",
                columns: new[] { "ProjectId", "ScopeNodeKey", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectCrossModuleMutations_ProjectId_Status_Upda~",
                table: "Workbench_ProjectCrossModuleMutations",
                columns: new[] { "ProjectId", "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectNodeBindings_ProjectObjectId",
                table: "Workbench_ProjectNodeBindings",
                column: "ProjectObjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectNodeLifecycleEvents_ProjectId_NodeKey_Occu~",
                table: "Workbench_ProjectNodeLifecycleEvents",
                columns: new[] { "ProjectId", "NodeKey", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectNodeLifecycleEvents_ProjectObjectId",
                table: "Workbench_ProjectNodeLifecycleEvents",
                column: "ProjectObjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectNodeReferences_ProjectObjectId_ReferenceK~1",
                table: "Workbench_ProjectNodeReferences",
                columns: new[] { "ProjectObjectId", "ReferenceKind", "ReferenceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectNodeReferences_ProjectObjectId_ReferenceKi~",
                table: "Workbench_ProjectNodeReferences",
                columns: new[] { "ProjectObjectId", "ReferenceKind", "OrderIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectObjectLinks_ProjectId_SourceNodeKey_Target~",
                table: "Workbench_ProjectObjectLinks",
                columns: new[] { "ProjectId", "SourceNodeKey", "TargetNodeKey", "LinkKind", "IsSystemManaged" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectObjects_ProjectId_NodeKey",
                table: "Workbench_ProjectObjects",
                columns: new[] { "ProjectId", "NodeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectProjectionLayouts_ProjectId_NodeKey",
                table: "Workbench_ProjectProjectionLayouts",
                columns: new[] { "ProjectId", "NodeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectStructureLeases_LeaseToken",
                table: "Workbench_ProjectStructureLeases",
                column: "LeaseToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectStructureLeases_ScopeKind_ScopeKey",
                table: "Workbench_ProjectStructureLeases",
                columns: new[] { "ScopeKind", "ScopeKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectStructureOperationAnalytics_OccurredAtUtc",
                table: "Workbench_ProjectStructureOperationAnalytics",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectStructureOperationAnalytics_ProjectId_Oper~",
                table: "Workbench_ProjectStructureOperationAnalytics",
                columns: new[] { "ProjectId", "OperationName" });

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ViewStates_ProjectId_SurfaceKind",
                table: "Workbench_ViewStates",
                columns: new[] { "ProjectId", "SurfaceKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workspace_ConnectorCommandAudits_ConnectorCommandId_Created~",
                table: "Workspace_ConnectorCommandAudits",
                columns: new[] { "ConnectorCommandId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Workspace_ConnectorCommands_ProjectId_ConnectorPluginKey_Co~",
                table: "Workspace_ConnectorCommands",
                columns: new[] { "ProjectId", "ConnectorPluginKey", "CommandKey", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workspace_ConnectorCommands_ProjectId_CreatedAtUtc",
                table: "Workspace_ConnectorCommands",
                columns: new[] { "ProjectId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Workspace_ConnectorCommands_Status_ApprovalState_NextAttemp~",
                table: "Workspace_ConnectorCommands",
                columns: new[] { "Status", "ApprovalState", "NextAttemptAtUtc", "LeaseExpiresAtUtc" });

            migrationBuilder.AddForeignKey(
                name: "FK_CognitiveMemory_BeliefStates_CognitiveMemory_Claims_ClaimId",
                table: "CognitiveMemory_BeliefStates",
                column: "ClaimId",
                principalTable: "CognitiveMemory_Claims",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CognitiveMemory_ClaimEvidenceLinks_CognitiveMemory_Claims_C~",
                table: "CognitiveMemory_ClaimEvidenceLinks",
                column: "ClaimId",
                principalTable: "CognitiveMemory_Claims",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CognitiveMemory_Claims_CognitiveMemory_Records_MemoryRecord~",
                table: "CognitiveMemory_Claims",
                column: "MemoryRecordId",
                principalTable: "CognitiveMemory_Records",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CognitiveMemory_DreamAggregateCandidates_CognitiveMemory_D~1",
                table: "CognitiveMemory_DreamAggregateCandidates",
                column: "ValidationRecordId",
                principalTable: "CognitiveMemory_DreamValidations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_ArtifactExpectations_Processes_StepDefinitions_St~",
                table: "Processes_ArtifactExpectations",
                column: "StepDefinitionId",
                principalTable: "Processes_StepDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_ArtifactRecords_Processes_Runs_ProcessRunId",
                table: "Processes_ArtifactRecords",
                column: "ProcessRunId",
                principalTable: "Processes_Runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_ArtifactRecords_Processes_StepRuns_StepRunId",
                table: "Processes_ArtifactRecords",
                column: "StepRunId",
                principalTable: "Processes_StepRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_ConformanceObservations_Processes_Runs_ProcessRun~",
                table: "Processes_ConformanceObservations",
                column: "ProcessRunId",
                principalTable: "Processes_Runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_ConformanceObservations_Processes_StepRuns_StepRu~",
                table: "Processes_ConformanceObservations",
                column: "StepRunId",
                principalTable: "Processes_StepRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_DecisionRecords_Processes_Runs_ProcessRunId",
                table: "Processes_DecisionRecords",
                column: "ProcessRunId",
                principalTable: "Processes_Runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_DecisionRecords_Processes_StepBranchOutcomes_Bran~",
                table: "Processes_DecisionRecords",
                column: "BranchOutcomeId",
                principalTable: "Processes_StepBranchOutcomes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_DecisionRecords_Processes_StepRuns_StepRunId",
                table: "Processes_DecisionRecords",
                column: "StepRunId",
                principalTable: "Processes_StepRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_Definitions_Processes_DefinitionVersions_Id_Activ~",
                table: "Processes_Definitions",
                columns: new[] { "Id", "ActivePublishedVersionId" },
                principalTable: "Processes_DefinitionVersions",
                principalColumns: new[] { "ProcessDefinitionId", "Id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_ImprovementCandidates_Processes_Runs_ProcessRunId",
                table: "Processes_ImprovementCandidates",
                column: "ProcessRunId",
                principalTable: "Processes_Runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_JournalEntries_Processes_Runs_ProcessRunId",
                table: "Processes_JournalEntries",
                column: "ProcessRunId",
                principalTable: "Processes_Runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_JournalEntries_Processes_StepRuns_StepRunId",
                table: "Processes_JournalEntries",
                column: "StepRunId",
                principalTable: "Processes_StepRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_LaunchApprovals_Processes_LaunchPlans_LaunchPlanId",
                table: "Processes_LaunchApprovals",
                column: "LaunchPlanId",
                principalTable: "Processes_LaunchPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_LaunchCandidates_Processes_LaunchPlanRoles_Launch~",
                table: "Processes_LaunchCandidates",
                column: "LaunchPlanRoleId",
                principalTable: "Processes_LaunchPlanRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_LaunchPlanRoles_Processes_LaunchPlans_LaunchPlanId",
                table: "Processes_LaunchPlanRoles",
                column: "LaunchPlanId",
                principalTable: "Processes_LaunchPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_LaunchPlans_Processes_Runs_GeneratedRunId",
                table: "Processes_LaunchPlans",
                column: "GeneratedRunId",
                principalTable: "Processes_Runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_RunAssignments_Processes_Runs_ProcessRunId",
                table: "Processes_RunAssignments",
                column: "ProcessRunId",
                principalTable: "Processes_Runs",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_Runs_Processes_StepRuns_ParentStepRunId",
                table: "Processes_Runs",
                column: "ParentStepRunId",
                principalTable: "Processes_StepRuns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_Claims_CognitiveMemory_ScoreEvaluations_Cur~",
                table: "CognitiveMemory_Claims");

            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_ContextFrames_CognitiveMemory_ScoreEvaluati~",
                table: "CognitiveMemory_ContextFrames");

            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_Records_CognitiveMemory_ScoreEvaluations_Ac~",
                table: "CognitiveMemory_Records");

            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_Records_CognitiveMemory_ScoreEvaluations_Co~",
                table: "CognitiveMemory_Records");

            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_Records_CognitiveMemory_Claims_PrimaryClaim~",
                table: "CognitiveMemory_Records");

            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_Records_CognitiveMemory_ContextFrames_Prima~",
                table: "CognitiveMemory_Records");

            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_DreamAggregateCandidates_CognitiveMemory_Re~",
                table: "CognitiveMemory_DreamAggregateCandidates");

            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_DreamAggregateCandidates_CognitiveMemory_Dr~",
                table: "CognitiveMemory_DreamAggregateCandidates");

            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_DreamAggregateCandidates_CognitiveMemory_D~1",
                table: "CognitiveMemory_DreamAggregateCandidates");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_StepBranchOutcomes_Processes_StepDefinitions_Step~",
                table: "Processes_StepBranchOutcomes");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_StepRuns_Processes_StepDefinitions_StepDefinition~",
                table: "Processes_StepRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_StepRuns_Processes_Runs_ProcessRunId",
                table: "Processes_StepRuns");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_Definitions_Processes_DefinitionVersions_Id_Activ~",
                table: "Processes_Definitions");

            migrationBuilder.DropTable(
                name: "Activity_Entries");

            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowArtifacts");

            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowComponents");

            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowDefinitions");

            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowEvents");

            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowExternalRequests");

            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowRuns");

            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowSettings");

            migrationBuilder.DropTable(
                name: "Automation_DeadLetters");

            migrationBuilder.DropTable(
                name: "Automation_DeliveryAttempts");

            migrationBuilder.DropTable(
                name: "Automation_EnvelopeDeliveries");

            migrationBuilder.DropTable(
                name: "Automation_ExecutionLogs");

            migrationBuilder.DropTable(
                name: "Automation_PluginIngressCursors");

            migrationBuilder.DropTable(
                name: "Automation_PluginIngressEnvelopes");

            migrationBuilder.DropTable(
                name: "Automation_Triggers");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_AnswerGateDecisions");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_AnswerPostureDecisions");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_AutomationSettings");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_BeliefStates");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_CalibrationAggregates");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_CalibrationBins");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_CalibrationEvents");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ClaimEvidenceLinks");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ConfidenceReinforcements");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ConsolidationCursors");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ConsolidationReports");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ContextBoundaries");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ContextFrameDimensions");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_CoverageMaps");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_CrossProjectPromotionCandidates");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_CuratorCapturedImprovements");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_CuratorSessions");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_CuratorTurns");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_DistributedJobs");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_DistributedWorkerResults");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_DistributedWorkers");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_DomainCompetenceProfiles");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_DreamAggregateClaimSourceMaps");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_DreamRunClusters");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_EntityAliases");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_EpisodeCausalLinks");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_EpisodeStepEvidence");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ExternalSourceIngestions");

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
                name: "CognitiveMemory_MutationAuditEvents");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_PredictionErrorEvidenceAnchors");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_PredictionErrorSignals");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_PredictionExpectationEvidenceAnchors");

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
                name: "CognitiveMemory_ProcedureAutomationBindings");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProcedureFailureModeEpisodes");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProcedureFailureModePredictionErrors");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProcedureSimulationEvidence");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProcedureSimulationSkills");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProcedureStepEvidence");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProcedureValidationEvidence");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProfessorReviewActions");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProfessorReviews");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_Projections");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProjectionStates");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_QualityClusterKeys");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_QualityClusterMembers");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_RecallCandidates");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_RecallContextSections");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_RecallSourceRefs");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_RecallTraceStages");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_RecordEvidenceAnchors");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_RelationEvidence");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ReplayJobPredictionErrors");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ReplayJobSignals");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ReplayJobTargets");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ReplayOutputs");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ReplayWorkerResults");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ScoreComponents");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SelfModelProfiles");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SelfModelUpdateProposals");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SelfRegulationAssessments");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SelfRegulationPolicyProfiles");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SignalConsumerPolicies");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SignalEvidenceAnchors");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SourceItemContextHints");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SourceItemGraphLinks");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SourceItemLayouts");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SourceLinks");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SourceScanFailures");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SourceTombstones");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SynthesizedStatementSourceMaps");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_TemporalEpisodeLinks");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_WorkspaceGoals");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_WorkspaceInhibitedCandidates");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_WorkspaceSlotEvidenceAnchors");

            migrationBuilder.DropTable(
                name: "Collaboration_InboxItems");

            migrationBuilder.DropTable(
                name: "Collaboration_Messages");

            migrationBuilder.DropTable(
                name: "Collaboration_Participants");

            migrationBuilder.DropTable(
                name: "Collaboration_Threads");

            migrationBuilder.DropTable(
                name: "CrmHr_AccountProfiles");

            migrationBuilder.DropTable(
                name: "CrmHr_AccountStakeholders");

            migrationBuilder.DropTable(
                name: "CrmHr_AiAgentProfiles");

            migrationBuilder.DropTable(
                name: "CrmHr_AiResourceBindings");

            migrationBuilder.DropTable(
                name: "CrmHr_AuditEntries");

            migrationBuilder.DropTable(
                name: "CrmHr_CapacityBlocks");

            migrationBuilder.DropTable(
                name: "CrmHr_ConfidentialNotes");

            migrationBuilder.DropTable(
                name: "CrmHr_InteractionParties");

            migrationBuilder.DropTable(
                name: "CrmHr_Interactions");

            migrationBuilder.DropTable(
                name: "CrmHr_LookupOptions");

            migrationBuilder.DropTable(
                name: "CrmHr_OnboardingTasks");

            migrationBuilder.DropTable(
                name: "CrmHr_Opportunities");

            migrationBuilder.DropTable(
                name: "CrmHr_OpportunityParties");

            migrationBuilder.DropTable(
                name: "CrmHr_OpportunityStageHistory");

            migrationBuilder.DropTable(
                name: "CrmHr_Parties");

            migrationBuilder.DropTable(
                name: "CrmHr_PartyAddresses");

            migrationBuilder.DropTable(
                name: "CrmHr_PartyContactPoints");

            migrationBuilder.DropTable(
                name: "CrmHr_PartyRelationships");

            migrationBuilder.DropTable(
                name: "CrmHr_PartyRoles");

            migrationBuilder.DropTable(
                name: "CrmHr_PartySkills");

            migrationBuilder.DropTable(
                name: "CrmHr_ProjectPartyAssignments");

            migrationBuilder.DropTable(
                name: "CrmHr_RecruitmentApplications");

            migrationBuilder.DropTable(
                name: "CrmHr_RecruitmentInterviews");

            migrationBuilder.DropTable(
                name: "CrmHr_Skills");

            migrationBuilder.DropTable(
                name: "CrmHr_StaffingRequests");

            migrationBuilder.DropTable(
                name: "CrmHr_WorkforceProfiles");

            migrationBuilder.DropTable(
                name: "Factory_PromptBlocks");

            migrationBuilder.DropTable(
                name: "Factory_PromptBlueprints");

            migrationBuilder.DropTable(
                name: "Factory_PromptBuildSessions");

            migrationBuilder.DropTable(
                name: "Factory_PromptFlowTemplates");

            migrationBuilder.DropTable(
                name: "Factory_PromptRunNodes");

            migrationBuilder.DropTable(
                name: "Factory_PromptRuns");

            migrationBuilder.DropTable(
                name: "Infrastructure_BackgroundJobRecords");

            migrationBuilder.DropTable(
                name: "Infrastructure_SearchDocuments");

            migrationBuilder.DropTable(
                name: "Plugins_CapabilityGrants");

            migrationBuilder.DropTable(
                name: "Plugins_Connections");

            migrationBuilder.DropTable(
                name: "Plugins_Installations");

            migrationBuilder.DropTable(
                name: "Plugins_Logs");

            migrationBuilder.DropTable(
                name: "Plugins_OAuthConnections");

            migrationBuilder.DropTable(
                name: "Plugins_OAuthSessions");

            migrationBuilder.DropTable(
                name: "Processes_ArtifactRecords");

            migrationBuilder.DropTable(
                name: "Processes_ConformanceObservations");

            migrationBuilder.DropTable(
                name: "Processes_DecisionRecords");

            migrationBuilder.DropTable(
                name: "Processes_ImprovementCandidates");

            migrationBuilder.DropTable(
                name: "Processes_JournalEntries");

            migrationBuilder.DropTable(
                name: "Processes_LaunchApprovals");

            migrationBuilder.DropTable(
                name: "Processes_LaunchProvisioningRequests");

            migrationBuilder.DropTable(
                name: "Processes_Outbox");

            migrationBuilder.DropTable(
                name: "Processes_RoleMessagingPolicies");

            migrationBuilder.DropTable(
                name: "Processes_RoleSkillRequirements");

            migrationBuilder.DropTable(
                name: "Processes_StepArtifactInputs");

            migrationBuilder.DropTable(
                name: "Processes_StepDependencies");

            migrationBuilder.DropTable(
                name: "Processes_StepRoleRequirements");

            migrationBuilder.DropTable(
                name: "Processes_WorkBriefs");

            migrationBuilder.DropTable(
                name: "Processes_WorkflowRunLinks");

            migrationBuilder.DropTable(
                name: "Projects_ProjectHierarchyLinks");

            migrationBuilder.DropTable(
                name: "Projects_ProjectOptionSelections");

            migrationBuilder.DropTable(
                name: "Projects_ProjectPhases");

            migrationBuilder.DropTable(
                name: "Projects_Projects");

            migrationBuilder.DropTable(
                name: "Prompts_PromptArtifacts");

            migrationBuilder.DropTable(
                name: "Prompts_PromptArtifactTags");

            migrationBuilder.DropTable(
                name: "Prompts_PromptCollections");

            migrationBuilder.DropTable(
                name: "Prompts_PromptTags");

            migrationBuilder.DropTable(
                name: "Prompts_PromptUsageRecords");

            migrationBuilder.DropTable(
                name: "Prompts_PromptVersions");

            migrationBuilder.DropTable(
                name: "Resources_ProjectResources");

            migrationBuilder.DropTable(
                name: "Security_SecretRecords");

            migrationBuilder.DropTable(
                name: "Security_SecretReferences");

            migrationBuilder.DropTable(
                name: "SchedulerPlanner_Runs");

            migrationBuilder.DropTable(
                name: "Storage_Catalog");

            migrationBuilder.DropTable(
                name: "Storage_RoutingRules");

            migrationBuilder.DropTable(
                name: "TestLab_TestCases");

            migrationBuilder.DropTable(
                name: "TestLab_TestEvidence");

            migrationBuilder.DropTable(
                name: "TestLab_TestPlans");

            migrationBuilder.DropTable(
                name: "TestLab_TestRuns");

            migrationBuilder.DropTable(
                name: "Validation_Findings");

            migrationBuilder.DropTable(
                name: "Validation_Checklists");

            migrationBuilder.DropTable(
                name: "Validation_Runs");

            migrationBuilder.DropTable(
                name: "Workbench_ProjectCrossModuleMutations");

            migrationBuilder.DropTable(
                name: "Workbench_ProjectNodeBindings");

            migrationBuilder.DropTable(
                name: "Workbench_ProjectNodeLifecycleEvents");

            migrationBuilder.DropTable(
                name: "Workbench_ProjectNodeReferences");

            migrationBuilder.DropTable(
                name: "Workbench_ProjectObjectLinks");

            migrationBuilder.DropTable(
                name: "Workbench_ProjectProjectionLayouts");

            migrationBuilder.DropTable(
                name: "Workbench_ProjectStructureLeases");

            migrationBuilder.DropTable(
                name: "Workbench_ProjectStructureOperationAnalytics");

            migrationBuilder.DropTable(
                name: "Workbench_ViewStates");

            migrationBuilder.DropTable(
                name: "Workspace_ConnectorCommandAudits");

            migrationBuilder.DropTable(
                name: "Workspace_ProviderProfiles");

            migrationBuilder.DropTable(
                name: "Workspace_Settings");

            migrationBuilder.DropTable(
                name: "Automation_Envelopes");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_Entities");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_EpisodeSteps");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProcedureFailureModes");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProcedureSimulations");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProcedureSteps");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_RecallContextPacks");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_Relations");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_MutationCommands");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ReplayJobs");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_Signals");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_DreamAggregateClaims");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SynthesizedStatements");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_EvidenceAnchors");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_WorkspaceFocusSlots");

            migrationBuilder.DropTable(
                name: "Processes_LaunchCandidates");

            migrationBuilder.DropTable(
                name: "Processes_ArtifactExpectations");

            migrationBuilder.DropTable(
                name: "Processes_RunAssignments");

            migrationBuilder.DropTable(
                name: "SchedulerPlanner_Plans");

            migrationBuilder.DropTable(
                name: "Workbench_ProjectObjects");

            migrationBuilder.DropTable(
                name: "Workspace_ConnectorCommands");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProcedureSkills");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_PredictionErrors");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SynthesizedRecalls");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SourceManifests");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_WorkspaceOpenQuestions");

            migrationBuilder.DropTable(
                name: "Processes_LaunchPlanRoles");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ConsolidationCandidates");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_TemporalEpisodes");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_PredictionExpectations");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_RecallTraces");

            migrationBuilder.DropTable(
                name: "Processes_LaunchPlans");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ConsolidationRuns");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SourceItems");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_AttentionDecisions");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_Runs");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_WorkspaceFrames");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ScoreEvaluations");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_Claims");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ContextFrames");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_Records");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_DreamRuns");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_DreamValidations");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_DreamAggregateCandidates");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_QualityClusters");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ReviewItems");

            migrationBuilder.DropTable(
                name: "Processes_StepDefinitions");

            migrationBuilder.DropTable(
                name: "Processes_RoleRequirements");

            migrationBuilder.DropTable(
                name: "Processes_Runs");

            migrationBuilder.DropTable(
                name: "Processes_StepRuns");

            migrationBuilder.DropTable(
                name: "Processes_StepBranchOutcomes");

            migrationBuilder.DropTable(
                name: "Processes_DefinitionVersions");

            migrationBuilder.DropTable(
                name: "Processes_Definitions");
        }
    }
}
