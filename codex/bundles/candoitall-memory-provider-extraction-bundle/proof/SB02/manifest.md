# SB02 Proof Manifest

## Status

- Subbundle: `SB02`
- Result: `Passed`
- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`
- Hash anchor: `bundle://proof/SB02/transcripts/passing-provider-registry-tests.txt` SHA-256: `ADFE601021DCF6BC8D31B570E33C36E1FE2482BBCD93F0E80D706CC26E2BCB0A`

## Validation Commands

- Failing-first transcript: `bundle://proof/SB02/transcripts/failing-first-provider-registry-tests.txt`
- Passing transcript: `bundle://proof/SB02/transcripts/passing-provider-registry-tests.txt`
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`

## Source Assertions

- Source proof transcript: `bundle://proof/SB02/transcripts/passing-provider-registry-tests.txt`
- Test proof transcript: `bundle://proof/SB02/transcripts/passing-provider-registry-tests.txt`
- Adversarial negative proof transcript: `bundle://proof/SB02/transcripts/failing-first-provider-registry-tests.txt`
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`
- Anti-stub audit result: No stub-only closure is accepted.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| SB02 closure proof | `bundle://proof/SB02/transcripts/passing-provider-registry-tests.txt` | `bundle://proof/SB02/transcripts/passing-provider-registry-tests.txt` | `bundle://proof/SB02/transcripts/passing-provider-registry-tests.txt` | `bundle://proof/SB02/transcripts/failing-first-provider-registry-tests.txt` |

## Readiness Decision

- Decision: `Passed`.
