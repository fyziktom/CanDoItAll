using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddProjectDefinitionOperationLifecycle : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.CreateTable(
                name: "Infrastructure_DatasetLifecycle",
                columns: table => new {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    NamespaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaintenanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    GuardProtocol = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_Infrastructure_DatasetLifecycle", x => x.Id);
                    table.CheckConstraint("CK_DatasetLifecycle_Maintenance", "\"MaintenanceId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("CK_DatasetLifecycle_Namespace", "\"NamespaceId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("CK_DatasetLifecycle_Singleton", "\"Id\" = 1");
                    table.CheckConstraint("CK_DatasetLifecycle_State", "\"State\" IN (1, 2)");
                    table.CheckConstraint("CK_DatasetLifecycle_Versions", "\"SchemaVersion\" > 0 AND \"GuardProtocol\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "Projects_DefinitionReceipts",
                columns: table => new {
                    NamespaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Epoch = table.Column<long>(type: "bigint", nullable: false),
                    CallerKind = table.Column<int>(type: "integer", nullable: false),
                    CallerKey = table.Column<byte[]>(type: "bytea", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    NormalizationVersion = table.Column<int>(type: "integer", nullable: false),
                    ResultVersion = table.Column<int>(type: "integer", nullable: false),
                    CommandKind = table.Column<int>(type: "integer", nullable: false),
                    SemanticDigest = table.Column<byte[]>(type: "bytea", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    LifetimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcceptedRevision = table.Column<Guid>(type: "uuid", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedPayload = table.Column<byte[]>(type: "bytea", nullable: false),
                    CreationFingerprint = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table => {
                    table.PrimaryKey("PK_Projects_DefinitionReceipts", x => new { x.NamespaceId, x.Epoch, x.CallerKind, x.CallerKey, x.OperationId });
                    table.CheckConstraint("CK_Projects_DefinitionReceipt_Address", "\"NamespaceId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"OperationId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"Epoch\" > 0");
                    table.CheckConstraint("CK_Projects_DefinitionReceipt_Caller", "\"CallerKind\" IN (1, 2, 3, 4) AND octet_length(\"CallerKey\") = 32");
                    table.CheckConstraint("CK_Projects_DefinitionReceipt_Creation", "(\"CommandKind\" = 1 AND \"CreationFingerprint\" IS NOT NULL AND \"CreationFingerprint\" ~ '^[0-9a-f]{64}$') OR (\"CommandKind\" <> 1 AND \"CreationFingerprint\" IS NULL)");
                    table.CheckConstraint("CK_Projects_DefinitionReceipt_Digest", "octet_length(\"SemanticDigest\") = 32");
                    table.CheckConstraint("CK_Projects_DefinitionReceipt_Interpretation", "\"SchemaVersion\" > 0 AND \"NormalizationVersion\" > 0 AND \"ResultVersion\" > 0 AND \"CommandKind\" IN (1, 2, 3)");
                    table.CheckConstraint("CK_Projects_DefinitionReceipt_Payload", "octet_length(\"AcceptedPayload\") BETWEEN 1 AND 2097152");
                    table.CheckConstraint("CK_Projects_DefinitionReceipt_Target", "\"ProjectId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"LifetimeId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"AcceptedRevision\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                });

            migrationBuilder.CreateTable(
                name: "Projects_OperationEpochs",
                columns: table => new {
                    NamespaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Epoch = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    OpenedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AdmissionClosesAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ClosedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    MutationDeadlineMilliseconds = table.Column<int>(type: "integer", nullable: false),
                    ReplayRetentionSeconds = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_Projects_OperationEpochs", x => new { x.NamespaceId, x.Epoch });
                    table.CheckConstraint("CK_Projects_OperationEpoch_Deadline", "\"MutationDeadlineMilliseconds\" > 0 AND \"ReplayRetentionSeconds\" > 0 AND \"AdmissionClosesAtUtc\" > \"OpenedAtUtc\"");
                    table.CheckConstraint("CK_Projects_OperationEpoch_Namespace", "\"NamespaceId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("CK_Projects_OperationEpoch_Number", "\"Epoch\" > 0");
                    table.CheckConstraint("CK_Projects_OperationEpoch_State", "(\"State\" = 1 AND \"ClosedAtUtc\" IS NULL) OR (\"State\" = 2 AND \"ClosedAtUtc\" IS NOT NULL AND \"ClosedAtUtc\" >= \"OpenedAtUtc\")");
                });

            migrationBuilder.CreateTable(
                name: "Projects_OperationState",
                columns: table => new {
                    NamespaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentEpoch = table.Column<long>(type: "bigint", nullable: false),
                    RetiredThroughEpoch = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table => {
                    table.PrimaryKey("PK_Projects_OperationState", x => x.NamespaceId);
                    table.CheckConstraint("CK_Projects_OperationState_Epoch", "\"CurrentEpoch\" > 0 AND \"RetiredThroughEpoch\" >= 0 AND \"RetiredThroughEpoch\" < \"CurrentEpoch\"");
                    table.CheckConstraint("CK_Projects_OperationState_Namespace", "\"NamespaceId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OperationEpochs_NamespaceId",
                table: "Projects_OperationEpochs",
                column: "NamespaceId",
                unique: true,
                filter: "\"State\" = 1");
        }
        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.DropTable(
                name: "Infrastructure_DatasetLifecycle");

            migrationBuilder.DropTable(
                name: "Projects_DefinitionReceipts");

            migrationBuilder.DropTable(
                name: "Projects_OperationEpochs");

            migrationBuilder.DropTable(
                name: "Projects_OperationState");
        }
    }
}
