# P3-01 Optional True-Canvas Renderer Spike

## Status
- Lifecycle status: `Ready`

## Objective
- Benchmark a true HTML5 canvas prototype only after the retained DOM-SVG path is stable and measured.

## Covered Inputs
- Audit recommendation to treat true canvas as a measured spike, not the first implementation move.
- Feature preservation items `F21` and `F31`.

## Prerequisites
- `P1-01` completed with trusted retained-renderer proof.
- `P1-02` completed with trusted culling proof.
- `P1-03` completed with trusted drag-loop proof.
- `P2-01` completed with trusted modularization proof.
- `P2-02` completed with trusted browser-regression proof.

## Exact Source References
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright`

## Deliverables
- Isolated benchmark or prototype for a true-canvas path.
- Measured comparison against the retained DOM-SVG baseline.
- Go or no-go decision documented from evidence.

## Dependency Impact
- Optional strategic work only.
- If started, it must not destabilize the already-proven retained renderer path.

## Validation Depth
- Performance comparison evidence.
- Browser proof that the spike remains isolated and does not break shipped behavior.
- Architectural review of trade-offs for export, accessibility, and overlays.

## Implementation Steps
- Build the narrowest possible prototype or benchmark harness.
- Compare retained and true-canvas behavior on equivalent graph tiers.
- Document whether the spike is worth pursuing.

## Do Not Do
- Do not replace the shipped renderer path without benchmark-backed approval.
- Do not let the spike erode existing browser proof.

## Acceptance Checklist
- A go or no-go decision is backed by measured evidence, not intuition.

## Proof Required
- Performance evidence comparing retained and true-canvas paths.
- Browser smoke proving shipped behavior still works.
- Written trade-off summary tied to the measurement.

## Browser Validation Logging
- Route: benchmark or isolated prototype route plus ProjectStructure smoke.
- Viewport: large-screen first.
- Record evidence paths, screenshots if applicable, and result in `reviews/01-execution-report.md`.

## Progression Gate
- This subbundle does not unlock mandatory downstream work. Close it only with an honest go or no-go recommendation.

## Suggested Agent Prompt
- Build only a narrow, isolated true-canvas benchmark and compare it against the retained renderer using the same graph tiers and browser proof, then document a go or no-go recommendation.
