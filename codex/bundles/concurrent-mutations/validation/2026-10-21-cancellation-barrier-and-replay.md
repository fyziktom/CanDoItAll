# Cancellation barrier and accounting replay

Status: all 84 selected cases pass, including 14 new cases. This corrects two findings
after signed `9b6785309`; it does not close the end-to-end bundle. SPA stays clean on
`codex/concurrent-mutations` at signed `70c65dd` without a merge or push.

## Findings and changes

The [entry design](../plan/P11-cancellation-barrier-and-replay.md) records both defects
before the production changes. The retained pre-fix test fails at `Assert.False` because
rework of a previously Failed step returns success and reactivates a CancelRequested
root. The runtime now explicitly refuses rework during cancellation. Under the existing
root SQL lock and current-state comparison, persistence allows CancelRequested to remain
pending or become Cancelled; Cancelled must remain Cancelled. Completed command replay
still precedes that invariant. No new query, lease, schema or project reference is added.

The terminal-accounting validator now validates its historical closure/work receipt
independently of current step status. The original request status remains bound into
the command identity and completion semantics. Immutable root/run/child/material/grant,
actor/action and measured work still bind recovery. Both snapshot modes prove legitimate
failed-step rework followed by exact receipt replay with current runtime, unchanged root
budget and Agent journal, and no repeated provider call or effect.

## Verification

| Selected lane | Discovered / passed | Seconds |
| --- | ---: | ---: |
| New runtime barrier | 3 / 3 | 4.40 |
| Terminal accounting owner, including four new current-state cases | 57 / 57 | 4.87 |
| Existing cancellation and rework methods | 9 / 9 | 4.28 |
| New persisted barrier and ordering cases | 5 / 5 | 47.67 |
| SQL/file accounting, including two new rework cases | 10 / 10 | 307.22 |
| Total | 84 / 84 | |

The direct-commit cases exercise all 17 forbidden status transitions across pending
and finalized cancellation, asserting unchanged runtime/events/outbox/receipts after
each refusal. Both deterministic cancellation/rework commit orders reject the stale
command; they are not claimed as simultaneous-race tests. The accounting class also
retains the actual competing-action, lost-acknowledgement and SQL-rollback cases.

All seven affected Release builds pass with existing warnings: Runtime 6.30 s,
Application 7.14 s, Persistence 6.28 s, Process module 89.40 s, Web 58.37 s,
Unit 31.91 s and Integration 34.76 s. Exact method inventories match built discovery.
Source remained unchanged through compilation and all test execution: 9,917 inputs,
SHA-256 `7ADC2BA9F08E8C737B6DCC497E6B786B204A9E6D2D111DF40B96788DE90856A1`.
The 8,078 runtime files across eleven standard Release output roots are unchanged,
SHA-256 `B5ED291D83A76CEC7406C53DC490465D659C197D33114C63F5D752DABF20C15A`.

Full portability scan and final enforcement pass with all 15,665 reviewed executable
findings unchanged; no baseline edit. Rendering, documentation and whitespace pass.
Scoped architecture comparisons add no type, member, edge, cycle or finding. Snapshots
are Runtime `snap-20261022002226-289734b4`, Persistence
`snap-20261022002235-531e33b7`, and Application `snap-20261022002245-bef0246a`.
Persistence analysis retains ten pre-existing WS0002 package vulnerability diagnostics;
its ordinary Release compiler succeeds with the existing warnings. Direct project
references and source review are retained alongside the bounded snapshot comparison.

The 391.17-second resource observer covers all five execution commands. Reconciliation
finds zero remaining observed processes, database-name deltas or temporary-path deltas.
The existing isolated PostgreSQL server is reused; ordinary 5032 and historical denied
cleanup remain untouched. The 116 host-load samples show median 10.18% and maximum
18.64% visible-process CPU. This is correctness proof, not a latency/capacity result.

Secret-reviewed retained proof is
`proof/2026-10-19-planning-authority/c4-cancellation-barrier-replay.json`, 389,667 bytes,
SHA-256 `C5A1825132087DE7180FCB521DBC364BEFBC220837384C5F2CE429FB0B274C5E`.
It includes the expected pre-fix failure, exact positive results, commands, architecture,
source/runtime identities, load samples and resource reconciliation. The ignored owned
validation directory retains the compact raw evidence and helpers. Documentation-only
closure edits receive a separately retained exact source bridge.

## Remaining work

Implement and qualify the selected Agent stop owner and concrete Process adapter, then
durable quiescence/recovery, snapshot refresh and supported producer activation. Existing
finite-read and sustained-capacity gates, Workflow/settings/SPA integration, remaining
writer waves and final integrated Stable closure remain open. Historical cleanup
rejections remain recorded in [resources](../resources.md); no rejected deletion was retried.
