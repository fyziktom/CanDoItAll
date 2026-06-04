# CanDoItAll.Modules.Processes

## Purpose

Canonical process runtime module for templates, process runs, step transitions, work briefs, governed outcomes, artifacts, and AI-agent dispatch.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj
```

## References

Project references:

- `../CanDoItAll.AgentFramework.Core/CanDoItAll.AgentFramework.Core.csproj`
- `../CanDoItAll.AgentFramework.Models/CanDoItAll.AgentFramework.Models.csproj`
- `../CanDoItAll.AgentFramework.Tooling/CanDoItAll.AgentFramework.Tooling.csproj`
- `../CanDoItAll.Components.BaseLib/CanDoItAll.Components.BaseLib.csproj`
- `../CanDoItAll.Components.CanvasLib/CanDoItAll.Components.CanvasLib.csproj`
- `../CanDoItAll.Components.Mermaid/CanDoItAll.Components.Mermaid.csproj`
- `../CanDoItAll.Components.WebGlLib/CanDoItAll.Components.WebGlLib.csproj`
- `../CanDoItAll.Infrastructure/CanDoItAll.Infrastructure.csproj`
- `../CanDoItAll.Modules.Activity/CanDoItAll.Modules.Activity.csproj`
- `../CanDoItAll.Modules.Collaboration/CanDoItAll.Modules.Collaboration.csproj`
- `../CanDoItAll.Modules.CrmHr/CanDoItAll.Modules.CrmHr.csproj`
- `../CanDoItAll.Modules.Projects/CanDoItAll.Modules.Projects.csproj`
- `../CanDoItAll.SharedKernel/CanDoItAll.SharedKernel.csproj`

Framework references:

- None

Direct package references:

- `JsonViewer.Blazor (0.0.6)`
- `Markdig (1.1.2)`
- `Microsoft.AspNetCore.Components.Web (10.0.5)`

## Architecture Notes

Processes owns the lifecycle above Workflows and Agent Framework execution. A process definition describes roles, step contracts, dependencies, artifact expectations, and governance rules. A process run owns step state, assignments, artifacts, manager directives, recovery, read models, and operator diagnostics. Workflows and agents execute underneath that boundary; they do not replace process lifecycle ownership.

The runtime is organized around these surfaces:

- **Definition and template authoring**: `ProcessesService`, definition child partials, `ProcessDefinitionLinter`, and the template catalog services load and validate process definitions, step operation contracts, artifact expectations, branch outcomes, and role requirements.
- **Launch planning and staffing**: `ProcessesService.Launch.*` builds launch plans, resolves candidate roles, applies process-manager overrides, and preserves project-structure launch context.
- **Run lifecycle and transitions**: `ProcessesService.Runtime.*`, `ProcessRuntimeProgressionPlanner`, `ProcessStepTransitionGuard`, and `ProcessStepRunBlockState` own run start, step transitions, stop/rerun, dependency compatibility, and block-state classification.
- **Dispatch and finalization**: `ProcessRunAutomationDispatchService.*` claims executable steps, builds execution metadata and prompts, validates tool outputs, projects artifacts, finalizes step completion, and creates recovery packets. External target grounding and stale path inspection are delegated to `ProcessExternalTargetGroundingService` so prompt rules, invocation metadata, and final-delivery validation consume the same typed normalization path.
- **Agent runtime tool provider**: `ProcessAgentRuntimeToolProvider` owns the 23 direct process tools exposed to MAF through `IAgentRuntimeToolProvider`. `ProcessesModuleServiceCollectionExtensions` registers it with DI. MAF composes it when the Processes module is loaded; MAF does not own process tool construction or reference this module directly. The provider is purpose-aware: read tools require process read access, mutation tools require explicit process write access, and no-access contexts expose no process tools.
- **Artifacts and lineage**: `ProcessArtifactIdentityService`, `ProcessArtifactProjectionLineage`, artifact projection/finalizer code, and runtime read queries own identity hash, content hash, lineage, projection source, trust status, and expectation satisfaction. Retention remains definition-driven through artifact expectation `RetentionDays`; cleanup must be explicit, dry-run first, and must preserve lineage metadata needed to explain stale, duplicate, or hash-mismatched evidence.
- **Observation and manager chat**: `ProcessObservationService`, `ProcessObservationDashboardState`, `ProcessManagerChatService`, and `ProcessManagerAgentResolver` expose run inspection, manager directives, chat, approvals, diagnostics, selected-run context, and explainable manager-agent resolution.
- **Project-structure integration**: `IProcessProjectStructureBridge`, `ProcessProjectStructureContext`, and the Workbench bridge synchronize process runs into project-structure nodes and feed grounded launch/output context back into Processes.
- **Operator UI**: `ProcessWorkspace*`, `LiveProcessesDashboard`, and run-detail components render authoring, launch, active runs, artifacts, assignments, execution telemetry, manager chat, and operator recovery surfaces.
- **Background runtime**: `ProcessCatalogWarmupWorker`, `ProcessRunRecoveryWorker`, and `ProcessOutboxDrainWorker` run only when the local runtime lane allows hosted workers.

Keep business behavior inside typed services and module contracts. MCP tools, Workbench bridges, templates, and UI components should call those services instead of duplicating runtime rules.

## Refactor Boundaries

The current source already has useful service boundaries, but several cross-cutting policies still deserve tighter ownership:

- **Artifact status projection** should be one typed mapping consumed by finalizer validation, runtime read models, health/recovery classification, API projection, and UI detail loaders.
- **Artifact identity and storage** should stay behind `ProcessArtifactIdentityService` and projection-lineage helpers, with race/recovery tests proving stale or invalid records cannot block later valid artifacts.
- **Output grounding** stays behind `ProcessExternalTargetGroundingService`; new consumers should use its typed target, scaffold, alias-pruning, stale-reference inspection, and prompt-redaction results instead of parsing external-target text locally.
- **Run folder projection** stays behind Workbench `ProjectStructureProcessRunFolderProjectionPolicy`, which projects current-run managed roots and generated or external-delivery output roots while collapsing or ignoring tool receipt internals and noisy child folders.
- **Manager resolution** stays behind `ProcessManagerAgentResolver`, which returns reason codes, confidence, candidate summaries, and ambiguity diagnostics while keeping configured and selected-run assignments ahead of fallback scoring.
- **Proof/test harness** should use named process-runtime slices instead of one broad timeout-prone filter.

Avoid introducing interfaces for trivial one-implementation helpers. Add an abstraction only when it protects a real module boundary, improves testability, or removes meaningful duplication.

## Runtime Invariants

- Processes own lifecycle, dependencies, artifacts, recovery, and closure.
- Workflows and agents execute process steps under Processes.
- Required artifacts cannot be treated as satisfied when validation reports missing, invalid, stale, wrong-producer, placeholder-only, unavailable, or hash-mismatched evidence.
- Artifact cleanup must not delete the only lineage record for a required artifact unless the owning expectation retention policy has expired and the operation records auditable cleanup evidence.
- Final delivery proof may reference an external target only when project-structure grounding produced a credible current-run target.
- Manager chat must prefer configured manager and selected-run assignment before fallback.
- Project-structure projection should expose navigable run/product folders without mirroring every artifact subdirectory.
- Live-run profiles must not seed transitions or artifacts that a real runtime path should produce.

## Operator Troubleshooting Map

Use current-run readbacks before changing state. Start with run detail and narrow to step-scoped routes only after identifying the failing step or artifact expectation.

| Symptom | Source-backed readback | Expected operator action |
| --- | --- | --- |
| Required output appears complete but the run is still blocked | Read run detail health, `invariantDiagnostics`, step `blockReasonCode`, `recoveryOptions`, and artifact status fields projected by `ProcessArtifactStatusProjectionService`. | Treat missing, invalid, stale, wrong-producer, placeholder-only, unavailable, or hash-mismatched records as unsatisfied. Record a new current-run artifact instead of editing status text. |
| Final delivery points to an external target or managed output folder | Inspect grounding metadata from `ProcessExternalTargetGroundingService`, including normalized target, scaffold, alias pruning, stale-reference inspection, and prompt-redacted paths. | Accept final delivery only when grounding found a credible current-run project-structure target or managed output root. Escalate ungrounded external aliases. |
| Manager chat answers the wrong run or agent | Inspect `ProcessManagerAgentResolver` reason code, confidence, selected-run assignment, configured manager, fallback candidates, and ambiguity diagnostics. | Prefer configured manager, then selected-run assignment, then fallback. Resolve ambiguity before sending a manager directive or direct message. |
| Project-structure output is too noisy or points to stale receipts | Inspect Workbench projection through `ProjectStructureProcessRunFolderProjectionPolicy`. | Project current-run managed roots and generated or external-delivery output roots; ignore wrong-run, dated receipt, traversal, absolute, and unanchored paths. |
| A live UI-driven run looks seeded | Read the selected `ProcessTemplateLiveRunProfile` or `ProcessTemplateLiveRunProfileSummary.FreshRunPolicy`. | Reject baseline transitions/artifacts as live proof. Require current-run evidence checks before validation and project-structure writeback. |
| Process tools are absent from an AgentFramework run | Inspect runtime DI for `IAgentRuntimeToolProvider`, confirm `ProcessAgentRuntimeToolProvider` is registered, check MAF progress for the provider key/display name and expected 23-tool attachment, and inspect receipt or trace `RuntimeToolProviderKey` when available. | Fix module/provider registration or agent process access metadata. Do not add a direct MAF reference to Processes. |

For API or agent-driven operation, prefer the HTTP `/api/processes` routes and the `candoitall-api-processes` skill. The old Processes MCP server is not the current control plane.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
- Process template pack: `Templates/Processes/README.md`
- Process API skill: `codex/skills/candoitall-api-processes/SKILL.md`
