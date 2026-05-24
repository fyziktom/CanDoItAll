# SB07 semantic invariants

## Status

Pending execution.

## Invariants to prove

- PostgreSQL-only runtime remains intact.
- Canonical runtime DB truth is not split.
- No stale claim can commit mutation.
- No normal runtime path uses profile-specific maintenance contexts unless explicitly allowed.
