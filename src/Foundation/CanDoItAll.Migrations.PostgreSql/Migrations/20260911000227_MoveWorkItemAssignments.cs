using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class MoveWorkItemAssignments : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.CreateTable(
                name: "Workbench_WorkAssignments",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PartyOrganizationAffiliationId = table.Column<Guid>(type: "uuid", nullable: true),
                    NodeKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PhaseName = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    OpportunityId = table.Column<Guid>(type: "uuid", nullable: true),
                    AllocationPercent = table.Column<decimal>(type: "numeric", nullable: true),
                    StartsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndsAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_Workbench_WorkAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workbench_WorkAssignments_CrmHr_PartyOrganizationAffiliatio~",
                        column: x => x.PartyOrganizationAffiliationId,
                        principalTable: "CrmHr_PartyOrganizationAffiliations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_WorkAssignments_OpportunityId",
                table: "Workbench_WorkAssignments",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_WorkAssignments_PartyId",
                table: "Workbench_WorkAssignments",
                column: "PartyId");

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_WorkAssignments_PartyOrganizationAffiliationId",
                table: "Workbench_WorkAssignments",
                column: "PartyOrganizationAffiliationId");

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_WorkAssignments_ProjectId",
                table: "Workbench_WorkAssignments",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_WorkAssignments_ProjectId_NodeKey",
                table: "Workbench_WorkAssignments",
                columns: new[] { "ProjectId", "NodeKey" });

            migrationBuilder.CreateIndex(
                name: "IX_Workbench_WorkAssignments_ProjectId_PartyId_NodeKey",
                table: "Workbench_WorkAssignments",
                columns: new[] { "ProjectId", "PartyId", "NodeKey" });
            migrationBuilder.Sql("""
                LOCK TABLE "CrmHr_ProjectPartyAssignments", "Workbench_WorkAssignments" IN ACCESS EXCLUSIVE MODE;
                DO $migration$
                DECLARE expected_rows bigint;
                DECLARE copied_rows bigint;
                DECLARE deleted_rows bigint;
                BEGIN
                    SELECT count(*) INTO expected_rows FROM "CrmHr_ProjectPartyAssignments" WHERE "AssignmentKind" = 'WorkItemAssignee';
                    INSERT INTO "Workbench_WorkAssignments" ("Id", "ProjectId", "PartyId", "PartyOrganizationAffiliationId", "NodeKey", "PhaseName", "OpportunityId", "AllocationPercent", "StartsAtUtc", "EndsAtUtc", "IsPrimary", "Source", "Notes")
                    SELECT "Id", "ProjectId", "PartyId", "PartyOrganizationAffiliationId", "NodeKey", "PhaseName", "OpportunityId", "AllocationPercent", "StartsAtUtc", "EndsAtUtc", "IsPrimary", "Source", "Notes" FROM "CrmHr_ProjectPartyAssignments" WHERE "AssignmentKind" = 'WorkItemAssignee';
                    GET DIAGNOSTICS copied_rows = ROW_COUNT;
                    IF copied_rows <> expected_rows OR (SELECT count(*) FROM "Workbench_WorkAssignments") <> expected_rows THEN
                        RAISE EXCEPTION 'Work assignment copy did not preserve the original row count.';
                    END IF;
                    IF EXISTS (
                        SELECT "Id", "ProjectId", "PartyId", "PartyOrganizationAffiliationId", "NodeKey", "PhaseName", "OpportunityId", "AllocationPercent", "StartsAtUtc", "EndsAtUtc", "IsPrimary", "Source", "Notes" FROM "CrmHr_ProjectPartyAssignments" WHERE "AssignmentKind" = 'WorkItemAssignee'
                        EXCEPT SELECT "Id", "ProjectId", "PartyId", "PartyOrganizationAffiliationId", "NodeKey", "PhaseName", "OpportunityId", "AllocationPercent", "StartsAtUtc", "EndsAtUtc", "IsPrimary", "Source", "Notes" FROM "Workbench_WorkAssignments"
                    ) THEN
                        RAISE EXCEPTION 'Work assignment copy did not preserve every original identity and field.';
                    END IF;
                    DELETE FROM "CrmHr_ProjectPartyAssignments" WHERE "AssignmentKind" = 'WorkItemAssignee';
                    GET DIAGNOSTICS deleted_rows = ROW_COUNT;
                    IF deleted_rows <> expected_rows THEN
                        RAISE EXCEPTION 'Work assignment cutover did not remove precisely the copied source rows.';
                    END IF;
                END $migration$;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "CK_CrmHr_ProjectPartyAssignments_ParticipationRole",
                table: "CrmHr_ProjectPartyAssignments",
                sql: "\"AssignmentKind\" <> 'WorkItemAssignee'");
        }

        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                LOCK TABLE "CrmHr_ProjectPartyAssignments", "Workbench_WorkAssignments" IN ACCESS EXCLUSIVE MODE;
                DO $migration$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM "Workbench_WorkAssignments" work
                        JOIN "CrmHr_ProjectPartyAssignments" participation ON participation."Id" = work."Id"
                    ) THEN
                        RAISE EXCEPTION 'Cannot restore Work assignments: a CRM participation row already uses an assignment identity.';
                    END IF;
                END $migration$;
                """);

            migrationBuilder.DropCheckConstraint(
                name: "CK_CrmHr_ProjectPartyAssignments_ParticipationRole",
                table: "CrmHr_ProjectPartyAssignments");

            migrationBuilder.Sql("""
                DO $migration$
                DECLARE expected_rows bigint;
                DECLARE copied_rows bigint;
                BEGIN
                    SELECT count(*) INTO expected_rows FROM "Workbench_WorkAssignments";
                    INSERT INTO "CrmHr_ProjectPartyAssignments" ("Id", "ProjectId", "PartyId", "PartyOrganizationAffiliationId", "NodeKey", "PhaseName", "OpportunityId", "AllocationPercent", "StartsAtUtc", "EndsAtUtc", "IsPrimary", "Source", "Notes", "AssignmentKind")
                    SELECT "Id", "ProjectId", "PartyId", "PartyOrganizationAffiliationId", "NodeKey", "PhaseName", "OpportunityId", "AllocationPercent", "StartsAtUtc", "EndsAtUtc", "IsPrimary", "Source", "Notes", 'WorkItemAssignee' FROM "Workbench_WorkAssignments";
                    GET DIAGNOSTICS copied_rows = ROW_COUNT;
                    IF copied_rows <> expected_rows THEN
                        RAISE EXCEPTION 'Restoring Work assignments did not preserve the row count.';
                    END IF;
                    IF EXISTS (
                        SELECT "Id", "ProjectId", "PartyId", "PartyOrganizationAffiliationId", "NodeKey", "PhaseName", "OpportunityId", "AllocationPercent", "StartsAtUtc", "EndsAtUtc", "IsPrimary", "Source", "Notes" FROM "Workbench_WorkAssignments"
                        EXCEPT SELECT "Id", "ProjectId", "PartyId", "PartyOrganizationAffiliationId", "NodeKey", "PhaseName", "OpportunityId", "AllocationPercent", "StartsAtUtc", "EndsAtUtc", "IsPrimary", "Source", "Notes" FROM "CrmHr_ProjectPartyAssignments" WHERE "AssignmentKind" = 'WorkItemAssignee'
                    ) THEN
                        RAISE EXCEPTION 'Restoring Work assignments did not preserve every identity and field.';
                    END IF;
                END $migration$;
                """);

            migrationBuilder.DropTable(
                name: "Workbench_WorkAssignments");
        }
    }
}
