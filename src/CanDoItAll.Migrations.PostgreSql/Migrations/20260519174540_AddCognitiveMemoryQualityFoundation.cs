using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddCognitiveMemoryQualityFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CognitiveMemory_DreamRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    TriggerKind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    PolicyProfileId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ClustersConsidered = table.Column<int>(type: "integer", nullable: false),
                    ClusterMembersRead = table.Column<int>(type: "integer", nullable: false),
                    ClaimsExtracted = table.Column<int>(type: "integer", nullable: false),
                    AggregateCandidatesCreated = table.Column<int>(type: "integer", nullable: false),
                    AggregateClaimsCreated = table.Column<int>(type: "integer", nullable: false),
                    AggregateClaimSourceMapsCreated = table.Column<int>(type: "integer", nullable: false),
                    ValidationRecordsCreated = table.Column<int>(type: "integer", nullable: false),
                    ReviewItemsCreated = table.Column<int>(type: "integer", nullable: false),
                    ApprovedCandidates = table.Column<int>(type: "integer", nullable: false),
                    RejectedCandidates = table.Column<int>(type: "integer", nullable: false),
                    NeedsReviewCandidates = table.Column<int>(type: "integer", nullable: false),
                    EvidenceCoverageRatio = table.Column<double>(type: "double precision", nullable: false),
                    WarningsJson = table.Column<string>(type: "TEXT", nullable: false),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FailureMessage = table.Column<string>(type: "TEXT", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_DreamRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_QualityClusters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClusterHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    PrimaryKeyFamily = table.Column<int>(type: "integer", nullable: false),
                    Readiness = table.Column<int>(type: "integer", nullable: false),
                    AccessLevel = table.Column<int>(type: "integer", nullable: false),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    PolicyProfileId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    KeyCount = table.Column<int>(type: "integer", nullable: false),
                    MemberCount = table.Column<int>(type: "integer", nullable: false),
                    SourceEvidenceCount = table.Column<int>(type: "integer", nullable: false),
                    ContradictionCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_QualityClusters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SynthesizedRecalls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecallTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Brief = table.Column<string>(type: "TEXT", nullable: false),
                    ReferencesShownByDefault = table.Column<bool>(type: "boolean", nullable: false),
                    StatementCount = table.Column<int>(type: "integer", nullable: false),
                    SourceMapCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SynthesizedRecalls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SynthesizedRecalls_CognitiveMemory_RecallTr~",
                        column: x => x.RecallTraceId,
                        principalTable: "CognitiveMemory_RecallTraces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_DreamRunClusters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DreamRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClusterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Readiness = table.Column<int>(type: "integer", nullable: false),
                    SelectionReasonCode = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    MemberCount = table.Column<int>(type: "integer", nullable: false),
                    ClaimCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_DreamRunClusters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_DreamRunClusters_CognitiveMemory_DreamRuns_~",
                        column: x => x.DreamRunId,
                        principalTable: "CognitiveMemory_DreamRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_DreamRunClusters_CognitiveMemory_QualityClu~",
                        column: x => x.ClusterId,
                        principalTable: "CognitiveMemory_QualityClusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_QualityClusterKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClusterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    KeyFamily = table.Column<int>(type: "integer", nullable: false),
                    Key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DisplayText = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_QualityClusterKeys", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_QualityClusterKeys_CognitiveMemory_QualityC~",
                        column: x => x.ClusterId,
                        principalTable: "CognitiveMemory_QualityClusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_QualityClusterMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClusterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    MemberKind = table.Column<int>(type: "integer", nullable: false),
                    MemoryRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccessLevel = table.Column<int>(type: "integer", nullable: false),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    ValidationState = table.Column<int>(type: "integer", nullable: false),
                    StabilityState = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_QualityClusterMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_QualityClusterMembers_CognitiveMemory_Evide~",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_QualityClusterMembers_CognitiveMemory_Quali~",
                        column: x => x.ClusterId,
                        principalTable: "CognitiveMemory_QualityClusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_QualityClusterMembers_CognitiveMemory_Recor~",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_QualityClusterMembers_CognitiveMemory_Sourc~",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SynthesizedStatements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SynthesisId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SynthesizedStatements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SynthesizedStatements_CognitiveMemory_Synth~",
                        column: x => x.SynthesisId,
                        principalTable: "CognitiveMemory_SynthesizedRecalls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_SynthesizedStatementSourceMaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SynthesisId = table.Column<Guid>(type: "uuid", nullable: false),
                    StatementId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    MemoryRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceSystem = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Locator = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    AccessLevel = table.Column<int>(type: "integer", nullable: false),
                    RedactionState = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_SynthesizedStatementSourceMaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMem~",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMe~1",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMe~2",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMe~3",
                        column: x => x.StatementId,
                        principalTable: "CognitiveMemory_SynthesizedStatements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_SynthesizedStatementSourceMaps_CognitiveMe~4",
                        column: x => x.SynthesisId,
                        principalTable: "CognitiveMemory_SynthesizedRecalls",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_DreamAggregateCandidates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DreamRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClusterId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SummaryText = table.Column<string>(type: "TEXT", nullable: false),
                    CanonicalText = table.Column<string>(type: "TEXT", nullable: false),
                    AccessLevel = table.Column<int>(type: "integer", nullable: false),
                    RiskLevel = table.Column<int>(type: "integer", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    PayloadHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ValidationRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    MemoryRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimCount = table.Column<int>(type: "integer", nullable: false),
                    SourceMapCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_DreamAggregateCandidates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_DreamAggregateCandidates_CognitiveMemory_Dr~",
                        column: x => x.DreamRunId,
                        principalTable: "CognitiveMemory_DreamRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_DreamAggregateCandidates_CognitiveMemory_Qu~",
                        column: x => x.ClusterId,
                        principalTable: "CognitiveMemory_QualityClusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_DreamAggregateCandidates_CognitiveMemory_Re~",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_DreamAggregateCandidates_CognitiveMemory_R~1",
                        column: x => x.ReviewItemId,
                        principalTable: "CognitiveMemory_ReviewItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_DreamAggregateClaims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AggregateCandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    ClaimKind = table.Column<int>(type: "integer", nullable: false),
                    ClaimText = table.Column<string>(type: "TEXT", nullable: false),
                    SubjectKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    PredicateKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_DreamAggregateClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_DreamAggregateClaims_CognitiveMemory_DreamA~",
                        column: x => x.AggregateCandidateId,
                        principalTable: "CognitiveMemory_DreamAggregateCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_DreamValidations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AggregateCandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Decision = table.Column<int>(type: "integer", nullable: false),
                    PolicyProfileId = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    IssueCount = table.Column<int>(type: "integer", nullable: false),
                    ClaimsChecked = table.Column<int>(type: "integer", nullable: false),
                    SourceMapsChecked = table.Column<int>(type: "integer", nullable: false),
                    IssuesJson = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_DreamValidations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_DreamValidations_CognitiveMemory_DreamAggre~",
                        column: x => x.AggregateCandidateId,
                        principalTable: "CognitiveMemory_DreamAggregateCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_DreamAggregateClaimSourceMaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AggregateCandidateId = table.Column<Guid>(type: "uuid", nullable: false),
                    AggregateClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceMemoryRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: true),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    AccessLevel = table.Column<int>(type: "integer", nullable: false),
                    RedactionState = table.Column<int>(type: "integer", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_DreamAggregateClaimSourceMaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_DreamAggregateClaimSourceMaps_CognitiveMemo~",
                        column: x => x.AggregateCandidateId,
                        principalTable: "CognitiveMemory_DreamAggregateCandidates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_DreamAggregateClaimSourceMaps_CognitiveMem~1",
                        column: x => x.AggregateClaimId,
                        principalTable: "CognitiveMemory_DreamAggregateClaims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_DreamAggregateClaimSourceMaps_CognitiveMem~2",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_DreamAggregateClaimSourceMaps_CognitiveMem~3",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_DreamAggregateClaimSourceMaps_CognitiveMem~4",
                        column: x => x.SourceMemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateCandidates_ClusterId",
                table: "CognitiveMemory_DreamAggregateCandidates",
                column: "ClusterId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateCandidates_DreamRunId_Cluster~",
                table: "CognitiveMemory_DreamAggregateCandidates",
                columns: new[] { "DreamRunId", "ClusterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateCandidates_MemoryRecordId",
                table: "CognitiveMemory_DreamAggregateCandidates",
                column: "MemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateCandidates_PayloadHash",
                table: "CognitiveMemory_DreamAggregateCandidates",
                column: "PayloadHash");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateCandidates_ProjectId_Mode_Sta~",
                table: "CognitiveMemory_DreamAggregateCandidates",
                columns: new[] { "ProjectId", "Mode", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateCandidates_ReviewItemId",
                table: "CognitiveMemory_DreamAggregateCandidates",
                column: "ReviewItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateCandidates_ValidationRecordId",
                table: "CognitiveMemory_DreamAggregateCandidates",
                column: "ValidationRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateClaims_AggregateCandidateId_S~",
                table: "CognitiveMemory_DreamAggregateClaims",
                columns: new[] { "AggregateCandidateId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateClaims_ProjectId_SubjectKey_P~",
                table: "CognitiveMemory_DreamAggregateClaims",
                columns: new[] { "ProjectId", "SubjectKey", "PredicateKey", "ObjectKey" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateClaimSourceMaps_AggregateCand~",
                table: "CognitiveMemory_DreamAggregateClaimSourceMaps",
                column: "AggregateCandidateId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateClaimSourceMaps_AggregateClai~",
                table: "CognitiveMemory_DreamAggregateClaimSourceMaps",
                columns: new[] { "AggregateClaimId", "SourceMemoryRecordId", "EvidenceAnchorId", "Direction" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateClaimSourceMaps_EvidenceAncho~",
                table: "CognitiveMemory_DreamAggregateClaimSourceMaps",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateClaimSourceMaps_ProjectId_Dir~",
                table: "CognitiveMemory_DreamAggregateClaimSourceMaps",
                columns: new[] { "ProjectId", "Direction" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateClaimSourceMaps_SourceItemId",
                table: "CognitiveMemory_DreamAggregateClaimSourceMaps",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamAggregateClaimSourceMaps_SourceMemoryR~",
                table: "CognitiveMemory_DreamAggregateClaimSourceMaps",
                column: "SourceMemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamRunClusters_ClusterId",
                table: "CognitiveMemory_DreamRunClusters",
                column: "ClusterId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamRunClusters_DreamRunId_ClusterId",
                table: "CognitiveMemory_DreamRunClusters",
                columns: new[] { "DreamRunId", "ClusterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamRunClusters_ProjectId_Readiness",
                table: "CognitiveMemory_DreamRunClusters",
                columns: new[] { "ProjectId", "Readiness" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamRuns_ProjectId_IdempotencyKey",
                table: "CognitiveMemory_DreamRuns",
                columns: new[] { "ProjectId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamRuns_ProjectId_Mode_Status_StartedAtUtc",
                table: "CognitiveMemory_DreamRuns",
                columns: new[] { "ProjectId", "Mode", "Status", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamValidations_AggregateCandidateId_Decis~",
                table: "CognitiveMemory_DreamValidations",
                columns: new[] { "AggregateCandidateId", "Decision" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_DreamValidations_ProjectId_Decision_Created~",
                table: "CognitiveMemory_DreamValidations",
                columns: new[] { "ProjectId", "Decision", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_QualityClusterKeys_ClusterId_KeyFamily_Key",
                table: "CognitiveMemory_QualityClusterKeys",
                columns: new[] { "ClusterId", "KeyFamily", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_QualityClusterKeys_ProjectId_KeyFamily_Key",
                table: "CognitiveMemory_QualityClusterKeys",
                columns: new[] { "ProjectId", "KeyFamily", "Key" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_QualityClusterMembers_ClusterId_MemberKind_~",
                table: "CognitiveMemory_QualityClusterMembers",
                columns: new[] { "ClusterId", "MemberKind", "MemoryRecordId", "SourceItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_QualityClusterMembers_EvidenceAnchorId",
                table: "CognitiveMemory_QualityClusterMembers",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_QualityClusterMembers_MemoryRecordId",
                table: "CognitiveMemory_QualityClusterMembers",
                column: "MemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_QualityClusterMembers_ProjectId_MemberKind",
                table: "CognitiveMemory_QualityClusterMembers",
                columns: new[] { "ProjectId", "MemberKind" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_QualityClusterMembers_SourceItemId",
                table: "CognitiveMemory_QualityClusterMembers",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_QualityClusters_ProjectId_AccessLevel_RiskL~",
                table: "CognitiveMemory_QualityClusters",
                columns: new[] { "ProjectId", "AccessLevel", "RiskLevel" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_QualityClusters_ProjectId_ClusterHash",
                table: "CognitiveMemory_QualityClusters",
                columns: new[] { "ProjectId", "ClusterHash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_QualityClusters_ProjectId_PrimaryKeyFamily_~",
                table: "CognitiveMemory_QualityClusters",
                columns: new[] { "ProjectId", "PrimaryKeyFamily", "Readiness" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedRecalls_CreatedAtUtc",
                table: "CognitiveMemory_SynthesizedRecalls",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedRecalls_ProjectId_RecallTraceId",
                table: "CognitiveMemory_SynthesizedRecalls",
                columns: new[] { "ProjectId", "RecallTraceId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedRecalls_RecallTraceId",
                table: "CognitiveMemory_SynthesizedRecalls",
                column: "RecallTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedStatements_ProjectId_CreatedAtUtc",
                table: "CognitiveMemory_SynthesizedStatements",
                columns: new[] { "ProjectId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedStatements_SynthesisId_Sequence",
                table: "CognitiveMemory_SynthesizedStatements",
                columns: new[] { "SynthesisId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_EvidenceAnch~",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                column: "EvidenceAnchorId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_MemoryRecord~",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                column: "MemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_ProjectId_Ac~",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                columns: new[] { "ProjectId", "AccessLevel", "RedactionState" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_SourceItemId",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_StatementId_~",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                columns: new[] { "StatementId", "MemoryRecordId", "SourceItemId", "EvidenceAnchorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_SynthesizedStatementSourceMaps_SynthesisId",
                table: "CognitiveMemory_SynthesizedStatementSourceMaps",
                column: "SynthesisId");

            migrationBuilder.AddForeignKey(
                name: "FK_CognitiveMemory_DreamAggregateCandidates_CognitiveMemory_D~1",
                table: "CognitiveMemory_DreamAggregateCandidates",
                column: "ValidationRecordId",
                principalTable: "CognitiveMemory_DreamValidations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_DreamAggregateCandidates_CognitiveMemory_Dr~",
                table: "CognitiveMemory_DreamAggregateCandidates");

            migrationBuilder.DropForeignKey(
                name: "FK_CognitiveMemory_DreamAggregateCandidates_CognitiveMemory_D~1",
                table: "CognitiveMemory_DreamAggregateCandidates");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_DreamAggregateClaimSourceMaps");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_DreamRunClusters");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_QualityClusterKeys");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_QualityClusterMembers");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SynthesizedStatementSourceMaps");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_DreamAggregateClaims");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SynthesizedStatements");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_SynthesizedRecalls");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_DreamRuns");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_DreamValidations");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_DreamAggregateCandidates");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_QualityClusters");
        }
    }
}
