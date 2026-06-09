# SB004 Startup Composition Inventory

## Status
Completed.

## Source Inventory
| Surface | Source | Inventory result |
| --- | --- | --- |
| Runtime module composition | `repo://src/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs` | `AddCanDoItAllRuntimeModules` calls `AddProcessesModule(configuration)`. |
| Module assembly discovery | `repo://src/CanDoItAll.Composition/ModuleAssemblies.cs` | `CanDoItAll.Modules.Processes` is included through `ProcessesModuleAssemblyMarker`. |
| Web startup service registration | `repo://src/CanDoItAll.Web/Program.cs` | Calls `AddCanDoItAllRuntimeModules`, `AddCanDoItAllApi`, and maps Razor components with `ModuleAssemblies.All`. |
| Process API map | `repo://src/CanDoItAll.Web/Api/ApiEndpointRouteBuilderExtensions.cs` and `repo://src/CanDoItAll.Web/Api/ProcessesApi.cs` | `/api/processes` is mapped through the central `/api` group. |
| Project-structure API map | `repo://src/CanDoItAll.Web/ProjectStructureAgentApi.cs` | Project-structure process start endpoints are mapped before app run. |
| Health route | `repo://src/CanDoItAll.Web/Program.cs` and `repo://src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs` | `/health` is mapped and backed by `RuntimeReadinessHealthCheck`. |
| Managed files | `repo://src/CanDoItAll.Web/Infrastructure/ManagedFilesEndpointRoutes.cs` | `/managed-files/{**path}`, `/storage/objects/preview`, and `/storage/objects/download` are mapped before API and Razor components. |
| Process services | `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` | `ProcessesService`, dispatch service, process runtime read service, runtime tool provider, catalog warmup, recovery, and outbox workers are registered. |
| Hosted-worker policy | `repo://tests/CanDoItAll.Tests.Integration/RuntimeHostedWorkerPolicyIntegrationTests.cs` | Published lanes suppress process background workers; source-watch lanes register outbox/recovery workers according to configuration. |

## Tests
- Startup/composition integration tests: `bundle://proof/SB004/transcripts/startup-composition-tests.txt`
- Test result: `bundle://proof/SB004/test-results/SB004-startup-composition.trx`
- Result: 7 passed, 0 failed.

## Scans
- Source assertions: `bundle://proof/SB004/transcripts/startup-composition-source-assertions.txt`
- Anti-stub/runtime-host drift: `bundle://proof/SB004/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- No transient bundle path: `bundle://proof/SB004/transcripts/no-transient-bundle-path-scan.txt`

## Changed Files
No production or long-lived test source changes were required for SB004. This subbundle is an inventory/proof gate over existing startup wiring.
