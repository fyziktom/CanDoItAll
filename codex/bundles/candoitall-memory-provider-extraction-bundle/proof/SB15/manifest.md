# SB15 Proof Manifest

## Status

- Subbundle: `SB15`
- Result: `Passed`
- Semantic invariant contract: `bundle://proof/SB15/semantic-invariants.md`
- Hash anchor: `bundle://proof/SB15/transcripts/passing-memory-test-suite.txt` SHA-256: `F8A3CCA06A7D63188F6E6F04A1E20D03DC49F6C91865DFF45BB72685499CEBD8`

## Validation Commands

- Failing-first transcript: `bundle://proof/SB15/transcripts/failing-first-shared-handler-tests.txt`
- Passing transcript: `bundle://proof/SB15/transcripts/passing-memory-test-suite.txt`
- Anti-stub audit transcript: `bundle://proof/SB15/transcripts/source-audit-anti-stub.txt`

## Source Assertions

- Source proof transcript: `bundle://proof/SB15/transcripts/passing-memory-test-suite.txt`
- Test proof transcript: `bundle://proof/SB15/transcripts/passing-memory-test-suite.txt`
- Adversarial negative proof transcript: `bundle://proof/SB15/transcripts/failing-first-shared-handler-tests.txt`
- Anti-stub audit transcript: `bundle://proof/SB15/transcripts/source-audit-anti-stub.txt`
- Anti-stub audit result: No stub-only closure is accepted.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| SB15 closure proof | `bundle://proof/SB15/transcripts/passing-memory-test-suite.txt` | `bundle://proof/SB15/transcripts/passing-memory-test-suite.txt` | `bundle://proof/SB15/transcripts/passing-memory-test-suite.txt` | `bundle://proof/SB15/transcripts/failing-first-shared-handler-tests.txt` |

## Readiness Decision

- Decision: `Passed`.
