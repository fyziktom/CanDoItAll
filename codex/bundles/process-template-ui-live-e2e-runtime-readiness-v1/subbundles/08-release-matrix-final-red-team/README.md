# SB08: Release matrix + final red-team closure

## Status
- Status: Blocked

## Objective
Run final build/test/browser/source-scan matrix and decide whether process execution is restored enough for broader branch merge planning.

## Covered Inputs
- Raw request: determine whether process execution works again, identify what is missing, and prepare the next detailed bundle as a zip.
- REQ-008: run release matrix, live OpenAI opt-in classification, red-team scans, and final code-first ratio gate.

## Prerequisites
- SB01-SB07 closure gates have passed or have explicit blockers that final closure will classify.
- Required build, test, Playwright, and optional live OpenAI environment state is known.

## Exact Source References
- repo://src/CanDoItAll.Modules.Processes
- repo://src/CanDoItAll.Processes.Core
- repo://src/CanDoItAll.Processes.Contracts
- repo://tests/CanDoItAll.Tests.Integration
- repo://tests/CanDoItAll.Tests.Playwright

## Deliverables
- Build and full unit tests.
- Focused integration matrix for SB03-SB07.
- Large-screen Playwright proof from SB02.
- Optional live OpenAI process-template smoke if explicit opt-in env vars are present.
- Final source scans and file-size checks.
- Short decision document: “Processes restored / still blocked / merge-ready conditions”.

## Dependency Impact
- This subbundle decides whether the branch is closer to merge, still blocked, or needs follow-up work.
- Final bundle closure, raw-note closure, and zip preparation depend on this subbundle.

## Validation Depth
- Run build, full unit tests, focused integration matrix, Playwright proof, optional live classification, source scans, file-size checks, final ratio, and completed-stage bundle validator.
- Add final red-team fake-proof audit across all critical manifests and semantic invariant contracts.
- Include semantic adequacy proof, manifest, passing transcripts, anti-stub audit, source scans, final ratio, and verifier artifact under `proof/SB08/`.

## Implementation Steps
- Run the required release matrix and capture transcripts.
- Classify live OpenAI proof as run, skipped due to absent opt-in, or blocked with exact environment reason.
- Run source scans for Core drift, driver self-registration/reflection, fallback selector, mutation APIs, secret leakage, bundle-path coupling, and large-file growth.
- Calculate final code-first ratio and update root status, execution report, raw-note closure, proof manifests, and zip artifact.
- Run completed-stage validator and repair any proof defects before closure.

## Do Not Do
- Do not count skipped live OpenAI as live provider proof.
- Do not approve execution-capable drivers.
- Do not let bundle/proof files dominate source/test changes.

## Acceptance Checklist
- Build 0 errors.
- Unit tests green.
- Focused representative template automation green.
- Browser proof present for launch flow.
- Core remains generic.
- Final code-first ratio recorded as blocking under the conservative `HEAD` baseline.

## Proof Required
- Build transcript.
- Unit transcript.
- Focused integration transcript.
- Playwright transcript/screenshots.
- Source scans.
- Final ratio calculation.

## Browser Validation Logging
- Required: cite SB02 Playwright evidence and any SB06 run-detail/operator readback UI proof with route, viewport, screenshot paths, and result.

## Progression Gate
- The branch may be considered closer to merge only if user-facing launch, representative automation, and runtime-host readback are all green.
- User-facing launch, representative automation, and runtime-host readback are green; final bundle closure remains blocked by the code-first ratio gate.

## Suggested Agent Prompt
- Implement SB08 by running the release matrix, final red-team scans, live opt-in classification, final code-first ratio, completed-stage validator, raw-note closure, and zip creation. Do not mark the bundle complete unless all artifact-backed proof exists.
