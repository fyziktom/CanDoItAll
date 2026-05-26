# SB08 Semantic Invariants

- Invariant ID: SB08-INV-001
- Expected behavior: Every process template step in the manifest declares typed governance fields, and generic/business templates use managed-artifact or read-only/external-action contracts instead of product-mutation contracts unless the step truly mutates a product target.
- Disallowed shallow implementation: docs-only, prompt-only, fixture-only, or test-only changes that do not exercise production code paths.
- Required proof: strict governance audit, production template-pack regression test, failing-first/adversarial proof, passing proof, source assertions, anti-stub audit, changed-file hashes.
