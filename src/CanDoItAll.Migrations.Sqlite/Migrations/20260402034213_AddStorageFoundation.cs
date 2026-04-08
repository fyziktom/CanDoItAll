using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddStorageFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StorageObjectReferenceJson",
                table: "Workbench_ProjectObjects",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Storage_Catalog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    ProviderKind = table.Column<int>(type: "INTEGER", nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsSystemDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsReadOnly = table.Column<bool>(type: "INTEGER", nullable: false),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    ConnectionMode = table.Column<int>(type: "INTEGER", nullable: false),
                    EndpointOrRoot = table.Column<string>(type: "TEXT", maxLength: 1200, nullable: false),
                    ConfigJson = table.Column<string>(type: "TEXT", nullable: false),
                    CapabilityMask = table.Column<int>(type: "INTEGER", nullable: false),
                    HealthStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    LastTestedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    LastHealthMessage = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CredentialSecretId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Storage_Catalog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Storage_RoutingRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    IsEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Priority = table.Column<int>(type: "INTEGER", nullable: false),
                    ScopeKind = table.Column<int>(type: "INTEGER", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: true),
                    NodeKey = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    UsagePurpose = table.Column<int>(type: "INTEGER", nullable: false),
                    ContentKind = table.Column<int>(type: "INTEGER", nullable: false),
                    MimePattern = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    MinimumContentLength = table.Column<long>(type: "INTEGER", nullable: true),
                    MaximumContentLength = table.Column<long>(type: "INTEGER", nullable: true),
                    EditIntent = table.Column<bool>(type: "INTEGER", nullable: false),
                    PreviewRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    PublishIntent = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiredCapabilities = table.Column<int>(type: "INTEGER", nullable: false),
                    PreferredStorageId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AlternativeStorageIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Storage_RoutingRules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Storage_Catalog_Name",
                table: "Storage_Catalog",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Storage_Catalog_ProviderKind_IsEnabled",
                table: "Storage_Catalog",
                columns: new[] { "ProviderKind", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_Storage_RoutingRules_ScopeKind_ProjectId_NodeKey_Priority_PreferredStorageId",
                table: "Storage_RoutingRules",
                columns: new[] { "ScopeKind", "ProjectId", "NodeKey", "Priority", "PreferredStorageId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Storage_Catalog");

            migrationBuilder.DropTable(
                name: "Storage_RoutingRules");

            migrationBuilder.DropColumn(
                name: "StorageObjectReferenceJson",
                table: "Workbench_ProjectObjects");
        }
    }
}
