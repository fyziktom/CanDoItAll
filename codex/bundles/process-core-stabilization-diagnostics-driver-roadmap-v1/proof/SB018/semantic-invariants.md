# SB018 Semantic Invariants

## Raw Note Closure
- Raw note owned: preserve artifact/subprocess behavior while making satisfaction and mapping reasons typed and stable.
- Literal closure: legacy source selection, latest eligible artifact selection, legacy text mapping messages, and string diagnostics remain compatible.

## Shallow-Pass Trap
- A shallow pass would add enum values without proving legacy source selection and projection behavior.
- This gate requires typed diagnostic tests, full dispatch integration proof, API/boundary proof, build proof, and Core no-storage scan.

## Semantic Positive Proof
- `ProcessCoreArtifactExpectationSatisfactionRules_SB016_INV_001_reports_trust_and_sensitivity_failures` proves satisfaction diagnostics distinguish satisfied, sensitivity-too-low, and trust-not-satisfied states.
- `ProcessCoreSubprocessArtifactSourceResolver_SB009_INV_001_rejects_ambiguous_child_mapping_and_selects_latest_eligible_artifact` proves typed mapping diagnostics preserve ambiguous string diagnostics and latest eligible artifact selection.
- `ProcessRunAutomationDispatchServiceTests` passed with 537 tests.

## Adversarial Negative Proof
- Ambiguous child mapping returns no source artifact and reason `AmbiguousMapping`.
- Low-sensitivity and low-trust artifacts return explicit unsatisfied reasons.
- `bundle://proof/SB018/transcripts/core-artifact-forbidden-token-scan.txt` proves no storage, workspace, persistence entity, projection writer, or driver tokens leaked into Core.

## Anti-Stub Audit
- `bundle://proof/SB018/transcripts/anti-stub-audit.txt` found no TODO, NotImplemented, stub, or fixture-specific markers in changed artifact/subprocess diagnostics production files.

## Boundary Proof
- Module artifact records and persistence remain in module adapters/services.
- No UI, browser, mobile, or media files were changed.
