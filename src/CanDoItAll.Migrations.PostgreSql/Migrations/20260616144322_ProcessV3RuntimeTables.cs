using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class ProcessV3RuntimeTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
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
                    ExecutorKind = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExecutorId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ExecutorDisplayName = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Prompt = table.Column<string>(type: "text", nullable: false),
                    ReadinessHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AssignmentReason = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    ProducedArtifactSlotIds = table.Column<string>(type: "text", nullable: false),
                    RequiredArtifactSlotIds = table.Column<string>(type: "text", nullable: false),
                    BranchGateSourceStepKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    BranchGateRequiredOutcomeKey = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_process_runtime_step_assignments", x => new { x.RunId, x.StepInstanceId });
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
                    ResultHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
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
                name: "IX_process_runtime_states_RootRunId",
                table: "process_runtime_states",
                column: "RootRunId");

            migrationBuilder.CreateIndex(
                name: "IX_process_runtime_states_Status",
                table: "process_runtime_states",
                column: "Status");

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
                name: "IX_process_runtime_steps_RunId_ActiveClaimToken",
                table: "process_runtime_steps",
                columns: new[] { "RunId", "ActiveClaimToken" });

            migrationBuilder.CreateIndex(
                name: "IX_process_runtime_steps_RunId_Status",
                table: "process_runtime_steps",
                columns: new[] { "RunId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_process_strategy_result_receipts_StepInstanceId_StrategyId_~",
                table: "process_strategy_result_receipts",
                columns: new[] { "StepInstanceId", "StrategyId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                name: "process_runtime_available_artifact_slots");

            migrationBuilder.DropTable(
                name: "process_runtime_events");

            migrationBuilder.DropTable(
                name: "process_runtime_idempotency_keys");

            migrationBuilder.DropTable(
                name: "process_runtime_step_assignments");

            migrationBuilder.DropTable(
                name: "process_runtime_steps");

            migrationBuilder.DropTable(
                name: "process_strategy_result_receipts");

            migrationBuilder.DropTable(
                name: "process_runtime_states");
        }
    }
}
