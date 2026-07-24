using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeCrmHrHighCardinalityQueries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CrmHr_AuditEntries_EntityType_EntityId",
                table: "CrmHr_AuditEntries");

            migrationBuilder.AddColumn<string>(
                name: "ProjectedCapabilitiesJson",
                table: "CrmHr_AiResourceBindings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "ProjectedCapabilityCount",
                table: "CrmHr_AiResourceBindings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ProjectedDefaultModel",
                table: "CrmHr_AiResourceBindings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProjectedExecutionMode",
                table: "CrmHr_AiResourceBindings",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectedInstructions",
                table: "CrmHr_AiResourceBindings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProjectedProviderName",
                table: "CrmHr_AiResourceBindings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProjectedRoleTitle",
                table: "CrmHr_AiResourceBindings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProjectedTagsJson",
                table: "CrmHr_AiResourceBindings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProjectedTemplateKey",
                table: "CrmHr_AiResourceBindings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProjectionUpdatedAtUtc",
                table: "CrmHr_AiResourceBindings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedDisplayName",
                table: "CrmHr_Parties",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                computedColumnSql: "regexp_replace(lower(trim(\"DisplayName\")), '[^[:alnum:]]', '', 'g')",
                stored: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedExternalCode",
                table: "CrmHr_Parties",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                computedColumnSql: "lower(trim(\"ExternalCode\"))",
                stored: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedLegalName",
                table: "CrmHr_Parties",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                computedColumnSql: "regexp_replace(lower(trim(\"LegalName\")), '[^[:alnum:]]', '', 'g')",
                stored: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedPreferredName",
                table: "CrmHr_Parties",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                computedColumnSql: "regexp_replace(lower(trim(\"PreferredName\")), '[^[:alnum:]]', '', 'g')",
                stored: true);

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Parties_NormalizedDisplayName",
                table: "CrmHr_Parties",
                column: "NormalizedDisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Parties_NormalizedExternalCode",
                table: "CrmHr_Parties",
                column: "NormalizedExternalCode");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Parties_NormalizedLegalName",
                table: "CrmHr_Parties",
                column: "NormalizedLegalName");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_Parties_NormalizedPreferredName",
                table: "CrmHr_Parties",
                column: "NormalizedPreferredName");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_InteractionParties_PartyId_Role_InteractionId",
                table: "CrmHr_InteractionParties",
                columns: new[] { "PartyId", "Role", "InteractionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_AuditEntries_EntityType_EntityId_CreatedAtUtc_Id",
                table: "CrmHr_AuditEntries",
                columns: new[] { "EntityType", "EntityId", "CreatedAtUtc", "Id" },
                descending: new[] { false, false, true, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CrmHr_Parties_NormalizedDisplayName",
                table: "CrmHr_Parties");

            migrationBuilder.DropIndex(
                name: "IX_CrmHr_Parties_NormalizedExternalCode",
                table: "CrmHr_Parties");

            migrationBuilder.DropIndex(
                name: "IX_CrmHr_Parties_NormalizedLegalName",
                table: "CrmHr_Parties");

            migrationBuilder.DropIndex(
                name: "IX_CrmHr_Parties_NormalizedPreferredName",
                table: "CrmHr_Parties");

            migrationBuilder.DropIndex(
                name: "IX_CrmHr_InteractionParties_PartyId_Role_InteractionId",
                table: "CrmHr_InteractionParties");

            migrationBuilder.DropIndex(
                name: "IX_CrmHr_AuditEntries_EntityType_EntityId_CreatedAtUtc_Id",
                table: "CrmHr_AuditEntries");

            migrationBuilder.DropColumn(
                name: "NormalizedDisplayName",
                table: "CrmHr_Parties");

            migrationBuilder.DropColumn(
                name: "NormalizedExternalCode",
                table: "CrmHr_Parties");

            migrationBuilder.DropColumn(
                name: "NormalizedLegalName",
                table: "CrmHr_Parties");

            migrationBuilder.DropColumn(
                name: "NormalizedPreferredName",
                table: "CrmHr_Parties");

            migrationBuilder.DropColumn(
                name: "ProjectedCapabilitiesJson",
                table: "CrmHr_AiResourceBindings");

            migrationBuilder.DropColumn(
                name: "ProjectedCapabilityCount",
                table: "CrmHr_AiResourceBindings");

            migrationBuilder.DropColumn(
                name: "ProjectedDefaultModel",
                table: "CrmHr_AiResourceBindings");

            migrationBuilder.DropColumn(
                name: "ProjectedExecutionMode",
                table: "CrmHr_AiResourceBindings");

            migrationBuilder.DropColumn(
                name: "ProjectedInstructions",
                table: "CrmHr_AiResourceBindings");

            migrationBuilder.DropColumn(
                name: "ProjectedProviderName",
                table: "CrmHr_AiResourceBindings");

            migrationBuilder.DropColumn(
                name: "ProjectedRoleTitle",
                table: "CrmHr_AiResourceBindings");

            migrationBuilder.DropColumn(
                name: "ProjectedTagsJson",
                table: "CrmHr_AiResourceBindings");

            migrationBuilder.DropColumn(
                name: "ProjectedTemplateKey",
                table: "CrmHr_AiResourceBindings");

            migrationBuilder.DropColumn(
                name: "ProjectionUpdatedAtUtc",
                table: "CrmHr_AiResourceBindings");

            migrationBuilder.CreateIndex(
                name: "IX_CrmHr_AuditEntries_EntityType_EntityId",
                table: "CrmHr_AuditEntries",
                columns: new[] { "EntityType", "EntityId" });
        }
    }
}
