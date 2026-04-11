# Code quality and refactor plan

## Why code quality matters here

This module is about to become a higher-risk operational boundary. That means maintainability is not cosmetic; it is a safety property. If governance, escalation, and future agent logic are piled into existing monoliths, the system will become harder to reason about exactly when correctness starts to matter most.

## Large-file pressure points

| File | Lines |
| --- | --- |
| src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs | 4969 |
| src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor | 1975 |
| src/CanDoItAll.Modules.CrmHr/CrmHrRecruitingServices.cs | 1413 |
| src/CanDoItAll.Modules.CrmHr/PartyDirectoryManagementService.cs | 1300 |
| src/CanDoItAll.Modules.CrmHr/Pages/CrmHrDirectoryPage.razor | 1292 |
| src/CanDoItAll.Modules.CrmHr/Pages/CrmHrCrmPage.razor | 1265 |
| src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs | 1203 |
| src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs | 1147 |
| src/CanDoItAll.Modules.CrmHr/Pages/CrmHrWorkforcePage.razor | 1025 |
| src/CanDoItAll.Modules.Processes/ProcessesService.cs | 993 |
| src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor | 973 |
| src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor.cs | 923 |
| src/CanDoItAll.Modules.Workbench/ProjectNodeKindRegistry.cs | 848 |
| src/CanDoItAll.Modules.Workbench/ProjectWorkbenchMetadata.cs | 847 |
| src/CanDoItAll.Modules.Workbench/ProjectStructureAgentService.cs | 802 |
| src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.Canvas.cs | 787 |
| src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.Workflows.cs | 710 |
| src/CanDoItAll.Modules.CrmHr/CrmHrBusinessModels.cs | 684 |
| src/CanDoItAll.Modules.Workbench/WorkbenchTabState.cs | 678 |
| src/CanDoItAll.Modules.Processes/ProcessRuntimeModels.cs | 676 |

## Process-specific code quality findings

| Area/File | Type | Lines | Priority | Issue | Recommendation |
| --- | --- | --- | --- | --- | --- |
| src/CanDoItAll.Modules.Processes/ProcessesService.cs | God service | 993 | Critical | Definition authoring, validation, publication, cloning, search indexing, activity writes, deletion, analytics helpers, and journal helper logic are co-located. | Split into definition authoring, publication, validation, cloning/import-export, and shared utility services. |
| src/CanDoItAll.Modules.Processes/ProcessesService.Runtime.cs | Runtime orchestration monolith | 479 | Critical | Run creation, assignment prebinding, state transitions, journaling, and artifact handling are combined in one partial file. | Extract runtime orchestration, assignment, approval/escalation, handoff, and artifact services. |
| src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor + .razor.cs + .Canvas.cs | Oversized UI surface | 2683 | High | Definition editing, runtime controls, exchange/import-export, canvas interactions, and selection logic live in one large surface. | Break the workspace into authoring, runtime operations, exchange, and canvas-controller components with smaller backing classes. |
| src/CanDoItAll.Modules.Processes/ProcessDefinitionModels.cs | Flattened canonical model | 558 | Critical | Multiple future canonical concerns are represented as free-text summary fields rather than normalized entities. | Introduce focused files for transitions, contracts, governance, and role/template references. |
| src/CanDoItAll.Modules.Processes/ProcessRuntimeModels.cs | Flattened runtime model | 676 | Critical | Journal, work brief, conformance, and improvement records exist, but handoffs, external correlations, evaluation records, and override models are missing. | Add dedicated runtime entity files grouped by orchestration, telemetry, and governance. |
| src/CanDoItAll.Modules.Processes/ProcessCanvasTemplateCatalog.cs | Local registry risk | 572 | High | Static role templates encode business semantics inside the process module. | Convert the catalog into starter import templates or CRM-HR-backed template references. |
| src/CanDoItAll.Modules.Workbench/ProjectStructureAssemblyService.cs | Projection service already large | 1203 | High | Further process projection logic could bloat an already large assembly service. | Introduce a process-specific projection contributor or sub-service for intervention nodes. |
| Process save/publish persistence pattern | Behavioral design issue |  | High | Child collections are removed and recreated on every save, and publish cloning performs large copy operations in one service. | Adopt stable aggregate child IDs and intent-specific change application. |
| Runtime mutation safety | Behavioral design issue |  | Critical | There is no optimistic concurrency token or runtime conflict strategy. | Add row versions or equivalent concurrency guards and explicit idempotency rules. |
| Cross-module canonicality | Architecture boundary issue |  | Critical | Processes currently own local template content and a future executor seam without enough bridge rules. | Document and enforce canonical ownership rules in code and tests across Processes, CRM-HR, Workspace, and Workbench. |

## Recommended target topology

### Definition side
- `Definitions/ProcessDefinitionService`
- `Definitions/ProcessPublishService`
- `Definitions/Validation/ProcessGraphValidator`
- `Definitions/Validation/GovernanceProfileValidator`
- `Definitions/Persistence/ProcessDefinitionRepository`

### Runtime side
- `Runtime/ProcessRunOrchestrator`
- `Runtime/Assignments/ProcessAssignmentService`
- `Runtime/Handoffs/ProcessHandoffService`
- `Runtime/Approvals/ProcessApprovalService`
- `Runtime/Escalations/ProcessEscalationService`
- `Runtime/Artifacts/ProcessArtifactService`
- `Runtime/Journal/ProcessJournalService`

### Query / projection side
- `Queries/ProcessAnalyticsQueryService`
- `Queries/ProcessJournalQueryService`
- `Projections/ProcessCanvasOverlayProjectionService`
- `Projections/ProjectStructureProcessProjectionService`

### Bridge side
- `Bridges/CrmHrProcessRoleBridge`
- `Bridges/WorkspaceCapabilityBridge`
- `Bridges/IProcessExecutorBridge` implemented later by an adapter package

## Canonicality rules to enforce in code review

- No durable role/agent template ownership in Processes.
- No durable provider registry in Processes.
- No runtime-only hidden workflow topology outside the published process graph.
- No project-structure mutation path that bypasses canonical process runtime state.
- No prompt-only governance for permissions or approvals.
