using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddCognitiveMemoryFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ProjectionStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ProjectionKind = table.Column<int>(type: "integer", nullable: false),
                    TargetProvider = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ProjectionSchemaVersion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LastSourceHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    LastProjectedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FailureMessage = table.Column<string>(type: "TEXT", nullable: false),
                    RebuildRequired = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ProjectionStates", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_RecallTraces",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    OperationMode = table.Column<int>(type: "integer", nullable: false),
                    RequestedByActorId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    PolicyProfileId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RequestHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    RequestHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    IncludedRecordCount = table.Column<int>(type: "integer", nullable: false),
                    ExcludedRecordCount = table.Column<int>(type: "integer", nullable: false),
                    TraceJson = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_RecallTraces", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_Records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Origin = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CanonicalText = table.Column<string>(type: "TEXT", nullable: false),
                    SummaryText = table.Column<string>(type: "TEXT", nullable: false),
                    ValidationState = table.Column<int>(type: "integer", nullable: false),
                    StabilityState = table.Column<int>(type: "integer", nullable: false),
                    CreatedInMode = table.Column<int>(type: "integer", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ContentHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SourceEvidenceCount = table.Column<int>(type: "integer", nullable: false),
                    GeneratedReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AccessLevel = table.Column<int>(type: "integer", nullable: false),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_Records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_Relations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceMemoryRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetMemoryRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    RelationKind = table.Column<int>(type: "integer", nullable: false),
                    EvidenceCount = table.Column<int>(type: "integer", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_Relations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ReviewItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewKind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    SubjectKind = table.Column<int>(type: "integer", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    ReasonCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ReasonText = table.Column<string>(type: "TEXT", nullable: false),
                    SourceEvidenceCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    DecidedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    DecidedByActorId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    DecisionNotes = table.Column<string>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ReviewItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_Runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    RunKind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OperationMode = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    InputHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    InputHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Cursor = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FailureMessage = table.Column<string>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_Runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SourceItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceManifestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceSystem = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceItemKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SourceItemType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Locator = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ContentHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    RedactionState = table.Column<int>(type: "integer", nullable: false),
                    AccessLevel = table.Column<int>(type: "integer", nullable: false),
                    AccessScope = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    ProvenanceJson = table.Column<string>(type: "TEXT", nullable: false),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SourceItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SourceLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MemoryRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceManifestId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceRole = table.Column<int>(type: "integer", nullable: false),
                    Locator = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    QuoteHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SourceLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SourceManifests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceSystem = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    SourceScopeKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    SourceSnapshotId = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    SnapshotHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    SnapshotHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProviderVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Cursor = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ScanStatus = table.Column<int>(type: "integer", nullable: false),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SourceManifests", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProjectionStates_ProjectId_ProjectionKind_T~",
                table: "CognitiveMemory_ProjectionStates",
                columns: new[] { "ProjectId", "ProjectionKind", "TargetProvider" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ProjectionStates_Status_RebuildRequired",
                table: "CognitiveMemory_ProjectionStates",
                columns: new[] { "Status", "RebuildRequired" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallTraces_ProjectId_OperationMode_Starte~",
                table: "CognitiveMemory_RecallTraces",
                columns: new[] { "ProjectId", "OperationMode", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_RecallTraces_RequestHash",
                table: "CognitiveMemory_RecallTraces",
                column: "RequestHash");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Records_ContentHash",
                table: "CognitiveMemory_Records",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Records_ProjectId_Kind_ValidationState",
                table: "CognitiveMemory_Records",
                columns: new[] { "ProjectId", "Kind", "ValidationState" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Records_ProjectId_StabilityState",
                table: "CognitiveMemory_Records",
                columns: new[] { "ProjectId", "StabilityState" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Relations_ProjectId_RelationKind",
                table: "CognitiveMemory_Relations",
                columns: new[] { "ProjectId", "RelationKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Relations_ProjectId_SourceMemoryRecordId_Ta~",
                table: "CognitiveMemory_Relations",
                columns: new[] { "ProjectId", "SourceMemoryRecordId", "TargetMemoryRecordId", "RelationKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Relations_TargetMemoryRecordId",
                table: "CognitiveMemory_Relations",
                column: "TargetMemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReviewItems_ProjectId_Status_RiskLevel",
                table: "CognitiveMemory_ReviewItems",
                columns: new[] { "ProjectId", "Status", "RiskLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ReviewItems_SubjectKind_SubjectId",
                table: "CognitiveMemory_ReviewItems",
                columns: new[] { "SubjectKind", "SubjectId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Runs_IdempotencyKey",
                table: "CognitiveMemory_Runs",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Runs_ProjectId_RunKind_Status",
                table: "CognitiveMemory_Runs",
                columns: new[] { "ProjectId", "RunKind", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceItems_ContentHash",
                table: "CognitiveMemory_SourceItems",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceItems_ProjectId_SourceSystem_SourceIt~",
                table: "CognitiveMemory_SourceItems",
                columns: new[] { "ProjectId", "SourceSystem", "SourceItemType" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceItems_SourceManifestId_SourceItemKey",
                table: "CognitiveMemory_SourceItems",
                columns: new[] { "SourceManifestId", "SourceItemKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceLinks_MemoryRecordId_SourceItemId_Evi~",
                table: "CognitiveMemory_SourceLinks",
                columns: new[] { "MemoryRecordId", "SourceItemId", "EvidenceRole" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceLinks_SourceItemId",
                table: "CognitiveMemory_SourceLinks",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceLinks_SourceManifestId",
                table: "CognitiveMemory_SourceLinks",
                column: "SourceManifestId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceManifests_ProjectId_SourceSystem_Obse~",
                table: "CognitiveMemory_SourceManifests",
                columns: new[] { "ProjectId", "SourceSystem", "ObservedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SourceManifests_SourceSystem_SourceScopeKey~",
                table: "CognitiveMemory_SourceManifests",
                columns: new[] { "SourceSystem", "SourceScopeKey", "SourceSnapshotId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CognitiveMemory_ProjectionStates");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_RecallTraces");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_Records");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_Relations");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ReviewItems");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_Runs");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SourceItems");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SourceLinks");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SourceManifests");
        }
    }
}
