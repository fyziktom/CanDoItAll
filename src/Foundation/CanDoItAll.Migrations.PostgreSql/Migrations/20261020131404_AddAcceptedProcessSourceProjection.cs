using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddAcceptedProcessSourceProjection : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.AddColumn<string>(
                name: "AcceptedSourceHeaderFingerprint",
                table: "process_prepared_launches",
                type: "character varying(71)",
                maxLength: 71,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcceptedSourceHeaderJson",
                table: "process_prepared_launches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AcceptedSourceVersion",
                table: "process_prepared_launches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "process_accepted_assignment_sources",
                columns: table => new {
                    AdmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StepInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    Fingerprint = table.Column<string>(type: "character varying(71)", maxLength: 71, nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_process_accepted_assignment_sources", x => new { x.AdmissionId, x.StepInstanceId });
                    table.CheckConstraint("CK_process_accepted_assignment_source", "\"StepInstanceId\" <> '00000000-0000-0000-0000-000000000000'::uuid\nAND octet_length(\"PayloadJson\") BETWEEN 2 AND 1048576\nAND \"Fingerprint\" ~ '^sha256:[0-9a-f]{64}$'");
                    table.ForeignKey(
                        name: "FK_process_accepted_assignment_sources_process_prepared_launch~",
                        column: x => x.AdmissionId,
                        principalTable: "process_prepared_launches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_process_prepared_launch_source",
                table: "process_prepared_launches",
                sql: "(\"AcceptedSourceVersion\" = 0 AND \"AcceptedSourceHeaderJson\" IS NULL AND \"AcceptedSourceHeaderFingerprint\" IS NULL)\nOR (\"AcceptedSourceVersion\" = 1 AND \"AcceptedAtUtc\" IS NOT NULL\n    AND \"AcceptedSourceHeaderJson\" IS NOT NULL AND octet_length(\"AcceptedSourceHeaderJson\") BETWEEN 2 AND 1048576\n    AND \"AcceptedSourceHeaderFingerprint\" IS NOT NULL AND \"AcceptedSourceHeaderFingerprint\" ~ '^sha256:[0-9a-f]{64}$')");
        }
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                DO $guard$
                BEGIN
                    IF EXISTS (SELECT 1 FROM process_prepared_launches WHERE "AcceptedSourceVersion" <> 0)
                        OR EXISTS (SELECT 1 FROM process_accepted_assignment_sources) THEN
                        RAISE EXCEPTION 'Cannot downgrade while retained accepted Process source evidence exists.';
                    END IF;
                END
                $guard$;
                """);

            migrationBuilder.DropTable(
                name: "process_accepted_assignment_sources");

            migrationBuilder.DropCheckConstraint(
                name: "CK_process_prepared_launch_source",
                table: "process_prepared_launches");

            migrationBuilder.DropColumn(
                name: "AcceptedSourceHeaderFingerprint",
                table: "process_prepared_launches");

            migrationBuilder.DropColumn(
                name: "AcceptedSourceHeaderJson",
                table: "process_prepared_launches");

            migrationBuilder.DropColumn(
                name: "AcceptedSourceVersion",
                table: "process_prepared_launches");
        }
    }
}
