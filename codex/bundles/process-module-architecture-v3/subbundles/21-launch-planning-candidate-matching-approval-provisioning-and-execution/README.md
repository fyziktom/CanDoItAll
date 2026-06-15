# SB21 Launch Planning, Candidate Matching, Approval, Provisioning, And Execution

## Status

Future implementation package; prepared by architecture bundle v3; not executed in v3.

## Objective

Rebuild process launch planning: launch name, operating mode, role demand matrix, candidate matching, gaps, approval submission/decision, provisioning, and execute-ready launch.

## Covered Inputs

- REQ-010, REQ-011, REQ-014, REQ-024, REQ-051, REQ-052.
- US-026 through US-029 and US-041.
- AC-004, AC-005, AC-012, AC-021, AC-039, AC-040.

## Prerequisites

- SB18 step/role/artifact definitions complete.
- SB06 builder plan compiler and SB09 manager policies complete.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsLaunchSection.razor`
- `repo://src/CanDoItAll.Modules.Processes/Launch`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessLaunchPlanningIntegrationTests.cs`
- `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProcessStartSmoke.cs`

## Target Projects / Files

- `src/CanDoItAll.Modules.Processes`
- New Process application/projection projects introduced by upstream backend subbundles.
- `tests/CanDoItAll.Tests.Components`
- `tests/CanDoItAll.Tests.Playwright`
- Subbundle proof directory for screenshots, snapshots, and execution report artifacts.

## Deliverables

- Launch planning UI over launch projections and typed commands.
- Candidate matching and role gap/provisioning projection.
- Approval/provisioning workflow UI.
- Execute-ready launch command wired through builder/runtime start contract.
- Component, integration, and Playwright proof.

## Dependency Impact

- SB22 run history depends on successful governed run creation.
- SB25 assignment views depend on assignment records produced by launch.

## Validation Depth

- Integration tests for launch plan creation, candidate selection, approval, provisioning, and execute-ready launch.
- Component tests for launch plan states and decision buttons.
- Playwright proof for a guarded launch flow.

## Refactoring Review Checkpoint

- Keep component rendering separate from projection loading and command dispatch.
- Keep projection client code out of low-level visual components.
- Split large components or services before handoff if they combine unrelated workflow areas.
- Verify UI code does not reference runtime internals, EF runtime entities, or old observation services.

## Performance Antipattern Notes

- Read `architecture/19-dotnet-performance-guardrails.md` and `validation/05-dotnet-performance-antipattern-checklist.md` before creating or modifying C# hot-path code.
- Record exact performance scan counts in the execution report when this subbundle changes runtime, dispatcher, manager, projection, template, Git, adapter, persistence, or UI service code.
- Do not introduce sync-over-async, unbounded event/projector queues, per-call `HttpClient`, per-call `JsonSerializerOptions`, load-all UI queries, or LINQ-heavy hot paths without a recorded mitigation and proof.
## Implementation Steps

1. Bind launch form and plan list to launch plan projections.
2. Implement role candidate matrix and candidate selection commands.
3. Implement approval submission and decision commands.
4. Implement provisioning and execute-ready launch command flow.
5. Add tests and Playwright proof.
6. Record story coverage for US-026 through US-029 and US-041.

## Do Not Do

- Do not allow arbitrary direct run creation from UI.
- Do not bypass builder plan compilation.
- Do not hide staffing/provisioning gaps.

## Stop And Report Conditions

- Stop if required projection fields are missing and would force direct runtime or persistence access from UI.
- Stop if preserving the current UX requires reviving old dispatcher/runtime behavior.
- Stop if browser proof cannot be captured for an owned browser-facing story.
- Stop if a story appears to require removal or major UX replacement without explicit user approval.

## Acceptance Checklist

- [ ] Launch plan creation works through typed commands.
- [ ] Candidate matrix and gaps are visible.
- [ ] Approval/provisioning gates are enforced.
- [ ] Execute-ready launch creates a governed run.
- [ ] Browser proof exists.

## Proof Required

- Integration/component test output.
- Playwright launch screenshot evidence.
- Story coverage table for US-026 through US-029 and US-041.

## Browser Validation Logging

- Required. Capture launch tab actions, candidate selection, approval/provisioning state, execute-ready result, screenshot, and console/network summary.

## Progression Gate

- SB22 may start after a run can be created through the governed launch path.

## Suggested Agent Prompt

Execute SB21 from `codex/bundles/process-module-architecture-v3/subbundles/21-launch-planning-candidate-matching-approval-provisioning-and-execution`. Rebuild launch planning and execute-ready flow without direct run creation shortcuts.

## Handoff Notes For Next Bundle

Record run identifiers, launch projection fields, and lifecycle events needed by SB22.
