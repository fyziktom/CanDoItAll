# Validation And Browser Proof

## Status

- Status: `Completed`

## Objective

- Prove the contextual floating agent windows work end to end with builds, tests, Playwright MCP, screenshots, and final bundle closure.

## Covered Inputs

- All raw notes, especially the explicit Playwright MCP and screenshot requirement.

## Prerequisites

- `01-shared-contextual-agent-window-contract` completed.
- `02-project-structure-integration` completed.
- `03-process-workspace-integration` completed.

## Exact Source References

- C:\repositories\CanDoItAll\CanDoItAll.slnx
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj
- C:\repositories\CanDoItAll\src\CanDoItAll.Web\CanDoItAll.Web.csproj
- C:\repositories\CanDoItAll\floating_agent_workspace_windows_bundle\reviews\01-execution-report.md

## Deliverables

- Build/test results.
- Playwright MCP screenshots for project and process contextual agent flows.
- Agents chat tab confirmation that contextual thread is visible.
- Completed execution report, raw-note closure, browser analytics review, and final validator run.

## Dependency Impact

- Final closure depends on this subbundle.
- Any weak browser proof must reopen the relevant host integration.

## Validation Depth

- End-to-end regression and closure with real browser proof.

## Implementation Steps

1. Run builds and focused tests.
2. Start or attach the app using the repo watch path.
3. Use Playwright MCP to validate project structure launcher, chat, prompt send, and screenshot.
4. Use Playwright MCP to validate process launcher, chat, prompt send, and screenshot.
5. Open Agents chat tab and confirm the created contextual thread exists.
6. Update execution report and run final closure validator.

## Scope Exceptions

- If provider credentials block a completed assistant response, record the blocker, but still prove thread creation, prompt submission attempt, UI behavior, and thread visibility.

## Do Not Do

- Do not replace Playwright proof with static reasoning.
- Do not close the bundle if screenshots show clipped or unreadable windows.

## Acceptance Checklist

- Build/test proof is recorded.
- Project route proof is recorded with screenshot.
- Process route proof is recorded with screenshot.
- Agents chat tab thread visibility proof is recorded.
- Raw notes are closed.

## Proof Required

- Build/test command output summary.
- Playwright MCP action summary and screenshot paths.
- Final prepared and completed validator pass.

## Browser Validation Logging

- Required for project, process, and Agents chat tab routes.

## Progression Gate

- The bundle may close only after all raw notes are solved or an explicit blocker is documented with enough evidence.

## Suggested Agent Prompt

```text
Run the validation and browser proof for the contextual floating agent windows, then synchronize the bundle report.
```
