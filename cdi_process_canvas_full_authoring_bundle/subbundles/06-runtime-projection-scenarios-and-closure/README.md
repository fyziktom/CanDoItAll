# 06-runtime-projection-scenarios-and-closure

## Status

- `Ready`

## Objective

- Validate the completed feature set on realistic software-development scenarios, bring runtime projection into the agreed readable parity, and close the initiative with real browser proof, raw-note closure, and final bundle sync.

## Covered Inputs

- `R005` Move toward canvas-primary authoring.
- `R009` Runtime nodes must project enough authored semantics to stay legible.
- `R018` Every authored relation and move must round-trip through save and reload.
- `R021` Validate on realistic software-development scenarios.
- `R022` Include review, QA, approval, and rework flows.
- `R023` Use Playwright proof with screenshot review.
- `R024` Final closure must state what is still form-only, if anything.

## Prerequisites

- `subbundles/01-node-inventory-and-port-semantics` must be `Completed` and trusted.
- `subbundles/02-canonical-port-model-and-persistence-foundation` must be `Completed` and trusted.
- `subbundles/03-shared-step-node-multi-port-rendering-and-gesture-parity` must be `Completed` and trusted.
- `subbundles/04-role-participation-authoring-via-canvas` must be `Completed` and trusted.
- `subbundles/05-step-contract-artifact-and-routing-authoring` must be `Completed` and trusted.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasSurfaceFactory.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDevelopmentSeedService.Scenarios.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration`
- `C:\repositories\CanDoItAll\cdi_process_canvas_full_authoring_bundle\inventories\02-target-scenarios.md`
- `C:\repositories\CanDoItAll\cdi_process_canvas_full_authoring_bundle\reviews\01-execution-report.md`

## Deliverables

- Updated seeded scenarios that exercise generalized canvas authoring.
- Runtime-node projection updates matching the agreed readable parity.
- Final test runs, Playwright walkthroughs, screenshots, and raw-note closure.
- Final bundle sync and validator closure.

## Dependency Impact

- This is the final proof phase.
- Weak proof here would leave the initiative with technical implementation but no trustworthy evidence that the process canvas is now materially primary.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Update or extend the seeded software-development scenarios so they exercise the new canvas-authorable relationships.
2. Bring runtime projection into the agreed readable parity with the definition graph.
3. Run the focused test suites and any broader confirmation runs needed for the affected projects.
4. Walk the seeded scenarios on `/processes` with Playwright, capture screenshots, and review them.
5. Close the raw request note by note in `reviews/01-execution-report.md`.
6. Rerun the bundle validator at the completed stage and synchronize the bundle docs with reality.

## Scope Exceptions

- Any remaining form-only editing surface must be listed explicitly in the final raw-note closure and residual-risk sections.

## Do Not Do

- Do not rely on only unit tests for closure.
- Do not call the canvas primary if ordinary scenario authoring still depends on hidden form-only graph editing.
- Do not leave the bundle docs out of sync with the shipped behavior and proof.

## Acceptance Checklist

- At least one seeded scenario proves role participation, step dependencies, branch routing, and any in-scope artifact links from the canvas.
- Runtime projection remains readable and semantically aligned with the authored definition graph.
- Browser proof and screenshots are recorded in the execution report.
- Raw notes are closed one by one with honest status.
- Final validator passes.

## Proof Required

- Final focused test commands and any broader confirmation run used for closure.
- Playwright walkthrough on `/processes` across the seeded scenarios.
- Large-screen screenshots and any narrower-width follow-up where layout changed.
- Updated `reviews/01-execution-report.md` with browser analytics, gate results, and raw-note closure.
- Completed-stage bundle validator pass.

## Browser Validation Logging

- Route: `/processes`
- Viewport: `Maximized desktop`, plus narrower follow-up where affected
- Playwright MCP actions: `open seeded scenarios`, `author or inspect representative links`, `reload`, `switch to runtime projection where applicable`, `capture screenshots`
- Screenshot evidence: `proof/screenshots/runtime-scenario-proof.png` and scenario-specific screenshots as needed
- Review questions: `Can the full authored process be understood from the canvas`, `Are loops and joins legible`, `Does runtime projection preserve authored meaning`, `Is anything still obviously form-only`

## Progression Gate

- This is the final phase. The initiative may close only after tests pass, browser proof is captured, raw-note closure is honest, and the completed-stage validator passes.

## Suggested Agent Prompt

```text
Implement only subbundle 06 from C:\repositories\CanDoItAll\cdi_process_canvas_full_authoring_bundle. Update the seeded software-development scenarios, bring runtime projection into readable parity, run the required tests, prove the scenarios on /processes with Playwright and screenshots, close the raw notes honestly, and synchronize the bundle before final validator closure.
```
