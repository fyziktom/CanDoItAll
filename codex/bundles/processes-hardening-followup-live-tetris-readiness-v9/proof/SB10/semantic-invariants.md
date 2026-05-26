# SB10 Semantic Invariants

- Invariant ID: `SB10-INV-001`
- Expected behavior: Make required evidence robust enough for the live test.
- Disallowed shallow implementation: prompt-only, docs-only, fixture-only, template-only, or source-assertion-only changes that do not affect production behavior where production behavior is required.
- Must protect generic process core from software-specific hardcoding.
