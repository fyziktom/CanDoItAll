# SB33 Semantic Invariants

## Validator Invariant Contract

- Invariant ID: `SB33-CLOSURE`
- Source raw note: Memory provider extraction bundle requirements and the relevant subbundle implementation prompt.
- Expected behavior: SB33 remains closed with source proof, passing validation, negative proof, anti-stub audit, and downstream gate evidence.
- Disallowed shallow implementation: README-only closure, skipped validation, missing negative proof, weak anti-stub proof, or proof paths that cannot be reproduced.
- Failing-first test: `bundle://proof/SB33/transcripts/failing-first-memory-playwright-tests.txt`
- Passing test: `bundle://proof/SB33/transcripts/passing-database-runtime-switching-integration-tests.txt`
- Changed source files: `bundle://proof/SB33/semantic-invariants.md` and `bundle://proof/SB33/manifest.md` summarize the implemented proof boundary for this subbundle.
- Production assertions: The shipped behavior for SB33 is represented by the manifest, passing transcript, negative transcript, and anti-stub transcript.
- Red-team negative case: A reviewer should reject SB33 if `bundle://proof/SB33/transcripts/failing-first-memory-playwright-tests.txt` or `bundle://proof/SB33/transcripts/audit-sb33-stub-xml-markers.txt` is missing, weak, or disconnected from the passing proof.
- Downstream dependency check: Later subbundles and final release closure rely on this proof through the execution report and SB34 release gate.
