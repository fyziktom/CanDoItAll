# SB14 Proof Manifest

## Status

- Subbundle: `SB14`
- Result: `Passed`
- Semantic invariant contract: `bundle://proof/SB14/semantic-invariants.md`
- Hash anchor: `bundle://proof/SB14/transcripts/passing-crm-resource-invalid-snapshot-regression.txt` SHA-256: `EB96D00DBCFD1D9FAC9E7BB5715F696A7FF304519B73268EA2EEDC38F38B773A`

## Validation Commands

- Failing-first transcript: `bundle://proof/SB14/transcripts/failing-first-source-adapter-regression-tests.txt`
- Passing transcript: `bundle://proof/SB14/transcripts/passing-crm-resource-invalid-snapshot-regression.txt`
- Anti-stub audit transcript: `bundle://proof/SB14/transcripts/source-audit-anti-stub.txt`

## Source Assertions

- Source proof transcript: `bundle://proof/SB14/transcripts/passing-crm-resource-invalid-snapshot-regression.txt`
- Test proof transcript: `bundle://proof/SB14/transcripts/passing-crm-resource-invalid-snapshot-regression.txt`
- Adversarial negative proof transcript: `bundle://proof/SB14/transcripts/failing-first-source-adapter-regression-tests.txt`
- Anti-stub audit transcript: `bundle://proof/SB14/transcripts/source-audit-anti-stub.txt`
- Anti-stub audit result: No stub-only closure is accepted.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| SB14 closure proof | `bundle://proof/SB14/transcripts/passing-crm-resource-invalid-snapshot-regression.txt` | `bundle://proof/SB14/transcripts/passing-crm-resource-invalid-snapshot-regression.txt` | `bundle://proof/SB14/transcripts/passing-crm-resource-invalid-snapshot-regression.txt` | `bundle://proof/SB14/transcripts/failing-first-source-adapter-regression-tests.txt` |

## Readiness Decision

- Decision: `Passed`.
