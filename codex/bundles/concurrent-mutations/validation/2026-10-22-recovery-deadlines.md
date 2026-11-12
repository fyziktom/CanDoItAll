# Cooperative recovery deadlines

Status: **Pass, Behavioral.** All 53 selected cases pass: 41 Unit and 12 Integration.
This qualifies the cooperative coordinator boundary; worker activation remains gated.

The existing Application coordinator owns a linked deadline through claiming, stage
work, checkpointing and yielding. A deadline returns the appended TimedOut outcome
without a cleanup write. Caller cancellation propagates. Both paths await owner unwind;
noncooperative work remains awaited and cannot start another checkpoint after expiry.
The database retains the actual original intent and lease, including a checkpoint that
committed before its acknowledgement was lost. A successor reads that state after expiry.

Seven new Unit cases cover claim, owner, checkpoint, committed checkpoint, noncooperative
return and both caller/deadline orderings. Two native Integration cases hold real File
coordination and PostgreSQL row locks. One-second deadlines unwind in 1015.7 ms and
1021.3 ms respectively; original evidence survives expiry and takeover, and stale
checkpoint attempts remain NotOwned. These local samples are not a latency guarantee.
The native tests use a supporting stage over actual owners, not the hosted worker.

Existing coordinator (22), contracts (12), queue (8) and registered completion (2)
regressions pass. Exact discovery matches execution. Seven production owners and both
test projects build in standard Release with local libraries and serialized outputs.
The initial Integration build's three test-only reference errors are retained; the
corrected build passes. All 44 source pins and 44 owning/copied outputs remain unchanged.

Full portability passes without a baseline edit: **15674 reviewed findings unchanged**.
Validation-tool tests, rendering register, documentation and whitespace checks pass.
Scoped CodeAnalytics reports 415 types, 4233 to 4234 members, 109 unchanged findings,
zero diagnostics and zero cycles; dependencies are unchanged. Only the enum gains a
member. No new production type, reference, partial owner or detached task is introduced.

All six observed execution commands reconcile with zero new process, database or
temporary-path residue. Two bulk scans were removed after exact size/hash/containment
checks and verified absent. Durable proof scanning reports zero findings. The raw local
scan's three matches are reviewed variable assignments in Execute.ps1, with no private
sentinel. Its raw failure and exact helper identity remain retained. Historical denied
cleanup targets and ordinary 5032 remain untouched.

[Compact proof](../proof/2026-10-22-recovery-deadlines.json): 86572 bytes, SHA256
`7745C297AED23BA40E007F54985A105A8E4D45E8AFF52207BBFFC9B335833AD5`.
Local records remain in `.artifacts/concurrent-mutations/validation/c4-recovery-deadlines`.

Next qualify finite hosted scheduling, live/legacy fairness and actual OS restart.
Whole-journal capacity, HTTP cutover, snapshot refresh/settings, remaining writers and
final E2E remain open. Broad Stable waits for the final integrated candidate. SPA stays
on `codex/concurrent-mutations`; main is unchanged.
