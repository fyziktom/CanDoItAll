# PRM-F05 — Transition rules, decisions, and explicit handoffs

## Objective

Model ordered responsibility changes between actors, decision branches, retries, default paths, and explicit handoff payloads for process execution.

## Priority and wave

- Priority: **Critical**
- Planned wave: **Wave 2**
- Depends on: **PRM-F02, PRM-F03, PRM-F04**

## Why this feature exists

This feature is part of the first process-management bundle because the user explicitly wants process definitions, actor responsibility, handoffs, and interactive modeling to land **before** the intelligence lake and before deep runtime coupling to the AgentFramework overlay.

## In scope

- Decision paths can carry condition text, default-path markers, and branch priority.
- Handoffs record source actor, target actor, payload summary, and completion reason.
- The engine rejects invalid graphs such as unreachable end states or orphaned transitions.
- Sequential specialized handoffs are first-class even before AgentFramework runtime integration.

## Non-goals

- Do not introduce advanced parallel orchestration in the first handoff release.

## Primary repo touchpoints

- `src/CanDoItAll.Modules.Processes/ProcessTransitionServices.cs (new)`
- `src/CanDoItAll.Modules.Processes/ProcessValidationServices.cs (new)`
- `tests/CanDoItAll.Tests.Unit/ProcessTransitionRulesTests.cs (new)`
- `tests/CanDoItAll.Tests.Integration/ProcessHandoffIntegrationTests.cs (new)`
