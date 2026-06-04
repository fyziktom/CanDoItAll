# Assumptions And Risks

## Assumptions

- Artifact validation rules are mostly pure functions, but some call sites combine them with file reads, execution snapshots, candidate state, and artifact records.
- The current dispatcher nested types are not yet ready to move into Process Core.
- A process driver architecture will eventually need typed evidence/validation concepts, but driver-pack implementation is too early.

## Critical Path Risks

- Extracting matcher logic without preserving exact fallback order could change which artifact expectation is satisfied.
- Moving text/content heuristics too broadly could accept placeholder content or reject legitimate deliverables.
- Provider-native browser evidence has separate visual-scoring semantics; collapsing it into generic matching would regress browser proof.
- Project-structure requirement preservation has delicate “downgrade/defer/drop” phrase handling.

## Validation Risks

- Compile-only checks are insufficient; focused artifact tests must prove exact match outcomes and negative cases.
- Line-count reduction alone is not enough; helper usage and behavior parity matter more.
- Broad Process Core movement would increase risk because many validation helpers still reference module-local enums and dispatcher context.

## Reopen Triggers

- Any changed external-reference key format.
- Any change in required artifact satisfaction status for existing tests.
- Any new Process Core or driver-pack project.
- Any mobile/small/medium proof artifact.
- Any helper that only wraps one line without reducing dependency or duplication.
