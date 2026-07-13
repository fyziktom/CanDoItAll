# SB11 Pilot Architecture UX Cleanup Gate

## Status

- `Ready`

## Objective

- Review/refactor the pilot into the stable seam that broader stories may reuse, and issue the progression decision.

## Covered Inputs

- N009, N012-N017; R015-R016, R024-R040.

## Prerequisites

- SB10 Completed with full Behavioral proof; SB09 foundations remain trusted.

## Exact Source References

- `bundle://subbundles/10-project-files-search-browser-and-interaction-pilot/README.md`
- `bundle://plan/architecture-checkpoints.md`
- `bundle://reviews/01-execution-report.md`
- `C:\repositories\CanDoItAll.FileTools\docs\host-integration-security.md`

## Deliverables

- Strict architecture, component usage, lifecycle, accessibility, desktop UX, proof, dependency, and old-owner-growth review.
- Cleanup of duplication, page-local policy, weak types/names, untested lifecycle, raw wrappers/CSS, visual/scroll/overlay defects.
- Rerun service/component/host/Playwright proof and one extension smoke showing another project/source can use the seam without monolith edit.
- Review measured large-source counters, cancellation, rendered-state bounds, and direct interaction handoff; rerun scoped performance scan for changed hot paths.
- Unqualified broader-UI Pass or exact reopen list.

## Dependency Impact

- SB12-SB18 are blocked until Pass.

## Validation Depth

- Proof tier: `Behavioral`.
- Critical architecture/UX progression gate.

## Implementation Steps

1. Apply Checkpoint C to actual code/proof and inspect screenshots at original resolution.
2. Repair concrete architecture/lifecycle/component/UX/test/performance findings without unmeasured micro-optimization.
3. Rerun the pilot positive/negative and desktop evidence.
4. Run C# architecture gate and record progression.

## C# Architecture Impact

- Cleanup/refactor only; no broader story.

## Boundary Ownership

- Confirms scope/coordinator/component/effect separation.

## Dependency Direction

- No new edge; refresh scoped graph if references changed during repair.

## Pattern Decision

- Validate PSR-05; remove facade/helper abstractions that merely rename page logic.

## Testability Contract

- Pilot behavior remains directly testable without `ProjectsPage`; browser proves production wiring.

## Partial Class Policy

- No new partial; parent page growth is measured and must remain thin.

## Architecture Proof Required

- Checkpoint C and C# gate Pass, owner/responsibility table, Components evidence, screenshot review, dependent extension smoke.

## Scope Exceptions

- No portfolio/card/canvas/process/resource/edit implementation.

## Do Not Do

- Do not accept “looks fine,” defer required cleanup, or add new stories while repairing.

## Acceptance Checklist

- [ ] Architecture/UX gate unqualified Pass.
- [ ] Real pilot positive/negative proof remains green.
- [ ] Parent owners remain thin and seam is reusable.
- [ ] Components, scroll, overlay, desktop visual and console proof pass.
- [ ] Accepted performance envelope and anti-pattern scan pass; direct known-file handoff remains browser-independent.
- [ ] SB12 unlock explicit.

## Proof Required

- Behavioral review record, C# gate, rerun commands, DOM/screenshots/review, dependency/source assertions, extension smoke.

## Browser Validation Logging

- Reuse SB10 route/page/viewports and rerun every affected interaction; capture replacement screenshots only when output changed, but inspect all accepted images.

## Progression Gate

- Only unqualified Pass unlocks SB12.

## Reopen Triggers

- A later story exposing pilot seam, lifecycle, component, authority, or UX weakness reopens SB10/SB11 and requires downstream revalidation.

## Suggested Agent Prompt

```text
Review and clean the project-files pilot only. Inspect actual code, tests, Components choices, browser state, and screenshots. Repair concrete defects, prove the reusable seam, and issue an unqualified Pass or reopen the exact owner.
```
