# UI UX Projection Contracts And Reuse Plan

## Design Intent

The current Process UI/UX direction is useful and should be preserved. The backend data source must change: UI reads application/projection services only. It does not query EF runtime entities, inspect runtime state tables, or compute process truth from raw logs.

The concrete current UI/UX story inventory is recorded in `analysis/06-current-implementation-user-story-map.md`. Future UI subbundles must use that map as a coverage checklist and must record Playwright proof for the story groups they own.

## UI/UX Surfaces To Preserve

| Current surface | Evidence | Preserve | Target data source | Forbidden data source |
| --- | --- | --- | --- | --- |
| Live Processes dashboard | `repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor` | History window selector, refresh, run cards, stats, activity, agents, metrics, tool analytics, escalation cards. | `LiveProcessSnapshot` and recent event projections. | Runtime EF entities or old observation service. |
| Process workspace shell | `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor` and `.razor.cs` partials | Tabs, launch flows, runs view, steps, artifacts, manager chat, analytics, graphs. | Application services returning projection DTOs. | Direct runtime service mutation or DbContext. |
| Definition canvas | `repo://src/CanDoItAll.Modules.Processes/Canvas` | Node/port visual model, branch router rendering, artifact visual links, layout concepts. | `DefinitionCanvasProjection`. | Definition EF entities directly. |
| Runtime canvas | `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsCanvasSection.razor` | Step status rendering, subprocess boundaries, active/blocked/completed visual state. | `RuntimeCanvasProjection`. | Runtime state tables directly. |
| Template catalog/editor | `repo://src/CanDoItAll.Modules.Processes/Templates` and template UI components | Catalog browsing, role/step/artifact editing, Mermaid preview as generated output. | Template application services and Git-backed template projections. | Sidecar Markdown/Mermaid as canonical behavior. |
| Run detail/timeline/dialogs | `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessObservationService.cs` | Run detail, stage detail, timeline, dialogs, blocked incident details. | `RunDetailProjection`, `TimelineProjection`, `IncidentProjection`. | Query-built runtime truth. |
| Manager chat/incidents | `repo://src/CanDoItAll.Modules.Processes/Runtime/Observation/ProcessManagerChatService.cs` | User communication with manager, incident action UI, escalation cards. | Manager incident projections and authorized action services. | Raw diagnostics as plain UI messages. |
| Git/versioning UI | New target | Status, diff, commit, merge, conflict resolution. | `CanDoItAll.Components.Git` plus template/process metadata. | Process-specific Git command calls from components. |

## Target Projection Contracts

Projection families:

- `LiveProcessSnapshot`
- `LiveRunCardProjection`
- `LiveRunEventProjection`
- `RunDetailProjection`
- `StepDetailProjection`
- `ArtifactMapProjection`
- `IncidentProjection`
- `ManagerMessageProjection`
- `DefinitionCanvasProjection`
- `RuntimeCanvasProjection`
- `TemplateCatalogProjection`
- `TemplateConflictProjection`
- `GitChangeProjection`

Each projection includes:

- projection schema version,
- source event sequence or source content hash,
- observed-at UTC,
- freshness/projector lag,
- sensitivity,
- authorization flags,
- restricted evidence links rather than raw sensitive content.

## UI Rules

- UI never computes runtime truth.
- UI can request commands through application services, but runtime applies transitions.
- UI displays branch outcomes from typed branch projections.
- UI displays generated Mermaid/Markdown only as projections/exports.
- UI refresh reads projection storage through application services.
- Force refresh bypasses memory cache but not projection contract boundaries.

## Reuse Notes

- Canvas visual layout and node/port ideas are candidates to adapt.
- Current Live Processes composition is a UX reference, not a data-source design.
- Current observation model names can inspire projection DTOs if they do not carry runtime internals.
- Existing component tests are regression references and should be rewritten against projection contracts.

## Required Tests

- Component tests for projection-only rendering.
- Tests proving UI cannot reference Persistence or Runtime internals.
- Live/history tests for time-window semantics.
- Canvas tests for definition and runtime projection rendering.
- Template editor tests for global/local override conflict states.
- Git UI component tests for diff/conflict/status flows.
- Playwright smoke tests for Live Processes, process workspace, launch, run details, template conflict, and manager incident action.
- Story-specific Playwright proof in SB13 through SB27, including screenshots under each subbundle proof directory.

## Failure Behavior

| Failure | Required response |
| --- | --- |
| Projection field missing for UI | Add projection contract/projector support; do not query runtime internals. |
| Projection stale | Show freshness/lag and allow force refresh through projection services. |
| Restricted diagnostic requested | Show authorized restricted link or access-denied projection. |
| UI needs command result | Application command returns command receipt and projection refresh token. |

## Invariants

- UI references `Processes.Application`, `Processes.Projections`, and shared UI components only.
- UI does not reference EF runtime entities.
- UI does not call dispatcher/runtime internals.
- UI does not parse raw runtime logs to infer state.
- UI does not treat generated template projections as canonical source.
