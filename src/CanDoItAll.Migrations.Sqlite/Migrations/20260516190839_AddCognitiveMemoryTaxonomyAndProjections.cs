using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCognitiveMemoryTaxonomyAndProjections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DisplayStrengthProjection",
                table: "CognitiveMemory_Relations",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "CognitiveMemory_Relations",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RelationBucket",
                table: "CognitiveMemory_Relations",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "RelationScoreEvaluationTraceId",
                table: "CognitiveMemory_Relations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActivationBucket",
                table: "CognitiveMemory_Records",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ActivationScoreEvaluationTraceId",
                table: "CognitiveMemory_Records",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConfidenceBucket",
                table: "CognitiveMemory_Records",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "ConfidenceScoreEvaluationTraceId",
                table: "CognitiveMemory_Records",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EvidenceAnchorCount",
                table: "CognitiveMemory_Records",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryClaimId",
                table: "CognitiveMemory_Records",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryContextFrameId",
                table: "CognitiveMemory_Records",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TopicKey",
                table: "CognitiveMemory_Records",
                type: "TEXT",
                maxLength: 240,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_Projections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    MemoryRecordId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectionStoreKind = table.Column<int>(type: "INTEGER", nullable: false),
                    ProjectionKind = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetProviderName = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    CollectionName = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    PointId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ProjectionProfileId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    EmbeddingProfileId = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    ProjectionSchemaVersion = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    VectorDimensions = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceHashAlgorithm = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    PayloadHashAlgorithm = table.Column<int>(type: "INTEGER", nullable: false),
                    PayloadHash = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StaleReason = table.Column<int>(type: "INTEGER", nullable: false),
                    RebuildRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    FailureCode = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    FailureMessage = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastProjectedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    DeletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_Projections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Projections_CognitiveMemory_Records_MemoryRecordId",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_RecordEvidenceAnchors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    MemoryRecordId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvidenceAnchorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvidenceRole = table.Column<int>(type: "INTEGER", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_RecordEvidenceAnchors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecordEvidenceAnchors_CognitiveMemory_EvidenceAnchors_EvidenceAnchorId",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RecordEvidenceAnchors_CognitiveMemory_Records_MemoryRecordId",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_RelationEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    RelationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EvidenceAnchorId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Direction = table.Column<int>(type: "INTEGER", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_RelationEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RelationEvidence_CognitiveMemory_EvidenceAnchors_EvidenceAnchorId",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_RelationEvidence_CognitiveMemory_Relations_RelationId",
                        column: x => x.RelationId,
                        principalTable: "CognitiveMemory_Relations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Relations_RelationScoreEvaluationTraceId",
                table: "CognitiveMemory_Relations",
                column: "RelationScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Relations_SourceMemoryRecordId",
                table: "CognitiveMemory_Relations",
                column: "SourceMemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Records_ActivationScoreEvaluationTraceId",
                table: "CognitiveMemory_Records",
                column: "ActivationScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Records_ConfidenceScoreEvaluationTraceId",
                table: "CognitiveMemory_Records",
                column: "ConfidenceScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Records_PrimaryClaimId",
                table: "CognitiveMemory_Records",
                column: "PrimaryClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Records_PrimaryContextFrameId",
                table: "CognitiveMemory_Records",
                column: "PrimaryContextFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Records_ProjectId_TopicKey",
                table: "CognitiveMemory_Records",
                columns: new[] { "ProjectId", "TopicKey" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Projections_MemoryRecordId_ProjectionStoreKind_ProjectionKind_ProjectionProfileId_EmbeddingProfileId",
                table: "CognitiveMemory_Projections",
                columns: new[] { "MemoryRecordId", "ProjectionStoreKind", "ProjectionKind", "ProjectionProfileId", "EmbeddingProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Projections_PayloadHash",
                table: "CognitiveMemory_Projections",
                column: "PayloadHash");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Projections_PointId",
                table: "CognitiveMemory_Projections",
                column: "PointId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Projections_ProjectId_CollectionName_Status",
                table: "CognitiveMemory_Projections",
                columns: new[] { "ProjectId", "CollectionName", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Projections_ProjectId_RebuildRequired_StaleReason",
                table: "CognitiveMemory_Projections",
                columns: new[] { "ProjectId", "RebuildRequired", "StaleReason" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Projections_SourceHash",
                table: "CognitiveMemory_Projections",
                column: "SourceHash");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecordEvidenceAnchors_EvidenceAnchorId_EvidenceRole",
                table: "CognitiveMemory_RecordEvidenceAnchors",
                columns: new[] { "EvidenceAnchorId", "EvidenceRole" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecordEvidenceAnchors_MemoryRecordId_EvidenceAnchorId_EvidenceRole",
                table: "CognitiveMemory_RecordEvidenceAnchors",
                columns: new[] { "MemoryRecordId", "EvidenceAnchorId", "EvidenceRole" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RelationEvidence_EvidenceAnchorId_Direction",
                table: "CognitiveMemory_RelationEvidence",
                columns: new[] { "EvidenceAnchorId", "Direction" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RelationEvidence_RelationId_EvidenceAnchorId_Direction",
                table: "CognitiveMemory_RelationEvidence",
                columns: new[] { "RelationId", "EvidenceAnchorId", "Direction" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CognitiveMemory_Records_CognitiveMemory_Claims_PrimaryClaimId",
                table: "CognitiveMemory_Records",
                column: "PrimaryClaimId",
                principalTable: "CognitiveMemory_Claims",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CognitiveMemory_Records_CognitiveMemory_ContextFrames_PrimaryContextFrameId",
                table: "CognitiveMemory_Records",
                column: "PrimaryContextFrameId",
                principalTable: "CognitiveMemory_ContextFrames",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CognitiveMemory_Records_CognitiveMemory_ScoreEvaluations_ActivationScoreEvaluationTraceId",
                table: "CognitiveMemory_Records",
                column: "ActivationScoreEvaluationTraceId",
                principalTable: "CognitiveMemory_ScoreEvaluations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CognitiveMemory_Records_CognitiveMemory_ScoreEvaluations_ConfidenceScoreEvaluationTraceId",
                table: "CognitiveMemory_Records",
                column: "ConfidenceScoreEvaluationTraceId",
                principalTable: "CognitiveMemory_ScoreEvaluations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CognitiveMemory_Relations_CognitiveMemory_Records_SourceMemoryRecordId",
                table: "CognitiveMemory_Relations",
                column: "SourceMemoryRecordId",
                principalTable: "CognitiveMemory_Records",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CognitiveMemory_Relations_CognitiveMemory_Records_TargetMemoryRecordId",
                table: "CognitiveMemory_Relations",
                column: "TargetMemoryRecordId",
                principalTable: "CognitiveMemory_Records",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CognitiveMemory_Relations_CognitiveMemory_ScoreEvaluations_RelationScoreEvaluationTraceId",
                table: "CognitiveMemory_Relations",
                column: "RelationScoreEvaluationTraceId",
                principalTable: "CognitiveMemory_ScoreEvaluations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CognitiveMemory_SourceLinks_CognitiveMemory_Records_MemoryRecordId",
                table: "CognitiveMemory_SourceLinks",
                column: "MemoryRecordId",
                principalTable: "CognitiveMemory_Records",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CognitiveMemory_SourceLinks_CognitiveMemory_SourceItems_SourceItemId",
                table: "CognitiveMemory_SourceLinks",
                column: "SourceItemId",
                principalTable: "CognitiveMemory_SourceItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CognitiveMemory_SourceLinks_CognitiveMemory_SourceManifests_SourceManifestId",
                table: "CognitiveMemory_SourceLinks",
                column: "SourceManifestId",
                principalTable: "CognitiveMemory_SourceManifests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_Records_CognitiveMemory_Claims_PrimaryClaimId",
                table: "CognitiveMemory_Records");

            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_Records_CognitiveMemory_ContextFrames_PrimaryContextFrameId",
                table: "CognitiveMemory_Records");

            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_Records_CognitiveMemory_ScoreEvaluations_ActivationScoreEvaluationTraceId",
                table: "CognitiveMemory_Records");

            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_Records_CognitiveMemory_ScoreEvaluations_ConfidenceScoreEvaluationTraceId",
                table: "CognitiveMemory_Records");

            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_Relations_CognitiveMemory_Records_SourceMemoryRecordId",
                table: "CognitiveMemory_Relations");

            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_Relations_CognitiveMemory_Records_TargetMemoryRecordId",
                table: "CognitiveMemory_Relations");

            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_Relations_CognitiveMemory_ScoreEvaluations_RelationScoreEvaluationTraceId",
                table: "CognitiveMemory_Relations");

            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_SourceLinks_CognitiveMemory_Records_MemoryRecordId",
                table: "CognitiveMemory_SourceLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_SourceLinks_CognitiveMemory_SourceItems_SourceItemId",
                table: "CognitiveMemory_SourceLinks");

            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_SourceLinks_CognitiveMemory_SourceManifests_SourceManifestId",
                table: "CognitiveMemory_SourceLinks");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_Projections");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_RecordEvidenceAnchors");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_RelationEvidence");

            migrationBuilder.DropIndex(
                name: "IX_CognitiveMemory_Relations_RelationScoreEvaluationTraceId",
                table: "CognitiveMemory_Relations");

            migrationBuilder.DropIndex(
                name: "IX_CognitiveMemory_Relations_SourceMemoryRecordId",
                table: "CognitiveMemory_Relations");

            migrationBuilder.DropIndex(
                name: "IX_CognitiveMemory_Records_ActivationScoreEvaluationTraceId",
                table: "CognitiveMemory_Records");

            migrationBuilder.DropIndex(
                name: "IX_CognitiveMemory_Records_ConfidenceScoreEvaluationTraceId",
                table: "CognitiveMemory_Records");

            migrationBuilder.DropIndex(
                name: "IX_CognitiveMemory_Records_PrimaryClaimId",
                table: "CognitiveMemory_Records");

            migrationBuilder.DropIndex(
                name: "IX_CognitiveMemory_Records_PrimaryContextFrameId",
                table: "CognitiveMemory_Records");

            migrationBuilder.DropIndex(
                name: "IX_CognitiveMemory_Records_ProjectId_TopicKey",
                table: "CognitiveMemory_Records");

            migrationBuilder.DropColumn(
                name: "DisplayStrengthProjection",
                table: "CognitiveMemory_Relations");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "CognitiveMemory_Relations");

            migrationBuilder.DropColumn(
                name: "RelationBucket",
                table: "CognitiveMemory_Relations");

            migrationBuilder.DropColumn(
                name: "RelationScoreEvaluationTraceId",
                table: "CognitiveMemory_Relations");

            migrationBuilder.DropColumn(
                name: "ActivationBucket",
                table: "CognitiveMemory_Records");

            migrationBuilder.DropColumn(
                name: "ActivationScoreEvaluationTraceId",
                table: "CognitiveMemory_Records");

            migrationBuilder.DropColumn(
                name: "ConfidenceBucket",
                table: "CognitiveMemory_Records");

            migrationBuilder.DropColumn(
                name: "ConfidenceScoreEvaluationTraceId",
                table: "CognitiveMemory_Records");

            migrationBuilder.DropColumn(
                name: "EvidenceAnchorCount",
                table: "CognitiveMemory_Records");

            migrationBuilder.DropColumn(
                name: "PrimaryClaimId",
                table: "CognitiveMemory_Records");

            migrationBuilder.DropColumn(
                name: "PrimaryContextFrameId",
                table: "CognitiveMemory_Records");

            migrationBuilder.DropColumn(
                name: "TopicKey",
                table: "CognitiveMemory_Records");
        }
    }
}
