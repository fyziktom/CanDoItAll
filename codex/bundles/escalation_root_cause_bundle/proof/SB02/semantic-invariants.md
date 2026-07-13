# SB02 Semantic Invariants

## Completion Acceptance

1. A `Completed` step can produce artifacts only after every evaluated completion gate is satisfied.
2. A failed completion gate always converts the adapter result to `NeedsManager` for SB02; SB03 owns safe/idempotent retry routing.
3. Aggregation must never make success easier than the previous first-failure path.

## Diagnostic Preservation

1. Every failed gate contributes its original diagnostic code.
2. Every failed gate preserves retry safety and idempotency classification.
3. Every failed gate contributes a manager signal with the same code as its diagnostic.
4. Requested artifact slots are the union of all failed gate requested slots, falling back to produced slots and then required slots only when no gate supplies explicit requested slots.

## Deterministic Primary Issue

1. Primary issue ordering is explicit, not dependent on dictionary iteration or message text.
2. Unsafe-to-retry findings outrank safe retry findings.
3. Missing required product tool receipts outrank downstream product readback failures.
4. For the incident shape, `process.adapter.product_required_tool_receipt_missing` is emitted before `process.adapter.product_required_file_content_missing`.

## Gate Coverage

The aggregate evaluator runs the existing gates for:

- Grounded outcome references.
- Product mutation completion.
- Product mutation write receipts.
- Required product tool receipts.
- Required process tool receipts.
- Required product state.
- Completed outcome text that still declares blockers.
- Produced managed artifact evidence.
- Produced managed artifact write receipt.

Duplicate findings from overlapping product state validators are de-duplicated by diagnostic code plus evidence identity.

## Anti-Stub / No-Bypass Assertions

1. No completion gate was removed to make the incident pass.
2. Existing single-gate tests still fail the completed output when only the file-content readback gate fails.
3. Existing single-gate tests still fail the completed output when only the required product tool receipt gate fails.
4. The new aggregate test fails unless both the missing script receipt and `.slnx` readback diagnostics are present.
5. The aggregate result does not collapse diagnostics into a generic `completion_gates_unsatisfied` code.

## Architecture Boundary

1. Aggregation stays inside `CanDoItAll.Modules.Processes` runtime integration.
2. No new dependency direction was introduced.
3. CodeAnalytics snapshot `snap-20260708182008-79c92788` reports no scoped dependency cycles.
4. Public contracts remain unchanged; downstream subbundles consume aggregate diagnostics through existing `ProcessExecutionAdapterResult`.


## Completed Validator Contract

- Invariant ID: SB02-FINAL-001
- Source raw note: GPTPro Extended escalation root-cause analysis and the user's broader process/template/artifact repair requirement.
- Expected behavior: The completed subbundle behavior remains implemented, tested, and represented by typed proof artifacts.
- Disallowed shallow implementation: Do not close the phase with prose-only proof, build-only proof, or hidden prompt-only gates.
- Failing-first test: N/A process/non-production final proof uses adversarial negative tests or preserved subbundle evidence in proof/SB02/transcripts/00-validator-metadata.txt.
- Passing test: Completed proof metadata is recorded in proof/SB02/transcripts/00-validator-metadata.txt and the subbundle manifest.
- Changed source files: bundle://subbundles/02-sb02-completion-gate-aggregator/README.md and bundle://proof/SB02/manifest.md.
- Production assertions: Runtime/template/process behavior remains covered by the subbundle proof manifest and final bundle validation.
- Red-team negative case: Shallow final closure without proof metadata, semantic invariant labels, and transcripts is rejected by the completed validator.
- Downstream dependency check: Final bundle validation and recorded CodeAnalytics snapshots verify no unresolved downstream gate remains.


## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative |
| --- | --- | --- | --- | --- |
| SB02 semantic proof metadata | proof/SB02/semantic-invariants.md | proof/SB02/transcripts/00-validator-metadata.txt | final proof closure | proof/SB02/manifest.md rejects missing semantic proof |
