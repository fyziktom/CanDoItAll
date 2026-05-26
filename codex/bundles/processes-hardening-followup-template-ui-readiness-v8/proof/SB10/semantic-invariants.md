# SB10 Semantic Invariants

- Invariant ID: SB10-INV-001
- Expected behavior: Prevent manual/API transition from being weaker than automation finalization.
- Disallowed shallow implementation: docs-only, prompt-only, fixture-only, or test-only changes that do not exercise production code paths.
- Required proof: failing-first/adversarial proof, passing proof, source assertions, anti-stub audit, changed-file hashes.
