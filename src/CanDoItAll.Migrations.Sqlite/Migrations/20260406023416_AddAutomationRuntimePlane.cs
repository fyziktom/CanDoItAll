using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomationRuntimePlane : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Automation_DeadLetters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EnvelopeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeliveryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EnvelopeType = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    HandlerKey = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: false),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DedupeKey = table.Column<string>(type: "TEXT", maxLength: 240, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CausationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    DeadLetteredAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Automation_DeadLetters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Automation_DeliveryAttempts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EnvelopeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DeliveryId = table.Column<Guid>(type: "TEXT", nullable: false),
                    HandlerKey = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    AttemptNumber = table.Column<int>(type: "INTEGER", nullable: false),
                    Outcome = table.Column<int>(type: "INTEGER", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CausationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Automation_DeliveryAttempts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Automation_Envelopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EnvelopeType = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    DedupeKey = table.Column<string>(type: "TEXT", maxLength: 240, nullable: true),
                    CorrelationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CausationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    AvailableAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Automation_Envelopes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Automation_ExecutionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventKind = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceType = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    SourceId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    CorrelationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CausationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Message = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    DetailsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Automation_ExecutionLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Automation_PluginIngressCursors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceKind = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    SourceKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    CursorValue = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Automation_PluginIngressCursors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Automation_PluginIngressEnvelopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SourceKind = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    SourceKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    ExternalMessageId = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    CursorValue = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    DedupeKey = table.Column<string>(type: "TEXT", maxLength: 280, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MaterializerKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MaterializationSummary = table.Column<string>(type: "TEXT", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", nullable: false),
                    ReceivedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    MaterializedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Automation_PluginIngressEnvelopes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Automation_Triggers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    OwnerKind = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnerKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    TriggerKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    TriggerKind = table.Column<int>(type: "INTEGER", nullable: false),
                    CronExpression = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    TimeZoneId = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    StartAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EndAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    MisfirePolicy = table.Column<int>(type: "INTEGER", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    DedupeKey = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    NextPlannedFireAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastFiredAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Automation_Triggers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Automation_EnvelopeDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    EnvelopeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EnvelopeType = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    HandlerKey = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    State = table.Column<int>(type: "INTEGER", nullable: false),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MaxAttempts = table.Column<int>(type: "INTEGER", nullable: false),
                    AvailableAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: false),
                    LockToken = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LockedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Automation_EnvelopeDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Automation_EnvelopeDeliveries_Automation_Envelopes_EnvelopeId",
                        column: x => x.EnvelopeId,
                        principalTable: "Automation_Envelopes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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
                name: "IX_Automation_EnvelopeDeliveries_State_AvailableAtUtc",
                table: "Automation_EnvelopeDeliveries",
                columns: new[] { "State", "AvailableAtUtc" });

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
                name: "IX_Automation_PluginIngressEnvelopes_SourceKind_SourceKey_DedupeKey",
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
                name: "Automation_Envelopes");
        }
    }
}
