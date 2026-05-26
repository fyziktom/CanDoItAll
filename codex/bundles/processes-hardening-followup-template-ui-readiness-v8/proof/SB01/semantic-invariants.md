# SB01 Semantic Invariants

- Invariant ID: SB01-INV-001
- Expected behavior: Fix build/compile integrity before all other work.
- Disallowed shallow implementation: docs-only, prompt-only, fixture-only, or test-only changes that do not exercise production code paths.
- Required proof: failing-first/adversarial proof, passing proof, source assertions, anti-stub audit, changed-file hashes.
