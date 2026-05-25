# SB04 proof manifest

## Status

Completed.

## Changed files

| File | SHA-256 before | SHA-256 after | Reason |
|---|---|---|---|
| `src/CanDoItAll.Modules.Automation/Runtime/AutomationRuntimeOptions.cs` | See `../SB08/transcripts/changed-file-hashes.txt` | See transcript | Add bounded parallelism defaults and validation attributes. |
| `src/CanDoItAll.Modules.Processes/Runtime/ProcessRuntimeOptions.cs` | See transcript | See transcript | Add process outbox batch parallelism limits. |
| `src/CanDoItAll.Modules.Automation/Services/AutomationModuleServiceCollectionExtensions.cs` | See transcript | See transcript | Validate runtime options at startup. |
| `src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` | See transcript | See transcript | Validate process runtime options at startup. |
| `src/CanDoItAll.Web/appsettings.json` | See transcript | See transcript | Set explicit production defaults for outbox/message/connector parallelism. |

## Commands

| Command | Result | Transcript |
|---|---|---|
| Focused lease/options integration filter | Passed, 5 tests | `transcripts/focused-lease-and-options-tests.txt` |
| Throughput options source audit | Passed | `transcripts/throughput-options-source-audit.txt` |

## Source assertions

| Assertion | Source | Proof |
|---|---|---|
| Automation dispatch and connector outbox default to bounded parallelism greater than one. | `AutomationRuntimeOptions.cs`, `appsettings.json` | Source audit transcript. |
| Process outbox has bounded batch size and max parallelism validation. | `ProcessRuntimeOptions.cs`, `appsettings.json` | Source audit transcript. |
| Invalid option values fail startup validation instead of being silently accepted. | Service collection extensions | Source audit transcript. |

## Negative tests

| Scenario | Expected | Result |
|---|---|---|
| Runtime options bind from configuration. | Parallelism values are visible through options. | Passed. |
| Default process worker options are registered. | Concurrent PostgreSQL outbox worker defaults are present. | Passed. |

## Remaining risks

The chosen defaults are conservative. Production tuning should still observe database CPU, connection pool saturation, and handler latency.
