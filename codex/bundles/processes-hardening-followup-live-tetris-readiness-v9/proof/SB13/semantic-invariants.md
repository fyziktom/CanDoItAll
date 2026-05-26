# SB13 Semantic Invariants

- Invariant ID: `SB13-INV-001`
- Expected behavior: Keep process core and templates generic, not software-only.
- Disallowed shallow implementation: prompt-only, docs-only, fixture-only, template-only, or source-assertion-only changes that do not affect production behavior where production behavior is required.
- Must protect generic process core from software-specific hardcoding.
