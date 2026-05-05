# Validation And Closure

## Status

- `Completed`

## Objective

- Prove the shared-card and dialog-editor implementation, record browser analytics, close raw notes, and run final bundle validators.

## Covered Inputs

- N001 through N008 closure proof and residual-risk review.

## Prerequisites

- `subbundles/01-shared-agent-card-foundation` completed.
- `subbundles/02-agents-tab-dialog-editor` completed.

## Exact Source References

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\AgentChatModalTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\AiAgentsPageTests.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayout.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\AgentsHomePage.razor`

## Deliverables

- Focused test/build results recorded.
- Browser-validation analytics recorded for card grid and open dialog states, or explicit blocker documented.
- Raw-note closure table completed.
- Bundle README and execution report synchronized.
- Final `validate_bundle.py --stage completed` run.

## Dependency Impact

- This phase determines whether the implementation can be considered complete for the user request. Weak proof reopens subbundle 01 or 02.

## Validation Depth

- `End-to-end UI closure`

## Implementation Steps

1. Run focused component tests for `AgentChatModalTests` and `AiAgentsPageTests`.
2. Run a build or broader test command appropriate to the changed projects.
3. Start the local app if available and capture Playwright/browser proof for `/agents?tab=agents`.
4. Record command results, screenshots, browser analytics, and gate decisions.
5. Close each raw note as Solved, Partially solved, or Not solved.
6. Run prepared and completed bundle validators and repair any documentation gaps.

## Scope Exceptions

- If the app cannot start locally, record the exact blocker and keep browser proof as a residual validation gap.

## Do Not Do

- Do not close a raw note without proof.
- Do not treat component tests as a replacement for visual proof when browser proof is available.
- Do not leave subbundle statuses as `Ready` or `In progress`.

## Acceptance Checklist

- Focused tests pass or failures are documented with a blocker.
- Browser proof includes card grid and open dialog states.
- Execution report includes Subbundle Gate Results and Browser Validation Analytics rows.
- Raw Note Closure has no pending rows.
- Final bundle validator passes.

## Proof Required

- `dotnet test` command output for targeted component tests.
- `dotnet build` or equivalent solution/project build result.
- Screenshot paths or explicit blocker.
- Validator output for prepared and completed stages.

## Browser Validation Logging

- Route: `/agents?tab=agents`.
- Viewports: large desktop and narrower width.
- Actions: navigate, inspect cards, double-click card, open dialog, inspect Identity and Skills/MCP tabs.
- Screenshots: record actual paths in `reviews/01-execution-report.md`.
- Review questions: all text readable, no clipping/overlap, modal z-order correct, tabs usable, Summary/Instructions space correct, cards use page space intentionally.

## Progression Gate

- Final response may be sent only after validators pass or a real blocker is documented with exact residual risk.

## Suggested Agent Prompt

```text
Validate and close the completed bundle. Do not make new feature changes unless proof exposes a defect that reopens an earlier subbundle.
```
