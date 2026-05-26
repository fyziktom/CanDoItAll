# SB01 Semantic Invariants

- Invariant ID: `SB01-INV-001`
- Expected behavior: Verify phase8 really fixed the previous structural issues and does not introduce build/test breakage.
- Disallowed shallow implementation: prompt-only, docs-only, fixture-only, template-only, or source-assertion-only changes that do not affect production behavior where production behavior is required.
- Must protect generic process core from software-specific hardcoding.
