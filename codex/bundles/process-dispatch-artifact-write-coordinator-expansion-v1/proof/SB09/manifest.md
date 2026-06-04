# SB09 Critical Manifest

Subbundle: SB09 - Migrate response-text artifact write path through coordinator
Status: Completed
Owned requirements: RQ-001, RQ-008, RQ-012, RQ-013
Criticality: Critical. Response-text artifacts write managed files before storage placement and include an existing-managed short-circuit path.

## Critical Invariants

- Response file creation, newline normalization, UTF-8 content bytes, and path traversal guard remain dispatcher-owned: `bundle://proof/SB09/semantic-invariants.md`.
- Response-text storage placement and artifact recording use `ProcessArtifactProjectionWriteCoordinator`: `bundle://proof/SB09/source-assertions/response-text-source-scan.txt`.
- Existing-managed response-target short-circuit remains soft and returns `false` on coordinator/recording failure: `bundle://proof/SB09/semantic-invariants.md`.
- Coordinator source scan shows no response/file/path/source matching semantics moved into the coordinator: `bundle://proof/SB09/source-assertions/response-text-source-scan.txt`.

## Proof

| Evidence | Path |
| --- | --- |
| Failing-first source guard | `bundle://proof/SB09/transcripts/failing-first-response-text-source-guard.txt` |
| Passing tests and full build | `bundle://proof/SB09/transcripts/response-text-tests.txt` |
| Source assertions | `bundle://proof/SB09/source-assertions/response-text-source-scan.txt` |
| Semantic invariants | `bundle://proof/SB09/semantic-invariants.md` |
| Anti-stub audit | `bundle://proof/SB09/source-assertions/anti-stub-audit.txt` |
| Changed-file hashes | `bundle://proof/SB09/source-assertions/changed-file-hashes.txt` |

## Browser And Host Proof

- Browser proof: N/A. SB09 is a service/runtime projection refactor.
- Host proof: N/A. Existing file writes remain inside the dispatcher and no shell launch, file-open, elevation, or desktop integration behavior changed.

## Completed Validator Proof Labels

- Semantic invariant contract: SB09 semantic contract at bundle://proof/SB09/semantic-invariants.md
- Failing-first transcript: bundle://proof/SB09/transcripts/failing-first-response-text-source-guard.txt
- Passing transcript: bundle://proof/SB09/transcripts/response-text-tests.txt
- Anti-stub audit transcript: bundle://proof/SB09/transcripts/anti-stub-audit.txt
- Representative SHA-256: C2F86E97CE8EC9B646C63B9E5D3CAFDA557BFF226D1594DBC80E02DB9A8CE4A9
