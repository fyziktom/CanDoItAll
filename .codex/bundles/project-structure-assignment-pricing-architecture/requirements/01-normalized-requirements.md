# Normalized Requirements

## R001 — Mixed direct assignments are editable

A canonical task may have multiple valid `WorkItemAssignee` records, including a person and an AI agent. Gantt and canvas task editors must open. They may choose a deterministic current selection for the single-choice control, but unchanged saves must preserve the complete assignment set.

## R002 — Mixed assignment mutation is safe

The current scalar editors must not replace or clear a mixed direct-assignment set. They select the unique primary assignment for context when one exists, expose an explicit mixed-assignment warning, and keep direct Person/Agent mutation read-only. Non-assignment edits and additive Workflow/Process attachment remain available. Existing single-assignee replacement and compensation behavior remains unchanged.

## R003 — Resource cost is strategy-owned

`ProjectStructureTaskResourceCostService` must select exactly one strongly typed strategy for `Person`, `Agent`, `Workflow`, or `Process`. Person uses CRM internal cost rate. Agent, workflow, and process use their execution-history estimation mechanisms. Missing or duplicate registrations fail explicitly.

## R004 — Unstarted task price is authoritative

New tasks are persisted with `ProjectTaskExecutionState.NotStarted`. Existing tasks refresh expected cost during update only when their explicit execution state is `NotStarted`. `Started`, `Completed`, and `Cancelled` tasks preserve their historical expected cost. Legacy tasks without the new metadata deserialize as `Unknown` and fail closed: they are not automatically repriced from schedule, status text, or progress.

## R005 — Missing source price clears stale price

If an unstarted selected resource has no authoritative/estimable quote, the persisted estimate has no cost amount or currency. The UI must expose the unavailable reason; it must not silently preserve the previous disconnected amount.

## R006 — Mutation paths agree

Gantt create/edit, canvas task create/edit, and agent/API task create/edit paths must use the same pricing policy at their relevant service or submission boundary. New-task paths explicitly write `NotStarted`. UI preview remains helpful but is not the only enforcement point.

## R007 — Real architecture extraction

Assignment interpretation and resource pricing move to cohesive top-level types. No new `ProjectStructurePage` partial, nested strategy, service locator, or project-reference cycle is allowed. Direct unit tests instantiate extracted owners without `ProjectStructurePage`.

## R008 — No-regression closure

Existing task scheduling, row ordering, resource attachment, optimistic concurrency, assignment rollback, canvas creation/editing, and dialog behavior continue to pass affected tests and builds. Browser proof targets the existing large-screen desktop composition only.
