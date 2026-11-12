# P02b migration, maintenance and transfer outcome review

Reviewed 2026-10-16 against the current candidate. This is source review; parent-owned
build-backed discovery, execution and migration parity remain the validation authority.

## Migration and owner mapping

`20261016163615_AddProjectDefinitionOperationLifecycle` adds exactly the lifecycle,
Projects operation state, epoch and receipt tables plus the partial unique open-epoch
index. Its owner configurations in `DatasetLifecycleModels.cs` and
`ProjectDefinitionOperationRecords.cs` agree with the canonical snapshot by inspection.
Parent independently reviewed generated idempotent SQL: four new tables and one partial
index, migration-history guards and transaction, no destructive upgrade DDL.

The receipt address primary key includes namespace, epoch, trusted caller kind/key and
operation ID. Checks bound caller/digest widths, nonempty identities, command/version
fields, accepted payload size and the create-only compensation fingerprint. Epoch
checks cover open/closed timestamp shape and positive issued promises. The state row
constrains retired-through below current; only one Open epoch exists per namespace.
The runtime rejects unsupported lifecycle versions/protocols rather than interpreting
the database's positive-version constraint as permission to use an unknown protocol.

No receipt-to-project cascade exists: retained operation evidence must survive deletion
of current business rows. Epoch/state cleanup is explicit and bounded. The absence of
foreign keys is intentional; owner initialization/readiness rejects inconsistent current
epoch state and import never copies live receipts or epochs as business DTOs.

`ProjectDefinitionOperationStore.FindLockedAsync` uses every receipt primary-key field.
`ObserveAsync` reads lifecycle, state, requested epoch, exact caller receipt and target
availability in one MVCC statement. Its joins match the lifecycle singleton, state and
epoch keys, receipt key, Project ID and retirement lifetime key. Current purge reads a
namespace/epoch receipt prefix ordered by the remaining key fields, locks at most 128
candidate rows and deletes at most 16 MiB of logical accepted payload. An AcceptedAt
index or payload index would not support a current query and is not proposed.

No release-blocking mapping/index mismatch was found. This does not establish an index
plan for millions of epochs or sustained receipt traffic. One-second admission windows
with long retention can create large metadata populations; bounded DELETE output alone
does not bound eligibility-search work.

## Maintenance scope and remaining capacity gate

The current worker has a one-minute cadence, 64 batches per cycle and one shared two-second
budget. Its structural row ceiling is 8,192 per minute before contention/byte limits.
Observed finite-backlog results of 8,192 rows in 823 ms and 192 rows in 42 ms establish
neither sustainable arrival capacity nor vacuum/reuse behavior. Rotation-only watermark
advancement makes default physical retention approximately 8–9 days plus backlog delay.

The next exact proposal is
[`P02b-retention-capacity-amendment.md`](../plan/P02b-retention-capacity-amendment.md):
bounded configurable cadence, typed row/byte/backlog observations and failure reporting,
then independent bounded contiguous retirement while the current epoch is Open. That
last change must also make rotation lock state before epoch; introducing an independent
watermark writer without changing rotation's current read/update order can lose progress.
No such independent writer exists in the current implementation.

Every purge DELETE repeats the exact active-binding predicate and eligible-retired
condition; its earlier precheck alone grants no authority. Active retirement refuses
old operations before physical deletion. Inactive namespaces retain both original replay
and closed-at retention horizons. No per-write quota/global counter, wider purge
transaction or automatic shorter promise is justified by the present observations.

## Generic transfer completion

`DatabaseTransferOperationRunner.RunTransferAsync` records the exact result only after
target CommitAsync returns. A later source commit, transaction/context disposal or
connection-close failure retains it in `DatabaseTransferCommittedException` with exact
profile IDs. `DatabaseTransferService` accepts only matching IDs and handler key and
returns its copied count with a typed warning. New logs contain IDs/counts/failure type,
not raw exception/SQL/driver details. CanConnect-only preflight contexts are disposed
before any handler dispatch.

The Workspace adapter maps warnings and non-success groups to Partial; the existing
ledger retains Partial, disables further dispatch and displays expanded warning details.
An independent UI review confirmed that refresh does not clear this state. A throwing
target CommitAsync never claims known commit, even when the server committed. There is
no durable transfer receipt across process loss; operator review remains required.

## Exclusive import blocker and repair

The initial review found a release-blocking data-loss path. In
`ProjectPackageService.ImportAllAsync`, the old `committed` Boolean was assigned only
after `RunLockedImportAsync` returned through transaction/connection cleanup. A cleanup
error after acknowledged SQL commit, or a lost commit acknowledgement, left it false.
The finally block then called `CleanupStagedWritesAsync`, physically deleting imported
assets now referenced by committed target rows. Its public error also falsely claimed
the target had not been replaced. An extraction-directory cleanup error could separately
hide already accepted import results.

There are exactly two production `RunExclusiveImportAsync` callers, both through the
Workbench `ProjectTransferTargetStateGuard.RunLockedImportAsync`: database-row Projects
transfer and package import. The repair is confined to these paths:

- Infrastructure retains commit knowledge outside the complete exclusive-import and
  maintenance-context lifetimes. `DatabaseImportCommitException` carries only target
  profile ID plus Acknowledged or Unconfirmed knowledge and the internal cause. It
  contains no generic result payload. Knowledge becomes Unconfirmed before invoking
  CommitAsync and Acknowledged only after it returns. Precommit errors keep their original
  behavior; every exception after commit invocation is conservatively non-compensable.
- Workbench retains its own typed result. Row import maps acknowledged knowledge to the
  existing committed-transfer exception, preserving exact counts. Unconfirmed commit
  returns explicit no-repeat text and remains Partial at the Workspace boundary.
- Package import retains knowledge outside storage orchestration and extraction cleanup.
  Only known precommit failures compensate staged assets. Acknowledged cleanup failure
  returns exact accepted counts plus `ProjectPackageImportWarning.CleanupIncomplete`;
  uncertainty returns `ProjectPackageErrorCodes.ImportOutcomeUnconfirmed`, retains bytes
  and identifies the package/target for inspection. Neither branch asserts rollback.
- The native package handler holds further import on either typed outcome, preserves
  acknowledged counts and displays the owner's uncertainty message. Later UI refresh
  cannot convert this into a fresh import command.

`ProjectTransferHistoryPersistenceTests.ImportCommit.cs` adds six discovery rows using
the existing real PostgreSQL fixture and actual file placement: four package cases
(flushed rows then precommit failure, server commit then lost acknowledgement,
acknowledged commit then connection cleanup failure, and post-owner publication failure)
and two row-transfer outcome cases. They verify bound file bytes, persisted rows, exact
acknowledged counts, safe compensation before commit, no false acknowledgement, and
sanitized diagnostics. Two component cases cover package warning/uncertain holds.
No build or test was run by this implementation agent.

The protocol prevents destructive automatic compensation under uncertainty; it does
not create a durable package-import journal. A process crash can leave staged assets
requiring operator inspection. No automated retry or garbage collection may treat such
assets as unreferenced based solely on a missing in-memory acknowledgement.
