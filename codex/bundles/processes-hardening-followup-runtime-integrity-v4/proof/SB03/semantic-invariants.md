# SB03 Semantic Invariants

## SB03-INV-001

Expected behavior: governed process steps with `agentProcessStepAllowsProductMutation=false` must not mutate product targets indirectly through PowerShell/Python helper scripts. Scripts must be inspected before execution; write signals against grounded product target aliases are denied, uninspected scripts are denied, and read-only validation scripts remain allowed.

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

- `proof/SB03/transcripts/failing-first.txt`
- `proof/SB03/transcripts/passing.txt`
- `proof/SB03/transcripts/source-assertions.txt`
- `proof/SB03/transcripts/anti-stub-audit.txt`
- `proof/SB03/transcripts/changed-file-hashes.txt`

Durable state note: SB03 introduces no new durable database state. The new inspected script content/failure signal is per-invocation runtime metadata only.
