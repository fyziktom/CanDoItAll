# Operator Audit Surface

## Status

- `Completed`

## Objective

- Expose mutation command, claim/evidence, projection failure, and retention cleanup audit signals through the operator snapshot/UI.

## Covered Inputs

- CM-P1-004
- CM-P1-003
- CM-P1-007

## Prerequisites

- Provider failure and retention cleanup subbundles passed or their output states are available.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\ReviewUi\CognitiveMemoryReviewUiContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\ReviewUi\CognitiveMemoryReviewUiService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\ReviewUi\CognitiveMemoryReviewUiTraceHealthQueries.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\Components\CognitiveMemoryHealthTab.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\Components\CognitiveMemoryReviewQueueTab.razor
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\CognitiveMemoryReviewUiServiceTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CognitiveMemoryPageTests.cs

## Deliverables

- Review UI DTOs for audit events.
- Query logic for recent mutation/projection/cleanup audit signals.
- Rendered Blazor section using existing component library patterns.
- Tests and browser proof.

## Dependency Impact

- Docs closure depends on accurate audit diagrams and screenshots.

## Validation Depth

- User-facing UI gate.

## Implementation Steps

1. Add audit DTOs and service query.
2. Render the audit surface in an existing tab or focused child component.
3. Add unit/component proof.
4. Capture browser proof.

## Do Not Do

- Do not query EF from Razor components.
- Do not add decorative cards or marketing layout.
- Do not expose sensitive payloads in audit summaries.

## Acceptance Checklist

- Mutation command/audit events are visible.
- Projection failures are visible.
- Claim/evidence change signals are visible.
- Retention cleanup activity is visible if cleanup execution exists.
- Mobile and desktop render without horizontal overflow.

## Proof Required

- Review UI unit tests.
- Component test if markup changes.
- Browser screenshots for `/cognitive-memory`.

## Proof Captured

- `CognitiveMemoryReviewUiSnapshot.OperatorAudit` exposes typed audit kind, subject kind, and status values for mutation commands/events, claim state, evidence anchors, projection failures, and retention cleanup runs.
- Health tab renders the operator audit surface through existing component wrappers without exposing raw payload JSON.
- Review UI unit tests passed, component Cognitive Memory test passed 1/1, and browser screenshots were captured for desktop and mobile.

## Browser Validation Logging

- Record route, viewport, selectors/assertions, screenshots, and console errors in `reviews/01-execution-report.md`.

## Progression Gate

- Continue only after audit data renders through the operator UI and proof is captured.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Add operator audit DTO/query/rendering through existing component patterns, avoid sensitive payloads, run targeted unit/component/browser proof, and update the execution report.
```
