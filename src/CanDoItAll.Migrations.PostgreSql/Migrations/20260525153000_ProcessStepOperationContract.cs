using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260525153000_ProcessStepOperationContract")]
    public partial class ProcessStepOperationContract : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllowedOperations",
                table: "Processes_StepDefinitions",
                type: "TEXT",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "OperationTargetScope",
                table: "Processes_StepDefinitions",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowedOperations",
                table: "Processes_StepDefinitions");

            migrationBuilder.DropColumn(
                name: "OperationTargetScope",
                table: "Processes_StepDefinitions");
        }
    }
}
