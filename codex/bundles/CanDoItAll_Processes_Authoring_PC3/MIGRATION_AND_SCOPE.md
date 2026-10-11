# PostgreSQL migration, scoped authority and operating limits

## Canonical model and additive migration

The current Processes runtime context uses the canonical PostgreSQL migrations assembly.
The migration project's design-time factory builds AppDbContext from the complete composition
module catalog. Add the new entities/configurations to the correct module/model registration
and append a normal migration in `src/Foundation/CanDoItAll.Migrations.PostgreSql` with its
model snapshot. Do not generate a separate Processes-only schema, use runtime EnsureCreated
on a production DB, edit an applied migration or reset a baseline to hide a missing table.

Review generated SQL against the complete model: no unrelated table drops/column rewrites.
Update the current database transfer/model-coverage contract as required. The narrow runtime
DbContext and the canonical application model must agree on the new tables and conversions.
Follow current migration README/testing commands; pending-model validation uses AppDbContext.
No SDK/provider/PostgreSQL major upgrade is part of PC3.

Use explicit owned PostgreSQL 18 fixtures per current testing.md. Keep credential-bearing
connection strings private and record only server major/patch and sanitized fixture identity.
Do not start/reuse the ordinary Compose stack, installed resources or port 5032. Test fixture
teardown must be bounded and report failures; it must never delete a non-owned database.

## Concurrency and database policy

Test against PostgreSQL, not only EF InMemory. The database must enforce unique global/project
aggregate keys and operation identities. Cover simultaneous first writes and concurrent writes
from separate contexts/connections; process-local locks are insufficient. Ensure all required
head/publication/receipt writes and project-admission checks participate in the same physical
coordinated transaction. Respect existing lock ordering to avoid deadlocks with retirement/
transfer and other owners. Do not hold locks during browser waits or external execution.

CAS failure is a conflict with no partial patch. Duplicate operation ID/same fingerprint is
recovery, not another write. Different fingerprint must fail. Faults after a real COMMIT and
before response must preserve known identity or recover it durably. Use typed safe diagnostics;
do not expose SQL, connection strings, raw exception details or profile secrets to UI.

## Project/profile lifetime and transfer

Capture existing project admission at the original native read/intent boundary and carry it
through writes. Revalidate using the existing ProjectWriteAdmission mutation gate in the
coordinated transaction. Reject a retired/recreated project with the same public GUID. Bind
profile-global writes to their original current profile; a profile switch cannot redirect
in-flight work to another DB. UI generation fences protect presentation only.

The current Processes transfer target-state participant checks runtime/prepared residues and
locks specific entity types. Include new authoring/publication/receipt facts in the applicable
owner/transfer policy and model coverage. Existing supported transfers must preserve their
meaning; unsupported cases fail explicitly before partial import. Do not invent a broad new
cross-database migration facility. Imported historical scope IDs are not current write
admissions, and source receipts cannot authorize target writes. Test non-empty target,
retired project and same-public-ID recreation paths without weakening existing guards.

## Fresh, upgrade and restart evidence

1. Fresh canonical schema: new aggregate works under normal production DI; model has no
   pending changes and all new constraints operate on PostgreSQL.
2. Upgrade a fixture populated with the pre-PC3 schema and representative workflows,
   process runtime plans, assignments, prepared admissions, projects and receipts. Apply the
   append-only migration; original data and recovery behavior survive. Reapplying the normal
   migration path is idempotent, not duplicate authoring rows.
3. Persist definitions, roles, steps, semantic canvas actions, positions and materialized
   imports; dispose scopes and open new ones, create a separate Web session, then terminate
   and start a real owned application OS process against that same fixture. Verify native
   read-back and immutable publication/launch identity at every layer.
4. Run two isolated database profiles and project lifetimes, including matching public IDs.
   Confirm no state/cache/receipt bleed. Global-scope uniqueness is tested concurrently.
5. Verify transfer/model coverage and the supported backup/restore path when touched. Generate
   current migration SQL/pending-model evidence through the safe canonical factory/config.

Changing a DI root is not test 3's process restart. A fixture retaining an owner in one
circuit is not a separate scope proof. Positive discoverable tests belong in the repository;
ignored local diagnosis programs alone do not satisfy these obligations.

## Existing in-memory drafts and deployment/rollback

Old instance-local snapshots were not durable records. Do not claim that a schema migration
can recover drafts lost before it ran. Document the operator precaution for still-open old
editors and preserve existing durable runtime/history data. Do not fabricate a recovery
migration from template defaults as if it were the user's lost edits.

Use an additive deployment sequence and explicitly state the supported reader/writer version
policy. Older binaries do not know that an authored overlay exists and can still run defaults.
A feature flag implemented only in the new binary cannot make an old binary fail closed.
Do not advertise mixed-version safety without actual compatibility enforcement at a shared
boundary. During activation require compatible application/worker readers or an explicit
maintenance boundary; preserve prepared/accepted old-format inputs via tested adapters.

Before activation, rollback to the backed-up compatible state can be planned. After new
authoring writes, running an old binary or dropping tables is not lossless rollback. Document
backup/forward-fix or explicit export/reconciliation limits and retain immutable history.
Do not execute deployment, rollback or migration of real profiles in this task. All code/SQL
validation happens on task-owned fixtures only.
