# Inherited Workbench task publication authority

Status: implemented and validated on backend `concurrent-mutations`. SPA remains on
`codex/concurrent-mutations`, without merge or push. The preceding signed checkpoint
still waits for the local PGP prompt; this slice is unstaged.

The [design](../plan/P07-inherited-task-publication-authority.md) connects the original
Process parent, exact approved payload and Core live work to the existing task publisher.
One selected catalog snapshot retains the executor and original launcher. Current source
and executor permissions are independent; governed Project scope comes from the source.
The existing enlisted Process fence runs before staging and after flush. Current profile
generation is checked on every active-authority/work check, including after the last SQL
wait and for operator-launched Processes. No interface, registration, project dependency,
new query family or lock is added. The workspace gate is released before SQL waits.

All **67 selected integration cases pass**: 28 new publication cases and 39 regressions.
They cover independent launcher/executor revocation, exact Project retirement/recreation,
expired claim, work-budget exhaustion, SQL lock waits, generation changes after the
enlisted recheck, rollback, exact receipt replay and lost-acknowledgement recovery.
The real Core inherited journal, file catalog, Process state and PostgreSQL publisher are
used. The fixture's background policy is explicit test composition; product producer,
historical receipt authorization and atomic Process child outcome settlement remain open.

The first candidate passed 62/64. Two expired-parent tests correctly retained a no-effect
outcome but then incorrectly attempted a fresh checkpoint through the expired parent.
The fixture now observes that outcome without reopening parent authority. Three generation
cases were added; the entire 67-case selection was repeated. Earlier compilation/import
and diff-check failures are retained with the initial proof.

Final standard Release builds pass: Workbench 19.9554805 seconds and Integration
48.952361 seconds. Exact discovery is 67, execution 445.4319119 seconds, and complete
observer coverage 458.4885029 seconds. Full portability enforcement passes with **15,647**
reviewed findings and unchanged baseline, without write-baseline. Validation-tool,
rendering, documentation, diff and exact retained-proof secret checks pass.
Historical broad Stable failures remain open.

Architecture gate: Pass with follow-up. Scoped snapshots
`snap-20261020082035-9f0bb7eb` and `snap-20261020091029-9f0bb7eb` change 1204 to 1205 types,
10187 to 10199 members and 460 to 461 findings; 106 registrations are unchanged.
The new informational 12-member authority finding is reviewed as cohesive. No project
cycle or new dependency is introduced. One existing module cycle, five type cycles and
204 CodeAnalytics diagnostics remain; direct builds provide scoped compilation evidence.

Both validation roots are Released. They reuse standard Release outputs and the existing
isolated PostgreSQL 18 fixture on 53619. Source/runtime identities verify unchanged and
both complete executions are observed. Each root has zero remaining observed processes
and zero database-name delta; three initial and thirteen final unowned TEMP deltas are
preserved. Shared fixture use and historical cleanup denials remain open.

Final source manifest: 9624 files,
`E05ABC9A10D5F32B467DB78724AAE30377B3C443B5E11BAD979A693FE28191ED`.
Final runtime manifest: 1704 files,
`F0B21CAD756A05BF331A6971334847BFDA16BD85362B9B491987B58B2600040B`.

UTF-8/LF proofs under `proof/2026-10-19-planning-authority`:

| File | SHA-256 |
| --- | --- |
| `c4-inherited-task-publication-authority.json` | `F6E9228998646F9195593F79EFEBB113DE1FC50CEA205A62053C040E4754F03F` |
| `c4-inherited-task-publication-authority-final.json` | `F831DBA3C203BE9BF2065F15FAEFADC0231CBC677A1A629B508D5081D8BA7C9A` |

Both proofs retain results, failed evidence, command timings, architecture, frozen
identities, observer coverage and resource reconciliation. Exact secret review includes
the private PostgreSQL sentinel without disclosure. Historical source/receipt recovery,
producer/manager/approval activation, atomic outcomes, Workflow, settings and capacity
remain the next integration work; this is not end-to-end closure.
