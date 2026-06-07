# SB009 Semantic Invariants

## Raw Note Closure
- Raw note owned: preserve existing process behavior while advancing a stable Process Core diagnostics surface.
- Literal closure: diagnostics are additive; existing route decisions, artifact matched ids, and module adapter return values remain unchanged.

## Shallow-Pass Trap
- A shallow pass would add reason enums that are never exercised, or update the public API snapshot without proving parity.
- This gate requires both public API guard proof and dispatch integration proof.

## Semantic Positive Proof
- `ProcessDispatchRoutePlanner_SB007_INV_001_exposes_additive_diagnostics_without_changing_decisions` asserts every diagnostic decision equals the corresponding legacy resolver decision.
- `ProcessArtifactExpectationMatcher_SB008_INV_001_exposes_match_diagnostics_without_changing_legacy_match` asserts the legacy matched artifact id is preserved while diagnostic reasons are exposed.
- `ProcessRunAutomationDispatchServiceTests` passed as a focused dispatch integration class with 535 tests.

## Adversarial Negative Proof
- Subprocess database requirement diagnostics report `DatabaseRequirementIgnoredForSubprocess` while preserving the legacy `Continue` decision.
- Artifact diagnostics cover no strong match, ambiguous kind match, ambiguous strong match, and a single mismatched-kind strong match that still preserves the legacy single-strong-match id.
- Unapproved Core API drift fails `Process_core_public_api_surface_is_explicitly_guarded`.

## Anti-Stub Audit
- `bundle://proof/SB009/transcripts/anti-stub-audit.txt` found no TODO, NotImplemented, stub, or fixture-specific markers in changed diagnostics production files.

## Boundary Proof
- `bundle://proof/SB009/transcripts/core-forbidden-token-scan.txt` found no forbidden module, infrastructure, runtime side-effect, or driver tokens in Process Core.
- No UI, browser, mobile, or media files were changed.
