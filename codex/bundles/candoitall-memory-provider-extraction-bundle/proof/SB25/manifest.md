# SB25 Proof Manifest

## Status

- Subbundle: `SB25`
- Result: `Passed`
- Semantic invariant contract: `bundle://proof/SB25/semantic-invariants.md`
- Hash anchor: `bundle://proof/SB25/transcripts/passing-main-solution-build.txt` SHA-256: `F67ED6792A5CA0447C604FF06810C184FC6F8BC656DAA2BDF7A9194936CD97CF`

## Validation Commands

- Failing-first transcript: `bundle://proof/SB25/transcripts/failing-first-native-persistence-audit.txt`
- Passing transcript: `bundle://proof/SB25/transcripts/passing-main-solution-build.txt`
- Anti-stub audit transcript: `bundle://proof/SB25/transcripts/anti-stub-audit.txt`

## Source Assertions

- Source proof transcript: `bundle://proof/SB25/transcripts/passing-main-solution-build.txt`
- Test proof transcript: `bundle://proof/SB25/transcripts/passing-main-solution-build.txt`
- Adversarial negative proof transcript: `bundle://proof/SB25/transcripts/failing-first-native-persistence-audit.txt`
- Anti-stub audit transcript: `bundle://proof/SB25/transcripts/anti-stub-audit.txt`
- Anti-stub audit result: No stub-only closure is accepted.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| SB25 closure proof | `bundle://proof/SB25/transcripts/passing-main-solution-build.txt` | `bundle://proof/SB25/transcripts/passing-main-solution-build.txt` | `bundle://proof/SB25/transcripts/passing-main-solution-build.txt` | `bundle://proof/SB25/transcripts/failing-first-native-persistence-audit.txt` |

## Readiness Decision

- Decision: `Passed`.
