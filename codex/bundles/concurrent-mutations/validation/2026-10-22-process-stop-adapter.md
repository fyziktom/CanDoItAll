# Original Process execution stop adapter

Status: Behavioral gate passes, 55/55 selected cases. This qualifies the registered
adapter; finite recovery stages, capacity and automatic worker activation remain open.
SPA remains on `codex/concurrent-mutations`.

Application exposes a narrow port with typed root, Process run and Agent execution
identities and the existing recovery pending result. The Processes module bridges that
port to Core's guarded ordinary stop owner, the original receipt reader and the durable
root barrier. No new project reference, runtime partial, full execution runtime, provider
call, effect or HTTP path was added. Inherited planning remains with its child owner.

Before selection and at the signal/lease/publication boundaries, the adapter checks the
stopped original root. It requires exact Process claim evidence, selected run/root,
original accepted source, admitted owner fingerprint and database profile/fingerprint.
It captures current profile identity, fingerprint and generation as values, so in-place
profile changes cannot move its comparison target. A renewed current generation is
allowed while the immutable original session generation remains unchanged. Current
dispatch permission cannot replace original receipt evidence after the root is stopped.

None is returned only for Core's verified Stopped or AlreadyClosed outcome. A live
signal returns ActiveWork; busy leases, missing journals, unknown effects and unfinished
completion retain their distinct pending reasons. Invalid or rebound evidence throws
before a write; the recovery coordinator already retains uncertain intents on failure.

| Class | Project / expected / discovered / passed |
| --- | --- |
| ProcessExecutionStopperTests | Unit / 27 / 27 / 27 |
| ProcessExecutionStopperTests | Integration / 6 / 6 / 6 |
| ProcessExecutionAuthorityPersistenceTests | Integration / 22 / 22 / 22 |

Filters use exact class prefixes plus a trailing dot, with
`CanDoItAll.Tests.Unit` and `CanDoItAll.Tests.Integration.Processes` namespaces.
Pure tests prove selection/barrier/source/profile rejection at each boundary, historical
generation retention, live signalling, original replay and unresolved outcomes without
constructing a runtime or using I/O. Integration creates the new Agent journal through
the actual registered Process source policy and admission owner before saving the run,
then uses native Process cancellation and the registered stop port. It proves new-scope
replay, renewed generation, active-root refusal, accepted-source corruption, unknown
provider dispatch and explicit missing legacy journals. New scope is not host restart.
The existing 22 source/claim/lifetime cases protect the reused seed's unchanged default.

Application, Processes module, Composition, Web, Unit and Integration build in dependency
order using standard Release outputs and `--no-restore --no-dependencies
-p:UseLocalCanDoItAllLibraries=true /m:1`. All twenty owning/copied DLL/PDB identities and
source pins match before/after testing. There were no production build or test failures.
Two validation helper defects were corrected before execution: method discovery included
a nested non-test fixture method, and the initial observer required root/state ownership
fields where the original pre-create record used path. Initial evidence and the corrected
ownership record are retained; exact discovery was repeated before launching tests.

The architecture gate passes for this adapter boundary. CodeAnalytics reports 359 to 360
types, 2915 to 2917 members, the same 143 findings and zero cycles. Its 124 pre-existing
design-time diagnostics are unchanged apart from locations, including project-load
diagnostics; these snapshots are not compilation proof. The actual Release builds pass.
No finding targets the new adapter. Full portability enforcement passes without baseline
changes (15670 reviewed findings), rendering review passes all 1212 inputs, and
documentation/whitespace gates pass. Validation-tool checks remain the unchanged 6/4/7
cases qualified in the preceding checkpoint. Broad Stable remains the final integration
gate.

The retained compact proof is `proof/2026-10-22-process-stop-adapter.json` under the
bundle root. All three execution commands are covered by the resource observer, with
zero new process, database-name or temporary-path residue. Existing isolated PostgreSQL
and standard outputs were reused. Historical policy-denied cleanup remains unresolved;
no denied resource was retried.

Progression: implement omission-compatible selected-run/notification cursors and sticky
pending cycles, then the runtime/decision/shared-scope/quiescence stages. Require original
mutation receipt recovery before replacing uncertain intents. Include persisted root
local work in final quiescence. Qualify cancellation-aware waits, fairness and the compact
index cost regression before worker activation. Snapshot refresh, Workflow/settings,
remaining writers and full E2E closure remain open.
