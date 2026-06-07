# SB031 Proof Manifest

## Scope
- Subbundle: `SB031 - Broad build/unit/integration smoke`
- Objective: run broad smoke proof after all Core, diagnostics, adapter, driver-doc, and test changes.

## Command Transcripts
- Build: `bundle://proof/SB031/transcripts/build.txt`
- Full unit tests: `bundle://proof/SB031/transcripts/full-unit-tests.txt`
- Architecture mega-class: `bundle://proof/SB031/transcripts/architecture-megaclass-tests.txt`
- Current stabilization architecture tests: `bundle://proof/SB031/transcripts/current-stabilization-architecture-tests.txt`
- Historical architecture compatibility tests: `bundle://proof/SB031/transcripts/historical-architecture-core-allowlist-compatibility-tests.txt`
- Focused process-dispatch integration tests: `bundle://proof/SB031/transcripts/process-dispatch-integration-tests.txt`
- Source assertions: `bundle://proof/SB031/transcripts/source-assertions.txt`
- Changed-file hashes: `bundle://proof/SB031/transcripts/changed-file-hashes.txt`

## Results
- Solution build passed with three unrelated pre-existing warnings.
- Full unit project passed: 1039 tests.
- Architecture mega-class passed: 92 tests.
- Focused process-dispatch integration passed: 539 tests.
- `RunGit` test helper was fixed to drain stdout/stderr asynchronously, removing a dirty-worktree stderr deadlock in architecture tests.

## Downstream Gate
- SB032/SB033 may close only while the broad smoke transcripts remain green.
