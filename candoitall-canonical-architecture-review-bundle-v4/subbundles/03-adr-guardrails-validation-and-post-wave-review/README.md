# 03 - ADR guardrails, validation, and post-wave review

## Status

- `Completed`

## Objective

- Record explicit architecture guardrails for the remaining structural debt, validate the shipped next-wave changes, and rerun the canonical-model review skillset against the result.

## Covered Inputs

- `RQ-05`
- `RQ-06`

## Prerequisites

- `01-workbench-lifecycle-compensation-and-typed-node-reference` must be completed or honestly blocked before this phase starts.
- `02-projection-only-party-metadata-and-display-guardrails` must be completed or honestly blocked before this phase starts.

## Exact Source References

- `C:\repositories\CanDoItAll\codex\skills\architecture-reviews`
- `C:\repositories\CanDoItAll\.codex\agents`
- `C:\repositories\CanDoItAll\architecture\reviews\2026-04-04-canonical-model-review-post-fix-node-scoped-assignment-canonicalization-and-lifecycle-reconciliation\report.md`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePartyPickerTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectWorkbenchServiceIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\ProjectPartyAssignmentFlowTests.cs`

## Deliverables

- ADRs for canonical ownership, projection-only metadata, typed node-reference direction, and Workbench-node extension guardrails
- Targeted validation proof and browser analytics
- A post-wave canonical-model review report and scorecard
- Completed bundle closure

## Dependency Impact

- No downstream subbundles remain.

## Validation Depth

- End-to-end closure

## Implementation Steps

1. Write the ADRs that should constrain future feature work.
2. Run the targeted build and test slices.
3. Attempt Playwright MCP proof and document the blocker if it persists.
4. Refresh screenshots or browser test evidence for the affected routes.
5. Rerun the architecture review skillset and update the execution report.
6. Run completed-stage bundle validation.

## Do Not Do

- Do not hide unresolved structural debt behind a falsely high stability score.
- Do not call the next-wave complete without updated review artifacts and bundle closure.

## Acceptance Checklist

- ADR guardrails exist and match the shipped code.
- Targeted build and tests pass.
- Browser proof is recorded honestly, with blocker plus fallback if MCP still fails.
- The post-wave review shows reduced risk relative to the prior scorecard.
- The bundle closes with completed-stage validator output.

## Proof Required

- `python scripts/validate_bundle.py <bundle-root> --stage prepared`
- relevant `dotnet build`
- relevant `dotnet test` slices
- Playwright proof or explicit blocker plus fallback evidence
- `python scripts/validate_bundle.py <bundle-root> --stage completed`

## Browser Validation Logging

- Target routes: `/projects/{ProjectId}/structure`, `/projects`, `/crm-hr/assignments`
- Required viewport passes: `1600x1000`, then narrower structure follow-up if needed
- Required Playwright MCP evidence: structure flow save and summary visibility checks
- Expected screenshot location: `C:\repositories\CanDoItAll\output\playwright\canonical-bundle-v4\` or refreshed evidence paths recorded in the execution report

## Progression Gate

- The bundle may close only after updated review artifacts and completed-stage validation are recorded.

## Suggested Agent Prompt

```text
Implement this subbundle only. Add the architecture guardrails, validate the shipped next-wave changes, rerun the canonical-model review, and close the bundle honestly.
```
