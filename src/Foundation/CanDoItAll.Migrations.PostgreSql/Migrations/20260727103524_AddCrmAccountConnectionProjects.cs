using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class AddCrmAccountConnectionProjects : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CrmHr_AccountConnectionProjects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountConnectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_AccountConnectionProjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CrmHr_AccountConnectionProjects_CrmHr_AccountStakeholders_A~",
                        column: x => x.AccountConnectionId,
                        principalTable: "CrmHr_AccountStakeholders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CrmHr_AccountConnectionProjects_Projects_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects_Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_AccountConnectionProjects_AccountConnectionId_Project~",
                table: "CrmHr_AccountConnectionProjects",
                columns: new[] { "AccountConnectionId", "ProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_AccountConnectionProjects_ProjectId_AccountConnection~",
                table: "CrmHr_AccountConnectionProjects",
                columns: new[] { "ProjectId", "AccountConnectionId" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CrmHr_AccountConnectionProjects");
        }
    }
}
