# Proof Manifest - SB06

## Subbundle

Tune Processes, Workflows, Automation, and Outbox for PostgreSQL

## Changed Files

Process runtime transactions, automation grounding SQL, workflow template edge IDs, activity timeline ordering, process assignment concurrency handling, and related integration tests were updated for PostgreSQL semantics.

## Commands Run

- `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter <previous integration failures> -v:minimal`
- `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter "Category!=Browser&Category!=LiveProcess" -v:minimal`

## Evidence Files

- `evidence/SB04/integration-failed-set-recheck-passed.log`
- `evidence/SB04/integration-assignment-concurrency-recheck.log`
- `evidence/SB04/integration-test-results-final-passed-2.log`

## Result

- [x] Passed
- [ ] Failed
- [ ] Partially complete

## Notes

PostgreSQL assignment concurrency is serialized with a transaction-scoped advisory lock at the shared run/role/step scope.
