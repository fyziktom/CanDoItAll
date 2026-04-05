# 03-centralize-node-kind-registry-lifecycle-and-role-capabilities

## Status

- `Prepared for Codex execution`

## Objective

Turn kind semantics, reclassification, and node-role capability rules into explicit governed contracts.

## Covered Inputs

- `PW6-003`
- `PW6-004`
- `PW6-009`

## Prerequisites

- SB02 complete.
- Decide the initial kind families and transition categories (note, task/work-item, decision, participant, connector, asset, etc.).

## Exact Source References

- `/mnt/data/unpacked_phase5_current/CanDoItAll-canonical-model-refactor/src/CanDoItAll.SharedKernel/ProjectObjectContracts.cs`
- `/mnt/data/unpacked_phase5_current/CanDoItAll-canonical-model-refactor/src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs`
- `/mnt/data/unpacked_phase5_current/CanDoItAll-canonical-model-refactor/src/CanDoItAll.Modules.Workbench/ProjectStructureNodeEditor.cs`
- `/mnt/data/unpacked_phase5_current/CanDoItAll-canonical-model-refactor/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `/mnt/data/unpacked_phase5_current/CanDoItAll-canonical-model-refactor/src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs`

## Evidence Focus

- `src/CanDoItAll.SharedKernel/ProjectObjectContracts.cs:3-44`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs:45-120; 225-377`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureNodeEditor.cs:45-180; 385-439`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:944-975`
- `src/CanDoItAll.Modules.CrmHr/CrmHrServices.cs:4888-4933`

## Deliverables

- Central node-kind registry and descriptors.
- Transition history plus facet migration/archival rules.
- Capability matrix defining allowed relations, commands, and assignment roles per kind/family.

## Dependency Impact

- Creates the semantic base for plugin hooks, MCP, and CRM/HR assignment rules.
- Preserves brainstorming-first workflow while making note-to-task evolution first-class.

## Validation Depth

- Registry contract tests.
- Reclassification tests with history and facet supersession.
- Role-to-kind capability tests for allowed and forbidden cases.

## Implementation Steps

- Introduce kind descriptors and replace scattered switch logic incrementally.
- Add node transition history and facet lifecycle contracts.
- Refactor reclassification to write history and use facet migration rather than raw in-place semantic mutation only.
- Drive node-scoped assignment validation from registry capabilities.

## Do Not Do

- Do not keep subtype strings as the hidden real kind system.
- Do not solve role validation by adding more ad-hoc special cases.

## Acceptance Checklist

- [ ] A new kind can be registered without editing multiple scattered switch blocks.
- [ ] Note-to-task or note-to-decision writes a durable transition history row.
- [ ] Node-scoped role validation is descriptor-driven.

## Proof Required

- Registry tests and examples.
- Lifecycle-history tests.
- Updated architecture notes showing allowed role matrix per kind/family.

## Browser Validation Logging

- Capture at least one note-to-task and note-to-decision flow if editor behavior changes.

## Progression Gate

- Do not start SB04 or SB05 until the registry and lifecycle contracts are trusted.

## Suggested Agent Prompt

Implement SB03 as a governed semantics layer. Keep node identity stable, add explicit lifecycle history, centralize kind descriptors, and drive node-role capabilities from the registry instead of scattered switches.
