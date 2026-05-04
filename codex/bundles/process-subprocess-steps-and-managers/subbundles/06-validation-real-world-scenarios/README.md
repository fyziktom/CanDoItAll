# validation real world scenarios

## Status

- `Completed`

## Objective

- Prove subprocesses work independently, inside parent processes, with manager reports, canvas editing, and templates.

## Covered Inputs

- Proper validation and real testing are mandatory.
- Validate random real-world small cases like existing simple apps in the main PostgreSQL database.
- Go atomically: prove subprocess first, then parent process.

## Prerequisites

- `subbundles/01-architecture-source-of-truth-and-schema`
- `subbundles/02-runtime-subprocess-orchestration`
- `subbundles/03-manager-control-plane-and-hr-override`
- `subbundles/04-canvas-and-editor-ui`
- `subbundles/05-default-software-development-subprocess-templates-and-agents`

## Exact Source References

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.Processes.Tests\ProcessesToolsTests.cs`
- `C:\repositories\CanDoItAll\Templates\Processes\manifest.json`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessRuntimeStateOverviewService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`

## Deliverables

- Unit/component/integration test results.
- Browser screenshots and review notes.
- Real scenario run notes for subprocess alone and parent-with-subprocess.
- Revalidation gate B decision.

## Dependency Impact

- This subbundle determines whether the bundle can close.

## Validation Depth

- `End-to-end regression and closure`

## Implementation Steps

1. Run targeted unit/component tests.
2. Run integration tests for subprocess and manager projections.
3. Run template loader/import tests.
4. Start the application and run browser proof for UI flows.
5. Validate a small real-world process scenario in PostgreSQL when local DB is available.
6. Record all commands, screenshots, and findings.

## Scope Exceptions

- If PostgreSQL is unavailable locally, record the exact blocker and substitute an integration database proof. Do not claim PostgreSQL proof without running it.

## Do Not Do

- Do not close on compile-only proof.
- Do not skip browser proof for the canvas/UI subbundle.

## Acceptance Checklist

- Subprocess template can run alone.
- Parent process can start and observe a child subprocess run.
- Manager report highlights child status/blockers.
- UI can create/change/open subprocess steps.
- Tests and screenshots are recorded.

## Proof Required

- Targeted `dotnet test` commands.
- Browser screenshot paths.
- Real scenario notes in `reviews/01-execution-report.md`.
- Revalidation gate B decision.

## Browser Validation Logging

- Target route or window: process workspace canvas and run operator console.
- Required viewport passes: maximized desktop and narrower follow-up for canvas.
- Required actions/assertions: subprocess creation/change/open, manager report review, parent run child status.
- Screenshot evidence: all screenshot paths in execution report.
- Review questions: Does the feature remain understandable when parent and child are both active? Are blockers surfaced without opening every child run?

## Progression Gate

- Continue only when subprocess-alone and parent-with-subprocess scenarios both pass.

## Suggested Agent Prompt

```text
Validate the completed subprocess feature end to end. Run tests, browser proof, and a small real process scenario. Record exact evidence and blockers.
```
