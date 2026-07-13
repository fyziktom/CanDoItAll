# Structured Input

## Core Objective

- State the primary outcome without implementation detail.

## Success Criteria

- List the observable conditions that prove the outcome is done.

## Hard Constraints

- List the non-negotiables.

## Allowed Side Effects

- State what may be changed. Use `none beyond documented subbundles` when scope is tight.

## Source Artifacts

- Reference the files, docs, screenshots, or prompts that define the task.

## Input Coverage Signals

- List each raw note or artifact that cannot be safely collapsed, merged, or deferred.

## Dependency And Sequencing Signals

- Note which requested outcomes obviously unlock or block later work.

## Validation Expectations

- Describe the proof required before implementation is complete.

## Evidence Contract

- List the commands, screenshots, browser checks, host checks, or artifacts that must exist before closure.

## UI Validation Strategy

- If the task is UI-related, note the large-screen Playwright pass, screenshot review questions, and narrower-width follow-up plan.

## Browser Validation Analytics

- If the task is UI-related, note how each subbundle will log route, viewport, Playwright MCP actions, assertions, screenshot paths, and result.

## Working Assumptions

- Record the assumptions made during bundle preparation.

## Primary Risks

- Record the main delivery, UI, architecture, or regression risks.
# Structured Input

## Problem

The Playwright Local MCP capability setup test fails from the Agents capability details dialog because the app host lacks a live setup runtime registration. Old managed development workspace data may also prevent newly required configuration fields from being present.

## Affected Areas

- Agent capability setup API and UI
- MCP runtime client factory
- Capability template validation and seed materialization
- MAF capability descriptor construction
- Development workspace seed refresh
- Agent project/process/workflow runtime tool access

## Constraints

- C#/.NET and Blazor changes only inside existing architecture.
- No silent fallback behavior that hides runtime failures.
- Strongly typed configuration where feasible.
- Large-screen UI validation only.
- App must be running at `http://localhost:5032/` after closure.

## Closure Evidence

- Focused unit, component, and integration tests.
- Live Playwright MCP setup pass from the UI.
- Live workspace record inspection.
- Large-screen UI screenshots for agents, projects, workflows, and processes.
