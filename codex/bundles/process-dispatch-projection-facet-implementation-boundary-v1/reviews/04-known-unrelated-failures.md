# Known Unrelated Failures

## Result

The final gating commands for this bundle pass. A broader non-gating run of `ProcessAgentExecutionBoundaryArchitectureTests` still has five unrelated failures that predate this projection-facet boundary work or depend on stale bundle artifacts outside this bundle.

## Non-gating Transcript

- bundle://proof/shared/transcripts/architecture-class-known-unrelated-failures.txt

## Failures Observed

- Missing `process-agent-execution-boundary-foundation-v1/architecture/02-execution-boundary-staging.md`.
- Missing `process-dispatch-step-completion-finalizer-boundary-v1/inventories/02-finalizer-method-classification-template.md`.
- Existing `Process_dispatch_claim_route_gate_b_SB08_INV_001_records_concurrency_helper_parity_and_blocks_side_effect_drift` assertion expecting `executionClient.GetExecutionRunDetailAsync`.
- Missing `process-dispatch-artifact-validation-rule-boundary-v1/inventories/02-artifact-validation-method-inventory-seed.md`.
- Missing `process-agent-execution-boundary-foundation-v1/inventories/02-agentframework-usage-in-processes.md`.

## Decision

These are not regressions from the projection facet implementation boundary. The passing focused projection tests, focused integration tests, full build, and source scans are the bundle gates.
