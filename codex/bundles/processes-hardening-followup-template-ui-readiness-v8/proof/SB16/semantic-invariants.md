# SB16 Semantic Invariants

- Invariant ID: SB16-INV-001
- Expected behavior: Run final red-team closure across templates and runtime.
- Disallowed shallow implementation: docs-only, prompt-only, fixture-only, or test-only changes that do not exercise production code paths.
- Required proof: failing-first/adversarial proof, passing proof, source assertions, anti-stub audit, changed-file hashes.
