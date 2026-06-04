# SB05 Semantic Invariants

## Invariant `SB05-INV-001`

- Invariant ID: SB05-INV-001

- Source raw note: MAF and Processes must be decoupled without simplifying or omitting process tool behavior.
- Expected behavior: MAF has no direct `CanDoItAll.Modules.Processes` project reference, no Processes namespace usage, and no legacy process tool builder implementation; registered providers remain the only process tool attachment path.
- Disallowed shallow implementation: Delete the old file/reference but lose process tools, leave a hidden MAF Processes source reference, or remove provider composition.
- Failing-first test and transcript: `bundle://proof/SB05/transcripts/static-architecture-test.txt` fails if forbidden direct dependency strings return; `bundle://proof/SB05/transcripts/process-provider-parity-after-reference-removal.txt` fails if process tools no longer attach through the provider path.
- Passing test and transcript: `bundle://proof/SB05/transcripts/static-architecture-test.txt`, `bundle://proof/SB05/transcripts/maf-project-build.txt`, `bundle://proof/SB05/transcripts/process-provider-parity-after-reference-removal.txt`, and `bundle://proof/SB05/transcripts/solution-build.txt` all pass.
- Changed source files and hashes: `bundle://proof/SB05/source-assertions/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB05/source-assertions/maf-project-reference-audit.txt`, `maf-forbidden-source-audit.txt`, `legacy-process-tool-file-deleted.txt`, and `composition-cleanup-source-audit.txt`.
- Red-team negative case: A reintroduced direct reference, namespace import, or legacy builder string is caught by static guard/source scans; missing provider tools are caught by the parity integration test.
- Downstream dependency check: SB06 can start because the direct MAF -> Processes reference is gone and guarded while provider parity still passes.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative test |
| --- | --- | --- | --- | --- |
| N/A | SB05 removes a compile-time dependency and legacy source path; it introduces no persisted production signal, state, record, or event. | N/A | N/A | N/A |
