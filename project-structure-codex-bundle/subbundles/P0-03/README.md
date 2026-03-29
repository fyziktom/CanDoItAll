# P0-03 Commit-Only Canvas State Persistence And UI-State Ownership Cleanup

## Status
- Lifecycle status: `Ready`

## Objective
- Keep pan, zoom, and other live viewport interaction state local to JS and persist only idle or committed snapshots.

## Covered Inputs
- Audit hotspot about InteractiveServer and DB chatter during active viewport interaction.
- Feature preservation items `F01`, `F08`, `F26`, and `F30`.

## Prerequisites
- `P0-01` completed with trusted browser proof.

## Exact Source References
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\canvasWorkbenchInterop.js`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\CanvasWorkbench.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\CanvasWorkbenchContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanvasWorkbenchTests.cs`

## Deliverables
- Clear separation between transient browser-owned viewport state and persisted committed UI state.
- Fewer state publications during active pan and zoom.
- No duplicated persistence ownership between domain node positions and long-lived UI manual positions.

## Dependency Impact
- Critical foundation for `P0-04` and `P1-01` because later batching and renderer proof is weak if live state still churns through the server.
- Shared-canvas path but primarily validated through ProjectStructure behavior.

## Validation Depth
- Component tests for workbench state publication and shared chrome.
- Browser proof for pan, zoom, refresh persistence, and selection sync.
- Persistence-path or counter evidence that active interaction no longer saves repeatedly.

## Implementation Steps
- Inspect `OnStateChanged` and JS publication cadence.
- Keep selection and overlay sync only as frequent as needed.
- Move persistence to idle or explicit commit boundaries.

## Do Not Do
- Do not change structural graph mutation flows here.
- Do not keep both domain and UI sources of truth for the same committed geometry.

## Acceptance Checklist
- No `SaveViewStateAsync` during active pan or zoom.
- No `RefreshCanvasSurface()` triggered by pure viewport movement.
- ProjectStructure drag no longer persists both domain X/Y and long-lived UI manual positions.

## Proof Required
- Targeted `CanvasWorkbench` tests.
- Playwright proof for viewport interaction and refresh restoration.
- Counter or log evidence tied to the persistence path.

## Browser Validation Logging
- Route: ProjectStructure structure route.
- Viewport: large-screen plus one refresh pass on the same route.
- Log the interaction steps, screenshots, and observed state behavior in `reviews/01-execution-report.md`.

## Progression Gate
- Do not start `P0-04` or `P1-01` until active viewport interaction is proven local and committed persistence still restores correctly.

## Suggested Agent Prompt
- Validate current viewport persistence behavior, then reduce publication to commit boundaries without regressing selection sync, minimap, diagnostics, or restored view state.
