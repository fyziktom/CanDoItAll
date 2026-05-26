# SB13 Semantic Invariants

- Invariant ID: SB13-INV-001
- Expected behavior: the repo-managed and active Processes API skill both document the current typed process governance model, including operation contracts, target scopes, contract mode, block/recovery health, projection lineage, workflow/subprocess mappings, concrete API examples, Tetris readiness, and workflow-as-executor boundaries.
- Disallowed shallow implementation: updating only prose without concrete route examples, omitting active skill-root synchronization, omitting workflow/subprocess mapping fields, omitting block/recovery fields, or describing Workflows as replacements for Processes.
- Required proof: adversarial required-term assertions, diff hygiene, active/repo skill hash equality, source assertions, anti-stub audit, and changed-file hashes.
- Positive proof: `bundle://proof/SB13/transcripts/passing.txt` proves `git diff --check` passed and the active skill hash matches the repo skill hash.
- Negative/adversarial proof: `bundle://proof/SB13/transcripts/failing-first.txt` fails if required guidance terms or active skill sync are missing.
