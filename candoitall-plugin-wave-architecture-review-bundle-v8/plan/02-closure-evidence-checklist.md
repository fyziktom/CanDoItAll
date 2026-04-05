## Closure evidence checklist

Codex must not mark the phase closed until it can attach proof for each hard gate.

### Required proof types
- `rg` output showing forbidden symbols/patterns are gone
- unit/integration/component tests added or updated
- schema migration evidence where persistence changes
- before/after ownership explanation for every moved field
- updated ADR or architecture note where the ownership model changes
- runtime evidence from real .NET environment after implementation

### Anti-evasion rule
Moving logic into a helper without deleting the forbidden source of truth is **not closure**.
