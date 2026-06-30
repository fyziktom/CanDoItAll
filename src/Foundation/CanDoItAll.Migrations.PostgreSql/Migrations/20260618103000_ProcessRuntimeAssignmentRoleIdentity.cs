using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260618103000_ProcessRuntimeAssignmentRoleIdentity")]
    public partial class ProcessRuntimeAssignmentRoleIdentity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RoleDisplayName",
                table: "process_runtime_step_assignments",
                type: "character varying(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RoleResourceKey",
                table: "process_runtime_step_assignments",
                type: "character varying(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RoleDisplayName",
                table: "process_runtime_step_assignments");

            migrationBuilder.DropColumn(
                name: "RoleResourceKey",
                table: "process_runtime_step_assignments");
        }
    }
}
