# SB03 Startup Operation And Timing Evidence

Command:

```text
dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-build --no-restore -nologo --filter "FullyQualifiedName~AgentFrameworkExecutionRunTrackingIntegrationTests.SendMessageAsync_records_current_startup_baseline" --logger "console;verbosity=detailed"
```

Result: `Pass, 4/4`.

| Scenario | Catalog loads | Catalog snapshot loads | Provider gets | O(1) provider snapshot captures | Session gets | Run-summary lists | Atomic starts | Detail gets | Detail saves | Catalog snapshot to runtime |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| warm-existing | 0 | 1 | 0 | 4 | 0 | 0 | 1 | 1 | 1 | 519.346 ms |
| cold-new | 0 | 1 | 0 | 4 | 0 | 0 | 1 | 1 | 1 | 272.880 ms |
| warm-new | 0 | 1 | 0 | 4 | 0 | 0 | 1 | 1 | 1 | 250.923 ms |
| cold-existing | 0 | 1 | 0 | 4 | 0 | 0 | 1 | 1 | 1 | 233.177 ms |

The four provider snapshot captures are bounded use-time validations at execution
boundaries. Immutable-state lookup is lock-free and the active-profile identity check
uses the existing short in-memory runtime-state lock. `ProviderProfileGet` remains
zero, but that counter does not include the canonical scalar provider-revision probe.
SB05 subsequently measured one SQL command for an unchanged non-synthetic provider,
zero for a synthetic provider, and three across the changed-provider scenario. This
artifact therefore makes no database-free capture claim.

The `warm-existing` case ran first and is an obvious JIT/first-case outlier. These
single observations are useful operation-count and ordering diagnostics, not a
statistically valid latency benchmark. No latency-improvement claim is made here; SB05
must measure repeated cold/warm distributions and decide the performance gate.
