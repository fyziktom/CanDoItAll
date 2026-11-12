using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddAcceptedProcessStepRequirements : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                ALTER TABLE process_accepted_assignment_sources
                    DROP CONSTRAINT "CK_process_accepted_execution_input",
                    ADD CONSTRAINT "CK_process_accepted_execution_input" CHECK (
                        ("InputVersion" = 0 AND "InputJson" IS NULL AND "InputFingerprint" IS NULL)
                        OR ("InputVersion" IN (1, 2) AND "InputJson" IS NOT NULL
                            AND octet_length("InputJson") BETWEEN 2 AND 4194304
                            AND "InputFingerprint" IS NOT NULL AND "InputFingerprint" ~ '^sha256:[0-9a-f]{64}$')
                    ) NOT VALID;
                """);
            migrationBuilder.Sql("""
                ALTER TABLE process_accepted_assignment_sources
                    VALIDATE CONSTRAINT "CK_process_accepted_execution_input";
                """, suppressTransaction: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM process_accepted_assignment_sources WHERE "InputVersion" = 2) THEN
                        RAISE EXCEPTION 'Cannot downgrade while retained original Process step requirements exist.';
                    END IF;
                END
                $guard$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_process_accepted_execution_input",
                table: "process_accepted_assignment_sources");

            migrationBuilder.AddCheckConstraint(
                name: "CK_process_accepted_execution_input",
                table: "process_accepted_assignment_sources",
                sql: "(\"InputVersion\" = 0 AND \"InputJson\" IS NULL AND \"InputFingerprint\" IS NULL)\nOR (\"InputVersion\" = 1 AND \"InputJson\" IS NOT NULL\n    AND octet_length(\"InputJson\") BETWEEN 2 AND 4194304\n    AND \"InputFingerprint\" IS NOT NULL AND \"InputFingerprint\" ~ '^sha256:[0-9a-f]{64}$')");
        }
    }
}
