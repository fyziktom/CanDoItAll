# SB30 Proof Manifest

## Status

- Subbundle: `SB30`
- Result: `Passed`
- Semantic invariant contract: `bundle://proof/SB30/semantic-invariants.md`
- Hash anchor: `bundle://proof/SB30/transcripts/passing-host-composition-dependency-removal-tests.txt` SHA-256: `D07CD061145003C918355EEFA8A65912E19184D6B7F8C9D3CCBC12A54CAA2647`

## Validation Commands

- Failing-first transcript: `bundle://proof/SB30/transcripts/failing-first-host-composition-audit.txt`
- Passing transcript: `bundle://proof/SB30/transcripts/passing-host-composition-dependency-removal-tests.txt`
- Anti-stub audit transcript: `bundle://proof/SB30/transcripts/anti-stub-and-xml-doc-audit.txt`

## Source Assertions

- Source proof transcript: `bundle://proof/SB30/transcripts/passing-host-composition-dependency-removal-tests.txt`
- Test proof transcript: `bundle://proof/SB30/transcripts/passing-host-composition-dependency-removal-tests.txt`
- Adversarial negative proof transcript: `bundle://proof/SB30/transcripts/failing-first-host-composition-audit.txt`
- Anti-stub audit transcript: `bundle://proof/SB30/transcripts/anti-stub-and-xml-doc-audit.txt`
- Anti-stub audit result: No stub-only closure is accepted.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| SB30 closure proof | `bundle://proof/SB30/transcripts/passing-host-composition-dependency-removal-tests.txt` | `bundle://proof/SB30/transcripts/passing-host-composition-dependency-removal-tests.txt` | `bundle://proof/SB30/transcripts/passing-host-composition-dependency-removal-tests.txt` | `bundle://proof/SB30/transcripts/failing-first-host-composition-audit.txt` |

## Readiness Decision

- Decision: `Passed`.
