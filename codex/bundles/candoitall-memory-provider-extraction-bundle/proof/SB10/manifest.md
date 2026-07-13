# SB10 Proof Manifest

## Status

- Subbundle: `SB10`
- Result: `Passed`
- Semantic invariant contract: `bundle://proof/SB10/semantic-invariants.md`
- Hash anchor: `bundle://proof/SB10/transcripts/passing-memory-test-suite.txt` SHA-256: `5BDE711E27565732882F6AAD304081475ECD608C001F7EE6D35EF25B150A0B57`

## Validation Commands

- Failing-first transcript: `bundle://proof/SB10/transcripts/failing-first-runtime-checkpoint-tests.txt`
- Passing transcript: `bundle://proof/SB10/transcripts/passing-memory-test-suite.txt`
- Anti-stub audit transcript: `bundle://proof/SB10/transcripts/source-audit-runtime-anti-stub.txt`

## Source Assertions

- Source proof transcript: `bundle://proof/SB10/transcripts/passing-memory-test-suite.txt`
- Test proof transcript: `bundle://proof/SB10/transcripts/passing-memory-test-suite.txt`
- Adversarial negative proof transcript: `bundle://proof/SB10/transcripts/failing-first-runtime-checkpoint-tests.txt`
- Anti-stub audit transcript: `bundle://proof/SB10/transcripts/source-audit-runtime-anti-stub.txt`
- Anti-stub audit result: No stub-only closure is accepted.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| SB10 closure proof | `bundle://proof/SB10/transcripts/passing-memory-test-suite.txt` | `bundle://proof/SB10/transcripts/passing-memory-test-suite.txt` | `bundle://proof/SB10/transcripts/passing-memory-test-suite.txt` | `bundle://proof/SB10/transcripts/failing-first-runtime-checkpoint-tests.txt` |

## Readiness Decision

- Decision: `Passed`.
