# Resource revision migration source checkpoint

The parent selected [the generation plan](../reviews/P05-R1-resource-migration-generation-plan.md)
at `6178AE05…`, including no persistent UUID default and permanent P0001 Down. The
canonical migration remains parent-owned and ungenerated for this slice. This checkpoint
authors one new Integration file, maintained migration guidance and a fail-closed SQL
review proposal. It changes no product model, migration, snapshot or main file.

`ResourceMetadataMigrationTests` has exactly **three Facts**, no theories, dynamic data,
skip or category exclusions. Filter:
`FullyQualifiedName~CanDoItAll.Tests.Integration.ResourceMetadataMigrationTests.` in
`tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj`.
Expected discovery/runtime 3 is source-derived; neither has run. The existing 343 ordinary
and four separate browser rows are unchanged. Including these three gives 346 ordinary
plus four browser, without adding another copy of stage1's fourteen or ResourceTestLab's ten.

The first Fact uses the actual C3 predecessor, separately covers empty and six retained
rows, preserves the nineteen-column old projection and whole Search rows, and verifies
distinct nonempty revisions unchanged through repeat migration/reopened contexts. Retained
shapes include actual ordinary/governed connector keys, unbound/null-lifetime references,
unknown connectors and malformed/oversized JSON. No editor or plugin normalizes those bytes.

The second Fact starts from an empty database through the full canonical migration chain.
It asserts canonical 169 / owner 1 and model parity, exact twenty-column catalog, required uuid
without default, validated/enforced nonempty check, Id PK and plain valid nonunique btree
on the three ordered candidate fields. Real omitted/null/empty revision attempts fail;
application-created valid revisions persist and duplicate candidate keys remain permitted.
Existing eighteen owner Facts remain the actual service CAS/atomic Search proof.

The third Fact reuses one predecessor fixture across five repaired drift arrangements:
partial Revision, changed Name type, missing PK, RLS and a user trigger. Each refusal
must be permanent P0001 and preserve corrupted pre-attempt schema, history, data and Search.
It then observes the real held relation's blocker PID in `pg_blocking_pids` for the exact
migration connection before requiring lock-timeout refusal and no partial installation.
Holder release and pending migration drain are awaited terminal before context disposal.
A separate later admission upgrades successfully, then Down must preserve schema, history,
row revisions and data while refusing permanently. No timeout around noncancelable disposal
claims that underlying cleanup completed; the parent runner owns any outer process deadline.

One private fixture type reuses the existing isolated PostgreSQL lease implementation.
It adds no Docker provisioning, host process, environment directory, new connection policy
or product dependency. The four owned database leases arise only when these Facts execute:
two empty/retained upgrade arrangements, one fresh catalog arrangement and one reused drift/
lock/Down arrangement. No database has been created during source preparation.

The SQL proposal is not executable migration evidence. Its generated-DDL marker raises
P0001 unconditionally until the parent inserts the exact reviewed column/backfill/check/
index body and generated index name. One temporary descriptor validates the supported
nineteen-column predecessor and twenty-column successor, keys/checks/indexes/defaults,
ordinary relation and unsupported custom schema behavior under actual relation exclusion.
No receipt, Projects, Search or lifecycle row is inspected or rewritten. The only intended
business-table DML is the one-time new Revision backfill; preservation is established by
reviewing that fixed write perimeter and the retained-data Fact, not a full payload audit
inside the migration. Five-second lock_timeout is a wait bound, not a migration-duration SLA.

Observed prerequisite identities before this proposal:

- Resource model: `2B374A5FC43379D72B631216B1F22652D769B5C38844732AE7B5F5E4E913E833`.
- Resource owner context: `71FC60D3B778A7ACBA6772CD0910EFE53F8665EBF95AC90D323AFB78229C8878`.
- Main C3 migration `20261017053554_AddWorkbenchTaskPlanReceipts`:
  `207226CED8E8DF29CEB9782B1CAD070A5CC399C9FA8EE19A9380D3EDE2C3A861`.
- Main canonical snapshot: `338896028E3206C613D34978C1F250963EC40037976E7EF5A6AD1DBFCFB2D79B`.

Generate the subject with suffix `AddResourceMetadataRevisions`; tests resolve its actual
generated timestamp. No timestamp or source migration is fabricated here. Recheck these
predecessors at transfer/generation and keep original Resource/stage2 inventories intact.

Required parent closure is canonical generation/manual SQL review, affected Release builds,
exact discovery and actual PostgreSQL execution, retained failure/cleanup evidence and full
portability/rendering enforcement. Runtime schema cost and retained compatibility remain
unproved. Shared Resource project gates/promotion G+R and capacity are still held after this
source checkpoint; these Facts do not release stage3 or claim durable Resource receipts.
