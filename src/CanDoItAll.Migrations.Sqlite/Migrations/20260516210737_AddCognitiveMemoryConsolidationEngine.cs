using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCognitiveMemoryConsolidationEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ConsolidationCursors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceSystem = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Cursor = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    LastSourceHashAlgorithm = table.Column<int>(type: "INTEGER", nullable: false),
                    LastSourceHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    LastRunId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ConsolidationCursors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ConsolidationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    TriggerKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ProfileName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    InputHashAlgorithm = table.Column<int>(type: "INTEGER", nullable: false),
                    InputHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    OutputHashAlgorithm = table.Column<int>(type: "INTEGER", nullable: false),
                    OutputHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Cursor = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    NextCursor = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    LeaseOwnerId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    SourceItemsScanned = table.Column<int>(type: "INTEGER", nullable: false),
                    CandidatesCreated = table.Column<int>(type: "INTEGER", nullable: false),
                    MutationCommandsSubmitted = table.Column<int>(type: "INTEGER", nullable: false),
                    ReviewItemsCreated = table.Column<int>(type: "INTEGER", nullable: false),
                    ProjectionInvalidations = table.Column<int>(type: "INTEGER", nullable: false),
                    FailureCode = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    FailureMessage = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
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
                name: "CognitiveMemory_ConsolidationCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CandidateKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EvidenceAnchorId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MemoryRecordId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MutationCommandId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReviewItemId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ScoreEvaluationTraceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ScoreBucket = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayPriorityProjection = table.Column<double>(type: "REAL", nullable: true),
                    SourceContentHashAlgorithm = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceContentHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    OutputHashAlgorithm = table.Column<int>(type: "INTEGER", nullable: false),
                    OutputHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    ReasonCode = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    ReasonText = table.Column<string>(type: "TEXT", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ConsolidationCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ConsolidationCandidates_CognitiveMemory_ConsolidationRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "CognitiveMemory_ConsolidationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ConsolidationReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RunId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReportHashAlgorithm = table.Column<int>(type: "INTEGER", nullable: false),
                    ReportHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ReportJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ConsolidationReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ConsolidationReports_CognitiveMemory_ConsolidationRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "CognitiveMemory_ConsolidationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationCandidates_MutationCommandId",
                table: "CognitiveMemory_ConsolidationCandidates",
                column: "MutationCommandId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationCandidates_ProjectId_CandidateKind_Status",
                table: "CognitiveMemory_ConsolidationCandidates",
                columns: new[] { "ProjectId", "CandidateKind", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationCandidates_ProjectId_SourceItemId_CandidateKind_SourceContentHash_AlgorithmVersion",
                table: "CognitiveMemory_ConsolidationCandidates",
                columns: new[] { "ProjectId", "SourceItemId", "CandidateKind", "SourceContentHash", "AlgorithmVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationCandidates_ReviewItemId",
                table: "CognitiveMemory_ConsolidationCandidates",
                column: "ReviewItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationCandidates_RunId_CandidateKind_Status",
                table: "CognitiveMemory_ConsolidationCandidates",
                columns: new[] { "RunId", "CandidateKind", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationCandidates_ScoreEvaluationTraceId",
                table: "CognitiveMemory_ConsolidationCandidates",
                column: "ScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationCursors_LastRunId",
                table: "CognitiveMemory_ConsolidationCursors",
                column: "LastRunId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationCursors_ProjectId_Mode_SourceSystem",
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
                name: "IX_CognitiveMemory_ConsolidationRuns_ProjectId_Mode_LeaseExpiresAtUtc",
                table: "CognitiveMemory_ConsolidationRuns",
                columns: new[] { "ProjectId", "Mode", "LeaseExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationRuns_ProjectId_Mode_Status_StartedAtUtc",
                table: "CognitiveMemory_ConsolidationRuns",
                columns: new[] { "ProjectId", "Mode", "Status", "StartedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CognitiveMemory_ConsolidationCandidates");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ConsolidationCursors");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ConsolidationReports");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ConsolidationRuns");
        }
    }
}
