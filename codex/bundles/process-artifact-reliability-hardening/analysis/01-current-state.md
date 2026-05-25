# Current State

## Process Runtime Boundary

`CanDoItAll.Modules.Processes` is the canonical process runtime for process definitions, runs, step transitions, work briefs, governed outcomes, process artifacts, and AI-agent dispatch. This module references AgentFramework services and workflow catalog services, but that does not make workflow execution the owner of process semantics.

`ProcessesService` exposes process definition/editor surfaces and includes workflow references in role models, such as preferred workflow definition/version ids. This means a process role may be bound to a workflow-backed executor, but the process still owns the process run, step run, artifact expectations, and transition state.

## Direct Agent Execution Path

In `ProcessRunAutomationDispatchService.DispatchAsync`, direct AgentFramework execution follows this broad path:

```text
ExecuteUntilSettledAsync
  -> ProjectExecutionArtifactsAsync
  -> TryRecoverMissingCompletionArtifactsAsync
  -> TransitionStepWithClaimAsync
```

This path has the right general idea: process artifacts are projected and missing completion artifacts can trigger manager recovery before the process step transitions.

## Workflow-Backed Execution Path

The dispatcher also calls `workflowRunCoordinator.TryRunOrObserveAsync`. If the workflow outcome is handled, it calls `HandleWorkflowExecutionOutcomeAsync` and returns.

The observed `HandleWorkflowExecutionOutcomeAsync` implementation reloads the step snapshot and transitions the step according to the workflow outcome. It does not show the same process-owned artifact projection, artifact validation, missing-artifact recovery, or finalizer diagnostics used by the direct AgentFramework path.

This is the strongest verified boundary gap: workflow-backed process roles can complete or block through a different finalization path than direct agent roles.

## Current Artifact Projection Surface

`ProjectExecutionArtifactsAsync` projects artifacts from multiple sources:

- AgentFramework execution artifacts
- deterministic process mock artifacts
- session workspace file writes
- workspace file mutation receipts
- existing managed artifact files
- final assistant response text
- provider-native browser outputs
- auto decision artifacts for completed steps

This breadth is useful, but it makes artifact validity dependent on mode-specific rules. The runtime needs to know when a final response text is allowed, when a real file is required, when a browser screenshot is acceptable, and when a summary/decision artifact cannot satisfy an evidence or deliverable expectation.

## Current Recovery Surface

Manager recovery is already present and beneficial. It creates recovery decisions and rework packets, asks a process manager agent to recover missing artifacts from current run history and evidence, projects recovered artifacts, and blocks when required artifacts remain missing.

However, the current implementation still has risks:

- it relies on mutable `HashSet` state inside `DispatchCandidate` shared across candidate copies
- manager fallback may select a generic `lead`, `manager`, or `orchestrator` instead of an explicitly recovery-capable manager
- directive recording and recovery lifecycle states are not separated enough
- recovered artifacts are not structurally distinguished strongly enough from primary execution artifacts
- missing evidence can still become a process artifact record if the projection logic is too permissive elsewhere

## Current Test Coverage Strength

The integration tests already cover many valuable cases:

- dispatch state eligibility and stale active run handling
- dispatch lease heartbeat and claim loss
- workspace tool profile selection
- exact path artifact matching
- rejecting unrelated/generated product files as narrative evidence
- rejecting Playwright scratch artifacts
- rejecting provider-native browser snapshots as regression evidence packs
- matching imported browser screenshots
- project-structure governed artifact paths
- workspace file mutation receipt extraction
- prompt rules against unrelated side actions and confirmation asks

This suite should be extended rather than replaced.
