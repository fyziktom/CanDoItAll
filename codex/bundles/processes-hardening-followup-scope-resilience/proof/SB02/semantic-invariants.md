# SB02 Semantic Invariants

## Invariant

- Invariant ID: `SB02-INV-001`
- Requirement IDs: RQ03, RQ04, RQ08, RQ12
- Expected behavior: Make workflow-backed roles and subprocess parent steps obey the same process artifact/finalizer contract as direct agents.
- Disallowed shallow implementation: prompt-only change, source-assertion-only proof, test-only fake, or branch-specific hardcoding.
- Failing-first proof: `bundle://proof/SB02/transcripts/failing-first.txt`
- Passing proof: `bundle://proof/SB02/transcripts/passing.txt`
- Anti-stub audit: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`

## Production Behavior Artifact Matrix

| Signal/state/record/event | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| TBD | TBD | TBD | TBD | TBD |

## Red-Team Negative Cases

- Add at least one realistic negative case where the old behavior would pass incorrectly or block unnecessarily.
- Include at least one generic/non-software case when the subbundle changes generic process semantics.
