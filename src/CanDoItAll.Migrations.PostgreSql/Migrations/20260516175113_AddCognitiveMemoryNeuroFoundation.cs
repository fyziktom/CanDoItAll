using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddCognitiveMemoryNeuroFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ContextFrames",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    FrameKind = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ConfidenceScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfidenceBucket = table.Column<int>(type: "integer", nullable: false),
                    DisplayConfidenceScore = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ContextFrames", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ContextFrames_CognitiveMemory_ScoreEvaluati~",
                        column: x => x.ConfidenceScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_EvidenceAnchors",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    AnchorKind = table.Column<int>(type: "integer", nullable: false),
                    SourceManifestId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceSystem = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Locator = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    StructuredPath = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    TextStart = table.Column<int>(type: "integer", nullable: true),
                    TextEnd = table.Column<int>(type: "integer", nullable: true),
                    QuoteHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    TrustLevel = table.Column<int>(type: "integer", nullable: false),
                    RedactionState = table.Column<int>(type: "integer", nullable: false),
                    SourceHashAlgorithm = table.Column<int>(type: "integer", nullable: false),
                    SourceHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ObservedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_EvidenceAnchors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_EvidenceAnchors_CognitiveMemory_SourceItems~",
                        column: x => x.SourceItemId,
                        principalTable: "CognitiveMemory_SourceItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_EvidenceAnchors_CognitiveMemory_SourceManif~",
                        column: x => x.SourceManifestId,
                        principalTable: "CognitiveMemory_SourceManifests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_MutationCommands",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommandKind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ActorKind = table.Column<int>(type: "integer", nullable: false),
                    ActorId = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    AffectedMemoryRecordIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    AffectedClaimIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    EvidenceAnchorIdsJson = table.Column<string>(type: "TEXT", nullable: false),
                    PayloadJson = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedVersionToken = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    RequiresHumanReview = table.Column<bool>(type: "boolean", nullable: false),
                    ReviewReason = table.Column<string>(type: "TEXT", nullable: false),
                    ResultVersionToken = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_MutationCommands", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_Claims",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    MemoryRecordId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClaimKind = table.Column<int>(type: "integer", nullable: false),
                    ClaimText = table.Column<string>(type: "TEXT", nullable: false),
                    SubjectKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    PredicateKey = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(240)", maxLength: 240, nullable: false),
                    PrimaryContextFrameId = table.Column<Guid>(type: "uuid", nullable: true),
                    ValidFromUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ValidToUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CurrentBeliefState = table.Column<int>(type: "integer", nullable: false),
                    CurrentBeliefScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CurrentBeliefBucket = table.Column<int>(type: "integer", nullable: false),
                    DisplayBeliefScore = table.Column<double>(type: "double precision", nullable: true),
                    ValidationState = table.Column<int>(type: "integer", nullable: false),
                    StabilityState = table.Column<int>(type: "integer", nullable: false),
                    SupersedesClaimId = table.Column<Guid>(type: "uuid", nullable: true),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_Claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Claims_CognitiveMemory_ContextFrames_Primar~",
                        column: x => x.PrimaryContextFrameId,
                        principalTable: "CognitiveMemory_ContextFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Claims_CognitiveMemory_Records_MemoryRecord~",
                        column: x => x.MemoryRecordId,
                        principalTable: "CognitiveMemory_Records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Claims_CognitiveMemory_ScoreEvaluations_Cur~",
                        column: x => x.CurrentBeliefScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ContextBoundaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceContextFrameId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetContextFrameId = table.Column<Guid>(type: "uuid", nullable: false),
                    BoundaryKind = table.Column<int>(type: "integer", nullable: false),
                    BoundaryPolicy = table.Column<int>(type: "integer", nullable: false),
                    ScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: true),
                    Explanation = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ContextBoundaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ContextBoundaries_CognitiveMemory_ContextFr~",
                        column: x => x.SourceContextFrameId,
                        principalTable: "CognitiveMemory_ContextFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ContextBoundaries_CognitiveMemory_ContextF~1",
                        column: x => x.TargetContextFrameId,
                        principalTable: "CognitiveMemory_ContextFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ContextBoundaries_CognitiveMemory_ScoreEval~",
                        column: x => x.ScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ContextFrameDimensions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ContextFrameId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    DimensionKind = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ValueKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ContextFrameDimensions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ContextFrameDimensions_CognitiveMemory_Cont~",
                        column: x => x.ContextFrameId,
                        principalTable: "CognitiveMemory_ContextFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_Entities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityKind = table.Column<int>(type: "integer", nullable: false),
                    CanonicalName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CanonicalNameKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PrimaryContextFrameId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfidenceScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConfidenceBucket = table.Column<int>(type: "integer", nullable: false),
                    DisplayConfidenceScore = table.Column<double>(type: "double precision", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_Entities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Entities_CognitiveMemory_ContextFrames_Prim~",
                        column: x => x.PrimaryContextFrameId,
                        principalTable: "CognitiveMemory_ContextFrames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_Entities_CognitiveMemory_ScoreEvaluations_C~",
                        column: x => x.ConfidenceScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_MutationAuditEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    MutationCommandId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    Sequence = table.Column<int>(type: "integer", nullable: false),
                    EventKind = table.Column<int>(type: "integer", nullable: false),
                    Message = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_MutationAuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_MutationAuditEvents_CognitiveMemory_Mutatio~",
                        column: x => x.MutationCommandId,
                        principalTable: "CognitiveMemory_MutationCommands",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_BeliefStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    StateKind = table.Column<int>(type: "integer", nullable: false),
                    ScoreEvaluationTraceId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectionBucket = table.Column<int>(type: "integer", nullable: false),
                    DisplayBeliefScore = table.Column<double>(type: "double precision", nullable: true),
                    Explanation = table.Column<string>(type: "TEXT", nullable: false),
                    AlgorithmVersion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CalculatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_BeliefStates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_BeliefStates_CognitiveMemory_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "CognitiveMemory_Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_BeliefStates_CognitiveMemory_ScoreEvaluatio~",
                        column: x => x.ScoreEvaluationTraceId,
                        principalTable: "CognitiveMemory_ScoreEvaluations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_ClaimEvidenceLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimId = table.Column<Guid>(type: "uuid", nullable: false),
                    EvidenceAnchorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<int>(type: "integer", nullable: false),
                    Explanation = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_ClaimEvidenceLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ClaimEvidenceLinks_CognitiveMemory_Claims_C~",
                        column: x => x.ClaimId,
                        principalTable: "CognitiveMemory_Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_ClaimEvidenceLinks_CognitiveMemory_Evidence~",
                        column: x => x.EvidenceAnchorId,
                        principalTable: "CognitiveMemory_EvidenceAnchors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CognitiveMemory_EntityAliases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                    EntityKind = table.Column<int>(type: "integer", nullable: false),
                    Alias = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    AliasKey = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CognitiveMemory_EntityAliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CognitiveMemory_EntityAliases_CognitiveMemory_Entities_Enti~",
                        column: x => x.EntityId,
                        principalTable: "CognitiveMemory_Entities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_BeliefStates_ClaimId_CalculatedAtUtc",
                table: "CognitiveMemory_BeliefStates",
                columns: new[] { "ClaimId", "CalculatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_BeliefStates_ScoreEvaluationTraceId",
                table: "CognitiveMemory_BeliefStates",
                column: "ScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_BeliefStates_StateKind_ProjectionBucket",
                table: "CognitiveMemory_BeliefStates",
                columns: new[] { "StateKind", "ProjectionBucket" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ClaimEvidenceLinks_ClaimId_EvidenceAnchorId~",
                table: "CognitiveMemory_ClaimEvidenceLinks",
                columns: new[] { "ClaimId", "EvidenceAnchorId", "Direction" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ClaimEvidenceLinks_EvidenceAnchorId_Directi~",
                table: "CognitiveMemory_ClaimEvidenceLinks",
                columns: new[] { "EvidenceAnchorId", "Direction" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Claims_CurrentBeliefScoreEvaluationTraceId",
                table: "CognitiveMemory_Claims",
                column: "CurrentBeliefScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Claims_MemoryRecordId",
                table: "CognitiveMemory_Claims",
                column: "MemoryRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Claims_PrimaryContextFrameId",
                table: "CognitiveMemory_Claims",
                column: "PrimaryContextFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Claims_ProjectId_ClaimKind_CurrentBeliefSta~",
                table: "CognitiveMemory_Claims",
                columns: new[] { "ProjectId", "ClaimKind", "CurrentBeliefState", "ValidationState" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Claims_ProjectId_SubjectKey_PredicateKey_Ob~",
                table: "CognitiveMemory_Claims",
                columns: new[] { "ProjectId", "SubjectKey", "PredicateKey", "ObjectKey" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ContextBoundaries_ProjectId_BoundaryPolicy",
                table: "CognitiveMemory_ContextBoundaries",
                columns: new[] { "ProjectId", "BoundaryPolicy" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ContextBoundaries_ProjectId_SourceContextFr~",
                table: "CognitiveMemory_ContextBoundaries",
                columns: new[] { "ProjectId", "SourceContextFrameId", "TargetContextFrameId", "BoundaryKind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ContextBoundaries_ScoreEvaluationTraceId",
                table: "CognitiveMemory_ContextBoundaries",
                column: "ScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ContextBoundaries_SourceContextFrameId",
                table: "CognitiveMemory_ContextBoundaries",
                column: "SourceContextFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ContextBoundaries_TargetContextFrameId",
                table: "CognitiveMemory_ContextBoundaries",
                column: "TargetContextFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ContextFrameDimensions_ContextFrameId_Dimen~",
                table: "CognitiveMemory_ContextFrameDimensions",
                columns: new[] { "ContextFrameId", "DimensionKind", "ValueKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ContextFrameDimensions_ProjectId_DimensionK~",
                table: "CognitiveMemory_ContextFrameDimensions",
                columns: new[] { "ProjectId", "DimensionKind", "ValueKey" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ContextFrames_ConfidenceScoreEvaluationTrac~",
                table: "CognitiveMemory_ContextFrames",
                column: "ConfidenceScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_ContextFrames_ProjectId_FrameKind_DisplayNa~",
                table: "CognitiveMemory_ContextFrames",
                columns: new[] { "ProjectId", "FrameKind", "DisplayName" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Entities_ConfidenceScoreEvaluationTraceId",
                table: "CognitiveMemory_Entities",
                column: "ConfidenceScoreEvaluationTraceId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Entities_PrimaryContextFrameId",
                table: "CognitiveMemory_Entities",
                column: "PrimaryContextFrameId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_Entities_ProjectId_EntityKind_CanonicalName~",
                table: "CognitiveMemory_Entities",
                columns: new[] { "ProjectId", "EntityKind", "CanonicalNameKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EntityAliases_EntityId",
                table: "CognitiveMemory_EntityAliases",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EntityAliases_ProjectId_EntityKind_AliasKey",
                table: "CognitiveMemory_EntityAliases",
                columns: new[] { "ProjectId", "EntityKind", "AliasKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EvidenceAnchors_ProjectId_AnchorKind_Observ~",
                table: "CognitiveMemory_EvidenceAnchors",
                columns: new[] { "ProjectId", "AnchorKind", "ObservedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EvidenceAnchors_ProjectId_SourceManifestId_~",
                table: "CognitiveMemory_EvidenceAnchors",
                columns: new[] { "ProjectId", "SourceManifestId", "SourceItemId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EvidenceAnchors_QuoteHash",
                table: "CognitiveMemory_EvidenceAnchors",
                column: "QuoteHash");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EvidenceAnchors_SourceHash",
                table: "CognitiveMemory_EvidenceAnchors",
                column: "SourceHash");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EvidenceAnchors_SourceItemId",
                table: "CognitiveMemory_EvidenceAnchors",
                column: "SourceItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_EvidenceAnchors_SourceManifestId",
                table: "CognitiveMemory_EvidenceAnchors",
                column: "SourceManifestId");

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_MutationAuditEvents_MutationCommandId_Seque~",
                table: "CognitiveMemory_MutationAuditEvents",
                columns: new[] { "MutationCommandId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_MutationAuditEvents_ProjectId_EventKind_Cre~",
                table: "CognitiveMemory_MutationAuditEvents",
                columns: new[] { "ProjectId", "EventKind", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_MutationCommands_ActorKind_ActorId",
                table: "CognitiveMemory_MutationCommands",
                columns: new[] { "ActorKind", "ActorId" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_MutationCommands_ProjectId_CommandKind_Stat~",
                table: "CognitiveMemory_MutationCommands",
                columns: new[] { "ProjectId", "CommandKind", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CognitiveMemory_MutationCommands_ProjectId_IdempotencyKey",
                table: "CognitiveMemory_MutationCommands",
                columns: new[] { "ProjectId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CognitiveMemory_BeliefStates");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ClaimEvidenceLinks");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ContextBoundaries");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ContextFrameDimensions");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_EntityAliases");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_MutationAuditEvents");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_Claims");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_EvidenceAnchors");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_Entities");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_MutationCommands");

            migrationBuilder.DropTable(
                name: "CognitiveMemory_ContextFrames");
        }
    }
}
