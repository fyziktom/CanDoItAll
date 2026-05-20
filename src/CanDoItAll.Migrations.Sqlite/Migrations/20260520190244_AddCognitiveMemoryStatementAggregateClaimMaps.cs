using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCognitiveMemoryStatementAggregateClaimMaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_StatementId_MemoryRecordId_SourceItemId_EvidenceAnchorId",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps");

            migrationBuilder.AddColumn<Guid>(
                name: "AggregateClaimId",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_AggregateClaimId",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                column: "AggregateClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_StatementId_AggregateClaimId",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                columns: new[] { "StatementId", "AggregateClaimId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_StatementId_MemoryRecordId_AggregateClaimId_SourceItemId_EvidenceAnchorId",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                columns: new[] { "StatementId", "MemoryRecordId", "AggregateClaimId", "SourceItemId", "EvidenceAnchorId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMemory_DreamAggregateClaims_AggregateClaimId",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                column: "AggregateClaimId",
                principalTable: "CognitiveMemory_DreamAggregateClaims",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMemory_DreamAggregateClaims_AggregateClaimId",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps");

            migrationBuilder.DropIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_AggregateClaimId",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps");

            migrationBuilder.DropIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_StatementId_AggregateClaimId",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps");

            migrationBuilder.DropIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_StatementId_MemoryRecordId_AggregateClaimId_SourceItemId_EvidenceAnchorId",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps");

            migrationBuilder.DropColumn(
                name: "AggregateClaimId",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_StatementId_MemoryRecordId_SourceItemId_EvidenceAnchorId",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                columns: new[] { "StatementId", "MemoryRecordId", "SourceItemId", "EvidenceAnchorId" },
                unique: true);
        }
    }
}
