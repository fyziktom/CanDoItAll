# SB05 Semantic Invariants

## Validator Invariant Contract

- Invariant ID: `SB05-CLOSURE`
- Source raw note: Memory provider extraction bundle requirements and the relevant subbundle implementation prompt.
- Expected behavior: SB05 remains closed with source proof, passing validation, negative proof, anti-stub audit, and downstream gate evidence.
- Disallowed shallow implementation: README-only closure, skipped validation, missing negative proof, weak anti-stub proof, or proof paths that cannot be reproduced.
- Failing-first test: `bundle://proof/SB05/transcripts/failing-first-foundation-checkpoint-tests.txt`
- Passing test: `bundle://proof/SB05/transcripts/passing-foundation-checkpoint-tests.txt`
- Changed source files: `bundle://proof/SB05/semantic-invariants.md` and `bundle://proof/SB05/manifest.md` summarize the implemented proof boundary for this subbundle.
- Production assertions: The shipped behavior for SB05 is represented by the manifest, passing transcript, negative transcript, and anti-stub transcript.
- Red-team negative case: A reviewer should reject SB05 if `bundle://proof/SB05/transcripts/failing-first-foundation-checkpoint-tests.txt` or `bundle://proof/SB05/transcripts/anti-stub-audit.txt` is missing, weak, or disconnected from the passing proof.
- Downstream dependency check: Later subbundles and final release closure rely on this proof through the execution report and SB34 release gate.
