using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
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
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<string>(
                name: "ReferenceKind",
                table: "Workbench_ProjectNodeReferences",
                type: "TEXT",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AlterColumn<int>(
                name: "ResourceKind",
                table: "Resources_ProjectResources",
                type: "INTEGER",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.CreateTable(
                name: "Workspace_ConnectorCommands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConnectorPluginKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    CommandKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ApprovalState = table.Column<int>(type: "INTEGER", nullable: false),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    NextAttemptAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastError = table.Column<string>(type: "TEXT", nullable: false),
                    ResultJson = table.Column<string>(type: "TEXT", nullable: false),
                    RequestedBy = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspace_ConnectorCommands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Workspace_ConnectorCommandAudits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ConnectorCommandId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    EventKind = table.Column<int>(type: "INTEGER", nullable: false),
                    Actor = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 400, nullable: false),
                    DetailsJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Workspace_ConnectorCommandAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Workspace_ConnectorCommandAudits_Workspace_ConnectorCommands_ConnectorCommandId",
                        column: x => x.ConnectorCommandId,
                        principalTable: "Workspace_ConnectorCommands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Workspace_ConnectorCommandAudits_ConnectorCommandId_CreatedAtUtc",
                table: "Workspace_ConnectorCommandAudits",
                columns: new[] { "ConnectorCommandId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Workspace_ConnectorCommands_ProjectId_ConnectorPluginKey_CommandKey_IdempotencyKey",
                table: "Workspace_ConnectorCommands",
                columns: new[] { "ProjectId", "ConnectorPluginKey", "CommandKey", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Workspace_ConnectorCommands_ProjectId_CreatedAtUtc",
                table: "Workspace_ConnectorCommands",
                columns: new[] { "ProjectId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Workspace_ConnectorCommands_Status_ApprovalState_NextAttemptAtUtc",
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
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExternalArtifactId",
                table: "Workbench_ProjectObjects",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalArtifactKind",
                table: "Workbench_ProjectObjects",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MarkerIcon",
                table: "Workbench_ProjectObjects",
                type: "TEXT",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MarkerLabel",
                table: "Workbench_ProjectObjects",
                type: "TEXT",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MarkerTone",
                table: "Workbench_ProjectObjects",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MediaContentType",
                table: "Workbench_ProjectObjects",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MediaOriginalFileName",
                table: "Workbench_ProjectObjects",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MediaRelativePath",
                table: "Workbench_ProjectObjects",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Route",
                table: "Workbench_ProjectObjects",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StorageObjectReferenceJson",
                table: "Workbench_ProjectObjects",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<int>(
                name: "ReferenceKind",
                table: "Workbench_ProjectNodeReferences",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 160);

            migrationBuilder.AlterColumn<int>(
                name: "ResourceKind",
                table: "Resources_ProjectResources",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldNullable: true);
        }
    }
}
