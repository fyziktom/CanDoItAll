# Driver Readiness Map

This map is documentation-only in this bundle. It names future dispatch/evidence
intent categories so later work can reason about helper-driver readiness without
creating Process Core, driver packs, production driver APIs, or new public
runtime contracts now.

## Current Runtime Cutline

The current module-local runtime boundary is:

| Boundary | Current owner | Future relevance |
| --- | --- | --- |
| Candidate and route facts | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteSnapshot.cs` | Supplies stable run/step/trigger facts for later intent classification. |
| Pre-execution route decisions | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRoutePlanner.cs` | Names database, materialization, recovery, subprocess, workflow, and agent execution branches without side effects. |
| Start-transition request construction | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchStartTransitionPlanner.cs` | Separates transition request shape from transition execution. |
| Claim and heartbeat session | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchGuardLease.cs` and `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchLeaseHeartbeat.cs` | Keeps durable claim/lost-claim behavior explicit before any future orchestration extraction. |
| Finalizer context construction | `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.FinalizerContextFactory.cs` | Names route-specific completion evidence contexts without introducing a driver contract. |

## Dispatch Intent Taxonomy

These are documentation names only. They are not enum values, public API names,
tool names, package names, driver identifiers, or process contract changes.

| Dispatch intent | Current route source | Evidence family | Future driver relationship |
| --- | --- |
| `SoftwareBuildValidation` | Direct-agent route | Build logs, compiler diagnostics, package restore output | DotNet/Rust/Node helper drivers may eventually produce build evidence. |
| `SoftwareTestValidation` | Direct-agent route | Unit/integration test results, filtered test transcripts, coverage summaries | Test helper drivers may eventually produce or validate test evidence. |
| `BrowserRuntimeValidation` | Direct-agent route | Large-screen runtime screenshots, browser snapshots, console logs | Browser/Playwright helper drivers may eventually produce runtime visual evidence. This bundle still forbids small/medium/mobile proof artifacts. |
| `DocumentDeliverableValidation` | Direct-agent, workflow, or subprocess route | DOCX/PDF/source document output, rendered review evidence, extraction summaries | Office/PDF/document helpers may eventually produce deliverable evidence. |
| `SpreadsheetValidation` | Direct-agent, workflow, or subprocess route | XLSX/CSV/TSV workbooks, formulas, data quality checks | Spreadsheet helpers may eventually produce spreadsheet evidence. |
| `BusinessAnalysisReview` | Direct-agent or workflow route | Analysis summaries, assumptions, risks, recommendations, decision records | Business-analysis helpers may eventually produce analysis and review evidence. |
| `HumanApprovalOrReview` | Waiting approval, workflow, or manual review route | Approval state, reviewer disposition, exception rationale | Human approval tools may eventually satisfy review/decision evidence. |
| `ManagerArtifactRecovery` | Manager recovery route | Recovered artifact records, finalizer context, recovered-for execution lineage | Recovery helpers may eventually target missing or invalid artifacts without rerunning the full route. |
| `SubprocessCompletionProjection` | Subprocess route | Child process status, child artifacts, parent-step transition context | Subprocess helpers may eventually project child outputs into parent evidence. |
| `WorkflowCompletionProjection` | Workflow route | Workflow run link, workflow artifacts, mapped output expectations | Workflow helpers may eventually project workflow output evidence into process artifacts. |

## Explicit Non-Goals

- Do not implement these drivers in this bundle.
- Do not add Process Core projects, namespaces, packages, or contracts.
- Do not introduce production process-driver APIs, registries, packs, or public tool names.
- Do not move route side effects into helpers that are documented as pure decisions.
- Do not use browser or viewport proof to satisfy this documentation-only subbundle.

The goal is to keep dispatch route facts named so a later, separately approved
initiative can map route/evidence intent to available helper packs after the
runtime boundary is stable.
