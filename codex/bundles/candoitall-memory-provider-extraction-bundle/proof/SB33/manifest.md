# SB33 Proof Manifest

## Status

- Subbundle: `SB33`
- Result: `Passed`
- Semantic invariant contract: `bundle://proof/SB33/semantic-invariants.md`
- Hash anchor: `bundle://proof/SB33/transcripts/passing-database-runtime-switching-integration-tests.txt` SHA-256: `42B5723B7DDA27773CDCEE7A29A32214BFFF1633D3D1593883C55C3FBF8C8E62`

## Validation Commands

- Failing-first transcript: `bundle://proof/SB33/transcripts/failing-first-memory-playwright-tests.txt`
- Passing transcript: `bundle://proof/SB33/transcripts/passing-database-runtime-switching-integration-tests.txt`
- Anti-stub audit transcript: `bundle://proof/SB33/transcripts/audit-sb33-stub-xml-markers.txt`

## Source Assertions

- Source proof transcript: `bundle://proof/SB33/transcripts/passing-database-runtime-switching-integration-tests.txt`
- Test proof transcript: `bundle://proof/SB33/transcripts/passing-database-runtime-switching-integration-tests.txt`
- Adversarial negative proof transcript: `bundle://proof/SB33/transcripts/failing-first-memory-playwright-tests.txt`
- Anti-stub audit transcript: `bundle://proof/SB33/transcripts/audit-sb33-stub-xml-markers.txt`
- Anti-stub audit result: No stub-only closure is accepted.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| SB33 closure proof | `bundle://proof/SB33/transcripts/passing-database-runtime-switching-integration-tests.txt` | `bundle://proof/SB33/transcripts/passing-database-runtime-switching-integration-tests.txt` | `bundle://proof/SB33/transcripts/passing-database-runtime-switching-integration-tests.txt` | `bundle://proof/SB33/transcripts/failing-first-memory-playwright-tests.txt` |

## Readiness Decision

- Decision: `Passed`.
