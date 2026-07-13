# SB19 Semantic Invariants

## Validator Invariant Contract

- Invariant ID: `SB19-CLOSURE`
- Source raw note: Memory provider extraction bundle requirements and the relevant subbundle implementation prompt.
- Expected behavior: SB19 remains closed with source proof, passing validation, negative proof, anti-stub audit, and downstream gate evidence.
- Disallowed shallow implementation: README-only closure, skipped validation, missing negative proof, weak anti-stub proof, or proof paths that cannot be reproduced.
- Failing-first test: `bundle://proof/SB19/transcripts/failing-first-maf-integration-checkpoint-tests.txt`
- Passing test: `bundle://proof/SB19/transcripts/passing-maf-integration-checkpoint-tests.txt`
- Changed source files: `bundle://proof/SB19/semantic-invariants.md` and `bundle://proof/SB19/manifest.md` summarize the implemented proof boundary for this subbundle.
- Production assertions: The shipped behavior for SB19 is represented by the manifest, passing transcript, negative transcript, and anti-stub transcript.
- Red-team negative case: A reviewer should reject SB19 if `bundle://proof/SB19/transcripts/failing-first-maf-integration-checkpoint-tests.txt` or `bundle://proof/SB19/transcripts/source-audit-maf-memory-anti-stub.txt` is missing, weak, or disconnected from the passing proof.
- Downstream dependency check: Later subbundles and final release closure rely on this proof through the execution report and SB34 release gate.
