# SB06 Semantic Invariants

## Invariant

- Invariant ID: `SB06-INV-001`
- Requirement IDs: RQ09, RQ11, RQ12
- Expected behavior: Stop repeated identical attempts before max retry count when no new evidence or mutation occurs.
- Disallowed shallow implementation: prompt-only change, source-assertion-only proof, test-only fake, or branch-specific hardcoding.
- Failing-first proof: `bundle://proof/SB06/transcripts/failing-first.txt`
- Passing proof: `bundle://proof/SB06/transcripts/passing.txt`
- Anti-stub audit: `bundle://proof/SB06/transcripts/anti-stub-audit.txt`

## Production Behavior Artifact Matrix

| Signal/state/record/event | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| TBD | TBD | TBD | TBD | TBD |

## Red-Team Negative Cases

- Add at least one realistic negative case where the old behavior would pass incorrectly or block unnecessarily.
- Include at least one generic/non-software case when the subbundle changes generic process semantics.
