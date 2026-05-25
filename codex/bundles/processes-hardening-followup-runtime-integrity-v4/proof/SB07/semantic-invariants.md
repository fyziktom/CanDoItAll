# SB07 Semantic Invariants

## SB07-INV-001

Expected behavior: artifact validation failures are classified by ownership. Own required output failures, including missing or invalid current-step artifacts, block or recover instead of completing onto negative branch outcomes. Review/approval/QA disposition routing remains allowed only when a required decision artifact is already satisfied and the remaining failure is explicitly classified as `ReviewDisposition`.

Disallowed shallow implementation:

- prompt-only change
- source-assertion-only proof
- tests that manually seed final state instead of exercising producer/consumer lifecycle
- branch-specific hardcoding
- software-only behavior for generic process runtime

Required proof:

- failing-first/red-team proof
- passing proof
- source assertions
- anti-stub audit
- changed-file hashes
- production behavior artifact matrix when new runtime state is introduced

Closure proof:

- `ArtifactDispositionRouter_SB07_INV_001_blocks_missing_own_required_artifact_even_with_negative_branch`
- `ArtifactContractValidation_SB07_INV_001_classifies_missing_required_artifact_as_own_output`
