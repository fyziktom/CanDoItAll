# 03 - Validation, browser proof, and post-fix architecture backcheck

## Status

- `Completed`

## Objective

- Prove the repaired canonical seam still works in the browser, then rerun the integrated architecture-review skillset to confirm the split-source-of-truth risk is materially reduced.

## Covered Inputs

- `RQ-06 Test Coverage`
- `RQ-07 Bundle Closure`

## Prerequisites

- `01-canonical-node-assignment-owner-and-editor-read-path` must be completed or honestly blocked before this subbundle starts.
- `02-node-lifecycle-reconciliation-and-canonical-guardrails` must be completed or honestly blocked before this subbundle starts.

## Exact Source References

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePartyPickerTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectPartyAssignmentIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectWorkbenchServiceIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\ProjectPartyAssignmentFlowTests.cs`
- `C:\repositories\CanDoItAll\codex\skills\architecture-reviews\`
- `C:\repositories\CanDoItAll\.codex\agents\`
- `C:\repositories\CanDoItAll\architecture\reviews\2026-04-04-canonical-model-review-canonical-model-refactor-branch-after-crm-hr-workbench-canonical-invariant-changes\`

## Deliverables

- Final build and targeted test proof for the repaired seam.
- Playwright browser proof with screenshots for the affected project/structure/assignments routes.
- A post-fix architecture review update that records what risks remain and what was actually solved.
- Completed bundle execution report and validator closure.

## Dependency Impact

- No downstream subbundles remain. This phase decides whether the bundle can close honestly.

## Validation Depth

- End-to-end regression and closure

## Implementation Steps

1. Run the targeted build and test slices.
2. Attempt browser validation with Playwright MCP and capture screenshots.
3. If MCP is blocked by the environment, record the blocker explicitly and use the narrowest honest fallback available.
4. Re-run the integrated architecture-review skillset against the repaired code.
5. Update the execution report and run completed-stage bundle validation.

## Scope Exceptions

- This phase does not reopen earlier subbundles unless proof shows the repair is incomplete.

## Do Not Do

- Do not claim closure if browser proof is missing for user-visible behavior without an explicit blocker and fallback explanation.
- Do not suppress remaining structural concerns from the post-fix review.

## Acceptance Checklist

- Targeted build and test slices pass.
- Browser proof confirms the main assignment/editor flows still work.
- The post-fix architecture review no longer reports the critical dual-write issue as unresolved.
- Bundle completion is backed by completed-stage validator output.

## Proof Required

- `python scripts/validate_bundle.py <bundle-root> --stage prepared`
- relevant `dotnet build`
- relevant `dotnet test` slices
- Playwright route proof and screenshots
- `python scripts/validate_bundle.py <bundle-root> --stage completed`

## Browser Validation Logging

- Target routes: `/crm-hr/assignments`, `/projects`, `/projects/{ProjectId}/structure`, `/projects/{ProjectId}/calendar`.
- Required viewport passes: `1600x1000`, then narrower follow-up on structure if layout changed.
- Required Playwright MCP evidence: save project-level assignments, open project card, verify structure-page participant/meeting/work-item flows, and smoke the calendar route.
- Expected screenshot location: `C:\repositories\CanDoItAll\output\playwright\canonical-bundle-v3\` unless a more precise path is created during execution.
- Screenshot review questions: do the structure-page editor and supporting routes still render without stale state, overlap, clipping, or error banners after the canonical repair?

## Progression Gate

- The bundle may close only after completed-stage validator output passes and the post-fix architecture review is recorded in the execution report.

## Suggested Agent Prompt

```text
Implement this subbundle only. Validate the repaired canonical seam with targeted build/test slices, browser proof, and a post-fix architecture review, then close the bundle honestly.
```
