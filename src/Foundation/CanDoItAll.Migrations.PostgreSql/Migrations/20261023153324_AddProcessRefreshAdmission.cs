using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddProcessRefreshAdmission : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("SET LOCAL lock_timeout = '5s';");
            migrationBuilder.AddColumn<Guid>(
                name: "RefreshCommandId",
                table: "process_decision_generations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_process_decision_generations_refresh",
                table: "process_decision_generations",
                sql: "\"RefreshCommandId\" IS NULL OR (\"RefreshCommandId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"Phase\" >= 2)");
        }

        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                SET LOCAL lock_timeout = '5s';
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM process_decision_generations WHERE "RefreshCommandId" IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM process_runtime_idempotency_keys
                            WHERE "DecisionReceiptJson" IS NOT NULL AND ("DecisionReceiptJson"::jsonb->>'version')::int >= 7) THEN
                        RAISE EXCEPTION 'Cannot downgrade retained Process refresh admission evidence.';
                    END IF;
                END $guard$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_process_decision_generations_refresh",
                table: "process_decision_generations");

            migrationBuilder.DropColumn(
                name: "RefreshCommandId",
                table: "process_decision_generations");
        }
    }
}
