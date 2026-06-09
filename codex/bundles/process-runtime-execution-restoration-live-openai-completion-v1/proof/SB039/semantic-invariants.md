# SB039 Semantic Invariants

## Status
Completed.

## Invariant SB037_INV_001
- Invariant ID: `SB037_INV_001`
- Source raw note: API launch endpoints must remain compatible while process execution is restored.
- Expected behavior: direct service run start, launch-plan execution, project-structure launch-plan route, and project-structure execute route all map to typed process runtime service calls and preserve route identity.
- Disallowed shallow implementation: route strings without durable run/launch-plan state, docs-only endpoint matrix, or driver runtime hook execution.
- Passing tests: `ProjectStructureAgentApi_start_process_node_SB011_INV_001_creates_project_scoped_launch_plan_with_bridge_context`, `ProjectStructureAgentApi_execute_process_node_SB012_INV_001_preserves_run_context_and_projects_output_folder`, `StartRunAsync_SB018_INV_001_persists_project_context_runtime_rows_and_dispatch_outbox`, and `StartRunAsync_SB018_INV_002_rejects_invalid_not_ready_and_duplicate_start_attempts`.

## Invariant SB038_INV_001
- Invariant ID: `SB038_INV_001`
- Source raw note: project/global launch plan migration must not lose project-structure context or duplicate launch plans.
- Expected behavior: process-workspace/global launch can infer unambiguous project-structure context, retried project-structure launch requests reuse the open launch plan for the same context, and generated runtime failure is reflected in launch-plan readback.
- Disallowed shallow implementation: duplicate launch plans for the same project-structure context, hidden runtime failure behind stale launch state, or ambiguous project inference.
- Passing tests: `CreateLaunchPlanAsync_infers_project_structure_context_from_single_process_link`, `CreateLaunchPlanAsync_reuses_open_project_structure_launch_plan_for_same_context`, and `Launch_plan_reads_project_generated_run_failure_as_effective_status`.

## Invariant SB039_INV_001
- Invariant ID: `SB039_INV_001`
- Source raw note: Gate M must prove launch API compatibility without runtime driver hosts.
- Expected behavior: the focused launch compatibility integration slice passes, active bundle-path scan is clean, and forbidden runtime driver host scan is clean.
- Disallowed shallow implementation: runtime driver registry/selector/host, execution-capable driver package, or report-only compatibility table.
- Failing-first/negative proof: `bundle://proof/SB039/red-team/shallow-launch-api-proof-rejected.md`
- Passing test: `bundle://proof/SB039/transcripts/launch-api-compatibility-tests.txt`
- Source assertions: `bundle://proof/SB039/transcripts/source-assertions.txt`

## Shallow-Pass Trap
A fake Gate M closure could prove launch planning screens render while avoiding actual runtime start. SB039 rejects that by requiring direct service run start, launch-plan execution, project-structure API start/execution, project/global context migration, generated runtime status projection, and clean forbidden-surface scans.

## Semantic Positive Proof
- `bundle://proof/SB037/api-launch-endpoints-compatibility-matrix.md`
- `bundle://proof/SB038/project-global-launch-plan-migration-guards-proof.md`
- `bundle://proof/SB039/transcripts/launch-api-compatibility-tests.txt`
- `bundle://proof/SB039/transcripts/source-assertions.txt`

## Adversarial Negative Proof
- `bundle://proof/SB039/red-team/shallow-launch-api-proof-rejected.md`

## Anti-Stub Audit
- `bundle://proof/SB039/transcripts/no-transient-bundle-path-scan.txt`
- `bundle://proof/SB039/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- No active bundle paths or forbidden runtime driver host surfaces were found in scoped source/tests.
