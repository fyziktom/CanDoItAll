# SB005 Process Module Registration Proof

## Status
Completed.

## Registration Surface
| Dependency | Registration source | Proof |
| --- | --- | --- |
| `ProcessesService` | `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` | Scoped registration present and resolved by startup smoke test. |
| `ProcessOutboxService` | `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` | Scoped registration present. |
| `IProcessAutomationExecutionClient` | `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` | Scoped registration to `ProcessAutomationExecutionClient`; covered by `ProcessAutomationExecutionClientTests`. |
| `IProcessRunAutomationDispatchService` | `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` | Scoped registration to `ProcessRunAutomationDispatchService`; resolved by startup smoke test. |
| `IProcessRuntimeReadQueryService` | `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` | Scoped registration to `ProcessRuntimeReadQueryService`. |
| `IAgentRuntimeToolProvider` | `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` | `ProcessAgentRuntimeToolProvider` is registered through `TryAddEnumerable`; composition parity test proves exact tool names. |
| Hosted workers | `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` | `ProcessCatalogWarmupWorker`, `ProcessRunRecoveryWorker`, and `ProcessOutboxDrainWorker` are governed by runtime lane/options and covered by hosted-worker policy tests. |

## Tests
- Integration registration tests: `bundle://proof/SB005/transcripts/process-module-registration-integration-tests.txt`
- Integration test result: `bundle://proof/SB005/test-results/SB005-process-module-registration-integration.trx`
- Unit tool-provider tests: `bundle://proof/SB005/transcripts/process-runtime-tool-provider-unit-tests.txt`
- Unit test result: `bundle://proof/SB005/test-results/SB005-process-runtime-tool-provider-unit.trx`
- Result: 8 integration tests and 10 unit tests passed.

## Scans
- Source assertions: `bundle://proof/SB005/transcripts/process-module-registration-source-assertions.txt`
- Anti-stub/runtime-host drift: `bundle://proof/SB005/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- No transient bundle path: `bundle://proof/SB005/transcripts/no-transient-bundle-path-scan.txt`

## Changed Files
No production or long-lived test source changes were required for SB005. Existing source already contains the expected registrations and existing tests prove them.
