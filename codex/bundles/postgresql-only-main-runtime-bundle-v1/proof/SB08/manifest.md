# Proof Manifest - SB08

## Subbundle

Consolidate PostgreSQL Migrations Into One Baseline

## Changed Files

Historical PostgreSQL migrations were replaced by `20260523211921_InitialPostgreSqlBaseline` plus the updated designer and model snapshot.

## Commands Run

- PostgreSQL migration inventory/model checks captured under `evidence/SB08`
- Fresh PostgreSQL database migration proof captured under `evidence/SB08`
- `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "Category!=Browser&Category!=LiveProcess" -v:minimal`

## Evidence Files

- `evidence/SB08/postgresql-baseline-proof.log`
- `evidence/SB08/postgresql-migration-inventory-after.log`
- `evidence/SB08/postgresql-pending-model-check.log`
- `evidence/SB04/integration-test-results-final-passed-2.log`
- `evidence/manual-real-db-alignment.md`

## Result

- [x] Passed
- [ ] Failed
- [ ] Partially complete

## Notes

Real existing databases require operator-led backup and schema/history alignment; no automatic historical live migration is claimed.
