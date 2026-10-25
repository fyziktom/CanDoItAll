# Closed initial preparation failure

Status: 202 distinct behaviors qualified by 201 initial passes and one passing exact
restart correction. All three frozen candidates are retained.
This checkpoint does not activate the Process executor or close the end-to-end bundle.

The [accepted design](../plan/P07-closed-startup-failure.md) closes only known local
preparation failure: the callback has finished, no save was attempted, actual elapsed work
is valid and lease cleanup succeeded. Core issues sealed in-memory evidence. Workbench
matches the original child, Agent, Project, profile, parent envelope and allocation, and
requires stable missing historical storage. Process closes the child and settles its
actual work atomically. Uncertain saves, cleanup and crashed work remain reconciliation.

The initial candidate discovered and executed 202 cases: 201 passed and one failed.
The failure occurred after the full host restart when the new test resolved scoped
IProcessRuntimeStateStore from the root provider. Its correction creates and disposes a
real async service scope and resolves both runtime store and result coordinator there.
No production behavior changes for that correction.

The scope-corrected candidate executed one case and failed before successful replay.
Its generic JSON round trip did not reconstruct readonly Process identifiers. The
second test-only correction uses the existing Process persistence converters, asserts
exact serialized command equality and a valid bounded result before restarting, and
includes runtime refusal diagnostics if replay still fails. Production remains unchanged.
All 392 production DLL copies match the initial candidate, and the only source changes
are this validation record, the resource ledger and the one test file.
The corrected restart case passes: the exact committed receipt replays after full host
restart without live evidence or another owner-outcome read.

The passing selection covers failed/cancelled preparation, overruns without clamping,
invalid clocks, cleanup errors, attempted/lost saves, competing fresh/recovery callers,
original identity substitution, stable missing versus existing/unstable storage, typed
in-memory forwarding, JSON exclusion, atomic settlement, exact receipt replay, historical
outcomes, both snapshot modes and selected preparation/authority regressions.

Eight affected production/test builds passed across the recorded attempts. The first
Workbench compile needed an AgentFramework.Models import; the second identified redundant
construction around an already typed DatabaseProfileGeneration. Each failed build retained
its unchanged source identity. The final Workbench, Unit and Integration builds passed in
standard Release outputs.

The seven-project architecture comparison adds seven types and 35 members. All nine
project edges and nine existing cycles remain unchanged. Two new informational member-count
findings are reviewed in the design: the immutable evidence carries one attempt's identity
and measurement; the admission coordinator still owns serialized admission/cleanup.
There is no new project reference, runtime partial, table, migration, successful-path
write or selected-source SQL query. Existing analyzer diagnostics remain.

Full portability enforcement passes with 15,657 reviewed findings and no remaining delta.
The one added case-policy finding uses existing ordinal case-insensitive executor-token
semantics, not a filesystem comparison. Documentation and whitespace checks pass.

Initial proof:

- Retained file: `proof/2026-10-19-planning-authority/c4-closed-startup-failure.json`,
  280,790 bytes, SHA-256 `14569A97AFDD62772CBCAC3620DDF48DE666FA3FDB3E7A60D565B98313141B00`.
- 9,808 source inputs: `80F2A99CD66ED4347C5B3105525777A1351AC26B943A3900A572640EFAAB32BB`.
- 2,595 runtime inputs: `1CEFC2AA939595EAD92A6BA933C4B350C8EEB65007572ADA16B100FC90F6073F`.
- Source/runtime identity held through execution. All 13 test commands were covered by
  the resource observer. No observed processes, database-name deltas or temporary-path
  deltas remain. Retained proof has zero secret findings.

Scope-correction proof: `proof/2026-10-19-planning-authority/c4-closed-startup-failure-final.json`,
31,289 bytes, SHA-256 `D29F08E2737FD3D162E27B7EB47C1DFDDE1127A6C46CD8F40C0136A60FD9070C`.
Its 9,809 source inputs hash to `D52B0CF890479290FE2624A0D47D9507A91A009F615BC59D15DF01560668F4F9`;
2,595 runtime inputs hash to `3890E3433719AE363B4E524FF5E6E89A5AD5D86C5D30F866F9168673B0D86986`.
The 141.669-second Integration build, fresh portability enforcement and documentation
checks passed. One observed runtime command took 43.348 seconds. Source/runtime identity
held, no observed resource deltas remain and proof scanning found no secrets.

Erratum: the scope-correction proof's `productionBridge.qualification` sentence describes
the intended conditional combination as qualified before the restart test passed. Its
recorded TRX outcome is Failed, and it supplies no passing restart evidence. Preserve
that immutable record; only the following passing candidate qualifies the missing case.

Verified restart proof: `proof/2026-10-19-planning-authority/c4-closed-startup-failure-verified.json`,
30,333 bytes, SHA-256 `C6D037D9904FDAA0B7F65BCDC8B603AEE342C45D947A26CEE9A7E1DBC48D74C8`.
Its 9,809 source inputs hash to `7474FB60F7C4FD1E65233D57102571EA98AA6C2D7DB43889E251B63E6B6C762D`;
2,595 runtime inputs hash to `AF81A2BE6575D46AF115BD8A7151C07CE729C82A0CEC9528F2741C9A1A37068C`.
The Integration Release build passed in 49.143 seconds with `--no-dependencies` because
all production source and 392 production DLL copies were verified unchanged. Exactly one
case was discovered and passed in 38.395 seconds. Fresh full portability enforcement
passes all 15,657 findings; documentation and whitespace checks pass. Source/runtime
identity held, the command was observed, and no process, database-name or temporary-path
delta remains. Proof scanning found no secrets. The candidate is Released.

The [capacity checkpoint](2026-10-21-process-capacity-bounds.md) subsequently passes 15
isolated unit cases for the pure startup-outcome verifier, including identity substitutions,
invalid measurements and preserved overruns. The earlier integration negatives additionally
verify real capability issuance and database settlement boundaries.

Historical automatic-approval cleanup denials remain recorded in the bundle resource
ledger; this candidate's zero deltas do not waive them. The signed root-material
checkpoint is still waiting for local GPG interaction. SPA remains on
`codex/concurrent-mutations`, without merge.

Capacity, actual adapter activation, manager completion, cancellation/refresh, Workflow,
persisted settings/SPA, remaining writers and final broad validation remain required.
