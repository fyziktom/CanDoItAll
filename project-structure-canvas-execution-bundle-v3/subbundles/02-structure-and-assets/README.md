# 02 Structure And Assets

## Status

- Status: `Completed`
- Legacy task coverage: `T06-T09`

## Objective

Keep CanvasLib maintainable by centralizing asset includes, preserving the split runtime structure, and documenting remaining compatibility boundaries.

## Covered Inputs

- `R05`
- `R07`

## Prerequisites

- `01-foundation-and-toolbox` is completed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\App.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\App.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Shared\Assets\CanvasLibHeadAssets.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Shared\Assets\CanvasLibBodyAssets.razor`

## Deliverables

- Shared include components consumed by the web and sandbox shells.
- Updated documentation that distinguishes active runtime paths from compatibility surfaces.

## Dependency Impact

- Prevents shell drift and keeps later renderer validation aligned across all consumers.

## Validation Depth

- Asset generation and asset verification.
- Shared shell source audit.

## Implementation Steps

- Generate and consume shared CanvasLib asset include components.
- Keep preview and calendar asset loading explicit through component parameters.
- Document structure and compatibility boundaries in the bundle closure material.

## Do Not Do

- Do not restore manual per-shell script lists.

## Acceptance Checklist

- Asset verification passes.
- Web and sandbox shells both consume the generated include components.
- Runtime and compatibility boundaries are documented.

## Proof Required

- `npm run canvaslib:build-assets`
- `npm run canvaslib:verify-assets`

## Browser Validation Logging

- Route: shared shell bootstrap for `/projects/{id}/structure`, `/prompt-factory`, and `/groups/canvas/benchmark`
- Evidence: browser routes load through the same generated runtime asset graph after verification.

## Progression Gate

- Passed because the generated asset graph is deterministic and both app shells load CanvasLib through the same include components.

## Suggested Agent Prompt

Confirm that CanvasLib assets are generated from source, consumed through shared include components, and not duplicated across shells before final closure proceeds.
