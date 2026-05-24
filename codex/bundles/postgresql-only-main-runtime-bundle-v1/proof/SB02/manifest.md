# Proof Manifest - SB02

## Subbundle

PostgreSQL-Only Database Profile and Control-Plane Contract

## Changed Files

Database profile models, startup resolution, control-plane service, runtime switching, profile defaults, tests, and documentation were updated for PostgreSQL-only activation with explicit legacy SQLite rejection.

## Commands Run

- `dotnet test .\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build -v:minimal`
- `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "Category!=Browser&Category!=LiveProcess" -v:minimal`

## Evidence Files

- `evidence/SB04/unit-test-results-final-passed-3.log`
- `evidence/SB04/integration-test-results-final-passed-2.log`

## Result

- [x] Passed
- [ ] Failed
- [ ] Partially complete

## Notes

Legacy SQLite profile data remains readable only for unsupported-state messaging and stale catalog cleanup. Runtime activation rejects it.
