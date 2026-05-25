# SB04 Semantic Invariants

## SB04-INV-001

Expected behavior: process external-target write authority must come from typed, trusted grounding sources only. Launch plan, current-run project-structure grounding, and explicit step contracts may produce writable aliases. Free-text process prompts, work briefs, upstream artifacts, upstream provenance, stale project-structure lines, and sibling-path mentions are read-only unless independently promoted by a trusted source.

Disallowed shallow implementation:

- prompt-only change
- source-assertion-only proof
- tests that manually seed final state instead of exercising producer/consumer lifecycle
- branch-specific hardcoding
- software-only behavior for generic process runtime

Required proof:

- failing-first/red-team proof
- passing proof
- source assertions
- anti-stub audit
- changed-file hashes
- production behavior artifact matrix when new runtime state is introduced

Proof captured:

- `proof/SB04/transcripts/failing-first.txt`
- `proof/SB04/transcripts/passing.txt`
- `proof/SB04/transcripts/source-assertions.txt`
- `proof/SB04/transcripts/anti-stub-audit.txt`
- `proof/SB04/transcripts/changed-file-hashes.txt`

Durable state note: SB04 introduces no new durable database state. Typed grounding records are transient dispatch metadata used to construct allowed/read-only alias metadata for a single process invocation.
