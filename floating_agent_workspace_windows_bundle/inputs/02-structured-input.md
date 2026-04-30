# Structured Input

## Core Objective

- Add contextual floating agent launcher and chat windows to the project structure and process definition canvas workflows.

## Hard Constraints

- Use existing shared components and floating-window primitives.
- Keep access filtering strongly typed through `AgentProjectStructureAccessMetadata` and `AgentProcessAccessMetadata`.
- Do not show agents without explicit access to the active project or process context.
- Use `TagEditor` for tag filtering.
- Reuse the existing chat body and chat service behavior.
- Browser proof must use Playwright MCP and screenshots.

## Source Artifacts

- User prompt and screenshot in current Codex thread.
- Existing AgentFramework chat page and `ChatWorkspacePanel`.
- Existing project/process access metadata.
- Existing ProjectStructure and Process canvas floating-window hosts.

## Input Coverage Signals

- The word `must` applies to search, tag editor, Playwright MCP, screenshots, and same-chat behavior.
- The request covers both project structure and process pages; proof for only one surface is insufficient.
- New contextual chat threads must later be discoverable from the Agents page chat tab.

## Dependency And Sequencing Signals

- Shared component behavior must land before project/process host integration.
- Project and process host integrations can proceed independently after the shared component compiles.
- Browser proof must run after both hosts are integrated.

## Validation Expectations

- Build affected projects.
- Use Playwright MCP to open project structure, open the launcher, filter/search, double-click an agent, send a prompt for calculator roadmap nodes, and capture screenshots.
- Use Playwright MCP to open process steps, open the launcher, double-click an agent, send a prompt for a review role, and capture screenshots.
- Open the Agents page chat tab and confirm the newly created contextual thread is visible.

## UI Validation Strategy

- Run the first browser pass in a large headed viewport.
- Inspect screenshots for readability, clipping, lateral overflow, alignment, z-order, and whether the launcher and chat windows remain usable next to existing canvas chrome.
- Run a narrower-width pass if the windows are visibly cramped or the large pass reveals responsive risk.

## Browser Validation Analytics

- Record route, viewport, Playwright MCP actions, assertions, screenshot paths, and result per UI subbundle in `reviews/01-execution-report.md`.

## Working Assumptions

- The launcher belongs on the project structure workbench route rather than the projects card board because the requested workflow is mindmap/project-structure analysis.
- The process launcher belongs on the process definition canvas in the Steps tab because the requested validation adds a review role to the mindmap-style process surface.
- One active contextual chat window is sufficient for this request as long as each double-click creates a new persisted thread.

## Primary Risks

- Incorrect access filtering could expose agents outside their allowed scope.
- Provider credentials may block a full assistant response during validation.
- Floating windows can collide with existing toolbox, selection, signals, or editor overlays.
