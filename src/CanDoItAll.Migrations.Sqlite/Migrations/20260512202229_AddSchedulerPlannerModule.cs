using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddSchedulerPlannerModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SchedulerPlanner_Plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false),
                    Description = table.Column<string>(type: "TEXT", nullable: false),
                    TargetKind = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TargetVersionId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TargetNameSnapshot = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    CronExpression = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    CronDescription = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    TimeZoneId = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    MisfirePolicy = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    StartAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EndAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    InputJson = table.Column<string>(type: "TEXT", nullable: false),
                    AutomationTriggerId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AutomationTriggerKey = table.Column<string>(type: "TEXT", maxLength: 180, nullable: false),
                    NextPlannedFireAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastFiredAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchedulerPlanner_Plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchedulerPlanner_Runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PlanId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DedupeKey = table.Column<string>(type: "TEXT", maxLength: 260, nullable: false),
                    AutomationEnvelopeId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CorrelationId = table.Column<Guid>(type: "TEXT", nullable: true),
                    FiredAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetRunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    TargetRunKind = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: false),
                    DispatchedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SchedulerPlanner_Runs");

            migrationBuilder.DropTable(
                name: "SchedulerPlanner_Plans");
        }
    }
}
