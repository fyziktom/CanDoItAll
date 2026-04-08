using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddCrmHrAccountsAndInteractions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CrmHr_AccountProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountPartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationshipStage = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CommercialNotes = table.Column<string>(type: "TEXT", nullable: false),
                    ConstraintNotes = table.Column<string>(type: "TEXT", nullable: false),
                    TimingRiskNotes = table.Column<string>(type: "TEXT", nullable: false),
                    LastChangedBy = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_AccountProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CrmHr_AccountStakeholders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountPartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelatedPartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_AccountStakeholders", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_AccountProfiles_AccountPartyId",
                table: "CrmHr_AccountProfiles",
                column: "AccountPartyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_AccountStakeholders_AccountPartyId_RelatedPartyId_Role",
                table: "CrmHr_AccountStakeholders",
                columns: new[] { "AccountPartyId", "RelatedPartyId", "Role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_AccountStakeholders_RelatedPartyId",
                table: "CrmHr_AccountStakeholders",
                column: "RelatedPartyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CrmHr_AccountProfiles");

            migrationBuilder.DropTable(
                name: "CrmHr_AccountStakeholders");
        }
    }
}
