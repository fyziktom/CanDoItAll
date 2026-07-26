using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessRunRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "process_run_record_participants");

            migrationBuilder.DropTable(
                name: "process_run_records");
        }
    }
}
