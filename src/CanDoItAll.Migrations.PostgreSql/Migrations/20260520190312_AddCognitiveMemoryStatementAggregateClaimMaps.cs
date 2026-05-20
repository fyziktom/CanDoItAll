using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddCognitiveMemoryStatementAggregateClaimMaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMem~",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps");

            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMe~1",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps");

            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMe~2",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps");

            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMe~3",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps");

            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMe~4",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps");

            migrationBuilder.DropIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_StatementId_~",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps");

            migrationBuilder.AddColumn<Guid>(
                name: "AggregateClaimId",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_AggregateCla~",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                column: "AggregateClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_StatementId_~",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                columns: new[] { "StatementId", "AggregateClaimId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_StatementId~1",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                columns: new[] { "StatementId", "MemoryRecordId", "AggregateClaimId", "SourceItemId", "EvidenceAnchorId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMem~",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                column: "AggregateClaimId",
                principalTable: "CognitiveMemory_DreamAggregateClaims",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMe~1",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                column: "EvidenceAnchorId",
                principalTable: "CognitiveMemory_EvidenceAnchors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMe~2",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                column: "MemoryRecordId",
                principalTable: "CognitiveMemory_Records",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMe~3",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                column: "SourceItemId",
                principalTable: "CognitiveMemory_SourceItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMe~4",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                column: "StatementId",
                principalTable: "CognitiveMemory_SynthesizedStatements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMe~5",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                column: "SynthesisId",
                principalTable: "CognitiveMemory_SynthesizedRecalls",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMem~",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps");

            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMe~1",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps");

            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMe~2",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps");

            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMe~3",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps");

            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMe~4",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps");

            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMe~5",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps");

            migrationBuilder.DropIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_AggregateCla~",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps");

            migrationBuilder.DropIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_StatementId_~",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps");

            migrationBuilder.DropIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_StatementId~1",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps");

            migrationBuilder.DropColumn(
                name: "AggregateClaimId",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_StatementId_~",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                columns: new[] { "StatementId", "MemoryRecordId", "SourceItemId", "EvidenceAnchorId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMem~",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                column: "EvidenceAnchorId",
                principalTable: "CognitiveMemory_EvidenceAnchors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMe~1",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                column: "MemoryRecordId",
                principalTable: "CognitiveMemory_Records",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMe~2",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                column: "SourceItemId",
                principalTable: "CognitiveMemory_SourceItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMe~3",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                column: "StatementId",
                principalTable: "CognitiveMemory_SynthesizedStatements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMe~4",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                column: "SynthesisId",
                principalTable: "CognitiveMemory_SynthesizedRecalls",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
