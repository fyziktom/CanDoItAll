# P0-05 Avoid Full Surface Reloads After Simple Mutations

## Status
- Lifecycle status: `Ready`

## Objective
- Stop forcing the heavyweight structure reload path for simple non-structural node changes.

## Covered Inputs
- Audit hotspot about full surface reloads after status and note updates.
- Feature preservation items `F05`, `F10`, `F16`, `F17`, `F18`, `F24`, `F28`, and `F31`.

## Prerequisites
- `P0-04` completed with trusted batching proof.

## Exact Source References
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureGraphAdapter.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`

## Deliverables
- Clear distinction between structural graph reloads and simple local surface patching.
- Local patch path for note, status, marker, priority, or progress changes where safe.
- Structural flows still land in consistent graph state.

## Dependency Impact
- Later renderer work benefits if simple mutations stop invalidating the whole scene.
- This subbundle can reopen if create, delete, or link flows show stale graph state later.

## Validation Depth
- Targeted ProjectStructure tests around edit, note, status, and summary workflows.
- Browser proof for inline note editing and representative non-structural mutation flows.
- One structural smoke to confirm create, delete, or link still converge correctly.

## Implementation Steps
- Audit which mutations currently call the full reload path.
- Narrow those paths only where the graph shape does not change.
- Preserve structural invalidation for hierarchy and relationship changes.

## Do Not Do
- Do not silently classify structural mutations as patch-safe.
- Do not accept weaker consistency just to avoid a reload.

## Acceptance Checklist
- Status, progress, marker, and priority changes no longer force full structure reloads.
- Inline note edit no longer needs a full reload when only the note node changed.
- Create, delete, and link flows still end in consistent graph state.

## Proof Required
- Targeted ProjectStructure tests.
- Playwright proof for inline note and mutation flows.
- One downstream graph-consistency smoke.

## Browser Validation Logging
- Route: ProjectStructure structure route.
- Viewport: large-screen first.
- Record the mutation flow, screenshots, and whether a full reload still occurred.

## Progression Gate
- Do not start renderer-heavy work until simple-mutation invalidation is narrowed and graph consistency still holds.

## Suggested Agent Prompt
- Inspect the current mutation invalidation paths, then replace only the safe full-reload calls with local graph or surface patching while preserving structural consistency.
