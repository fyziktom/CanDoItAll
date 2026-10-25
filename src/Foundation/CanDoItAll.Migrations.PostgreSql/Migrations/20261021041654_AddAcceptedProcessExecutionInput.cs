using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddAcceptedProcessExecutionInput : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<string>(
                name: "InputFingerprint",
                table: "process_accepted_assignment_sources",
                type: "character varying(71)",
                maxLength: 71,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InputJson",
                table: "process_accepted_assignment_sources",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InputVersion",
                table: "process_accepted_assignment_sources",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_process_accepted_execution_input",
                table: "process_accepted_assignment_sources",
                sql: "(\"InputVersion\" = 0 AND \"InputJson\" IS NULL AND \"InputFingerprint\" IS NULL)\nOR (\"InputVersion\" = 1 AND \"InputJson\" IS NOT NULL\n    AND octet_length(\"InputJson\") BETWEEN 2 AND 4194304\n    AND \"InputFingerprint\" IS NOT NULL AND \"InputFingerprint\" ~ '^sha256:[0-9a-f]{64}$')");
        }
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM process_accepted_assignment_sources WHERE "InputVersion" <> 0) THEN
                        RAISE EXCEPTION 'Cannot downgrade while retained original Process execution input exists.';
                    END IF;
                END
                $guard$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_process_accepted_execution_input",
                table: "process_accepted_assignment_sources");

            migrationBuilder.DropColumn(
                name: "InputFingerprint",
                table: "process_accepted_assignment_sources");

            migrationBuilder.DropColumn(
                name: "InputJson",
                table: "process_accepted_assignment_sources");

            migrationBuilder.DropColumn(
                name: "InputVersion",
                table: "process_accepted_assignment_sources");
        }
    }
}
