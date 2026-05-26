# SB03 Semantic Invariants

- Invariant ID: SB03-INV-001
- Expected behavior: Build an explicit matrix for every template in `Templates/Processes/manifest.json`.
- Disallowed shallow implementation: docs-only, prompt-only, fixture-only, or test-only changes that do not exercise production code paths.
- Required proof: failing-first/adversarial proof, passing proof, source assertions, anti-stub audit, changed-file hashes.
