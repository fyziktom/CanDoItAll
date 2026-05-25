# SB07 Semantic Invariants

## Invariant

- Invariant ID: `SB07-INV-001`
- Requirement IDs: RQ10, RQ11, RQ12
- Expected behavior: Catch ambiguous or unsafe process definitions before agents execute them.
- Disallowed shallow implementation: prompt-only change, source-assertion-only proof, test-only fake, or branch-specific hardcoding.
- Failing-first proof: `bundle://proof/SB07/transcripts/failing-first.txt`
- Passing proof: `bundle://proof/SB07/transcripts/passing.txt`
- Anti-stub audit: `bundle://proof/SB07/transcripts/anti-stub-audit.txt`

## Production Behavior Artifact Matrix

| Signal/state/record/event | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| TBD | TBD | TBD | TBD | TBD |

## Red-Team Negative Cases

- Add at least one realistic negative case where the old behavior would pass incorrectly or block unnecessarily.
- Include at least one generic/non-software case when the subbundle changes generic process semantics.
