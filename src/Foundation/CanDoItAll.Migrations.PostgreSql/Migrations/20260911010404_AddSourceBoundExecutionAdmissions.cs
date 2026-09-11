using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddSourceBoundExecutionAdmissions : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<string>(
                name: "StructureAuthorityJson",
                table: "SchedulerPlanner_Plans",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LaunchAdmissionId",
                table: "process_runtime_states",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectAdmissionDatabaseProfileId",
                table: "process_runtime_states",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectAdmissionLifetimeId",
                table: "process_runtime_states",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectAdmissionProjectId",
                table: "process_runtime_states",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "process_prepared_launches",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CallerIntentId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdmissionSequence = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(71)", maxLength: 71, nullable: false),
                    PreparationFingerprint = table.Column<string>(type: "character varying(71)", maxLength: 71, nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    PreparedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Execute = table.Column<bool>(type: "boolean", nullable: true),
                    State = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ContinuationOwner = table.Column<Guid>(type: "uuid", nullable: true),
                    ContinuationGeneration = table.Column<long>(type: "bigint", nullable: false),
                    ContinuationLeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LinkDeliveryState = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DeliveredLinkId = table.Column<Guid>(type: "uuid", nullable: true),
                    LinkConflictReason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    PublicFailure = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table => {
                    table.PrimaryKey("PK_process_prepared_launches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects_ProjectCreationReservations",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DatabaseProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    LifetimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequesterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ParentLifetimeId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConsumedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CancelledAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table => {
                    table.PrimaryKey("PK_Projects_ProjectCreationReservations", x => x.Id);
                    table.CheckConstraint("CK_Projects_CreationReservation_Parent", "(\"ParentProjectId\" IS NULL AND \"ParentLifetimeId\" IS NULL) OR (\"ParentProjectId\" IS NOT NULL AND \"ParentLifetimeId\" IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "SchedulerPlanner_FireAdmissions",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    DedupeKey = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    SnapshotJson = table.Column<string>(type: "TEXT", nullable: false),
                    SnapshotFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PreparedWorkflowRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcceptedWorkflowRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Generation = table.Column<long>(type: "bigint", nullable: false),
                    LeaseOwner = table.Column<Guid>(type: "uuid", nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextObservationAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OutcomeJson = table.Column<string>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_SchedulerPlanner_FireAdmissions", x => x.Id);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_process_runtime_states_project_admission",
                table: "process_runtime_states",
                sql: "(\"ProjectAdmissionDatabaseProfileId\" IS NULL AND \"ProjectAdmissionProjectId\" IS NULL AND \"ProjectAdmissionLifetimeId\" IS NULL)\nOR (\"ProjectAdmissionDatabaseProfileId\" IS NOT NULL AND \"ProjectAdmissionProjectId\" IS NOT NULL AND \"ProjectAdmissionLifetimeId\" IS NOT NULL\n    AND \"ProjectAdmissionDatabaseProfileId\" <> '00000000-0000-0000-0000-000000000000'::uuid\n    AND \"ProjectAdmissionProjectId\" <> '00000000-0000-0000-0000-000000000000'::uuid\n    AND \"ProjectAdmissionLifetimeId\" <> '00000000-0000-0000-0000-000000000000'::uuid)");

            migrationBuilder.CreateIndex(
                name: "IX_process_prepared_launches_AdmissionSequence",
                table: "process_prepared_launches",
                column: "AdmissionSequence",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_process_prepared_launches_CallerIntentId",
                table: "process_prepared_launches",
                column: "CallerIntentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_process_prepared_launches_LinkDeliveryState_AdmissionSequen~",
                table: "process_prepared_launches",
                columns: new[] { "LinkDeliveryState", "AdmissionSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_process_prepared_launches_RunId",
                table: "process_prepared_launches",
                column: "RunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_process_prepared_launches_State_ContinuationLeaseExpiresAtU~",
                table: "process_prepared_launches",
                columns: new[] { "State", "ContinuationLeaseExpiresAtUtc", "AdmissionSequence" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjectCreationReservations_LifetimeId",
                table: "Projects_ProjectCreationReservations",
                column: "LifetimeId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjectCreationReservations_ParentProjectId_State",
                table: "Projects_ProjectCreationReservations",
                columns: new[] { "ParentProjectId", "State" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ProjectCreationReservations_ProjectId",
                table: "Projects_ProjectCreationReservations",
                column: "ProjectId",
                unique: true,
                filter: "\"State\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulerPlanner_FireAdmissions_DedupeKey",
                table: "SchedulerPlanner_FireAdmissions",
                column: "DedupeKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulerPlanner_FireAdmissions_PlanId",
                table: "SchedulerPlanner_FireAdmissions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_SchedulerPlanner_FireAdmissions_PreparedWorkflowRunId",
                table: "SchedulerPlanner_FireAdmissions",
                column: "PreparedWorkflowRunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchedulerPlanner_FireAdmissions_State_NextObservationAtUtc_~",
                table: "SchedulerPlanner_FireAdmissions",
                columns: new[] { "State", "NextObservationAtUtc", "LeaseExpiresAtUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                LOCK TABLE "Projects_ProjectCreationReservations", "SchedulerPlanner_FireAdmissions", "SchedulerPlanner_Plans",
                    "process_prepared_launches", "process_runtime_states" IN ACCESS EXCLUSIVE MODE;
                DO $migration$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Projects_ProjectCreationReservations")
                        OR EXISTS (SELECT 1 FROM "SchedulerPlanner_FireAdmissions")
                        OR EXISTS (SELECT 1 FROM "process_prepared_launches")
                        OR EXISTS (SELECT 1 FROM "SchedulerPlanner_Plans" WHERE "StructureAuthorityJson" IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM "process_runtime_states" WHERE "LaunchAdmissionId" IS NOT NULL
                            OR "ProjectAdmissionDatabaseProfileId" IS NOT NULL OR "ProjectAdmissionProjectId" IS NOT NULL
                            OR "ProjectAdmissionLifetimeId" IS NOT NULL) THEN
                        RAISE EXCEPTION 'Cannot remove retained source-bound execution admission or creation reservation evidence.';
                    END IF;
                END $migration$;
                """);

            migrationBuilder.DropTable(
                name: "process_prepared_launches");

            migrationBuilder.DropTable(
                name: "Projects_ProjectCreationReservations");

            migrationBuilder.DropTable(
                name: "SchedulerPlanner_FireAdmissions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_process_runtime_states_project_admission",
                table: "process_runtime_states");

            migrationBuilder.DropColumn(
                name: "StructureAuthorityJson",
                table: "SchedulerPlanner_Plans");

            migrationBuilder.DropColumn(
                name: "LaunchAdmissionId",
                table: "process_runtime_states");

            migrationBuilder.DropColumn(
                name: "ProjectAdmissionDatabaseProfileId",
                table: "process_runtime_states");

            migrationBuilder.DropColumn(
                name: "ProjectAdmissionLifetimeId",
                table: "process_runtime_states");

            migrationBuilder.DropColumn(
                name: "ProjectAdmissionProjectId",
                table: "process_runtime_states");
        }
    }
}
