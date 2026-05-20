using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class AddCognitiveMemoryQualityFollowupHardening : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AggregateEligible",
                table: "CognitiveMemory_QualityClusters",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "CohesionScore",
                table: "CognitiveMemory_QualityClusters",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "CompositeScore",
                table: "CognitiveMemory_QualityClusters",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "EligibilityReason",
                table: "CognitiveMemory_QualityClusters",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "GuardPenaltyScore",
                table: "CognitiveMemory_QualityClusters",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "SemanticSignalScore",
                table: "CognitiveMemory_QualityClusters",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "SourceDiversityScore",
                table: "CognitiveMemory_QualityClusters",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "SourceIndependenceScore",
                table: "CognitiveMemory_QualityClusters",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "SupportingSignalScore",
                table: "CognitiveMemory_QualityClusters",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AnchorRetiredAtUtc",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AnchorState",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "AssimilatedMemoryRecordId",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CaptureLanguage",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CaptureScope",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewItemId",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TargetClaimIdsJson",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "TargetConfidenceScore",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "TargetingStatus",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_QualityClusters_ProjectId_AggregateEligible~",
                table: "CognitiveMemory_QualityClusters",
                columns: new[] { "ProjectId", "AggregateEligible", "CompositeScore" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorCapturedImprovements_AppliedMemoryRe~",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                column: "AppliedMemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorCapturedImprovements_AssimilatedMemo~",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                column: "AssimilatedMemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorCapturedImprovements_ProjectId_Ancho~",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                columns: new[] { "ProjectId", "AnchorState", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorCapturedImprovements_ProjectId_Targe~",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                columns: new[] { "ProjectId", "TargetingStatus", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_CuratorCapturedImprovements_ReviewItemId",
                table: "CognitiveMemory_CuratorCapturedImprovements",
                column: "ReviewItemId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CognitiveMemory_QualityClusters_ProjectId_AggregateEligible~",
                table: "CognitiveMemory_QualityClusters");

            migrationBuilder.DropIndex(
                name: "IX_CognitiveMemory_CuratorCapturedImprovements_AppliedMemoryRe~",
                table: "CognitiveMemory_CuratorCapturedImprovements");

            migrationBuilder.DropIndex(
                name: "IX_CognitiveMemory_CuratorCapturedImprovements_AssimilatedMemo~",
                table: "CognitiveMemory_CuratorCapturedImprovements");

            migrationBuilder.DropIndex(
                name: "IX_CognitiveMemory_CuratorCapturedImprovements_ProjectId_Ancho~",
                table: "CognitiveMemory_CuratorCapturedImprovements");

            migrationBuilder.DropIndex(
                name: "IX_CognitiveMemory_CuratorCapturedImprovements_ProjectId_Targe~",
                table: "CognitiveMemory_CuratorCapturedImprovements");

            migrationBuilder.DropIndex(
                name: "IX_CognitiveMemory_CuratorCapturedImprovements_ReviewItemId",
                table: "CognitiveMemory_CuratorCapturedImprovements");

            migrationBuilder.DropColumn(
                name: "AggregateEligible",
                table: "CognitiveMemory_QualityClusters");

            migrationBuilder.DropColumn(
                name: "CohesionScore",
                table: "CognitiveMemory_QualityClusters");

            migrationBuilder.DropColumn(
                name: "CompositeScore",
                table: "CognitiveMemory_QualityClusters");

            migrationBuilder.DropColumn(
                name: "EligibilityReason",
                table: "CognitiveMemory_QualityClusters");

            migrationBuilder.DropColumn(
                name: "GuardPenaltyScore",
                table: "CognitiveMemory_QualityClusters");

            migrationBuilder.DropColumn(
                name: "SemanticSignalScore",
                table: "CognitiveMemory_QualityClusters");

            migrationBuilder.DropColumn(
                name: "SourceDiversityScore",
                table: "CognitiveMemory_QualityClusters");

            migrationBuilder.DropColumn(
                name: "SourceIndependenceScore",
                table: "CognitiveMemory_QualityClusters");

            migrationBuilder.DropColumn(
                name: "SupportingSignalScore",
                table: "CognitiveMemory_QualityClusters");

            migrationBuilder.DropColumn(
                name: "AnchorRetiredAtUtc",
                table: "CognitiveMemory_CuratorCapturedImprovements");

            migrationBuilder.DropColumn(
                name: "AnchorState",
                table: "CognitiveMemory_CuratorCapturedImprovements");

            migrationBuilder.DropColumn(
                name: "AssimilatedMemoryRecordId",
                table: "CognitiveMemory_CuratorCapturedImprovements");

            migrationBuilder.DropColumn(
                name: "CaptureLanguage",
                table: "CognitiveMemory_CuratorCapturedImprovements");

            migrationBuilder.DropColumn(
                name: "CaptureScope",
                table: "CognitiveMemory_CuratorCapturedImprovements");

            migrationBuilder.DropColumn(
                name: "ReviewItemId",
                table: "CognitiveMemory_CuratorCapturedImprovements");

            migrationBuilder.DropColumn(
                name: "TargetClaimIdsJson",
                table: "CognitiveMemory_CuratorCapturedImprovements");

            migrationBuilder.DropColumn(
                name: "TargetConfidenceScore",
                table: "CognitiveMemory_CuratorCapturedImprovements");

            migrationBuilder.DropColumn(
                name: "TargetingStatus",
                table: "CognitiveMemory_CuratorCapturedImprovements");
        }
    }
}
