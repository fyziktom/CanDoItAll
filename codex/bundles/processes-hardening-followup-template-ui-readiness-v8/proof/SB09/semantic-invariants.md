# SB09 Semantic Invariants

- Invariant ID: SB09-INV-001
- Expected behavior: Required workflow and subprocess artifact expectations have explicit mapping fields available end to end, and strict lint rejects missing or ambiguous mappings instead of relying on same-kind/title projection heuristics.
- Disallowed shallow implementation: docs-only, prompt-only, fixture-only, or test-only changes that do not exercise production code paths.
- Required proof: failing-first/adversarial proof, passing strict-lint/template-projection/mapper ambiguity tests, source assertions, anti-stub audit, changed-file hashes.
