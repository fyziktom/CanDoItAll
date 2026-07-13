# SB19 Proof Manifest

## Status

- Subbundle: `SB19`
- Result: `Passed`
- Semantic invariant contract: `bundle://proof/SB19/semantic-invariants.md`
- Hash anchor: `bundle://proof/SB19/transcripts/passing-maf-integration-checkpoint-tests.txt` SHA-256: `F639C6DDF519E62A73C7D76A9664ED4183D99540471371F297B68AF6767F2577`

## Validation Commands

- Failing-first transcript: `bundle://proof/SB19/transcripts/failing-first-maf-integration-checkpoint-tests.txt`
- Passing transcript: `bundle://proof/SB19/transcripts/passing-maf-integration-checkpoint-tests.txt`
- Anti-stub audit transcript: `bundle://proof/SB19/transcripts/source-audit-maf-memory-anti-stub.txt`

## Source Assertions

- Source proof transcript: `bundle://proof/SB19/transcripts/passing-maf-integration-checkpoint-tests.txt`
- Test proof transcript: `bundle://proof/SB19/transcripts/passing-maf-integration-checkpoint-tests.txt`
- Adversarial negative proof transcript: `bundle://proof/SB19/transcripts/failing-first-maf-integration-checkpoint-tests.txt`
- Anti-stub audit transcript: `bundle://proof/SB19/transcripts/source-audit-maf-memory-anti-stub.txt`
- Anti-stub audit result: No stub-only closure is accepted.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| SB19 closure proof | `bundle://proof/SB19/transcripts/passing-maf-integration-checkpoint-tests.txt` | `bundle://proof/SB19/transcripts/passing-maf-integration-checkpoint-tests.txt` | `bundle://proof/SB19/transcripts/passing-maf-integration-checkpoint-tests.txt` | `bundle://proof/SB19/transcripts/failing-first-maf-integration-checkpoint-tests.txt` |

## Readiness Decision

- Decision: `Passed`.
