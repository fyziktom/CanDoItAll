using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CanDoItAll.Migrations.PostgreSql.Migrations {
    public partial class AddResourceMetadataRevisions : Migration {
        protected override void Up(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                DO $resource_revision$
                DECLARE
                    resource_id regclass := to_regclass('"Resources_ProjectResources"');
                    canonical_schema name;
                    schema_id oid;
                    expected_id regclass;
                    candidate_index_name text := 'IX_Resources_ProjectResources_ProjectId_ProjectLifetimeId_Conn~';
                    original_lock_timeout text := current_setting('lock_timeout');
                    phase integer;
                BEGIN
                    IF resource_id IS NULL OR current_setting('transaction_isolation') <> 'read committed' THEN
                        RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Resource revision installation requires its canonical predecessor and a ReadCommitted migration transaction.';
                    END IF;
                    SELECT n.oid, n.nspname INTO STRICT schema_id, canonical_schema
                    FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE c.oid = resource_id;
                    IF canonical_schema <> current_schema()
                        OR resource_id IS DISTINCT FROM to_regclass(format('%I.%I', canonical_schema, 'Resources_ProjectResources'))
                        OR (SELECT max("MigrationId") FROM "__EFMigrationsHistory") IS DISTINCT FROM '20261017053554_AddWorkbenchTaskPlanReceipts' THEN
                        RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Resource revision installation found an unsupported schema or migration predecessor.';
                    END IF;
                    PERFORM set_config('lock_timeout', '5s', true);
                    EXECUTE format('LOCK TABLE %I.%I IN ACCESS EXCLUSIVE MODE', canonical_schema, 'Resources_ProjectResources');
                    IF resource_id IS DISTINCT FROM to_regclass(format('%I.%I', canonical_schema, 'Resources_ProjectResources')) THEN
                        RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Resource revision predecessor identity changed before exclusion.';
                    END IF;

                    CREATE TEMP TABLE r1_expected_resource (
                        "Id" uuid NOT NULL,
                        "ProjectId" uuid NOT NULL,
                        "ProjectLifetimeId" uuid,
                        "OwnerPartyId" uuid,
                        "MaintainerPartyId" uuid,
                        "ResourceKind" integer,
                        "Name" character varying(200) NOT NULL,
                        "Description" text NOT NULL,
                        "ConnectorPluginKey" character varying(160) NOT NULL,
                        "ConfigSchemaVersion" character varying(40) NOT NULL,
                        "LocationOrIdentifier" character varying(1000) NOT NULL,
                        "ConfigJson" text NOT NULL,
                        "LinkedSecretIdsJson" text NOT NULL,
                        "ValidationStatus" integer NOT NULL,
                        "Sensitivity" integer NOT NULL,
                        "SupportsPreview" boolean NOT NULL,
                        "SupportsIndexing" boolean NOT NULL,
                        "CreatedAtUtc" timestamp with time zone NOT NULL,
                        "UpdatedAtUtc" timestamp with time zone NOT NULL,
                        CONSTRAINT "PK_Resources_ProjectResources" PRIMARY KEY ("Id")
                    ) ON COMMIT DROP;
                    expected_id := 'pg_temp.r1_expected_resource'::regclass;

                    FOR phase IN 1..2 LOOP
                        IF EXISTS (SELECT 1 FROM pg_class WHERE oid = resource_id AND
                                (relnamespace <> schema_id OR relkind <> 'r' OR relpersistence <> 'p'
                                    OR relispartition OR relrowsecurity OR relforcerowsecurity))
                            OR EXISTS (SELECT 1 FROM pg_inherits WHERE inhrelid = resource_id OR inhparent = resource_id)
                            OR EXISTS (SELECT 1 FROM pg_rewrite WHERE ev_class = resource_id)
                            OR EXISTS (SELECT 1 FROM pg_trigger WHERE tgrelid = resource_id AND NOT tgisinternal)
                            OR EXISTS (SELECT 1 FROM pg_constraint WHERE contype = 'f' AND
                                (conrelid = resource_id OR confrelid = resource_id)) THEN
                            RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Resource revision installation requires the ordinary canonical relation without custom rules, triggers, foreign keys or row security.';
                        END IF;
                        IF EXISTS (
                            WITH actual AS (
                                SELECT a.attname, a.atttypid, a.atttypmod, a.attnotnull, a.attidentity, a.attgenerated, a.attcollation,
                                    pg_get_expr(d.adbin, d.adrelid, false) AS default_expression
                                FROM pg_attribute a LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
                                WHERE a.attrelid = resource_id AND a.attnum > 0 AND NOT a.attisdropped
                            ), expected AS (
                                SELECT a.attname, a.atttypid, a.atttypmod, a.attnotnull, a.attidentity, a.attgenerated, a.attcollation,
                                    pg_get_expr(d.adbin, d.adrelid, false) AS default_expression
                                FROM pg_attribute a LEFT JOIN pg_attrdef d ON d.adrelid = a.attrelid AND d.adnum = a.attnum
                                WHERE a.attrelid = expected_id AND a.attnum > 0 AND NOT a.attisdropped
                            ) (TABLE actual EXCEPT ALL TABLE expected) UNION ALL (TABLE expected EXCEPT ALL TABLE actual)
                        ) THEN
                            RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Resource revision installation found unexpected columns, nullability or defaults.';
                        END IF;
                        IF EXISTS (
                            WITH actual AS (
                                SELECT conname, contype, condeferrable, condeferred, convalidated, conenforced, connoinherit,
                                    pg_get_constraintdef(oid, false) AS definition
                                FROM pg_constraint WHERE conrelid = resource_id AND contype <> 'n'
                            ), expected AS (
                                SELECT conname, contype, condeferrable, condeferred, convalidated, conenforced, connoinherit,
                                    pg_get_constraintdef(oid, false) AS definition
                                FROM pg_constraint WHERE conrelid = expected_id AND contype <> 'n'
                            ) (TABLE actual EXCEPT ALL TABLE expected) UNION ALL (TABLE expected EXCEPT ALL TABLE actual)
                        ) OR EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid = resource_id AND contype = 'n'
                            AND (NOT convalidated OR NOT conenforced)) THEN
                            RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Resource revision installation found unexpected keys or checks.';
                        END IF;
                        IF EXISTS (
                            WITH actual AS (
                                SELECT c.relname, c.relam, i.indnatts, i.indnkeyatts, i.indisunique, i.indisprimary, i.indisexclusion,
                                    i.indimmediate, i.indisvalid, i.indisready, i.indislive, i.indnullsnotdistinct,
                                    i.indcollation, i.indclass, i.indoption, pg_get_expr(i.indpred, i.indrelid, false) AS predicate,
                                    pg_get_expr(i.indexprs, i.indrelid, false) AS expressions,
                                    ARRAY(SELECT pg_get_indexdef(i.indexrelid, key.ordinality::integer, false)
                                        FROM unnest(i.indkey) WITH ORDINALITY key(value, ordinality) ORDER BY key.ordinality) AS columns
                                FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid WHERE i.indrelid = resource_id
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
                            RAISE EXCEPTION USING ERRCODE = 'P0001', MESSAGE = 'Resource revision installation found unexpected or invalid indexes.';
                        END IF;

                        IF phase = 1 THEN
                            EXECUTE format('ALTER TABLE %I.%I ADD COLUMN "Revision" uuid', canonical_schema, 'Resources_ProjectResources');
                            EXECUTE format('UPDATE %I.%I SET "Revision" = gen_random_uuid()', canonical_schema, 'Resources_ProjectResources');
                            EXECUTE format('ALTER TABLE %I.%I ALTER COLUMN "Revision" SET NOT NULL', canonical_schema, 'Resources_ProjectResources');
                            EXECUTE format('ALTER TABLE %I.%I ADD CONSTRAINT "CK_Resources_ProjectResources_Revision" CHECK ("Revision" <> ''00000000-0000-0000-0000-000000000000''::uuid)', canonical_schema, 'Resources_ProjectResources');
                            EXECUTE format('CREATE INDEX %I ON %I.%I ("ProjectId", "ProjectLifetimeId", "ConnectorPluginKey")', candidate_index_name, canonical_schema, 'Resources_ProjectResources');

                            ALTER TABLE pg_temp.r1_expected_resource ADD COLUMN "Revision" uuid NOT NULL;
                            ALTER TABLE pg_temp.r1_expected_resource ADD CONSTRAINT "CK_Resources_ProjectResources_Revision"
                                CHECK ("Revision" <> '00000000-0000-0000-0000-000000000000'::uuid);
                            EXECUTE format('CREATE INDEX %I ON pg_temp.r1_expected_resource ("ProjectId", "ProjectLifetimeId", "ConnectorPluginKey")', candidate_index_name);
                        END IF;
                    END LOOP;
                    PERFORM set_config('lock_timeout', original_lock_timeout, true);
                END;
                $resource_revision$;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder) {
            migrationBuilder.Sql("""
                DO $resource_revision_down$
                BEGIN
                    RAISE EXCEPTION USING ERRCODE = 'P0001',
                        MESSAGE = 'Resource metadata revision downgrade is unsupported. Use an explicitly coordinated stopped-host restore with matching binaries and activation state.';
                END;
                $resource_revision_down$;
                """);
        }
    }
}
