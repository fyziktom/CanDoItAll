# SB11 Execution Adapters And First Layered Driver Slice

## Status

Future implementation package; prepared by architecture bundle v3; not executed in v3.

## Objective

Prove execution strategy adapters and layered drivers with a representative narrow slice, including workflow, single-agent, agent-group, handoff, scheduler-trigger, and project/workbench integration boundaries.

## Why This Bundle Exists

The architecture must support real execution without leaking adapter-specific APIs into generic runtime/core. This bundle proves that with one layered driver path.

## Covered Inputs

- REQ-006 through REQ-009.
- REQ-039 and REQ-040 where execution modifies files and must be audited.
- v3 adapter boundary requirements.

## Context Reset: Read These First

- SB10 execution report.
- `architecture/16-execution-adapters-and-integration-boundaries.md`
- `architecture/06-driver-strategy-and-manager-model.md`
- `architecture/10-security-governance-and-agent-change-auditing.md`

## Exact Source References

- `repo://codex/bundles/process-module-architecture-v3/architecture/16-execution-adapters-and-integration-boundaries.md`
- `repo://codex/bundles/process-module-architecture-v3/architecture/06-driver-strategy-and-manager-model.md`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAutomationExecutionClient.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessWorkflowRunCoordinator.cs`
- `repo://src/CanDoItAll.Modules.Processes/ProjectStructure/ProcessProjectStructureContext.cs`

## Source Evidence To Use

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessAutomationExecutionClient.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessWorkflowRunCoordinator.cs`
- `repo://src/CanDoItAll.Modules.Processes/ProjectStructure/ProcessProjectStructureContext.cs`
- SB01 adapter/dispatch archive.

## Prerequisites

- SB10 complete.
- Runtime/manager/projection contracts stable.

## In Scope

- Workflow execution strategy adapter.
- Single-agent strategy adapter.
- Agent-group/collaboration adapter.
- Handoff adapter strategy.
- Scheduler-triggered start adapter.
- Project/workbench context adapter.
- First representative layered driver stack.
- Diagnostic/redaction behavior.
- Adapter result envelope tests.
- Agent mutation audit integration where relevant.

## Out Of Scope

- Do not implement every possible domain driver.
- Do not rebuild UI.
- Do not change core/runtime for concrete adapters.
- Do not implement final E2E proof; that is SB14.

## Target Projects / Files

- `src/CanDoItAll.Processes.Drivers.*`
- adapter implementation projects or folders selected by implementation design.
- `src/CanDoItAll.Processes.Application`
- adapter/driver tests.

## Deliverables

- Adapter strategy implementations for representative execution kinds.
- First layered driver slice.
- Result envelope and diagnostic tests.
- No generic runtime changes for concrete adapters.

## Expected Deliverables

- Runtime sees only strategy IDs and result envelopes.
- Adapter diagnostics become restricted evidence/user-safe summaries.
- Project/workbench/scheduler context stays outside core/runtime.

## Dependency Impact

- SB12 uses adapter behavior to validate template/process compatibility.
- SB14 uses adapter slice in E2E representative flow.

## Validation Depth

- Validate with adapter envelope tests, diagnostic redaction tests, driver layering tests, mutation audit tests, integration-boundary scans, and security review.

## Architecture Invariants That Must Hold

- Core/runtime do not reference concrete workflow/agent/handoff/scheduler/project APIs.
- Adapters do not mutate runtime state directly.
- Strategy results go through envelopes.
- Unauthorized file changes are audited through Git wrapper.

## Implementation Steps

1. Select representative layered driver slice.
2. Implement adapter strategy contracts.
3. Implement result envelope normalization.
4. Implement diagnostic redaction and restricted evidence refs.
5. Implement scheduler/project/workbench context adapters.
6. Add tests and scans.

## Refactoring Review Checkpoint

- Split adapter IO from result normalization.
- Keep driver facets separate from strategy implementations.
- Verify no adapter-specific types leak into core/runtime.

## Required Tests / Proof

- Adapter result envelope tests.
- Diagnostic redaction tests.
- Driver layering tests.
- Unauthorized mutation audit tests where file mutation exists.
- Dependency leak tests.

## Search Proof

- Search Core/Runtime for adapter-specific names.
- Search for direct workflow/agent/handoff calls in Runtime.
- Search for ad hoc Git calls outside `CanDoItAll.Git`.

## Stop And Report Conditions

- Stop if generic runtime must reference concrete adapter APIs.
- Stop if adapters need to mutate runtime state directly.
- Stop if diagnostics cannot be safely classified.

## Do Not Do

- Do not leak adapter APIs into core/runtime.
- Do not bypass strategy envelopes.
- Do not implement every domain driver in this slice.
- Do not call Git outside `CanDoItAll.Git`.

## Acceptance Checklist

- [ ] Adapter strategies implemented.
- [ ] First layered driver slice works.
- [ ] Result envelope tests pass.
- [ ] Diagnostic/redaction tests pass.
- [ ] Dependency leak scans pass.

## Proof Required

- Test output.
- Dependency scan.
- Security/redaction review.
- Driver contract proof.

## Browser Validation Logging

- Browser validation is not required unless the representative adapter slice includes browser-visible behavior; if it does, record route, viewport, screenshots, and assertions.

## Progression Gate

- SB12 may proceed after adapter and driver slice proof shows no generic runtime leaks.

## Suggested Agent Prompt

Execute SB11 from `codex/bundles/process-module-architecture-v3/subbundles/11-execution-adapters-and-first-layered-driver-slice`. Prove adapters and one layered driver slice without changing generic runtime for concrete integrations.

## Handoff Notes For Next Bundle

Record adapter APIs, driver slice boundaries, diagnostics behavior, mutation audit behavior, and gaps for SB12/SB14.
