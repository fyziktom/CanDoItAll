# SB05: Boundary and regression scans

## Status
- Current status: Completed

## Objective
Ensure stabilization did not reintroduce architectural drift while fixing functional/runtime issues.

## Covered Inputs
- RN-004: Stabilize process functionality before further runtime extraction.

## Prerequisites
- SB04 closure gate must be green or must record an exact UI blocker.
- Boundary source references must exist.

## Exact Source References
- `repo://src/CanDoItAll.Processes.Core`
- `repo://src/CanDoItAll.Modules.Processes`
- `repo://src/CanDoItAll.Processes.Contracts`
- `repo://tests/CanDoItAll.Tests.Unit/ProcessDriverContractApiVerificationBoundaryTests.cs`

## Deliverables
- Process Core leakage scan.
- Driver/runtime-host effectful API scan.
- Scheduler/workflow direct driver hook scan.
- Boundary unit-test transcript.

## Dependency Impact
- SB06 may start only after boundary scans pass or name a concrete architectural blocker.
- Any boundary drift invalidates merge-ready classification.

## Validation Depth
- Entry gate: confirm SB04 proof and boundary source references.
- Closure gate: scan transcripts, boundary test transcript, anti-stub audit, and proof manifest.
- Semantic Adequacy Gate: record shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note closure in `bundle://proof/SB05/semantic-invariants.md`.

## Implementation Steps
- Scan Process Core for template/domain/runtime/driver/EF/UI/AgentFramework/OpenAI leakage.
- Scan driver/runtime-host paths for effectful APIs.
- Scan scheduler/workflow paths for direct driver hooks.
- Scan source/tests for unintended concrete bundle-path coupling.
- Run relevant boundary unit tests.

## Scope Exceptions
- None planned.

## Do Not Do
- Do not extract dispatcher/process runtime core into a new package.
- Do not create execution-capable drivers.
- Do not add reflection discovery, fallback selector, or driver self-registration.
- Do not weaken Process Core genericity.
- Do not create proof-heavy churn.

## Acceptance Checklist
- Process Core remains generic.
- No execution-capable host or driver registry added.
- No fallback selector/reflection discovery/self-registration.
- No hidden process mutation through drivers.

## Proof Required
- Source scans.
- Boundary unit tests.
- Anti-stub scan.
- `bundle://proof/SB05/manifest.md` with changed-file hashes and portable artifact references.
- `bundle://proof/SB05/semantic-invariants.md` with invariant IDs cited by transcripts.

## Browser Validation Logging
- N/A: SB05 has no browser-visible behavior.

## Progression Gate
- SB06 may start only after boundary scans pass.

## Suggested Agent Prompt
- Run boundary tests and source scans for Process Core leakage, execution-capable drivers, fallback selectors, reflection discovery, and hidden scheduler/workflow driver hooks.
