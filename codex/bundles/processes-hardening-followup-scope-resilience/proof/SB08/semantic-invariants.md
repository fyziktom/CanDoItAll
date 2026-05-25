# SB08 Semantic Invariants

## Invariant

- Invariant ID: `SB08-INV-001`
- Requirement IDs: RQ01, RQ02, RQ03, RQ04, RQ05, RQ06, RQ07, RQ08, RQ09, RQ10, RQ11, RQ12
- Expected behavior: Prove the hardening with realistic process scenarios and final closure gates.
- Disallowed shallow implementation: prompt-only change, source-assertion-only proof, test-only fake, or branch-specific hardcoding.
- Failing-first proof: `bundle://proof/SB08/transcripts/failing-first.txt`
- Passing proof: `bundle://proof/SB08/transcripts/passing.txt`
- Anti-stub audit: `bundle://proof/SB08/transcripts/anti-stub-audit.txt`

## Production Behavior Artifact Matrix

| Signal/state/record/event | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| TBD | TBD | TBD | TBD | TBD |

## Red-Team Negative Cases

- Add at least one realistic negative case where the old behavior would pass incorrectly or block unnecessarily.
- Include at least one generic/non-software case when the subbundle changes generic process semantics.
