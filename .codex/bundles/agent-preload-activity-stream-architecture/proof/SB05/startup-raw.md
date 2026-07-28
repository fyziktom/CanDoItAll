# SB05 Final Startup Samples

## Provenance

- Class: `Confirmed handoff`
- Original raw console output: not retained
- Original command line: not retained
- The exact samples, diagnostics, and five-pass/four-scenario result below were
  confirmed by the parent validation workflow.
- The filename is retained for the bundle's required artifact path; this document
  must not be cited as a raw console transcript.

## Suggested reproduction command

```powershell
dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-build --no-restore -m:1 -p:UseSharedCompilation=false -nodeReuse:false --filter "FullyQualifiedName~AgentFrameworkExecutionRunTrackingIntegrationTests.SendMessageAsync_records_current_startup_baseline" --logger "console;verbosity=detailed"
```

This is a reproducible suggestion, not the original retained command. The confirmed
validation ran five repetitions against the final coherent backend, and every
repetition passed all four scenarios.

## Samples

| Repetition | Cold/new | Warm/new | Cold/existing | Warm/existing |
| ---: | ---: | ---: | ---: | ---: |
| 1 | 231.915 | 241.942 | 153.681 | 396.294 |
| 2 | 257.906 | 316.867 | 164.588 | 433.011 |
| 3 | 220.631 | 273.891 | 185.760 | 392.603 |
| 4 | 578.993 | 214.265 | 168.137 | 527.575 |
| 5 | 230.016 | 234.413 | 183.230 | 407.779 |

## Invariant diagnostic row

Every one of the 20 scenario executions reported:

```text
accepted-publications=1
catalog-loads=0
catalog-snapshot-loads=1
provider-gets=0
provider-snapshot-acquires=1
provider-snapshot-captures=3
session-gets=0
run-summary-lists=0
atomic-chat-starts=1
run-detail-gets=0
run-detail-saves=0
run-detail-updates=1
```

Cold rows reported one preparation refresh and zero reuse. Warm rows reported zero
refresh and one reuse. All rows asserted this typed milestone order:

```text
ActivityAcceptedPublished
CatalogSnapshotLoad
ProviderSnapshotAcquire
ExecutionEventPublished
RuntimeEntered
```
