# SB03 Proof Manifest

## Status

- Subbundle: `SB03`
- Result: `Passed`
- Semantic invariant contract: `bundle://proof/SB03/semantic-invariants.md`
- Hash anchor: `bundle://proof/SB03/transcripts/passing-ledger-lifecycle-tests.txt` SHA-256: `2165573E1E2A272080C892316E532EE1043ADEBB5F484EFCE2F3D9644A1C2106`

## Validation Commands

- Failing-first transcript: `bundle://proof/SB03/transcripts/failing-first-ledger-lifecycle-tests.txt`
- Passing transcript: `bundle://proof/SB03/transcripts/passing-ledger-lifecycle-tests.txt`
- Anti-stub audit transcript: `bundle://proof/SB03/transcripts/anti-stub-audit.txt`

## Source Assertions

- Source proof transcript: `bundle://proof/SB03/transcripts/passing-ledger-lifecycle-tests.txt`
- Test proof transcript: `bundle://proof/SB03/transcripts/passing-ledger-lifecycle-tests.txt`
- Adversarial negative proof transcript: `bundle://proof/SB03/transcripts/failing-first-ledger-lifecycle-tests.txt`
- Anti-stub audit transcript: `bundle://proof/SB03/transcripts/anti-stub-audit.txt`
- Anti-stub audit result: No stub-only closure is accepted.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| SB03 closure proof | `bundle://proof/SB03/transcripts/passing-ledger-lifecycle-tests.txt` | `bundle://proof/SB03/transcripts/passing-ledger-lifecycle-tests.txt` | `bundle://proof/SB03/transcripts/passing-ledger-lifecycle-tests.txt` | `bundle://proof/SB03/transcripts/failing-first-ledger-lifecycle-tests.txt` |

## Readiness Decision

- Decision: `Passed`.
