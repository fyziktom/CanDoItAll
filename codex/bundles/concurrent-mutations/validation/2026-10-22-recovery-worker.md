# Finite hosted recovery and OS restart

Status: **Pass, Behavioral.** All 64 selected cases pass: 50 Unit and 14 Integration.
The worker is implemented with Enabled=false while the capacity and activation gates
remain open. SPA remains on `codex/concurrent-mutations`; main is unchanged.

## Behavior and ownership

The internal Processes module worker creates an async scope for one finite coordinator
slice. Preferred origins repeat live/live/live/legacy, including failures. Legacy seeding
follows its recovery turn, uses a separate scope and cooperative deadline, and selects
at most one page. Idle and failure pacing prevent spinning. Shutdown awaits owner unwind
before disposal. The existing Application coordinator and Persistence queue keep their
responsibilities; no production project reference or partial owner is added.

Typed options bind nested limits, validate on start and copy the immutable defaults per
instance. MCP host lanes suppress registration. Defaults are eight operations/two seconds,
a thirty-second database lease, sixteen legacy roots, one-second idle pacing and at least
25 ms between productive slices. A noncooperative owner can still exceed its deadline;
the worker does not abandon it or dispose its scope underneath it.

Four actual OS tests terminate the first child immediately before original accounting or
after accounting commits but before recovery checkpoints. Each uses the real registered
dispatcher/queue and inherited producer state, for both StepSnapshot and RunSnapshot.
The replacement process reclaims the original action, preserves saved receipts, charges
the original measured work once and completes the scope/root. Both after-accounting
rows replay twice across further lease expiries without double charging. All eight child
identities are independently present in the resource observer. Four first children are
deliberately killed; four replacements exit successfully. No listener or new stack is used.

## Validation

| Selection | Cases | Result |
| --- | ---: | --- |
| Hosted scheduling, shutdown and seed deadline | 7 | Pass |
| Options and host-lane registration | 7 | Pass |
| Existing module registration | 7 | Pass |
| Actual OS termination/restart | 4 | Pass |
| Existing coordinator and deadlines | 29 | Pass |
| Native queue and registered completion | 10 | Pass |

Build-backed discovery agrees with execution. All 57 source pins and 56 owning/copied
outputs remain unchanged. Affected production, Support, child and test projects build
in standard Release with local libraries and serialized outputs. Two initial test-only
build failures omitted SharedKernel imports; both are retained. Pre-execution review
also corrected shared options defaults, made the quota explicit, categorized OS tests
and retained repeated exact-action replay after legitimate cooperative expiry.

Full portability passes with **15675 reviewed findings**. The only added allowance is
the explicit friend-assembly declaration for the dedicated recovery test executable;
the exact baseline diff was reviewed and final enforcement omitted --write-baseline.
Tooling, rendering, documentation and whitespace checks pass. Broad Stable remains at
the final integrated freeze.

CodeAnalytics `snap-20261022204226-b1eb8ad6` to `snap-20261022205532-b1eb8ad6` reports
361 to 363 types and 2920 to 2930 members. The 143 semantic findings, production project
references and zero cycles remain unchanged. The existing large composition-file
finding reflects six added registration lines. Both loads retain 124 pre-existing
transitive NuGet audit diagnostics; this is not a clean workspace-load claim. The only
new project edge is the Integration build-only reference to the test host, which consumes
existing Tests.Support. No production assembly references a test implementation.

## Resources and progression

All eight execution commands were observed. Reconciliation reports zero new process,
database-name or temporary-path residue. Two bulk scans were removed after exact
size/hash/containment checks and verified absent. Durable proof scanning reports zero
findings; three raw local matches are reviewed helper variable assignments, with no
private sentinel. Raw results and exact hashes remain retained. Historical denied cleanup
targets and ordinary 5032 remain untouched.

[Compact proof](../proof/2026-10-22-recovery-worker.json): 138439 bytes, SHA256
`20961BBD3228D673F7027CEBEB3A1133E3DE735986A3FF3C3CC72EFE39D491A2`.
Local commands, TRX and identities are in
`.artifacts/concurrent-mutations/validation/c4-recovery-worker`.

The 3:1 proof is a per-worker opportunity test with substituted Unit ports. It does not
establish sustained multi-host fairness or latency. Next exercise the hosted loop against
a native live/legacy backlog, then resolve whole-journal capacity and HTTP/status cutover.
Snapshot refresh/settings, remaining writers and final E2E still require closure.
