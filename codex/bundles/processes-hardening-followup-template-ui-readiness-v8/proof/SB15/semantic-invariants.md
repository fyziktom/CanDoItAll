# SB15 Semantic Invariants

- Invariant ID: SB15-INV-001
- Expected behavior: Prepare the next UI test without running it yet.
- Disallowed shallow implementation: docs-only, prompt-only, fixture-only, or test-only changes that do not exercise production code paths.
- Required proof: failing-first/adversarial proof, passing proof, source assertions, anti-stub audit, changed-file hashes.
