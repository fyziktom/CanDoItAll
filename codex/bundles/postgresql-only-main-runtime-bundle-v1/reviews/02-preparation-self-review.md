# Preparation self-review

## Coverage

This bundle covers:

- Runtime provider removal.
- Control-plane/profile cleanup.
- UI/dev endpoint cleanup.
- Test support cleanup.
- General runtime limitation cleanup.
- Process/workflow PostgreSQL tuning.
- Snapshot defer/removal.
- PostgreSQL migration consolidation.
- Final validation.

## Important ordering

The bundle intentionally separates SB05 and SB06:

- SB05 removes general SQLite-era runtime limitations.
- SB06 applies PostgreSQL-only assumptions to process/workflow specifics.

This matches the user's requirement that general limitations be handled before concrete process/workflow updates.

## Known uncertainty

The exact current code may have changed since observations were captured. Codex must verify every referenced file on branch `development` before editing.

## Out-of-scope confirmation

CanDoItAll.IPFS is not modified.
