# SB01 Semantic Invariants

- Invariant ID: `SB01-INV-001`
- Expected behavior: Freeze failed-run evidence and create a failing-first regression before upgrade.
- Disallowed shallow implementation: prompt-only, docs-only, fixture-only, source-assertion-only, or loosening validation without preserving safety.
