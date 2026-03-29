# P3-02 Optional Shared-Library Consolidation

## Status
- Lifecycle status: `Ready`

## Objective
- Retire or intentionally isolate duplicated canvas component trees after the main fixes are stable.

## Covered Inputs
- Audit recommendation to delay consolidation until the main path is proven.
- Feature preservation items `F33` and `F34`.

## Prerequisites
- `P2-01` completed with trusted shared-canvas modularization proof.

## Exact Source References
- `C:\repositories\CanDoItAll\src\CanDoItAll.ComponentKit`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib`

## Deliverables
- Consumer inventory and canonical-library decision.
- Consolidation or explicit isolation plan grounded in current usage.
- Shared-canvas behavior preserved across active consumers.

## Dependency Impact
- Optional strategic cleanup.
- Can affect multiple consumers, so broad proof is required before closure.

## Validation Depth
- Consumer inventory review.
- Browser smoke for ProjectStructure, PromptFactory, and any remaining consumer affected by the consolidation.
- Build or test proof across touched consumers.

## Implementation Steps
- Inventory actual consumers first.
- Remove or isolate duplication only after the inventory and ownership decision are explicit.
- Keep the canonical path obvious in code and documentation.

## Do Not Do
- Do not start this cleanup early.
- Do not merge two trees blindly without consumer proof.

## Acceptance Checklist
- There is one clearly canonical shared canvas implementation path or an explicitly documented reason for temporary duality.

## Proof Required
- Consumer inventory evidence.
- Shared-surface browser smoke.
- Build or test proof for touched consumers.

## Browser Validation Logging
- Route: ProjectStructure route and `/prompt-factory`, plus any additional affected consumer.
- Viewport: large-screen first.
- Record browser smoke, screenshots if useful, and result in `reviews/01-execution-report.md`.

## Progression Gate
- No mandatory downstream work depends on this task. Close it only when the ownership decision is explicit and the affected consumers are proven.

## Suggested Agent Prompt
- Inventory current consumers of CanvasLib and ComponentKit first, then consolidate or intentionally isolate the duplicate trees only if the canonical ownership path can be proven across affected browser surfaces.
