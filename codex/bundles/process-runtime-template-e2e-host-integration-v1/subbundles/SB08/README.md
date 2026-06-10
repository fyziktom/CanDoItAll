# SB08: Release matrix, live proof classification, and final red-team

## Status
- Completed

## Objective
Release matrix, live proof classification, and final red-team.

## Covered Inputs
- inputs/00-original-request.md
- requirements/01-normalized-requirements.md
- analysis/01-real-code-review.md
- analysis/04-gap-analysis.md

## Prerequisites
- SB01-SB07 closure gates pass or carry explicit blockers that final closure honestly reports.
- All critical proof manifests and semantic invariant contracts exist for completed subbundles.
- Optional live OpenAI smoke variables are present before any live-provider proof is claimed.

## Exact Source References
- repo://CanDoItAll.slnx
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDryRunExecutionHost.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationRuntimeHost.cs
- repo://tests/CanDoItAll.Tests.Integration/LiveProcessRunOpenAiSmokeIntegrationTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs

## Scope

Run the final release matrix and close only if code-first ratio and real runtime proof pass.

Deliverables:
- build, unit, focused integration matrix;
- Playwright large-screen smoke if UI/project route proof is required;
- optional live OpenAI process-run smoke with explicit model/timeout/token budget;
- source scans for forbidden effects, Core leakage, selector fallback, reflection discovery, self-registration, secrets, bundle-path coupling;
- code-first ratio report with docs excluded from implementation ratio;
- final red-team outcome.


## Dependency Impact
- This is the final closure gate for the bundle. If validation fails, the bundle remains incomplete or explicitly blocked.

## Validation Depth
- Critical. Requires full release-matrix proof and final red-team review.
- Semantic adequacy proof must reject fake-proof fixtures, report-only proof, and optional live-smoke claims without opt-in variables.
- Browser proof is required only for UI-visible changes or route proof.

## Implementation Steps
- Run build, unit tests, focused integration matrix, and source scans.
- Run Playwright large-screen smoke only if UI/project/project-structure route proof is required.
- Classify live OpenAI smoke as run or skipped based strictly on explicit opt-in variables.
- Compute the final source/test/docs/bundle ratio and run the completed-stage bundle validator.

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
- `proof/SB08/manifest.md` with changed-file hashes, transcript paths, source assertions, anti-stub audit, final matrix, and red-team artifact.
- `proof/SB08/semantic-invariants.md` tying `REQ-008` to release-matrix and fake-proof resistance.
- Short execution-report row.
- For critical new production records/events, include a production behavior artifact matrix.

## Browser Validation Logging
- N/A unless UI routes/components are touched or route proof is required. If needed, use large desktop viewport only and record route, viewport, assertions, screenshot paths, and result.

## Progression Gate
- Close only after final release matrix, red-team review, raw-note closure, code-first ratio, and completed-stage validator pass.
- Reopen if proof is report-only, bundle-heavy, source/test changes are too small, or live proof is claimed without opt-in variables.

## Suggested Agent Prompt
Implement SB08 as a coherent code-first slice. Prefer larger source/test changes over proof scaffolding. Keep runtime-host execution future-gated and preserve generic Process Core boundaries.
