using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations;

public partial class AddDefinitionSharingOptions : Migration {
    protected override void Up(MigrationBuilder migrationBuilder) {
        migrationBuilder.AddColumn<bool>(
            name: "ApiAvailable",
            table: "Workspace_RemoteTemplatePublications",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<bool>(
            name: "SharedTemplate",
            table: "Workspace_RemoteTemplatePublications",
            type: "boolean",
            nullable: false,
            defaultValue: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder) {
        migrationBuilder.Sql("""
            DO $$
            BEGIN
                IF EXISTS (
                    SELECT 1 FROM "Workspace_RemoteTemplatePublications"
                    WHERE NOT "ApiAvailable" OR NOT "SharedTemplate"
                ) THEN
                    RAISE EXCEPTION 'Downgrade refused. The previous schema cannot preserve these definition sharing choices.';
                END IF;
            END $$;
            """);

        migrationBuilder.DropColumn(name: "ApiAvailable", table: "Workspace_RemoteTemplatePublications");
        migrationBuilder.DropColumn(name: "SharedTemplate", table: "Workspace_RemoteTemplatePublications");
    }
}
