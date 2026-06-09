# SB045 Semantic Invariants

## Status
Completed.

## Invariant SB043_INV_001
- Invariant ID: `SB043_INV_001`
- Source raw note: after E2E restoration, decide whether a runtime host is feasible.
- Expected behavior: the decision remains `Not approved` for a process-driver runtime host because current E2E proof uses process-owned runtime paths and future approval requires explicit source-backed gates.
- Disallowed shallow implementation: using successful process runtime E2E proof to approve a generic driver host, registry, selector, manager command, scheduler hook, or workflow hook.
- Passing tests: `Process_driver_runtime_host_roadmap_remains_not_approved_until_future_gate_is_source_backed` and `Scheduler_and_workflow_trigger_start_paths_use_process_service_without_driver_runtime_hooks`.

## Invariant SB044_INV_001
- Invariant ID: `SB044_INV_001`
- Source raw note: runtime host denial/regression tests must still block runtime driver host surfaces.
- Expected behavior: unit contract/architecture tests and integration read-only adapter tests pass; production scans contain no process driver host, registry, selector, manager command, or route registration surface.
- Disallowed shallow implementation: docs-only denial without source scans/tests or allowing runtime-host terms in production source.
- Passing proof: `bundle://proof/SB045/transcripts/runtime-host-denial-unit-tests.txt`, `bundle://proof/SB045/transcripts/runtime-host-denial-integration-tests.txt`, and `bundle://proof/SB045/transcripts/production-driver-runtime-host-scan.txt`.

## Invariant SB045_INV_001
- Invariant ID: `SB045_INV_001`
- Source raw note: Gate O must prove runtime host is still blocked or explicitly future-gated.
- Expected behavior: runtime driver host remains blocked/future-gated; lane-gated process hosted workers remain allowed; active bundle-path scan and runtime-host drift scans are clean.
- Disallowed shallow implementation: conflating normal process hosted workers or browser runtime-host metadata with process driver runtime host approval.
- Failing-first/negative proof: `bundle://proof/SB045/red-team/runtime-host-approval-proof-rejected.md`
- Passing tests: `bundle://proof/SB045/transcripts/runtime-host-denial-unit-tests.txt`, `bundle://proof/SB045/transcripts/runtime-host-denial-integration-tests.txt`, and `bundle://proof/SB045/transcripts/hosted-worker-policy-tests.txt`.

## Shallow-Pass Trap
A fake Gate O closure could approve runtime driver hosting because the application now runs E2E. SB045 rejects that by requiring explicit future-gate language, direct denial tests, no-host integration tests, hosted-worker policy distinction, and clean production driver-host scans.

## Semantic Positive Proof
- `bundle://proof/SB043/runtime-host-feasibility-decision.md`
- `bundle://proof/SB044/runtime-host-denial-regression-proof.md`
- `bundle://proof/SB045/transcripts/source-assertions.txt`

## Adversarial Negative Proof
- `bundle://proof/SB045/red-team/runtime-host-approval-proof-rejected.md`

## Anti-Stub Audit
- `bundle://proof/SB045/transcripts/no-transient-bundle-path-scan.txt`
- `bundle://proof/SB045/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- `bundle://proof/SB045/transcripts/production-driver-runtime-host-scan.txt`
- No active bundle paths or forbidden production process driver runtime host surfaces were found.
