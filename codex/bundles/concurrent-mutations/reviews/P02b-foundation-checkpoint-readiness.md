# Persistence, import and activation checkpoint readiness

Status: source review supports the incremental foundation checkpoint; no new blocking
defect was found in the reviewed frozen scope. This is not full P02 or engine-wide
closure. Parent-owned native/browser validation and the final source checkpoint record
remain authoritative. This review ran no build, test, database operation or scanner.

Reviewed the exact tracked diff and new source for Infrastructure lifecycle/activation,
transfer runner/session/plan/cleanup, Composition activation coordination, offline Web
command, Workbench package/row imports, Projects transfer residue, and their caller
adaptations. Rechecked the previously accepted
[retention implementation review](P02b-retention-capacity-review.md) against the current
unchanged maintenance implementation. Root's validation chronology is intentionally
left untouched.

## Boundaries and compatibility

- Infrastructure owns dataset identity, activation files and generic transfer ordering;
  it has no Projects reference. Composition joins SQL lifecycle activation and initial
  Projects epoch in the existing coordinated transaction. Projects owns epochs/results;
  Workbench owns package assets and import compensation. The reviewed production change
  adds no project reference or package dependency.
- The required `DatabaseTransferTargetPlan` is an intentional application API change.
  Provider, Workspace, History and Simple Chat callers now supply complete typed owner
  models before execution. History and Secret participants publish their existing model
  types instead of Infrastructure discovering module ownership. Target owner model and
  SaveChanges/command guards reject models outside the admitted plan. This is a trusted
  owner boundary, not an arbitrary-SQL security sandbox.
- Generic target transfers require an explicitly active binding. Existing migrated-only
  test targets were activated explicitly. Production package/row-import provisioning
  uses the existing bootstrapper, now including activation readiness; mismatch/Prepared
  state fails rather than silently adopting a different dataset. InMemory is confined
  to the existing explicit test branch, not a relational lifecycle bypass.

## Ordering, cleanup and outcome knowledge

- The target remains Serializable. After BEGIN, utility lock-timeout/lifecycle exclusion
  and the complete canonical table lock set precede the first target snapshot-producing
  SQL. Exact captured binding validation follows those locks. Generic transfer cannot
  add later target tables; exclusive imports retain their managed-storage session gates,
  ACCESS EXCLUSIVE NOWAIT tables and transaction keys. This preserves visibility of a
  writer that commits while transfer waits and avoids the earlier stale-snapshot defect.
- History initialization keys are canonical, sorted/deduplicated and acquired as session
  gates before BEGIN. After target locks/binding validation, the same connection hands
  them to transaction gates and releases session keys before effects. Partial acquisition,
  Begin failure and cleanup retain exact release attempts; unconfirmed cleanup clears
  the relevant connection pool. No session gate is deliberately returned held to a pool.
- Generic transfer records its exact result only after target CommitAsync returns.
  Subsequent source commit or transaction/connection cleanup cannot erase it: the typed
  exception is matched to source, target and handler, then mapped to retained counts and
  a cleanup warning. Service preflight contexts dispose before dispatch, so their failure
  cannot hide a later completed effect. Workspace maps warnings to Partial and holds
  further dispatch. New warning logs contain identifiers/counts/failure type, not raw
  exception diagnostics.
- Exclusive imports mark commit knowledge Unconfirmed before CommitAsync and
  Acknowledged after its return. Package knowledge spans owner, maintenance, storage-plan
  and extraction cleanup. Definite precommit failure compensates staged payloads;
  acknowledged or uncertain commit preserves them. Row import retains acknowledged
  counts and returns an explicit unconfirmed outcome for lost acknowledgement.
  No automatic retry or destructive compensation follows uncertainty.
- The repaired package fault assertions recognize exactly one payload and its persistent
  `.candoitall.lock` coordination file. Rollback must remove the payload; commit/lost-ack
  must retain its bytes, fresh database binding and valid managed provenance. The lock
  file is not a second asset or a provenance sidecar. Current corrected execution belongs
  to the parent's validation record; this review does not infer a pass from source.
- Retained definition receipts prevent treating a target with historical operation
  evidence as empty. Empty initialized epoch metadata alone does not block import.
  Business DTO/package transfer neither copies nor clears operation epochs/receipts.

## Activation and recovery

Prepared file precedes SQL lifecycle plus owner initialization; Active file follows a
known SQL commit. Resume reuses the exact retained plan. Attach preserves SQL namespace
and maintenance identity. Reactivation mints and persists a new namespace once, closes
old admission in the same SQL transaction, and retains old evidence for its issued
horizons. A losing concurrent bootstrap requires explicit exact-plan attachment;
ordinary startup never autoheals that mismatch.

The bounded profile binding cache removes per-write filesystem serialization; every
SQL guard/lookup still checks the exact captured binding. Supported activation remains
offline, with writers stopped. Copying SQL and control-plane files while bypassing that
protocol is not claimed safe. The offline command keeps validation enabled, omits only
seven unsupported runtime storage implementations, resolves the explicit profile and
never starts the Web host. Normal runtime registrations are unchanged by that omission.

## Gates that remain open

1. **Capacity and maintenance:** current production still has fixed one-minute cadence
   and rotation-only retirement. The full protected baseline's finite disjoint rates
   exceed the small-receipt structural cleanup cap. Implement the accepted bounded
   cadence/observable-cycle/standalone monotonic retirement slice next, with state-before-
   epoch ordering and its focused tests. Keep the 60-second default until mixed-load
   foreground/cleanup and vacuum evidence supports changing it. No production arrival
   distribution, hard disk-pressure admission control or unlimited capacity is claimed.
2. **Runtime/owner coverage:** agent/process snapshot-versus-refresh/replan settings,
   approval/checkpoint policy, remaining Projects original-state intents and other engine
   owners remain open. Protected definition writes and selected adapters do not cover all
   legacy writers or establish engine-wide lost-update prevention.
3. **Caller cutover:** final native/browser/HTTP/governed evidence and the remaining
   supported-caller inventory remain explicit in their owner records. Do not infer them
   from persistence tests. Package warning/uncertainty hold is page-lifetime state; there
   is no durable package-import operation journal or automatic recovery/retry guarantee.
4. **Validation and cleanup:** parent records final discovery/results, dependency/static
   enforcement, canonical migration parity, incremental commit and resource cleanup.
   Earlier source/test failures remain historical evidence, not replaced by this review.

These remaining gates permit a clearly labeled foundation commit, not a final completion
claim for the user's end-to-end concurrent editing request.
