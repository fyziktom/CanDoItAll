using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddConnectorCommandOutboxBoundary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalArtifactId",
                table: "Workbench_ProjectObjects");

            migrationBuilder.DropColumn(
                name: "ExternalArtifactKind",
                table: "Workbench_ProjectObjects");

            migrationBuilder.DropColumn(
                name: "MarkerIcon",
                table: "Workbench_ProjectObjects");

            migrationBuilder.DropColumn(
                name: "MarkerLabel",
                table: "Workbench_ProjectObjects");

            migrationBuilder.DropColumn(
                name: "MarkerTone",
                table: "Workbench_ProjectObjects");

            migrationBuilder.DropColumn(
                name: "MediaContentType",
                table: "Workbench_ProjectObjects");

            migrationBuilder.DropColumn(
                name: "MediaOriginalFileName",
                table: "Workbench_ProjectObjects");

            migrationBuilder.DropColumn(
                name: "MediaRelativePath",
                table: "Workbench_ProjectObjects");

            migrationBuilder.DropColumn(
                name: "Route",
                table: "Workbench_ProjectObjects");

            migrationBuilder.DropColumn(
                name: "StorageObjectReferenceJson",
                table: "Workbench_ProjectObjects");

            migrationBuilder.AlterColumn<int>(
                name: "ProviderKind",
                table: "Workspace_ProviderProfiles",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceKind",
                table: "Workbench_ProjectNodeReferences",
                type: "character varying(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceId",
                table: "Workbench_ProjectNodeReferences",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<int>(
                name: "ResourceKind",
                table: "Resources_ProjectResources",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateTable(
                name: "Workspace_ConnectorCommands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectorPluginKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CommandKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ApprovalState = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: false),
                    ResultJson = table.Column<string>(type: "TEXT", nullable: false),
                    RequestedBy = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspace_ConnectorCommands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workspace_ConnectorCommandAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConnectorCommandId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventKind = table.Column<int>(type: "integer", nullable: false),
                    Actor = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Message = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    DetailsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspace_ConnectorCommandAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workspace_ConnectorCommandAudits_Workspace_ConnectorCommand~",
                        column: x => x.ConnectorCommandId,
                        principalTable: "Workspace_ConnectorCommands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Workspace_ConnectorCommandAudits_ConnectorCommandId_Created~",
                table: "Workspace_ConnectorCommandAudits",
                columns: new[] { "ConnectorCommandId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Workspace_ConnectorCommands_ProjectId_ConnectorPluginKey_Co~",
                table: "Workspace_ConnectorCommands",
                columns: new[] { "ProjectId", "ConnectorPluginKey", "CommandKey", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workspace_ConnectorCommands_ProjectId_CreatedAtUtc",
                table: "Workspace_ConnectorCommands",
                columns: new[] { "ProjectId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Workspace_ConnectorCommands_Status_ApprovalState_NextAttemp~",
                table: "Workspace_ConnectorCommands",
                columns: new[] { "Status", "ApprovalState", "NextAttemptAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Workspace_ConnectorCommandAudits");

            migrationBuilder.DropTable(
                name: "Workspace_ConnectorCommands");

            migrationBuilder.AlterColumn<int>(
                name: "ProviderKind",
                table: "Workspace_ProviderProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExternalArtifactId",
                table: "Workbench_ProjectObjects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalArtifactKind",
                table: "Workbench_ProjectObjects",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MarkerIcon",
                table: "Workbench_ProjectObjects",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MarkerLabel",
                table: "Workbench_ProjectObjects",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MarkerTone",
                table: "Workbench_ProjectObjects",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MediaContentType",
                table: "Workbench_ProjectObjects",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MediaOriginalFileName",
                table: "Workbench_ProjectObjects",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MediaRelativePath",
                table: "Workbench_ProjectObjects",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Route",
                table: "Workbench_ProjectObjects",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StorageObjectReferenceJson",
                table: "Workbench_ProjectObjects",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<int>(
                name: "ReferenceKind",
                table: "Workbench_ProjectNodeReferences",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(160)",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<Guid>(
                name: "ReferenceId",
                table: "Workbench_ProjectNodeReferences",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<int>(
                name: "ResourceKind",
                table: "Resources_ProjectResources",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
