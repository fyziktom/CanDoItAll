# SB001 Source Inventory

## Gate Decision
- Entry gate: Pass. SB001 has no prerequisite subbundle, owns the real-code verification raw note, and all exact source references exist.
- Closure gate: Pass for reconciliation and inventory. No production code changed in SB001.
- Carry-forward finding: long-lived unit-test fixture data contains concrete prior `codex/bundles/<bundle-name>` paths. SB002/SB003 must remove or replace that coupling.

## Source-Backed Runtime Surfaces
- Global API: `repo://src/CanDoItAll.Web/Api/ProcessesApi.cs` maps `/api/processes` under the shared `/api` group, including `/runs/start`, launch-plan routes, template routes, run detail, artifacts, assignments, transitions, manager directives, and analytics.
- Global UI navigation: `repo://src/CanDoItAll.Web/Composition/ShellNavigation.cs` lists `/processes` and `/processes/live`; `repo://src/CanDoItAll.Web/Components/Routes.razor` uses `ModuleAssemblies.All`.
- Process UI components: `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor` and `repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`.
- Project-structure launch: `repo://src/CanDoItAll.Web/ProjectStructureAgentApi.cs` maps `/api/project-structure/projects/{projectId}/nodes/{nodeId}/process/start`, and `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAgentService.cs` owns `StartProcessNodeAsync`.
- Process launch context bridge: `repo://src/CanDoItAll.Modules.Processes/ProjectStructure/ProcessProjectStructureContext.cs`, `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs`, and `repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessRunSyncBridge.cs`.
- Scheduler-origin launch: `repo://src/CanDoItAll.Modules.SchedulerPlanner/SchedulerPlannerService.cs` uses `ProcessesService.StartRunFromTriggerAsync`.
- Trigger start service: `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.TriggerStart.cs` validates trigger source kind/requester/source id and delegates to `StartRunAsync`.
- Launch-plan execution: `repo://src/CanDoItAll.Modules.Processes/Launch/ProcessesService.Launch.cs` delegates `ExecuteLaunchPlanAsync` to `StartRunAsync`.
- Dispatch/finalizer runtime: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService*.cs`.
- Hosted runtime lane: `repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs` registers `ProcessCatalogWarmupWorker`, `ProcessRunRecoveryWorker`, and `ProcessOutboxDrainWorker` behind runtime options.
- MAF process tools: `repo://src/CanDoItAll.Modules.Processes/AgentTools/ProcessAgentRuntimeToolProvider.cs` is registered as `IAgentRuntimeToolProvider`.
- Read-only driver diagnostics: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationBatchOrchestrator.cs` composes read-only verification lanes and does not approve a generic driver runtime host.

## Proof Artifacts
- Source reconciliation transcript: `bundle://proof/SB001/transcripts/source-reconciliation.txt`.
- Focused unit test transcript: `bundle://proof/SB001/transcripts/focused-unit-tests.txt`.
- Focused unit TRX: `bundle://proof/SB001/test-results/SB001-focused-unit.trx`.
- Anti-stub scan: `bundle://proof/SB001/transcripts/anti-stub-scan.txt`.
- Transient bundle path scan: `bundle://proof/SB001/transcripts/transient-bundle-path-scan.txt`.
