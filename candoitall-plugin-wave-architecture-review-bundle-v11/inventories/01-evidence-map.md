# Evidence map

## Bundle10 closure evidence
- zero-write `LoadAsync(...)`: `src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs:130-176`
- explicit projection repair seam: `src/CanDoItAll.Modules.Workbench/ProjectStructureProjectionMaintenanceService.cs:15-68`
- zero-write proof tests: `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchProjectionMaintenanceIntegrationTests.cs:15-198`
- unknown manifest shared-editor round-trip proof: `tests/CanDoItAll.Tests.Integration/UnknownConnectorManifestIntegrationTests.cs:18-99`

## Phase11 gap evidence
- in-memory background job queue: `src/CanDoItAll.Infrastructure/BackgroundJobs/BackgroundJobs.cs:15-20,93-101`
- queue registered as singleton in-memory implementation: `src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs:98-100`
- inline “background” work in prompt factory: `src/CanDoItAll.Modules.Factory/PromptFactoryService.cs:688-721,744-771`
- connector outbox pending processor exists: `src/CanDoItAll.Modules.Workspace/ConnectorOutboxService.cs:326-354`
- automation workspace consumes singular signal provider: `src/CanDoItAll.Modules.Automation/AutomationModels.cs:10-24`
- default null signal provider registration: `src/CanDoItAll.Modules.Automation/AutomationModuleServiceCollectionExtensions.cs:9-13`
- additional CRM/HR signal provider registration: `src/CanDoItAll.Modules.CrmHr/CrmHrModuleServiceCollectionExtensions.cs:9-21`
- search baseline showing no hosted worker / Quartz / MQTT tokens and no queue consumer: `inventories/05-runtime-gap-search-baseline.txt`

## Advisory evidence
- marker fallback: `src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs:77-82`
- reference fallback: `src/CanDoItAll.Modules.Workbench/ProjectNodeBindings.cs:391-395`
- hotspots: `inventories/06-advisory-hotspots.md`
