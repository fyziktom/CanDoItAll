# SB01: Baseline, real-code inventory, and code-first ratio guard

## Status
- Completed

## Objective
Baseline, real-code inventory, and code-first ratio guard.

## Covered Inputs
- inputs/00-original-request.md
- requirements/01-normalized-requirements.md
- analysis/01-real-code-review.md
- analysis/04-gap-analysis.md

## Prerequisites
- Prepared-stage bundle validator passes.
- Current branch and start SHA are recorded before implementation edits.
- No downstream subbundle has started from unverified baseline claims.

## Exact Source References
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDryRunExecutionPipeline.cs
- repo://src/CanDoItAll.Processes.Contracts/Runtime/ProcessRuntimeHostContractModels.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs

## Scope

Refresh branch baseline, diff stats, current code inventory, and code-first ratio guard. Do not implement feature code before the ratio policy and exact source inventory are in place.

Key references:
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDryRunExecutionPipeline.cs
- repo://src/CanDoItAll.Processes.Contracts/Runtime/ProcessRuntimeHostContractModels.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs

Deliverables:
- update or add tests that parse grouped numstat and reject proof-heavy closure;
- record current template/runtime-host inventory;
- record large-file baseline for touched files.


## Dependency Impact
- This subbundle gates SB02-SB08 because every later closure depends on current branch inventory and the code-first ratio baseline.
- If validation fails, downstream work is not trustworthy.

## Validation Depth
- Critical. Requires focused tests and source assertions.
- Semantic adequacy proof must reject a report-only ratio check that ignores real source/test churn.
- Browser proof is required only for UI-visible changes or route proof.

## Implementation Steps
- Record the current branch, start SHA, and clean or dirty worktree state.
- Inspect the ratio guard and runtime-host inventory tests against current source paths.
- Add or update focused tests that reject proof-heavy closure when source/test changes do not dominate bundle churn.
- Record large-file and boundary source scans for files touched by later process-runtime work.

## Do Not Do
- Do not create execution-capable drivers.
- Do not add reflection discovery, fallback selector, driver self-registration, or generic effectful runtime host.
- Do not mutate process state through drivers.
- Do not add domain-specific concepts into Process Core.
- Do not create large proof scaffolding or repeated boilerplate during execution.

## Acceptance Checklist
- Real source/test code changed unless this is an explicit inventory blocker.
- No effectful driver execution added.
- Process Core remains generic.
- Focused tests prove behavior.
- Source scans pass.
- Code-first ratio is not weakened.

## Proof Required
- Focused test transcript.
- Source scan transcript.
- `proof/SB01/manifest.md` with changed-file hashes, transcript paths, source assertions, and anti-stub audit.
- `proof/SB01/semantic-invariants.md` tying `REQ-001` to negative and positive ratio/inventory proof.
- Short execution-report row.
- For critical new production records/events, include a production behavior artifact matrix.

## Browser Validation Logging
- N/A unless UI routes/components are touched or route proof is required. If needed, use large desktop viewport only and record route, viewport, assertions, screenshot paths, and result.

## Progression Gate
- Proceed only after the acceptance checklist passes and the prepared-stage validator still passes after any bundle repair.
- Reopen if proof is report-only, bundle-heavy, or source/test changes are too small.

## Suggested Agent Prompt
Implement SB01 as a coherent code-first slice. Prefer larger source/test changes over proof scaffolding. Keep runtime-host execution future-gated and preserve generic Process Core boundaries.
