using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class AddProcessVerificationAuditRecords : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Processes_VerificationAuditRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    Lane = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ResponseCount = table.Column<int>(type: "integer", nullable: false),
                    AcceptedCount = table.Column<int>(type: "integer", nullable: false),
                    DeniedCount = table.Column<int>(type: "integer", nullable: false),
                    NoMutationPerformed = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsProcessMutation = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsTransitionMutation = table.Column<bool>(type: "boolean", nullable: false),
                    AllowsFinalizerMutation = table.Column<bool>(type: "boolean", nullable: false),
                    ObservationHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Processes_VerificationAuditRecords", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_VerificationAuditRecords_Lane",
                table: "Processes_VerificationAuditRecords",
                column: "Lane");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_VerificationAuditRecords_ObservationHash",
                table: "Processes_VerificationAuditRecords",
                column: "ObservationHash");

            migrationBuilder.CreateIndex(
                name: "IX_Processes_VerificationAuditRecords_ProcessRunId_RecordedAtU~",
                table: "Processes_VerificationAuditRecords",
                columns: new[] { "ProcessRunId", "RecordedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Processes_VerificationAuditRecords_StepRunId_RecordedAtUtc",
                table: "Processes_VerificationAuditRecords",
                columns: new[] { "StepRunId", "RecordedAtUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Processes_VerificationAuditRecords");
        }
    }
}
