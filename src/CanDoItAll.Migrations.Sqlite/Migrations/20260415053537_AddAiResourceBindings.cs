using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddAiResourceBindings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CrmHr_AiResourceBindings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PartyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    TechnicalAgentId = table.Column<Guid>(type: "TEXT", nullable: true),
                    BindingStatus = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    BindingReason = table.Column<string>(type: "TEXT", nullable: false),
                    LastError = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_AiResourceBindings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_AiResourceBindings_PartyId",
                table: "CrmHr_AiResourceBindings",
                column: "PartyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_AiResourceBindings_TechnicalAgentId",
                table: "CrmHr_AiResourceBindings",
                column: "TechnicalAgentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CrmHr_AiResourceBindings");
        }
    }
}
