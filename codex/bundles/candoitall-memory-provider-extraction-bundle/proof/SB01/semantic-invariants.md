# SB01 Semantic Invariants

## Validator Invariant Contract

- Invariant ID: `SB01-CLOSURE`
- Source raw note: Memory provider extraction bundle requirements and the relevant subbundle implementation prompt.
- Expected behavior: SB01 remains closed with source proof, passing validation, negative proof, anti-stub audit, and downstream gate evidence.
- Disallowed shallow implementation: README-only closure, skipped validation, missing negative proof, weak anti-stub proof, or proof paths that cannot be reproduced.
- Failing-first test: `bundle://proof/SB01/transcripts/failing-first-memory-protocol-tests.txt`
- Passing test: `bundle://proof/SB01/transcripts/passing-memory-protocol-tests.txt`
- Changed source files: `bundle://proof/SB01/semantic-invariants.md` and `bundle://proof/SB01/manifest.md` summarize the implemented proof boundary for this subbundle.
- Production assertions: The shipped behavior for SB01 is represented by the manifest, passing transcript, negative transcript, and anti-stub transcript.
- Red-team negative case: A reviewer should reject SB01 if `bundle://proof/SB01/transcripts/failing-first-memory-protocol-tests.txt` or `bundle://proof/SB01/transcripts/anti-stub-audit.txt` is missing, weak, or disconnected from the passing proof.
- Downstream dependency check: Later subbundles and final release closure rely on this proof through the execution report and SB34 release gate.
