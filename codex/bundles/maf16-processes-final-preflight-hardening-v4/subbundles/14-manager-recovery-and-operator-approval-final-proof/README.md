# SB14: 14-manager-recovery-and-operator-approval-final-proof

## Goal

Close recovery and operator approval edge cases.

## Required work

- Operator decision artifact must not satisfy required brief/deliverable unless explicit decision expectation.
- Manager recovery artifact must include content, current-run lineage, and original expectation id.
- Pending approval must show clear next action and not mask failed state.
- Add tests covering invalid and valid recovery.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package / MAF adapter / process runtime / API / UI / template.
- Explicit note whether this subbundle is behavior-changing or proof-only.

## Closure criteria

Do not close this subbundle until proof files under `proof/SB14` are filled and the downstream dependency is safe.
