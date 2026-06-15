# SB21 Launch Planning, Candidate Readiness, Approval, Provisioning, And Execution

## Status

Future implementation package; prepared by architecture bundle v3; not executed in v3.

## Objective

Rebuild process launch planning: launch name, operating mode, role demand matrix, candidate discovery, advisory suitability scoring, deterministic candidate readiness, missing tool/right/capability reporting, gaps, approval submission/decision, provisioning, and execute-ready launch.

The current implementation already has useful HR-driven scoring and a candidate matrix. The rewrite must preserve that UX direction, but it must not treat HR score as proof that a candidate can execute the role. Every candidate selected for a process role must have typed readiness findings that show missing required tools, rights, permissions, approvals, bindings, access, provisioning tasks, and execution blockers.

## Covered Inputs

- REQ-010, REQ-011, REQ-014, REQ-024, REQ-051, REQ-052, REQ-054.
- US-026 through US-029, US-041, and US-056.
- AC-004, AC-005, AC-012, AC-021, AC-039, AC-040, AC-042.

## Context Reset Files

- `analysis/06-current-implementation-user-story-map.md`
- `analysis/08-current-role-candidate-selection-gap.md`
- `architecture/04-builder-and-instance-composition.md`
- `architecture/14-manager-runtime-and-control-loop.md`
- `architecture/18-user-story-coverage-model.md`
- `architecture/20-role-candidate-selection-and-readiness.md`
- `traceability/04-user-story-coverage-map.md`
- `validation/04-user-story-coverage-validation.md`
- `validation/06-role-candidate-readiness-validation.md`

## Prerequisites

- SB18 step/role/artifact definitions complete.
- SB06 builder plan compiler and SB09 manager policies complete.
- Candidate readiness architecture in `architecture/20-role-candidate-selection-and-readiness.md` accepted.
- Projection contracts from SB10/SB13 available so UI does not query runtime internals.

## Exact Source References

- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsLaunchSection.razor`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Launch`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Persistence/Entities/ProcessRuntimeModels.cs`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/tests/CanDoItAll.Tests.Integration/ProcessLaunchPlanningIntegrationTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/AgentFrameworkExecutionCapabilityFilteringIntegrationTests.cs`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProcessStartSmoke.cs`
- `repo://codex/bundles/process-module-architecture-v3/analysis/08-current-role-candidate-selection-gap.md`
- `repo://codex/bundles/process-module-architecture-v3/architecture/20-role-candidate-selection-and-readiness.md`
- `repo://codex/bundles/process-module-architecture-v3/validation/06-role-candidate-readiness-validation.md`

## Target Projects / Files

- `src/CanDoItAll.Modules.Processes`
- New Process application/projection projects introduced by upstream backend subbundles.
- New launch/readiness application services introduced by this subbundle.
- New launch/readiness projection DTOs introduced by this subbundle.
- `tests/CanDoItAll.Tests.Components`
- `tests/CanDoItAll.Tests.Integration`
- `tests/CanDoItAll.Tests.Playwright`
- Subbundle proof directory for screenshots, snapshots, readiness matrices, and execution report artifacts.

## Deliverables

- Launch planning UI over launch projections and typed commands.
- Role execution requirement compiler that aggregates requirements from roles, steps, operation contracts, artifacts, selected operating mode, driver descriptors, project scope, and manager policy.
- Candidate discovery over existing agents, people, workflows, assignments, provisioning proposals, and explicit gap candidates.
- Advisory suitability score with explainable breakdown.
- Deterministic candidate readiness assessment with typed findings for missing required tools, rights, capabilities, approvals, provider/workflow bindings, project/resource access, direct messaging permission, availability, and policy blockers.
- Itemized provisioning and approval tasks linked to readiness findings.
- Candidate matrix and selected-role card that show score and readiness separately.
- Approval/provisioning workflow UI that cannot hide unresolved blockers.
- Execute-ready launch command wired through builder/runtime start contract with readiness assessment hashes copied into assignments.
- Component, integration, negative-case, redaction, reassessment, and Playwright proof.

## Dependency Impact

- SB22 run history depends on successful governed run creation and assignment snapshots that include candidate readiness hashes.
- SB24 operator control depends on launch readiness findings becoming manager-readable incidents when unresolved assignment problems surface at runtime.
- SB25 assignment/evidence views depend on selected candidate assignment records, unresolved warning lists, and readiness evidence references.
- SB27 API/tool compatibility depends on the same typed readiness projection so agent tools cannot bypass launch blockers.

## Validation Depth

- Integration tests for launch plan creation, candidate discovery, suitability scoring, deterministic readiness assessment, candidate selection, approval, provisioning, reassessment, and execute-ready launch.
- Negative tests proving high HR score does not override missing required tools or rights.
- Tests proving provisioning completion does not clear readiness until reassessment observes fresh evidence.
- Redaction tests for sensitive right/tool details.
- Component tests for candidate matrix readiness badges, finding expansion, selected-candidate summary, approval/provisioning actions, and blocked execute state.
- Playwright proof for a guarded launch flow with ready, missing-tool, missing-right, approval-required, provisioning-required, and execute-ready states.

## Refactoring Review Checkpoint

- Keep component rendering separate from projection loading and command dispatch.
- Keep candidate scoring separate from readiness evaluation.
- Keep HR agent recommendations out of authority-granting or blocker-clearing code.
- Keep projection client code out of low-level visual components.
- Split large components or services before handoff if they combine unrelated workflow areas.
- Verify UI code does not reference runtime internals, EF runtime entities, old observation services, or raw policy evidence.

