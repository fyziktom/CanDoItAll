# SB01 Proof Manifest

## Status

- Subbundle: `SB01`
- Result: `Passed`
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`
- Hash anchor: `bundle://proof/SB01/transcripts/passing-memory-protocol-tests.txt` SHA-256: `E56FDCCB97AEE4829A4B97268BAAA9F9EA8334BB9D3170327E4BABC057D4153F`

## Validation Commands

- Failing-first transcript: `bundle://proof/SB01/transcripts/failing-first-memory-protocol-tests.txt`
- Passing transcript: `bundle://proof/SB01/transcripts/passing-memory-protocol-tests.txt`
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`

## Source Assertions

- Source proof transcript: `bundle://proof/SB01/transcripts/passing-memory-protocol-tests.txt`
- Test proof transcript: `bundle://proof/SB01/transcripts/passing-memory-protocol-tests.txt`
- Adversarial negative proof transcript: `bundle://proof/SB01/transcripts/failing-first-memory-protocol-tests.txt`
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`
- Anti-stub audit result: No stub-only closure is accepted.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| SB01 closure proof | `bundle://proof/SB01/transcripts/passing-memory-protocol-tests.txt` | `bundle://proof/SB01/transcripts/passing-memory-protocol-tests.txt` | `bundle://proof/SB01/transcripts/passing-memory-protocol-tests.txt` | `bundle://proof/SB01/transcripts/failing-first-memory-protocol-tests.txt` |

## Readiness Decision

- Decision: `Passed`.
