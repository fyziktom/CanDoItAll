# SB03 Semantic Invariants

- Invariant ID: `SB03-INVARIANT-001`
- Source raw note: `RQ-004` MAF provider composition must use provider-neutral names and tests before product provider migration starts.
- Expected behavior: Runtime-provider composition policy is isolated in a provider-specific partial, uses provider-neutral helper names, preserves approval wrapping, duplicate-tool failure, provider-key failure, failure diagnostics, and no-provider behavior.
- Disallowed shallow implementation: Renaming one method while leaving process-specific composition helpers or weakening duplicate/approval/failure behavior.
- Failing-first test: `bundle://proof/SB03/transcripts/failing-first-old-process-specific-helper-presence.txt` rejects the old process-specific helper-name surface.
- Passing test: `bundle://proof/SB03/transcripts/provider-neutral-name-scan.txt`, `bundle://proof/SB03/transcripts/runtime-tool-provider-composition-tests.txt`, and `bundle://proof/SB03/transcripts/solution-build.txt`.
- Changed source files: `bundle://proof/SB03/source-assertions/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB03/source-assertions/provider-composition-source-assertions.txt`.
- Red-team negative case: The old-name presence check plus duplicate-provider and duplicate-tool tests would fail a shallow rename or policy weakening.
- Downstream dependency check: SB04 may start only after Gate A confirms metadata exists, provider composition is generic, and process tool parity is still covered by `bundle://proof/SB03/transcripts/runtime-tool-provider-composition-tests.txt`.
