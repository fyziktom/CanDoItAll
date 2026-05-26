# SB04 Semantic Invariants

- Invariant ID: `SB04-INV-001`
- Expected behavior: Define exactly which skills and tools each process role needs for the live test.
- Disallowed shallow implementation: prompt-only, docs-only, fixture-only, template-only, or source-assertion-only changes that do not affect production behavior where production behavior is required.
- Must protect generic process core from software-specific hardcoding.