## Performance Antipattern Notes

- Read `architecture/19-dotnet-performance-guardrails.md` and `validation/05-dotnet-performance-antipattern-checklist.md` before creating or modifying C# hot-path code.
- Candidate readiness evaluation must batch shared directory, tool-provider, workflow, provider-profile, rights, and assignment evidence instead of performing repeated per-candidate external calls.
- Record exact performance scan counts in the execution report when this subbundle changes runtime, dispatcher, manager, projection, template, Git, adapter, persistence, or UI service code.
- Do not introduce sync-over-async, unbounded event/projector queues, per-call `HttpClient`, per-call `JsonSerializerOptions`, load-all UI queries, or LINQ-heavy hot paths without a recorded mitigation and proof.

## Candidate Readiness Notes

- HR score is advisory. It helps rank candidates but cannot prove launch readiness.
- Missing required tools and missing required rights are blocking by default.
- Missing optional capabilities may be warnings only when the role requirement explicitly marks them optional.
- Every blocker must be a typed `CandidateReadinessFinding`, not buried in `RecommendationSummary` text.
- Every provisionable blocker must produce a durable provisioning task linked to the finding.
- Every approval-sensitive blocker must produce an approval task or an explicit policy denial.
- A selected candidate may remain selected while not executable, but the UI must show that approval or execution is blocked.
- Launch approval and execution gates must use readiness status and findings, not score thresholds.
- Runtime assignments must include readiness assessment hash, requirement set hash, evidence snapshot hash, unresolved warnings, and approved override references.

## Implementation Steps

1. Bind launch form and plan list to launch plan projections.
2. Implement the role execution requirement compiler.
3. Implement candidate discovery and gap candidate creation.
4. Implement advisory suitability scoring and score breakdown projection.
5. Implement deterministic readiness evaluator and typed readiness findings.
6. Implement provisioning and approval planners linked to findings.
7. Implement role candidate matrix, selected-candidate summary, finding expansion, and candidate selection commands.
8. Implement approval submission, approval decision, provisioning, reassessment, and blocked/ready launch command flow.
9. Copy readiness assessment hashes and unresolved warning lists into assignment snapshots when execute-ready launch starts.
10. Add unit, integration, component, redaction, negative-case, reassessment, and Playwright proof.
11. Record story coverage for US-026 through US-029, US-041, and US-056.

## Do Not Do

- Do not allow arbitrary direct run creation from UI.
- Do not bypass builder plan compilation.
- Do not hide staffing, readiness, rights, tool, approval, provisioning, or access gaps.
- Do not let HR score suppress deterministic readiness blockers.
- Do not represent readiness only as free text.
- Do not clear blockers because a provisioning task was marked complete without reassessment.
- Do not expose raw sensitive right/tool evidence in ordinary UI projections.

## Stop And Report Conditions

- Stop if required projection fields are missing and would force direct runtime or persistence access from UI.
- Stop if preserving the current UX requires reviving old dispatcher/runtime behavior.
- Stop if candidate readiness cannot be represented as typed findings without changing upstream contracts.
- Stop if HR score is the only available signal for missing tool/right decisions.
- Stop if approval or execution can proceed while required missing-tool or missing-right blockers remain unresolved without an audited override.
- Stop if browser proof cannot be captured for an owned browser-facing story.
- Stop if a story appears to require removal or major UX replacement without explicit user approval.

## Acceptance Checklist

- [ ] Launch plan creation works through typed commands.
- [ ] Candidate matrix and gaps are visible.
- [ ] Suitability score and deterministic readiness are displayed separately.
- [ ] Missing required tools, rights, capabilities, approvals, bindings, and access are visible as typed findings.
- [ ] Provisioning and approval tasks are itemized and linked to findings.
- [ ] Approval/provisioning gates are enforced.
- [ ] Execute-ready launch is blocked while required readiness blockers remain.
- [ ] Execute-ready launch creates a governed run with assignment readiness hashes.
- [ ] Browser proof exists.

## Proof Required

- Integration/component test output.
- Candidate readiness evaluator tests.
- Negative tests for high score with missing required tool and high score with missing required right.
- Reassessment test proving provisioning completion alone does not clear a blocker.
- Redaction test for sensitive missing-right details.
- Playwright launch screenshot evidence for candidate matrix readiness states.
- Story coverage table for US-026 through US-029, US-041, and US-056.

## Browser Validation Logging

- Required. Capture launch tab actions, candidate selection, candidate readiness expansion/details, missing tool blocker, missing right blocker, approval/provisioning state, execute-ready blocked/ready result, screenshot, and console/network summary.

## Progression Gate

- SB22 may start after a run can be created through the governed launch path and after required candidate readiness blockers demonstrably prevent approval/execution until resolved.

## Suggested Agent Prompt

Execute SB21 from `codex/bundles/process-module-architecture-v3/subbundles/21-launch-planning-candidate-matching-approval-provisioning-and-execution`. Rebuild launch planning, deterministic candidate readiness, missing tool/right reporting, approval/provisioning, and execute-ready flow without direct run creation shortcuts or score-only HR candidate decisions.

## Handoff Notes For Next Bundle

Record run identifiers, launch projection fields, candidate readiness models, missing tool/right example findings, provisioning/approval task shapes, assignment readiness hashes, and lifecycle events needed by SB22.
