# Core Timing

## Baseline

- Command: `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter FullyQualifiedName~ProcessActiveRunSummaryPerformanceTests -v:minimal --logger "console;verbosity=detailed"`
- Scenario: `LoadActiveRunSummariesAsync` over 12 active process runs.
- Result: `239 ms`.

## Optimized

- Command: `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter FullyQualifiedName~ProcessActiveRunSummaryPerformanceTests -v:minimal --logger "console;verbosity=detailed"`
- Scenario: same 12 active process runs, plus correctness assertions for pending outbox, dead-letter outbox, and blocked-step summary values.
- Final result: `60 ms`.
- Earlier optimized run before analyzer-warning cleanup: `51 ms`.

## Interpretation

The active-run summary path is about `75%` faster in the repeated measurement because it no longer loads full run details or scans AgentFramework execution history once per active process run.
