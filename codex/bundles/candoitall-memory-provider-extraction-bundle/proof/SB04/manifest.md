# SB04 Proof Manifest

## Status

- Subbundle: `SB04`
- Result: `Passed`
- Semantic invariant contract: `bundle://proof/SB04/semantic-invariants.md`
- Hash anchor: `bundle://proof/SB04/transcripts/passing-memory-test-suite.txt` SHA-256: `4DF790C48BA9C6C1487AF42CA60BA615600A5A8E21CE3350AB1ECF05FAB0CFA9`

## Validation Commands

- Failing-first transcript: `bundle://proof/SB04/transcripts/failing-first-source-gateway-tests.txt`
- Passing transcript: `bundle://proof/SB04/transcripts/passing-memory-test-suite.txt`
- Anti-stub audit transcript: `bundle://proof/SB04/transcripts/anti-stub-audit.txt`

## Source Assertions

- Source proof transcript: `bundle://proof/SB04/transcripts/passing-memory-test-suite.txt`
- Test proof transcript: `bundle://proof/SB04/transcripts/passing-memory-test-suite.txt`
- Adversarial negative proof transcript: `bundle://proof/SB04/transcripts/failing-first-source-gateway-tests.txt`
- Anti-stub audit transcript: `bundle://proof/SB04/transcripts/anti-stub-audit.txt`
- Anti-stub audit result: No stub-only closure is accepted.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| SB04 closure proof | `bundle://proof/SB04/transcripts/passing-memory-test-suite.txt` | `bundle://proof/SB04/transcripts/passing-memory-test-suite.txt` | `bundle://proof/SB04/transcripts/passing-memory-test-suite.txt` | `bundle://proof/SB04/transcripts/failing-first-source-gateway-tests.txt` |

## Readiness Decision

- Decision: `Passed`.
