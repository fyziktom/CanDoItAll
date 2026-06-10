# SB03: Software/Blazor/.NET process execution E2E

## Status
- Completed

## Objective
Software/Blazor/.NET process execution E2E.

## Covered Inputs
- inputs/00-original-request.md
- requirements/01-normalized-requirements.md
- analysis/01-real-code-review.md
- analysis/04-gap-analysis.md

## Prerequisites
- SB02 closure gate passes with exact software/Blazor/.NET template keys.
- Multi-team development status is resolved or explicitly blocked.
- No runtime-host execution-capable driver work has been approved.

## Exact Source References
- repo://src/CanDoItAll.Modules.Processes/Launch/ProcessLaunchPlanDisplayProjector.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs
- repo://src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureProcessRunSyncBridge.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessLaunchPlanningIntegrationTests.cs

## Scope

Execute a representative software-development template through real process services from project context.

Deliverables:
- import/publish/start run;
- outbox/dispatch/finalizer path;
- required artifacts and managed content readback;
- run detail/read model proof;
- project/project-structure launch coverage where applicable;
- deterministic fallback proof and optional live provider proof only if opted in.


## Dependency Impact
- This subbundle gates SB04 and SB05 because manager readback and genericity proof require at least one real software process run.
- If validation fails, downstream work is not trustworthy.

## Validation Depth
- Critical. Requires focused tests and source assertions.
- Semantic adequacy proof must reject tests that only create a launch plan without dispatch, finalizer, artifacts, and readback.
- Browser proof is required only for UI-visible changes or route proof.

## Implementation Steps
- Launch a representative software/Blazor/.NET template from project or project-structure context in tests.
- Prove persisted run, outbox, dispatch claim, finalizer, artifacts, and run-detail/read-model availability.
- Add deterministic fallback proof and classify live provider proof as skipped unless explicit opt-in variables are present.
- Keep runtime-host execution-capable drivers blocked.

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
- `proof/SB03/manifest.md` with changed-file hashes, transcript paths, source assertions, and anti-stub audit.
- `proof/SB03/semantic-invariants.md` tying `REQ-003` to real execution proof.
- Short execution-report row.
- For critical new production records/events, include a production behavior artifact matrix.

## Browser Validation Logging
- N/A unless UI routes/components are touched or route proof is required. If needed, use large desktop viewport only and record route, viewport, assertions, screenshot paths, and result.

## Progression Gate
- Proceed only after the software/Blazor/.NET process path reaches artifacts and readback through runtime services.
- Reopen if proof is report-only, bundle-heavy, or source/test changes are too small.

## Suggested Agent Prompt
Implement SB03 as a coherent code-first slice. Prefer larger source/test changes over proof scaffolding. Keep runtime-host execution future-gated and preserve generic Process Core boundaries.
