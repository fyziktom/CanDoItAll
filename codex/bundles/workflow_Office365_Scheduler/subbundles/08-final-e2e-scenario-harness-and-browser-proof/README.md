# 08-final-e2e-scenario-harness-and-browser-proof

## Status

- Status: `Completed`

## Objective

Close the bundle with fake Graph end-to-end proof across Office365, workflow templates, project writes, Scheduler Planner UX, retry/idempotency, and browser-visible configuration.

## Covered Inputs

- R1-R12: final integrated proof and raw-note closure.

## Prerequisites

- SB01-SB07 closure gates passed or blockers are explicitly recorded.
- All critical proof manifests and semantic invariant files for completed subbundles exist.

## Exact Source References

- `repo://CanDoItAll.slnx`
- `repo://Templates/Workflows/manifest.yaml`
- `repo://Templates/Workflows/workflows/workflow-executor-catalog-workflows.yaml`
- `repo://src/plugins/CanDoItAll.Plugin.Office365/Office365WorkflowExecutor.cs`
- `repo://src/CanDoItAll.Modules.SchedulerPlanner/Pages/SchedulerPlannerPage.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowTemplatePackLoaderTests.cs`
- `repo://tests/CanDoItAll.Tests.Components/SchedulerPlannerPageTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/SchedulerPlannerIntegrationTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/ProjectStructureWorkflowScenarioHarnessTests.cs`

## Scope

- Run final restore/build/targeted unit/integration/component matrix.
- Run fake Graph no-message, one-message summary, one-message tasks, retry, and processed-category scenarios.
- Capture browser proof on `/scheduler` and `/agents/workflows`.
- Audit raw notes R1-R12 one by one.
- Run completed-stage bundle validator and repair any proof gaps.

## Dependency Impact

- This is the final closure gate. It may reopen any prior subbundle whose proof is contradicted by final scenario or browser observations.

## Validation Depth

- Critical verifier/red-team proof that rejects fake proof, fixture-only behavior, manually seeded production signals, and missing transcript paths.
- Completed-stage validator with all critical manifests, semantic invariants, transcripts, hashes, source assertions, and anti-stub audit artifacts present.
- Browser proof for Scheduler setup without raw JSON and Workflows template visibility.

## Implementation Steps

1. Re-run restore/build.
2. Run targeted unit tests for Office365 executor, workflow templates, Scheduler schema, idempotency, and approval/retry policy.
3. Run integration scenario harness tests with fake Graph.
4. Run component tests for Scheduler typed form and Workflows template visibility.
5. Run browser proof for `/scheduler` desktop/narrow and `/agents/workflows`.
6. Complete raw-note closure and final verifier artifact.
7. Run completed-stage validator and update root/report status.

## Do Not Do

- Do not close from prose-only proof.
- Do not use live Office365 credentials in automated proof.
- Do not leave any completed critical subbundle with missing manifest, missing semantic invariant, missing transcript, or machine-specific-only proof path.

## Acceptance Checklist

- New Office365 executor is visible in plugin catalog and workflow toolbox.
- New templates are visible in template pack and seed.
- Scheduler configures the scenario without raw JSON while raw JSON remains available.
- No-message runs are not failures.
- Processed category mark happens after successful project write.
- Retry does not duplicate summary/tasks.
- No live Office365 credentials are required in automated tests.
- Completed-stage validator passes.

## Proof Required

- `bundle://proof/SB08/manifest.md`
- `bundle://proof/SB08/semantic-invariants.md`
- Restore/build transcript.
- Targeted unit/integration/component transcripts.
- Browser proof artifacts and screenshots.
- Final verifier/red-team artifact.
- Completed-stage validator transcript.

## Closure Notes

- Restore, solution build, targeted unit, fake Graph integration, Scheduler integration, project-structure scenario harness, component, EF pending-model, source assertion, anti-stub, and browser proof passed.
- Browser proof captured Scheduler and Workflows desktop/narrow routes plus Office365 executor toolbox visibility.
- Raw notes R1-R12 are closed in `bundle://reviews/01-execution-report.md`.

## Browser Validation Logging

- Routes: `/scheduler` and `/agents/workflows`.
- Viewports: desktop large viewport first, then narrow/mobile.
- Actions: verify template visibility, select Office365 email-watch workflow, configure email/contact, project/node, category, two-hour interval, raw JSON sync, save validation, and history/status where implemented.
- Record screenshots, assertions, and result in `reviews/01-execution-report.md`.

## Progression Gate

- Close the bundle only when code, tests, browser proof, manifests, raw-note closure, and completed-stage validator all agree; otherwise reopen the failing subbundle.

## Suggested Agent Prompt

Run the final fake Graph scenario harness, targeted test matrix, browser proof, raw-note audit, and completed-stage bundle validation; repair any evidence or implementation gap before declaring closure.
