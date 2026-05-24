# Proof Manifest - SB04

## Subbundle

Convert Tests and Test Support Away From SQLite

## Changed Files

Test support database leases, profile editor factories, unit tests, component tests, integration tests, and stale SQLite write-coordination tests were converted to PostgreSQL or explicit in-memory unit-test providers.

## Commands Run

- `dotnet test .\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build -v:minimal`
- `dotnet test .\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-build --filter <32 previous failures> -v:minimal`
- `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "Category!=Browser&Category!=LiveProcess" -v:minimal`

## Evidence Files

- `evidence/SB04/unit-test-results-final-passed-3.log`
- `evidence/SB04/component-targeted-final-passed-2.log`
- `evidence/SB04/component-database-profile-settings-final.log`
- `evidence/SB04/integration-test-results-final-passed-2.log`
- `evidence/SB04/test-audit.log`

## Result

- [x] Passed
- [ ] Failed
- [ ] Partially complete

## Notes

The full integration suite passed with browser/live-process categories excluded. Component validation includes the original failure set and database profile/settings coverage.
