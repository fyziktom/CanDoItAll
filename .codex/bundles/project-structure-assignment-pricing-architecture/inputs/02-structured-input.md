# Structured Input

## Core Objective

- Restore correct Gantt editing and authoritative task pricing without losing Project Structure behavior, while extracting a cohesive, testable assignment/pricing slice from the partial-page architecture.

## Success Criteria

- A task with a person and an AI agent assigned can open in the Gantt task dialog.
- Saving non-assignee fields does not delete or rewrite its existing assignments.
- A mixed direct-assignment set remains intact; its scalar editor is read-only while ordinary task fields and additive Workflow/Process attachment remain editable.
- New and currently unstarted tasks refresh cost from the selected resource rather than retaining a stale/manual amount.
- Person pricing uses the CRM internal workforce rate; agent, process, and workflow pricing use independently registered estimator strategies.
- A missing source price cannot leave a disconnected old amount on an unstarted task.
- Extracted behavior is tested without constructing `ProjectStructurePage`.

## Hard Constraints

- Do not remove or weaken existing Project Structure behavior.
- Do not add another `ProjectStructurePage` partial as the final boundary.
- Keep resource kinds and lifecycle decisions strongly typed.
- Fail explicitly for a missing or duplicate strategy registration.
- Preserve historical cost once a task has started.
- Keep logs actionable and free of sensitive names or payloads.

## Allowed Side Effects

- Production and test code in the Workbench, AgentFramework integration, application composition, and the bundle itself, limited to the documented assignment/pricing slice.

## Source Artifacts

- `inputs/00-original-request.md`
- Current source and tests listed in `inventories/01-scope-inventory.md`

## Input Coverage Signals

- `N001`: mixed person/agent assignment must not block Gantt opening.
- `N002`: unstarted task creation/update must refresh person pricing from CRM.
- `N003`: agent/process/workflow pricing must use an estimation strategy.
- `N004`: a workforce member without a CRM price must not leave a disconnected task price.
- `N005`: improve the Project Structure partial-class architecture with real types/test seams.
- `N006`: preserve all unrelated functionality and validate regressions.

## Dependency And Sequencing Signals

- Assignment resolution and the cost-strategy registry are the critical foundation.
- Authoritative lifecycle refresh depends on those boundaries.
- Browser/regression closure depends on both behavior phases and reopens them on contradiction.

## Validation Expectations

- Behavioral proof for the foundation and feature phases; Standard plus browser-visible regression proof for closure.

## Evidence Contract

- `SB01` Behavioral: direct resolver/strategy tests, negative registration tests, affected build, source assertions, architecture checkpoint.
- `SB02` Behavioral: positive mixed-assignee and CRM/history refresh cases plus missing-price and started-task boundaries.
- `SB03` Behavioral: targeted component suite, Workbench/app build, large-screen Gantt open-dialog browser smoke if the host is available, final architecture and bundle validators.

## UI Validation Strategy

- Primary surface: existing Project Structure Gantt tab and its existing wide task-details dialog.
- Supporting content, stats, fields, sizing, and scroll ownership stay unchanged; this is a behavior/architecture repair, not a composition redesign.
- Target: maximized desktop, nominally `1600x1000`; the modal body remains the scroll owner and its close/save actions must remain visible.
- Required open-overlay proof: a mixed-assignee task opens the existing dialog without the `Task details unavailable` notification.

## Browser Validation Analytics

- `SB03` logs the project-structure route, `1600x1000` viewport, Gantt task activation, dialog visibility, absence of assignment-conflict notification, screenshot path, modal scroll ownership, and action visibility.

## Working Assumptions

- “Task did not happen” is represented by explicit `ProjectTaskExecutionState.NotStarted` metadata. New tasks set it. Legacy missing state is `Unknown` and is not inferred from scheduled dates, free-text status, or progress.
- When an unstarted task has a selected resource but its authoritative source has no quote, the stale expected cost is cleared explicitly rather than silently preserved or invented.
- The existing single-choice assignee editor may show a deterministic primary/current assignee. When more than one canonical direct assignment exists, direct mutation is read-only and all assignments are preserved.

## Primary Risks

- Enabling scalar replacement for a mixed set would erase valid assignments and cannot be allowed by this bundle.
- Client-only recalculation would leave agent/API mutation paths inconsistent.
- A strategy abstraction could be cosmetic if the old service retains provider branches.
- Agent cost estimation can create a dependency cycle if implemented in the wrong project.
- The very large partial page makes broad refactoring unsafe; this bundle owns only the assignment/pricing slice.
