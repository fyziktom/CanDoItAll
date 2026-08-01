using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddPartyOrganizationAffiliations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CrmHr_WorkforceProfiles_PartyId",
                table: "CrmHr_WorkforceProfiles");

            migrationBuilder.AddColumn<Guid>(
                name: "PartyOrganizationAffiliationId",
                table: "CrmHr_ProjectPartyAssignments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CrmHr_PartyOrganizationAffiliations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PersonPartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationPartyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AffiliationKind = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    JobTitle = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    EmployeeCode = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    OrganizationUnitPartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    ManagerPartyId = table.Column<Guid>(type: "uuid", nullable: true),
                    ValidFromUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ValidToUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: false),
                    LastChangedBy = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CrmHr_PartyOrganizationAffiliations", x => x.Id);
                    table.CheckConstraint("CK_CrmHr_PartyOrganizationAffiliations_PersonManager", "\"ManagerPartyId\" IS NULL OR \"PersonPartyId\" <> \"ManagerPartyId\"");
                    table.CheckConstraint("CK_CrmHr_PartyOrganizationAffiliations_PersonOrganization", "\"PersonPartyId\" <> \"OrganizationPartyId\"");
                    table.CheckConstraint("CK_CrmHr_PartyOrganizationAffiliations_PersonUnit", "\"OrganizationUnitPartyId\" IS NULL OR \"PersonPartyId\" <> \"OrganizationUnitPartyId\"");
                    table.CheckConstraint("CK_CrmHr_PartyOrganizationAffiliations_ValidDates", "\"ValidToUtc\" IS NULL OR \"ValidFromUtc\" IS NULL OR \"ValidToUtc\" >= \"ValidFromUtc\"");
                    table.ForeignKey(
                        name: "FK_CrmHr_PartyOrganizationAffiliations_CrmHr_Parties_ManagerPa~",
                        column: x => x.ManagerPartyId,
                        principalTable: "CrmHr_Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CrmHr_PartyOrganizationAffiliations_CrmHr_Parties_Organizat~",
                        column: x => x.OrganizationPartyId,
                        principalTable: "CrmHr_Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CrmHr_PartyOrganizationAffiliations_CrmHr_Parties_Organiza~1",
                        column: x => x.OrganizationUnitPartyId,
                        principalTable: "CrmHr_Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CrmHr_PartyOrganizationAffiliations_CrmHr_Parties_PersonPar~",
                        column: x => x.PersonPartyId,
                        principalTable: "CrmHr_Parties",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql(
                """
                WITH home_organization_candidates AS
                (
                    SELECT
                        profile."Id" AS "ProfileId",
                        home_organization."Id" AS "OrganizationPartyId",
                        NULL::uuid AS "OrganizationUnitPartyId"
                    FROM "CrmHr_WorkforceProfiles" AS profile
                    INNER JOIN "CrmHr_Parties" AS home_organization
                        ON home_organization."Id" = profile."HomeUnitPartyId"
                       AND home_organization."PartyType" = 'Organization'

                    UNION

                    SELECT
                        profile."Id" AS "ProfileId",
                        organization."Id" AS "OrganizationPartyId",
                        organization_unit."Id" AS "OrganizationUnitPartyId"
                    FROM "CrmHr_WorkforceProfiles" AS profile
                    INNER JOIN "CrmHr_Parties" AS organization_unit
                        ON organization_unit."Id" = profile."HomeUnitPartyId"
                       AND organization_unit."PartyType" = 'OrganizationUnit'
                    INNER JOIN "CrmHr_PartyRelationships" AS relationship
                        ON relationship."RelationshipKind" = 'PartOf'
                       AND
                       (
                           relationship."SourcePartyId" = organization_unit."Id"
                           OR relationship."TargetPartyId" = organization_unit."Id"
                       )
                    INNER JOIN "CrmHr_Parties" AS organization
                        ON organization."PartyType" = 'Organization'
                       AND
                       (
                           (
                               relationship."SourcePartyId" = organization_unit."Id"
                               AND relationship."TargetPartyId" = organization."Id"
                           )
                           OR
                           (
                               relationship."TargetPartyId" = organization_unit."Id"
                               AND relationship."SourcePartyId" = organization."Id"
                           )
                       )
                ),
                resolved_profile_organizations AS
                (
                    SELECT candidate.*
                    FROM home_organization_candidates AS candidate
                    WHERE NOT EXISTS
                    (
                        SELECT 1
                        FROM home_organization_candidates AS other
                        WHERE other."ProfileId" = candidate."ProfileId"
                          AND other."OrganizationPartyId" <> candidate."OrganizationPartyId"
                    )
                ),
                membership_candidates AS
                (
                    SELECT
                        relationship."Id" AS "RelationshipId",
                        CASE
                            WHEN source_party."PartyType" = 'Person'
                            THEN source_party."Id"
                            ELSE target_party."Id"
                        END AS "PersonPartyId",
                        CASE
                            WHEN source_party."PartyType" = 'Organization'
                            THEN source_party."Id"
                            ELSE target_party."Id"
                        END AS "OrganizationPartyId",
                        relationship."StartDateUtc",
                        relationship."EndDateUtc",
                        relationship."Notes"
                    FROM "CrmHr_PartyRelationships" AS relationship
                    INNER JOIN "CrmHr_Parties" AS source_party
                        ON source_party."Id" = relationship."SourcePartyId"
                    INNER JOIN "CrmHr_Parties" AS target_party
                        ON target_party."Id" = relationship."TargetPartyId"
                    WHERE relationship."RelationshipKind" = 'MemberOf'
                      AND
                      (
                          (
                              source_party."PartyType" = 'Person'
                              AND target_party."PartyType" = 'Organization'
                          )
                          OR
                          (
                              target_party."PartyType" = 'Person'
                              AND source_party."PartyType" = 'Organization'
                          )
                      )
                ),
                single_memberships AS
                (
                    SELECT membership.*
                    FROM membership_candidates AS membership
                    WHERE NOT EXISTS
                    (
                        SELECT 1
                        FROM membership_candidates AS other
                        WHERE other."PersonPartyId" = membership."PersonPartyId"
                          AND other."RelationshipId" <> membership."RelationshipId"
                    )
                ),
                compatible_roles AS
                (
                    SELECT role.*
                    FROM "CrmHr_PartyRoles" AS role
                    WHERE role."RoleKind" IN
                        ('Employee', 'Contractor', 'Freelancer')
                ),
                single_compatible_roles AS
                (
                    SELECT role.*
                    FROM compatible_roles AS role
                    WHERE NOT EXISTS
                    (
                        SELECT 1
                        FROM compatible_roles AS other
                        WHERE other."PartyId" = role."PartyId"
                          AND other."Id" <> role."Id"
                    )
                ),
                affiliation_candidates AS
                (
                    SELECT
                        profile."PartyId" AS "PersonPartyId",
                        resolved."OrganizationPartyId",
                        profile."WorkforceKind" AS "AffiliationKind",
                        profile."JobTitle",
                        profile."EmployeeCode",
                        resolved."OrganizationUnitPartyId",
                        CASE
                            WHEN manager."PartyType" = 'Person'
                            THEN profile."ManagerPartyId"
                            ELSE NULL
                        END AS "ManagerPartyId",
                        profile."StartDateUtc" AS "ValidFromUtc",
                        profile."EndDateUtc" AS "ValidToUtc",
                        profile."Notes"
                    FROM "CrmHr_WorkforceProfiles" AS profile
                    INNER JOIN "CrmHr_Parties" AS person
                        ON person."Id" = profile."PartyId"
                       AND person."PartyType" = 'Person'
                    INNER JOIN resolved_profile_organizations AS resolved
                        ON resolved."ProfileId" = profile."Id"
                    LEFT JOIN "CrmHr_Parties" AS manager
                        ON manager."Id" = profile."ManagerPartyId"
                    WHERE profile."WorkforceKind" IN
                        ('Employee', 'Contractor', 'Freelancer')

                    UNION ALL

                    SELECT
                        membership."PersonPartyId",
                        membership."OrganizationPartyId",
                        role."RoleKind" AS "AffiliationKind",
                        COALESCE(NULLIF(profile."JobTitle", ''), role."Title"),
                        COALESCE(profile."EmployeeCode", ''),
                        NULL::uuid AS "OrganizationUnitPartyId",
                        CASE
                            WHEN manager."PartyType" = 'Person'
                            THEN profile."ManagerPartyId"
                            ELSE NULL
                        END AS "ManagerPartyId",
                        membership."StartDateUtc" AS "ValidFromUtc",
                        membership."EndDateUtc" AS "ValidToUtc",
                        membership."Notes"
                    FROM single_memberships AS membership
                    INNER JOIN single_compatible_roles AS role
                        ON role."PartyId" = membership."PersonPartyId"
                    LEFT JOIN "CrmHr_WorkforceProfiles" AS profile
                        ON profile."PartyId" = membership."PersonPartyId"
                    LEFT JOIN "CrmHr_Parties" AS manager
                        ON manager."Id" = profile."ManagerPartyId"
                    WHERE NOT EXISTS
                    (
                        SELECT 1
                        FROM "CrmHr_WorkforceProfiles" AS resolved_profile
                        INNER JOIN resolved_profile_organizations AS resolved
                            ON resolved."ProfileId" = resolved_profile."Id"
                        WHERE resolved_profile."PartyId" =
                            membership."PersonPartyId"
                    )
                )
                INSERT INTO "CrmHr_PartyOrganizationAffiliations"
                (
                    "Id",
                    "PersonPartyId",
                    "OrganizationPartyId",
                    "AffiliationKind",
                    "IsPrimary",
                    "JobTitle",
                    "EmployeeCode",
                    "OrganizationUnitPartyId",
                    "ManagerPartyId",
                    "ValidFromUtc",
                    "ValidToUtc",
                    "Notes",
                    "LastChangedBy",
                    "CreatedAtUtc",
                    "UpdatedAtUtc"
                )
                SELECT
                    gen_random_uuid(),
                    candidate."PersonPartyId",
                    candidate."OrganizationPartyId",
                    candidate."AffiliationKind",
                    (
                        (
                            candidate."ValidFromUtc" IS NULL
                            OR candidate."ValidFromUtc" <=
                                date_trunc('day', CURRENT_TIMESTAMP)
                        )
                        AND
                        (
                            candidate."ValidToUtc" IS NULL
                            OR candidate."ValidToUtc" >=
                                date_trunc('day', CURRENT_TIMESTAMP)
                        )
                    ),
                    candidate."JobTitle",
                    candidate."EmployeeCode",
                    candidate."OrganizationUnitPartyId",
                    candidate."ManagerPartyId",
                    candidate."ValidFromUtc",
                    candidate."ValidToUtc",
                    candidate."Notes",
                    'crm-hr-affiliation-migration',
                    CURRENT_TIMESTAMP,
                    CURRENT_TIMESTAMP
                FROM affiliation_candidates AS candidate;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_WorkforceProfiles_PartyId",
                table: "CrmHr_WorkforceProfiles",
                column: "PartyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_ProjectPartyAssignments_PartyOrganizationAffiliationId",
                table: "CrmHr_ProjectPartyAssignments",
                column: "PartyOrganizationAffiliationId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_PartyOrganizationAffiliations_ManagerPartyId",
                table: "CrmHr_PartyOrganizationAffiliations",
                column: "ManagerPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_PartyOrganizationAffiliations_OrganizationPartyId",
                table: "CrmHr_PartyOrganizationAffiliations",
                column: "OrganizationPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_PartyOrganizationAffiliations_OrganizationUnitPartyId",
                table: "CrmHr_PartyOrganizationAffiliations",
                column: "OrganizationUnitPartyId");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_PartyOrganizationAffiliations_PersonPartyId_ValidFrom~",
                table: "CrmHr_PartyOrganizationAffiliations",
                columns: new[] { "PersonPartyId", "ValidFromUtc", "ValidToUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_CrmHr_Affiliations_BusinessKey",
                table: "CrmHr_PartyOrganizationAffiliations",
                columns: new[] { "PersonPartyId", "OrganizationPartyId", "AffiliationKind", "ValidFromUtc", "ValidToUtc" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "UX_CrmHr_Affiliations_PrimaryPerson",
                table: "CrmHr_PartyOrganizationAffiliations",
                column: "PersonPartyId",
                unique: true,
                filter: "\"IsPrimary\" = TRUE");

            migrationBuilder.AddForeignKey(
                name: "FK_CrmHr_ProjectPartyAssignments_CrmHr_PartyOrganizationAffili~",
                table: "CrmHr_ProjectPartyAssignments",
                column: "PartyOrganizationAffiliationId",
                principalTable: "CrmHr_PartyOrganizationAffiliations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CrmHr_ProjectPartyAssignments_CrmHr_PartyOrganizationAffili~",
                table: "CrmHr_ProjectPartyAssignments");

            migrationBuilder.DropTable(
                name: "CrmHr_PartyOrganizationAffiliations");

            migrationBuilder.DropIndex(
                name: "IX_CrmHr_WorkforceProfiles_PartyId",
                table: "CrmHr_WorkforceProfiles");

            migrationBuilder.DropIndex(
                name: "IX_CrmHr_ProjectPartyAssignments_PartyOrganizationAffiliationId",
                table: "CrmHr_ProjectPartyAssignments");

            migrationBuilder.DropColumn(
                name: "PartyOrganizationAffiliationId",
                table: "CrmHr_ProjectPartyAssignments");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_WorkforceProfiles_PartyId",
                table: "CrmHr_WorkforceProfiles",
                column: "PartyId");
        }
    }
}
