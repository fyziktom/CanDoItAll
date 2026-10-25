using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddInheritedTaskPlanReceiptIdentity : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Workbench_TaskPlanReceipt_Caller",
                table: "Workbench_TaskPlanReceipts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Workbench_TaskPlanReceipt_Interpretation",
                table: "Workbench_TaskPlanReceipts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Workbench_TaskPlanReceipt_Caller",
                table: "Workbench_TaskPlanReceipts",
                sql: "\"CallerKind\" IN (1, 2) AND octet_length(\"CallerKey\") = 32");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Workbench_TaskPlanReceipt_Interpretation",
                table: "Workbench_TaskPlanReceipts",
                sql: "\"SchemaVersion\" > 0 AND \"NormalizationVersion\" > 0 AND \"ResultVersion\" > 0 AND \"SourceVersion\" > 0 AND \"SourceKind\" = \"CallerKind\" AND (\"CallerKind\" = 1 OR (\"SchemaVersion\" = 2 AND \"NormalizationVersion\" = 1 AND \"ResultVersion\" = 2 AND \"SourceVersion\" = 2))");
        }

        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                LOCK TABLE "Workbench_TaskPlanReceipts" IN ACCESS EXCLUSIVE MODE;
                DO $receipt$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Workbench_TaskPlanReceipts" WHERE "CallerKind" <> 1 OR "SourceKind" <> 1) THEN
                        RAISE EXCEPTION 'Cannot downgrade retained inherited task-plan receipts.';
                    END IF;
                END $receipt$;
                """);
            migrationBuilder.DropCheckConstraint(
                name: "CK_Workbench_TaskPlanReceipt_Caller",
                table: "Workbench_TaskPlanReceipts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Workbench_TaskPlanReceipt_Interpretation",
                table: "Workbench_TaskPlanReceipts");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Workbench_TaskPlanReceipt_Caller",
                table: "Workbench_TaskPlanReceipts",
                sql: "\"CallerKind\" = 1 AND octet_length(\"CallerKey\") = 32");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Workbench_TaskPlanReceipt_Interpretation",
                table: "Workbench_TaskPlanReceipts",
                sql: "\"SchemaVersion\" > 0 AND \"NormalizationVersion\" > 0 AND \"ResultVersion\" > 0 AND \"SourceVersion\" > 0 AND \"SourceKind\" = 1");
        }
    }
}
