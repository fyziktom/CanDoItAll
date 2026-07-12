using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class GenericMemoryProviderRuntime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}
