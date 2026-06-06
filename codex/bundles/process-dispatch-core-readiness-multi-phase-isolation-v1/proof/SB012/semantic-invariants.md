# SB012 Semantic Invariants

- PostgreSQL requirement blocking still logs actionable run and step state.
- Missing upstream materialization still blocks supported step states before requesting materialization.
- Unsupported or failed transitions log and fail predictably; no silent fallback was introduced.
