# Exact original Process source in a held catalog

Status: implemented and validated on backend `concurrent-mutations`. SPA remains clean on
`codex/concurrent-mutations`, without merge or push. The preceding parent-continuation
signed commit is waiting for the local PGP prompt; subsequent slices remain unstaged.

The [design](../plan/P07-selected-process-source-authority.md) fixes a source/executor
identity assumption before inherited publication. Process source checks now resolve the
original launching Agent by exact ID from the already held primary/selected Agents.
A different executor cannot supply missing or revoked launcher permissions. Capture,
read/write validation, Project ceilings and governed receipt reads share that selection.
The existing held Workflow-source check moves into its owner and becomes origin-neutral;
its tiny runtime partial is removed. Original profile generation, scope, exact Project
lifetime and operation ceilings remain enforced.

No project dependency, interface, registration, query or lease is added. Selection scans
at most two held Agents and does not reacquire the catalog. It authorizes the original
source only: inherited publication still needs separate executor permission, selected
Process transaction authority, original approved payload and live finite work.

## Validation

All **23 selected integration cases pass**, including seven new source/executor cases.
Both primary orders, missing source and source tools/read/write/exact-lifetime revocation
are covered against real file-backed catalog leases. Existing actual Process and Workflow
admission, rollback, replay, claim and governed receipt cases also pass.

The initial candidate passed 22/23. The remaining existing deletion-order fixture invoked
a closed legacy deletion overload. It now captures original lifecycle intent before
holding source authority and dispatches through the native lifecycle owner, preserving
its SQL-before-catalog ordering assertions. The complete 23-case selection was repeated.
An exact source-manifest comparison proves this one test file is the only change between
candidates; production is unchanged after architecture review.

Standard Release builds pass: Workbench 19.8061997 seconds and Integration 42.1666843.
Exact discovery is 23; execution is 140.963078 seconds. Full portability enforcement
passes with **15,647 reviewed findings**, unchanged baseline and no write-baseline waiver.
Validation-tool tests, rendering, documentation, diff checks and exact retained-proof
secret review pass. Historical broad Stable failures remain open.

Architecture gate: Pass with follow-up. Scoped snapshots
`snap-20261020080757-9f0bb7eb` and `snap-20261020082035-9f0bb7eb` retain 1204 types,
106 registrations and 460 findings; members change from 10186 to 10187. No new finding
or project cycle appears. The existing source authority size observation changes from
37 to 38 members; one existing module cycle, five type cycles and 204 diagnostics remain.
CodeAnalytics still reports blocking diagnostics; direct Release builds are the scoped
compilation evidence.

## Resources and retained proof

Both `c4-selected-process-source-authority` and `...-final` validation roots are Released.
Ownership preceded creation. They reuse standard Release outputs and the existing isolated
PostgreSQL 18 fixture on 53619. Both complete executions are observed and their frozen
source/runtime identities verify unchanged. Final observer duration is 154.6325593 seconds.
Each root has zero remaining observed processes and zero database-name delta. Four initial
and five final unowned TEMP deltas are preserved. The shared fixture remains needed;
ordinary 5032, installed tooling and historical cleanup denials are unchanged.

Final source manifest: 9618 files,
`CE758F4A82F9A24E9C7DD813EB7B1D18CC7521EC981BA3880C49FB3B5AFF9DC2`.
Final runtime manifest: 1704 files,
`C65F90F45E2A82191BECAA69D786ED808677136133429B15CE87BFE4B2C8028A`.

UTF-8/LF proofs under `proof/2026-10-19-planning-authority`:

| File | SHA-256 |
| --- | --- |
| `c4-selected-process-source-authority.json` | `66404809FF489C5A953E6E442209A8439CD299868273E4DAD35D935AD5492180` |
| `c4-selected-process-source-authority-final.json` | `894A20E91FF9B412F825AD17A54B3AF6E0F6217ECE9C533F4F29A0DF78B314F5` |

Both retain results, failed evidence, command timings, architecture, frozen identity,
observer coverage and resource reconciliation. Final proof includes the exact test-only
production bridge. Secret review includes the private PostgreSQL sentinel without
disclosure. Actual inherited task authority/publication, Process producer and atomic
outcome accounting, approval/history/manager, Workflow, settings and capacity remain open.
