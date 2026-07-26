# Scope Inventory

## Contracts And Projections

- `src/Processes/CanDoItAll.Processes.Projections/ProcessProjectionReadModels.cs`
- `src/Processes/CanDoItAll.Processes.Projections/ProcessProjectionContracts.cs`
- `src/Processes/CanDoItAll.Processes.Projections/ProcessProjectionQueries.cs`
- `src/Processes/CanDoItAll.Processes.Projections/ProcessRuntimeProjectionProjector.cs`
- `src/Processes/CanDoItAll.Processes.Projections/ProcessProjectionWorker.cs`

## Application Read/Finalization Paths

- `src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeProjectionCatchupService.cs`
- `src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs`
- `src/Processes/CanDoItAll.Processes.Application/ProcessWorkspaceShellProjectionService.cs`

## Persistence

- `src/Processes/CanDoItAll.Processes.Persistence/ProcessPersistenceEntities.cs`
- `src/Processes/CanDoItAll.Processes.Persistence/ProcessPersistenceConfigurations.cs`
- `src/Processes/CanDoItAll.Processes.Persistence/ProcessPersistenceDbContext.cs`
- `src/Processes/CanDoItAll.Processes.Persistence/EfProcessProjectionStore.cs`
- `src/Processes/CanDoItAll.Processes.Persistence/EfProcessRuntimeUnitOfWork.cs`
- `src/Processes/CanDoItAll.Processes.Persistence/EfProcessRuntimeStepAssignmentStore.cs`
- `src/Foundation/CanDoItAll.Migrations.PostgreSql`

## Provider/Module Integration

- `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionObservationReader.cs`
- `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessRuntimeUsageTelemetryReader.cs`
- Processes module service-registration/composition files selected during SB02/SB03.
- Existing Agent Framework structured-output and hosted-worker/claim patterns selected during implementation inspection.

## Consumers

- `src/App/CanDoItAll.Web/Api/ProcessesApi.cs`
- `src/Modules/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor`
- `src/Modules/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`
- `src/Modules/CanDoItAll.Modules.Dashboard/Services/ProcessDashboardActivityQueryService.cs`
- `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessProjectionContributor.cs`
- `src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/Economics/EfProcessHistoricalRunCostReader.cs`
- `src/Modules/CanDoItAll.Modules.CrmHr/Services/HrAgentProcessReviewService.cs`

## Tests

- `tests/Unit/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/ProcessWorkspaceShellTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/ProcessDashboardActivityQueryTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/EfProcessHistoricalRunCostReaderTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/HrAgentProcessReviewServiceTests.cs`
- `tests/Integration/CanDoItAll.Tests.Integration/ProcessApiIntegrationTests.cs`
- New focused test files are preferred over enlarging already broad fixtures when cohesion improves.

## External Documentation

- `C:\repositories\CanDoItAll.SharedInfo\codex\skills\candoitall-api-processes\SKILL.md`

## Scope Guard

Potentially related but deferred unless required by failing proof: a general Agent Framework filesystem indexing redesign, a global projection-transaction refactor, UI redesign, and deletion of legacy deep-detail routes.
