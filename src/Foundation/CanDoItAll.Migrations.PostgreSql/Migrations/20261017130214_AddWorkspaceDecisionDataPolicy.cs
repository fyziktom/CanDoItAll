using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddWorkspaceDecisionDataPolicy : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
            DO $workspace_policy$
            DECLARE
                workspace_id regclass := to_regclass('"Workspace_Settings"');
                lifecycle_id regclass := to_regclass('"Infrastructure_DatasetLifecycle"');
                history_id regclass := to_regclass('"__EFMigrationsHistory"');
                canonical_schema name;
                schema_id oid;
                expected_id regclass;
                latest_migration text;
                original_lock_timeout text := current_setting('lock_timeout');
                phase integer;
            BEGIN
                IF workspace_id IS NULL OR lifecycle_id IS NULL OR history_id IS NULL
                    OR current_setting('transaction_isolation') <> 'read committed' THEN
                    RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Workspace policy installation requires its canonical predecessor and a ReadCommitted migration transaction.';
                END IF;
                SELECT n.oid, n.nspname INTO STRICT schema_id, canonical_schema
                FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE c.oid = workspace_id;
                IF canonical_schema <> current_schema()
                    OR workspace_id IS DISTINCT FROM to_regclass(format('%I.%I', canonical_schema, 'Workspace_Settings'))
                    OR lifecycle_id IS DISTINCT FROM to_regclass(format('%I.%I', canonical_schema, 'Infrastructure_DatasetLifecycle'))
                    OR history_id IS DISTINCT FROM to_regclass(format('%I.%I', canonical_schema, '__EFMigrationsHistory'))
                    OR EXISTS (SELECT 1 FROM pg_class WHERE oid IN (lifecycle_id, history_id)
                        AND (relnamespace <> schema_id OR relkind <> 'r' OR relpersistence <> 'p' OR relispartition
                            OR relrowsecurity OR relforcerowsecurity))
                    OR EXISTS (SELECT 1 FROM pg_inherits WHERE inhrelid IN (lifecycle_id, history_id) OR inhparent IN (lifecycle_id, history_id)) THEN
                    RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Workspace policy installation found an unsupported canonical schema or lifecycle relation.';
                END IF;
                EXECUTE format('SELECT max("MigrationId") FROM %I.%I', canonical_schema, '__EFMigrationsHistory') INTO latest_migration;
                IF latest_migration IS DISTINCT FROM '20261017082009_AddResourceMetadataRevisions' THEN
                    RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Workspace policy installation found an unsupported migration predecessor.';
                END IF;
                PERFORM set_config('lock_timeout', '5s', true);
                EXECUTE format('LOCK TABLE %I.%I IN EXCLUSIVE MODE', canonical_schema, 'Infrastructure_DatasetLifecycle');
                EXECUTE format('LOCK TABLE %I.%I IN ACCESS EXCLUSIVE MODE', canonical_schema, 'Workspace_Settings');
                IF workspace_id IS DISTINCT FROM to_regclass(format('%I.%I', canonical_schema, 'Workspace_Settings'))
                    OR lifecycle_id IS DISTINCT FROM to_regclass(format('%I.%I', canonical_schema, 'Infrastructure_DatasetLifecycle'))
                    OR history_id IS DISTINCT FROM to_regclass(format('%I.%I', canonical_schema, '__EFMigrationsHistory'))
                    OR EXISTS (SELECT 1 FROM pg_class WHERE oid IN (lifecycle_id, history_id)
                        AND (relnamespace <> schema_id OR relkind <> 'r' OR relpersistence <> 'p' OR relispartition
                            OR relrowsecurity OR relforcerowsecurity))
                    OR EXISTS (SELECT 1 FROM pg_inherits WHERE inhrelid IN (lifecycle_id, history_id) OR inhparent IN (lifecycle_id, history_id)) THEN
                    RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Workspace policy predecessor identities changed before exclusion.';
                END IF;
                EXECUTE format('SELECT max("MigrationId") FROM %I.%I', canonical_schema, '__EFMigrationsHistory') INTO latest_migration;
                IF latest_migration IS DISTINCT FROM '20261017082009_AddResourceMetadataRevisions' THEN
                    RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Workspace policy migration predecessor changed before exclusion.';
                END IF;

                CREATE TEMP TABLE p07_expected_workspace (
                    "Id" uuid NOT NULL,
                    "WorkspaceName" character varying(200) NOT NULL,
                    "DefaultProviderProfileId" uuid,
                    "DefaultPromptOutputFormat" character varying(40) NOT NULL,
                    "CurrencyCode" character varying(3) NOT NULL DEFAULT 'USD',
                    "CurrencyCultureName" character varying(40) NOT NULL DEFAULT 'en-US',
                    "Notes" text NOT NULL,
                    "UpdatedAtUtc" timestamp with time zone NOT NULL,
                    "ConcurrencyToken" uuid NOT NULL DEFAULT gen_random_uuid(),
                    "IsCurrent" boolean NOT NULL,
                    CONSTRAINT "PK_Workspace_Settings" PRIMARY KEY ("Id")
                ) ON COMMIT DROP;
                CREATE UNIQUE INDEX "IX_Workspace_Settings_IsCurrent" ON p07_expected_workspace ("IsCurrent") WHERE "IsCurrent";
                expected_id := 'pg_temp.p07_expected_workspace'::regclass;

                FOR phase IN 1..2 LOOP
                    IF EXISTS (SELECT 1 FROM pg_class WHERE oid = workspace_id AND
                            (relnamespace <> schema_id OR relkind <> 'r' OR relpersistence <> 'p'
                                OR relispartition OR relrowsecurity OR relforcerowsecurity))
                        OR EXISTS (SELECT 1 FROM pg_inherits WHERE inhrelid = workspace_id OR inhparent = workspace_id)
                        OR EXISTS (SELECT 1 FROM pg_rewrite WHERE ev_class = workspace_id)
                        OR EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid = workspace_id AND NOT tgisinternal)
                        OR EXISTS (SELECT 1 FROM pg_constraint WHERE contype = 'f' AND
                            (conrelid = workspace_id OR confrelid = workspace_id)) THEN
                        RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Workspace policy installation requires the ordinary canonical relation without custom rules, triggers, foreign keys or row security.';
                    END IF;
                    IF EXISTS (
                        WITH actual AS (
                            SELECT a.attname, a.atttypid, a.atttypmod, a.attnotnull, a.attidentity, a.attgenerated, a.attcollation,
                                pg_get_expr(d.adbin, d.adrelid, false) AS default_expression
                            FROM pg_attribute a LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
                            WHERE a.attrelid = workspace_id AND a.attnum > 0 AND NOT a.attisdropped
                        ), expected AS (
                            SELECT a.attname, a.atttypid, a.atttypmod, a.attnotnull, a.attidentity, a.attgenerated, a.attcollation,
                                pg_get_expr(d.adbin, d.adrelid, false) AS default_expression
                            FROM pg_attribute a LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
                            WHERE a.attrelid = expected_id AND a.attnum > 0 AND NOT a.attisdropped
                        ) (TABLE actual EXCEPT ALL TABLE expected) UNION ALL (TABLE expected EXCEPT ALL TABLE actual)
                    ) THEN
                        RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Workspace policy installation found unexpected columns, nullability or defaults.';
                    END IF;
                    IF EXISTS (
                        WITH actual AS (
                            SELECT conname, contype, condeferrable, condeferred, convalidated, conenforced, connoinherit,
                                pg_get_constraintdef(oid, false) AS definition
                            FROM pg_constraint WHERE conrelid = workspace_id AND contype <> 'n'
                        ), expected AS (
                            SELECT conname, contype, condeferrable, condeferred, convalidated, conenforced, connoinherit,
                                pg_get_constraintdef(oid, false) AS definition
                            FROM pg_constraint WHERE conrelid = expected_id AND contype <> 'n'
                        ) (TABLE actual EXCEPT ALL TABLE expected) UNION ALL (TABLE expected EXCEPT ALL TABLE actual)
                    ) OR EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = workspace_id AND contype = 'n'
                        AND (NOT convalidated OR NOT conenforced)) THEN
                        RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Workspace policy installation found unexpected keys or checks.';
                    END IF;
                    IF EXISTS (
                        WITH actual AS (
                            SELECT c.relname, c.relam, i.indnatts, i.indnkeyatts, i.indisunique, i.indisprimary, i.indisexclusion,
                                i.indimmediate, i.indisvalid, i.indisready, i.indislive, i.indnullsnotdistinct,
                                i.indcollation, i.indclass, i.indoption, pg_get_expr(i.indpred, i.indrelid, false) AS predicate,
                                pg_get_expr(i.indexprs, i.indrelid, false) AS expressions,
                                ARRAY(SELECT pg_get_indexdef(i.indexrelid, key.ordinality::integer, false)
                                    FROM unnest(i.indkey) WITH ORDINALITY key(value, ordinality) ORDER BY key.ordinality) AS columns
                            FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid WHERE i.indrelid = workspace_id
                        ), expected AS (
                            SELECT c.relname, c.relam, i.indnatts, i.indnkeyatts, i.indisunique, i.indisprimary, i.indisexclusion,
                                i.indimmediate, i.indisvalid, i.indisready, i.indislive, i.indnullsnotdistinct,
                                i.indcollation, i.indclass, i.indoption, pg_get_expr(i.indpred, i.indrelid, false) AS predicate,
                                pg_get_expr(i.indexprs, i.indrelid, false) AS expressions,
                                ARRAY(SELECT pg_get_indexdef(i.indexrelid, key.ordinality::integer, false)
                                    FROM unnest(i.indkey) WITH ORDINALITY key(value, ordinality) ORDER BY key.ordinality) AS columns
                            FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid WHERE i.indrelid = expected_id
                        ) (TABLE actual EXCEPT ALL TABLE expected) UNION ALL (TABLE expected EXCEPT ALL TABLE actual)
                    ) THEN
                        RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Workspace policy installation found unexpected or invalid indexes.';
                    END IF;

                    IF phase = 1 THEN
                        EXECUTE format('ALTER TABLE %I.%I ADD COLUMN "DecisionDataPolicyJson" text', canonical_schema, 'Workspace_Settings');
                        EXECUTE format('ALTER TABLE %I.%I ADD CONSTRAINT "CK_Workspace_Settings_DecisionDataPolicyJson" CHECK ("DecisionDataPolicyJson" IS NULL OR octet_length("DecisionDataPolicyJson") BETWEEN 1 AND 1024)', canonical_schema, 'Workspace_Settings');
                        ALTER TABLE pg_temp.p07_expected_workspace ADD COLUMN "DecisionDataPolicyJson" text;
                        ALTER TABLE pg_temp.p07_expected_workspace ADD CONSTRAINT "CK_Workspace_Settings_DecisionDataPolicyJson"
                            CHECK ("DecisionDataPolicyJson" IS NULL OR octet_length("DecisionDataPolicyJson") BETWEEN 1 AND 1024);
                    END IF;
                END LOOP;
                PERFORM set_config('lock_timeout', original_lock_timeout, true);
            END;
            $workspace_policy$;
            """);
        }

        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
            DO $workspace_policy_down$
            BEGIN
                RAISE EXCEPTION USING ERRCODE = 'P0001',
                    MESSAGE = 'Workspace decision policy downgrade is unsupported. Use an explicitly coordinated stopped-host restore with matching binaries and activation state.';
            END;
            $workspace_policy_down$;
            """);
        }
    }
}
