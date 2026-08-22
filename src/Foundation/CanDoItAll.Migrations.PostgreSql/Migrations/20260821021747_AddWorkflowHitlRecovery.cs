using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkflowHitlRecovery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AgentFramework_WorkflowBackendCheckpointSessions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Backend = table.Column<int>(type: "integer", nullable: false),
                    Format = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    FormatVersion = table.Column<int>(type: "integer", nullable: false),
                    CompilerContractVersion = table.Column<int>(type: "integer", nullable: false),
                    TopologyFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NextCommitOrdinal = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFramework_WorkflowBackendCheckpointSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AF_WfCheckpointSessions_Runs",
                        column: x => x.RunId,
                        principalTable: "AgentFramework_WorkflowRuns",
                        principalColumn: "RunId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentFramework_WorkflowExecutorInvocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ScopeKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InvocationKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    WorkflowVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExecutorId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    ExecutorContractVersion = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CausationRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    CausationRequestVersion = table.Column<long>(type: "bigint", nullable: false),
                    CausationOperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    LogicalGeneration = table.Column<long>(type: "bigint", nullable: false),
                    InputHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    ConcurrencyVersion = table.Column<long>(type: "bigint", nullable: false),
                    LeaseOwnerId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    LeaseEpoch = table.Column<long>(type: "bigint", nullable: false),
                    LeaseAcquiredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ProtectedStoredResult = table.Column<string>(type: "TEXT", nullable: false),
                    StoredResultHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FailureCode = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    SafeMessage = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFramework_WorkflowExecutorInvocations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AgentFramework_WorkflowExternalRequestBoundaries",
                columns: table => new
                {
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestVersion = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    ResponseContractJson = table.Column<string>(type: "TEXT", nullable: false),
                    ContinuationJson = table.Column<string>(type: "TEXT", nullable: false),
                    RequestPayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AuthorizationPolicyJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFramework_WorkflowExternalRequestBoundaries", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK_AF_WfRequestBoundaries_Requests",
                        column: x => x.RequestId,
                        principalTable: "AgentFramework_WorkflowExternalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentFramework_WorkflowExternalResponseOperations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    RunId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpectedRequestVersion = table.Column<long>(type: "bigint", nullable: false),
                    IdempotencyKeyHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ResponsePayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorScopeFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProtectedResponsePayload = table.Column<string>(type: "TEXT", nullable: false),
                    ActorKind = table.Column<int>(type: "integer", nullable: false),
                    ActorSubjectId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CorrelationId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    Attempt = table.Column<int>(type: "integer", nullable: false),
                    OperationVersion = table.Column<long>(type: "bigint", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LeaseOwnerId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    LeaseEpoch = table.Column<long>(type: "bigint", nullable: false),
                    LeaseAcquiredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LeaseExpiresAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    OutcomeCode = table.Column<int>(type: "integer", nullable: false),
                    SafeMessage = table.Column<string>(type: "TEXT", nullable: false),
                    FinalResultJson = table.Column<string>(type: "TEXT", nullable: true),
                    ReplayCount = table.Column<int>(type: "integer", nullable: false),
                    LastReplayedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ConcurrencyToken = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFramework_WorkflowExternalResponseOperations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AF_WfResponseOperations_Requests",
                        column: x => x.RequestId,
                        principalTable: "AgentFramework_WorkflowExternalRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AF_WfResponseOperations_Runs",
                        column: x => x.RunId,
                        principalTable: "AgentFramework_WorkflowRuns",
                        principalColumn: "RunId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AgentFramework_WorkflowBackendCheckpointPayloads",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    SessionId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    ParentCheckpointId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CommitOrdinal = table.Column<long>(type: "bigint", nullable: false),
                    ProtectedPayload = table.Column<string>(type: "TEXT", nullable: false),
                    PayloadHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ExternalRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    BackendRequestId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    BackendRequestPortId = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentFramework_WorkflowBackendCheckpointPayloads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WfBackendCheckpointPayloads_Parent",
                        column: x => x.ParentCheckpointId,
                        principalTable: "AgentFramework_WorkflowBackendCheckpointPayloads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WfBackendCheckpointPayloads_Sessions",
                        column: x => x.SessionId,
                        principalTable: "AgentFramework_WorkflowBackendCheckpointSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AF_WfBackendCheckpoints_Parent",
                table: "AgentFramework_WorkflowBackendCheckpointPayloads",
                column: "ParentCheckpointId");

            migrationBuilder.CreateIndex(
                name: "UX_AF_WfBackendCheckpoints_ExternalRequest",
                table: "AgentFramework_WorkflowBackendCheckpointPayloads",
                column: "ExternalRequestId",
                unique: true,
                filter: "\"ExternalRequestId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_AF_WfBackendCheckpoints_SessionOrdinal",
                table: "AgentFramework_WorkflowBackendCheckpointPayloads",
                columns: new[] { "SessionId", "CommitOrdinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AF_WfCheckpointSessions_WorkflowVersion",
                table: "AgentFramework_WorkflowBackendCheckpointSessions",
                columns: new[] { "WorkflowId", "WorkflowVersionId" });

            migrationBuilder.CreateIndex(
                name: "UX_AF_WfCheckpointSessions_Run",
                table: "AgentFramework_WorkflowBackendCheckpointSessions",
                column: "RunId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AF_WorkflowExecutorInvocations_Causation",
                table: "AgentFramework_WorkflowExecutorInvocations",
                columns: new[] { "RunId", "CausationRequestId", "CausationOperationId" });

            migrationBuilder.CreateIndex(
                name: "IX_AF_WorkflowExecutorInvocations_Lease",
                table: "AgentFramework_WorkflowExecutorInvocations",
                columns: new[] { "State", "LeaseExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "UX_AF_WorkflowExecutorInvocations_IdempotencyKey",
                table: "AgentFramework_WorkflowExecutorInvocations",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_AF_WorkflowExecutorInvocations_Key",
                table: "AgentFramework_WorkflowExecutorInvocations",
                column: "InvocationKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_AF_WorkflowExecutorInvocations_Scope",
                table: "AgentFramework_WorkflowExecutorInvocations",
                column: "ScopeKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AF_WfResponseOperations_Recovery",
                table: "AgentFramework_WorkflowExternalResponseOperations",
                columns: new[] { "State", "LeaseExpiresAtUtc", "AcceptedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AF_WfResponseOperations_Run",
                table: "AgentFramework_WorkflowExternalResponseOperations",
                column: "RunId");

            migrationBuilder.CreateIndex(
                name: "UX_AF_WfResponseOperations_Fingerprint",
                table: "AgentFramework_WorkflowExternalResponseOperations",
                columns: new[] { "RequestId", "IdempotencyKeyHash", "ActorScopeFingerprint" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_AF_WfResponseOperations_Request",
                table: "AgentFramework_WorkflowExternalResponseOperations",
                column: "RequestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowBackendCheckpointPayloads");

            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowExecutorInvocations");

            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowExternalRequestBoundaries");

            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowExternalResponseOperations");

            migrationBuilder.DropTable(
                name: "AgentFramework_WorkflowBackendCheckpointSessions");
        }
    }
}
