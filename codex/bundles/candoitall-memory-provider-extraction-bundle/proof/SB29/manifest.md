# SB29 Proof Manifest

## Status

- Subbundle: `SB29`
- Result: `Passed`
- Semantic invariant contract: `bundle://proof/SB29/semantic-invariants.md`
- Hash anchor: `bundle://proof/SB29/transcripts/passing-main-native-remote-driver-tests.txt` SHA-256: `D16AEDE1CA182344944397B1F9A9D0C3E8E4A58686C89BAEA8898FD489185E2E`

## Validation Commands

- Failing-first transcript: `bundle://proof/SB29/transcripts/failing-first-native-hardening-audit.txt`
- Passing transcript: `bundle://proof/SB29/transcripts/passing-main-native-remote-driver-tests.txt`
- Anti-stub audit transcript: `bundle://proof/SB29/transcripts/native-anti-stub-audit.txt`

## Source Assertions

- Source proof transcript: `bundle://proof/SB29/transcripts/passing-main-native-remote-driver-tests.txt`
- Test proof transcript: `bundle://proof/SB29/transcripts/passing-main-native-remote-driver-tests.txt`
- Adversarial negative proof transcript: `bundle://proof/SB29/transcripts/failing-first-native-hardening-audit.txt`
- Anti-stub audit transcript: `bundle://proof/SB29/transcripts/native-anti-stub-audit.txt`
- Anti-stub audit result: No stub-only closure is accepted.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| SB29 closure proof | `bundle://proof/SB29/transcripts/passing-main-native-remote-driver-tests.txt` | `bundle://proof/SB29/transcripts/passing-main-native-remote-driver-tests.txt` | `bundle://proof/SB29/transcripts/passing-main-native-remote-driver-tests.txt` | `bundle://proof/SB29/transcripts/failing-first-native-hardening-audit.txt` |

## Readiness Decision

- Decision: `Passed`.
