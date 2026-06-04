# SB02 Semantic Invariants

1. Recorded upstream artifact inputs must match a source step run and a source process run before they can be offered to a downstream step.
2. A recorded artifact with a stale `ProcessRunId` must not satisfy a current-run artifact expectation, even when the title, kind, or path shape matches.
3. A current-run artifact that points at product roots such as `src/` or `tests/` must be rejected as a managed artifact proof source.
4. `artifacts/process-runs/{other-guid}` and `output/process-runs/{other-guid}` roots are stale or unrelated unless the run token is the current process run id.
5. Scoped current-run output roots such as `output/scopes/.../process-runs/{current-run}` and current process mock output roots remain valid.
6. Projection lineage and external reference lineage must bind artifacts to the current execution, workflow, subprocess, or recovery run for producer kinds that require that binding.
7. Completion finalization and completion artifact validation must use the same managed-boundary classifier for stale-root rejection.
8. SB02 must not introduce Tetris-specific production logic, permissive warnings for stale-lineage failure, or stubbed validator behavior.

## Shallow-Pass Trap

A class-existence test is insufficient. SB02 requires:

- Adversarial negative proof: stale process-run artifacts and product-root artifacts are rejected before they can satisfy required upstream inputs.
- Semantic positive proof: current-run managed artifacts still satisfy upstream input resolution, and existing completion artifact characterization tests continue to pass.

## Dependency Smoke

The integration regression slice exercises the process dispatch dependency graph through `CanDoItAll.Tests.Integration`, including artifact input selection, completion artifact validation, and wrong-root finalizer behavior. The drift scanner verifies that SB02 did not add new unowned process/workflow/tool identifiers.
