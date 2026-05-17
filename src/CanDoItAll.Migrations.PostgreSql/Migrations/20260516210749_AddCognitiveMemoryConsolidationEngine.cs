using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Cursor = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    LastSourceHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    LastSourceHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LastRunId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ConsolidationCursors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ConsolidationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    TriggerKind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ProfileName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    InputHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    InputHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OutputHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    OutputHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Cursor = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    NextCursor = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    LeaseOwnerId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    SourceItemsScanned = table.Column<int>(type: "integer", nullable: false),
                    CandidatesCreated = table.Column<int>(type: "integer", nullable: false),
                    MutationCommandsSubmitted = table.Column<int>(type: "integer", nullable: false),
                    ReviewItemsCreated = table.Column<int>(type: "integer", nullable: false),
                    ProjectionInvalidations = table.Column<int>(type: "integer", nullable: false),
                    FailureCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FailureMessage = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
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
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    CandidateKind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: true),
                    MemoryRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    MutationCommandId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ScoreBucket = table.Column<int>(type: "integer", nullable: false),
                    DisplayPriorityProjection = table.Column<double>(type: "double precision", nullable: true),
                    SourceContentHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    SourceContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    OutputHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    OutputHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ReasonText = table.Column<string>(type: "TEXT", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ConsolidationCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ConsolidationCandidates_CognitiveMemory_Con~",
                        column: x => x.RunId,
                        principalTable: "CognitiveMemory_ConsolidationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ConsolidationReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReportHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    ReportHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ReportJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ConsolidationReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ConsolidationReports_CognitiveMemory_Consol~",
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
                name: "IX_CognitiveMemory_ConsolidationCandidates_ProjectId_Candidate~",
                table: "CognitiveMemory_ConsolidationCandidates",
                columns: new[] { "ProjectId", "CandidateKind", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationCandidates_ProjectId_SourceIte~",
                table: "CognitiveMemory_ConsolidationCandidates",
                columns: new[] { "ProjectId", "SourceItemId", "CandidateKind", "SourceContentHash", "AlgorithmVersion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationCandidates_ReviewItemId",
                table: "CognitiveMemory_ConsolidationCandidates",
                column: "ReviewItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationCandidates_RunId_CandidateKind~",
                table: "CognitiveMemory_ConsolidationCandidates",
                columns: new[] { "RunId", "CandidateKind", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationCandidates_ScoreEvaluationTrac~",
                table: "CognitiveMemory_ConsolidationCandidates",
                column: "ScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationCursors_LastRunId",
                table: "CognitiveMemory_ConsolidationCursors",
                column: "LastRunId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationCursors_ProjectId_Mode_SourceS~",
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
                name: "IX_CognitiveMemory_ConsolidationRuns_ProjectId_Mode_LeaseExpir~",
                table: "CognitiveMemory_ConsolidationRuns",
                columns: new[] { "ProjectId", "Mode", "LeaseExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ConsolidationRuns_ProjectId_Mode_Status_Sta~",
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
