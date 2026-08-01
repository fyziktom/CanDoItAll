using System;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class InitialPostgreSqlBaseline : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "AgentFramework_WorkflowDefinitions",
                columns: table => new
                {
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    PreferredBackend = table.Column<int>(type: "integer", nullable: false),
                    DefinitionJson = table.Column<string>(type: "TEXT", nullable: false),
                    InstructionSnapshotSchemaVersion = table.Column<int>(type: "integer", nullable: false),
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
                name: "AgentFramework_WorkflowCheckpoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Backend = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    TrustBoundary = table.Column<int>(type: "integer", nullable: false),
                    ResumeAvailability = table.Column<int>(type: "integer", nullable: false),
                    NodeId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ExternalRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    BackendCheckpointId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PayloadReference = table.Column<string>(type: "character varying(1200)", maxLength: 1200, nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    ResumeUnavailableReason = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ResumedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFramework_WorkflowCheckpoints", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentFramework_WorkflowLaunchIdempotency",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CallerKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    SelectionKind = table.Column<int>(type: "integer", nullable: false),
                    RequestedVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    OriginKind = table.Column<int>(type: "integer", nullable: false),
                    OriginScopeKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CanonicalInputHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    ClaimToken = table.Column<Guid>(type: "uuid", nullable: false),
                    ReservedRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletionJson = table.Column<string>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReplayCount = table.Column<int>(type: "integer", nullable: false),
                    LastReplayedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFramework_WorkflowLaunchIdempotency", x => x.Id);
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
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    TerminalAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OriginJson = table.Column<string>(type: "TEXT", nullable: false)
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
                name: "AgentFramework_WorkflowUsageObservations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExecutorId = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    ComponentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProducerKind = table.Column<int>(type: "integer", nullable: false),
                    InvocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    ProviderProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProviderName = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    ProviderNameKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    ProviderKind = table.Column<int>(type: "integer", nullable: true),
                    TransportKind = table.Column<int>(type: "integer", nullable: true),
                    Model = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    ModelKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    SourcePhase = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    UsageStatus = table.Column<int>(type: "integer", nullable: false),
                    PricingStatus = table.Column<int>(type: "integer", nullable: false),
                    PricingProvenance = table.Column<int>(type: "integer", nullable: false),
                    InputTokens = table.Column<int>(type: "integer", nullable: false),
                    CachedInputTokens = table.Column<int>(type: "integer", nullable: false),
                    OutputTokens = table.Column<int>(type: "integer", nullable: false),
                    ReasoningTokens = table.Column<int>(type: "integer", nullable: false),
                    TotalTokens = table.Column<int>(type: "integer", nullable: false),
                    ToolCallCount = table.Column<int>(type: "integer", nullable: false),
                    CostUsd = table.Column<decimal>(type: "numeric(28,12)", precision: 28, scale: 12, nullable: true),
                    PricingProfileHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PricingVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ProviderRequestId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ProviderResponseId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    OriginJson = table.Column<string>(type: "TEXT", nullable: false),
                    OriginKind = table.Column<int>(type: "integer", nullable: true),
                    OriginProcessRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    OriginProcessAssignmentId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFramework_WorkflowUsageObservations", x => x.Id);
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
                    ProjectedExecutionMode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ProjectedProviderName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProjectedDefaultModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProjectedCapabilityCount = table.Column<int>(type: "integer", nullable: false),
                    ProjectedRoleTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProjectedInstructions = table.Column<string>(type: "TEXT", nullable: false),
                    ProjectedTemplateKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProjectedTagsJson = table.Column<string>(type: "TEXT", nullable: false),
                    ProjectedCapabilitiesJson = table.Column<string>(type: "TEXT", nullable: false),
                    ProjectionUpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    RecognizedAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    RecognizedCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
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
                    NormalizedDisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, computedColumnSql: "regexp_replace(lower(trim(\"DisplayName\")), '[^[:alnum:]]', '', 'g')", stored: true),
                    NormalizedLegalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, computedColumnSql: "regexp_replace(lower(trim(\"LegalName\")), '[^[:alnum:]]', '', 'g')", stored: true),
                    NormalizedPreferredName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false, computedColumnSql: "regexp_replace(lower(trim(\"PreferredName\")), '[^[:alnum:]]', '', 'g')", stored: true),
                    NormalizedExternalCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false, computedColumnSql: "lower(trim(\"ExternalCode\"))", stored: true),
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
                    TagsJson = table.Column<string>(type: "TEXT", nullable: false, defaultValue: "[]"),
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
                    RateUnit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "Hour"),
                    RateCurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "USD"),
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
                name: "Memory_EventInbox",
                columns: table => new
                {
                    InboxRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderInstanceId = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    DedupeKey = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ForgetAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memory_EventInbox", x => x.InboxRecordId);
                });

            migrationBuilder.CreateTable(
                name: "Memory_EventOutbox",
                columns: table => new
                {
                    OutboxRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderInstanceId = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    DedupeKey = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    PayloadKind = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RecordJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memory_EventOutbox", x => x.OutboxRecordId);
                });

            migrationBuilder.CreateTable(
                name: "Memory_FeedbackLedger",
                columns: table => new
                {
                    FeedbackRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderInstanceId = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ForgetAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memory_FeedbackLedger", x => x.FeedbackRecordId);
                });

            migrationBuilder.CreateTable(
                name: "Memory_OperationLedger",
                columns: table => new
                {
                    RecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderInstanceId = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    CapabilityId = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ForgetAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memory_OperationLedger", x => x.RecordId);
                });

            migrationBuilder.CreateTable(
                name: "Memory_ProviderProfiles",
                columns: table => new
                {
                    InstanceId = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    DriverKind = table.Column<int>(type: "integer", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    HealthState = table.Column<int>(type: "integer", nullable: false),
                    WorkspaceScope = table.Column<int>(type: "integer", nullable: false),
                    SelectionTagsJson = table.Column<string>(type: "TEXT", nullable: false),
                    FallbackBehavior = table.Column<int>(type: "integer", nullable: false),
                    ManifestJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memory_ProviderProfiles", x => x.InstanceId);
                });

            migrationBuilder.CreateTable(
                name: "Memory_SourceRequests",
                columns: table => new
                {
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderInstanceId = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memory_SourceRequests", x => x.JobId);
                });

            migrationBuilder.CreateTable(
                name: "Memory_WorkerLeases",
                columns: table => new
                {
                    Phase = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerId = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    LeaseToken = table.Column<Guid>(type: "uuid", nullable: false),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memory_WorkerLeases", x => x.Phase);
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
                name: "process_artifact_ledger_events",
                columns: table => new
                {
                    LedgerEventId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_artifact_ledger_events", x => x.LedgerEventId);
                });

            migrationBuilder.CreateTable(
                name: "process_instance_plans",
                columns: table => new
                {
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    RootPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentPlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentStepId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PlanSchemaVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DefinitionContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_instance_plans", x => x.PlanId);
                });

            migrationBuilder.CreateTable(
                name: "process_outbox_messages",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubscriberKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AvailableAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    DeliveredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastErrorClass = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_outbox_messages", x => x.MessageId);
                });

            migrationBuilder.CreateTable(
                name: "process_projection_dead_letters",
                columns: table => new
                {
                    DeadLetterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectorName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ShardKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    GlobalSequence = table.Column<long>(type: "bigint", nullable: false),
                    ErrorClass = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    DiagnosticReference = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    RetryPolicy = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    DeadLetteredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_projection_dead_letters", x => x.DeadLetterId);
                });

            migrationBuilder.CreateTable(
                name: "process_projection_history",
                columns: table => new
                {
                    ProjectorName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ProjectionKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    GlobalSequence = table.Column<long>(type: "bigint", nullable: false),
                    RootRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EventType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Sensitivity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_projection_history", x => new { x.ProjectorName, x.ProjectionKey, x.GlobalSequence });
                });

            migrationBuilder.CreateTable(
                name: "process_projection_snapshots",
                columns: table => new
                {
                    ProjectorName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ProjectionKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_projection_snapshots", x => new { x.ProjectorName, x.ProjectionKey });
                });

            migrationBuilder.CreateTable(
                name: "process_projector_offsets",
                columns: table => new
                {
                    ProjectorName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ShardKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    GlobalSequence = table.Column<long>(type: "bigint", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_projector_offsets", x => new { x.ProjectorName, x.ShardKey });
                });

            migrationBuilder.CreateTable(
                name: "process_run_record_participants",
                columns: table => new
                {
                    ParticipantId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_run_record_participants", x => new { x.ParticipantId, x.RunId });
                });

            migrationBuilder.CreateTable(
                name: "process_run_records",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    RootRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefinitionId = table.Column<Guid>(type: "uuid", nullable: true),
                    DefinitionVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Disposition = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    LifecycleState = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Completeness = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DurationMilliseconds = table.Column<long>(type: "bigint", nullable: true),
                    TotalStepCount = table.Column<int>(type: "integer", nullable: false),
                    ExecutableStepCount = table.Column<int>(type: "integer", nullable: false),
                    CompletedStepCount = table.Column<int>(type: "integer", nullable: false),
                    FailedStepCount = table.Column<int>(type: "integer", nullable: false),
                    CancelledStepCount = table.Column<int>(type: "integer", nullable: false),
                    RepetitionCount = table.Column<int>(type: "integer", nullable: false),
                    ExecutionCount = table.Column<int>(type: "integer", nullable: false),
                    ReworkCount = table.Column<int>(type: "integer", nullable: false),
                    IncidentCount = table.Column<int>(type: "integer", nullable: false),
                    EscalationCount = table.Column<int>(type: "integer", nullable: false),
                    InputTokenCount = table.Column<long>(type: "bigint", nullable: false),
                    CachedInputTokenCount = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokenCount = table.Column<long>(type: "bigint", nullable: false),
                    ReasoningTokenCount = table.Column<long>(type: "bigint", nullable: false),
                    TotalTokenCount = table.Column<long>(type: "bigint", nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    ActualCost = table.Column<decimal>(type: "numeric(20,6)", precision: 20, scale: 6, nullable: false),
                    ToolCallCount = table.Column<int>(type: "integer", nullable: false),
                    ArtifactCount = table.Column<int>(type: "integer", nullable: false),
                    SubprocessCount = table.Column<int>(type: "integer", nullable: false),
                    FactsJson = table.Column<string>(type: "jsonb", nullable: true),
                    ParticipantIdsJson = table.Column<string>(type: "jsonb", nullable: false),
                    AvailableEvidenceSources = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    MissingEvidenceSources = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CompletenessWarningsJson = table.Column<string>(type: "jsonb", nullable: false),
                    FactsStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    FactsLeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                    FactsLeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FactsAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    FactsNextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FactsLastErrorClass = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FactsLastErrorDiagnosticReference = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    NarrativeJson = table.Column<string>(type: "jsonb", nullable: true),
                    NarrativeStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NarrativeLeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                    NarrativeLeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NarrativeAttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NarrativeNextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NarrativeLastErrorClass = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NarrativeLastErrorDiagnosticReference = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    SourceGlobalSequence = table.Column<long>(type: "bigint", nullable: false),
                    SourceRootSequence = table.Column<long>(type: "bigint", nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_run_records", x => x.RunId);
                });

            migrationBuilder.CreateTable(
                name: "process_runtime_events",
                columns: table => new
                {
                    GlobalSequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RootSequence = table.Column<long>(type: "bigint", nullable: false),
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    RootRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    CausationId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    SchemaVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Sensitivity = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EventType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_runtime_events", x => x.GlobalSequence);
                });

            migrationBuilder.CreateTable(
                name: "process_runtime_idempotency_keys",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    CommandId = table.Column<Guid>(type: "uuid", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_runtime_idempotency_keys", x => new { x.RunId, x.CommandId });
                });

            migrationBuilder.CreateTable(
                name: "process_runtime_states",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    RootRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false),
                    BlockedRecoveryActionsJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_runtime_states", x => x.RunId);
                });

            migrationBuilder.CreateTable(
                name: "process_runtime_step_assignments",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RoleKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RoleResourceKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    RoleDisplayName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    ExecutorKind = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExecutorId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ExecutorDisplayName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkflowVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    WorkflowOutputMapping = table.Column<int>(type: "integer", nullable: true),
                    Prompt = table.Column<string>(type: "text", nullable: false),
                    ReadinessHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AssignmentReason = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    ProducedArtifactSlotIds = table.Column<string>(type: "text", nullable: false),
                    RequiredArtifactSlotIds = table.Column<string>(type: "text", nullable: false),
                    AllowedOperations = table.Column<string>(type: "text", nullable: false),
                    OperationTargetScope = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LaunchVariablesJson = table.Column<string>(type: "text", nullable: false),
                    CapabilityScopeJson = table.Column<string>(type: "text", nullable: false),
                    BranchGateSourceStepKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    BranchGateRequiredOutcomeKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_runtime_step_assignments", x => new { x.RunId, x.StepInstanceId });
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
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Phase = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CurrentDraftText = table.Column<string>(type: "TEXT", nullable: false),
                    SearchText = table.Column<string>(type: "TEXT", nullable: false),
                    CurrentVersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Provenance = table.Column<int>(type: "integer", nullable: false),
                    SourceKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SourceCatalog = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SourceGroupKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SourceGroupName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SourceItemKind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    SourceOrderIndex = table.Column<int>(type: "integer", nullable: true),
                    SourceFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    RecommendedTemperature = table.Column<double>(type: "double precision", nullable: true),
                    RecommendedMaxOutputTokens = table.Column<int>(type: "integer", nullable: true),
                    RecommendedTopP = table.Column<double>(type: "double precision", nullable: true),
                    IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                    IsFavorite = table.Column<bool>(type: "boolean", nullable: false),
                    ArchivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prompts_PromptArtifacts", x => x.Id);
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
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NameKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prompts_PromptTags", x => x.Id);
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
                    ExtraSettingsJson = table.Column<string>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
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
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "USD"),
                    CurrencyCultureName = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "en-US"),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspace_Settings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentFramework_WorkflowDefinitionHeads",
                columns: table => new
                {
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    VersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalNamespace = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ExternalKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFramework_WorkflowDefinitionHeads", x => x.WorkflowId);
                    table.ForeignKey(
                        name: "FK_AgentFramework_WorkflowDefinitionHeads_AgentFramework_Workf~",
                        column: x => x.VersionId,
                        principalTable: "AgentFramework_WorkflowDefinitions",
                        principalColumn: "VersionId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "process_dispatch_claims",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimToken = table.Column<Guid>(type: "uuid", nullable: false),
                    StepInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RenewedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResultIdempotencyKey = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_dispatch_claims", x => new { x.RunId, x.ClaimToken });
                    table.ForeignKey(
                        name: "FK_process_dispatch_claims_process_runtime_states_RunId",
                        column: x => x.RunId,
                        principalTable: "process_runtime_states",
                        principalColumn: "RunId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "process_runtime_available_artifact_slots",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    SlotId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_runtime_available_artifact_slots", x => new { x.RunId, x.SlotId });
                    table.ForeignKey(
                        name: "FK_process_runtime_available_artifact_slots_process_runtime_st~",
                        column: x => x.RunId,
                        principalTable: "process_runtime_states",
                        principalColumn: "RunId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "process_runtime_input_artifacts",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumerStepInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequiredSlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectionHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Availability = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProducerStepInstanceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: true),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_runtime_input_artifacts", x => new { x.RunId, x.ConsumerStepInstanceId, x.RequiredSlotId, x.ConnectionHash });
                    table.ForeignKey(
                        name: "FK_process_runtime_input_artifacts_process_runtime_states_RunId",
                        column: x => x.RunId,
                        principalTable: "process_runtime_states",
                        principalColumn: "RunId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "process_runtime_steps",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepDefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsExecutable = table.Column<bool>(type: "boolean", nullable: false),
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    DependencyStepIds = table.Column<string>(type: "text", nullable: false),
                    RequiredArtifactSlotIds = table.Column<string>(type: "text", nullable: false),
                    ProducedArtifactSlotIds = table.Column<string>(type: "text", nullable: false),
                    RequiredRuntimeToolNamesJson = table.Column<string>(type: "text", nullable: false),
                    ArtifactDescriptorsJson = table.Column<string>(type: "text", nullable: false),
                    SubprocessArtifactMappingsJson = table.Column<string>(type: "text", nullable: false),
                    ActiveClaimToken = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedResultKey = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_runtime_steps", x => new { x.RunId, x.StepInstanceId });
                    table.ForeignKey(
                        name: "FK_process_runtime_steps_process_runtime_states_RunId",
                        column: x => x.RunId,
                        principalTable: "process_runtime_states",
                        principalColumn: "RunId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "process_strategy_result_receipts",
                columns: table => new
                {
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    StrategyId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    IdempotencyKey = table.Column<Guid>(type: "uuid", nullable: false),
                    Outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AppliedStepStatus = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResultHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    UserSafeSummary = table.Column<string>(type: "text", nullable: true),
                    AppliedSequence = table.Column<long>(type: "bigint", nullable: false),
                    DiagnosticsJson = table.Column<string>(type: "text", nullable: false),
                    ProducedArtifactsJson = table.Column<string>(type: "text", nullable: false),
                    RecoveryDecisionJson = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_strategy_result_receipts", x => new { x.RunId, x.StepInstanceId, x.StrategyId, x.IdempotencyKey });
                    table.ForeignKey(
                        name: "FK_process_strategy_result_receipts_process_runtime_states_Run~",
                        column: x => x.RunId,
                        principalTable: "process_runtime_states",
                        principalColumn: "RunId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_AccountConnectionProjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_AccountConnectionProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrmHr_AccountConnectionProjects_CrmHr_AccountStakeholders_A~",
                        column: x => x.AccountConnectionId,
                        principalTable: "CrmHr_AccountStakeholders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CrmHr_AccountConnectionProjects_Projects_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects_Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Prompts_PromptCompatibilityWarningPreferences",
                columns: table => new
                {
                    PromptArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Consumer = table.Column<int>(type: "integer", nullable: false),
                    IssueCode = table.Column<int>(type: "integer", nullable: false),
                    IsSuppressed = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prompts_PromptCompatibilityWarningPreferences", x => new { x.PromptArtifactId, x.Consumer, x.IssueCode });
                    table.ForeignKey(
                        name: "FK_Prompts_PromptCompatibilityWarningPreferences_Prompts_Promp~",
                        column: x => x.PromptArtifactId,
                        principalTable: "Prompts_PromptArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Prompts_PromptSupportedConsumers",
                columns: table => new
                {
                    PromptArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    Consumer = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prompts_PromptSupportedConsumers", x => new { x.PromptArtifactId, x.Consumer });
                    table.ForeignKey(
                        name: "FK_Prompts_PromptSupportedConsumers_Prompts_PromptArtifacts_Pr~",
                        column: x => x.PromptArtifactId,
                        principalTable: "Prompts_PromptArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Prompts_PromptSupportedProviderModels",
                columns: table => new
                {
                    PromptArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderKey = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ModelKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Provider = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Model = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsPreferred = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prompts_PromptSupportedProviderModels", x => new { x.PromptArtifactId, x.ProviderKey, x.ModelKey });
                    table.ForeignKey(
                        name: "FK_Prompts_PromptSupportedProviderModels_Prompts_PromptArtifac~",
                        column: x => x.PromptArtifactId,
                        principalTable: "Prompts_PromptArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Prompts_PromptTemplateTokens",
                columns: table => new
                {
                    PromptArtifactId = table.Column<Guid>(type: "uuid", nullable: false),
                    NameKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prompts_PromptTemplateTokens", x => new { x.PromptArtifactId, x.NameKey });
                    table.ForeignKey(
                        name: "FK_Prompts_PromptTemplateTokens_Prompts_PromptArtifacts_Prompt~",
                        column: x => x.PromptArtifactId,
                        principalTable: "Prompts_PromptArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
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
                    table.ForeignKey(
                        name: "FK_Prompts_PromptUsageRecords_Prompts_PromptArtifacts_PromptAr~",
                        column: x => x.PromptArtifactId,
                        principalTable: "Prompts_PromptArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    OutputFormat = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceBlueprintId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    TitleSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    SummarySnapshot = table.Column<string>(type: "TEXT", nullable: false),
                    KindSnapshot = table.Column<int>(type: "integer", nullable: false),
                    RecommendedTemperatureSnapshot = table.Column<double>(type: "double precision", nullable: true),
                    RecommendedMaxOutputTokensSnapshot = table.Column<int>(type: "integer", nullable: true),
                    RecommendedTopPSnapshot = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prompts_PromptVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Prompts_PromptVersions_Prompts_PromptArtifacts_PromptArtifa~",
                        column: x => x.PromptArtifactId,
                        principalTable: "Prompts_PromptArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    table.ForeignKey(
                        name: "FK_Prompts_PromptArtifactTags_Prompts_PromptArtifacts_PromptAr~",
                        column: x => x.PromptArtifactId,
                        principalTable: "Prompts_PromptArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Prompts_PromptArtifactTags_Prompts_PromptTags_PromptTagId",
                        column: x => x.PromptTagId,
                        principalTable: "Prompts_PromptTags",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                    Route = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    RetryCategory = table.Column<int>(type: "integer", nullable: false),
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
                name: "AgentFramework_WorkflowComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    ProviderProfileId = table.Column<Guid>(type: "uuid", nullable: true),
                    Model = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Modality = table.Column<int>(type: "integer", nullable: false),
                    PromptArtifactId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromptVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                    PromptGalleryBindingSchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    ComponentJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFramework_WorkflowComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AgentFramework_WorkflowComponents_Prompts_PromptArtifacts_P~",
                        column: x => x.PromptArtifactId,
                        principalTable: "Prompts_PromptArtifacts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgentFramework_WorkflowComponents_Prompts_PromptVersions_Pr~",
                        column: x => x.PromptVersionId,
                        principalTable: "Prompts_PromptVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowArtifacts_RunId_CreatedAtUtc",
                table: "AgentFramework_WorkflowArtifacts",
                columns: new[] { "RunId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowComponents_Name",
                table: "AgentFramework_WorkflowComponents",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowComponents_PromptVersionId",
                table: "AgentFramework_WorkflowComponents",
                column: "PromptVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowComponents_ProviderProfileId",
                table: "AgentFramework_WorkflowComponents",
                column: "ProviderProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowComponents_PromptBinding",
                table: "AgentFramework_WorkflowComponents",
                columns: new[] { "PromptArtifactId", "PromptVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowComponents_PromptGalleryBindingSchema_Id",
                table: "AgentFramework_WorkflowComponents",
                columns: new[] { "PromptGalleryBindingSchemaVersion", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowDefinitionHeads_VersionId",
                table: "AgentFramework_WorkflowDefinitionHeads",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitionHeads_ExternalIdentity",
                table: "AgentFramework_WorkflowDefinitionHeads",
                columns: new[] { "ExternalNamespace", "ExternalKey" },
                unique: true,
                filter: "\"ExternalNamespace\" <> ''");

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowDefinitions_WorkflowId",
                table: "AgentFramework_WorkflowDefinitions",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowDefinitions_WorkflowId_UpdatedAtUtc",
                table: "AgentFramework_WorkflowDefinitions",
                columns: new[] { "WorkflowId", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_InstructionSnapshotSchema_Id",
                table: "AgentFramework_WorkflowDefinitions",
                columns: new[] { "InstructionSnapshotSchemaVersion", "VersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkflowDefinitions_WorkflowId_Revision",
                table: "AgentFramework_WorkflowDefinitions",
                columns: new[] { "WorkflowId", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowEvents_RunId_CreatedAtUtc",
                table: "AgentFramework_WorkflowEvents",
                columns: new[] { "RunId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowExternalRequests_RunId_RespondedAtUtc",
                table: "AgentFramework_WorkflowExternalRequests",
                columns: new[] { "RunId", "RespondedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowCheckpoints_ExternalRequestId",
                table: "AgentFramework_WorkflowCheckpoints",
                column: "ExternalRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowCheckpoints_RunId_CreatedAtUtc",
                table: "AgentFramework_WorkflowCheckpoints",
                columns: new[] { "RunId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowCheckpoints_RunId_Kind",
                table: "AgentFramework_WorkflowCheckpoints",
                columns: new[] { "RunId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_AF_WorkflowLaunchIdempotency_Lease",
                table: "AgentFramework_WorkflowLaunchIdempotency",
                columns: new[] { "State", "LeaseExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_AF_WorkflowLaunchIdempotency_ApiKey",
                table: "AgentFramework_WorkflowLaunchIdempotency",
                column: "CallerKey",
                unique: true,
                filter: "\"OriginKind\" = 0");

            migrationBuilder.CreateIndex(
                name: "UX_AF_WorkflowLaunchIdempotency_Run",
                table: "AgentFramework_WorkflowLaunchIdempotency",
                column: "ReservedRunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_AF_WorkflowLaunchIdempotency_Scope",
                table: "AgentFramework_WorkflowLaunchIdempotency",
                columns: new[] { "CallerKey", "WorkflowId", "SelectionKind", "RequestedVersionId", "Mode", "OriginKind", "OriginScopeKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowRuns_State_UpdatedAtUtc_RunId",
                table: "AgentFramework_WorkflowRuns",
                columns: new[] { "State", "UpdatedAtUtc", "RunId" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowRuns_UpdatedAtUtc",
                table: "AgentFramework_WorkflowRuns",
                column: "UpdatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowRuns_WorkflowId",
                table: "AgentFramework_WorkflowRuns",
                column: "WorkflowId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowUsageObservations_NodeId_ExecutorId",
                table: "AgentFramework_WorkflowUsageObservations",
                columns: new[] { "NodeId", "ExecutorId" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowUsageObservations_OriginProcessRunId~",
                table: "AgentFramework_WorkflowUsageObservations",
                columns: new[] { "OriginProcessRunId", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowUsageObservations_ProviderNameKey_Mo~",
                table: "AgentFramework_WorkflowUsageObservations",
                columns: new[] { "ProviderNameKey", "ModelKey" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowUsageObservations_RunId_RecordedAtUtc",
                table: "AgentFramework_WorkflowUsageObservations",
                columns: new[] { "RunId", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentFramework_WorkflowUsageObservations_WorkflowId_Recorde~",
                table: "AgentFramework_WorkflowUsageObservations",
                columns: new[] { "WorkflowId", "RecordedAtUtc" });

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
                name: "IX_CrmHr_AccountConnectionProjects_AccountConnectionId_Project~",
                table: "CrmHr_AccountConnectionProjects",
                columns: new[] { "AccountConnectionId", "ProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_AccountConnectionProjects_ProjectId_AccountConnection~",
                table: "CrmHr_AccountConnectionProjects",
                columns: new[] { "ProjectId", "AccountConnectionId" });

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
                name: "IX_CrmHr_AuditEntries_EntityType_EntityId_CreatedAtUtc_Id",
                table: "CrmHr_AuditEntries",
                columns: new[] { "EntityType", "EntityId", "CreatedAtUtc", "Id" },
                descending: new[] { false, false, true, false });

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
                name: "IX_CrmHr_InteractionParties_PartyId_Role_InteractionId",
                table: "CrmHr_InteractionParties",
                columns: new[] { "PartyId", "Role", "InteractionId" });

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
                name: "IX_CrmHr_Opportunities_AccountPartyId_Stage",
                table: "CrmHr_Opportunities",
                columns: new[] { "AccountPartyId", "Stage" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Opportunities_AccountPartyId_UpdatedAtUtc_Id",
                table: "CrmHr_Opportunities",
                columns: new[] { "AccountPartyId", "UpdatedAtUtc", "Id" },
                descending: new[] { false, true, false });

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
                name: "IX_CrmHr_Parties_DisplayName_Id",
                table: "CrmHr_Parties",
                columns: new[] { "DisplayName", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Parties_ExternalCode",
                table: "CrmHr_Parties",
                column: "ExternalCode");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Parties_NormalizedDisplayName",
                table: "CrmHr_Parties",
                column: "NormalizedDisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Parties_NormalizedExternalCode",
                table: "CrmHr_Parties",
                column: "NormalizedExternalCode");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Parties_NormalizedLegalName",
                table: "CrmHr_Parties",
                column: "NormalizedLegalName");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Parties_NormalizedPreferredName",
                table: "CrmHr_Parties",
                column: "NormalizedPreferredName");

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
                name: "IX_CrmHr_ProjectPartyAssignments_ProjectId_AssignmentKind_Node~",
                table: "CrmHr_ProjectPartyAssignments",
                columns: new[] { "ProjectId", "AssignmentKind", "NodeKey" });

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
                name: "IX_Memory_EventInbox_DedupeKey",
                table: "Memory_EventInbox",
                column: "DedupeKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Memory_EventInbox_ProviderInstanceId_Status_UpdatedAtUtc",
                table: "Memory_EventInbox",
                columns: new[] { "ProviderInstanceId", "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Memory_EventInbox_Status_ExpiresAtUtc_ForgetAtUtc",
                table: "Memory_EventInbox",
                columns: new[] { "Status", "ExpiresAtUtc", "ForgetAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Memory_EventOutbox_ProviderInstanceId_Status_UpdatedAtUtc",
                table: "Memory_EventOutbox",
                columns: new[] { "ProviderInstanceId", "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Memory_FeedbackLedger_ProviderInstanceId_Status_UpdatedAtUtc",
                table: "Memory_FeedbackLedger",
                columns: new[] { "ProviderInstanceId", "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Memory_FeedbackLedger_Status_ExpiresAtUtc_ForgetAtUtc",
                table: "Memory_FeedbackLedger",
                columns: new[] { "Status", "ExpiresAtUtc", "ForgetAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Memory_OperationLedger_OperationId",
                table: "Memory_OperationLedger",
                column: "OperationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Memory_OperationLedger_ProviderInstanceId_Status_UpdatedAtU~",
                table: "Memory_OperationLedger",
                columns: new[] { "ProviderInstanceId", "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Memory_OperationLedger_Status_ExpiresAtUtc_ForgetAtUtc",
                table: "Memory_OperationLedger",
                columns: new[] { "Status", "ExpiresAtUtc", "ForgetAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Memory_ProviderProfiles_DriverKind_IsEnabled",
                table: "Memory_ProviderProfiles",
                columns: new[] { "DriverKind", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_Memory_SourceRequests_ProviderInstanceId_Status_UpdatedAtUtc",
                table: "Memory_SourceRequests",
                columns: new[] { "ProviderInstanceId", "Status", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Memory_WorkerLeases_LeaseExpiresAtUtc",
                table: "Memory_WorkerLeases",
                column: "LeaseExpiresAtUtc");

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
                name: "IX_process_artifact_ledger_events_EventId",
                table: "process_artifact_ledger_events",
                column: "EventId");

            migrationBuilder.CreateIndex(
                name: "IX_process_artifact_ledger_events_SlotId_LedgerEventId",
                table: "process_artifact_ledger_events",
                columns: new[] { "SlotId", "LedgerEventId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_process_dispatch_claims_RunId_Status_ExpiresAtUtc",
                table: "process_dispatch_claims",
                columns: new[] { "RunId", "Status", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_process_dispatch_claims_StepInstanceId_ClaimToken",
                table: "process_dispatch_claims",
                columns: new[] { "StepInstanceId", "ClaimToken" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_process_instance_plans_CreatedAtUtc",
                table: "process_instance_plans",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_process_instance_plans_DefinitionId_DefinitionVersionId",
                table: "process_instance_plans",
                columns: new[] { "DefinitionId", "DefinitionVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_process_instance_plans_RootPlanId",
                table: "process_instance_plans",
                column: "RootPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_process_outbox_messages_EventId_SubscriberKind",
                table: "process_outbox_messages",
                columns: new[] { "EventId", "SubscriberKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_process_outbox_messages_Status_AvailableAtUtc_LockedAtUtc",
                table: "process_outbox_messages",
                columns: new[] { "Status", "AvailableAtUtc", "LockedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_process_projection_dead_letters_ProjectorName_ShardKey_Glob~",
                table: "process_projection_dead_letters",
                columns: new[] { "ProjectorName", "ShardKey", "GlobalSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_process_projection_history_ProjectorName_RootRunId_Occurred~",
                table: "process_projection_history",
                columns: new[] { "ProjectorName", "RootRunId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_process_projection_history_ProjectorName_RunId_GlobalSequen~",
                table: "process_projection_history",
                columns: new[] { "ProjectorName", "RunId", "GlobalSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_process_projection_snapshots_UpdatedAtUtc",
                table: "process_projection_snapshots",
                column: "UpdatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_process_projector_offsets_GlobalSequence",
                table: "process_projector_offsets",
                column: "GlobalSequence");

            migrationBuilder.CreateIndex(
                name: "IX_process_run_record_participants_RunId",
                table: "process_run_record_participants",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_process_run_records_DefinitionId",
                table: "process_run_records",
                column: "DefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_process_run_records_EndedAtUtc_RunId",
                table: "process_run_records",
                columns: new[] { "EndedAtUtc", "RunId" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_process_run_records_LifecycleState_DefinitionId_EndedAtUtc_~",
                table: "process_run_records",
                columns: new[] { "LifecycleState", "DefinitionId", "EndedAtUtc", "RunId" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_process_run_records_LifecycleState_Disposition_EndedAtUtc_R~",
                table: "process_run_records",
                columns: new[] { "LifecycleState", "Disposition", "EndedAtUtc", "RunId" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_process_run_records_LifecycleState_EndedAtUtc_RunId",
                table: "process_run_records",
                columns: new[] { "LifecycleState", "EndedAtUtc", "RunId" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_process_run_records_LifecycleState_FactsStatus_FactsNextAtt~",
                table: "process_run_records",
                columns: new[] { "LifecycleState", "FactsStatus", "FactsNextAttemptAtUtc", "FactsLeaseExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_process_run_records_LifecycleState_NarrativeStatus_Narrativ~",
                table: "process_run_records",
                columns: new[] { "LifecycleState", "NarrativeStatus", "NarrativeNextAttemptAtUtc", "NarrativeLeaseExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_process_run_records_LifecycleState_ParentRunId_EndedAtUtc_R~",
                table: "process_run_records",
                columns: new[] { "LifecycleState", "ParentRunId", "EndedAtUtc", "RunId" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_process_run_records_LifecycleState_ProjectId_EndedAtUtc_Run~",
                table: "process_run_records",
                columns: new[] { "LifecycleState", "ProjectId", "EndedAtUtc", "RunId" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_process_run_records_LifecycleState_RootRunId_EndedAtUtc_Run~",
                table: "process_run_records",
                columns: new[] { "LifecycleState", "RootRunId", "EndedAtUtc", "RunId" },
                descending: new[] { false, false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_process_run_records_ProjectId",
                table: "process_run_records",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_process_run_records_RootRunId",
                table: "process_run_records",
                column: "RootRunId");

            migrationBuilder.CreateIndex(
                name: "IX_process_runtime_events_EventId",
                table: "process_runtime_events",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_process_runtime_events_RootRunId_RootSequence",
                table: "process_runtime_events",
                columns: new[] { "RootRunId", "RootSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_process_runtime_events_RunId_OccurredAtUtc",
                table: "process_runtime_events",
                columns: new[] { "RunId", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_process_runtime_input_artifacts_RunId_ConsumerStepInstanceI~",
                table: "process_runtime_input_artifacts",
                columns: new[] { "RunId", "ConsumerStepInstanceId", "Availability" });

            migrationBuilder.CreateIndex(
                name: "IX_process_runtime_input_artifacts_RunId_ProducerStepInstanceId",
                table: "process_runtime_input_artifacts",
                columns: new[] { "RunId", "ProducerStepInstanceId" });

            migrationBuilder.CreateIndex(
                name: "IX_process_runtime_input_artifacts_RunId_RequiredSlotId",
                table: "process_runtime_input_artifacts",
                columns: new[] { "RunId", "RequiredSlotId" });

            migrationBuilder.CreateIndex(
                name: "IX_process_runtime_states_RootRunId",
                table: "process_runtime_states",
                column: "RootRunId");

            migrationBuilder.CreateIndex(
                name: "IX_process_runtime_states_Status",
                table: "process_runtime_states",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_process_runtime_states_UpdatedAtUtc_RunId",
                table: "process_runtime_states",
                columns: new[] { "UpdatedAtUtc", "RunId" },
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_process_runtime_step_assignments_ExecutorKind_ExecutorId",
                table: "process_runtime_step_assignments",
                columns: new[] { "ExecutorKind", "ExecutorId" });

            migrationBuilder.CreateIndex(
                name: "IX_process_runtime_step_assignments_PlanId",
                table: "process_runtime_step_assignments",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_process_runtime_step_assignments_RunId_StepKey",
                table: "process_runtime_step_assignments",
                columns: new[] { "RunId", "StepKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_process_runtime_step_assignments_WorkflowId_WorkflowVersion~",
                table: "process_runtime_step_assignments",
                columns: new[] { "WorkflowId", "WorkflowVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_process_runtime_steps_RunId_ActiveClaimToken",
                table: "process_runtime_steps",
                columns: new[] { "RunId", "ActiveClaimToken" });

            migrationBuilder.CreateIndex(
                name: "IX_process_runtime_steps_RunId_Status",
                table: "process_runtime_steps",
                columns: new[] { "RunId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_process_strategy_result_receipts_RunId_AppliedSequence",
                table: "process_strategy_result_receipts",
                columns: new[] { "RunId", "AppliedSequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_process_strategy_result_receipts_StepInstanceId_StrategyId_~",
                table: "process_strategy_result_receipts",
                columns: new[] { "StepInstanceId", "StrategyId", "IdempotencyKey" },
                unique: true);

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
                name: "IX_Projects_Projects_Name_Id",
                table: "Projects_Projects",
                columns: new[] { "Name", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_Projects_UpdatedAtUtc_Id",
                table: "Projects_Projects",
                columns: new[] { "UpdatedAtUtc", "Id" },
                descending: new[] { true, false });

            migrationBuilder.CreateIndex(
                name: "IX_Prompts_PromptArtifacts_IsArchived_IsFavorite_UpdatedAtUtc_~",
                table: "Prompts_PromptArtifacts",
                columns: new[] { "IsArchived", "IsFavorite", "UpdatedAtUtc", "Title", "Id" },
                descending: new[] { false, true, true, false, false });

            migrationBuilder.CreateIndex(
                name: "IX_Prompts_PromptArtifacts_IsArchived_Status_Kind_UpdatedAtUtc",
                table: "Prompts_PromptArtifacts",
                columns: new[] { "IsArchived", "Status", "Kind", "UpdatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Prompts_PromptArtifacts_Provenance_SourceKey",
                table: "Prompts_PromptArtifacts",
                columns: new[] { "Provenance", "SourceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prompts_PromptArtifactTags_PromptTagId",
                table: "Prompts_PromptArtifactTags",
                column: "PromptTagId");

            migrationBuilder.CreateIndex(
                name: "IX_Prompts_PromptSupportedProviderModels_PromptArtifactId",
                table: "Prompts_PromptSupportedProviderModels",
                column: "PromptArtifactId",
                unique: true,
                filter: "\"IsPreferred\"");

            migrationBuilder.CreateIndex(
                name: "IX_Prompts_PromptSupportedProviderModels_ProviderKey_ModelKey_~",
                table: "Prompts_PromptSupportedProviderModels",
                columns: new[] { "ProviderKey", "ModelKey", "PromptArtifactId" });

            migrationBuilder.CreateIndex(
                name: "IX_Prompts_PromptTags_Name",
                table: "Prompts_PromptTags",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Prompts_PromptTags_NameKey",
                table: "Prompts_PromptTags",
                column: "NameKey");

            migrationBuilder.CreateIndex(
                name: "IX_Prompts_PromptUsageRecords_PromptArtifactId",
                table: "Prompts_PromptUsageRecords",
                column: "PromptArtifactId");

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
                name: "IX_Workbench_ProjectObjectLinks_ProjectId_LinkKind_IsSystemMan~",
                table: "Workbench_ProjectObjectLinks",
                columns: new[] { "ProjectId", "LinkKind", "IsSystemManaged" });

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
                name: "IX_Workbench_ProjectObjects_ProjectId_ObjectType_ObjectSubtype~",
                table: "Workbench_ProjectObjects",
                columns: new[] { "ProjectId", "ObjectType", "ObjectSubtype", "IsSystemManaged" });

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_ProjectObjects_ProjectId_ParentNodeKey_ObjectType~",
                table: "Workbench_ProjectObjects",
                columns: new[] { "ProjectId", "ParentNodeKey", "ObjectType", "IsSystemManaged" });

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

            migrationBuilder.Sql(PostgreSqlMigrationBaseline.CreateCustomObjectsSql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(PostgreSqlMigrationBaseline.DropCustomIndexesSql);

            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowArtifacts");

            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowComponents");

            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowDefinitionHeads");

            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowEvents");

            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowExternalRequests");

            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowCheckpoints");

            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowLaunchIdempotency");

            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowRuns");

            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowSettings");

            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowUsageObservations");

            migrationBuilder.DropTable(
                name: "Collaboration_InboxItems");

            migrationBuilder.DropTable(
                name: "Collaboration_Messages");

            migrationBuilder.DropTable(
                name: "Collaboration_Participants");

            migrationBuilder.DropTable(
                name: "Collaboration_Threads");

            migrationBuilder.DropTable(
                name: "CrmHr_AccountConnectionProjects");

            migrationBuilder.DropTable(
                name: "CrmHr_AccountProfiles");

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
                name: "Infrastructure_BackgroundJobRecords");

            migrationBuilder.DropTable(
                name: "Infrastructure_SearchDocuments");

            migrationBuilder.DropTable(
                name: "Memory_EventInbox");

            migrationBuilder.DropTable(
                name: "Memory_EventOutbox");

            migrationBuilder.DropTable(
                name: "Memory_FeedbackLedger");

            migrationBuilder.DropTable(
                name: "Memory_OperationLedger");

            migrationBuilder.DropTable(
                name: "Memory_ProviderProfiles");

            migrationBuilder.DropTable(
                name: "Memory_SourceRequests");

            migrationBuilder.DropTable(
                name: "Memory_WorkerLeases");

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
                name: "process_artifact_ledger_events");

            migrationBuilder.DropTable(
                name: "process_dispatch_claims");

            migrationBuilder.DropTable(
                name: "process_instance_plans");

            migrationBuilder.DropTable(
                name: "process_outbox_messages");

            migrationBuilder.DropTable(
                name: "process_projection_dead_letters");

            migrationBuilder.DropTable(
                name: "process_projection_history");

            migrationBuilder.DropTable(
                name: "process_projection_snapshots");

            migrationBuilder.DropTable(
                name: "process_projector_offsets");

            migrationBuilder.DropTable(
                name: "process_run_record_participants");

            migrationBuilder.DropTable(
                name: "process_run_records");

            migrationBuilder.DropTable(
                name: "process_runtime_available_artifact_slots");

            migrationBuilder.DropTable(
                name: "process_runtime_events");

            migrationBuilder.DropTable(
                name: "process_runtime_idempotency_keys");

            migrationBuilder.DropTable(
                name: "process_runtime_input_artifacts");

            migrationBuilder.DropTable(
                name: "process_runtime_step_assignments");

            migrationBuilder.DropTable(
                name: "process_runtime_steps");

            migrationBuilder.DropTable(
                name: "process_strategy_result_receipts");

            migrationBuilder.DropTable(
                name: "Projects_ProjectHierarchyLinks");

            migrationBuilder.DropTable(
                name: "Projects_ProjectOptionSelections");

            migrationBuilder.DropTable(
                name: "Projects_ProjectPhases");

            migrationBuilder.DropTable(
                name: "Prompts_PromptArtifactTags");

            migrationBuilder.DropTable(
                name: "Prompts_PromptCollections");

            migrationBuilder.DropTable(
                name: "Prompts_PromptCompatibilityWarningPreferences");

            migrationBuilder.DropTable(
                name: "Prompts_PromptSupportedConsumers");

            migrationBuilder.DropTable(
                name: "Prompts_PromptSupportedProviderModels");

            migrationBuilder.DropTable(
                name: "Prompts_PromptTemplateTokens");

            migrationBuilder.DropTable(
                name: "Prompts_PromptUsageRecords");

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
                name: "Prompts_PromptVersions");

            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowDefinitions");

            migrationBuilder.DropTable(
                name: "CrmHr_AccountStakeholders");

            migrationBuilder.DropTable(
                name: "Projects_Projects");

            migrationBuilder.DropTable(
                name: "process_runtime_states");

            migrationBuilder.DropTable(
                name: "Prompts_PromptTags");

            migrationBuilder.DropTable(
                name: "SchedulerPlanner_Plans");

            migrationBuilder.DropTable(
                name: "Workbench_ProjectObjects");

            migrationBuilder.DropTable(
                name: "Workspace_ConnectorCommands");

            migrationBuilder.DropTable(
                name: "Prompts_PromptArtifacts");
        }
    }
}
