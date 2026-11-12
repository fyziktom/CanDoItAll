using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddWorkbenchTaskPlanReceipts : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                DO $c3$
                DECLARE
                    projects_pool constant uuid := 'e6eaf1c1-5dbc-46bf-8641-1ae9fe3ae376';
                    workbench_pool constant uuid := 'c91aaef3-cfc1-4c29-88aa-10693fecd4e3';
                    lifecycle_id regclass := to_regclass('"Infrastructure_DatasetLifecycle"');
                    state_id regclass := to_regclass('"Infrastructure_FiniteOperationState"');
                    epoch_id regclass := to_regclass('"Infrastructure_FiniteOperationEpochs"');
                    receipt_id regclass;
                    projects_receipt_id regclass;
                    canonical_schema name;
                    canonical_schema_id oid;
                    original_lock_timeout text := current_setting('lock_timeout');
                    active_namespace uuid;
                    issued_at timestamp with time zone;
                    allowed_pools uuid[];
                    pair record;
                    phase integer;
                BEGIN
                    IF current_setting('transaction_isolation') <> 'read committed' THEN
                        RAISE EXCEPTION USING ERRCODE = 'P0001',
                            MESSAGE = 'Workbench task-plan installation requires a ReadCommitted migration transaction.';
                    END IF;
                    IF lifecycle_id IS NULL OR state_id IS NULL OR epoch_id IS NULL THEN
                        RAISE EXCEPTION USING ERRCODE = 'P0001',
                            MESSAGE = 'Workbench task-plan installation requires the exact finite-operation predecessor.';
                    END IF;
                    SELECT n.oid, n.nspname INTO STRICT canonical_schema_id, canonical_schema
                    FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE c.oid = lifecycle_id;
                    IF canonical_schema <> current_schema()
                        OR EXISTS (SELECT 1 FROM pg_class WHERE oid IN (lifecycle_id, state_id, epoch_id)
                            AND (relnamespace <> canonical_schema_id OR relpersistence <> 'p' OR relkind <> 'r'
                                OR relispartition OR relrowsecurity OR relforcerowsecurity))
                        OR lifecycle_id IS DISTINCT FROM to_regclass(format('%I.%I', canonical_schema, 'Infrastructure_DatasetLifecycle'))
                        OR state_id IS DISTINCT FROM to_regclass(format('%I.%I', canonical_schema, 'Infrastructure_FiniteOperationState'))
                        OR epoch_id IS DISTINCT FROM to_regclass(format('%I.%I', canonical_schema, 'Infrastructure_FiniteOperationEpochs'))
                        OR to_regclass('"Workbench_TaskPlanReceipts"') IS NOT NULL
                        OR to_regclass(format('%I.%I', canonical_schema, 'Workbench_TaskPlanReceipts')) IS NOT NULL
                        OR to_regclass('"Projects_OperationState"') IS NOT NULL
                        OR to_regclass('"Projects_OperationEpochs"') IS NOT NULL
                        OR to_regclass(format('%I.%I', canonical_schema, 'Projects_OperationState')) IS NOT NULL
                        OR to_regclass(format('%I.%I', canonical_schema, 'Projects_OperationEpochs')) IS NOT NULL THEN
                        RAISE EXCEPTION USING ERRCODE = 'P0001',
                            MESSAGE = 'Workbench task-plan installation found unsupported schema resolution or pre-existing destination metadata.';
                    END IF;
                    PERFORM set_config('lock_timeout', '5s', true);
                    LOCK TABLE "Infrastructure_DatasetLifecycle" IN ACCESS EXCLUSIVE MODE;
                    LOCK TABLE "Infrastructure_FiniteOperationState" IN ACCESS EXCLUSIVE MODE;
                    LOCK TABLE "Infrastructure_FiniteOperationEpochs" IN ACCESS EXCLUSIVE MODE;

                    CREATE TEMP TABLE c3_expected_lifecycle (
                        "Id" integer NOT NULL,
                        "NamespaceId" uuid NOT NULL,
                        "MaintenanceId" uuid NOT NULL,
                        "SchemaVersion" integer NOT NULL,
                        "GuardProtocol" integer NOT NULL,
                        "State" integer NOT NULL,
                        CONSTRAINT "PK_Infrastructure_DatasetLifecycle" PRIMARY KEY ("Id"),
                        CONSTRAINT "CK_DatasetLifecycle_Maintenance" CHECK ("MaintenanceId" <> '00000000-0000-0000-0000-000000000000'::uuid),
                        CONSTRAINT "CK_DatasetLifecycle_Namespace" CHECK ("NamespaceId" <> '00000000-0000-0000-0000-000000000000'::uuid),
                        CONSTRAINT "CK_DatasetLifecycle_Singleton" CHECK ("Id" = 1),
                        CONSTRAINT "CK_DatasetLifecycle_State" CHECK ("State" IN (1, 2)),
                        CONSTRAINT "CK_DatasetLifecycle_Versions" CHECK ("SchemaVersion" > 0 AND "GuardProtocol" > 0)
                    ) ON COMMIT DROP;
                    CREATE TEMP TABLE c3_expected_state (
                        "NamespaceId" uuid NOT NULL,
                        "PoolId" uuid NOT NULL,
                        "RetentionProtocolVersion" integer NOT NULL,
                        "CurrentEpoch" bigint NOT NULL,
                        "RetiredThroughEpoch" bigint NOT NULL,
                        CONSTRAINT "PK_Infrastructure_FiniteOperationState" PRIMARY KEY ("NamespaceId", "PoolId"),
                        CONSTRAINT "CK_Infrastructure_FiniteOperationState_Epoch" CHECK ("CurrentEpoch" > 0 AND "RetiredThroughEpoch" >= 0 AND "RetiredThroughEpoch" < "CurrentEpoch"),
                        CONSTRAINT "CK_Infrastructure_FiniteOperationState_Namespace" CHECK ("NamespaceId" <> '00000000-0000-0000-0000-000000000000'::uuid),
                        CONSTRAINT "CK_Infrastructure_FiniteOperationState_Pool" CHECK ("PoolId" <> '00000000-0000-0000-0000-000000000000'::uuid),
                        CONSTRAINT "CK_Infrastructure_FiniteOperationState_Protocol" CHECK ("RetentionProtocolVersion" > 0)
                    ) ON COMMIT DROP;
                    CREATE TEMP TABLE c3_expected_epochs (
                        "NamespaceId" uuid NOT NULL,
                        "PoolId" uuid NOT NULL,
                        "Epoch" bigint NOT NULL,
                        "State" integer NOT NULL,
                        "OpenedAtUtc" timestamp with time zone NOT NULL,
                        "AdmissionClosesAtUtc" timestamp with time zone NOT NULL,
                        "ClosedAtUtc" timestamp with time zone,
                        "MutationDeadlineMilliseconds" integer NOT NULL,
                        "ReplayRetentionSeconds" bigint NOT NULL,
                        CONSTRAINT "PK_Infrastructure_FiniteOperationEpochs" PRIMARY KEY ("NamespaceId", "PoolId", "Epoch"),
                        CONSTRAINT "CK_Infrastructure_FiniteOperationEpoch_Deadline" CHECK ("MutationDeadlineMilliseconds" > 0 AND "ReplayRetentionSeconds" > 0 AND "AdmissionClosesAtUtc" > "OpenedAtUtc"),
                        CONSTRAINT "CK_Infrastructure_FiniteOperationEpoch_Namespace" CHECK ("NamespaceId" <> '00000000-0000-0000-0000-000000000000'::uuid),
                        CONSTRAINT "CK_Infrastructure_FiniteOperationEpoch_Number" CHECK ("Epoch" > 0),
                        CONSTRAINT "CK_Infrastructure_FiniteOperationEpoch_Pool" CHECK ("PoolId" <> '00000000-0000-0000-0000-000000000000'::uuid),
                        CONSTRAINT "CK_Infrastructure_FiniteOperationEpoch_State" CHECK (("State" = 1 AND "ClosedAtUtc" IS NULL) OR ("State" = 2 AND "ClosedAtUtc" IS NOT NULL AND "ClosedAtUtc" >= "OpenedAtUtc"))
                    ) ON COMMIT DROP;
                    CREATE UNIQUE INDEX "IX_Infrastructure_FiniteOperationEpochs_NamespaceId_PoolId"
                        ON c3_expected_epochs ("NamespaceId", "PoolId") WHERE "State" = 1;
                    CREATE TEMP TABLE c3_expected_receipt (
                        "NamespaceId" uuid NOT NULL,
                        "Epoch" bigint NOT NULL,
                        "CallerKind" integer NOT NULL,
                        "CallerKey" bytea NOT NULL,
                        "OperationId" uuid NOT NULL,
                        "SchemaVersion" integer NOT NULL,
                        "NormalizationVersion" integer NOT NULL,
                        "ResultVersion" integer NOT NULL,
                        "SemanticDigest" bytea NOT NULL,
                        "ProjectId" uuid NOT NULL,
                        "LifetimeId" uuid NOT NULL,
                        "OriginalDefinitionRevision" uuid NOT NULL,
                        "DecisionId" uuid NOT NULL,
                        "DecisionGeneration" bigint NOT NULL,
                        "SourceKind" integer NOT NULL,
                        "SourceVersion" integer NOT NULL,
                        "SourceFingerprint" bytea NOT NULL,
                        "AcceptedAtUtc" timestamp with time zone NOT NULL,
                        "AcceptedPayload" bytea NOT NULL,
                        CONSTRAINT "PK_Workbench_TaskPlanReceipts" PRIMARY KEY ("NamespaceId", "Epoch", "CallerKind", "CallerKey", "OperationId"),
                        CONSTRAINT "CK_Workbench_TaskPlanReceipt_Address" CHECK ("NamespaceId" <> '00000000-0000-0000-0000-000000000000'::uuid AND "OperationId" <> '00000000-0000-0000-0000-000000000000'::uuid AND "Epoch" > 0),
                        CONSTRAINT "CK_Workbench_TaskPlanReceipt_Caller" CHECK ("CallerKind" = 1 AND octet_length("CallerKey") = 32),
                        CONSTRAINT "CK_Workbench_TaskPlanReceipt_Digest" CHECK (octet_length("SemanticDigest") = 32 AND octet_length("SourceFingerprint") = 32),
                        CONSTRAINT "CK_Workbench_TaskPlanReceipt_Interpretation" CHECK ("SchemaVersion" > 0 AND "NormalizationVersion" > 0 AND "ResultVersion" > 0 AND "SourceVersion" > 0 AND "SourceKind" = 1),
                        CONSTRAINT "CK_Workbench_TaskPlanReceipt_Target" CHECK ("ProjectId" <> '00000000-0000-0000-0000-000000000000'::uuid AND "LifetimeId" <> '00000000-0000-0000-0000-000000000000'::uuid AND "OriginalDefinitionRevision" <> '00000000-0000-0000-0000-000000000000'::uuid),
                        CONSTRAINT "CK_Workbench_TaskPlanReceipt_Decision" CHECK ("DecisionId" <> '00000000-0000-0000-0000-000000000000'::uuid AND "DecisionGeneration" > 0),
                        CONSTRAINT "CK_Workbench_TaskPlanReceipt_Payload" CHECK (octet_length("AcceptedPayload") BETWEEN 1 AND 2097152)
                    ) ON COMMIT DROP;

                    FOR phase IN 1..2 LOOP
                        IF phase = 2 THEN
                            CREATE TABLE "Workbench_TaskPlanReceipts" (
                                "NamespaceId" uuid NOT NULL,
                                "Epoch" bigint NOT NULL,
                                "CallerKind" integer NOT NULL,
                                "CallerKey" bytea NOT NULL,
                                "OperationId" uuid NOT NULL,
                                "SchemaVersion" integer NOT NULL,
                                "NormalizationVersion" integer NOT NULL,
                                "ResultVersion" integer NOT NULL,
                                "SemanticDigest" bytea NOT NULL,
                                "ProjectId" uuid NOT NULL,
                                "LifetimeId" uuid NOT NULL,
                                "OriginalDefinitionRevision" uuid NOT NULL,
                                "DecisionId" uuid NOT NULL,
                                "DecisionGeneration" bigint NOT NULL,
                                "SourceKind" integer NOT NULL,
                                "SourceVersion" integer NOT NULL,
                                "SourceFingerprint" bytea NOT NULL,
                                "AcceptedAtUtc" timestamp with time zone NOT NULL,
                                "AcceptedPayload" bytea NOT NULL,
                                CONSTRAINT "PK_Workbench_TaskPlanReceipts" PRIMARY KEY ("NamespaceId", "Epoch", "CallerKind", "CallerKey", "OperationId"),
                                CONSTRAINT "CK_Workbench_TaskPlanReceipt_Address" CHECK ("NamespaceId" <> '00000000-0000-0000-0000-000000000000'::uuid AND "OperationId" <> '00000000-0000-0000-0000-000000000000'::uuid AND "Epoch" > 0),
                                CONSTRAINT "CK_Workbench_TaskPlanReceipt_Caller" CHECK ("CallerKind" = 1 AND octet_length("CallerKey") = 32),
                                CONSTRAINT "CK_Workbench_TaskPlanReceipt_Decision" CHECK ("DecisionId" <> '00000000-0000-0000-0000-000000000000'::uuid AND "DecisionGeneration" > 0),
                                CONSTRAINT "CK_Workbench_TaskPlanReceipt_Digest" CHECK (octet_length("SemanticDigest") = 32 AND octet_length("SourceFingerprint") = 32),
                                CONSTRAINT "CK_Workbench_TaskPlanReceipt_Interpretation" CHECK ("SchemaVersion" > 0 AND "NormalizationVersion" > 0 AND "ResultVersion" > 0 AND "SourceVersion" > 0 AND "SourceKind" = 1),
                                CONSTRAINT "CK_Workbench_TaskPlanReceipt_Payload" CHECK (octet_length("AcceptedPayload") BETWEEN 1 AND 2097152),
                                CONSTRAINT "CK_Workbench_TaskPlanReceipt_Target" CHECK ("ProjectId" <> '00000000-0000-0000-0000-000000000000'::uuid AND "LifetimeId" <> '00000000-0000-0000-0000-000000000000'::uuid AND "OriginalDefinitionRevision" <> '00000000-0000-0000-0000-000000000000'::uuid)
                            );

                            receipt_id := to_regclass(format('%I.%I', canonical_schema, 'Workbench_TaskPlanReceipts'));
                            IF receipt_id IS NULL OR receipt_id IS DISTINCT FROM to_regclass('"Workbench_TaskPlanReceipts"') THEN
                                RAISE EXCEPTION USING ERRCODE = 'P0001',
                                    MESSAGE = 'Workbench task-plan installation did not create its exact canonical receipt relation.';
                            END IF;
                            IF active_namespace IS NOT NULL THEN
                                issued_at := clock_timestamp();
                                INSERT INTO "Infrastructure_FiniteOperationState"
                                    ("NamespaceId", "PoolId", "RetentionProtocolVersion", "CurrentEpoch", "RetiredThroughEpoch")
                                VALUES (active_namespace, workbench_pool, 1, 1, 0);
                                INSERT INTO "Infrastructure_FiniteOperationEpochs"
                                    ("NamespaceId", "PoolId", "Epoch", "State", "OpenedAtUtc", "AdmissionClosesAtUtc",
                                        "ClosedAtUtc", "MutationDeadlineMilliseconds", "ReplayRetentionSeconds")
                                VALUES (active_namespace, workbench_pool, 1, 1, issued_at,
                                    issued_at + interval '86400 seconds', NULL, 30000, 604800);
                            END IF;
                        END IF;

                        IF lifecycle_id IS DISTINCT FROM to_regclass('"Infrastructure_DatasetLifecycle"')
                            OR state_id IS DISTINCT FROM to_regclass('"Infrastructure_FiniteOperationState"')
                            OR epoch_id IS DISTINCT FROM to_regclass('"Infrastructure_FiniteOperationEpochs"')
                            OR lifecycle_id IS DISTINCT FROM to_regclass(format('%I.%I', canonical_schema, 'Infrastructure_DatasetLifecycle'))
                            OR state_id IS DISTINCT FROM to_regclass(format('%I.%I', canonical_schema, 'Infrastructure_FiniteOperationState'))
                            OR epoch_id IS DISTINCT FROM to_regclass(format('%I.%I', canonical_schema, 'Infrastructure_FiniteOperationEpochs'))
                            OR to_regclass('"Projects_OperationState"') IS NOT NULL
                            OR to_regclass('"Projects_OperationEpochs"') IS NOT NULL
                            OR to_regclass(format('%I.%I', canonical_schema, 'Projects_OperationState')) IS NOT NULL
                            OR to_regclass(format('%I.%I', canonical_schema, 'Projects_OperationEpochs')) IS NOT NULL THEN
                            RAISE EXCEPTION USING ERRCODE = 'P0001',
                                MESSAGE = 'Workbench task-plan installation lost exact predecessor schema correspondence.';
                        END IF;
                        FOR pair IN SELECT * FROM (VALUES
                            (lifecycle_id, 'pg_temp.c3_expected_lifecycle'::regclass),
                            (state_id, 'pg_temp.c3_expected_state'::regclass),
                            (epoch_id, 'pg_temp.c3_expected_epochs'::regclass),
                            (receipt_id, 'pg_temp.c3_expected_receipt'::regclass)
                        ) AS relations(actual, expected) WHERE actual IS NOT NULL LOOP
                            IF EXISTS (SELECT 1 FROM pg_class WHERE oid = pair.actual
                                    AND (relnamespace <> canonical_schema_id OR relpersistence <> 'p' OR relkind <> 'r'
                                        OR relispartition OR relrowsecurity OR relforcerowsecurity))
                                OR EXISTS (SELECT 1 FROM pg_inherits WHERE inhrelid = pair.actual OR inhparent = pair.actual)
                                OR EXISTS (SELECT 1 FROM pg_rewrite WHERE ev_class = pair.actual) THEN
                                RAISE EXCEPTION USING ERRCODE = 'P0001',
                                    MESSAGE = 'Workbench task-plan installation requires ordinary canonical tables without inheritance, row security or rules.';
                            END IF;
                            IF pair.actual IN (state_id, epoch_id, receipt_id) AND (
                                EXISTS (SELECT 1 FROM pg_constraint WHERE contype = 'f'
                                    AND (conrelid = pair.actual OR confrelid = pair.actual))
                                OR EXISTS (SELECT 1 FROM pg_trigger WHERE NOT tgisinternal AND tgrelid = pair.actual)) THEN
                                RAISE EXCEPTION USING ERRCODE = 'P0001',
                                    MESSAGE = 'Workbench task-plan metadata cannot have cross-owner foreign keys or user triggers.';
                            END IF;
                            IF EXISTS (
                                WITH actual AS (
                                    SELECT a.attname, a.atttypid, a.atttypmod, a.attnotnull, a.attidentity, a.attgenerated, a.attcollation,
                                        pg_get_expr(d.adbin, d.adrelid, false) AS default_expression
                                    FROM pg_attribute a LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
                                    WHERE a.attrelid = pair.actual AND a.attnum > 0 AND NOT a.attisdropped
                                ), expected AS (
                                    SELECT a.attname, a.atttypid, a.atttypmod, a.attnotnull, a.attidentity, a.attgenerated, a.attcollation,
                                        pg_get_expr(d.adbin, d.adrelid, false) AS default_expression
                                    FROM pg_attribute a LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
                                    WHERE a.attrelid = pair.expected AND a.attnum > 0 AND NOT a.attisdropped
                                ) (TABLE actual EXCEPT ALL TABLE expected) UNION ALL (TABLE expected EXCEPT ALL TABLE actual)
                            ) THEN
                                RAISE EXCEPTION USING ERRCODE = 'P0001',
                                    MESSAGE = 'Workbench task-plan installation found unexpected columns, nullability or defaults.';
                            END IF;
                            IF EXISTS (
                                WITH actual AS (
                                    SELECT conname, contype, condeferrable, condeferred, conenforced, convalidated, connoinherit,
                                        pg_get_constraintdef(oid, false) AS definition
                                    FROM pg_constraint WHERE conrelid = pair.actual AND contype <> 'n'
                                ), expected AS (
                                    SELECT conname, contype, condeferrable, condeferred, conenforced, convalidated, connoinherit,
                                        pg_get_constraintdef(oid, false) AS definition
                                    FROM pg_constraint WHERE conrelid = pair.expected AND contype <> 'n'
                                ) (TABLE actual EXCEPT ALL TABLE expected) UNION ALL (TABLE expected EXCEPT ALL TABLE actual)
                            ) OR EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = pair.actual
                                AND contype = 'n' AND (NOT conenforced OR NOT convalidated)) THEN
                                RAISE EXCEPTION USING ERRCODE = 'P0001',
                                    MESSAGE = 'Workbench task-plan installation found unexpected keys or check constraints.';
                            END IF;
                            IF EXISTS (
                                WITH actual AS (
                                    SELECT c.relname, c.relam, i.indnatts, i.indnkeyatts, i.indisunique, i.indisprimary, i.indisexclusion,
                                        i.indimmediate, i.indisvalid, i.indisready, i.indislive, i.indnullsnotdistinct,
                                        i.indcollation, i.indclass, i.indoption, pg_get_expr(i.indpred, i.indrelid, false) AS predicate,
                                        pg_get_expr(i.indexprs, i.indrelid, false) AS expressions,
                                        ARRAY(SELECT pg_get_indexdef(i.indexrelid, key.ordinality::integer, false)
                                            FROM unnest(i.indkey) WITH ORDINALITY AS key(value, ordinality) ORDER BY key.ordinality) AS columns
                                    FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid WHERE i.indrelid = pair.actual
                                ), expected AS (
                                    SELECT c.relname, c.relam, i.indnatts, i.indnkeyatts, i.indisunique, i.indisprimary, i.indisexclusion,
                                        i.indimmediate, i.indisvalid, i.indisready, i.indislive, i.indnullsnotdistinct,
                                        i.indcollation, i.indclass, i.indoption, pg_get_expr(i.indpred, i.indrelid, false) AS predicate,
                                        pg_get_expr(i.indexprs, i.indrelid, false) AS expressions,
                                        ARRAY(SELECT pg_get_indexdef(i.indexrelid, key.ordinality::integer, false)
                                            FROM unnest(i.indkey) WITH ORDINALITY AS key(value, ordinality) ORDER BY key.ordinality) AS columns
                                    FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid WHERE i.indrelid = pair.expected
                                ) (TABLE actual EXCEPT ALL TABLE expected) UNION ALL (TABLE expected EXCEPT ALL TABLE actual)
                            ) THEN
                                RAISE EXCEPTION USING ERRCODE = 'P0001',
                                    MESSAGE = 'Workbench task-plan installation found an unexpected or invalid index.';
                            END IF;
                        END LOOP;

                        allowed_pools := CASE WHEN phase = 1 THEN ARRAY[projects_pool] ELSE ARRAY[projects_pool, workbench_pool] END;
                        IF EXISTS (SELECT 1 FROM "Infrastructure_DatasetLifecycle"
                                WHERE "Id" <> 1 OR "State" <> 1 OR "SchemaVersion" <> 1 OR "GuardProtocol" <> 1)
                            OR EXISTS (SELECT 1 FROM "Infrastructure_FiniteOperationState"
                                WHERE NOT ("PoolId" = ANY(allowed_pools)) OR "RetentionProtocolVersion" <> 1)
                            OR EXISTS (SELECT 1 FROM "Infrastructure_FiniteOperationEpochs" e
                                LEFT JOIN "Infrastructure_FiniteOperationState" s
                                    ON s."NamespaceId" = e."NamespaceId" AND s."PoolId" = e."PoolId"
                                WHERE NOT (e."PoolId" = ANY(allowed_pools)) OR s."NamespaceId" IS NULL OR e."Epoch" > s."CurrentEpoch") THEN
                            RAISE EXCEPTION USING ERRCODE = 'P0001',
                                MESSAGE = 'Workbench task-plan installation requires supported retained finite-operation metadata.';
                        END IF;
                        SELECT "NamespaceId" INTO active_namespace FROM "Infrastructure_DatasetLifecycle";
                        IF active_namespace IS NULL THEN
                            IF EXISTS (SELECT 1 FROM "Infrastructure_FiniteOperationState")
                                OR EXISTS (SELECT 1 FROM "Infrastructure_FiniteOperationEpochs") THEN
                                RAISE EXCEPTION USING ERRCODE = 'P0001',
                                    MESSAGE = 'Workbench task-plan installation cannot adopt retained metadata without an active lifecycle.';
                            END IF;
                            IF phase = 1 THEN
                                projects_receipt_id := to_regclass(format('%I.%I', canonical_schema, 'Projects_DefinitionReceipts'));
                                IF projects_receipt_id IS NULL OR projects_receipt_id IS DISTINCT FROM to_regclass('"Projects_DefinitionReceipts"')
                                    OR EXISTS (SELECT 1 FROM pg_class WHERE oid = projects_receipt_id
                                        AND (relnamespace <> canonical_schema_id OR relpersistence <> 'p' OR relkind <> 'r'
                                            OR relispartition OR relrowsecurity OR relforcerowsecurity))
                                    OR EXISTS (SELECT 1 FROM pg_inherits WHERE inhrelid = projects_receipt_id OR inhparent = projects_receipt_id)
                                    OR EXISTS (SELECT 1 FROM pg_rewrite WHERE ev_class = projects_receipt_id) THEN
                                    RAISE EXCEPTION USING ERRCODE = 'P0001',
                                        MESSAGE = 'Workbench task-plan installation cannot inspect orphan receipt residue through an unsupported relation.';
                                END IF;
                                LOCK TABLE "Projects_DefinitionReceipts" IN SHARE MODE;
                                IF projects_receipt_id IS DISTINCT FROM to_regclass('"Projects_DefinitionReceipts"')
                                    OR projects_receipt_id IS DISTINCT FROM to_regclass(format('%I.%I', canonical_schema, 'Projects_DefinitionReceipts'))
                                    OR EXISTS (SELECT 1 FROM pg_class WHERE oid = projects_receipt_id
                                        AND (relnamespace <> canonical_schema_id OR relpersistence <> 'p' OR relkind <> 'r'
                                            OR relispartition OR relrowsecurity OR relforcerowsecurity))
                                    OR EXISTS (SELECT 1 FROM pg_inherits WHERE inhrelid = projects_receipt_id OR inhparent = projects_receipt_id)
                                    OR EXISTS (SELECT 1 FROM pg_rewrite WHERE ev_class = projects_receipt_id) THEN
                                    RAISE EXCEPTION USING ERRCODE = 'P0001',
                                        MESSAGE = 'Workbench task-plan installation lost the original receipt relation.';
                                END IF;
                                IF EXISTS (SELECT 1 FROM "Projects_DefinitionReceipts") THEN
                                    RAISE EXCEPTION USING ERRCODE = 'P0001',
                                        MESSAGE = 'Workbench task-plan installation cannot adopt retained receipts without an active lifecycle.';
                                END IF;
                            END IF;
                        ELSE
                            IF EXISTS (SELECT 1 FROM unnest(allowed_pools) AS expected(pool)
                                WHERE NOT EXISTS (
                                    SELECT 1 FROM "Infrastructure_FiniteOperationState" s
                                    JOIN "Infrastructure_FiniteOperationEpochs" e
                                        ON e."NamespaceId" = s."NamespaceId" AND e."PoolId" = s."PoolId" AND e."Epoch" = s."CurrentEpoch"
                                    WHERE s."NamespaceId" = active_namespace AND s."PoolId" = expected.pool
                                        AND s."RetentionProtocolVersion" = 1 AND e."State" = 1))
                                OR EXISTS (SELECT 1 FROM "Infrastructure_FiniteOperationEpochs"
                                    WHERE "State" = 1 AND "NamespaceId" <> active_namespace)
                                OR EXISTS (SELECT 1 FROM "Infrastructure_FiniteOperationState" s
                                    WHERE s."NamespaceId" = active_namespace AND s."PoolId" = ANY(allowed_pools)
                                        AND (SELECT count(*) FROM "Infrastructure_FiniteOperationEpochs" e
                                            WHERE e."NamespaceId" = s."NamespaceId" AND e."PoolId" = s."PoolId"
                                                AND e."Epoch" > s."RetiredThroughEpoch" AND e."Epoch" < s."CurrentEpoch" AND e."State" = 2)
                                            <> s."CurrentEpoch" - s."RetiredThroughEpoch" - 1) THEN
                                RAISE EXCEPTION USING ERRCODE = 'P0001',
                                    MESSAGE = 'Workbench task-plan installation requires complete active finite-operation readiness.';
                            END IF;
                        END IF;
                        IF phase = 2 THEN
                            IF EXISTS (SELECT 1 FROM "Workbench_TaskPlanReceipts")
                                OR (active_namespace IS NULL AND (
                                    EXISTS (SELECT 1 FROM "Infrastructure_FiniteOperationState" WHERE "PoolId" = workbench_pool)
                                    OR EXISTS (SELECT 1 FROM "Infrastructure_FiniteOperationEpochs" WHERE "PoolId" = workbench_pool)))
                                OR (active_namespace IS NOT NULL AND (
                                    (SELECT count(*) FROM "Infrastructure_FiniteOperationState" WHERE "PoolId" = workbench_pool) <> 1
                                    OR (SELECT count(*) FROM "Infrastructure_FiniteOperationEpochs" WHERE "PoolId" = workbench_pool) <> 1
                                    OR NOT EXISTS (SELECT 1 FROM "Infrastructure_FiniteOperationState"
                                        WHERE "PoolId" = workbench_pool AND "NamespaceId" = active_namespace
                                            AND "RetentionProtocolVersion" = 1 AND "CurrentEpoch" = 1 AND "RetiredThroughEpoch" = 0)
                                    OR NOT EXISTS (SELECT 1 FROM "Infrastructure_FiniteOperationEpochs"
                                        WHERE "PoolId" = workbench_pool AND "NamespaceId" = active_namespace AND "Epoch" = 1 AND "State" = 1
                                            AND "OpenedAtUtc" = issued_at AND "AdmissionClosesAtUtc" = issued_at + interval '86400 seconds'
                                            AND "ClosedAtUtc" IS NULL AND "MutationDeadlineMilliseconds" = 30000 AND "ReplayRetentionSeconds" = 604800))) THEN
                                RAISE EXCEPTION USING ERRCODE = 'P0001',
                                    MESSAGE = 'Workbench task-plan installation did not preserve its exact empty receipt and issued pool promises.';
                            END IF;
                        END IF;
                    END LOOP;
                    PERFORM set_config('lock_timeout', original_lock_timeout, true);
                END;
                $c3$;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                DO $c3_down$
                BEGIN
                    RAISE EXCEPTION USING ERRCODE = 'P0001',
                        MESSAGE = 'Workbench task-plan downgrade is unsupported. Use an explicitly coordinated stopped-host restore with matching binaries.';
                END;
                $c3_down$;
                """);
        }
    }
}
