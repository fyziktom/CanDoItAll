using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddProcessAuthoring : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.CreateTable(
                name: "process_authoring_heads",
                columns: table => new {
                    DatabaseProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectLifetimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    Lifecycle = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    PublishedId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Summary = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    Criticality = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OperatingMode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_process_authoring_heads", x => new { x.DatabaseProfileId, x.ProjectId, x.ProjectLifetimeId, x.DefinitionKey });
                    table.CheckConstraint("CK_process_authoring_heads_revision", "\"Revision\" > 0");
                    table.CheckConstraint("CK_process_authoring_heads_scope", "(\"ProjectId\" = '00000000-0000-0000-0000-000000000000'::uuid) = (\"ProjectLifetimeId\" = '00000000-0000-0000-0000-000000000000'::uuid)");
                });

            migrationBuilder.CreateTable(
                name: "process_authoring_publications",
                columns: table => new {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DatabaseProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectLifetimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Revision = table.Column<long>(type: "bigint", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(71)", maxLength: 71, nullable: false),
                    ContentJson = table.Column<string>(type: "text", nullable: false),
                    PublishedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_process_authoring_publications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "process_authoring_receipts",
                columns: table => new {
                    DatabaseProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    CallerId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectLifetimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    DefinitionKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    RequestFingerprint = table.Column<string>(type: "character varying(71)", maxLength: 71, nullable: false),
                    ReceiptJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_process_authoring_receipts", x => new { x.DatabaseProfileId, x.CallerId, x.OperationId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_process_authoring_heads_DatabaseProfileId_ProjectId_Project~",
                table: "process_authoring_heads",
                columns: new[] { "DatabaseProfileId", "ProjectId", "ProjectLifetimeId", "Lifecycle" });

            migrationBuilder.CreateIndex(
                name: "IX_process_authoring_publications_DatabaseProfileId_ProjectId_~",
                table: "process_authoring_publications",
                columns: new[] { "DatabaseProfileId", "ProjectId", "ProjectLifetimeId", "DefinitionKey", "Revision" },
                unique: true);
        }
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropTable(
                name: "process_authoring_heads");

            migrationBuilder.DropTable(
                name: "process_authoring_publications");

            migrationBuilder.DropTable(
                name: "process_authoring_receipts");
        }
    }
}
