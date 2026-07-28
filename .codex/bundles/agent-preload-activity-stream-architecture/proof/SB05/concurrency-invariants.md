# SB05 Backend Concurrency and Storage Invariants

## Invariant matrix

| ID | Required invariant | Production enforcement | Adversarial proof | Result |
| --- | --- | --- | --- | --- |
| `SB05-CONC-001` | Typed `Accepted` exists before catalog/provider/store work | `AgentExecutionActivityCoordinator.AdmitOperation` publishes the first sequenced activity synchronously | Startup ordering is identical across 20 final scenario executions; activity-admission group passed 11/11 | Pass |
| `SB05-CONC-002` | One owner serializes a file-store mutation | in-process gate plus `FileSandboxWorkspaceCrossProcessLock`; atomic start validates catalog/session/run under the held boundary | competing start, paused-commit reader, stale revision, dropped-history, cancellation, and corrupt-journal cases | Pass |
| `SB05-CONC-003` | Interrupted multi-file commits recover idempotently | typed journal is persisted before commit stages; recovery preflights and rolls forward | generic-new 6/6; combined generic/chat/update 33/33 | Pass with P2 durability follow-up |
| `SB05-CONC-004` | Admission read count is independent of historical run count | chat index and latest-run header path avoid directory/payload scans | 4 versus 96 runs: new remains 11 physical JSON reads; existing remains 15 | Pass |
| `SB05-CONC-005` | Usage updates scale with the affected delta | incremental agent/provider/model aggregates | canonical rebuild equivalence, delimiter-bearing keys, removal and identity-change rejection; storage group 10/10 | Pass |
| `SB05-CONC-006` | EF work never overlaps on one scoped context | provider factory creates contexts; process stores remain sequential; state/assignment reads batch IDs | batched process test plus source scan showing no `Task.WhenAll`/`Parallel.*` in reviewed chain | Pass |
| `SB05-CONC-007` | Capability/runtime composition stays ordered and per-run | execution builds mutable capability/tool/runtime state sequentially | architecture/source review; no parallel composition primitive in the path | Pass |
| `SB05-CONC-008` | Provider publication is immutable and fenced | scalar revision probes, publication generation, rebuild gate, atomic immutable state replacement | current/changed/delete/failure/superseded-profile tests | Pass with P2 cross-host window |
| `SB05-CONC-009` | Slow compatibility consumers do not gate canonical send/activity completion | isolated compatibility dispatcher owns subscriber mailboxes | blocked subscriber test in the 11/11 activity group | Pass; database-switch notification remains synchronous |

## Complexity boundaries

- New-session admission: 11 physical JSON opens at both 4 and 96 historical runs.
- Existing terminal-latest admission: 15 physical JSON opens at both 4 and 96 runs.
- Chat index record lookup is O(1), but the current JSON index representation still
  has O(R) bytes and parse CPU as the index grows.
- Usage projection delta work is O(A + P + M) for affected agent, provider, and model
  aggregates.
- Process enrichment selects at most 10 runs and batches runtime state/assignment
  reads; EF split-query command count is described in
  `bundle://proof/SB05/ef-query-proof.md`.

## Residual P2 risks

### Synchronous database-switch subscribers

`DatabaseSwitchNotificationService.Publish` invokes subscribers one by one on the
switching thread. It aggregates exceptions, but one blocked subscriber can still
delay the switch. Canonical agent compatibility subscribers are isolated; this
remaining control-plane event is not. A future repair should give profile switching
a bounded/asynchronous notification policy without allowing stale-profile use.

### No physical flush durability proof

The WAL tests prove process-crash interruption and idempotent roll-forward at every
injected commit stage. `WriteJsonAtomicallyAsync` currently uses
`File.WriteAllTextAsync` followed by atomic replacement and does not call
`FileStream.Flush(flushToDisk: true)` or prove directory-entry durability. The tests
therefore do not prove survival of power loss or storage-controller cache loss.

### Final provider cross-host validation window

The provider service probes the canonical revision immediately before returning or
refreshing an immutable lease. A different host can still commit after that final
probe and before the caller uses the lease. The next acquisition detects the change,
and local commits update the snapshot immediately, but there is no distributed
transaction spanning provider validation and external provider use.

None of these P2 items permits shared `DbContext` access, stale snapshot write-back,
or silent recovery failure. They are explicit hardening work, not hidden fallback
mechanisms.
