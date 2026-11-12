using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations;

public partial class ProtectWorkflowComponentObservations : Migration {
    protected override void Up(MigrationBuilder migrationBuilder) {
        migrationBuilder.AddColumn<Guid>(name: "Revision", table: "AgentFramework_WorkflowComponents",
            type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()");
    }

    protected override void Down(MigrationBuilder migrationBuilder) {
        migrationBuilder.DropColumn(name: "Revision", table: "AgentFramework_WorkflowComponents");
    }
}
