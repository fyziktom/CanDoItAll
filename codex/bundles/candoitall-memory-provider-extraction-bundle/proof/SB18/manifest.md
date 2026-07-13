# SB18 Proof Manifest

## Status

- Subbundle: `SB18`
- Result: `Passed`
- Semantic invariant contract: `bundle://proof/SB18/semantic-invariants.md`
- Hash anchor: `bundle://proof/SB18/transcripts/passing-memory-context-contributor-tests.txt` SHA-256: `6FF9DAEAFCD3065DA6738BEEB55ACD80BFA5224D995CE19196C2FDD2697846F8`

## Validation Commands

- Failing-first transcript: `bundle://proof/SB18/transcripts/failing-first-memory-context-contributor-tests.txt`
- Passing transcript: `bundle://proof/SB18/transcripts/passing-memory-context-contributor-tests.txt`
- Anti-stub audit transcript: `bundle://proof/SB18/transcripts/source-audit-memory-context-contributor-anti-stub.txt`

## Source Assertions

- Source proof transcript: `bundle://proof/SB18/transcripts/passing-memory-context-contributor-tests.txt`
- Test proof transcript: `bundle://proof/SB18/transcripts/passing-memory-context-contributor-tests.txt`
- Adversarial negative proof transcript: `bundle://proof/SB18/transcripts/failing-first-memory-context-contributor-tests.txt`
- Anti-stub audit transcript: `bundle://proof/SB18/transcripts/source-audit-memory-context-contributor-anti-stub.txt`
- Anti-stub audit result: No stub-only closure is accepted.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| SB18 closure proof | `bundle://proof/SB18/transcripts/passing-memory-context-contributor-tests.txt` | `bundle://proof/SB18/transcripts/passing-memory-context-contributor-tests.txt` | `bundle://proof/SB18/transcripts/passing-memory-context-contributor-tests.txt` | `bundle://proof/SB18/transcripts/failing-first-memory-context-contributor-tests.txt` |

## Readiness Decision

- Decision: `Passed`.
