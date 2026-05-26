using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    public partial class ProcessDefinitionContractMode : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContractMode",
                table: "Processes_DefinitionVersions",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Compatibility");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContractMode",
                table: "Processes_DefinitionVersions");
        }
    }
}
