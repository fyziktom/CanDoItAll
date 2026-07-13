# SB03 Semantic Invariants

## Validator Invariant Contract

- Invariant ID: `SB03-CLOSURE`
- Source raw note: Memory provider extraction bundle requirements and the relevant subbundle implementation prompt.
- Expected behavior: SB03 remains closed with source proof, passing validation, negative proof, anti-stub audit, and downstream gate evidence.
- Disallowed shallow implementation: README-only closure, skipped validation, missing negative proof, weak anti-stub proof, or proof paths that cannot be reproduced.
- Failing-first test: `bundle://proof/SB03/transcripts/failing-first-ledger-lifecycle-tests.txt`
- Passing test: `bundle://proof/SB03/transcripts/passing-ledger-lifecycle-tests.txt`
- Changed source files: `bundle://proof/SB03/semantic-invariants.md` and `bundle://proof/SB03/manifest.md` summarize the implemented proof boundary for this subbundle.
- Production assertions: The shipped behavior for SB03 is represented by the manifest, passing transcript, negative transcript, and anti-stub transcript.
- Red-team negative case: A reviewer should reject SB03 if `bundle://proof/SB03/transcripts/failing-first-ledger-lifecycle-tests.txt` or `bundle://proof/SB03/transcripts/anti-stub-audit.txt` is missing, weak, or disconnected from the passing proof.
- Downstream dependency check: Later subbundles and final release closure rely on this proof through the execution report and SB34 release gate.
