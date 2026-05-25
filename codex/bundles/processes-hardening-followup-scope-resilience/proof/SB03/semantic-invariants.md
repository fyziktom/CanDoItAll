# SB03 Semantic Invariants

## Invariant

- Invariant ID: `SB03-INV-001`
- Requirement IDs: RQ05, RQ11, RQ12
- Expected behavior: Route negative findings to modeled process branches instead of blocking whenever a governed disposition can be made.
- Disallowed shallow implementation: prompt-only change, source-assertion-only proof, test-only fake, or branch-specific hardcoding.
- Failing-first proof: `bundle://proof/SB03/transcripts/failing-first.txt`
- Passing proof: `bundle://proof/SB03/transcripts/passing.txt`
- Anti-stub audit: `bundle://proof/SB03/transcripts/anti-stub-audit.txt`

## Production Behavior Artifact Matrix

| Signal/state/record/event | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| TBD | TBD | TBD | TBD | TBD |

## Red-Team Negative Cases

- Add at least one realistic negative case where the old behavior would pass incorrectly or block unnecessarily.
- Include at least one generic/non-software case when the subbundle changes generic process semantics.
