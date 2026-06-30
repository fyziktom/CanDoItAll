using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class RemoveUnusedValidationActivityAutomationModules : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Activity_Entries");

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
                name: "Validation_Findings");

            migrationBuilder.DropTable(
                name: "Validation_Checklists");

            migrationBuilder.DropTable(
                name: "Validation_Runs");

            migrationBuilder.DropTable(
                name: "Automation_Envelopes");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Activity_Entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Actor = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ArtifactId = table.Column<Guid>(type: "uuid", nullable: true),
                    ArtifactKind = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Route = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activity_Entries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Automation_DeadLetters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    CausationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DeadLetteredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DedupeKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    DeliveryId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvelopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvelopeType = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: false),
                    HandlerKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false)
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
                    AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                    CausationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeliveryId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvelopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: false),
                    HandlerKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    AvailableAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CausationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DedupeKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: true),
                    EnvelopeType = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                    CausationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DetailsJson = table.Column<string>(type: "TEXT", nullable: false),
                    EventKind = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    SourceId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false)
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
                    CursorValue = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    SourceKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SourceKind = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
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
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CursorValue = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    DedupeKey = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: false),
                    ExternalMessageId = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    LastError = table.Column<string>(type: "TEXT", nullable: false),
                    MaterializationSummary = table.Column<string>(type: "TEXT", nullable: false),
                    MaterializedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MaterializerKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SourceKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SourceKind = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                    CronExpression = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    DedupeKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    EndAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastFiredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MisfirePolicy = table.Column<int>(type: "integer", nullable: false),
                    NextPlannedFireAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OwnerKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    OwnerKind = table.Column<int>(type: "integer", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    StartAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    TimeZoneId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    TriggerKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    TriggerKind = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Automation_Triggers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Validation_Findings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Detail = table.Column<string>(type: "TEXT", nullable: false),
                    RecommendedAction = table.Column<string>(type: "TEXT", nullable: false),
                    RuleCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ValidationRunId = table.Column<Guid>(type: "uuid", nullable: false)
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
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ItemsJson = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ValidationType = table.Column<int>(type: "integer", nullable: false),
                    VersionLabel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
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
                    ArtifactRoute = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ArtifactTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ChecklistId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Decision = table.Column<int>(type: "integer", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ResponsiblePartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceContent = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ValidationType = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Validation_Runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Automation_EnvelopeDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    AvailableAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EnvelopeId = table.Column<Guid>(type: "uuid", nullable: false),
                    EnvelopeType = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    HandlerKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    LastAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: false),
                    LockToken = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LockedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
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
                name: "IX_Validation_Runs_CreatedAtUtc",
                table: "Validation_Runs",
                column: "CreatedAtUtc");
        }
    }
}
