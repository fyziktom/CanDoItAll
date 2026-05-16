# 08 Human Review UI

## Status

- Ready after recall and consolidation traces exist.

## Execution Control

- Before editing code, update `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\checklists\cognitive-memory-implementation-control.xlsx`.
- Mark this subbundle `In Progress`, verify prerequisite rows are `Passed`, and record target branch/commit.
- During implementation, update owned checklist rows and proof paths.
- Before closure, update workbook `Phase Gates`, `Phase Acceptance Checklist`, `Validation Evidence`, `Handoff Log`, and `reviews/01-execution-report.md`.
- If evidence is missing or an upstream assumption fails, mark the subbundle `Blocked` and stop downstream work.
## Objective
- Provide operator UI for memory review, source evidence, recall traces, consolidation health, projection health, and memory detail inspection.

## Covered Inputs

- Requirements FR-016, FR-020, NFR-007, NFR-008, and NFR-011.
- UI/operator experience architecture.

## Prerequisites

- `05-recall-orchestrator` must provide traces.
- `06-consolidation-engine` must create review items.
- `14-neuro-foundation-claim-evidence-ledger` must provide claim/evidence/context/review targets.
- `17-temporal-replay-scheduler` and `18-procedural-skill-memory-simulation` should exist when review queues expose replay or procedure skill decisions.
- Existing UI component conventions must be followed.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\CanDoItAll.Components.BaseLib.csproj
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\11-ui-and-operator-experience.md
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj

## Deliverables

- Memory explorer page.
- Review queue page.
- Recall trace viewer.
- Consolidation and projection health views.
- Component/browser validation evidence.

## Dependency Impact

- UI consumes application services only.
- Review decisions mutate memory through service methods with policy and trace.
- Components should fit existing BaseLib/Radzen patterns if present in the host project.

## Validation Depth

- Component tests for rendering and state transitions.
- Playwright evidence for review, trace, and health routes.
- Accessibility and responsive checks for dense operational pages.

## Implementation Steps

- Add review list/detail views.
- Add recall trace and source evidence panels.
- Add consolidation/projection health views.
- Wire accept/reject/defer review actions through services.

## Do Not Do

- Do not build a marketing or landing page.
- Do not put domain logic in Blazor components.
- Do not hide evidence behind generated summaries only.

## Acceptance Checklist

- Operators can see why memory was recalled or flagged.
- Review actions are explicit and auditable.
- UI shows unavailable projection or failed consolidation state clearly.

## Proof Required

- Component test results.
- Playwright screenshots for desktop and mobile/dense viewport where relevant.
- Review action persistence evidence.

## Browser Validation Logging

- Record route, viewport, screenshot path, and result in `reviews/01-execution-report.md`.
- Include at least review queue, trace viewer, and projection/consolidation health routes.

## Progression Gate

- Proceed to closure only after review and trace evidence are visible to operators.

## Suggested Agent Prompt

- Implement the Cognitive Memory operator UI using existing component conventions and provide browser evidence for review, trace, and health workflows.
