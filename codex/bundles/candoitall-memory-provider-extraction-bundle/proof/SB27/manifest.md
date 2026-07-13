# SB27 Proof Manifest

## Status

- Subbundle: `SB27`
- Result: `Passed`
- Semantic invariant contract: `bundle://proof/SB27/semantic-invariants.md`
- Hash anchor: `bundle://proof/SB27/transcripts/passing-main-native-remote-driver-tests.txt` SHA-256: `2FC887235EDDAD3D61D201690CA90ADCB4B1C73CDD0B63B8E9300FD3276F3AF9`

## Validation Commands

- Failing-first transcript: `bundle://proof/SB27/transcripts/failing-first-native-protocol-api-audit.txt`
- Passing transcript: `bundle://proof/SB27/transcripts/passing-main-native-remote-driver-tests.txt`
- Anti-stub audit transcript: `bundle://proof/SB27/transcripts/anti-stub-audit.txt`

## Source Assertions

- Source proof transcript: `bundle://proof/SB27/transcripts/passing-main-native-remote-driver-tests.txt`
- Test proof transcript: `bundle://proof/SB27/transcripts/passing-main-native-remote-driver-tests.txt`
- Adversarial negative proof transcript: `bundle://proof/SB27/transcripts/failing-first-native-protocol-api-audit.txt`
- Anti-stub audit transcript: `bundle://proof/SB27/transcripts/anti-stub-audit.txt`
- Anti-stub audit result: No stub-only closure is accepted.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| SB27 closure proof | `bundle://proof/SB27/transcripts/passing-main-native-remote-driver-tests.txt` | `bundle://proof/SB27/transcripts/passing-main-native-remote-driver-tests.txt` | `bundle://proof/SB27/transcripts/passing-main-native-remote-driver-tests.txt` | `bundle://proof/SB27/transcripts/failing-first-native-protocol-api-audit.txt` |

## Readiness Decision

- Decision: `Passed`.
