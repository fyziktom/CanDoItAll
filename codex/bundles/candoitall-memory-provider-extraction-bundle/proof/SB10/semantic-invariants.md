# SB10 Semantic Invariants

## Validator Invariant Contract

- Invariant ID: `SB10-CLOSURE`
- Source raw note: Memory provider extraction bundle requirements and the relevant subbundle implementation prompt.
- Expected behavior: SB10 remains closed with source proof, passing validation, negative proof, anti-stub audit, and downstream gate evidence.
- Disallowed shallow implementation: README-only closure, skipped validation, missing negative proof, weak anti-stub proof, or proof paths that cannot be reproduced.
- Failing-first test: `bundle://proof/SB10/transcripts/failing-first-runtime-checkpoint-tests.txt`
- Passing test: `bundle://proof/SB10/transcripts/passing-memory-test-suite.txt`
- Changed source files: `bundle://proof/SB10/semantic-invariants.md` and `bundle://proof/SB10/manifest.md` summarize the implemented proof boundary for this subbundle.
- Production assertions: The shipped behavior for SB10 is represented by the manifest, passing transcript, negative transcript, and anti-stub transcript.
- Red-team negative case: A reviewer should reject SB10 if `bundle://proof/SB10/transcripts/failing-first-runtime-checkpoint-tests.txt` or `bundle://proof/SB10/transcripts/source-audit-runtime-anti-stub.txt` is missing, weak, or disconnected from the passing proof.
- Downstream dependency check: Later subbundles and final release closure rely on this proof through the execution report and SB34 release gate.
