# 03-centralize-node-kind-registry-and-lifecycle

## Status

- `Prepared for Codex execution`

## Objective

Replace fragmented kind semantics with a central registry and add explicit lifecycle history for node reclassification.

## Covered Inputs

- `PWA-003`
- `PWA-004`
- `R-002`
- `R-003`

## Prerequisites

- SB02 complete.
- Carrier and facet ownership model agreed.

## Exact Source References

- `/mnt/data/unpacked_current/CanDoItAll-canonical-model-refactor/src/CanDoItAll.SharedKernel/ProjectObjectContracts.cs`
- `/mnt/data/unpacked_current/CanDoItAll-canonical-model-refactor/src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs`
- `/mnt/data/unpacked_current/CanDoItAll-canonical-model-refactor/src/CanDoItAll.Modules.Workbench/ProjectStructureNodeEditor.cs`
- `/mnt/data/unpacked_current/CanDoItAll-canonical-model-refactor/src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs`
- `/mnt/data/unpacked_current/CanDoItAll-canonical-model-refactor/tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs`

## Evidence Focus

- `src/CanDoItAll.SharedKernel/ProjectObjectContracts.cs:3-44`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs:45-90`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs:123-159`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs:225-377`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureNodeEditor.cs:77-148`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureNodeEditor.cs:204-233`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureNodeEditor.cs:385-439`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs:944-975`
- `tests/CanDoItAll.Tests.Integration/ProjectWorkbenchServiceIntegrationTests.cs:949-1002`

## Deliverables

- Central node-kind registry/descriptors.
- Lifecycle history persistence for reclassification and subtype/family transitions.
- Registry-driven editor and canvas descriptors.
- Transition rules for note → task/decision/etc. and family-specific facet migration.

## Dependency Impact

- Directly unblocks the plugin wave because plugins can register kinds and descriptors instead of editing switches.
- Protects the brainstorming-to-structured-workflow story with explicit history.

## Validation Depth

- Unit tests for descriptor resolution.
- Integration tests for reclassification history.
- MCP regression tests for node creation/edit/reclassification.

## Implementation Steps

- Create registry contracts that describe kind key, family, allowed relations, facet owner, editor schema, palette/icon hints, and plugin ownership.
- Refactor canvas catalog and node editor to consume registry descriptors instead of hardcoded subtype maps.
- Introduce lifecycle-history persistence and write it from reclassification flows.
- Split in-place subtype changes from cross-family transitions, and archive old facet snapshots for the latter.

## Do Not Do

- Do not keep subtype strings as the hidden source of meaning while merely wrapping them in a registry facade.
- Do not destroy prior semantic state during reclassification.

## Acceptance Checklist

- [ ] A new internal kind or plugin kind can be registered without editing multiple switch statements.
- [ ] Reclassification persists a lifecycle-history event.
- [ ] Facet migration rules are explicit and tested.

## Proof Required

- Descriptor tests.
- Lifecycle-history integration tests.
- Updated architecture docs describing kind registry semantics.

## Browser Validation Logging

- Capture note-to-task/decision conversion if editor UX changes.

## Progression Gate

- SB04 depends on registry-driven extensibility being real, not nominal.

## Suggested Agent Prompt

Implement SB03 by creating a central node-kind registry and lifecycle-history model. Refactor canvas/editor code to consume descriptors, preserve the note-to-structured-node workflow, and make reclassification auditable with facet migration rules.
