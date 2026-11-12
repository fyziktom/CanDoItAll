# C3 migration guard and explicit pool installation

Source-only review/proposal, 2026-10-17. No migration, model, product or test source was
edited; no EF/build/test/database command was run. The parent owns canonical generation,
actual SQL review and execution. This refines [schema readiness](../plan/P04-C3-schema-execution-readiness.md)
SHA `7F521C40C1049427D91FBBAAEC67CCFD013F5642C906A077332EFBC8E6412558`.
The predecessor remains `20261016233100_ExtractFiniteOperationEpochMetadata`.

## Reviewed proof and limits

The initial three migration Facts at `E803FC2E9DD0242F9DD8640AFA6979ADA9090F44F30EE3922884F5BFA7E66AB2`
were independently reviewed. The four approved additional corruption arrangements were
then reviewed at `D0E5C8F4D01477A97624F4BA5B914814700E5C1E09D49A65AA9BAF43C6D40BF5`.
There is no source blocker in the inspected fixture, exception boundary or assertions.
The class still has three Facts, no theory/dynamic/skip expansion. Its corruption loop
now has twelve arrangements, not twelve discovered tests.

The positive inactive fixture is deliberately valid: Projects state CurrentEpoch3 /
RetiredThrough1 retains only Closed epoch2. Its absent historical current3 is compatible
with completed inactive cleanup. Do not reject or recreate it. In contrast, the new
active-prefix arrangement retains Closed1 and Open3 with CurrentEpoch3 / RetiredThrough0
but omits2; it must fail. Closing active current1, making the known inactive epoch Open,
and deleting lifecycle while retaining metadata must also fail.

Fresh upgrade leaves lifecycle/state/epoch empty. The injected bootstrap trigger fails
inside the actual EF initialization save, leaving the exact Prepared file and rolling
back SQL. Removing that trigger and Resume must install both pools with the saved binding.
Active upgrade compares full Projects/lifecycle/receipt tuples and the activation file,
then checks issued Workbench timings and repeat-readiness stability. Down compares both
families' retained evidence and unchanged migration history. Owner/canonical metadata,
169 canonical entities, 16 Workbench entities, address PK, seven enforced/validated checks,
no FK/default/extra index/user trigger, and seven actual invalid inserts are asserted.

These are authored arrangements, not execution evidence. Check-name enumeration plus one
invalid input per check does not independently prove every check expression; the exact
catalog/DDL audit below remains required. Run against the isolated C3 model cohort:
Resource's separately authored Revision/index migration must not leak into that generation
or cause the parity test to be weakened. Schema bytes, no-pending-model and 169/16 are
claims to be verified after parent generation, not proof from source counting.

## One transaction and a small fixed perimeter

Use one new migration Up with an initial guarded SQL block, the exact canonical-generated
receipt DDL and installation/postflight in the same EF migration transaction. A single
`DO $c3$` containing the reviewed generated CREATE TABLE SQL is the smallest shape, as in
C2; do not create a runtime helper, invoke application options or suppress the transaction.
The designer/snapshot still come from canonical generation. The idempotent script must
retain the migration-history guard and transaction; no `IF NOT EXISTS`/`ON CONFLICT` inside
the migration may adopt a previously created receipt table or pool.

Resolve lifecycle, neutral state and neutral epoch OIDs before creating temporary
templates. Require each resolved table to live in the supported canonical schema and
resolve unqualified names to those same OIDs; do not let a temporary/search-path shadow
stand in for it. The new receipt table name must be absent. C2 predecessor tables
`Projects_OperationState` and `Projects_OperationEpochs` must remain absent. All following
catalog checks address the captured OIDs, with `pg_temp` qualification for expected tables.
The generated new table must use the same canonical schema as the predecessor, with a
postflight OID/schema check. Do not silently retarget a noncanonical search path.

Require the ordinary ReadCommitted migration transaction; refuse another isolation mode
rather than taking a snapshot before waiting and claiming it is current. Before any
retained-row query, save the previous lock_timeout, set a local five-second lock_timeout,
and take complete utility locks in this order:

```sql
LOCK TABLE "Infrastructure_DatasetLifecycle" IN ACCESS EXCLUSIVE MODE;
LOCK TABLE "Infrastructure_FiniteOperationState" IN ACCESS EXCLUSIVE MODE;
LOCK TABLE "Infrastructure_FiniteOperationEpochs" IN ACCESS EXCLUSIVE MODE;
```

