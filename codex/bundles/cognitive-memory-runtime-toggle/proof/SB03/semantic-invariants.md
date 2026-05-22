# SB03 Semantic Invariants

## Invariants

- Only the configured development PostgreSQL database was reset.
- Migrations were applied after reset, not assumed from a previous database state.
- The database now contains the new runtime setting column with the same default used by the entity configuration.
- Validation includes targeted behavioral tests and a full solution build.

## Residual Risk

- Manual demo runs must use the same development connection string or an explicitly selected profile pointing at `candoitall_development`.
