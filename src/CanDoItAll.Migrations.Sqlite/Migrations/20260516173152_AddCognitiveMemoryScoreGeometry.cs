using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCognitiveMemoryScoreGeometry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ScoreEvaluations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OwnerKind = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SpaceKind = table.Column<int>(type: "INTEGER", nullable: false),
                    SchemaVersion = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    NormalizationProfile = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    InputHashAlgorithm = table.Column<int>(type: "INTEGER", nullable: false),
                    InputHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    ScalarProjectionKind = table.Column<int>(type: "INTEGER", nullable: false),
                    ProjectionBucket = table.Column<int>(type: "INTEGER", nullable: false),
                    DisplayScore = table.Column<double>(type: "REAL", nullable: true),
                    MissingRequiredDimensionCount = table.Column<int>(type: "INTEGER", nullable: false),
                    MatchedShapeCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TracePayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    CalculatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ScoreEvaluations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ScoreComponents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ScoreEvaluationTraceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    OwnerKind = table.Column<int>(type: "INTEGER", nullable: false),
                    OwnerId = table.Column<Guid>(type: "TEXT", nullable: true),
                    SpaceKind = table.Column<int>(type: "INTEGER", nullable: false),
                    SchemaVersion = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    DimensionKind = table.Column<int>(type: "INTEGER", nullable: false),
                    NormalizedValue = table.Column<double>(type: "REAL", nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: false),
                    EvidenceKind = table.Column<int>(type: "INTEGER", nullable: false),
                    EvidenceId = table.Column<Guid>(type: "TEXT", nullable: true),
                    EvidenceConfidence = table.Column<double>(type: "REAL", nullable: true),
                    CalculatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    ComponentPayloadJson = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ScoreComponents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ScoreComponents_CognitiveMemory_ScoreEvaluations_ScoreEvaluationTraceId",
                        column: x => x.ScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ScoreComponents_OwnerKind_OwnerId_DimensionKind",
                table: "CognitiveMemory_ScoreComponents",
                columns: new[] { "OwnerKind", "OwnerId", "DimensionKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ScoreComponents_ProjectId_SpaceKind_DimensionKind_CalculatedAtUtc",
                table: "CognitiveMemory_ScoreComponents",
                columns: new[] { "ProjectId", "SpaceKind", "DimensionKind", "CalculatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ScoreComponents_ScoreEvaluationTraceId_DimensionKind",
                table: "CognitiveMemory_ScoreComponents",
                columns: new[] { "ScoreEvaluationTraceId", "DimensionKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ScoreComponents_SchemaVersion_DimensionKind",
                table: "CognitiveMemory_ScoreComponents",
                columns: new[] { "SchemaVersion", "DimensionKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ScoreEvaluations_InputHash",
                table: "CognitiveMemory_ScoreEvaluations",
                column: "InputHash");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ScoreEvaluations_OwnerKind_OwnerId_SpaceKind",
                table: "CognitiveMemory_ScoreEvaluations",
                columns: new[] { "OwnerKind", "OwnerId", "SpaceKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ScoreEvaluations_ProjectId_SpaceKind_SchemaVersion_CalculatedAtUtc",
                table: "CognitiveMemory_ScoreEvaluations",
                columns: new[] { "ProjectId", "SpaceKind", "SchemaVersion", "CalculatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CognitiveMemory_ScoreComponents");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ScoreEvaluations");
        }
    }
}
