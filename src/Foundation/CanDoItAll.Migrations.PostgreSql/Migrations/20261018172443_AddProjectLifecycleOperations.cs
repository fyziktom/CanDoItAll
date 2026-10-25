using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddProjectLifecycleOperations : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                SET LOCAL lock_timeout = '5s';
                DO $lifecycle_preflight$
                DECLARE
                    projects_pool constant uuid := 'e6eaf1c1-5dbc-46bf-8641-1ae9fe3ae376';
                    relation_name text;
                    relation_id regclass;
                    canonical_schema name := current_schema();
                BEGIN
                    IF current_setting('transaction_isolation') <> 'read committed' THEN
                        RAISE EXCEPTION 'Project lifecycle installation requires a ReadCommitted migration transaction.';
                    END IF;
                    FOREACH relation_name IN ARRAY ARRAY[
                        'Infrastructure_DatasetLifecycle', 'Infrastructure_FiniteOperationState',
                        'Infrastructure_FiniteOperationEpochs', 'Projects_DefinitionReceipts',
                        'Projects_ProjectCreationReservations', 'AgentFramework_ProjectAccessRevocations'] LOOP
                        relation_id := to_regclass(format('%I', relation_name));
                        IF relation_id IS NULL OR relation_id IS DISTINCT FROM to_regclass(format('%I.%I', canonical_schema, relation_name))
                            OR NOT EXISTS (SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
                                WHERE c.oid = relation_id AND n.nspname = canonical_schema AND c.relkind = 'r'
                                    AND c.relpersistence = 'p' AND NOT c.relispartition AND NOT c.relrowsecurity AND NOT c.relforcerowsecurity) THEN
                            RAISE EXCEPTION 'Project lifecycle installation found an unsupported predecessor relation.';
                        END IF;
                        EXECUTE format('LOCK TABLE %I.%I IN ACCESS EXCLUSIVE MODE', canonical_schema, relation_name);
                        IF relation_id IS DISTINCT FROM to_regclass(format('%I.%I', canonical_schema, relation_name))
                            OR EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid = relation_id AND NOT tgisinternal) THEN
                            RAISE EXCEPTION 'Project lifecycle installation found changed relation identity or a user trigger.';
                        END IF;
                    END LOOP;
                    IF EXISTS (SELECT 1 FROM pg_constraint WHERE contype = 'f' AND (
                        conrelid IN ('"Infrastructure_FiniteOperationState"'::regclass, '"Infrastructure_FiniteOperationEpochs"'::regclass, '"Projects_DefinitionReceipts"'::regclass)
                        OR confrelid IN ('"Infrastructure_FiniteOperationState"'::regclass, '"Infrastructure_FiniteOperationEpochs"'::regclass, '"Projects_DefinitionReceipts"'::regclass)))
                        OR EXISTS (SELECT 1 FROM "Infrastructure_FiniteOperationState"
                            WHERE "PoolId" = projects_pool AND "RetentionProtocolVersion" <> 1)
                        OR EXISTS (SELECT 1 FROM "Infrastructure_FiniteOperationEpochs" e WHERE e."PoolId" = projects_pool
                            AND NOT EXISTS (SELECT 1 FROM "Infrastructure_FiniteOperationState" s
                                WHERE s."NamespaceId" = e."NamespaceId" AND s."PoolId" = e."PoolId"))
                        OR EXISTS (SELECT 1 FROM "Projects_DefinitionReceipts" r WHERE NOT EXISTS (
                            SELECT 1 FROM "Infrastructure_FiniteOperationEpochs" e
                            WHERE e."PoolId" = projects_pool AND e."NamespaceId" = r."NamespaceId" AND e."Epoch" = r."Epoch")) THEN
                        RAISE EXCEPTION 'Project lifecycle installation requires intact version-one Projects metadata without foreign-key side effects.';
                    END IF;
                END;
                $lifecycle_preflight$;
                """);
            migrationBuilder.AddColumn<Guid>(
                name: "NamespaceId",
                table: "Projects_ProjectCreationReservations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompletionId",
                table: "AgentFramework_ProjectAccessRevocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompletionNamespaceId",
                table: "AgentFramework_ProjectAccessRevocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Projects_DeletionCompletions",
                columns: table => new
                {
                    NamespaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DatabaseProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    LifetimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    OriginKind = table.Column<int>(type: "integer", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReleaseState = table.Column<int>(type: "integer", nullable: false),
                    ReleasedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReferencePayload = table.Column<byte[]>(type: "bytea", nullable: false),
                    RecoveryPayload = table.Column<byte[]>(type: "bytea", nullable: false),
                    RecoveryDigest = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects_DeletionCompletions", x => new { x.NamespaceId, x.CompletionId });
                    table.CheckConstraint("CK_Projects_DeletionCompletion_Address", "\"NamespaceId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"CompletionId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("CK_Projects_DeletionCompletion_Interpretation", "\"SchemaVersion\" = 1 AND \"OriginKind\" IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_Projects_DeletionCompletion_Recovery", "octet_length(\"RecoveryPayload\") BETWEEN 1 AND 65536 AND octet_length(\"RecoveryDigest\") = 32");
                    table.CheckConstraint("CK_Projects_DeletionCompletion_References", "octet_length(\"ReferencePayload\") BETWEEN 1 AND 16384");
                    table.CheckConstraint("CK_Projects_DeletionCompletion_Release", "isfinite(\"CreatedAtUtc\") AND ((\"ReleaseState\" = 1 AND \"ReleasedAtUtc\" IS NULL) OR (\"ReleaseState\" = 2 AND \"ReleasedAtUtc\" IS NOT NULL AND isfinite(\"ReleasedAtUtc\")))");
                    table.CheckConstraint("CK_Projects_DeletionCompletion_Target", "\"DatabaseProfileId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"ProjectId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"LifetimeId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                });

            migrationBuilder.CreateTable(
                name: "Projects_LifecycleReceipts",
                columns: table => new
                {
                    NamespaceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Epoch = table.Column<long>(type: "bigint", nullable: false),
                    CallerKind = table.Column<int>(type: "integer", nullable: false),
                    CallerKey = table.Column<byte[]>(type: "bytea", nullable: false),
                    OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                    DatabaseProfileId = table.Column<Guid>(type: "uuid", nullable: false),
                    SchemaVersion = table.Column<int>(type: "integer", nullable: false),
                    NormalizationVersion = table.Column<int>(type: "integer", nullable: false),
                    ResultVersion = table.Column<int>(type: "integer", nullable: false),
                    CommandKind = table.Column<int>(type: "integer", nullable: false),
                    SemanticDigest = table.Column<byte[]>(type: "bytea", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    LifetimeId = table.Column<Guid>(type: "uuid", nullable: false),
                    AcceptedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    AcceptedPayload = table.Column<byte[]>(type: "bytea", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects_LifecycleReceipts", x => new { x.NamespaceId, x.Epoch, x.CallerKind, x.CallerKey, x.OperationId });
                    table.CheckConstraint("CK_Projects_LifecycleReceipt_Address", "\"NamespaceId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"OperationId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"DatabaseProfileId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"Epoch\" > 0");
                    table.CheckConstraint("CK_Projects_LifecycleReceipt_Caller", "\"CallerKind\" IN (1, 2, 3, 4) AND octet_length(\"CallerKey\") = 32");
                    table.CheckConstraint("CK_Projects_LifecycleReceipt_Digest", "octet_length(\"SemanticDigest\") = 32");
                    table.CheckConstraint("CK_Projects_LifecycleReceipt_Interpretation", "\"SchemaVersion\" > 0 AND \"NormalizationVersion\" > 0 AND \"ResultVersion\" > 0 AND \"CommandKind\" IN (1, 2, 3, 4)");
                    table.CheckConstraint("CK_Projects_LifecycleReceipt_Payload", "octet_length(\"AcceptedPayload\") BETWEEN 1 AND 16384");
                    table.CheckConstraint("CK_Projects_LifecycleReceipt_Target", "\"ProjectId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"LifetimeId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
                    table.CheckConstraint("CK_Projects_LifecycleReceipt_Time", "isfinite(\"AcceptedAtUtc\")");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Projects_CreationReservation_Namespace",
                table: "Projects_ProjectCreationReservations",
                sql: "\"NamespaceId\" IS NULL OR \"NamespaceId\" <> '00000000-0000-0000-0000-000000000000'::uuid");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AF_ProjectAccessRevocations_Completion",
                table: "AgentFramework_ProjectAccessRevocations",
                sql: "(\"CompletionNamespaceId\" IS NULL AND \"CompletionId\" IS NULL) OR (\"CompletionNamespaceId\" IS NOT NULL AND \"CompletionId\" IS NOT NULL AND \"CompletionNamespaceId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"CompletionId\" <> '00000000-0000-0000-0000-000000000000'::uuid AND \"DatabaseProfileId\" IS NOT NULL AND \"ProjectLifetimeId\" IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_DeletionCompletions_NamespaceId_ProjectId_Lifetime~",
                table: "Projects_DeletionCompletions",
                columns: new[] { "NamespaceId", "ProjectId", "LifetimeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_DeletionCompletions_NamespaceId_ReleaseState_Creat~",
                table: "Projects_DeletionCompletions",
                columns: new[] { "NamespaceId", "ReleaseState", "CreatedAtUtc" });

            migrationBuilder.Sql("""
                UPDATE "Infrastructure_FiniteOperationState"
                SET "RetentionProtocolVersion" = 2
                WHERE "PoolId" = 'e6eaf1c1-5dbc-46bf-8641-1ae9fe3ae376'::uuid;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                DO $lifecycle_down$
                BEGIN
                    RAISE EXCEPTION 'Project lifecycle downgrade is unsupported. Restore a compatible backup and binaries at a stopped-host maintenance boundary.';
                END;
                $lifecycle_down$;
                """);
        }
    }
}