The lifecycle table lock also protects the absent-singleton case. State precedes epochs;
no row/data read is used to acquire these table locks. No Project/business-table lock or
owner callback is added. If lifecycle is absent, resolve the existing Projects receipt
table, require an ordinary nonpartitioned/noninherited table without RLS or rewrite rules,
and acquire SHARE before its bounded EXISTS residue check. That branch adds no full
receipt count/address audit/template, payload read or receipt DML; it cannot falsely report empty through
row security or an unsupported relation kind. Its lock also uses the same five-second bound. Recheck captured OID/name correspondence after exclusion. Locks
last through table creation, both inserts, postflight and migration history commit. This
is a stopped-host maintenance operation, not a live-drain promise. PostgreSQL documents
ordered table-lock acquisition and the first-snapshot distinction for stronger isolation
levels in [LOCK](https://www.postgresql.org/docs/18/sql-lock.html).

Five seconds bounds each lock wait, not the complete migration. Preserve the caller's
command cancellation/watchdog and any stricter existing statement timeout; restore the
prior lock_timeout on successful completion. Do not pretend `set_config(statement_timeout)`
inside an already running DO creates a new deadline for that outer statement. If the
parent chooses an additional server statement budget, set it before sending the DO and
record that separate command boundary. No new throughput or hard rollback-time guarantee
is implied. Timeout scopes are documented in [client defaults](https://www.postgresql.org/docs/18/runtime-config-client.html).

## Exact catalog preflight

Reuse the migration-local C2 expected-template comparison, not `CREATE TABLE LIKE actual`
(which would copy the defect being checked). Three explicit `ON COMMIT DROP` templates
represent the C2 lifecycle/state/epoch post-state; they contain no retained rows. Lifecycle
uses the immutable C2 declaration. State and epochs include their neutral PoolId keys/check
names and RetentionProtocolVersion. Do not derive expected SQL
from mutable runtime constants or call a product helper from the migration.

For each actual/expected pair compare symmetric EXCEPT ALL sets of:

- Columns by name, type OID/modifier, nullability, identity/generated flags, collation and
  parsed default expression. Ignore physical ordinal position: C2 appended pool columns,
  while a canonical fresh model may order them differently.
- Every named PK/check and other non-NOT-NULL constraint: type, deferrability/deferred,
  enforced/validated/no-inherit flags and `pg_get_constraintdef(oid, false)`. Require any
  PostgreSQL18 NOT NULL constraints to be enforced/validated; column nullability must match.
- Every index: name, access method, key/include arity, unique/primary/exclusion/immediacy,
  valid/ready/live flags, null-distinct mode, collation/opclass/options, predicate, expression
  and ordered key definitions. Use index OIDs, not an unqualified same-named temp index.

Require ordinary persistent nonpartitioned/noninherited tables, no RLS/forced RLS or rewrite
rules, and no user triggers or FKs in either direction on the neutral metadata. The new
receipt has no FK in either direction or user trigger. These prevent a supposedly neutral
insert or future cleanup from acquiring hidden cross-owner locks/effects. Unexpected
outgoing lifecycle constraints are also rejected by the exact template comparison.
Projects receipt interpretation and schema evolution stay with their existing owner;
C3 does not need another complete Projects receipt catalog template. PG18 exposes the
required constraint flags in [pg_constraint](https://www.postgresql.org/docs/18/catalog-pg-constraint.html).

Expected neutral shapes are fixed:

| Table | Columns and required key/index/check shape |
| --- | --- |
| State | NamespaceId uuid, PoolId uuid, RetentionProtocolVersion int, CurrentEpoch bigint, RetiredThroughEpoch bigint; all NOT NULL/no defaults. PK(NamespaceId,PoolId). Nonempty namespace/pool; protocol>0; CurrentEpoch>0 and 0<=RetiredThrough<CurrentEpoch. |
| Epochs | NamespaceId uuid, PoolId uuid, Epoch bigint, State int, OpenedAtUtc timestamptz, AdmissionClosesAtUtc timestamptz, nullable ClosedAtUtc timestamptz, MutationDeadlineMilliseconds int, ReplayRetentionSeconds bigint; no defaults. PK(NamespaceId,PoolId,Epoch); unique (NamespaceId,PoolId) WHERE State=1. Five exact C2 checks: nonempty namespace/pool, Epoch>0, positive deadlines and AdmissionCloses>Opened, Open/null-close or Closed/non-null-close>=Opened. |

There must be exactly one neutral partial Open index and no additional metadata index.
Receipt schema postflight uses the canonical generated definition: nineteen required
columns, one address PK `(NamespaceId,Epoch,CallerKind,CallerKey,OperationId)`, exactly the
seven reviewed checks, 32-byte keys/digests, payload1..2097152, no defaults or extra indexes.
The initial caller/source domains remain exactly1; version columns stay positive rather
than being rewritten to the current wire version. No owner receipt codec is invoked.

## Retained-data guard and install fragment

The following is the exact semantic predicate/install proposal to embed after catalog
validation. It is not a runnable replacement for generated DDL/catalog guards. The SQL
uses frozen migration literals; `projects_pool`, `workbench_pool`, `active_namespace` and
`issued_at` are local DO variables. Do not persist this proposal as a migration until
parent generation/review. The GUIDs are permanent, not test-generated values.

```sql
projects_pool constant uuid := 'e6eaf1c1-5dbc-46bf-8641-1ae9fe3ae376';
workbench_pool constant uuid := 'c91aaef3-cfc1-4c29-88aa-10693fecd4e3';
```

Require zero or one lifecycle row using the exact singleton schema. A present row must
have Id1, State1, SchemaVersion1 and GuardProtocol1. Existing Workbench metadata, any other
pool and any unsupported retention protocol are refused globally, including in inactive
namespaces. Do not treat a matching existing Workbench state/epoch as already installed.

```sql
IF EXISTS (SELECT 1 FROM "Infrastructure_DatasetLifecycle"
    WHERE "Id" <> 1 OR "State" <> 1 OR "SchemaVersion" <> 1 OR "GuardProtocol" <> 1)
 OR EXISTS (SELECT 1 FROM "Infrastructure_FiniteOperationState"
    WHERE "PoolId" <> projects_pool OR "RetentionProtocolVersion" <> 1)
 OR EXISTS (SELECT 1 FROM "Infrastructure_FiniteOperationEpochs" e
    LEFT JOIN "Infrastructure_FiniteOperationState" s
      ON s."NamespaceId" = e."NamespaceId" AND s."PoolId" = e."PoolId"
    WHERE e."PoolId" <> projects_pool OR s."NamespaceId" IS NULL
      OR e."Epoch" > s."CurrentEpoch") THEN
  RAISE EXCEPTION USING ERRCODE = 'P0001',
    MESSAGE = 'Workbench task-plan installation requires supported retained Projects metadata.';
END IF;

SELECT "NamespaceId" INTO active_namespace FROM "Infrastructure_DatasetLifecycle";
IF active_namespace IS NULL THEN
  IF EXISTS (SELECT 1 FROM "Infrastructure_FiniteOperationState")
    OR EXISTS (SELECT 1 FROM "Infrastructure_FiniteOperationEpochs")
    OR EXISTS (SELECT 1 FROM "Projects_DefinitionReceipts") THEN
    RAISE EXCEPTION USING ERRCODE = 'P0001',
      MESSAGE = 'Workbench task-plan installation cannot adopt retained data without an active lifecycle.';
  END IF;
ELSE
  IF NOT EXISTS (
      SELECT 1 FROM "Infrastructure_FiniteOperationState" s
      JOIN "Infrastructure_FiniteOperationEpochs" e
        ON e."NamespaceId" = s."NamespaceId" AND e."PoolId" = s."PoolId"
        AND e."Epoch" = s."CurrentEpoch"
      WHERE s."NamespaceId" = active_namespace AND s."PoolId" = projects_pool
        AND s."RetentionProtocolVersion" = 1 AND e."State" = 1)
    OR EXISTS (SELECT 1 FROM "Infrastructure_FiniteOperationEpochs" e
      WHERE e."State" = 1 AND e."NamespaceId" <> active_namespace)
    OR EXISTS (
      SELECT 1 FROM "Infrastructure_FiniteOperationState" s
      WHERE s."NamespaceId" = active_namespace AND s."PoolId" = projects_pool
        AND (SELECT count(*) FROM "Infrastructure_FiniteOperationEpochs" e
          WHERE e."NamespaceId" = s."NamespaceId" AND e."PoolId" = s."PoolId"
            AND e."Epoch" > s."RetiredThroughEpoch" AND e."Epoch" < s."CurrentEpoch"
            AND e."State" = 2) <> s."CurrentEpoch" - s."RetiredThroughEpoch" - 1) THEN
    RAISE EXCEPTION USING ERRCODE = 'P0001',
      MESSAGE = 'Workbench task-plan installation requires complete active Projects epoch readiness.';
  END IF;
END IF;
```

The unique Open index plus exact current Open and global foreign-namespace Open refusal
establishes the single current head. PK/range checks plus the closed-prefix count detect
active holes without `generate_series` or allocation proportional to CurrentEpoch. Do not
require current/prefix rows for inactive namespaces, and do not shrink/recompute any old
admission, deadline or replay promise. Old closed epochs may carry valid historical
values different from current settings. The subtraction is overflow-safe because the
validated state constraint gives 0<=RetiredThrough<CurrentEpoch<=bigint maximum; subtracting
RetiredThrough and then1 stays in range. No increment, generated numeric sequence or
loop over epoch values is needed.

Neutral metadata readiness is sufficient for this distinct pool installation. Do not add
an O(receipts) address audit: C3 neither reinterprets nor mutates Projects receipts, and the
existing owner remains responsible for their validity. Only the lifecycle-absent branch
uses EXISTS to refuse receipt residue without activation. That probe can stop at its first
visible tuple; an empty table with dead tuples can still require visibility work, so this
is a bounded-result query, not a constant-time claim. No AcceptedPayload is selected.
Existing business rows without lifecycle are not a reason to invent activation; only the
finite metadata/receipt absence gate applies here.

Only after these guards create the canonical receipt table. Then, only when
`active_namespace IS NOT NULL`, issue exactly one new pair using one captured server time:

```sql
issued_at := clock_timestamp();
INSERT INTO "Infrastructure_FiniteOperationState"
  ("NamespaceId", "PoolId", "RetentionProtocolVersion", "CurrentEpoch", "RetiredThroughEpoch")
VALUES (active_namespace, workbench_pool, 1, 1, 0);
INSERT INTO "Infrastructure_FiniteOperationEpochs"
  ("NamespaceId", "PoolId", "Epoch", "State", "OpenedAtUtc", "AdmissionClosesAtUtc",
   "ClosedAtUtc", "MutationDeadlineMilliseconds", "ReplayRetentionSeconds")
VALUES (active_namespace, workbench_pool, 1, 1, issued_at, issued_at + interval '86400 seconds',
  NULL, 30000, 604800);
```

`clock_timestamp()` is captured after waiting/validation, never the migration transaction's
start time. Both timestamps derive from that single value; PostgreSQL distinguishes clock
and transaction time in [date/time functions](https://www.postgresql.org/docs/18/functions-datetime.html).
The migration promise is intentionally 86400 elapsed seconds/30seconds/seven days,
independent of runtime configuration. Use seconds rather than a calendar `interval '1 day'`
so session timezone/DST cannot shorten or lengthen the promised elapsed window. Future rotation uses validated configured options; these issued values
remain unchanged. A fresh schema inserts no pair and explicit bootstrap later installs
all compiled pools. No Workbench row is added to an inactive namespace.

Postflight requires the exact new receipt catalog, empty receipt table, and either no
Workbench metadata (absent lifecycle) or exactly the one just-issued state and epoch,
including complete timestamp/deadline/version equality. Re-run active readiness for the
two known pools and global no-foreign-Open/unknown/orphan-epoch rules using their explicit set;
do not accidentally run the pre-install Projects-only predicate after adding Workbench.
Existing table OIDs/schema remain unchanged and obsolete table names remain absent.

Preserving Projects bytes follows from the write perimeter: only CREATE TABLE for the new
family and INSERT of the distinct Workbench PoolId are allowed; no UPDATE/DELETE/ALTER of
Projects, neutral predecessor or lifecycle rows. Metadata triggers/FKs/rules are refused
before those inserts. Avoid copying or serializing all receipt payloads into a temporary
correspondence table merely to prove a non-write. The real upgrade tests compare retained
Projects/receipt/lifecycle values across commit/restart; that is additional executed
proof, not a reason to widen the production migration's payload reads.

## Permanent refusal and operational cost

Down contains only the unconditional refusal, before any generated DropTable or metadata
removal. Retain no unreachable destructive generated Down operations:

```sql
DO $c3_down$
BEGIN
  RAISE EXCEPTION USING ERRCODE = 'P0001',
    MESSAGE = 'Workbench task-plan downgrade is unsupported. Use an explicitly coordinated stopped-host restore with matching binaries.';
END;
$c3_down$;
```

Use P0001 for every deliberate compatibility refusal, preserving Npgsql IsTransient=false;
SQLSTATE55000 was observed as transient and is intentionally not reused. Do not catch and
normalize unrelated cancellation, lock timeout, permission or storage errors into this
compatibility refusal. No automatic retry/adoption follows. Older Projects-only readiness
must reject the new active pool set; new AlreadyApplied/Ready/Attach/Resume must reject
missing Workbench metadata rather than initialize it.

Catalog work is fixed to three exact predecessor descriptors and one new table, plus the
minimal Projects receipt relation check only for absent lifecycle. Guard data work is
linear in retained neutral metadata in the worst case, with indexed namespace/pool/epoch
joins. There is no active-install receipt count, copy, address scan or payload read. Prefix count scales with
retained rows, never the numeric epoch range. PostgreSQL may choose/spill a join plan;
this source design does not assert a measured memory or latency ceiling. Only two small
rows and the empty table/index/check catalog are written. Record actual migration time,
retained state/epoch counts and query plans on an owned representative fixture;
large retained distribution/capacity remains an open gate. No broad data export is needed.
