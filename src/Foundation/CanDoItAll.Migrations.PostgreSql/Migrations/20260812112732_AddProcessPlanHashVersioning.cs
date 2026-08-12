using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessPlanHashVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExecutionState",
                table: "process_instance_plans",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MigrationReason",
                table: "process_instance_plans",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlanHashAlgorithmVersion",
                table: "process_instance_plans",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE process_instance_plans
                SET "PlanHashAlgorithmVersion" = 'LegacyV1',
                    "ExecutionState" = 'NeedsRecompile',
                    "MigrationReason" = 'HostCapabilitiesWereNotSealed'
                WHERE "CreatedAtUtc" < TIMESTAMPTZ '2026-08-11 18:53:52+00'
                  AND "PayloadJson" NOT LIKE '%"hostProfileId"%'
                  AND "PayloadJson" NOT LIKE '%"hostCapabilities"%'
                  AND "PayloadJson" NOT LIKE '%"requiredHostCapabilities"%'
                  AND "PayloadJson" NOT LIKE '%"requiredRuntimeToolNames"%';

                UPDATE process_instance_plans
                SET "PlanHashAlgorithmVersion" = 'HostCapabilitiesV2',
                    "ExecutionState" = 'Executable',
                    "MigrationReason" = NULL
                WHERE "PayloadJson" LIKE '%"hostProfileId"%'
                  AND "PayloadJson" LIKE '%"hostCapabilities"%'
                  AND "PayloadJson" LIKE '%"requiredHostCapabilities"%'
                  AND "PayloadJson" LIKE '%"requiredRuntimeToolNames"%';

                UPDATE process_instance_plans
                SET "ExecutionState" = 'Unknown'
                WHERE "ExecutionState" IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "ExecutionState",
                table: "process_instance_plans",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExecutionState",
                table: "process_instance_plans");

            migrationBuilder.DropColumn(
                name: "MigrationReason",
                table: "process_instance_plans");

            migrationBuilder.DropColumn(
                name: "PlanHashAlgorithmVersion",
                table: "process_instance_plans");
        }
    }
}
