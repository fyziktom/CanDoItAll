using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddCognitiveMemorySourceIngestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SourceItemKey",
                table: "CognitiveMemory_SourceItems",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);

            migrationBuilder.AddColumn<string>(
                name: "ContentText",
                table: "CognitiveMemory_SourceItems",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SourceItemContextHints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextFrameId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    DimensionKind = table.Column<int>(type: "integer", nullable: false),
                    ValueKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SourceItemContextHints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SourceItemContextHints_CognitiveMemory_Cont~",
                        column: x => x.ContextFrameId,
                        principalTable: "CognitiveMemory_ContextFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SourceItemContextHints_CognitiveMemory_Sour~",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SourceItemGraphLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceManifestId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceItemKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    TargetSourceItemKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    LinkKind = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IsUserAuthored = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SourceItemGraphLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SourceItemGraphLinks_CognitiveMemory_Source~",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SourceItemGraphLinks_CognitiveMemory_Sourc~1",
                        column: x => x.SourceManifestId,
                        principalTable: "CognitiveMemory_SourceManifests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SourceItemLayouts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    X = table.Column<double>(type: "double precision", nullable: true),
                    Y = table.Column<double>(type: "double precision", nullable: true),
                    ZIndex = table.Column<int>(type: "integer", nullable: true),
                    StartUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    SurfaceKind = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SourceItemLayouts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SourceItemLayouts_CognitiveMemory_SourceIte~",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SourceScanFailures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceSystem = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceScopeKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    CursorHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExceptionCategory = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    RetryPolicy = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SourceScanFailures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SourceScanFailures_CognitiveMemory_Runs_Run~",
                        column: x => x.RunId,
                        principalTable: "CognitiveMemory_Runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SourceTombstones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceSystem = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceScopeKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    SourceItemKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PreviousSourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    DetectedInManifestId = table.Column<Guid>(type: "uuid", nullable: false),
                    TombstonedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SourceTombstones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SourceTombstones_CognitiveMemory_SourceItem~",
                        column: x => x.PreviousSourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SourceTombstones_CognitiveMemory_SourceMani~",
                        column: x => x.DetectedInManifestId,
                        principalTable: "CognitiveMemory_SourceManifests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceItemContextHints_ContextFrameId",
                table: "CognitiveMemory_SourceItemContextHints",
                column: "ContextFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceItemContextHints_ProjectId_DimensionK~",
                table: "CognitiveMemory_SourceItemContextHints",
                columns: new[] { "ProjectId", "DimensionKind", "ValueKey" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceItemContextHints_SourceItemId_Context~",
                table: "CognitiveMemory_SourceItemContextHints",
                columns: new[] { "SourceItemId", "ContextFrameId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceItemGraphLinks_ProjectId_LinkKind",
                table: "CognitiveMemory_SourceItemGraphLinks",
                columns: new[] { "ProjectId", "LinkKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceItemGraphLinks_SourceItemId",
                table: "CognitiveMemory_SourceItemGraphLinks",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceItemGraphLinks_SourceManifestId_Sourc~",
                table: "CognitiveMemory_SourceItemGraphLinks",
                columns: new[] { "SourceManifestId", "SourceItemKey", "TargetSourceItemKey", "LinkKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceItemLayouts_ProjectId_SurfaceKind",
                table: "CognitiveMemory_SourceItemLayouts",
                columns: new[] { "ProjectId", "SurfaceKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceItemLayouts_SourceItemId",
                table: "CognitiveMemory_SourceItemLayouts",
                column: "SourceItemId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceScanFailures_ProjectId_SourceSystem_C~",
                table: "CognitiveMemory_SourceScanFailures",
                columns: new[] { "ProjectId", "SourceSystem", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceScanFailures_RunId",
                table: "CognitiveMemory_SourceScanFailures",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceScanFailures_SourceSystem_SourceScope~",
                table: "CognitiveMemory_SourceScanFailures",
                columns: new[] { "SourceSystem", "SourceScopeKey", "ExceptionCategory" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceTombstones_DetectedInManifestId",
                table: "CognitiveMemory_SourceTombstones",
                column: "DetectedInManifestId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceTombstones_PreviousSourceItemId",
                table: "CognitiveMemory_SourceTombstones",
                column: "PreviousSourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceTombstones_ProjectId_SourceSystem_Tom~",
                table: "CognitiveMemory_SourceTombstones",
                columns: new[] { "ProjectId", "SourceSystem", "TombstonedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceTombstones_SourceSystem_SourceScopeKe~",
                table: "CognitiveMemory_SourceTombstones",
                columns: new[] { "SourceSystem", "SourceScopeKey", "SourceItemKey", "DetectedInManifestId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CognitiveMemory_SourceItemContextHints");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SourceItemGraphLinks");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SourceItemLayouts");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SourceScanFailures");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SourceTombstones");

            migrationBuilder.DropColumn(
                name: "ContentText",
                table: "CognitiveMemory_SourceItems");

            migrationBuilder.AlterColumn<string>(
                name: "SourceItemKey",
                table: "CognitiveMemory_SourceItems",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);
        }
    }
}
