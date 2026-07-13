# Structured Input

## Problem Statement

The current process escalation path does not expose enough typed evidence to distinguish whether a blocked step failed because of:

- process instructions that are too brittle or contradictory;
- missing or denied runtime tools;
- missing or denied MCP tools;
- skill suppression or unwanted skill availability;
- domain process driver gaps;
- agent/provider behavior;
- missing managed artifacts or project-structure access;
- generic runtime/projection defects.

The previous change made this worse by trying to repair a .NET delivery proof problem in generic completion validation. That violated the architecture boundary and moved failure earlier in the process.

## Target Outcome

Prepare a staged refactoring bundle that first improves diagnostics and capability readiness, then isolates driver-owned recovery and .NET delivery specifics, and finally hardens templates and E2E replay tests.

## Non-Negotiable Boundaries

- Generic process runtime, dispatcher, projection, and process contracts must remain domain-neutral.
- Domain-specific behavior belongs in process definitions, process drivers, driver strategies, domain process adapters, or agent/team templates.
- Do not add calculator, tetris, Blazor, .NET, screenshot, or Playwright semantics to generic runtime services.
- Do not rely on prompt text as the only enforcement mechanism for required tools, MCPs, skills, or suppressions.
- Do not add silent fallback mechanisms. Recovery must classify failure explicitly and either retry with typed evidence or block with actionable diagnostics.

## Primary Investigation Questions

- Can launch/readiness detect that a process step requires specific tools, MCPs, skills, or suppressed skills before the agent starts?
- Can the manager fallback classify and recover missing artifacts, denied tools, missing MCPs, and instruction non-compliance without domain leaks?
- Which parts of the current process runtime are too large or mixed to unit test root causes independently?
- Which .NET/software delivery rules currently live in generic adapter or common process layers?
- Which template instructions are overfit to Calculator, Tetris, Blazor WebAssembly, or screenshot-heavy apps?

## Prepared-Only Scope

This bundle does not implement source changes. It prescribes phases, boundaries, proof, and validation criteria for the next implementation run.
