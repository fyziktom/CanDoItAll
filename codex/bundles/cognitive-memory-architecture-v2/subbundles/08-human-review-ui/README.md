# 08 Human Review UI

## Status

- Passed on 2026-05-16. Closure recorded in `checklists/cognitive-memory-implementation-control.xlsx` and `reviews/01-execution-report.md`.

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

## Implementation Summary

- Added `ICognitiveMemoryReviewUiService` and DTO contracts for the operator dashboard, memory explorer/detail, source links, review queue/detail, recall trace stages/candidates/source references, consolidation runs, projection health, replay jobs, procedure skills, and simulation review counts.
- Added `/cognitive-memory` and `/memory` routes using existing BaseLib `PageScaffold`, `PageHeader`, `Tabs`, `Grid`, `Stack`, `SurfaceCard`, `SummaryTile`, `StatusBadge`, `SelectionListItem`, `Button`, and `EmptyState` patterns.
- Added shell navigation entry `Cognitive Memory`.
- Wired supported V1 review decisions through the service: approve, reject, request changes, and defer. The service records actor id, notes, decision timestamp, concurrency token, and review status only; it does not mutate canonical memory truth directly.
- Added human-readable enum labels, provider-safe bounded reads, SQLite-safe client-side ordering for `DateTimeOffset` fields, and explicit provider-backed adapter failures when optional semantic/RAG drivers are resolved without registrations.
- Did not fake split/merge/reopen actions; those remain future backend-contract scope.

## Validation Evidence

- `dotnet build src\CanDoItAll.Web\CanDoItAll.Web.csproj --no-restore` passed with zero warnings.
- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter FullyQualifiedName~CognitiveMemoryReviewUiServiceTests` passed 2/2.
- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-restore --filter FullyQualifiedName~CognitiveMemoryPageTests` passed 1/1.
- `dotnet test tests\CanDoItAll.Tests.Playwright\CanDoItAll.Tests.Playwright.csproj --no-restore --filter FullyQualifiedName~CognitiveMemoryReviewUiPlaywrightTests` passed 1/1.
- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore --filter FullyQualifiedName~CognitiveMemory` passed 76/76.
- Static scans found no direct truth/upsert/final-score mutation, blocking async calls, TODOs, or unimplemented stubs in the review UI surface. Expected matches were limited to supported review decisions, service/test `SaveChangesAsync`, and Blazor injection defaults.

## Browser Evidence

- `.artifacts/playwright/cognitive-memory-review-ui/review-dashboard-desktop.png`
- `.artifacts/playwright/cognitive-memory-review-ui/memory-explorer-desktop.png`
- `.artifacts/playwright/cognitive-memory-review-ui/review-queue-desktop.png`
- `.artifacts/playwright/cognitive-memory-review-ui/trace-viewer-desktop.png`
- `.artifacts/playwright/cognitive-memory-review-ui/health-mobile.png`
- `.artifacts/playwright/cognitive-memory-review-ui/browser-plugin-route.png`
- `.artifacts/playwright/cognitive-memory-review-ui/browser-plugin-route-snapshot.md`

## Closure Notes

- The CanDoItAll components MCP transport was unavailable (`Transport closed`), so component selection used local BaseLib and existing page-source inspection as the fallback. Build, component tests, Playwright, and Browser plugin proof validate the fallback.
- `07-maf-workflow-integration` may start. Probing workbench, answer gate UI, Epistemic Drive UI, cross-project memory UI, and distributed compute remain blocked until their own gates.

## Progression Gate

- Proceed to closure only after review and trace evidence are visible to operators.

## Suggested Agent Prompt

- Implement the Cognitive Memory operator UI using existing component conventions and provide browser evidence for review, trace, and health workflows.
