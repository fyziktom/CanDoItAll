# SB08: 08-workflow-evaluation-expected-output-proof

## Goal

Adopt or explicitly defer workflow expected-output evaluation.

## Required work

- Use MAF workflow expected output/ground truth if package exposes it.
- If not adopted, add a clear bridge test using CanDoItAll process/workflow assertions and mark MAF evaluator deferred.
- Ensure workflow-backed process steps produce mapped artifacts that pass process-owned validation.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB08` are updated and downstream subbundles can rely on it.

## Status

- Completed

## Objective

Keep workflow evaluation proof aligned with existing process assertions.

## Covered Inputs

- RQ06 workflow behavior after upgrade.

## Prerequisites

- Workflow source remains available and unchanged by artifact validation work.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs`

## Deliverables

- Workflow expected-output proof remains documented as process assertion based.

## Dependency Impact

- SB18 records workflow proof as a release gate boundary.

## Validation Depth

- Source inspection and existing workflow tests.

## Implementation Steps

- Inspect workflow compiler path.
- Avoid mixing workflow expected-output work into artifact status projection.

## Do Not Do

- Do not add test-only workflow output acceptance.

## Acceptance Checklist

- Workflow proof boundary is explicit.

## Proof Required

- Final report and existing workflow source references.

## Browser Validation Logging

- No browser route is affected.

## Progression Gate

- Workflow proof does not block artifact validation fixes.

## Suggested Agent Prompt

Confirm workflow proof boundaries and keep this bundle focused on runtime artifact correctness.
