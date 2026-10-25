using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class ExtractFiniteOperationEpochMetadata : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                DO $c2$
                DECLARE
                    state_id regclass := to_regclass('"Projects_OperationState"');
                    epoch_id regclass := to_regclass('"Projects_OperationEpochs"');
                    receipt_id regclass := to_regclass('"Projects_DefinitionReceipts"');
                    lifecycle_id regclass := to_regclass('"Infrastructure_DatasetLifecycle"');
                    relation_id regclass;
                    index_id regclass;
                    pair record;
                    phase integer;
                    invalid_history boolean;
                    receipt_count bigint;
                    original_lock_timeout text := current_setting('lock_timeout');
                BEGIN
                    IF state_id IS NULL OR epoch_id IS NULL OR receipt_id IS NULL OR lifecycle_id IS NULL
                        OR to_regclass('"Infrastructure_FiniteOperationState"') IS NOT NULL
                        OR to_regclass('"Infrastructure_FiniteOperationEpochs"') IS NOT NULL THEN
                        RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Finite-operation migration requires the exact predecessor tables and no pre-existing destination tables.';
                    END IF;
                    IF EXISTS (SELECT 1 FROM pg_class WHERE oid IN (state_id, epoch_id, receipt_id, lifecycle_id)
                            AND (relkind <> 'r' OR relispartition OR relrowsecurity OR relforcerowsecurity)) THEN
                        RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Finite-operation migration requires ordinary nonpartitioned tables without row security.';
                    END IF;
                    PERFORM set_config('lock_timeout', '5s', true);
                    LOCK TABLE "Infrastructure_DatasetLifecycle", "Projects_DefinitionReceipts",
                        "Projects_OperationEpochs", "Projects_OperationState" IN ACCESS EXCLUSIVE MODE;

                    CREATE TEMP TABLE c2_expected_state (
                        "NamespaceId" uuid NOT NULL,
                        "CurrentEpoch" bigint NOT NULL,
                        "RetiredThroughEpoch" bigint NOT NULL,
                        CONSTRAINT "PK_Projects_OperationState" PRIMARY KEY ("NamespaceId"),
                        CONSTRAINT "CK_Projects_OperationState_Epoch" CHECK ("CurrentEpoch" > 0 AND "RetiredThroughEpoch" >= 0 AND "RetiredThroughEpoch" < "CurrentEpoch"),
                        CONSTRAINT "CK_Projects_OperationState_Namespace" CHECK ("NamespaceId" <> '00000000-0000-0000-0000-000000000000'::uuid)
                    ) ON COMMIT DROP;
                    CREATE TEMP TABLE c2_expected_epochs (
                        "NamespaceId" uuid NOT NULL,
                        "Epoch" bigint NOT NULL,
                        "State" integer NOT NULL,
                        "OpenedAtUtc" timestamp with time zone NOT NULL,
                        "AdmissionClosesAtUtc" timestamp with time zone NOT NULL,
                        "ClosedAtUtc" timestamp with time zone,
                        "MutationDeadlineMilliseconds" integer NOT NULL,
                        "ReplayRetentionSeconds" bigint NOT NULL,
                        CONSTRAINT "PK_Projects_OperationEpochs" PRIMARY KEY ("NamespaceId", "Epoch"),
                        CONSTRAINT "CK_Projects_OperationEpoch_Deadline" CHECK ("MutationDeadlineMilliseconds" > 0 AND "ReplayRetentionSeconds" > 0 AND "AdmissionClosesAtUtc" > "OpenedAtUtc"),
                        CONSTRAINT "CK_Projects_OperationEpoch_Namespace" CHECK ("NamespaceId" <> '00000000-0000-0000-0000-000000000000'::uuid),
                        CONSTRAINT "CK_Projects_OperationEpoch_Number" CHECK ("Epoch" > 0),
                        CONSTRAINT "CK_Projects_OperationEpoch_State" CHECK (("State" = 1 AND "ClosedAtUtc" IS NULL) OR ("State" = 2 AND "ClosedAtUtc" IS NOT NULL AND "ClosedAtUtc" >= "OpenedAtUtc"))
                    ) ON COMMIT DROP;
                    CREATE UNIQUE INDEX "IX_Projects_OperationEpochs_NamespaceId" ON c2_expected_epochs ("NamespaceId") WHERE "State" = 1;
                    CREATE TEMP TABLE c2_expected_receipts (
                        "NamespaceId" uuid NOT NULL,
                        "Epoch" bigint NOT NULL,
                        "CallerKind" integer NOT NULL,
                        "CallerKey" bytea NOT NULL,
                        "OperationId" uuid NOT NULL,
                        "SchemaVersion" integer NOT NULL,
                        "NormalizationVersion" integer NOT NULL,
                        "ResultVersion" integer NOT NULL,
                        "CommandKind" integer NOT NULL,
                        "SemanticDigest" bytea NOT NULL,
                        "ProjectId" uuid NOT NULL,
                        "LifetimeId" uuid NOT NULL,
                        "AcceptedRevision" uuid NOT NULL,
                        "AcceptedAtUtc" timestamp with time zone NOT NULL,
                        "AcceptedPayload" bytea NOT NULL,
                        "CreationFingerprint" character varying(64),
                        CONSTRAINT "PK_Projects_DefinitionReceipts" PRIMARY KEY ("NamespaceId", "Epoch", "CallerKind", "CallerKey", "OperationId"),
                        CONSTRAINT "CK_Projects_DefinitionReceipt_Address" CHECK ("NamespaceId" <> '00000000-0000-0000-0000-000000000000'::uuid AND "OperationId" <> '00000000-0000-0000-0000-000000000000'::uuid AND "Epoch" > 0),
                        CONSTRAINT "CK_Projects_DefinitionReceipt_Caller" CHECK ("CallerKind" IN (1, 2, 3, 4) AND octet_length("CallerKey") = 32),
                        CONSTRAINT "CK_Projects_DefinitionReceipt_Creation" CHECK (("CommandKind" = 1 AND "CreationFingerprint" IS NOT NULL AND "CreationFingerprint" ~ '^[0-9a-f]{64}$') OR ("CommandKind" <> 1 AND "CreationFingerprint" IS NULL)),
                        CONSTRAINT "CK_Projects_DefinitionReceipt_Digest" CHECK (octet_length("SemanticDigest") = 32),
                        CONSTRAINT "CK_Projects_DefinitionReceipt_Interpretation" CHECK ("SchemaVersion" > 0 AND "NormalizationVersion" > 0 AND "ResultVersion" > 0 AND "CommandKind" IN (1, 2, 3)),
                        CONSTRAINT "CK_Projects_DefinitionReceipt_Payload" CHECK (octet_length("AcceptedPayload") BETWEEN 1 AND 2097152),
                        CONSTRAINT "CK_Projects_DefinitionReceipt_Target" CHECK ("ProjectId" <> '00000000-0000-0000-0000-000000000000'::uuid AND "LifetimeId" <> '00000000-0000-0000-0000-000000000000'::uuid AND "AcceptedRevision" <> '00000000-0000-0000-0000-000000000000'::uuid)
                    ) ON COMMIT DROP;
                    CREATE TEMP TABLE c2_expected_lifecycle (
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

                    FOR phase IN 1..2 LOOP
                        IF phase = 2 THEN
                            ALTER TABLE "Projects_OperationState" RENAME TO "Infrastructure_FiniteOperationState";
                            ALTER TABLE "Projects_OperationEpochs" RENAME TO "Infrastructure_FiniteOperationEpochs";
                            FOREACH relation_id IN ARRAY ARRAY[state_id, 'pg_temp.c2_expected_state'::regclass] LOOP
                                EXECUTE format('ALTER TABLE %s ADD COLUMN "PoolId" uuid, ADD COLUMN "RetentionProtocolVersion" integer', relation_id);
                                EXECUTE format('UPDATE %s SET "PoolId" = ''e6eaf1c1-5dbc-46bf-8641-1ae9fe3ae376''::uuid, "RetentionProtocolVersion" = 1', relation_id);
                                EXECUTE format('ALTER TABLE %s ALTER COLUMN "PoolId" SET NOT NULL, ALTER COLUMN "RetentionProtocolVersion" SET NOT NULL', relation_id);
                                EXECUTE format('ALTER TABLE %s DROP CONSTRAINT "PK_Projects_OperationState", ADD CONSTRAINT "PK_Infrastructure_FiniteOperationState" PRIMARY KEY ("NamespaceId", "PoolId")', relation_id);
                                EXECUTE format('ALTER TABLE %s RENAME CONSTRAINT "CK_Projects_OperationState_Epoch" TO "CK_Infrastructure_FiniteOperationState_Epoch"', relation_id);
                                EXECUTE format('ALTER TABLE %s RENAME CONSTRAINT "CK_Projects_OperationState_Namespace" TO "CK_Infrastructure_FiniteOperationState_Namespace"', relation_id);
                                EXECUTE format('ALTER TABLE %s ADD CONSTRAINT "CK_Infrastructure_FiniteOperationState_Pool" CHECK ("PoolId" <> ''00000000-0000-0000-0000-000000000000''::uuid), ADD CONSTRAINT "CK_Infrastructure_FiniteOperationState_Protocol" CHECK ("RetentionProtocolVersion" > 0)', relation_id);
                            END LOOP;
                            FOREACH relation_id IN ARRAY ARRAY[epoch_id, 'pg_temp.c2_expected_epochs'::regclass] LOOP
                                EXECUTE format('ALTER TABLE %s ADD COLUMN "PoolId" uuid', relation_id);
                                EXECUTE format('UPDATE %s SET "PoolId" = ''e6eaf1c1-5dbc-46bf-8641-1ae9fe3ae376''::uuid', relation_id);
                                EXECUTE format('ALTER TABLE %s ALTER COLUMN "PoolId" SET NOT NULL', relation_id);
                                EXECUTE format('ALTER TABLE %s DROP CONSTRAINT "PK_Projects_OperationEpochs", ADD CONSTRAINT "PK_Infrastructure_FiniteOperationEpochs" PRIMARY KEY ("NamespaceId", "PoolId", "Epoch")', relation_id);
                                EXECUTE format('ALTER TABLE %s RENAME CONSTRAINT "CK_Projects_OperationEpoch_Deadline" TO "CK_Infrastructure_FiniteOperationEpoch_Deadline"', relation_id);
                                EXECUTE format('ALTER TABLE %s RENAME CONSTRAINT "CK_Projects_OperationEpoch_Namespace" TO "CK_Infrastructure_FiniteOperationEpoch_Namespace"', relation_id);
                                EXECUTE format('ALTER TABLE %s RENAME CONSTRAINT "CK_Projects_OperationEpoch_Number" TO "CK_Infrastructure_FiniteOperationEpoch_Number"', relation_id);
                                EXECUTE format('ALTER TABLE %s RENAME CONSTRAINT "CK_Projects_OperationEpoch_State" TO "CK_Infrastructure_FiniteOperationEpoch_State"', relation_id);
                                EXECUTE format('ALTER TABLE %s ADD CONSTRAINT "CK_Infrastructure_FiniteOperationEpoch_Pool" CHECK ("PoolId" <> ''00000000-0000-0000-0000-000000000000''::uuid)', relation_id);
                                SELECT i.indexrelid::regclass INTO STRICT index_id FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid
                                    WHERE i.indrelid = relation_id AND c.relname = 'IX_Projects_OperationEpochs_NamespaceId';
                                EXECUTE format('DROP INDEX %s', index_id);
                                EXECUTE format('CREATE UNIQUE INDEX "IX_Infrastructure_FiniteOperationEpochs_NamespaceId_PoolId" ON %s ("NamespaceId", "PoolId") WHERE "State" = 1', relation_id);
                            END LOOP;
                        END IF;

                        IF EXISTS (SELECT 1 FROM pg_constraint WHERE contype = 'f'
                                AND (conrelid IN (state_id, epoch_id) OR confrelid IN (state_id, epoch_id)))
                            OR EXISTS (SELECT 1 FROM pg_trigger WHERE NOT tgisinternal AND tgrelid IN (state_id, epoch_id)) THEN
                            RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Finite-operation metadata has unsupported foreign keys or user triggers.';
                        END IF;
                        FOR pair IN SELECT * FROM (VALUES
                            (state_id, 'pg_temp.c2_expected_state'::regclass),
                            (epoch_id, 'pg_temp.c2_expected_epochs'::regclass),
                            (receipt_id, 'pg_temp.c2_expected_receipts'::regclass),
                            (lifecycle_id, 'pg_temp.c2_expected_lifecycle'::regclass)
                        ) AS relations(actual, expected) LOOP
                            IF EXISTS (SELECT 1 FROM pg_class WHERE oid = pair.actual
                                    AND (relkind <> 'r' OR relispartition OR relrowsecurity OR relforcerowsecurity))
                                OR EXISTS (SELECT 1 FROM pg_inherits WHERE inhrelid = pair.actual OR inhparent = pair.actual)
                                OR EXISTS (SELECT 1 FROM pg_rewrite WHERE ev_class = pair.actual) THEN
                                RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Finite-operation migration requires ordinary nonpartitioned tables without inheritance, row security or rules.';
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
                                RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Finite-operation migration found unexpected columns, nullability or defaults.';
                            END IF;
                            IF EXISTS (
                                WITH actual AS (
                                    SELECT conname, contype, condeferrable, condeferred, conenforced, convalidated, connoinherit,
                                        pg_get_constraintdef(oid, false) AS definition FROM pg_constraint WHERE conrelid = pair.actual AND contype <> 'n'
                                ), expected AS (
                                    SELECT conname, contype, condeferrable, condeferred, conenforced, convalidated, connoinherit,
                                        pg_get_constraintdef(oid, false) AS definition FROM pg_constraint WHERE conrelid = pair.expected AND contype <> 'n'
                                ) (TABLE actual EXCEPT ALL TABLE expected) UNION ALL (TABLE expected EXCEPT ALL TABLE actual)
                            ) OR EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = pair.actual AND contype = 'n' AND (NOT conenforced OR NOT convalidated)) THEN
                                RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Finite-operation migration found unexpected keys or check constraints.';
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
                                RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Finite-operation migration found an unexpected or invalid index.';
                            END IF;
                        END LOOP;

                        EXECUTE format($history$
                            SELECT EXISTS (SELECT 1 FROM "Infrastructure_DatasetLifecycle" WHERE "Id" <> 1 OR "State" <> 1
                                    OR "SchemaVersion" <> 1 OR "GuardProtocol" <> 1)
                                OR (NOT EXISTS (SELECT 1 FROM "Infrastructure_DatasetLifecycle")
                                    AND (EXISTS (SELECT 1 FROM %1$s) OR EXISTS (SELECT 1 FROM %2$s)
                                        OR EXISTS (SELECT 1 FROM "Projects_DefinitionReceipts")))
                                OR EXISTS (SELECT 1 FROM %2$s e LEFT JOIN %1$s s ON s."NamespaceId" = e."NamespaceId"
                                    WHERE s."NamespaceId" IS NULL OR e."Epoch" > s."CurrentEpoch")
                                OR EXISTS (SELECT 1 FROM "Projects_DefinitionReceipts" r
                                    LEFT JOIN %1$s s ON s."NamespaceId" = r."NamespaceId"
                                    LEFT JOIN %2$s e ON e."NamespaceId" = r."NamespaceId" AND e."Epoch" = r."Epoch"
                                    WHERE s."NamespaceId" IS NULL OR e."NamespaceId" IS NULL)
                                OR EXISTS (SELECT 1 FROM "Infrastructure_DatasetLifecycle" d
                                    LEFT JOIN %1$s s ON s."NamespaceId" = d."NamespaceId"
                                    LEFT JOIN %2$s e ON e."NamespaceId" = s."NamespaceId" AND e."Epoch" = s."CurrentEpoch"
                                    WHERE s."NamespaceId" IS NULL OR e."NamespaceId" IS NULL OR e."State" <> 1)
                                OR EXISTS (SELECT 1 FROM "Infrastructure_DatasetLifecycle" d JOIN %1$s s ON s."NamespaceId" = d."NamespaceId"
                                    WHERE (SELECT count(*) FROM %2$s e WHERE e."NamespaceId" = s."NamespaceId"
                                        AND e."Epoch" > s."RetiredThroughEpoch" AND e."Epoch" < s."CurrentEpoch" AND e."State" = 2)
                                        <> s."CurrentEpoch" - s."RetiredThroughEpoch" - 1)
                                OR EXISTS (SELECT 1 FROM %2$s e WHERE e."State" = 1 AND NOT EXISTS
                                    (SELECT 1 FROM "Infrastructure_DatasetLifecycle" d WHERE d."NamespaceId" = e."NamespaceId"))
                            $history$, state_id, epoch_id) INTO invalid_history;
                        IF invalid_history THEN
                            RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Finite-operation retained history is inconsistent or requires explicit lifecycle recovery.';
                        END IF;

                        IF phase = 1 THEN
                            CREATE TEMP TABLE c2_retained_state ON COMMIT DROP AS
                                SELECT "NamespaceId", "CurrentEpoch", "RetiredThroughEpoch" FROM "Projects_OperationState";
                            CREATE TEMP TABLE c2_retained_epochs ON COMMIT DROP AS
                                SELECT "NamespaceId", "Epoch", "State", "OpenedAtUtc", "AdmissionClosesAtUtc", "ClosedAtUtc",
                                    "MutationDeadlineMilliseconds", "ReplayRetentionSeconds" FROM "Projects_OperationEpochs";
                            CREATE TEMP TABLE c2_retained_lifecycle ON COMMIT DROP AS
                                SELECT "Id", "NamespaceId", "MaintenanceId", "SchemaVersion", "GuardProtocol", "State" FROM "Infrastructure_DatasetLifecycle";
                            SELECT count(*) INTO receipt_count FROM "Projects_DefinitionReceipts";
                        ELSE
                            IF EXISTS (SELECT 1 FROM "Infrastructure_FiniteOperationState"
                                    WHERE "PoolId" <> 'e6eaf1c1-5dbc-46bf-8641-1ae9fe3ae376'::uuid OR "RetentionProtocolVersion" <> 1)
                                OR EXISTS (SELECT 1 FROM "Infrastructure_FiniteOperationEpochs"
                                    WHERE "PoolId" <> 'e6eaf1c1-5dbc-46bf-8641-1ae9fe3ae376'::uuid)
                                OR EXISTS (
                                    (SELECT "NamespaceId", "CurrentEpoch", "RetiredThroughEpoch" FROM "Infrastructure_FiniteOperationState"
                                        EXCEPT ALL TABLE c2_retained_state)
                                    UNION ALL (TABLE c2_retained_state EXCEPT ALL
                                        SELECT "NamespaceId", "CurrentEpoch", "RetiredThroughEpoch" FROM "Infrastructure_FiniteOperationState")
                                ) OR EXISTS (
                                    (SELECT "NamespaceId", "Epoch", "State", "OpenedAtUtc", "AdmissionClosesAtUtc", "ClosedAtUtc",
                                        "MutationDeadlineMilliseconds", "ReplayRetentionSeconds" FROM "Infrastructure_FiniteOperationEpochs"
                                        EXCEPT ALL TABLE c2_retained_epochs)
                                    UNION ALL (TABLE c2_retained_epochs EXCEPT ALL
                                        SELECT "NamespaceId", "Epoch", "State", "OpenedAtUtc", "AdmissionClosesAtUtc", "ClosedAtUtc",
                                            "MutationDeadlineMilliseconds", "ReplayRetentionSeconds" FROM "Infrastructure_FiniteOperationEpochs")
                                ) OR EXISTS (
                                    (SELECT "Id", "NamespaceId", "MaintenanceId", "SchemaVersion", "GuardProtocol", "State" FROM "Infrastructure_DatasetLifecycle"
                                        EXCEPT ALL TABLE c2_retained_lifecycle)
                                    UNION ALL (TABLE c2_retained_lifecycle EXCEPT ALL
                                        SELECT "Id", "NamespaceId", "MaintenanceId", "SchemaVersion", "GuardProtocol", "State" FROM "Infrastructure_DatasetLifecycle")
                                ) OR (SELECT count(*) FROM "Projects_DefinitionReceipts") <> receipt_count
                                OR to_regclass('"Projects_OperationState"') IS NOT NULL
                                OR to_regclass('"Projects_OperationEpochs"') IS NOT NULL THEN
                                RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Finite-operation migration did not preserve exact metadata and receipt correspondence.';
                            END IF;
                        END IF;
                    END LOOP;
                    ALTER TABLE "Workspace_Settings"
                        ADD COLUMN "ConcurrencyToken" uuid NOT NULL DEFAULT gen_random_uuid(),
                        ADD COLUMN "IsCurrent" boolean NOT NULL DEFAULT false;
                    UPDATE "Workspace_Settings" SET "IsCurrent" = true
                        WHERE "Id" = (SELECT "Id" FROM "Workspace_Settings" ORDER BY "UpdatedAtUtc" DESC, "Id" ASC LIMIT 1);
                    ALTER TABLE "Workspace_Settings" ALTER COLUMN "IsCurrent" DROP DEFAULT;
                    CREATE UNIQUE INDEX "IX_Workspace_Settings_IsCurrent" ON "Workspace_Settings" ("IsCurrent") WHERE "IsCurrent";
                    PERFORM set_config('lock_timeout', original_lock_timeout, true);
                END;
                $c2$;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                DO $c2_down$
                BEGIN
                    RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Finite-operation metadata downgrade is unsupported. Use an explicitly coordinated stopped-host restore with matching binaries.';
                END;
                $c2_down$;
                """);
        }
    }
}
