# SB01: Baseline, real-code inventory, and code-first ratio guard

## Status
Prepared.

## Objective
Baseline, real-code inventory, and code-first ratio guard.

## Covered inputs
- inputs/00-original-request.md
- requirements/01-normalized-requirements.md
- analysis/01-real-code-review.md
- analysis/04-gap-analysis.md

## Exact source references
See body below. Add exact file paths during implementation if the inventory discovers renamed or moved sources.

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


## Dependency impact
This subbundle gates the next subbundle. If validation fails, downstream work is not trustworthy.

## Validation depth
Critical. Requires focused tests and source assertions. Browser proof is required only for UI-visible changes or route proof.

## Do Not Do
- Do not create execution-capable drivers.
- Do not add reflection discovery, fallback selector, driver self-registration, or generic effectful runtime host.
- Do not mutate process state through drivers.
- Do not add domain-specific concepts into Process Core.
- Do not create large proof scaffolding or repeated boilerplate during execution.

## Acceptance checklist
- Real source/test code changed unless this is an explicit inventory blocker.
- No effectful driver execution added.
- Process Core remains generic.
- Focused tests prove behavior.
- Source scans pass.
- Code-first ratio is not weakened.

## Proof required
- Focused test transcript.
- Source scan transcript.
- Short execution-report row.
- For critical new production records/events, include a production behavior artifact matrix.

## Browser validation logging
N/A unless UI routes/components are touched or route proof is required. If needed, use large desktop viewport only and record route, viewport, assertions, screenshot paths, and result.

## Progression gate
Proceed only after acceptance checklist passes. Reopen if proof is report-only, bundle-heavy, or source/test changes are too small.

## Suggested agent prompt
Implement SB01 as a coherent code-first slice. Prefer larger source/test changes over proof scaffolding. Keep runtime-host execution future-gated and preserve generic Process Core boundaries.
