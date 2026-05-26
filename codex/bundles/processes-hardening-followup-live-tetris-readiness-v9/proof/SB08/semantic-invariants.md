# SB08 Semantic Invariants

- Invariant ID: `SB08-INV-001`
- Expected behavior: Validate agents before execution starts.
- Disallowed shallow implementation: prompt-only, docs-only, fixture-only, template-only, or source-assertion-only changes that do not affect production behavior where production behavior is required.
- Must protect generic process core from software-specific hardcoding.
